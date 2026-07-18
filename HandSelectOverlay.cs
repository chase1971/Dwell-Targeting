using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// Lightweight hand-select dwell (discard, draw-pile pick, upgrade pick, etc.). Slot numbers sit
/// Layout recomputed when the hand changes; dwell rects are cached separately from visible buttons.
/// </summary>
internal static class HandSelectOverlay
{
    private const int CanvasLayerOrder = 128;
    private const int GapAboveCard = CardButtonRow.DefaultGapAboveCard;

    private static List<(NCardHolder Holder, int Slot)>? _entries;
    private static List<(Rect2 Bounds, NCardHolder Holder, int Slot)>? _dwellTargets;
    private static ulong _cachedHandId;
    private static int _cachedSignature;
    private static int _cachedButtonSize = -1;
    private static bool _lastShowVisuals = true;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly List<Button> _numberButtons = new();

    internal static void Sync(NPlayerHand hand)
    {
        if (!NodeQuery.IsLive(hand))
        {
            Hide();
            return;
        }

        if (CardConfirmPhaseQuery.IsActive())
        {
            HideNumberButtons();
            _dwellTargets = null;
            return;
        }

        int signature = ComputeSignature(hand);
        ulong handId = hand.GetInstanceId();

        bool showChanged = _lastShowVisuals != SettingsStore.Current.ShowOverlays;
        if (showChanged)
            _lastShowVisuals = SettingsStore.Current.ShowOverlays;

        if (_entries == null
            || handId != _cachedHandId
            || signature != _cachedSignature
            || showChanged)
        {
            _cachedHandId = handId;
            _cachedSignature = signature;
            _entries = BuildEntries(hand);
            RebuildLayout();
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (CardConfirmPhaseQuery.IsActive())
        {
            CardConfirmPhaseQuery.CollectDwellTargets(targets);
            return;
        }

        if (_dwellTargets == null)
            return;

        foreach (var (bounds, holder, slot) in _dwellTargets)
        {
            if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder))
                continue;

            var captured = holder;
            int capturedSlot = slot;
            targets.Add(DwellHoverService.Card(
                bounds,
                () => CardSelectionService.TrySelect(captured, capturedSlot),
                $"HandSelect:{capturedSlot}"));
        }
    }

    internal static void Hide()
    {
        _entries = null;
        _dwellTargets = null;
        _cachedHandId = 0;
        _cachedSignature = 0;
        _cachedButtonSize = -1;
        _lastShowVisuals = true;
        HideNumberButtons();
    }

    private static void RebuildLayout()
    {
        if (_entries == null || _entries.Count == 0)
        {
            _dwellTargets = null;
            HideNumberButtons();
            return;
        }

        int buttonSize = SettingsStore.GetCardButtonSize(_entries.Count);
        int fontSize = Math.Max(12, buttonSize / 2);
        float opacity = SettingsStore.GetCardButtonOpacity();
        bool showVisuals = SettingsStore.Current.ShowOverlays;

        var targets = new List<(Rect2, NCardHolder, int)>();
        EnsureCanvas();
        if (_root == null)
            return;

        while (_numberButtons.Count < _entries.Count)
        {
            var button = new Button
            {
                Name = $"HandSelectPick{_numberButtons.Count + 1}",
                FocusMode = Control.FocusModeEnum.None,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 2
            };
            _root.AddChild(button);
            _numberButtons.Add(button);
        }

        if (buttonSize != _cachedButtonSize)
        {
            _cachedButtonSize = buttonSize;
            foreach (var button in _numberButtons)
            {
                if (button == null || !NodeQuery.IsLive(button))
                    continue;

                OverlayButtonFactory.ApplySize(button, buttonSize);
                OverlayButtonFactory.ApplyCardStyle(button, fontSize, opacity);
            }
        }

        _root.Visible = showVisuals;

        int handSize = _entries.Count;

        for (int i = 0; i < _numberButtons.Count; i++)
        {
            var button = _numberButtons[i];
            if (button == null || !NodeQuery.IsLive(button))
                continue;

            if (i >= _entries.Count)
            {
                button.Visible = false;
                continue;
            }

            var (holder, slot) = _entries[i];
            if (!NodeQuery.IsLive(holder)
                || !NodeQuery.IsVisible(holder)
                || !CardAnchorService.TryGetCardRect(holder, out var cardRect))
            {
                button.Visible = false;
                continue;
            }

            float centerX = cardRect.Position.X + (cardRect.Size.X / 2f);
            int gapAboveCard = CardButtonRow.ResolveGapAboveCard(handSize, handSize - 1 - i);
            var bounds = new Rect2(
                centerX - (buttonSize / 2f),
                cardRect.Position.Y - gapAboveCard - buttonSize,
                buttonSize,
                buttonSize);

            targets.Add((bounds, holder, slot));

            button.Text = slot.ToString();
            button.GlobalPosition = bounds.Position;
            button.Size = bounds.Size;
            button.Visible = showVisuals;
        }

        _dwellTargets = targets;
    }

    private static List<(NCardHolder Holder, int Slot)> BuildEntries(NPlayerHand hand)
    {
        var holders = new List<NCardHolder>();
        foreach (var holderObj in hand.ActiveHolders)
        {
            if (holderObj is NCardHolder typed)
                holders.Add(typed);
        }

        if (holders.Count == 0)
            holders.AddRange(NodeQuery.FindAllSortedByPosition<NCardHolder>(hand));

        holders.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            return cmp != 0 ? cmp : a.GetInstanceId().CompareTo(b.GetInstanceId());
        });

        var list = new List<(NCardHolder, int)>();
        int slot = 1;
        foreach (var holder in holders)
        {
            if (holder.CardModel == null || !NodeQuery.IsVisible(holder))
                continue;

            list.Add((holder, slot));
            slot++;
        }

        return list;
    }

    private static int ComputeSignature(NPlayerHand hand)
    {
        int hash = hand.GetChildCount();
        foreach (var holderObj in hand.ActiveHolders)
        {
            if (holderObj is not NCardHolder holder || !NodeQuery.IsLive(holder))
                continue;

            hash = HashCode.Combine(
                hash,
                holder.GetInstanceId(),
                holder.CardModel?.Id.Entry,
                (int)holder.GlobalPosition.X,
                (int)holder.GlobalPosition.Y);
        }

        return hash;
    }

    private static void HideNumberButtons()
    {
        foreach (var button in _numberButtons)
        {
            if (button != null && NodeQuery.IsLive(button))
                button.Visible = false;
        }

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellHandSelectLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellHandSelectRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _numberButtons.Clear();
        _cachedButtonSize = -1;
    }
}
