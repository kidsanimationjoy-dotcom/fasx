using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Fasx.Agent.Windows;

public partial class ScreenPreviewWindow : Window
{
    private bool _browserReady;
    private bool _isClosing;

    public ScreenPreviewWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var profilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FASX",
                "WebView2");

            Directory.CreateDirectory(profilePath);
            var environment = await CoreWebView2Environment.CreateAsync(
                userDataFolder: profilePath);

            await ScreenBrowser.EnsureCoreWebView2Async(environment);
            var core = ScreenBrowser.CoreWebView2;

            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;

            // This preview requires only the system screen-capture picker.
            // Other browser permissions (camera, microphone, location, clipboard, etc.) are denied.
            core.PermissionRequested += (_, args) =>
            {
                args.State = CoreWebView2PermissionState.Deny;
                args.Handled = true;
            };

            core.ScreenCaptureStarting += (_, _) =>
            {
                SetStatus("Windows is showing its screen picker. Choose the display or app window you intend to share.");
            };

            core.WebMessageReceived += (_, args) =>
            {
                try
                {
                    var message = JsonSerializer.Deserialize<string>(args.WebMessageAsJson);
                    if (message == "captureStarted")
                    {
                        SetStatus("Screen capture is active. Windows displays a visible border around the selected item.");
                    }
                    else if (message == "captureStopped")
                    {
                        SetStatus("Screen capture stopped.");
                    }
                    else if (!string.IsNullOrWhiteSpace(message))
                    {
                        SetStatus(message);
                    }
                }
                catch (JsonException)
                {
                    SetStatus("The preview page sent an invalid status message.");
                }
            };

            core.ProcessFailed += (_, _) =>
                SetStatus("The WebView2 preview process stopped. Close this window and try again.");

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
            var previewPath = Path.Combine(assetsPath, "screen-preview.html");
            if (!File.Exists(previewPath))
            {
                throw new FileNotFoundException("The FASX screen preview page is missing.", previewPath);
            }

            core.SetVirtualHostNameToFolderMapping(
                "fasx.local",
                assetsPath,
                CoreWebView2HostResourceAccessKind.DenyCors);

            core.Navigate("https://fasx.local/screen-preview.html");
            _browserReady = true;
            SetStatus("Ready. Select Start preview in the page to open the Windows picker.");
        }
        catch (Exception exception)
        {
            SetStatus("Screen preview could not start: " + exception.Message);
            MessageBox.Show(
                this,
                "FASX could not initialize Microsoft Edge WebView2. Make sure the Microsoft Edge WebView2 Runtime is installed, then try again.\n\n" +
                exception.Message,
                "FASX screen preview unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        if (_browserReady)
        {
            try
            {
                await ScreenBrowser.CoreWebView2.ExecuteScriptAsync("window.fasxStopCapture?.()");
            }
            catch
            {
                // Closing the WebView also releases its local media capture.
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

        CaptureStatusText.Text = message;
    }
}
