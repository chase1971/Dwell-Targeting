using Godot;

namespace DwellTargeting;

/// <summary>
/// Dwell-to-flag feedback beacon. Sits under the potion bar (fallback: top-center) so it is
/// reachable on most in-run screens without typing or screenshots.
/// </summary>
internal static class FeedbackBeaconOverlay
{
    private const int ButtonSize = 48;
    private const int CanvasLayerOrder = 134;
    private const float GapBelowPotions = 6f;
    private const long FlaggedFlashMs = 1500;

    private static CanvasLayer? _layer;
    private static Button? _button;
    private static Rect2 _bounds;
    private static bool _initialized;
    private static long _flaggedUntilMs;

    internal static void EnsureInitialized()
    {
        if (_initialized)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellFeedbackBeaconLayer" };
        tree.Root.AddChild(_layer);

        _button = new Button
        {
            Name = "DwellFeedbackBeaconButton",
            Text = "Flag",
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
            TooltipText = "Dwell to save diagnostic feedback (last ~20s + current state)."
        };
        ApplyStyle(_button);
        _button.Pressed += OnPressed;
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
        {
            _button.Visible = true;
            long now = System.Environment.TickCount64;
            if (now >= _flaggedUntilMs && _button.Text != "Flag")
                _button.Text = "Flag";
        }

        PositionButton();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_bounds.Size.X < 1)
            return;

        targets.Add(DwellHoverService.Menu(_bounds, FeedbackBeaconService.Fire, "FeedbackBeacon"));
    }

    private static void OnPressed()
    {
        FeedbackBeaconService.Fire();
        _flaggedUntilMs = System.Environment.TickCount64 + FlaggedFlashMs;
        if (_button != null && NodeQuery.IsLive(_button))
            _button.Text = "Flagged";
    }

    private static void PositionButton()
    {
        if (_button == null || !NodeQuery.IsLive(_button))
            return;

        Vector2 pos;
        if (PotionSlotOverlay.TryGetPotionBarRect(out var potionRect))
        {
            float x = potionRect.Position.X + ((potionRect.Size.X - ButtonSize) / 2f);
            float y = potionRect.End.Y + GapBelowPotions;
            pos = new Vector2(x, y);
        }
        else
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var viewportSize = tree?.Root?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            pos = new Vector2((viewportSize.X - ButtonSize) / 2f, 8f);
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
            BgColor = new Color(0.22f, 0.14f, 0.04f, 0.94f),
            BorderColor = new Color(1f, 0.72f, 0.22f, 1f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };

        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("focus", style);
        button.AddThemeFontSizeOverride("font_size", 12);
    }
}
