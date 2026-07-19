using Godot;

namespace DwellTargeting;

/// <summary>
/// Dwell-to-trigger diagnostic rescan. Sits just left of the overlay visibility toggle.
/// </summary>
internal static class ManualRescanOverlay
{
    private const int ButtonSize = 40;
    private const int CanvasLayerOrder = 133;
    private const float Gap = 4f;

    private static CanvasLayer? _layer;
    private static Button? _button;
    private static Rect2 _bounds;
    private static bool _initialized;

    internal static void EnsureInitialized()
    {
        if (_initialized)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellManualScanLayer" };
        tree.Root.AddChild(_layer);

        _button = new Button
        {
            Name = "DwellManualScanButton",
            Text = "Scan",
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
            TooltipText = "Dwell to rescan this screen and log what the mod finds."
        };
        ApplyStyle(_button);
        _button.Pressed += () => ManualRescanService.Run();
        _layer.AddChild(_button);
        PositionButton();
        _initialized = true;
    }

    internal static void UpdateFrame()
    {
        if (!_initialized)
            EnsureInitialized();

        if (_layer != null && NodeQuery.IsLive(_layer))
            _layer.Visible = true;

        if (_button != null && NodeQuery.IsLive(_button))
            _button.Visible = true;

        PositionButton();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_bounds.Size.X < 1)
            return;

        targets.Add(DwellHoverService.Menu(_bounds, ManualRescanService.Run, "ManualScan"));
    }

    private static void PositionButton()
    {
        if (_button == null || !NodeQuery.IsLive(_button))
            return;

        Vector2 pos;
        if (UtilityBarOverlay.TryGetAnchorRect("map", out var mapRect))
        {
            pos = new Vector2(
                mapRect.Position.X - (ButtonSize * 2) - (Gap * 2),
                mapRect.Position.Y + ((mapRect.Size.Y - ButtonSize) / 2f));
        }
        else
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var viewportSize = tree?.Root?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            pos = new Vector2(viewportSize.X - (ButtonSize * 2) - 12f, 8f);
        }

        _button.GlobalPosition = pos;
        _button.ResetSize();
        _button.Size = _button.GetCombinedMinimumSize();
        _bounds = _button.GetGlobalRect();
    }

    private static void ApplyStyle(Button button)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.14f, 0.22f, 0.92f),
            BorderColor = new Color(0.45f, 0.65f, 0.95f, 1f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6
        };

        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("focus", style);
        button.AddThemeFontSizeOverride("font_size", 11);
    }
}
