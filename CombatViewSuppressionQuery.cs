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
    private const int CheckIntervalMs = 200;

    private static long _lastCheck;
    private static bool _open;

    internal static bool IsViewingScreenOpen()
    {
        long now = System.Environment.TickCount64;
        if (now - _lastCheck < CheckIntervalMs)
            return _open;

        _lastCheck = now;
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        _open = root != null && Walk(root);
        return _open;
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
