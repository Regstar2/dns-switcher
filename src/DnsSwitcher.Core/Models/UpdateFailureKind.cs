namespace DnsSwitcher.Core.Models;

public enum UpdateFailureKind
{
    Network,
    InvalidRelease,
    MissingInstaller,
    MissingChecksum,
    ChecksumInvalid,
    ChecksumMismatch,
    InvalidDownloadUrl,
    LaunchCancelled,
    LaunchFailed,
}
