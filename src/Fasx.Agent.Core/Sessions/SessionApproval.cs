using Fasx.Agent.Core.Security;

namespace Fasx.Agent.Core.Sessions;

/// <summary>
/// Represents a local user's approval for a subset of the requested permissions.
/// The trusted Windows UI must create this object only after explicit local approval.
/// </summary>
public sealed record SessionApproval(
    Guid SessionId,
    SessionPermission ApprovedPermissions,
    DateTimeOffset ApprovedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool ApprovedByLocalUser);
