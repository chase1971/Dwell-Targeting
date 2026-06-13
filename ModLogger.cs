namespace DwellTargeting;

internal static class ModLogger
{
    private static readonly string LogPath;
    private static readonly object Lock = new();

    static ModLogger()
    {
        LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2",
            "logs",
            "dwell-targeting.log");
    }

    internal static void Info(string message) => Write("INFO", message);

    internal static void Warn(string message) => Write("WARN", message);

    internal static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                /* never break gameplay for logging */
            }
        }

        Godot.GD.Print($"[DwellTargeting] {line}");
    }
}
