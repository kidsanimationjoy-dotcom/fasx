using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;
using Fasx.Agent.Windows.Networking;
using Microsoft.Web.WebView2.Core;

namespace Fasx.Agent.Windows;

public partial class RemoteScreenWindow : Window
{
    private readonly RemoteViewSessionServer _server;
    private readonly SessionAuthorizationCoordinator _authorization;
    private readonly Guid _sessionId;
    private readonly DispatcherTimer _answerTimer;
    private bool _browserReady;
    private bool _closing;
    private bool _answerDelivered;

    public RemoteScreenWindow(
        RemoteViewSessionServer server,
        SessionAuthorizationCoordinator authorization,
        Guid sessionId)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _sessionId = sessionId;
        InitializeComponent();

        _answerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _answerTimer.Tick += (_, _) => CheckForPhoneAnswer();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_authorization.HasPermission(
            _sessionId,
            SessionPermission.ViewScreen,
            DateTimeOffset.UtcNow))
        {
            SetStatus("View screen permission is not active. This window will close.");
            Close();
            return;
        }

        try
        {
            var profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FASX",
                "WebView2");

            Directory.CreateDirectory(profilePath);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profilePath);
            await RemoteScreenBrowser.EnsureCoreWebView2Async(environment);

            var core = RemoteScreenBrowser.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;

            // The remote sender requires no camera, microphone, clipboard, file, or location access.
            // Display capture is mediated separately by the WebView2 screen-capture picker.
            core.PermissionRequested += (_, args) =>
            {
                args.State = CoreWebView2PermissionState.Deny;
                args.Handled = true;
            };

            core.ScreenCaptureStarting += (_, _) =>
                SetStatus("Windows screen picker opened. Select the display or application window to share.");

            core.WebMessageReceived += (_, args) => HandleWebMessage(args.WebMessageAsJson);
            core.ProcessFailed += (_, _) =>
            {
                SetStatus("The WebView2 screen sender stopped unexpectedly. The session will be revoked.");
                StopSessionAndClose();
            };

            core.NavigationStarting += (_, args) =>
            {
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) ||
                    !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    !uri.Host.Equals("fasx.local", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };

            var assetsPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var pagePath = Path.Combine(assetsPath, "remote-screen.html");
            if (!File.Exists(pagePath))
            {
                throw new FileNotFoundException("The FASX remote-screen page is missing.", pagePath);
            }

            core.SetVirtualHostNameToFolderMapping(
                "fasx.local",
                assetsPath,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.Navigate("https://fasx.local/remote-screen.html");

            _browserReady = true;
            _answerTimer.Start();
            SetStatus("Ready. Press Start screen sharing in the page to open the Windows picker.");
        }
        catch (Exception exception)
        {
            SetStatus("The screen sender could not start: " + exception.Message);
            MessageBox.Show(
                this,
                "FASX could not initialize the WebView2 screen sender. Verify that Microsoft Edge WebView2 Runtime is installed.\n\n" +
                exception.Message,
                "FASX screen sharing unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            StopSessionAndClose();
        }
    }

    private void HandleWebMessage(string rawMessage)
    {
        try
        {
            using var document = JsonDocument.Parse(rawMessage);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            switch (type)
            {
                case "offer":
                    if (!root.TryGetProperty("sdp", out var sdpElement))
                    {
                        SetStatus("The screen sender did not include a WebRTC offer.");
                        return;
                    }

                    var sdp = sdpElement.GetString();
                    if (!_server.StoreHostOfferLocally(
                        _sessionId,
                        sdp ?? string.Empty,
                        DateTimeOffset.UtcNow,
                        out var error))
                    {
                        SetStatus("The secure screen offer was rejected: " + error);
                        return;
                    }

                    SetStatus("Screen offer is ready. Waiting for the approved phone to answer.");
                    break;

                case "status":
                    if (root.TryGetProperty("message", out var messageElement))
                    {
                        SetStatus(messageElement.GetString() ?? "Screen sharing status changed.");
                    }
                    break;

                case "peerState":
                    if (root.TryGetProperty("state", out var stateElement))
                    {
                        var state = stateElement.GetString() ?? string.Empty;
                        SetStatus("Phone connection: " + state);

                        if (state is "disconnected" or "failed" or "closed")
                        {
                            SetStatus("Phone disconnected. Stopping capture and revoking View screen.");
                            StopSessionAndClose();
                        }
                    }
                    break;

                case "captureStopped":
                    SetStatus("Screen capture stopped. View screen permission has been revoked.");
                    _authorization.RevokeLocally(_sessionId);
                    _answerTimer.Stop();
                    break;
            }
        }
        catch (JsonException)
        {
            SetStatus("The screen sender returned an invalid status message.");
        }
    }

    private void CheckForPhoneAnswer()
    {
        if (_closing || _answerDelivered)
        {
            return;
        }

        if (!_authorization.HasPermission(
            _sessionId,
            SessionPermission.ViewScreen,
            DateTimeOffset.UtcNow))
        {
            SetStatus("View screen permission expired or was revoked. Capture is stopping.");
            StopSessionAndClose();
            return;
        }

        var answer = _server.TakePhoneAnswerForHost(_sessionId, DateTimeOffset.UtcNow);
        if (answer is null)
        {
            return;
        }

        try
        {
            RemoteScreenBrowser.CoreWebView2.PostWebMessageAsJson(
                JsonSerializer.Serialize(new { type = "answer", sdp = answer }));
            _answerDelivered = true;
            SetStatus("Phone answer received. WebRTC is establishing the view-only stream.");
        }
        catch (Exception exception)
        {
            SetStatus("Could not apply the phone answer: " + exception.Message);
            StopSessionAndClose();
        }
    }

    private async void StopSession_Click(object sender, RoutedEventArgs e)
    {
        await StopBrowserCaptureAsync();
        _authorization.RevokeLocally(_sessionId);
        Close();
    }

    private void StopSessionAndClose()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _answerTimer.Stop();
        _authorization.RevokeLocally(_sessionId);

        if (_browserReady)
        {
            _ = RemoteScreenBrowser.CoreWebView2.ExecuteScriptAsync(
                "window.fasxStopRemoteScreen?.()");
        }

        Close();
    }

    private async Task StopBrowserCaptureAsync()
    {
        _answerTimer.Stop();
        if (_browserReady)
        {
            try
            {
                await RemoteScreenBrowser.CoreWebView2.ExecuteScriptAsync(
                    "window.fasxStopRemoteScreen?.()");
            }
            catch
            {
                // Closing the WebView also releases its local display-capture track.
            }
        }
    }

    private void SetStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetStatus(message));
            return;
        }

        RemoteScreenStatusText.Text = message;
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        if (_closing)
        {
            _answerTimer.Stop();
            _authorization.RevokeLocally(_sessionId);
            return;
        }

        _closing = true;
        await StopBrowserCaptureAsync();
        _authorization.RevokeLocally(_sessionId);
    }
}
