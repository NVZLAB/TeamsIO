using System.Text;

internal static class AppLog
{
    private const long MaximumLogBytes = 1_000_000;
    private static readonly object Sync = new();
    private static string? _logPath;

    public static string LogPath => _logPath ?? "";

    public static void Initialize()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeamsIO",
            "Logs");
        Directory.CreateDirectory(logDirectory);
        _logPath = Path.Combine(logDirectory, "TeamsIO.log");
        RotateIfNeeded();
        Info($"TeamsIO {AppInfo.CurrentVersion} started.");
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception) =>
        Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        if (_logPath is null)
            return;

        var entry = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("O"))
            .Append(" [")
            .Append(level)
            .Append("] ")
            .AppendLine(message);

        if (exception is not null)
            entry.AppendLine(exception.ToString());

        try
        {
            lock (Sync)
            {
                File.AppendAllText(_logPath, entry.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never bring down the application.
        }
    }

    private static void RotateIfNeeded()
    {
        if (_logPath is null ||
            !File.Exists(_logPath) ||
            new FileInfo(_logPath).Length < MaximumLogBytes)
        {
            return;
        }

        File.Move(_logPath, _logPath + ".old", overwrite: true);
    }
}
