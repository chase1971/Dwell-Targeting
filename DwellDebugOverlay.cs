using Godot;

namespace DwellTargeting;

/// <summary>
/// Debug visualization that draws the live dwell target rectangles (plus their names) on screen so hitbox
/// alignment can be verified against the actual cards/buttons. Requires ModConfig "Show Hitbox Outlines"
/// and overlay visuals ON (map toggle).
/// Pools <see cref="Panel"/> outlines + <see cref="Label"/>s; nothing is interactive (mouse-filter Ignore).
/// </summary>
internal static class DwellDebugOverlay
{
    private const int CanvasLayerOrder = 200;

    private const float MarkerSize = 18f;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static Panel? _mouseMarker;
    private static readonly List<Panel> _boxes = new();
    private static readonly List<Label> _labels = new();

    internal static void Render(IReadOnlyList<DwellHoverService.Target> targets)
    {
        if (!SettingsStore.ShouldDrawHitboxDebug() || targets.Count == 0)
        {
            Hide();
            return;
        }

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;
        EnsurePool(targets.Count);

        for (int i = 0; i < _boxes.Count; i++)
        {
            var box = _boxes[i];
            var label = _labels[i];

            if (i >= targets.Count)
            {
                box.Visible = false;
                label.Visible = false;
                continue;
            }

            var bounds = targets[i].Bounds;
            box.GlobalPosition = bounds.Position;
            box.Size = bounds.Size;
            box.Visible = true;

            label.Text = targets[i].Name;
            label.GlobalPosition = new Vector2(bounds.Position.X + 3f, bounds.Position.Y + 2f);
            label.Visible = true;
        }

        UpdateMouseMarker();
    }

    private static void UpdateMouseMarker()
    {
        if (_mouseMarker == null)
            return;

        var mouse = DwellHoverService.GetMousePosition();
        if (mouse == null)
        {
            _mouseMarker.Visible = false;
            return;
        }

        // Red dot drawn (in screen space) where the mod reads the cursor. If it sits under the real cursor,
        // the input space matches the draw space and only the target rects are wrong.
        _mouseMarker.GlobalPosition = mouse.Value - new Vector2(MarkerSize / 2f, MarkerSize / 2f);
        _mouseMarker.Visible = true;
    }

    internal static void Hide()
    {
        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void EnsurePool(int count)
    {
        while (_boxes.Count < count)
        {
            var box = new Panel
            {
                Name = $"DwellDebugBox{_boxes.Count}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 1
            };
            box.AddThemeStyleboxOverride("panel", CreateBoxStyle());
            _root!.AddChild(box);
            _boxes.Add(box);

            var label = new Label
            {
                Name = $"DwellDebugLabel{_labels.Count}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 2
            };
            label.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.4f, 1f));
            label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 1f));
            label.AddThemeConstantOverride("outline_size", 4);
            label.AddThemeFontSizeOverride("font_size", 15);
            _root!.AddChild(label);
            _labels.Add(label);
        }
    }

    private static StyleBoxFlat CreateBoxStyle() => new()
    {
        BgColor = new Color(0.20f, 1f, 0.45f, 0.16f),
        BorderColor = new Color(0.20f, 1f, 0.45f, 0.95f),
        BorderWidthBottom = 2,
        BorderWidthTop = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2
    };

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellDebugLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellDebugRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _boxes.Clear();
        _labels.Clear();

        _mouseMarker = new Panel
        {
            Name = "DwellDebugMouse",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(MarkerSize, MarkerSize),
            ZIndex = 3
        };
        var markerStyle = new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.1f, 0.1f, 0.85f),
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9
        };
        _mouseMarker.AddThemeStyleboxOverride("panel", markerStyle);
        _root.AddChild(_mouseMarker);
    }
}
