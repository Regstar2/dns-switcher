using Microsoft.Win32;

namespace DnsSwitcher.Infrastructure.Windows.Startup;

public sealed class WindowsAutostartManager(string valueName)
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string ValueName { get; } = string.IsNullOrWhiteSpace(valueName)
        ? throw new ArgumentException("Value name must not be empty.", nameof(valueName))
        : valueName;

    public bool IsEnabled(string executablePath, params string[] arguments)
    {
        var currentValue = GetCommandLine();
        var expectedValue = BuildCommandLine(executablePath, arguments);

        return string.Equals(currentValue, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    public string? GetCommandLine()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) as string;
    }

    public void Enable(string executablePath, params string[] arguments)
    {
        var commandLine = BuildCommandLine(executablePath, arguments);

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Windows autostart registry key could not be opened.");

        key.SetValue(ValueName, commandLine, RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);

        if (key?.GetValue(ValueName) is null)
        {
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static string BuildCommandLine(string executablePath, params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var commandParts = new List<string>
        {
            QuoteArgument(Path.GetFullPath(executablePath)),
        };

        commandParts.AddRange(arguments
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .Select(QuoteArgument));

        return string.Join(" ", commandParts);
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
