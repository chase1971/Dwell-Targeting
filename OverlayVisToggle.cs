using Godot;

namespace DwellTargeting;

/// <summary>
/// Small dwell control beside the map icon. Toggles every overlay visual (numbered buttons, enemy labels,
/// green debug outlines) invisible while dwell targets keep working. This button always stays visible.
/// </summary>
internal static class OverlayVisToggle
{
    private const int ButtonSize = 40;
    private const int CanvasLayerOrder = 133;
    private const float MapGap = 4f;
    private const float FallbackMargin = 8f;

    private static CanvasLayer? _layer;
    private static Button? _button;
    private static Rect2 _bounds;
    private static bool _initialized;
    private static bool _positionCached;
    private static Rect2 _cachedMapAnchor;

    internal static void EnsureInitialized()
    {
        if (_initialized)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellOverlayVisLayer" };
        tree.Root.AddChild(_layer);

        _button = new Button
        {
            Name = "DwellOverlayVisToggle",
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(ButtonSize, ButtonSize),
            TooltipText = "Hide/show all overlay visuals. Dwell still works when hidden."
        };
        ApplyStyle(_button);
        _button.Pressed += Toggle;
        _layer.AddChild(_button);

        RefreshLabel();
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

        RefreshLabel();
        PositionButton();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_bounds.Size.X < 1)
            return;

        targets.Add(DwellHoverService.Menu(_bounds, Toggle, "OverlayVisToggle"));
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (_bounds.Size.X < 1 || !_bounds.HasPoint(globalPos))
            return false;

        if (!DwellActivationCooldown.TryRunMenuAction(Toggle))
            return false;

        message = "Overlay visibility toggled";
        return true;
    }

    private static void Toggle()
    {
        bool next = !SettingsStore.Current.ShowOverlays;
        SettingsStore.ApplyShowOverlays(next, persist: true, syncModConfig: true);
        ModLogger.Info($"Overlay visuals {(next ? "shown" : "hidden")} — dwell still active.");
        RefreshLabel();
    }

    private static void RefreshLabel()
    {
        if (_button == null || !NodeQuery.IsLive(_button))
            return;

        _button.Text = SettingsStore.Current.ShowOverlays ? "ON" : "OFF";
        ApplyStyle(_button);
    }

    private static void PositionButton()
    {
        if (_button == null || !NodeQuery.IsLive(_button))
            return;

        Vector2 pos;
        if (UtilityBarOverlay.TryGetAnchorRect("map", out var mapRect))
        {
            if (_positionCached && _cachedMapAnchor == mapRect)
                return;

            _cachedMapAnchor = mapRect;
            pos = new Vector2(
                mapRect.Position.X - ButtonSize - MapGap,
                mapRect.Position.Y + ((mapRect.Size.Y - ButtonSize) / 2f));
        }
        else
        {
            if (_positionCached)
                return;

            var tree = Engine.GetMainLoop() as SceneTree;
            var viewportSize = tree?.Root?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            pos = new Vector2(viewportSize.X - ButtonSize - FallbackMargin, FallbackMargin);
        }

        _button.GlobalPosition = pos;
        _button.ResetSize();
        _button.Size = _button.GetCombinedMinimumSize();
        _bounds = _button.GetGlobalRect();
        _positionCached = true;
    }

    private static void ApplyStyle(Button button)
    {
        bool overlaysOn = SettingsStore.Current.ShowOverlays;
        var style = new StyleBoxFlat
        {
            BgColor = overlaysOn
                ? new Color(0.10f, 0.22f, 0.14f, 0.92f)
                : new Color(0.24f, 0.12f, 0.12f, 0.92f),
            BorderColor = overlaysOn
                ? new Color(0.35f, 0.95f, 0.45f, 1f)
                : new Color(0.95f, 0.45f, 0.35f, 1f),
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
        button.AddThemeFontSizeOverride("font_size", 12);
    }
}
