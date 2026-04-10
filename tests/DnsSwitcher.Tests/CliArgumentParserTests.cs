using DnsSwitcher.Cli;

namespace DnsSwitcher.Tests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void Parse_ReturnsInteractiveInvocation_WhenNoArgumentsAreProvided()
    {
        var result = CliArgumentParser.Parse([]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invocation);
        Assert.True(result.Invocation!.IsInteractive);
        Assert.Null(result.Invocation.Command);
    }

    [Fact]
    public void Parse_NormalizesLegacyAliases_AndReadsGlobalOptions()
    {
        var result = CliArgumentParser.Parse(["switch", "google", "--adapter", "Wi-Fi", "--config=C:\\dns\\profiles.json"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invocation);
        Assert.Equal(CliCommand.Apply, result.Invocation!.Command);
        Assert.Equal("google", result.Invocation.CommandArgument);
        Assert.Equal("Wi-Fi", result.Invocation.AdapterSelection);
        Assert.Equal("C:\\dns\\profiles.json", result.Invocation.ConfigPath);
    }

    [Fact]
    public void Parse_ReturnsError_ForUnknownOption()
    {
        var result = CliArgumentParser.Parse(["status", "--wat"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Unknown option: --wat", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ReturnsUsageError_WhenApplyArgumentIsMissing()
    {
        var result = CliArgumentParser.Parse(["apply"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Usage: dns-switcher apply <profile-id>", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ReadsServiceCommand_AndOptionalAgentPath()
    {
        var result = CliArgumentParser.Parse(["service", "install", "C:\\tools\\DnsSwitcher.Agent.Windows.exe"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invocation);
        Assert.Equal(CliCommand.Service, result.Invocation!.Command);
        Assert.Equal("install", result.Invocation.CommandArgument);
        Assert.Equal("C:\\tools\\DnsSwitcher.Agent.Windows.exe", result.Invocation.SecondaryArgument);
    }

    [Fact]
    public void Parse_ParsesTestCommand_WithAdapterOption()
    {
        var result = CliArgumentParser.Parse(["test", "--adapter", "Wi-Fi"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invocation);
        Assert.Equal(CliCommand.Test, result.Invocation!.Command);
        Assert.Equal("Wi-Fi", result.Invocation.AdapterSelection);
    }

    [Fact]
    public void Parse_ParsesTestSitesCommand_WithConfigOverride()
    {
        var result = CliArgumentParser.Parse(["test-sites", "--config", "C:\\dns\\profiles.json"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invocation);
        Assert.Equal(CliCommand.TestSites, result.Invocation!.Command);
        Assert.Equal("C:\\dns\\profiles.json", result.Invocation.ConfigPath);
    }

    [Fact]
    public void Parse_ParsesBenchmarkCommand_WithAdapterOption()
    {
        var result = CliArgumentParser.Parse(["benchmark", "--adapter", "Wi-Fi"]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invocation);
        Assert.Equal(CliCommand.Benchmark, result.Invocation!.Command);
        Assert.Equal("Wi-Fi", result.Invocation.AdapterSelection);
    }
}
