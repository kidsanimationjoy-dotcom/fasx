namespace Fasx.Agent.Core.Flows;

/// <summary>
/// The only workflow actions supported by the first local FASX Flows release.
/// </summary>
public enum FasxFlowAction
{
    OpenBuiltInApp = 1,
    OpenHttpsWebsite = 2,
    PickAndOpenFolder = 3
}

public sealed record FasxFlowStep(
    FasxFlowAction Action,
    string? Value = null);

public sealed record FasxFlow(
    Guid Id,
    string Name,
    IReadOnlyList<FasxFlowStep> Steps);
