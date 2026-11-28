using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class SessionAuthorizationCoordinatorTests
{
    [Fact]
    public void OnlyTrustedAuthenticatedDeviceCanSubmitSessionRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var trustedDeviceId = Guid.NewGuid();
        var untrustedDeviceId = Guid.NewGuid();
        var coordinator = new SessionAuthorizationCoordinator(id => id == trustedDeviceId);

        var accepted = coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, trustedDeviceId),
            trustedDeviceId,
            now,
            out var acceptedError);
        var rejected = coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, untrustedDeviceId),
            untrustedDeviceId,
            now,
            out var rejectedError);

        Assert.True(accepted, acceptedError);
        Assert.False(rejected);
        Assert.Equal("This device is not paired with this computer.", rejectedError);
    }

    [Fact]
    public void RequestDeviceMustMatchAuthenticatedDevice()
    {
        var now = DateTimeOffset.UtcNow;
        var trustedDeviceId = Guid.NewGuid();
        var otherDeviceId = Guid.NewGuid();
        var coordinator = new SessionAuthorizationCoordinator(id => id == trustedDeviceId);

        var accepted = coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, otherDeviceId),
            trustedDeviceId,
            now,
            out var error);

        Assert.False(accepted);
        Assert.Equal("The authenticated device does not match the session request.", error);
    }

    [Fact]
    public void ExpiredOrOverlongRequestIsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var coordinator = new SessionAuthorizationCoordinator(_ => true);

        Assert.False(coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, deviceId, expiresAt: now),
            deviceId,
            now,
            out _));

        Assert.False(coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, deviceId, expiresAt: now.AddMinutes(3)),
            deviceId,
            now,
            out _));
    }

    [Fact]
    public void RequestPermissionCannotBeEmptyOrContainUnknownBits()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var coordinator = new SessionAuthorizationCoordinator(_ => true);

        Assert.False(coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, deviceId, permissions: SessionPermission.None),
            deviceId,
            now,
            out _));

        Assert.False(coordinator.SubmitAuthenticatedRequest(
            CreateRequest(now, deviceId, permissions: SessionPermission.ViewScreen | (SessionPermission)(1 << 20)),
            deviceId,
            now,
            out _));
    }

    [Fact]
    public void LocalApprovalCanGrantOnlyRequestedSubset()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var request = CreateRequest(
            now,
            deviceId,
            SessionPermission.ViewScreen | SessionPermission.ControlInput);
        var coordinator = new SessionAuthorizationCoordinator(_ => true);

        Assert.True(coordinator.SubmitAuthenticatedRequest(request, deviceId, now, out var error), error);
        var approval = coordinator.ApproveLocally(
            request.SessionId,
            SessionPermission.ViewScreen,
            now.AddSeconds(1),
            TimeSpan.FromMinutes(10));

        Assert.Equal(SessionPermission.ViewScreen, approval.ApprovedPermissions);
        Assert.True(coordinator.HasPermission(request.SessionId, SessionPermission.ViewScreen, now.AddSeconds(2)));
        Assert.False(coordinator.HasPermission(request.SessionId, SessionPermission.ControlInput, now.AddSeconds(2)));
        Assert.Equal(SessionStatus.Approved, coordinator.GetStatus(request.SessionId, now.AddSeconds(2)));
    }

    [Fact]
    public void LocalApprovalCannotGrantPermissionNotRequested()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var request = CreateRequest(now, deviceId, SessionPermission.ViewScreen);
        var coordinator = new SessionAuthorizationCoordinator(_ => true);

        Assert.True(coordinator.SubmitAuthenticatedRequest(request, deviceId, now, out _));

        Assert.Throws<InvalidOperationException>(() =>
            coordinator.ApproveLocally(
                request.SessionId,
                SessionPermission.ViewScreen | SessionPermission.ControlInput,
                now.AddSeconds(1),
                TimeSpan.FromMinutes(5)));
        Assert.Equal(SessionStatus.AwaitingLocalApproval, coordinator.GetStatus(request.SessionId, now));
    }

    [Fact]
    public void ApprovedSessionOutlivesRequestDeadlineButNotItsApprovalLease()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var request = CreateRequest(now, deviceId);
        var coordinator = new SessionAuthorizationCoordinator(_ => true);

        Assert.True(coordinator.SubmitAuthenticatedRequest(request, deviceId, now, out _));
        coordinator.ApproveLocally(
            request.SessionId,
            SessionPermission.ViewScreen,
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5));

        Assert.Equal(SessionStatus.Approved, coordinator.GetStatus(request.SessionId, now.AddMinutes(1)));
        Assert.True(coordinator.HasPermission(
            request.SessionId,
            SessionPermission.ViewScreen,
            now.AddMinutes(1)));

        Assert.Equal(SessionStatus.Expired, coordinator.GetStatus(request.SessionId, now.AddMinutes(6)));
        Assert.False(coordinator.HasPermission(
            request.SessionId,
            SessionPermission.ViewScreen,
            now.AddMinutes(6)));
    }

    [Fact]
    public void PendingRequestExpiresWithoutLocalApproval()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var request = CreateRequest(now, deviceId, expiresAt: now.AddSeconds(10));
        var coordinator = new SessionAuthorizationCoordinator(_ => true);

        Assert.True(coordinator.SubmitAuthenticatedRequest(request, deviceId, now, out _));
        Assert.Empty(coordinator.GetPendingRequests(now.AddSeconds(11)));
        Assert.Equal(SessionStatus.Expired, coordinator.GetStatus(request.SessionId, now.AddSeconds(11)));
    }

    [Fact]
    public void LocalDenialAndRevocationAreTerminal()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var coordinator = new SessionAuthorizationCoordinator(_ => true);
        var deniedRequest = CreateRequest(now, deviceId);

        Assert.True(coordinator.SubmitAuthenticatedRequest(deniedRequest, deviceId, now, out _));
        coordinator.DenyLocally(deniedRequest.SessionId);
        Assert.Equal(SessionStatus.Denied, coordinator.GetStatus(deniedRequest.SessionId, now));
        Assert.False(coordinator.RevokeLocally(deniedRequest.SessionId));

        var approvedRequest = CreateRequest(now, deviceId);
        Assert.True(coordinator.SubmitAuthenticatedRequest(approvedRequest, deviceId, now, out _));
        coordinator.ApproveLocally(
            approvedRequest.SessionId,
            SessionPermission.ViewScreen,
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5));

        Assert.True(coordinator.RevokeLocally(approvedRequest.SessionId));
        Assert.False(coordinator.RevokeLocally(approvedRequest.SessionId));
        Assert.Null(coordinator.GetActiveApproval(approvedRequest.SessionId, now.AddSeconds(2)));
        Assert.Equal(SessionStatus.Revoked, coordinator.GetStatus(approvedRequest.SessionId, now.AddSeconds(2)));
    }

    [Fact]
    public void OneDeviceCannotOpenTwoPendingOrActiveSessionsAtOnce()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var coordinator = new SessionAuthorizationCoordinator(_ => true);
        var first = CreateRequest(now, deviceId);
        var second = CreateRequest(now, deviceId);

        Assert.True(coordinator.SubmitAuthenticatedRequest(first, deviceId, now, out _));
        Assert.False(coordinator.SubmitAuthenticatedRequest(second, deviceId, now, out var error));
        Assert.Equal("This device already has a pending or active session.", error);
    }

    [Fact]
    public void ApprovalLeaseMustBeWithinSafeBounds()
    {
        var now = DateTimeOffset.UtcNow;
        var deviceId = Guid.NewGuid();
        var request = CreateRequest(now, deviceId);
        var coordinator = new SessionAuthorizationCoordinator(_ => true);
        Assert.True(coordinator.SubmitAuthenticatedRequest(request, deviceId, now, out _));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinator.ApproveLocally(
                request.SessionId,
                SessionPermission.ViewScreen,
                now.AddSeconds(1),
                TimeSpan.FromSeconds(10)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            coordinator.ApproveLocally(
                request.SessionId,
                SessionPermission.ViewScreen,
                now.AddSeconds(1),
                TimeSpan.FromHours(1)));
    }

    private static SessionRequest CreateRequest(
        DateTimeOffset now,
        Guid deviceId,
        SessionPermission permissions = SessionPermission.ViewScreen,
        DateTimeOffset? expiresAt = null) =>
        new(
            Guid.NewGuid(),
            deviceId,
            permissions,
            expiresAt ?? now.AddMinutes(2));
}
