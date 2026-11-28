using System.Windows;
using Fasx.Agent.Windows.Diagnostics;

namespace Fasx.Agent.Windows;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshSnapshot();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshSnapshot();

    private void RefreshSnapshot()
    {
        try
        {
            HealthDetailsText.Text = WindowsDiagnosticsProvider.Capture().ToDisplayText();
        }
        catch (Exception exception)
        {
            HealthDetailsText.Text =
                "FASX could not read local system information.\n\n" +
                exception.Message +
                "\n\nNo network request was made.";
        }
    }
}
