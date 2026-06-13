using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Left-side dwell buttons for draw/discard/deck/exhaust/map/menu shortcuts.
/// Default keys match STS2 keyboard_mapping: A S D X M Escape.
/// </summary>
internal static class UtilityBarOverlay
{
    private const int CanvasLayerOrder = 127;
    private const int LeftMargin = 24;
    private const int TopFractionPercent = 34;
    private const int ButtonGap = 8;

    private static readonly Color UtilityBg = new(0.1f, 0.12f, 0.16f, 0.92f);
    private static readonly Color UtilityBorder = new(0.55f, 0.75f, 0.95f, 1f);

    private static CanvasLayer? _layer;
    private static VBoxContainer? _root;
    private static readonly Dictionary<string, Button> _buttons = new();
    private static int _buttonSize;

    internal static void Sync(bool visible)
    {
        if (!visible || !RunManager.Instance.IsInProgress)
        {
            Hide();
            return;
        }

        EnsureUi();
        if (_root == null)
            return;

        RebuildButtons();
        ApplyPresentation();
        PositionBar();
        _root.Visible = _buttons.Count > 0;
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_root == null || !NodeQuery.IsLive(_root) || !_root.Visible)
            return;

        foreach (var pair in _buttons)
        {
            var button = pair.Value;
            if (!NodeQuery.IsLive(button) || !button.Visible)
                continue;

            var rect = button.GetGlobalRect();
            string id = pair.Key;
            targets.Add(new DwellHoverService.Target(rect, () => Activate(id), $"Utility:{id}"));
        }
    }

    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var pair in _buttons)
        {
            var button = pair.Value;
            if (!NodeQuery.IsLive(button) || !button.Visible)
                continue;

            if (!button.GetGlobalRect().HasPoint(globalPos))
                continue;

            message = $"Utility {pair.Key} clicked";
            Activate(pair.Key);
            return true;
        }

        return false;
    }

    internal static bool ContainsPoint(Vector2 globalPos)
    {
        foreach (var button in _buttons.Values)
        {
            if (!NodeQuery.IsLive(button) || !button.Visible)
                continue;

            if (button.GetGlobalRect().HasPoint(globalPos))
                return true;
        }

        return false;
    }

    internal static void Hide()
    {
        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void EnsureUi()
    {
        if (_root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        if (_layer == null || !NodeQuery.IsLive(_layer))
        {
            _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellUtilityLayer" };
            tree.Root.AddChild(_layer);
        }

        _buttonSize = SettingsStore.GetUtilityButtonSize();
        _root = new VBoxContainer
        {
            Name = "DwellUtilityBar",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddThemeConstantOverride("separation", ButtonGap);
        _layer.AddChild(_root);
        ModLogger.Info("Utility bar overlay created.");
    }

    private static void ApplyPresentation()
    {
        int size = SettingsStore.GetUtilityButtonSize();
        if (size != _buttonSize)
        {
            _buttonSize = size;
            foreach (var button in _buttons.Values)
                OverlayButtonFactory.ApplySize(button, size);
        }

        int fontSize = Math.Clamp(_buttonSize / 5, 14, 28);
        float opacity = SettingsStore.GetMenuButtonOpacity();
        foreach (var button in _buttons.Values)
            OverlayButtonFactory.ApplyMenuStyle(button, UtilityBg, UtilityBorder, fontSize, opacity);
    }

    private static void RebuildButtons()
    {
        if (_root == null)
            return;

        var desired = GetDesiredButtons();
        foreach (var stale in _buttons.Keys.Where(k => !desired.Any(d => d.Id == k)).ToList())
        {
            if (_buttons.TryGetValue(stale, out var button) && NodeQuery.IsLive(button))
                button.QueueFree();
            _buttons.Remove(stale);
        }

        foreach (var spec in desired)
        {
            if (_buttons.ContainsKey(spec.Id))
            {
                _buttons[spec.Id].Visible = true;
                continue;
            }

            var button = OverlayButtonFactory.CreateMenuButton(
                $"DwellUtility_{spec.Id}",
                spec.Label,
                _buttonSize,
                UtilityBg,
                UtilityBorder,
                () => Activate(spec.Id));

            _root.AddChild(button);
            _buttons[spec.Id] = button;
        }

        foreach (var pair in _buttons)
            pair.Value.Visible = desired.Any(d => d.Id == pair.Key);
    }

    private static void PositionBar()
    {
        if (_root == null)
            return;

        var viewport = _root.GetViewportRect();
        _root.ResetSize();
        var size = _root.GetCombinedMinimumSize();
        _root.GlobalPosition = new Vector2(
            LeftMargin,
            viewport.Size.Y * (TopFractionPercent / 100f));
        _root.Size = size;
    }

    private static void Activate(string id)
    {
        Key key = id switch
        {
            "draw" => Key.A,
            "discard" => Key.S,
            "deck" => Key.D,
            "exhaust" => Key.X,
            "map" => Key.M,
            "menu" => Key.Escape,
            _ => Key.None
        };

        if (key == Key.None)
            return;

        ModLogger.Info($"Utility bar '{id}' -> {key}");
        InputForwardService.PressKey(key);
    }

    private static List<UtilityButtonSpec> GetDesiredButtons()
    {
        var settings = SettingsStore.Current;
        var list = new List<UtilityButtonSpec>();

        if (settings.ShowDrawPileButton)
            list.Add(new UtilityButtonSpec("draw", "A\nDRAW"));
        if (settings.ShowDiscardPileButton)
            list.Add(new UtilityButtonSpec("discard", "S\nDISC"));
        if (settings.ShowDeckButton)
            list.Add(new UtilityButtonSpec("deck", "D\nDECK"));
        if (settings.ShowExhaustPileButton)
            list.Add(new UtilityButtonSpec("exhaust", "X\nEXH"));
        if (settings.ShowMapButton)
            list.Add(new UtilityButtonSpec("map", "M\nMAP"));
        if (settings.ShowMenuButton)
            list.Add(new UtilityButtonSpec("menu", "ESC\nMENU"));

        return list;
    }

    private readonly struct UtilityButtonSpec(string id, string label)
    {
        internal string Id { get; } = id;
        internal string Label { get; } = label;
    }
}
