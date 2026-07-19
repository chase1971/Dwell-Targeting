using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// One hover-scroll strip for scrollable views (deck, map, card piles). Uses cached view state
/// from ViewScreenQuery / OverlayModeService — no per-frame root tree walks.
/// </summary>
internal static class ViewScrollOverlay
{
    private const float MapLeftMargin = 130f;
    private const float DefaultLeftMargin = 130f;

    internal static void Sync()
    {
        if (!TryGetActiveContext(out string tag, out float leftMargin))
            HoverScrollStripOverlay.Hide();
        else
            HoverScrollStripOverlay.Sync(tag, leftMargin);
    }

    internal static void Hide() => HoverScrollStripOverlay.Hide();

    private static bool TryGetActiveContext(out string tag, out float leftMargin)
    {
        tag = string.Empty;
        leftMargin = DefaultLeftMargin;

        var mode = OverlayModeService.GetMode();

        if (mode == OverlayMode.Rewards)
            return false;

        if (mode == OverlayMode.PileSelect && TryGetPileScrollContext(out tag))
            return true;

        if (mode == OverlayMode.Map || OverlayModeService.GetCachedMapScreen() != null)
        {
            tag = "Map";
            leftMargin = MapLeftMargin;
            return true;
        }

        if (!ViewScreenQuery.IsViewingScreenOpen())
            return false;

        if (ViewScreenQuery.IsDeckViewOpen())
        {
            tag = "Deck";
            return true;
        }

        tag = "Pile";
        return true;
    }

    private static bool TryGetPileScrollContext(out string tag)
    {
        tag = "Pile";

        if (!OverlayModeService.TryGetPileSelectScreen(out var screen))
            return false;

        return PileSelectOverlay.IsScrollableGridScreen(screen);
    }
}
