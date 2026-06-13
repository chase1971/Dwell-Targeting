namespace DwellTargeting;

internal static class HandBlockPolicy
{
    /// <summary>Hand stays unlocked — vanilla card hover/click/read; dwell buttons use API/keys.</summary>
    internal static bool ShouldBlockHandInput(OverlayMode mode) => false;

    internal static bool ShouldBlockMouseCardPlay(OverlayMode mode) => false;

    internal static bool ShouldConsumeHandClicks(OverlayMode mode) => false;
}
