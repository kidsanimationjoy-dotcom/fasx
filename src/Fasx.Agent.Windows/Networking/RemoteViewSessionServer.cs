using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.RateLimiting;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fasx.Agent.Windows.Networking;

/// <summary>
/// Temporary HTTPS endpoint for requesting a view-only session from one already-paired phone.
/// It authenticates the phone key and creates a local-approval request; it does not start capture.
/// </summary>
public sealed class RemoteViewSessionServer : IAsyncDisposable
{
    public const int Port = 47834;
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplication _app;
    private readonly X509Certificate2 _certificate;
    private readonly object _sync = new();
    private readonly PairingCoordinator _pairingCoordinator;
    private readonly SessionAuthorizationCoordinator _authorization;
    private readonly PairedDevice _targetDevice;
    private ViewSessionInvitation? _invitation;
    private byte[]? _secretHash;
    private bool _claimConsumed;
    private bool _disposed;

    private RemoteViewSessionServer(
        WebApplication app,
        X509Certificate2 certificate,
        IPAddress hostAddress,
        string tlsPin,
        PairingCoordinator pairingCoordinator,
        SessionAuthorizationCoordinator authorization,
        PairedDevice targetDevice)
    {
        _app = app;
        _certificate = certificate;
        HostAddress = hostAddress;
        TlsPin = tlsPin;
        _pairingCoordinator = pairingCoordinator;
        _authorization = authorization;
        _targetDevice = targetDevice;
    }

    public IPAddress HostAddress { get; }
    public string TlsPin { get; }
    public Uri RequestEndpoint => new($"https://{HostAddress}:{Port}/api/view/request");

    public static async Task<RemoteViewSessionServer> StartAsync(
        PairingCoordinator pairingCoordinator,
        SessionAuthorizationCoordinator authorization,
        PairedDevice targetDevice,
        IPAddress hostAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pairingCoordinator);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(targetDevice);
        ArgumentNullException.ThrowIfNull(hostAddress);

        if (!IsPrivateIpv4(hostAddress))
        {
            throw new ArgumentException("A private RFC1918 IPv4 network address is required.", nameof(hostAddress));
        }

        if (!pairingCoordinator.GetPairedDevices().Any(device =>
            device.DeviceId == targetDevice.DeviceId &&
            device.PublicKeySpki == targetDevice.PublicKeySpki))
        {
            throw new InvalidOperationException("The selected phone is no longer a trusted paired device.");
        }

        var certificate = CreateHostCertificate(hostAddress);
        var tlsPin = CreateSpkiPin(certificate);
        WebApplication? app = null;
        RemoteViewSessionServer? server = null;

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Production",
                ApplicationName = typeof(RemoteViewSessionServer).Assembly.FullName
            });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 16 * 1024;
                options.Listen(hostAddress, Port, listenOptions => listenOptions.UseHttps(certificate));
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("view-request", limiter =>
                {
                    limiter.PermitLimit = 8;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.AutoReplenishment = true;
                });
                options.AddFixedWindowLimiter("view-status", limiter =>
                {
                    limiter.PermitLimit = 180;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.AutoReplenishment = true;
                });
            });

            app = builder.Build();
            server = new RemoteViewSessionServer(
                app,
                certificate,
                hostAddress,
                tlsPin,
                pairingCoordinator,
                authorization,
                targetDevice);

            app.Use(async (context, next) =>
            {
                context.Response.Headers["Cache-Control"] = "no-store";
                await next();
            });
            app.UseRateLimiter();

            app.MapPost("/api/view/request", async (HttpContext context) =>
            {
                ViewSessionClaim? claim;
                try
                {
                    claim = await context.Request.ReadFromJsonAsync<ViewSessionClaim>(
                        JsonOptions,
                        context.RequestAborted);
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "The view-session request JSON is invalid." });
                }
                catch (InvalidOperationException)
                {
                    return Results.BadRequest(new { error = "The view-session request must use application/json." });
                }

                if (claim is null)
                {
                    return Results.BadRequest(new { error = "A view-session request is required." });
                }

                var result = server.AcceptClaim(claim, DateTimeOffset.UtcNow);
                if (!result.Accepted)
                {
                    return Results.BadRequest(new { error = result.Error });
                }

                return Results.Accepted(
                    $"/api/view/status/{claim.SessionId:D}",
                    new { status = SessionStatus.AwaitingLocalApproval.ToString(), permission = "ViewScreen" });
            }).RequireRateLimiting("view-request");

            app.MapGet("/api/view/status/{sessionId:guid}", (Guid sessionId) =>
            {
                if (!server.IsKnownSession(sessionId))
                {
                    return Results.NotFound(new { error = "View-session request was not found." });
                }

                var status = authorization.GetStatus(sessionId, DateTimeOffset.UtcNow);
                return Results.Json(new { status = status.ToString(), permission = "ViewScreen" }, options: JsonOptions);
            }).RequireRateLimiting("view-status");

            await app.StartAsync(cancellationToken);
            return server;
        }
        catch
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }
            certificate.Dispose();
            throw;
        }
    }

    public ViewSessionInvitation CreateInvitation(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RemoteViewSessionServer));
            }

            if (_invitation is not null && nowUtc < _invitation.ExpiresAtUtc && !_claimConsumed)
            {
                throw new InvalidOperationException("A view invitation is already active.");
            }

            ClearInvitationSecret();
            var secretBytes = RandomNumberGenerator.GetBytes(32);
            var nonceBytes = RandomNumberGenerator.GetBytes(32);
            var invitation = new ViewSessionInvitation(
                Guid.NewGuid(),
                Base64Url.Encode(secretBytes),
                Base64Url.Encode(nonceBytes),
                nowUtc.Add(InvitationLifetime));

            _secretHash = SHA256.HashData(secretBytes);
            _invitation = invitation;
            _claimConsumed = false;

            CryptographicOperations.ZeroMemory(secretBytes);
            CryptographicOperations.ZeroMemory(nonceBytes);
            return invitation;
        }
    }

    public bool IsKnownSession(Guid sessionId)
    {
        lock (_sync)
        {
            return _invitation?.SessionId == sessionId && _claimConsumed;
        }
    }

    private ClaimAcceptanceResult AcceptClaim(ViewSessionClaim claim, DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (_disposed || _invitation is null || _secretHash is null)
            {
                return ClaimAcceptanceResult.Failure("There is no active view invitation.");
            }

            if (_claimConsumed)
            {
                return ClaimAcceptanceResult.Failure("This view invitation has already been used.");
            }

            if (nowUtc >= _invitation.ExpiresAtUtc)
            {
                ClearInvitationSecret();
                return ClaimAcceptanceResult.Failure("This view invitation has expired.");
            }

            if (claim.SessionId != _invitation.SessionId ||
                claim.DeviceId != _targetDevice.DeviceId ||
                !string.Equals(claim.Nonce, _invitation.Nonce, StringComparison.Ordinal))
            {
                return ClaimAcceptanceResult.Failure("The device or session challenge does not match this invitation.");
            }

            if (!Base64Url.TryDecode(claim.Secret, out var suppliedSecret) || suppliedSecret.Length != 32)
            {
                CryptographicOperations.ZeroMemory(suppliedSecret);
                return ClaimAcceptanceResult.Failure("The view invitation secret is invalid.");
            }

            var suppliedHash = SHA256.HashData(suppliedSecret);
            var matches = CryptographicOperations.FixedTimeEquals(_secretHash, suppliedHash);
            CryptographicOperations.ZeroMemory(suppliedSecret);
            CryptographicOperations.ZeroMemory(suppliedHash);

            if (!matches)
            {
                return ClaimAcceptanceResult.Failure("The view invitation secret is incorrect.");
            }

            var proof = new SessionDeviceProof(
                claim.SessionId,
                claim.DeviceId,
                claim.Nonce,
                claim.Signature);

            if (!SessionProofVerifier.Verify(_targetDevice, proof))
            {
                return ClaimAcceptanceResult.Failure("The paired-device signature could not be verified.");
            }

            var sessionRequest = new SessionRequest(
                claim.SessionId,
                _targetDevice.DeviceId,
                SessionPermission.ViewScreen,
                _invitation.ExpiresAtUtc);

            if (!_authorization.SubmitAuthenticatedRequest(
                sessionRequest,
                _targetDevice.DeviceId,
                nowUtc,
                out var error))
            {
                return ClaimAcceptanceResult.Failure(error ?? "The view-session request was rejected.");
            }

            _claimConsumed = true;
            ClearInvitationSecret();
            return ClaimAcceptanceResult.Success();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            if (_invitation is not null && _claimConsumed)
            {
                _authorization.CancelLocally(_invitation.SessionId);
            }
            ClearInvitationSecret();
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _app.StopAsync(timeout.Token);
        }
        finally
        {
            await _app.DisposeAsync();
            _certificate.Dispose();
        }
    }

    private void ClearInvitationSecret()
    {
        if (_secretHash is not null)
        {
            CryptographicOperations.ZeroMemory(_secretHash);
            _secretHash = null;
        }
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               (bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168));
    }

    private static X509Certificate2 CreateHostCertificate(IPAddress hostAddress)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=FASX View Session",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(hostAddress);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        var usages = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-2),
            DateTimeOffset.UtcNow.AddHours(3));
    }

    private static string CreateSpkiPin(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey()
            ?? throw new CryptographicException("FASX host certificate has no RSA public key.");
        return "sha256/" + Convert.ToBase64String(
            SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
    }

    private sealed record ViewSessionClaim(
        Guid SessionId,
        Guid DeviceId,
        string Secret,
        string Nonce,
        string Signature);

    private sealed record ClaimAcceptanceResult(bool Accepted, string? Error)
    {
        public static ClaimAcceptanceResult Success() => new(true, null);
        public static ClaimAcceptanceResult Failure(string error) => new(false, error);
    }
}

public sealed record ViewSessionInvitation(
    Guid SessionId,
    string Secret,
    string Nonce,
    DateTimeOffset ExpiresAtUtc);
