using Fasx.Agent.Core.Security;

namespace Fasx.Agent.Core.Sessions;

/// <summary>
/// A short-lived request to start a session with a specific set of capabilities.
/// This is a domain contract only; transport and authentication are implemented separately.
/// </summary>
public sealed record SessionRequest(
    Guid SessionId,
    Guid DeviceId,
    SessionPermission RequestedPermissions,
    DateTimeOffset ExpiresAtUtc);
