using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.5.0", "1.5.0", 0)]
    [InlineData("1.5.1", "1.5.0", 1)]
    [InlineData("1.6.0", "1.5.9", 1)]
    [InlineData("2.0.0", "1.99.0", 1)]
    [InlineData("1.5.0", "1.5.1", -1)]
    [InlineData("1.6.0-beta.1", "1.6.0", -1)]
    [InlineData("1.6.0-rc.1", "1.6.0-beta.2", 1)]
    [InlineData("1.6.0-beta.10", "1.6.0-beta.2", 1)]
    public void CompareTo_UsesSemanticVersionPrecedence(string left, string right, int expectedSign)
    {
        var comparison = SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right));

        Assert.Equal(expectedSign, Math.Sign(comparison));
    }

    [Theory]
    [InlineData("v1.5.0", "1.5.0")]
    [InlineData("1.5.0+build.7", "1.5.0")]
    [InlineData("1.6.0-beta.1+sha.123", "1.6.0-beta.1")]
    public void Parse_NormalizesSupportedReleaseTags(string input, string expected)
    {
        Assert.Equal(expected, SemanticVersion.Parse(input).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("01.5.0")]
    [InlineData("1.5.0-beta.01")]
    [InlineData("release-1.5.0")]
    public void TryParse_RejectsMalformedVersions(string input)
    {
        Assert.False(SemanticVersion.TryParse(input, out _));
    }
}
