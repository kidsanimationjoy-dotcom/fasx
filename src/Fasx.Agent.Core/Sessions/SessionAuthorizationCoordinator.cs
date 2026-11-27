using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;

namespace Fasx.Agent.Core.Sessions;

/// <summary>
/// In-process authorization gate for remote sessions.
/// The transport must first authenticate the paired device's signature and pass
/// its authenticated device ID here. Approval and revocation are local-only
/// operations and must never be exposed as network endpoints.
/// </summary>
public sealed class SessionAuthorizationCoordinator
{
    private static readonly TimeSpan MaximumRequestLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinimumApprovalLease = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumApprovalLease = TimeSpan.FromMinutes(30);

    private readonly object _sync = new();
    private readonly Func<Guid, bool> _isTrustedDevice;
    private readonly Dictionary<Guid, SessionRecord> _sessions = [];

    public SessionAuthorizationCoordinator(Func<Guid, bool> isTrustedDevice)
    {
        _isTrustedDevice = isTrustedDevice
            ?? throw new ArgumentNullException(nameof(isTrustedDevice));
    }

    /// <summary>
    /// Submit a request only after the caller has verified the paired device's
    /// challenge signature. authenticatedDeviceId must come from that verified
    /// identity, not from an untrusted request body.
    /// </summary>
    public bool SubmitAuthenticatedRequest(
        SessionRequest request,
        Guid authenticatedDeviceId,
        DateTimeOffset nowUtc,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(request);
        error = null;

        if (authenticatedDeviceId == Guid.Empty ||
            request.DeviceId != authenticatedDeviceId)
        {
            error = "The authenticated device does not match the session request.";
            return false;
        }

        if (!_isTrustedDevice(authenticatedDeviceId))
        {
            error = "This device is not paired with this computer.";
            return false;
        }

        if (request.SessionId == Guid.Empty ||
            !SessionPermissionPolicy.IsPermissionSetValid(request.RequestedPermissions))
        {
            error = "The session request contains an invalid identifier or permission set.";
            return false;
        }

        if (request.ExpiresAtUtc <= nowUtc ||
            request.ExpiresAtUtc > nowUtc.Add(MaximumRequestLifetime))
        {
            error = "The session request has expired or is valid for too long.";
            return false;
        }

        lock (_sync)
        {
            UpdateExpiredSessions(nowUtc);

            if (_sessions.ContainsKey(request.SessionId))
            {
                error = "This session identifier has already been used.";
                return false;
            }

            var deviceHasOpenSession = _sessions.Values.Any(record =>
                record.Request.DeviceId == authenticatedDeviceId &&
                record.Status is SessionStatus.AwaitingLocalApproval or SessionStatus.Approved);

            if (deviceHasOpenSession)
            {
                error = "This device already has a pending or active session.";
                return false;
            }

            _sessions.Add(request.SessionId, new SessionRecord(request));
            return true;
        }
    }

    public IReadOnlyList<SessionRequest> GetPendingRequests(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            UpdateExpiredSessions(nowUtc);
            return _sessions.Values
                .Where(record => record.Status == SessionStatus.AwaitingLocalApproval)
                .Select(record => record.Request)
                .OrderBy(record => record.ExpiresAtUtc)
                .ToArray();
        }
    }

    /// <summary>Grant only the selected subset after an explicit local user action.</summary>
    public SessionApproval ApproveLocally(
        Guid sessionId,
        SessionPermission approvedPermissions,
        DateTimeOffset nowUtc,
        TimeSpan approvalLease)
    {
        if (approvalLease < MinimumApprovalLease || approvalLease > MaximumApprovalLease)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvalLease),
                $"Approval lease must be between {MinimumApprovalLease} and {MaximumApprovalLease}.");
        }

        if (!SessionPermissionPolicy.IsPermissionSetValid(approvedPermissions))
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvedPermissions),
                "At least one known permission must be approved.");
        }

        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var record) ||
                record.Status != SessionStatus.AwaitingLocalApproval)
            {
                throw new InvalidOperationException("Session request is not awaiting local approval.");
            }

            if (record.Request.ExpiresAtUtc <= nowUtc)
            {
                record.Status = SessionStatus.Expired;
                throw new InvalidOperationException("Session request expired before local approval.");
            }

            if ((approvedPermissions & ~record.Request.RequestedPermissions) != SessionPermission.None)
            {
                throw new InvalidOperationException("A session cannot be granted permissions it did not request.");
            }

            var approval = new SessionApproval(
                sessionId,
                approvedPermissions,
                nowUtc,
                nowUtc.Add(approvalLease),
                ApprovedByLocalUser: true);

            record.Approval = approval;
            record.Status = SessionStatus.Approved;
            return approval;
        }
    }

    public void DenyLocally(Guid sessionId)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var record) ||
                record.Status != SessionStatus.AwaitingLocalApproval)
            {
                throw new InvalidOperationException("Session request is not awaiting local approval.");
            }

            record.Status = SessionStatus.Denied;
        }
    }

    public bool RevokeLocally(Guid sessionId)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var record) ||
                record.Status != SessionStatus.Approved)
            {
                return false;
            }

            record.Status = SessionStatus.Revoked;
            record.Approval = null;
            return true;
        }
    }

    /// <summary>Stop a pending or approved session from the local Windows UI.</summary>
    public bool CancelLocally(Guid sessionId)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(sessionId, out var record) ||
                record.Status is not (SessionStatus.AwaitingLocalApproval or SessionStatus.Approved))
            {
                return false;
            }

            record.Status = SessionStatus.Cancelled;
            record.Approval = null;
            return true;
        }
    }

    public SessionApproval? GetActiveApproval(Guid sessionId, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            UpdateExpiredSessions(nowUtc);

            if (!_sessions.TryGetValue(sessionId, out var record) ||
                record.Status != SessionStatus.Approved ||
                record.Approval is null ||
                !SessionPermissionPolicy.IsApprovalValid(record.Request, record.Approval, nowUtc))
            {
                return null;
            }

            return record.Approval;
        }
    }

    public bool HasPermission(
        Guid sessionId,
        SessionPermission permission,
        DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            UpdateExpiredSessions(nowUtc);

            return _sessions.TryGetValue(sessionId, out var record) &&
                   record.Status == SessionStatus.Approved &&
                   record.Approval is not null &&
                   SessionPermissionPolicy.HasPermission(
                       record.Request,
                       record.Approval,
                       permission,
                       nowUtc);
        }
    }

    public SessionStatus GetStatus(Guid sessionId, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            UpdateExpiredSessions(nowUtc);
            return _sessions.TryGetValue(sessionId, out var record)
                ? record.Status
                : SessionStatus.NotFound;
        }
    }

    private void UpdateExpiredSessions(DateTimeOffset nowUtc)
    {
        foreach (var record in _sessions.Values)
        {
            if (record.Status == SessionStatus.AwaitingLocalApproval &&
                record.Request.ExpiresAtUtc <= nowUtc)
            {
                record.Status = SessionStatus.Expired;
            }
            else if (record.Status == SessionStatus.Approved &&
                     (record.Approval is null || record.Approval.ExpiresAtUtc <= nowUtc))
            {
                record.Status = SessionStatus.Expired;
                record.Approval = null;
            }
        }
    }

    private sealed class SessionRecord(SessionRequest request)
    {
        public SessionRequest Request { get; } = request;
        public SessionApproval? Approval { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.AwaitingLocalApproval;
    }
}

public enum SessionStatus
{
    NotFound,
    AwaitingLocalApproval,
    Approved,
    Denied,
    Expired,
    Revoked,
    Cancelled
}
