namespace DnsSwitcher.Core.Models;

public sealed record ApplicationMetadata(
    string ProductName,
    SemanticVersion Version,
    string DisplayVersion,
    Uri RepositoryUri);
