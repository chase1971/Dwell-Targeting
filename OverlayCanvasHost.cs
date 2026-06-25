using Godot;

namespace DwellTargeting;

/// <summary>
/// Owns the long-lived nodes the overlay attaches to the game's scene root: the input
/// router (captures dwell clicks) and the fallback CanvasLayer + Control that button rows
/// parent to when a card holder can't host them directly. Extracted from
/// <see cref="HandTargetingOverlay"/> so that class stays a per-frame coordinator.
/// </summary>
internal static class OverlayCanvasHost
{
    private const int CanvasLayerOrder = 128;

    private static DwellInputRouter? _inputRouter;
    private static CanvasLayer? _layer;
    private static Control? _fallbackRoot;

    /// <summary>The full-rect Control that overlay button rows fall back to when unparented.</summary>
    internal static Control? FallbackRoot => _fallbackRoot;

    /// <summary>Attach the dwell input router to the scene root if it isn't already live.</summary>
    internal static void EnsureInputRouter()
    {
        if (_inputRouter != null && NodeQuery.IsLive(_inputRouter))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _inputRouter = new DwellInputRouter { Name = "DwellTargetingInputRouter", ProcessMode = Node.ProcessModeEnum.Always };
        tree.Root.AddChild(_inputRouter);
        ModLogger.Info("Input router attached to scene root.");
    }

    /// <summary>Create the fallback CanvasLayer + root Control if they aren't already live.</summary>
    internal static void EnsureFallbackCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _fallbackRoot != null && NodeQuery.IsLive(_fallbackRoot))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellTargetingLayer" };
        tree.Root.AddChild(_layer);

        _fallbackRoot = new Control
        {
            Name = "DwellTargetingRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _fallbackRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_fallbackRoot);
        ModLogger.Info($"CanvasLayer created at layer {CanvasLayerOrder}.");
    }
}
