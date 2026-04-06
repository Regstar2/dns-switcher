using DnsSwitcher.Core.Models;

namespace DnsSwitcher.Core.Services;

public sealed class AppConfigValidationException(IReadOnlyList<ValidationError> errors)
    : InvalidOperationException(CreateMessage(errors))
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;

    private static string CreateMessage(IReadOnlyList<ValidationError> errors)
    {
        return errors.Count == 0
            ? "App config validation failed."
            : $"App config validation failed with {errors.Count} error(s).";
    }
}
