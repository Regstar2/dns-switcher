namespace DnsSwitcher.Core.Models;

public sealed record ValidationError(string Code, string Path, string Message);
