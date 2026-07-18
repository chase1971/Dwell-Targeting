using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// One hover-scroll strip for scrollable views (deck, map, card piles). Card-pick drafts that
/// are not scrollable stay excluded; upgrade/removal grids with clipped rows get scroll.
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

        if (IsCardGridViewOpen())
        {
            tag = "Pile";
            return true;
        }

        return false;
    }

    private static bool TryGetPileScrollContext(out string tag)
    {
        tag = "Pile";
        if (!OverlayModeService.TryGetPileSelectScreen(out var screen))
            return false;

        return screen is NCardGridSelectionScreen or NChooseACardSelectionScreen;
    }

    private static bool IsCardGridViewOpen()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return false;

        return WalkForCardGrid(root);
    }

    private static bool WalkForCardGrid(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return false;

        if (node is CanvasItem canvas && NodeQuery.IsVisible(canvas)
            && node is NCardsViewScreen or NCardPileScreen or NSimpleCardsViewScreen)
        {
            return true;
        }

        try
        {
            foreach (var child in node.GetChildren())
            {
                if (WalkForCardGrid(child))
                    return true;
            }
        }
        catch
        {
            /* disposed mid-walk */
        }

        return false;
    }
}
