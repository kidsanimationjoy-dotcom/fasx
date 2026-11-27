using System.Windows;
using Fasx.Agent.Core.Pairing;
using Fasx.Agent.Windows.Storage;

namespace Fasx.Agent.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        try
        {
            var pairingCoordinator = new PairingCoordinator(new DpapiPairedDeviceStore());
            PairedDevicesValue.Text = pairingCoordinator.GetPairedDevices().Count.ToString();
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
}
