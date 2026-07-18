using Godot;

namespace DwellTargeting;

/// <summary>
/// Removes old offset-number canvas layers from builds before v0.10.63.
/// </summary>
internal static class LegacyOverlayCleanup
{
    internal static void RunOnce()
    {
        RemovePauseMenuCanvas();
        RemoveMainMenuCanvas();
    }

    internal static void RemovePauseMenuCanvas() => RemoveLayer("DwellPauseMenuLayer");

    internal static void RemoveMainMenuCanvas() => RemoveLayer("DwellMainMenuLayer");

    private static void RemoveLayer(string layerName)
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return;

        var layer = root.GetNodeOrNull(layerName);
        if (layer != null && NodeQuery.IsLive(layer))
            layer.QueueFree();
    }
}
