using Fasx.Agent.Core.Security;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class RemoteInputPolicyTests
{
    [Theory]
    [InlineData("pointer", "move", 0.5, 0.5, 0, null, true)]
    [InlineData("pointer", "click", 0.1, 0.9, 0, null, true)]
    [InlineData("pointer", "rightClick", 0.5, 0.5, 0, null, true)]
    [InlineData("pointer", "scroll", 0.5, 0.5, 120, null, true)]
    [InlineData("pointer", "scroll", 0.5, 0.5, 500, null, false)]
    [InlineData("pointer", "move", 1.5, 0.5, 0, null, false)]
    [InlineData("pointer", "move", double.NaN, 0.5, 0, null, false)]
    [InlineData("key", "press", 0, 0, 0, "ENTER", true)]
    [InlineData("key", "press", 0, 0, 0, "ARROWRIGHT", true)]
    [InlineData("key", "press", 0, 0, 0, "A", false)]
    [InlineData("key", "press", 0, 0, 0, "WIN", false)]
    [InlineData("key", "press", 0, 0, 0, "ALT+F4", false)]
    [InlineData("shell", "run", 0, 0, 0, "whoami", false)]
    public void AcceptsOnlyBoundedPointerAndAllowlistedKeyEvents(
        string kind,
        string action,
        double x,
        double y,
        int delta,
        string? key,
        bool expected)
    {
        var command = new RemoteInputMessage(kind, action, x, y, delta, key);
        Assert.Equal(expected, RemoteInputPolicy.IsValid(command));
    }
}
