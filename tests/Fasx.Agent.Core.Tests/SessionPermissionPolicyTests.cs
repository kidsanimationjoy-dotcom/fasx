using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;
using Xunit;

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
            now.AddMinutes(2));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(10),
            ApprovedByLocalUser: true);

        Assert.True(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
        Assert.True(SessionPermissionPolicy.HasPermission(request, approval, SessionPermission.ViewScreen, now));
        Assert.False(SessionPermissionPolicy.HasPermission(request, approval, SessionPermission.ControlInput, now));
    }

    [Fact]
    public void ApprovedSessionRemainsValidAfterRequestDeadline()
    {
        var approvedAt = DateTimeOffset.UtcNow;
        var request = new SessionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionPermission.ViewScreen,
            approvedAt.AddMinutes(2));
        var approval = new SessionApproval(
            request.SessionId,
            SessionPermission.ViewScreen,
            approvedAt,
            approvedAt.AddMinutes(10),
            ApprovedByLocalUser: true);

        Assert.True(SessionPermissionPolicy.IsApprovalValid(
            request,
            approval,
            approvedAt.AddMinutes(5)));
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
            now.AddMinutes(2));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen | SessionPermission.ControlInput,
            now,
            now.AddMinutes(5),
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
            now.AddMinutes(2));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(5),
            ApprovedByLocalUser: false);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
    }

    [Fact]
    public void ApprovalIsRejectedAfterSessionLeaseExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var request = new SessionRequest(
            sessionId,
            Guid.NewGuid(),
            SessionPermission.ViewScreen,
            now.AddMinutes(2));
        var approval = new SessionApproval(
            sessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(5),
            ApprovedByLocalUser: true);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now.AddMinutes(5)));
    }

    [Fact]
    public void ApprovalIsRejectedWhenGrantedAfterRequestDeadline()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new SessionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionPermission.ViewScreen,
            now.AddMinutes(-1));
        var approval = new SessionApproval(
            request.SessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(5),
            ApprovedByLocalUser: true);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
    }

    [Fact]
    public void UnknownPermissionBitsAreRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new SessionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionPermission.ViewScreen | (SessionPermission)(1 << 20),
            now.AddMinutes(2));
        var approval = new SessionApproval(
            request.SessionId,
            SessionPermission.ViewScreen,
            now,
            now.AddMinutes(5),
            ApprovedByLocalUser: true);

        Assert.False(SessionPermissionPolicy.IsApprovalValid(request, approval, now));
        Assert.False(SessionPermissionPolicy.HasPermission(
            request,
            approval,
            (SessionPermission)(1 << 20),
            now));
    }
}
