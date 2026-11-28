using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Windows.Networking;
using QRCoder;

namespace Fasx.Agent.Windows;

public partial class PairingWindow : Window
{
    private readonly PairingCoordinator _pairingCoordinator;
    private readonly DispatcherTimer _refreshTimer;
    private LocalPairingServer? _server;
    private PairingInvitation? _invitation;

    public PairingWindow(PairingCoordinator pairingCoordinator)
    {
        _pairingCoordinator = pairingCoordinator ?? throw new ArgumentNullException(nameof(pairingCoordinator));
        InitializeComponent();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => RefreshPendingRequests();
        PairedDevicesCountText.Text = $"{_pairingCoordinator.GetPairedDevices().Count} trusted devices";
    }

    private async void StartPairing_Click(object sender, RoutedEventArgs e)
    {
        StartPairingButton.IsEnabled = false;

        try
        {
            if (_server is null)
            {
                var address = LocalNetworkAddressProvider.GetPrivateIpv4Addresses().FirstOrDefault();
                if (address is null)
                {
                    PairingStatusText.Text = "No private Wi-Fi or Ethernet IPv4 address was found. Connect to your local network and try again.";
                    return;
                }

                _server = await LocalPairingServer.StartAsync(_pairingCoordinator, address);
            }

            // Replacing an invitation invalidates the old QR immediately.
            if (_invitation is not null)
            {
                _pairingCoordinator.CancelInvitationLocally(_invitation.PairingId);
            }

            _invitation = _pairingCoordinator.CreateInvitation(DateTimeOffset.UtcNow);
            var qrPayload = JsonSerializer.Serialize(new
            {
                v = 1,
                kind = "fasx-pair",
                pairingId = _invitation.PairingId.ToString("D"),
                secret = _invitation.Secret,
                endpoint = _server.ClaimEndpoint.ToString(),
                tlsPin = _server.TlsPin,
                expiresAtUtc = _invitation.ExpiresAtUtc.UtcDateTime.ToString("O")
            });

            PairingQrImage.Source = CreateQrImage(qrPayload);
            PairingStatusText.Text = "Invitation ready. Scan it with the FASX Android app, then approve the phone request below.";
            PairingEndpointText.Text = _server.ClaimEndpoint.Host + ":" + LocalPairingServer.Port;
            PairingExpiryText.Text = "Expires: " + _invitation.ExpiresAtUtc.ToLocalTime().ToString("T");
            StopPairingButton.Visibility = Visibility.Visible;
            StartPairingButton.Content = "Create a new invitation";
            _refreshTimer.Start();
            RefreshPendingRequests();
        }
        catch (Exception exception)
        {
            PairingStatusText.Text = "Could not start local pairing: " + exception.Message;
            await StopServerAsync();
        }
        finally
        {
            StartPairingButton.IsEnabled = true;
        }
    }

    private async void StopPairing_Click(object sender, RoutedEventArgs e)
    {
        await StopServerAsync();
        PairingQrImage.Source = null;
        PairingEndpointText.Text = string.Empty;
        PairingExpiryText.Text = string.Empty;
        PairingStatusText.Text = "Pairing stopped. The local listener is closed.";
        StopPairingButton.Visibility = Visibility.Collapsed;
        StartPairingButton.Content = "Enable local pairing";
        PendingPairingsPanel.Children.Clear();
        PendingPairingsPanel.Children.Add(CreateEmptyState());
    }

    private void RefreshPendingRequests()
    {
        if (_server is null) return;

        var pending = _pairingCoordinator.GetPendingRequests(DateTimeOffset.UtcNow);
        PendingPairingsPanel.Children.Clear();

        if (pending.Count == 0)
        {
            PendingPairingsPanel.Children.Add(CreateEmptyState());
        }
        else
        {
            foreach (var request in pending)
            {
                PendingPairingsPanel.Children.Add(CreatePendingRequestCard(request));
            }
        }

        PairedDevicesCountText.Text = $"{_pairingCoordinator.GetPairedDevices().Count} trusted devices";
    }

    private static TextBlock CreateEmptyState() => new()
    {
        Text = "No pending pairing requests.",
        Foreground = new SolidColorBrush(Color.FromRgb(129, 144, 170)),
        FontSize = 13
    };

    private UIElement CreatePendingRequestCard(PendingPairingRequest request)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(32, 44, 69)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(13),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = request.DeviceName,
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = Brushes.White
        });
        content.Children.Add(new TextBlock
        {
            Text = "Device ID: " + request.DeviceId.ToString("N")[..12],
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(170, 182, 204)),
            Margin = new Thickness(0, 4, 0, 10)
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var approveButton = new Button
        {
            Content = "Approve device",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(57, 120, 232)),
            Foreground = Brushes.White
        };
        approveButton.Click += (_, _) => ApproveRequest(request);

        var denyButton = new Button
        {
            Content = "Deny",
            Padding = new Thickness(10, 6, 10, 6),
            Background = new SolidColorBrush(Color.FromRgb(56, 65, 84)),
            Foreground = Brushes.White
        };
        denyButton.Click += (_, _) => DenyRequest(request);

        buttons.Children.Add(approveButton);
        buttons.Children.Add(denyButton);
        content.Children.Add(buttons);
        card.Child = content;
        return card;
    }

    private void ApproveRequest(PendingPairingRequest request)
    {
        try
        {
            _pairingCoordinator.ApproveLocally(request.RequestId, DateTimeOffset.UtcNow);
            PairingStatusText.Text = $"Approved {request.DeviceName}. The phone is now a paired device; remote-control permissions are still separate.";
        }
        catch (Exception exception)
        {
            PairingStatusText.Text = "Approval failed: " + exception.Message;
        }

        RefreshPendingRequests();
    }

    private void DenyRequest(PendingPairingRequest request)
    {
        try
        {
            _pairingCoordinator.DenyLocally(request.RequestId);
            PairingStatusText.Text = $"Denied pairing request from {request.DeviceName}.";
        }
        catch (Exception exception)
        {
            PairingStatusText.Text = "Could not deny request: " + exception.Message;
        }

        RefreshPendingRequests();
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
        if (_invitation is not null)
        {
            _pairingCoordinator.CancelInvitationLocally(_invitation.PairingId);
            _invitation = null;
        }

        if (_server is not null)
        {
            var server = _server;
            _server = null;
            await server.DisposeAsync();
        }
    }

    protected override async void OnClosed(EventArgs e)
    {
        await StopServerAsync();
        base.OnClosed(e);
    }
}
