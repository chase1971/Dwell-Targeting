using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace DwellTargeting;

/// <summary>
/// Map screen helpers: dwell targets over travelable nodes, direct dwell on Proceed/Skip, plus one
/// ▲/▼ hover-scroll pair on the left (same proven single-strip path as the original right-side scroll).
/// </summary>
internal static class MapOverlay
{
    private const int RescanIntervalFrames = 10;
    private const int CanvasLayerOrder = 130;
    private const int ArrowSize = 70;
    private const int ArrowGap = 18;
    private const float ArrowLeftMargin = 130f;
    private const int ScrollIntervalFrames = 3;
    private const float ProceedHitboxPadding = 24f;

    private static NMapScreen? _screen;
    private static List<NMapPoint>? _cachedPoints;
    private static NProceedButton? _proceedButton;
    private static int _framesSinceScan;
    private static int _scrollFrameCounter;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static Button? _upArrow;
    private static Button? _downArrow;
    private static long _nextScrollLogTick;
    private static long _nextSyncLogTick;

    internal static void Sync()
    {
        _screen = OverlayModeService.GetCachedMapScreen();
        if (_screen == null)
        {
            Hide();
            return;
        }

        EnsureCanvas();
        LeftHoverScrollOverlay.Hide();
        bool deckViewOpen = CombatViewSuppressionQuery.IsDeckViewOpen();
        if (!deckViewOpen)
            HoverScrollStripOverlay.Hide();

        if (_root != null)
            _root.Visible = true;
        if (!deckViewOpen)
        {
            PositionArrows();
            HandleHoverScroll();
        }
        else if (_upArrow != null && _downArrow != null)
        {
            _upArrow.Visible = false;
            _downArrow.Visible = false;
            _scrollFrameCounter = 0;
        }

        _framesSinceScan++;
        if (_cachedPoints == null || _framesSinceScan >= RescanIntervalFrames)
        {
            _framesSinceScan = 0;
            _cachedPoints = FindTravelablePoints(_screen);
            _proceedButton = FindProceedButton();
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextSyncLogTick)
        {
            _nextSyncLogTick = now + 1500;
            ModLogger.Info(
                $"[Map] sync travelable={_cachedPoints?.Count ?? -1} proceed={(_proceedButton != null)} " +
                $"upArrowPos=({_upArrow?.GlobalPosition.X:F0},{_upArrow?.GlobalPosition.Y:F0}).");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_screen == null || !NodeQuery.IsLive(_screen) || _cachedPoints == null)
            return;

        int slot = 1;
        foreach (var point in _cachedPoints)
        {
            if (!NodeQuery.IsLive(point) || !NodeQuery.IsVisible(point) || point.State != MapPointState.Travelable)
                continue;

            if (!ControlHitboxService.TryGetDwellRect(point, out var rect))
                continue;

            var captured = point;
            targets.Add(DwellHoverService.Card(rect, () => MapSelectionService.TrySelect(captured), $"MapNode:{slot}"));
            slot++;
        }

        if (TryGetProceedRect(out var proceedRect))
            targets.Add(DwellHoverService.Menu(proceedRect, ActivateProceed, "MapProceed"));
    }

    internal static void Hide()
    {
        _screen = null;
        _cachedPoints = null;
        _proceedButton = null;
        _framesSinceScan = 0;
        _scrollFrameCounter = 0;

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void HandleHoverScroll()
    {
        if (_upArrow == null || _downArrow == null)
            return;

        var mouse = DwellHoverService.GetMousePosition();
        if (mouse == null)
            return;

        bool overUp = _upArrow.Visible && _upArrow.GetGlobalRect().HasPoint(mouse.Value);
        bool overDown = _downArrow.Visible && _downArrow.GetGlobalRect().HasPoint(mouse.Value);

        if (!overUp && !overDown)
        {
            _scrollFrameCounter = 0;
            return;
        }

        _scrollFrameCounter++;
        if (_scrollFrameCounter % ScrollIntervalFrames != 0)
            return;

        MapScrollService.Scroll(overUp);

        long now = System.Environment.TickCount64;
        if (now >= _nextScrollLogTick)
        {
            _nextScrollLogTick = now + 1000;
            ModLogger.Info($"[MapScroll] scrolling {(overUp ? "up" : "down")}.");
        }
    }

    private static void PositionArrows()
    {
        if (_root == null || _upArrow == null || _downArrow == null)
            return;

        var size = _root.GetViewportRect().Size;
        float x = ArrowLeftMargin;
        float centerY = size.Y / 2f;

        _upArrow.Size = new Vector2(ArrowSize, ArrowSize);
        _downArrow.Size = new Vector2(ArrowSize, ArrowSize);
        _upArrow.GlobalPosition = new Vector2(x, centerY - ArrowSize - (ArrowGap / 2f));
        _downArrow.GlobalPosition = new Vector2(x, centerY + (ArrowGap / 2f));
    }

    private static NProceedButton? FindProceedButton()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var button in NodeQuery.FindAll<NProceedButton>(root))
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;

            return button;
        }

        return null;
    }

    private static bool TryGetProceedRect(out Rect2 rect)
    {
        rect = default;
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton) || !NodeQuery.IsVisible(_proceedButton))
            return false;

        if (_proceedButton is NClickableControl { IsEnabled: false })
            return false;

        rect = _proceedButton.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(ProceedHitboxPadding);
        return true;
    }

    private static void ActivateProceed()
    {
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton))
        {
            ModLogger.Warn("[Map] Proceed dwell fired but proceed button missing/dead.");
            return;
        }

        RewardSelectionService.TryProceed(_proceedButton);
    }

    private static List<NMapPoint> FindTravelablePoints(NMapScreen screen) =>
        NodeQuery.FindAll<NMapPoint>(screen)
            .Where(p => NodeQuery.IsVisible(p) && p.State == MapPointState.Travelable)
            .ToList();

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root)
            && _upArrow != null && NodeQuery.IsLive(_upArrow) && _downArrow != null && NodeQuery.IsLive(_downArrow))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellMapLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellMapRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _upArrow = CreateArrow("MapScrollUp", "▲");
        _downArrow = CreateArrow("MapScrollDown", "▼");
        _root.AddChild(_upArrow);
        _root.AddChild(_downArrow);
    }

    private static Button CreateArrow(string name, string glyph)
    {
        var button = new Button
        {
            Name = name,
            Text = glyph,
            CustomMinimumSize = new Vector2(ArrowSize, ArrowSize),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.1f, 0.14f, 0.9f),
            BorderColor = new Color(0.45f, 0.85f, 1f, 1f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10
        };
        button.AddThemeStyleboxOverride("normal", style);
        button.AddThemeStyleboxOverride("hover", style);
        button.AddThemeStyleboxOverride("pressed", style);
        button.AddThemeStyleboxOverride("focus", style);
        button.AddThemeFontSizeOverride("font_size", ArrowSize / 2);
        return button;
    }
}
