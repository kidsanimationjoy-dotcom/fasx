namespace Fasx.Agent.Core.Flows;

/// <summary>
/// Validates stored workflows against a fixed, non-script action vocabulary.
/// A workflow can never contain shell text, executable paths, or arbitrary commands.
/// </summary>
public static class FasxFlowPolicy
{
    public const int MaximumFlows = 20;
    public const int MaximumStepsPerFlow = 8;
    public const int MaximumNameLength = 40;
    public const int MaximumWebsiteLength = 512;

    private static readonly HashSet<string> BuiltInApps = new(StringComparer.Ordinal)
    {
        "notepad",
        "calculator",
        "paint",
        "file-explorer"
    };

    public static bool IsValid(FasxFlow? flow)
    {
        if (flow is null ||
            flow.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(flow.Name) ||
            flow.Name.Length > MaximumNameLength ||
            flow.Name.Any(char.IsControl) ||
            flow.Steps is null ||
            flow.Steps.Count is < 1 or > MaximumStepsPerFlow)
        {
            return false;
        }

        foreach (var step in flow.Steps)
        {
            if (step is null)
            {
                return false;
            }

            switch (step.Action)
            {
                case FasxFlowAction.OpenBuiltInApp:
                    if (step.Value is null || !BuiltInApps.Contains(step.Value))
                    {
                        return false;
                    }
                    break;

                case FasxFlowAction.OpenHttpsWebsite:
                    if (!IsSafeHttpsWebsite(step.Value))
                    {
                        return false;
                    }
                    break;

                case FasxFlowAction.PickAndOpenFolder:
                    if (!string.IsNullOrEmpty(step.Value))
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }
        }

        return true;
    }

    public static bool IsSafeHttpsWebsite(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaximumWebsiteLength ||
            value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(uri.Host) &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               (uri.IsDefaultPort || uri.Port == 443) &&
               !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }
}
