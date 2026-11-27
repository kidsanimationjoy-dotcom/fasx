using System.Windows;
using Fasx.Agent.Windows.Diagnostics;

namespace Fasx.Agent.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RefreshHealth();
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
