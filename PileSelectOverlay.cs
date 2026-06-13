using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace DwellTargeting;

/// <summary>
/// Numbered dwell buttons above cards shown in pile/grid selection screens.
/// </summary>
internal static class PileSelectOverlay
{
    private const int GapAboveCard = 28;
    private const int CanvasLayerOrder = 130;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly Dictionary<ulong, PileCardButton> _buttons = new();

    internal static void Sync()
    {
        if (!OverlayModeService.TryGetPileSelectScreen(out Node screen))
        {
            Hide();
            return;
        }

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;

        var cards = NodeQuery.FindAll<NCard>(screen)
            .Where(c => NodeQuery.IsVisible(c) && IsSelectableCard(c))
            .OrderBy(c => c.GlobalPosition.Y)
            .ThenBy(c => c.GlobalPosition.X)
            .ToList();

        var liveIds = new HashSet<ulong>();
        int slot = 1;
        int buttonSize = ComputeButtonSize(cards.Count);

        foreach (var card in cards)
        {
            ulong id = card.GetInstanceId();
            liveIds.Add(id);

            if (!_buttons.TryGetValue(id, out var side))
            {
                side = new PileCardButton(card, _root);
                _buttons[id] = side;
                ModLogger.Info($"Pile select button {slot} for {card.Name}.");
            }

            side.Sync(slot, buttonSize);
            slot++;
        }

        foreach (var pair in _buttons.ToList())
        {
            if (!liveIds.Contains(pair.Key))
            {
                pair.Value.Dispose();
                _buttons.Remove(pair.Key);
            }
        }

        var dwellTargets = new List<DwellHoverService.Target>();
        CollectDwellTargets(dwellTargets);
        DwellHoverService.ProcessFrame(dwellTargets, GetProcessDelta());
    }

    internal static void Hide()
    {
        foreach (var side in _buttons.Values)
            side.Dispose();
        _buttons.Clear();

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        foreach (var side in _buttons.Values)
        {
            if (side.TryActivateAt(globalPos, out message))
                return true;
        }

        return false;
    }

    private static bool IsSelectableCard(NCard card)
    {
        if (card is not Control control)
            return false;

        var rect = control.GetGlobalRect();
        return rect.Size.X >= 80f && rect.Size.Y >= 100f;
    }

    private static int ComputeButtonSize(int cardCount) =>
        cardCount >= 12 ? 26 : cardCount >= 8 ? 30 : cardCount >= 5 ? 34 : 38;

    private static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        foreach (var side in _buttons.Values)
            side.CollectDwellTargets(targets);
    }

    private static double GetProcessDelta()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        return tree?.Root?.GetProcessDeltaTime() ?? (1.0 / 60.0);
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellPileSelectLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellPileSelectRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);
        ModLogger.Info("Pile select overlay canvas created.");
    }

    private sealed class PileCardButton
    {
        private readonly NCard _card;
        private readonly Control _host;
        private Button? _button;
        private int _slot;

        internal PileCardButton(NCard card, Control root)
        {
            _card = card;
            _host = new Control
            {
                Name = $"DwellPileBtn_{card.GetInstanceId()}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 200
            };
            root.AddChild(_host);
        }

        internal void Sync(int slot, int buttonSize)
        {
            _slot = slot;
            EnsureButton(buttonSize);
            PositionButton();
        }

        internal void CollectDwellTargets(List<DwellHoverService.Target> targets)
        {
            if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
                return;

            var rect = _button.GetGlobalRect();
            targets.Add(new DwellHoverService.Target(
                rect,
                () => PileCardSelectionService.TrySelect(_card, _slot),
                $"Pile:{_slot}"));
        }

        internal bool TryActivateAt(Vector2 globalPos, out string message)
        {
            message = string.Empty;
            if (_button == null || !NodeQuery.IsLive(_button) || !_button.Visible)
                return false;

            if (!_button.GetGlobalRect().HasPoint(globalPos))
                return false;

            message = $"Pile select slot {_slot}";
            PileCardSelectionService.TrySelect(_card, _slot);
            return true;
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
                _button.Text = _slot.ToString();
                _button.CustomMinimumSize = new Vector2(buttonSize, buttonSize);
                _button.Visible = true;
                return;
            }

            int fontSize = Math.Max(12, buttonSize / 2);
            _button = new Button
            {
                Text = _slot.ToString(),
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
            _button.Pressed += () => PileCardSelectionService.TrySelect(_card, _slot);
            _host.AddChild(_button);
        }

        private void PositionButton()
        {
            if (_button == null || _card is not Control cardControl || !NodeQuery.IsLive(cardControl))
                return;

            var targetRect = cardControl.GetGlobalRect();
            _button.ResetSize();
            var size = _button.GetCombinedMinimumSize();
            float centerX = targetRect.Position.X + (targetRect.Size.X / 2f);
            float x = centerX - (size.X / 2f);
            float y = targetRect.Position.Y - GapAboveCard - size.Y;
            _button.GlobalPosition = new Vector2(x, y);
            _button.Size = size;
            _button.Visible = true;
        }
    }
}
