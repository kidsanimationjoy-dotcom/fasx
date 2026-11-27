using Fasx.Agent.Core.Sessions;

namespace Fasx.Agent.Core.Security;

/// <summary>
/// Centralizes the rules for checking a local user's session approval.
/// A remote peer cannot widen a grant by sending a different permission mask.
/// </summary>
public static class SessionPermissionPolicy
{
    public static bool IsApprovalValid(
        SessionRequest request,
        SessionApproval approval,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(approval);

        if (!approval.ApprovedByLocalUser ||
            approval.SessionId != request.SessionId ||
            request.ExpiresAtUtc <= nowUtc ||
            approval.ExpiresAtUtc <= nowUtc ||
            approval.ApprovedAtUtc > nowUtc ||
            approval.ApprovedPermissions == SessionPermission.None)
        {
            return false;
        }

        // A local approval may grant fewer permissions than requested, never more.
        var permissionsNotRequested = approval.ApprovedPermissions & ~request.RequestedPermissions;
        return permissionsNotRequested == SessionPermission.None;
    }

    public static bool HasPermission(
        SessionRequest request,
        SessionApproval approval,
        SessionPermission permission,
        DateTimeOffset nowUtc)
    {
        if (permission == SessionPermission.None || !IsApprovalValid(request, approval, nowUtc))
        {
            return false;
        }

        return (approval.ApprovedPermissions & permission) == permission;
    }
}
