using Godot;

namespace DwellTargeting;

/// <summary>
/// Large ▲/▼ hover zones for wheel-style scrolling without dragging.
/// Map: one pair on the left (inset toward the map) and one on the right (original position).
/// Pile/card selection screens: one centered pair on the left.
/// </summary>
internal static class LeftHoverScrollOverlay
{
    private const int CanvasLayerOrder = 129;
    private const int ArrowSize = 70;
    private const int ArrowGap = 18;
    private const float EdgeMargin = 40f;
    private const float MapLeftInset = 130f;
    private readonly record struct StripPlacement(float CenterYFraction, bool OnRightEdge);

    private static readonly StripPlacement[] MapPlacements =
    [
        new(0.5f, OnRightEdge: false),
        new(0.5f, OnRightEdge: true),
    ];

    private static readonly StripPlacement[] PilePlacements = [new(0.5f, OnRightEdge: false)];

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly List<(Button Up, Button Down)> _strips = [];
    private static int _scrollFrameCounter;
    private static long _nextScrollLogTick;
    private static string _activeTag = string.Empty;
    private const float ScrollRatePerFrame = 1f / 6f;

    internal static void SyncMap()
    {
        _activeTag = "Map";
        SyncStrips(MapPlacements);
    }

    internal static void SyncPileSelect()
    {
        _activeTag = "Pile";
        SyncStrips(PilePlacements);
    }

    internal static void UpdateFrame()
    {
        if (_root == null || !_root.Visible || _strips.Count == 0)
            return;

        HandleHoverScroll();
    }

    internal static void Hide()
    {
        _activeTag = string.Empty;
        _scrollFrameCounter = 0;
        HoverScrollPacer.Reset();

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void SyncStrips(StripPlacement[] placements)
    {
        EnsureCanvas();
        if (_root == null)
            return;

        EnsureStripCount(placements.Length);
        PositionStrips(placements);
        _root.Visible = true;
        HandleHoverScroll();
    }

    private static void HandleHoverScroll()
    {
        var mouse = DwellHoverService.GetMousePosition();
        if (mouse == null)
        {
            _scrollFrameCounter = 0;
            HoverScrollPacer.Reset();
            return;
        }

        bool overUp = false;
        bool overDown = false;
        foreach (var (up, down) in _strips)
        {
            if (up.Visible && up.GetGlobalRect().HasPoint(mouse.Value))
                overUp = true;
            if (down.Visible && down.GetGlobalRect().HasPoint(mouse.Value))
                overDown = true;
        }

        if (overUp && overDown)
        {
            _scrollFrameCounter = 0;
            HoverScrollPacer.Reset();
            return;
        }

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
            ModLogger.Info($"[{_activeTag}Scroll] scrolling {(overUp ? "up" : "down")}.");
        }
    }

    private static void PositionStrips(StripPlacement[] placements)
    {
        if (_root == null)
            return;

        var size = _root.GetViewportRect().Size;

        for (int i = 0; i < _strips.Count; i++)
        {
            var (up, down) = _strips[i];
            if (i >= placements.Length)
            {
                up.Visible = false;
                down.Visible = false;
                continue;
            }

            var placement = placements[i];
            float x = placement.OnRightEdge
                ? size.X - EdgeMargin - ArrowSize
                : _activeTag == "Map"
                    ? MapLeftInset
                    : EdgeMargin;
            float centerY = size.Y * placement.CenterYFraction;

            up.Size = new Vector2(ArrowSize, ArrowSize);
            down.Size = new Vector2(ArrowSize, ArrowSize);
            up.GlobalPosition = new Vector2(x, centerY - ArrowSize - (ArrowGap / 2f));
            down.GlobalPosition = new Vector2(x, centerY + (ArrowGap / 2f));
            up.Visible = true;
            down.Visible = true;
        }
    }

    private static void EnsureStripCount(int count)
    {
        EnsureCanvas();
        if (_root == null)
            return;

        while (_strips.Count < count)
        {
            int index = _strips.Count + 1;
            var up = CreateArrow($"ScrollUp{index}", "▲");
            var down = CreateArrow($"ScrollDown{index}", "▼");
            _root.AddChild(up);
            _root.AddChild(down);
            _strips.Add((up, down));
        }
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellHoverScrollLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellHoverScrollRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);
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
