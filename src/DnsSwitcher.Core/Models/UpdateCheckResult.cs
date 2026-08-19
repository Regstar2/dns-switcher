namespace DnsSwitcher.Core.Models;

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    SemanticVersion CurrentVersion,
    UpdateInfo? Update,
    UpdateFailureKind? FailureKind)
{
    public static UpdateCheckResult Current(SemanticVersion currentVersion) =>
        new(UpdateCheckStatus.Current, currentVersion, null, null);

    public static UpdateCheckResult Available(SemanticVersion currentVersion, UpdateInfo update) =>
        new(UpdateCheckStatus.UpdateAvailable, currentVersion, update, null);

    public static UpdateCheckResult Unavailable(SemanticVersion currentVersion, UpdateFailureKind failureKind) =>
        new(UpdateCheckStatus.Unavailable, currentVersion, null, failureKind);
}
