using System.Windows;
using Fasx.Agent.Windows.Diagnostics;
using Fasx.Agent.Windows.Identity;

namespace Fasx.Agent.Windows;

public partial class MainWindow : Window
{
    private LocalDeviceIdentity? _deviceIdentity;

    public MainWindow()
    {
        InitializeComponent();
        LoadDeviceIdentity();
        RefreshHealth();
    }

    private void LoadDeviceIdentity()
    {
        try
        {
            _deviceIdentity = new WindowsDeviceIdentityStore().LoadOrCreate();
            DeviceIdValue.Text = _deviceIdentity.DeviceId.ToString("D");
            IdentityStatusValue.Text = "Private key protected for this Windows account.";
        }
        catch (Exception exception)
        {
            DeviceIdValue.Text = "Unavailable";
            IdentityStatusValue.Text =
                $"Device identity could not be loaded ({exception.GetType().Name}). Pairing is unavailable.";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _deviceIdentity?.Dispose();
        base.OnClosed(e);
    }

    private void RefreshHealthButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshHealth();
    }

    private void RefreshHealth()
    {
        try
        {
            var health = LocalHealthService.Read();

            OsValue.Text = health.OperatingSystem;
            ProcessorValue.Text = health.LogicalProcessors;
            MemoryValue.Text = health.Memory;
            DiskValue.Text = health.SystemDrive;
            BatteryValue.Text = health.Battery;
            UptimeValue.Text = health.Uptime;
            HealthUpdatedValue.Text = $"Updated locally at {health.CapturedAt.LocalDateTime:HH:mm:ss}";
        }
        catch (Exception exception)
        {
            HealthUpdatedValue.Text = $"Could not read all local system information: {exception.Message}";
        }
    }
}
