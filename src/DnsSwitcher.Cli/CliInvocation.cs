namespace DnsSwitcher.Cli;

public sealed record CliInvocation(
    CliCommand? Command,
    string? CommandArgument,
    string? AdapterSelection,
    string? ConfigPath)
{
    public bool IsInteractive => Command is null;
}
