using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Core.Security;
using Fasx.Agent.Core.Sessions;
using Fasx.Agent.Windows.Networking;
using QRCoder;

namespace Fasx.Agent.Windows;

public partial class ViewSessionWindow : Window
{
    private readonly PairingCoordinator _pairingCoordinator;
    private readonly SessionAuthorizationCoordinator _authorization;
    private readonly DispatcherTimer _refreshTimer;
    private RemoteViewSessionServer? _server;
    private ViewSessionInvitation? _invitation;
    private bool _closing;

    public ViewSessionWindow(PairingCoordinator pairingCoordinator)
    {
        _pairingCoordinator = pairingCoordinator ?? throw new ArgumentNullException(nameof(pairingCoordinator));
        _authorization = new SessionAuthorizationCoordinator(deviceId =>
            _pairingCoordinator.GetPairedDevices().Any(device => device.DeviceId == deviceId));

        InitializeComponent();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => RefreshSessionStatus();

        PairedDeviceCombo.ItemsSource = _pairingCoordinator.GetPairedDevices().ToArray();
        if (PairedDeviceCombo.Items.Count > 0)
        {
            PairedDeviceCombo.SelectedIndex = 0;
        }
        else
        {
            CreateInviteButton.IsEnabled = false;
            SessionStatusText.Text = "No paired phones are registered yet. Pair a phone first.";
        }
    }

    private void PairedDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_server is null && PairedDeviceCombo.SelectedItem is PairedDevice selected)
        {
            SessionStatusText.Text = "Ready to create a view-only invitation for " + selected.DeviceName + ".";
        }
    }

    private async void CreateInvite_Click(object sender, RoutedEventArgs e)
    {
        if (PairedDeviceCombo.SelectedItem is not PairedDevice targetDevice)
        {
            SessionStatusText.Text = "Select a paired phone first.";
            return;
        }

        CreateInviteButton.IsEnabled = false;

        try
        {
            var address = LocalNetworkAddressProvider.GetPrivateIpv4Addresses().FirstOrDefault();
            if (address is null)
            {
                throw new InvalidOperationException(
                    "No private Wi-Fi or Ethernet IPv4 address was found. Connect both devices to a trusted local network.");
            }

            _server = await RemoteViewSessionServer.StartAsync(
                _pairingCoordinator,
                _authorization,
                targetDevice,
                address);

            _invitation = _server.CreateInvitation(DateTimeOffset.UtcNow);
            var qrPayload = JsonSerializer.Serialize(new
            {
                v = 1,
                kind = "fasx-view",
                sessionId = _invitation.SessionId.ToString("D"),
                secret = _invitation.Secret,
                nonce = _invitation.Nonce,
                endpoint = _server.RequestEndpoint.ToString(),
                tlsPin = _server.TlsPin,
                expiresAtUtc = _invitation.ExpiresAtUtc.UtcDateTime.ToString("O")
            });

            ViewQrImage.Source = CreateQrImage(qrPayload);
            SessionEndpointText.Text = _server.RequestEndpoint.Host + ":" + RemoteViewSessionServer.Port;
            SessionExpiryText.Text = "Expires: " + _invitation.ExpiresAtUtc.ToLocalTime().ToString("T");
            SessionStatusText.Text = "Invitation ready for " + targetDevice.DeviceName + ". Scan it using the FASX app.";
            ApprovalDescriptionText.Text = "Waiting for the selected phone to prove its paired-device identity.";
            StopSessionButton.Visibility = Visibility.Visible;
            _refreshTimer.Start();
        }
        catch (Exception exception)
        {
            SessionStatusText.Text = "Could not create the view invitation: " + exception.Message;
            await StopServerAsync();
            CreateInviteButton.IsEnabled = true;
        }
    }

    private void RefreshSessionStatus()
    {
        if (_server is null || _invitation is null)
        {
            return;
        }

        if (!_server.IsKnownSession(_invitation.SessionId))
        {
            if (DateTimeOffset.UtcNow >= _invitation.ExpiresAtUtc)
            {
                SessionStatusText.Text = "Invitation expired. Stop this session and create a new one.";
                ApprovalDescriptionText.Text = "No session request was approved.";
            }
            return;
        }

        var status = _authorization.GetStatus(_invitation.SessionId, DateTimeOffset.UtcNow);
        switch (status)
        {
            case SessionStatus.AwaitingLocalApproval:
                SessionStatusText.Text = "A verified request has arrived. Your approval is required.";
                ApprovalDescriptionText.Text = "The paired phone is requesting View screen only.";
                ApprovalButtons.Visibility = Visibility.Visible;
                break;

            case SessionStatus.Approved:
                ApprovalButtons.Visibility = Visibility.Collapsed;
                SessionStatusText.Text = "View screen permission approved.";
                ApprovalDescriptionText.Text = "The phone is authorized for View screen for up to 10 minutes. Live video transport is the next connection step; no screen frames are being sent yet.";
                break;

            case SessionStatus.Denied:
                ApprovalButtons.Visibility = Visibility.Collapsed;
                ApprovalDescriptionText.Text = "You denied the view-only session request.";
                break;

            case SessionStatus.Expired:
                ApprovalButtons.Visibility = Visibility.Collapsed;
                ApprovalDescriptionText.Text = "The session request or its approval lease expired.";
                break;

            case SessionStatus.Revoked:
            case SessionStatus.Cancelled:
                ApprovalButtons.Visibility = Visibility.Collapsed;
                ApprovalDescriptionText.Text = "The view-only session was stopped locally.";
                break;
        }
    }

    private void ApproveView_Click(object sender, RoutedEventArgs e)
    {
        if (_invitation is null)
        {
            return;
        }

        try
        {
            _authorization.ApproveLocally(
                _invitation.SessionId,
                SessionPermission.ViewScreen,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(10));

            ApprovalButtons.Visibility = Visibility.Collapsed;
            SessionStatusText.Text = "View screen permission approved.";
            ApprovalDescriptionText.Text = "Only View screen is approved for 10 minutes. The screen stream has not started.";
        }
        catch (Exception exception)
        {
            ApprovalDescriptionText.Text = "Approval failed: " + exception.Message;
        }
    }

    private void DenyView_Click(object sender, RoutedEventArgs e)
    {
        if (_invitation is null)
        {
            return;
        }

        try
        {
            _authorization.DenyLocally(_invitation.SessionId);
            ApprovalButtons.Visibility = Visibility.Collapsed;
            SessionStatusText.Text = "View-only session denied.";
            ApprovalDescriptionText.Text = "The phone will receive the denial status.";
        }
        catch (Exception exception)
        {
            ApprovalDescriptionText.Text = "Could not deny request: " + exception.Message;
        }
    }

    private async void StopSession_Click(object sender, RoutedEventArgs e)
    {
        await StopServerAsync();
        ViewQrImage.Source = null;
        SessionEndpointText.Text = string.Empty;
        SessionExpiryText.Text = string.Empty;
        ApprovalButtons.Visibility = Visibility.Collapsed;
        StopSessionButton.Visibility = Visibility.Collapsed;
        CreateInviteButton.IsEnabled = PairedDeviceCombo.Items.Count > 0;
        SessionStatusText.Text = "View-only session stopped. The temporary HTTPS listener is closed.";
        ApprovalDescriptionText.Text = "No active view-session request.";
    }

    private static BitmapImage CreateQrImage(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(9);

        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private async Task StopServerAsync()
    {
        _refreshTimer.Stop();

        if (_server is not null)
        {
            var server = _server;
            _server = null;
            await server.DisposeAsync();
        }

        _invitation = null;
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_closing)
        {
            base.OnClosed(e);
            return;
        }

        _closing = true;
        await StopServerAsync();
        base.OnClosed(e);
    }
}
