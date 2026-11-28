using System.Diagnostics;
using System.Windows;
using Fasx.Agent.Core.Flows;
using Microsoft.Win32;

namespace Fasx.Agent.Windows.Flows;

internal static class LocalFlowExecutor
{
    private static readonly IReadOnlyDictionary<string, string> Applications =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["notepad"] = "notepad.exe",
            ["calculator"] = "calc.exe",
            ["paint"] = "mspaint.exe",
            ["file-explorer"] = "explorer.exe"
        };

    public static bool Run(FasxFlow flow, Window owner)
    {
        if (!FasxFlowPolicy.IsValid(flow))
        {
            MessageBox.Show(owner, "This flow is invalid and cannot run.", "FASX Flows",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var summary = string.Join(
            Environment.NewLine,
            flow.Steps.Select((step, index) => $"{index + 1}. {Describe(step)}"));

        var confirmation = MessageBox.Show(
            owner,
            $"Run the following local actions?{Environment.NewLine}{Environment.NewLine}{summary}{Environment.NewLine}{Environment.NewLine}FASX will not run scripts or arbitrary commands.",
            "Confirm FASX Flow",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes) return false;

        foreach (var step in flow.Steps)
        {
            try
            {
                switch (step.Action)
                {
                    case FasxFlowAction.OpenBuiltInApp:
                        Process.Start(new ProcessStartInfo(Applications[step.Value!])
                        {
                            UseShellExecute = true
                        });
                        break;

                    case FasxFlowAction.OpenHttpsWebsite:
                        Process.Start(new ProcessStartInfo(step.Value!)
                        {
                            UseShellExecute = true
                        });
                        break;

                    case FasxFlowAction.PickAndOpenFolder:
                        var folderDialog = new OpenFolderDialog
                        {
                            Title = "Choose a folder for this FASX Flow step"
                        };
                        if (folderDialog.ShowDialog(owner) != true) return false;

                        Process.Start(new ProcessStartInfo(folderDialog.FolderName)
                        {
                            UseShellExecute = true
                        });
                        break;

                    default:
                        return false;
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    owner,
                    $"FASX stopped the flow because an action could not be completed.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                    "FASX Flow stopped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        MessageBox.Show(owner, "The selected FASX Flow finished.", "FASX Flows",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return true;
    }

    private static string Describe(FasxFlowStep step) =>
        step.Action switch
        {
            FasxFlowAction.OpenBuiltInApp => "Open " + step.Value,
            FasxFlowAction.OpenHttpsWebsite => "Open secure website: " + step.Value,
            FasxFlowAction.PickAndOpenFolder => "Choose and open a folder",
            _ => "Unsupported action"
        };
}
