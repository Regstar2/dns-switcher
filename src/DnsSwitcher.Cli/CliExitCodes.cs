namespace DnsSwitcher.Cli;

public static class CliExitCodes
{
    public const int Success = 0;
    public const int InvalidArguments = 1;
    public const int InvalidConfig = 2;
    public const int ProfileNotFound = 3;
    public const int AdapterError = 4;
    public const int AdminRequired = 5;
    public const int DnsOperationFailed = 6;
    public const int UnexpectedError = 7;
}
