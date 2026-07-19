namespace DwellTargeting;

/// <summary>
/// Invalidates overlay discovery caches when the active screen mode changes so returning to a
/// screen (rest site → upgrade grid → rest site) always gets a fresh scan after settle.
/// </summary>
internal static class ScreenOverlayInvalidator
{
    internal static void OnModeEntered(OverlayMode mode)
    {
        BackButtonOverlay.InvalidateLookup();
        ViewScreenQuery.RequestScan();

        switch (mode)
        {
            case OverlayMode.Room:
                RoomOverlay.PrepareForEntry();
                break;
            case OverlayMode.PileSelect:
                PileSelectOverlay.PrepareForEntry();
                DeckViewOverlay.PrepareForEntry();
                break;
            case OverlayMode.Event:
                EventOverlay.PrepareForEntry();
                break;
            case OverlayMode.Shop:
                ShopOverlay.InvalidateDiscovery();
                break;
            case OverlayMode.Map:
                MapOverlay.PrepareForEntry();
                break;
            case OverlayMode.Rewards:
                RewardsOverlay.PrepareForEntry();
                break;
        }
    }
}
