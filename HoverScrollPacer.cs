namespace DwellTargeting;

/// <summary>
/// Fractional frame pacing for hover-to-scroll (map, deck, card grids). At scale 1.0 the map
/// strip matches the old every-2-frames rate; default 0.8 is ~20% slower.
/// </summary>
internal static class HoverScrollPacer
{
    private static float _accumulator;

    internal static void Reset() => _accumulator = 0f;

    /// <param name="baseRatePerFrame">
    /// Scroll credits added per hover frame at scale 1.0 (map strip = 0.5 → one tick every 2 frames).
    /// </param>
    internal static bool TryConsumeScrollTick(float baseRatePerFrame)
    {
        _accumulator += baseRatePerFrame * SettingsStore.GetHoverScrollSpeedScale();
        if (_accumulator < 1f)
            return false;

        _accumulator -= 1f;
        return true;
    }
}
