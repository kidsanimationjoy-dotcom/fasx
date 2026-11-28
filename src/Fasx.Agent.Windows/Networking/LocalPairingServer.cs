using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.RateLimiting;
using Fasx.Agent.Core.Pairing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fasx.Agent.Windows.Networking;

/// <summary>
/// Temporary HTTPS listener for first-time same-LAN pairing.
/// It exposes only claim submission and read-only status; local approval is never an HTTP route.
/// </summary>
public sealed class LocalPairingServer : IAsyncDisposable
{
    public const int Port = 47832;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WebApplication _app;
    private readonly X509Certificate2 _certificate;
    private bool _disposed;

    private LocalPairingServer(WebApplication app, X509Certificate2 certificate, IPAddress hostAddress, string tlsPin)
    {
        _app = app;
        _certificate = certificate;
        HostAddress = hostAddress;
        TlsPin = tlsPin;
    }

    public IPAddress HostAddress { get; }
    public string TlsPin { get; }
    public Uri ClaimEndpoint => new($"https://{HostAddress}:{Port}/api/pairing/claim");

    public static async Task<LocalPairingServer> StartAsync(
        PairingCoordinator coordinator,
        IPAddress hostAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(hostAddress);
        if (!IsPrivateIpv4(hostAddress))
        {
            throw new ArgumentException("A private RFC1918 IPv4 network address is required.", nameof(hostAddress));
        }

        var certificate = CreateHostCertificate(hostAddress);
        var tlsPin = CreateSpkiPin(certificate);
        WebApplication? app = null;

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Production",
                ApplicationName = typeof(LocalPairingServer).Assembly.FullName
            });

            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 16 * 1024;
                options.Listen(hostAddress, Port, listenOptions => listenOptions.UseHttps(certificate));
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("pairing-claim", limiter =>
                {
                    limiter.PermitLimit = 12;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.AutoReplenishment = true;
                });
                options.AddFixedWindowLimiter("pairing-status", limiter =>
                {
                    limiter.PermitLimit = 180;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.AutoReplenishment = true;
                });
            });

            app = builder.Build();
            app.Use(async (context, next) =>
            {
                context.Response.Headers["Cache-Control"] = "no-store";
                await next();
            });
            app.UseRateLimiter();

            Func<HttpContext, Task<IResult>> claimHandler = async context =>
            {
                PairingClaim? claim;
                try
                {
                    claim = await context.Request.ReadFromJsonAsync<PairingClaim>(
                        JsonOptions,
                        context.RequestAborted);
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "The pairing claim JSON is invalid." });
                }
                catch (InvalidOperationException)
                {
                    return Results.BadRequest(new { error = "The pairing claim must use application/json." });
                }

                if (claim is null)
                {
                    return Results.BadRequest(new { error = "A pairing claim is required." });
                }

                var result = coordinator.SubmitClaim(claim, DateTimeOffset.UtcNow);
                if (!result.Accepted)
                {
                    return Results.BadRequest(new { error = result.Error });
                }

                return Results.Accepted(
                    $"/api/pairing/status/{claim.PairingId:D}",
                    new { status = PairingStatus.AwaitingLocalApproval.ToString() });
            };

            app.MapPost("/api/pairing/claim", claimHandler).RequireRateLimiting("pairing-claim");
            app.MapGet("/api/pairing/status/{pairingId:guid}", (Guid pairingId) =>
            {
                var status = coordinator.GetStatus(pairingId, DateTimeOffset.UtcNow);
                if (status == PairingStatus.NotFound)
                {
                    return Results.NotFound(new { error = "Pairing invitation was not found." });
                }

                return Results.Json(new { status = status.ToString() }, options: JsonOptions);
            }).RequireRateLimiting("pairing-status");

            await app.StartAsync(cancellationToken);
            return new LocalPairingServer(app, certificate, hostAddress, tlsPin);
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

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
            "CN=FASX Local Pairing",
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
        var digest = SHA256.HashData(rsa.ExportSubjectPublicKeyInfo());
        return "sha256/" + Convert.ToBase64String(digest);
    }
}
