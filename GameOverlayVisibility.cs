using Godot;

namespace DwellTargeting;

/// <summary>
/// Detects when game menus/settings are open so overlays can step aside.
/// Blocking menu nodes are found once (one tree walk), then only visibility is checked.
/// Empty scan results are cached too — re-walk only on mode change or a slow safety interval.
/// </summary>
internal static class GameOverlayVisibility
{
    private const long RescanIntervalMs = 500;

    private static readonly string[] BlockingTypeNames =
    [
        "SettingsScreen",
        "NSettingsScreen",
        "NPauseMenu",
        "PauseMenu",
        "NOptionsScreen",
        "OptionsScreen"
    ];

    private static readonly List<Node> _blockingNodes = new();
    private static bool _nodesCached;
    private static long _lastScanTick;

    internal static void InvalidateCache()
    {
        _nodesCached = false;
        _lastScanTick = 0;
    }

    internal static bool ShouldHideOverlays(bool blockingMenuOpen)
    {
        if (!SettingsStore.Current.HideOverlaysInMenus)
            return false;

        return blockingMenuOpen;
    }

    /// <summary>
    /// Call once per frame with the already-known pause-menu state to avoid duplicate tree walks.
    /// </summary>
    internal static bool ComputeBlockingMenuOpen(bool pauseMenuOpen)
    {
        if (SettingsOverlay.IsOpen || pauseMenuOpen)
            return true;

        if (!SettingsStore.Current.HideOverlaysInMenus)
            return false;

        EnsureBlockingNodesCached();
        PruneDeadNodes();

        foreach (var node in _blockingNodes)
        {
            if (node is CanvasItem canvas && NodeQuery.IsLive(node) && NodeQuery.IsVisible(canvas))
                return true;
        }

        return false;
    }

    /// <summary>One tree walk — collect every blocking menu node, then visibility-only checks.</summary>
    private static void EnsureBlockingNodesCached()
    {
        long now = System.Environment.TickCount64;
        if (_nodesCached && now - _lastScanTick < RescanIntervalMs)
            return;

        _blockingNodes.Clear();
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
        {
            _nodesCached = true;
            _lastScanTick = now;
            return;
        }

        CollectBlockingNodes(tree.Root);
        _nodesCached = true;
        _lastScanTick = now;
    }

    private static void PruneDeadNodes()
    {
        for (int i = _blockingNodes.Count - 1; i >= 0; i--)
        {
            if (!NodeQuery.IsLive(_blockingNodes[i]))
                _blockingNodes.RemoveAt(i);
        }
    }

    private static void CollectBlockingNodes(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (IsBlockingNode(node))
            _blockingNodes.Add(node);

        try
        {
            foreach (var child in node.GetChildren())
                CollectBlockingNodes(child);
        }
        catch
        {
            /* disposed mid-walk */
        }
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
