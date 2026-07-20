using Godot;

namespace DwellTargeting;

/// <summary>
/// Single left-side ▲/▼ hover scroll strip. Only one instance must be active at a time — two strips
/// break wheel scrolling (see map dual-strip regression).
/// </summary>
internal static class HoverScrollStripOverlay
{
    private const int CanvasLayerOrder = 129;
    private const int ArrowSize = 70;
    private const int ArrowGap = 18;
    private const float DefaultLeftMargin = 130f;
    private static CanvasLayer? _layer;
    private static Control? _root;
    private static Button? _upArrow;
    private static Button? _downArrow;
    private static int _scrollFrameCounter;
    private static long _nextScrollLogTick;
    private static string _tag = string.Empty;
    private static float _leftMargin = DefaultLeftMargin;
    private const float ScrollRatePerFrame = 0.5f;

    internal static void Sync(string tag, float leftMargin = DefaultLeftMargin)
    {
        _tag = tag;
        _leftMargin = leftMargin;

        EnsureCanvas();
        if (_root != null)
            _root.Visible = true;

        PositionArrows();
        HandleHoverScroll();
    }

    internal static void Hide()
    {
        _tag = string.Empty;
        _scrollFrameCounter = 0;
        HoverScrollPacer.Reset();

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void HandleHoverScroll()
    {
        if (_upArrow == null || _downArrow == null)
            return;

        var mouse = DwellHoverService.GetMousePosition();
        if (mouse == null)
        {
            _scrollFrameCounter = 0;
            HoverScrollPacer.Reset();
            return;
        }

        bool overUp = _upArrow.Visible && _upArrow.GetGlobalRect().HasPoint(mouse.Value);
        bool overDown = _downArrow.Visible && _downArrow.GetGlobalRect().HasPoint(mouse.Value);

        if (!overUp && !overDown)
        {
            _scrollFrameCounter = 0;
            HoverScrollPacer.Reset();
            return;
        }

        _scrollFrameCounter++;
        if (!HoverScrollPacer.TryConsumeScrollTick(ScrollRatePerFrame))
            return;

        MapScrollService.Scroll(overUp);

        long now = System.Environment.TickCount64;
        if (now >= _nextScrollLogTick)
        {
            _nextScrollLogTick = now + 1000;
            ModLogger.Info($"[{_tag}Scroll] scrolling {(overUp ? "up" : "down")}.");
        }
    }

    private static void PositionArrows()
    {
        if (_root == null || _upArrow == null || _downArrow == null)
            return;

        var size = _root.GetViewportRect().Size;
        float x = _leftMargin;
        float centerY = size.Y / 2f;

        _upArrow.Size = new Vector2(ArrowSize, ArrowSize);
        _downArrow.Size = new Vector2(ArrowSize, ArrowSize);
        _upArrow.GlobalPosition = new Vector2(x, centerY - ArrowSize - (ArrowGap / 2f));
        _downArrow.GlobalPosition = new Vector2(x, centerY + (ArrowGap / 2f));
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root)
            && _upArrow != null && NodeQuery.IsLive(_upArrow) && _downArrow != null && NodeQuery.IsLive(_downArrow))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellHoverScrollStripLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellHoverScrollStripRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _upArrow = CreateArrow("HoverScrollUp", "▲");
        _downArrow = CreateArrow("HoverScrollDown", "▼");
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
