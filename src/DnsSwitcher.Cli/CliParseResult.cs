namespace DnsSwitcher.Cli;

public sealed record CliParseResult(CliInvocation? Invocation, string? ErrorMessage)
{
    public bool IsSuccess => Invocation is not null && string.IsNullOrWhiteSpace(ErrorMessage);

    public static CliParseResult Success(CliInvocation invocation)
    {
        return new CliParseResult(invocation, null);
    }

    public static CliParseResult Failure(string errorMessage)
    {
        return new CliParseResult(null, errorMessage);
    }
}
