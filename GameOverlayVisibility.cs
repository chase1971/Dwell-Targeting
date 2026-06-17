using Godot;

namespace DwellTargeting;

/// <summary>
/// Detects when game menus/settings are open so overlays can step aside.
/// </summary>
internal static class GameOverlayVisibility
{
    private static readonly string[] BlockingTypeNames =
    [
        "SettingsScreen",
        "NSettingsScreen",
        "NPauseMenu",
        "PauseMenu",
        "NOptionsScreen",
        "OptionsScreen"
    ];

    private static long _lastCheckTicks;
    private static bool _menuOpen;
    private const int MenuCheckIntervalMs = 500;

    internal static bool ShouldHideOverlays()
    {
        if (!SettingsStore.Current.HideOverlaysInMenus)
            return false;

        return IsBlockingMenuOpen();
    }

    internal static bool IsBlockingMenuOpen()
    {
        long now = System.Environment.TickCount64;
        if (now - _lastCheckTicks < MenuCheckIntervalMs)
            return _menuOpen;

        _lastCheckTicks = now;
        _menuOpen = DetectBlockingMenuOpen();
        return _menuOpen;
    }

    private static bool DetectBlockingMenuOpen()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        return Walk(tree.Root);
    }

    private static bool Walk(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return false;

        if (node is CanvasItem canvas && NodeQuery.IsVisible(canvas) && IsBlockingNode(node))
            return true;

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

    private static bool IsBlockingNode(Node node)
    {
        string typeName = node.GetType().Name;
        foreach (string blockingName in BlockingTypeNames)
        {
            if (typeName.Contains(blockingName, StringComparison.Ordinal))
                return true;
        }

        string nodeName = node.Name;
        return nodeName.Contains("SettingsTab", StringComparison.OrdinalIgnoreCase)
            || nodeName.Contains("ModConfig", StringComparison.OrdinalIgnoreCase);
    }
}
