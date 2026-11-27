using Xunit;
using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;

namespace Fasx.Agent.Core.Tests;

public sealed class SessionPermissionPolicyTests
{
    [Fact]
    public void ApprovalIsValidWhenLocalUserApprovesRequestedSubset()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var request = new SessionRequest(
            sessionId,
            Guid.NewGuid(),
            SessionPermission.ViewScreen | SessionPermission.ControlInput,
            now.AddMinutes(5));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(2),
            ApprovedByLocalUser: true);

        Assert.True(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
        Assert.True(SessionPermissionPolicy.HasPermission(request, approval, SessionPermission.ViewScreen, now));
        Assert.False(SessionPermissionPolicy.HasPermission(request, approval, SessionPermission.ControlInput, now));
    }

    [Fact]
    public void ApprovalIsRejectedWhenItGrantsUnrequestedPermission()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var request = new SessionRequest(
            sessionId,
            Guid.NewGuid(),
            SessionPermission.ViewScreen,
            now.AddMinutes(5));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen | SessionPermission.ControlInput,
            now,
            now.AddMinutes(2),
            ApprovedByLocalUser: true);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
    }

    [Fact]
    public void ApprovalIsRejectedWhenLocalApprovalIsMissing()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var request = new SessionRequest(
            sessionId,
            Guid.NewGuid(),
            SessionPermission.ViewScreen,
            now.AddMinutes(5));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(2),
            ApprovedByLocalUser: false);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
    }

    [Fact]
    public void ApprovalIsRejectedAfterExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var request = new SessionRequest(
            sessionId,
            Guid.NewGuid(),
            SessionPermission.ViewScreen,
            now.AddMinutes(-1));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen,
            now.AddMinutes(-4),
            now.AddMinutes(-2),
            ApprovedByLocalUser: true);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
    }
}
