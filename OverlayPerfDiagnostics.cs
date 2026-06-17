using System.Diagnostics;

namespace DwellTargeting;

/// <summary>
/// Throttled per-frame timing for overlay hot paths (logged every 3 seconds).
/// </summary>
internal static class OverlayPerfDiagnostics
{
    private const int LogIntervalMs = 3000;

    private static long _lastLogTicks;
    private static int _frameCount;
    private static double _totalMs;
    private static double _getModeMs;
    private static double _handSyncMs;
    private static double _stylesMs;
    private static double _dwellMs;

    internal static bool Enabled => SettingsStore.Current.EnablePerfLogging;

    internal static long BeginTick() => Enabled ? Stopwatch.GetTimestamp() : 0;

    internal static void AddCategory(string category, long startTicks)
    {
        if (!Enabled || startTicks == 0)
            return;

        double ms = (Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency;
        switch (category)
        {
            case "getMode":
                _getModeMs += ms;
                break;
            case "handSync":
                _handSyncMs += ms;
                break;
            case "styles":
                _stylesMs += ms;
                break;
            case "dwell":
                _dwellMs += ms;
                break;
            case "total":
                _totalMs += ms;
                break;
        }
    }

    internal static void EndFrame(long frameStartTicks)
    {
        if (!Enabled)
            return;

        AddCategory("total", frameStartTicks);
        _frameCount++;

        long now = Environment.TickCount64;
        if (now - _lastLogTicks < LogIntervalMs)
            return;

        if (_frameCount == 0)
            return;

        double frames = _frameCount;
        ModLogger.Info(
            $"Perf avg/ms frame total={_totalMs / frames:F3} getMode={_getModeMs / frames:F3} " +
            $"styles={_stylesMs / frames:F3} handSync={_handSyncMs / frames:F3} dwell={_dwellMs / frames:F3}");

        _frameCount = 0;
        _totalMs = 0;
        _getModeMs = 0;
        _handSyncMs = 0;
        _stylesMs = 0;
        _dwellMs = 0;
        _lastLogTicks = now;
    }
}
