using System.Windows;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Windows.Storage;

namespace Fasx.Agent.Windows;

public partial class MainWindow : Window
{
    private readonly PairingCoordinator? _pairingCoordinator;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _pairingCoordinator = new PairingCoordinator(new DpapiPairedDeviceStore());
            RefreshPairedDeviceCount();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"FASX could not safely open its protected device store. No paired-device access will be enabled.\n\n{exception.Message}",
                "FASX device store error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
    }

    private void PairDevices_Click(object sender, RoutedEventArgs e)
    {
        if (_pairingCoordinator is null)
        {
            return;
        }

        var pairingWindow = new PairingWindow(_pairingCoordinator)
        {
            Owner = this
        };
        pairingWindow.ShowDialog();
        RefreshPairedDeviceCount();
    }

    private void PreviewScreen_Click(object sender, RoutedEventArgs e)
    {
        var previewWindow = new ScreenPreviewWindow
        {
            Owner = this
        };
        previewWindow.ShowDialog();
    }

    private void ViewSession_Click(object sender, RoutedEventArgs e)
    {
        if (_pairingCoordinator is null)
        {
            return;
        }

        var viewSessionWindow = new ViewSessionWindow(_pairingCoordinator)
        {
            Owner = this
        };
        viewSessionWindow.ShowDialog();
        RefreshPairedDeviceCount();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var diagnosticsWindow = new DiagnosticsWindow
        {
            Owner = this
        };
        diagnosticsWindow.ShowDialog();
    }

    private void RefreshPairedDeviceCount()
    {
        if (_pairingCoordinator is not null)
        {
            PairedDevicesValue.Text = _pairingCoordinator.GetPairedDevices().Count.ToString();
        }
    }
}
