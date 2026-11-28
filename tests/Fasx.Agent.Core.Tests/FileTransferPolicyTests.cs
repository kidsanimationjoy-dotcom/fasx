using Fasx.Agent.Core.Security;
using Xunit;

namespace Fasx.Agent.Core.Tests;

public sealed class FileTransferPolicyTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(1024, true)]
    [InlineData(20971520, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(20971521, false)]
    public void EnforcesFileSizeLimit(long size, bool expected) =>
        Assert.Equal(expected, FileTransferPolicy.IsSizeAllowed(size));

    [Theory]
    [InlineData("notes.txt", true)]
    [InlineData("photo 2026.png", true)]
    [InlineData("résumé.pdf", true)]
    [InlineData("..", false)]
    [InlineData(".", false)]
    [InlineData("../notes.txt", false)]
    [InlineData("folder\\notes.txt", false)]
    [InlineData("C:notes.txt", false)]
    [InlineData("CON.txt", false)]
    [InlineData("LPT1.csv", false)]
    [InlineData("name.", false)]
    [InlineData("name ", false)]
    [InlineData("", false)]
    [InlineData("this-is-a-very-long-file-name-that-is-deliberately-over-one-hundred-and-twenty-characters-in-total-because-it-must-be-rejected-by-policy.txt", false)]
    public void AcceptsOnlySafeLeafFileNames(string fileName, bool expected) =>
        Assert.Equal(expected, FileTransferPolicy.IsSafeFileName(fileName));
}
