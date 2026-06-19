using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace DwellTargeting;

/// <summary>
/// Buffered, non-blocking logger. Game-thread callers only enqueue a string; a background timer
/// flushes batches to disk every <see cref="FlushIntervalMs"/> ms. This matters because the old
/// synchronous File.AppendAllText-per-line (plus GD.Print per line) ran on the game thread and was
/// itself a measurable source of the combat lag — especially while perf logging was on.
/// </summary>
internal static class ModLogger
{
    private const int FlushIntervalMs = 250;
    private const int MaxQueued = 8000;
    private const int MaxPerFlush = 4000;

    private static readonly string LogPath;
    private static readonly ConcurrentQueue<string> Queue = new();
    private static readonly Timer FlushTimer;
    private static int _queuedApprox;

    static ModLogger()
    {
        LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2",
            "logs",
            "dwell-targeting.log");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        }
        catch
        {
            /* never break gameplay for logging */
        }

        FlushTimer = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
    }

    internal static void Info(string message) => Enqueue("INFO", message, print: false);

    internal static void Warn(string message) => Enqueue("WARN", message, print: true);

    internal static void Error(string message) => Enqueue("ERROR", message, print: true);

    private static void Enqueue(string level, string message, bool print)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

        // Drop new lines if the writer is badly backed up — logging must never grow unbounded or
        // stall the game thread waiting on disk.
        if (Volatile.Read(ref _queuedApprox) < MaxQueued)
        {
            Queue.Enqueue(line);
            Interlocked.Increment(ref _queuedApprox);
        }

        // Console mirror only for problems; per-line GD.Print on hot-path INFO was part of the stall.
        if (print)
            Godot.GD.Print($"[DwellTargeting] {line}");
    }

    private static void Flush()
    {
        if (Queue.IsEmpty)
            return;

        var sb = new StringBuilder();
        int drained = 0;
        while (drained < MaxPerFlush && Queue.TryDequeue(out var line))
        {
            sb.Append(line).Append(Environment.NewLine);
            drained++;
        }

        if (drained == 0)
            return;

        Interlocked.Add(ref _queuedApprox, -drained);

        try
        {
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch
        {
            /* never break gameplay for logging */
        }
    }
}
