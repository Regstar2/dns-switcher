namespace DnsSwitcher.Infrastructure.Windows.Configuration;

internal static class AtomicFileWriter
{
    public static string CreateTempPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? ".";
        var fileName = Path.GetFileName(targetPath);
        return Path.Combine(directory, $".{fileName}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
    }

    public static async Task MoveOverwritingWithRetryAsync(
        string tempPath,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 10;

        try
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Move(tempPath, targetPath, overwrite: true);
                    return;
                }
                catch (Exception exception) when (IsTransientFileAccessException(exception) && attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            DeleteQuietly(tempPath);
        }
    }

    public static void DeleteQuietly(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsTransientFileAccessException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
