namespace DnsSwitcher.Core.Models;

public sealed record UpdateInfo(
    SemanticVersion Version,
    string InstallerFileName,
    Uri InstallerUri,
    Uri ChecksumUri,
    Uri ReleasePageUri,
    DateTimeOffset? PublishedAt);
