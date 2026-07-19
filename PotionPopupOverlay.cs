using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Potions;

namespace DwellTargeting;

/// <summary>
/// Numbered dwell buttons beside Use/Discard choices in the potion popup menu.
/// </summary>
internal static class PotionPopupOverlay
{
    private const int ButtonSize = 44;
    private const int CanvasLayerOrder = 131;
    private const int SideOffset = 72;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly Dictionary<ulong, PopupSideButton> _sideButtons = new();
    private static ulong _cachedPopupId;
    private static NPotionPopup? _cachedPopup;
    private static bool _scanPending;

    internal static void RequestScan()
    {
        _scanPending = true;
    }

    internal static void InvalidateLookup()
    {
        _scanPending = false;
        Hide();
    }

    internal static void Sync()
    {
        if (_cachedPopup != null && NodeQuery.IsLive(_cachedPopup) && NodeQuery.IsVisible(_cachedPopup))
        {
            EnsureCanvas();
            if (_root != null)
                _root.Visible = true;
            return;
        }

        _cachedPopup = null;

        if (!_scanPending)
        {
            Hide();
            return;
        }

        _scanPending = false;
        if (!TryGetVisiblePopup(out var popup))
        {
            Hide();
            return;
        }

        _cachedPopup = popup;
        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;

        ulong popupId = popup.GetInstanceId();
        if (_cachedPopupId == popupId && _sideButtons.Count > 0)
            return;

        _cachedPopupId = popupId;
        RebuildSideButtons(popup);
    }

    private static void RebuildSideButtons(NPotionPopup popup)
    {
        foreach (var side in _sideButtons.Values)
            side.Dispose();
        _sideButtons.Clear();

        var menuButtons = NodeQuery.FindAll<NPotionPopupButton>(popup)
            .Where(IsEnabledMenuButton)
            .OrderBy(b => b.GlobalPosition.Y)
            .ThenBy(b => b.GlobalPosition.X)
            .ToList();

        int slot = 1;
        foreach (var menuButton in menuButtons)
        {
            ulong id = menuButton.GetInstanceId();
            var side = new PopupSideButton(menuButton, _root!);
            _sideButtons[id] = side;
            side.Sync(slot, ButtonSize);
            ModLogger.Info($"Potion popup side button {slot} for {menuButton.Name}.");
            slot++;
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        foreach (var side in _sideButtons.Values)
            side.CollectDwellTargets(targets);
    }

    internal static void Hide()
    {
        foreach (var side in _sideButtons.Values)
            side.Dispose();
        _sideButtons.Clear();
        _cachedPopupId = 0;
        _cachedPopup = null;

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var side in _sideButtons.Values)
        {
            if (side.TryActivateAt(globalPos, out message))
                return true;
        }

        return false;
    }

    internal static bool TryHitDwellButton(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var side in _sideButtons.Values)
        {
            if (side.TryHitAt(globalPos, out message))
                return true;
        }

        return false;
    }

    private static bool TryGetVisiblePopup(out NPotionPopup popup)
    {
        popup = null!;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        foreach (var candidate in NodeQuery.FindAll<NPotionPopup>(tree.Root))
        {
            if (!NodeQuery.IsVisible(candidate))
                continue;

            popup = candidate;
            return true;
        }

        return false;
    }

    private static bool IsEnabledMenuButton(NPotionPopupButton button) =>
        NodeQuery.IsVisible(button)
        && button is NClickableControl { IsEnabled: true };

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellPotionPopupLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellPotionPopupRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);
        ModLogger.Info("Potion popup overlay canvas created.");
    }

    private sealed class PopupSideButton
    {
        private readonly NPotionPopupButton _target;
        private readonly Control _host;
        private Button? _button;
        private string _label = "?";

        internal PopupSideButton(NPotionPopupButton target, Control root)
        {
            _target = target;
            _host = new Control
            {
                Name = $"DwellPotionPopupSide_{target.GetInstanceId()}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 200
            };
            root.AddChild(_host);
        }

        internal void Sync(int slot, int buttonSize) => Sync(slot.ToString(), buttonSize);

        internal void Sync(string label, int buttonSize)
        {
            _label = label;
            EnsureButton(buttonSize);
            PositionButton();
        }

        internal void CollectDwellTargets(List<DwellHoverService.Target> targets)
        {
            if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
                return;

            var rect = _button.GetGlobalRect();
            targets.Add(DwellHoverService.Card(rect, Activate, $"PotionPopup:{_label}"));
        }

        internal bool TryHitAt(Vector2 globalPos, out string message) =>
            TryActivateAt(globalPos, out message, activate: false);

        internal bool TryActivateAt(Vector2 globalPos, out string message) =>
            TryActivateAt(globalPos, out message, activate: true);

        private bool TryActivateAt(Vector2 globalPos, out string message, bool activate)
        {
            message = string.Empty;
            if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
                return false;

            if (!_button.GetGlobalRect().HasPoint(globalPos))
                return false;

            message = activate ? $"Potion popup side '{_label}' activated" : $"Hit potion popup side '{_label}'";
            if (activate)
                Activate();

            return true;
        }

        private void Activate()
        {
            ModLogger.Info($"Potion popup side button '{_label}'");
            if (!NodeQuery.IsLive(_target))
                return;

            if (_target is NClickableControl clickable)
                clickable.ForceClick();
            else
                InputForwardService.TryActivateControl(_target);
        }

        internal void Dispose()
        {
            if (NodeQuery.IsLive(_host))
                _host.QueueFree();
            _button = null;
        }

        private void EnsureButton(int buttonSize)
        {
            if (_button != null && NodeQuery.IsLive(_button))
            {
                _button.Text = _label;
                _button.CustomMinimumSize = new Vector2(buttonSize, buttonSize);
                _button.Visible = true;
                return;
            }

            int fontSize = Math.Max(12, buttonSize / 2);
            _button = new Button
            {
                Text = _label,
                CustomMinimumSize = new Vector2(buttonSize, buttonSize),
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Stop,
                ZIndex = 2
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.1f, 0.14f, 0.95f),
                BorderColor = new Color(0.45f, 0.85f, 1f, 1f),
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8
            };
            _button.AddThemeStyleboxOverride("normal", style);
            _button.AddThemeStyleboxOverride("hover", style);
            _button.AddThemeStyleboxOverride("pressed", style);
            _button.AddThemeStyleboxOverride("focus", style);
            _button.AddThemeFontSizeOverride("font_size", fontSize);
            _button.Pressed += Activate;
            _host.AddChild(_button);
        }

        private void PositionButton()
        {
            if (_button == null || !NodeQuery.IsLive(_target))
                return;

            var targetRect = _target.GetGlobalRect();
            _button.ResetSize();
            var size = _button.GetCombinedMinimumSize();
            float x = targetRect.Position.X - SideOffset - size.X;
            float y = targetRect.Position.Y + ((targetRect.Size.Y - size.Y) / 2f);
            _button.GlobalPosition = new Vector2(x, y);
            _button.Size = size;
            _button.Visible = true;
        }
    }
}
