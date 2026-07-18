using Godot;

namespace DwellTargeting;

/// <summary>
/// ModConfig wraps options in its own ScrollContainer, which fights the game settings scroll.
/// Disable nested scrollbars so the outer settings panel scrolls everything.
/// </summary>
internal static class ModConfigScrollHarmonizer
{
    private static readonly HashSet<ulong> _harmonized = new();

    internal static void Sync(bool blockingMenuOpen)
    {
        if (!SettingsOverlay.IsOpen && !ModConfigBridge.IsRegistered)
        {
            _harmonized.Clear();
            return;
        }

        if (!SettingsOverlay.IsOpen && !blockingMenuOpen)
        {
            _harmonized.Clear();
            return;
        }

        if (_harmonized.Count > 0)
            return;

        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return;

        Walk(root, outerScroll: null);
    }

    private static void Walk(Node node, ScrollContainer? outerScroll)
    {
        if (!NodeQuery.IsLive(node))
            return;

        ScrollContainer? nextOuter = outerScroll;
        if (node is ScrollContainer scroll && NodeQuery.IsVisible(scroll))
        {
            if (outerScroll != null && IsModConfigScroll(scroll))
                DisableInnerScroll(scroll);

            nextOuter = scroll;
        }

        try
        {
            foreach (var child in node.GetChildren())
                Walk(child, nextOuter);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static bool IsModConfigScroll(ScrollContainer scroll)
    {
        for (var current = scroll.GetParent(); current != null; current = current.GetParent())
        {
            string typeName = current.GetType().FullName ?? string.Empty;
            string nodeName = current.Name;
            if (typeName.Contains("ModConfig", StringComparison.OrdinalIgnoreCase)
                || nodeName.Contains("ModConfig", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DisableInnerScroll(ScrollContainer scroll)
    {
        if (!_harmonized.Add(scroll.GetInstanceId()))
            return;

        // Godot ScrollMode.Disabled = 3 — let the outer settings panel scroll all mod options.
        scroll.Set("vertical_scroll_mode", 3);
        scroll.Set("horizontal_scroll_mode", 3);
    }
}
