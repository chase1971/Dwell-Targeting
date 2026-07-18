using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// Detects the card / deck / draw / exhaust pile views and the map opened *on top of an active
/// combat*. Combat is still "in progress" underneath, so the overlay would otherwise keep drawing
/// its per-card play buttons over the viewed pile — the bleed-through the user reported. While one
/// of these views is up we suppress the combat overlay (but keep the universal Back button so the
/// view can be closed hands-free).
/// </summary>
internal static class CombatViewSuppressionQuery
{
    internal static bool DetectViewingScreenNow()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        return root != null && Walk(root);
    }

    private static bool Walk(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return false;

        if (node is CanvasItem canvas && NodeQuery.IsVisible(canvas))
        {
            if (node is NCardsViewScreen or NDeckViewScreen or NCardPileScreen or NSimpleCardsViewScreen)
                return true;
            if (node is NMapScreen { IsOpen: true })
                return true;
        }

        try
        {
            foreach (var child in node.GetChildren())
            {
                if (Walk(child))
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
