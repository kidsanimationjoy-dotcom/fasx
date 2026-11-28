using System;
using System.Linq;
using Fasx.Agent.Core.Flows;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class FasxFlowPolicyTests
{
    [Fact]
    public void AcceptsAFlowMadeOnlyFromRegisteredActions()
    {
        var flow = new FasxFlow(
            Guid.NewGuid(),
            "Morning setup",
            new[]
            {
                new FasxFlowStep(FasxFlowAction.OpenBuiltInApp, "notepad"),
                new FasxFlowStep(FasxFlowAction.OpenHttpsWebsite, "https://example.com"),
                new FasxFlowStep(FasxFlowAction.PickAndOpenFolder)
            });

        Assert.True(FasxFlowPolicy.IsValid(flow));
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("powershell")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("anything")]
    public void RejectsUnregisteredApplications(string application)
    {
        var flow = new FasxFlow(
            Guid.NewGuid(),
            "Unsafe app",
            new[] { new FasxFlowStep(FasxFlowAction.OpenBuiltInApp, application) });

        Assert.False(FasxFlowPolicy.IsValid(flow));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("https://user:pass@example.com")]
    [InlineData("https://localhost")]
    [InlineData("")]
    public void RejectsUnsafeWebsiteTargets(string url)
    {
        var flow = new FasxFlow(
            Guid.NewGuid(),
            "Unsafe website",
            new[] { new FasxFlowStep(FasxFlowAction.OpenHttpsWebsite, url) });

        Assert.False(FasxFlowPolicy.IsValid(flow));
    }

    [Fact]
    public void RejectsTooManyStepsAndUnknownActionNumbers()
    {
        var tooManySteps = new FasxFlow(
            Guid.NewGuid(),
            "Too many steps",
            Enumerable.Repeat(new FasxFlowStep(FasxFlowAction.OpenBuiltInApp, "notepad"), 9).ToArray());

        var unknownAction = new FasxFlow(
            Guid.NewGuid(),
            "Unknown action",
            new[] { new FasxFlowStep((FasxFlowAction)999, "anything") });

        Assert.False(FasxFlowPolicy.IsValid(tooManySteps));
        Assert.False(FasxFlowPolicy.IsValid(unknownAction));
    }

    [Fact]
    public void RejectsEmptyNamesAndFolderStepsWithArbitraryPaths()
    {
        var emptyName = new FasxFlow(
            Guid.NewGuid(),
            " ",
            new[] { new FasxFlowStep(FasxFlowAction.OpenBuiltInApp, "notepad") });

        var arbitraryFolder = new FasxFlow(
            Guid.NewGuid(),
            "Folder path",
            new[] { new FasxFlowStep(FasxFlowAction.PickAndOpenFolder, "C:\\Users\\Other") });

        Assert.False(FasxFlowPolicy.IsValid(emptyName));
        Assert.False(FasxFlowPolicy.IsValid(arbitraryFolder));
    }
}
