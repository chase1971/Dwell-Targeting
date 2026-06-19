using System.Diagnostics;
using System.Text;

namespace DwellTargeting;

/// <summary>
/// Per-frame profiler for the overlay hot paths. Each named section accumulates elapsed time AND a
/// call count, so the 3-second summary shows both how expensive a thing is (ms/frame) and how often
/// it runs (calls/frame). Counters with no time (e.g. scene-tree walks / nodes visited) reveal how
/// much full-tree scanning happens each frame — usually the real source of combat lag.
/// Only active when Performance Logging is enabled in settings.
/// </summary>
internal static class OverlayPerfDiagnostics
{
    private const int LogIntervalMs = 3000;

    private static readonly object Lock = new();
    private static readonly Dictionary<string, Bucket> Buckets = new();
    private static long _lastLogTicks;
    private static int _frameCount;

    private struct Bucket
    {
        public double Ms;
        public long Count;
    }

    internal static bool Enabled => SettingsStore.Current.EnablePerfLogging;

    internal static long BeginTick() => Enabled ? Stopwatch.GetTimestamp() : 0;

    /// <summary>Accumulate elapsed time (ms) and one call for a named section.</summary>
    internal static void Add(string name, long startTicks)
    {
        if (!Enabled || startTicks == 0)
            return;

        double ms = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
        lock (Lock)
        {
            Buckets.TryGetValue(name, out var b);
            b.Ms += ms;
            b.Count++;
            Buckets[name] = b;
        }
    }

    /// <summary>Legacy alias for <see cref="Add"/>.</summary>
    internal static void AddCategory(string category, long startTicks) => Add(category, startTicks);

    /// <summary>Count an event without timing it (e.g. number of tree walks or nodes visited).</summary>
    internal static void Count(string name, long n = 1)
    {
        if (!Enabled || n == 0)
            return;

        lock (Lock)
        {
            Buckets.TryGetValue(name, out var b);
            b.Count += n;
            Buckets[name] = b;
        }
    }

    internal static void EndFrame(long frameStartTicks)
    {
        if (!Enabled)
            return;

        Add("total", frameStartTicks);
        _frameCount++;

        long now = Environment.TickCount64;
        if (now - _lastLogTicks < LogIntervalMs)
            return;

        string? report = null;
        lock (Lock)
        {
            if (_frameCount > 0 && Buckets.Count > 0)
                report = BuildReport();

            Buckets.Clear();
            _frameCount = 0;
            _lastLogTicks = now;
        }

        if (report != null)
            ModLogger.Info(report);
    }

    private static string BuildReport()
    {
        double frames = _frameCount;
        var sb = new StringBuilder();
        sb.Append($"Perf 3s over {_frameCount} frames (sorted by ms/frame):");

        foreach (var pair in Buckets.OrderByDescending(p => p.Value.Ms).ThenByDescending(p => p.Value.Count))
        {
            var b = pair.Value;
            sb.Append(Environment.NewLine);
            if (b.Ms > 0)
                sb.Append($"  {pair.Key,-20} ms/f={b.Ms / frames,8:F3}  calls/f={b.Count / frames,8:F2}");
            else
                sb.Append($"  {pair.Key,-20} {string.Empty,14}  calls/f={b.Count / frames,8:F2}");
        }

        return sb.ToString();
    }
}
