using Fasx.Agent.Core.Sessions;

namespace Fasx.Agent.Core.Security;

/// <summary>
/// Centralizes the rules for checking a local user's session approval.
/// A remote peer cannot widen a grant by sending a different permission mask.
/// </summary>
public static class SessionPermissionPolicy
{
    public const SessionPermission AllKnownPermissions =
        SessionPermission.ViewScreen |
        SessionPermission.ControlInput |
        SessionPermission.TransferFiles |
        SessionPermission.ShareClipboard |
        SessionPermission.ReadDiagnostics |
        SessionPermission.PlayRemoteAudio |
        SessionPermission.RunPowerActions |
        SessionPermission.ControlPeripherals;

    public static bool IsPermissionSetValid(SessionPermission permissions) =>
        permissions != SessionPermission.None &&
        (permissions & ~AllKnownPermissions) == SessionPermission.None;

    public static bool IsApprovalValid(
        SessionRequest request,
        SessionApproval approval,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(approval);

        if (request.SessionId == Guid.Empty ||
            request.DeviceId == Guid.Empty ||
            approval.SessionId != request.SessionId ||
            !IsPermissionSetValid(request.RequestedPermissions) ||
            !IsPermissionSetValid(approval.ApprovedPermissions) ||
            !approval.ApprovedByLocalUser ||
            approval.ApprovedAtUtc >= request.ExpiresAtUtc ||
            approval.ApprovedAtUtc > nowUtc ||
            approval.ExpiresAtUtc <= approval.ApprovedAtUtc ||
            approval.ExpiresAtUtc <= nowUtc)
        {
            return false;
        }

        // A local approval may grant fewer permissions than requested, never more.
        // The request expiry is only the deadline to approve; it does not end an
        // already-approved session. The approval lease controls session expiry.
        var permissionsNotRequested = approval.ApprovedPermissions & ~request.RequestedPermissions;
        return permissionsNotRequested == SessionPermission.None;
    }

    public static bool HasPermission(
        SessionRequest request,
        SessionApproval approval,
        SessionPermission permission,
        DateTimeOffset nowUtc)
    {
        if (!IsPermissionSetValid(permission) ||
            !IsApprovalValid(request, approval, nowUtc))
        {
            return false;
        }

        return (approval.ApprovedPermissions & permission) == permission;
    }
}
