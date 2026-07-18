namespace DwellTargeting;

/// <summary>
/// Tracks full-screen pile/deck views opened on top of combat. Scans run only when a view open
/// is requested (utility bar, back closing) or mode changes — not on a timer.
/// </summary>
internal static class ViewScreenQuery
{
    private static bool _otherViewOpen;
    private static bool _scanPending = true;
    private static bool _deckLookupPending;

    internal static void RequestScan()
    {
        _scanPending = true;
        _deckLookupPending = true;
    }

    internal static void Invalidate()
    {
        _scanPending = true;
        _otherViewOpen = false;
    }

    internal static bool ConsumeDeckLookupRequest()
    {
        if (!_deckLookupPending)
            return false;

        _deckLookupPending = false;
        return true;
    }

    internal static bool IsDeckViewOpen() => DeckViewOverlay.IsOpen;

    internal static bool IsViewingScreenOpen()
    {
        if (DeckViewOverlay.IsOpen)
            return true;

        var mode = OverlayModeService.GetMode();
        if (mode == OverlayMode.Map)
            return true;

        if (!_scanPending)
            return _otherViewOpen;

        _scanPending = false;
        _otherViewOpen = CombatViewSuppressionQuery.DetectViewingScreenNow();
        return _otherViewOpen;
    }
}
