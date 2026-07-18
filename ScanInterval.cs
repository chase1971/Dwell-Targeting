namespace DwellTargeting;

/// <summary>
/// Central scan cadence from settings — one slider tunes every throttled tree walk.
/// </summary>
internal static class ScanInterval
{
    /// <summary>Base frame interval between scene-tree rescans (60 FPS → frames ≈ seconds × 60).</summary>
    internal static int Frames(float multiplier = 1f)
    {
        int baseFrames = SettingsStore.GetTreeScanIntervalFrames();
        return Math.Max(2, (int)Math.Round(baseFrames * multiplier));
    }

    /// <summary>OverlayModeService cache TTL derived from the same setting.</summary>
    internal static int ModeCacheMs() =>
        Math.Clamp(Frames() * 16, 120, 2400);
}
