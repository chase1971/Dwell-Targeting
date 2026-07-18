using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace DwellTargeting;

internal sealed class CardButtonRow
{
    private const int ButtonGap = 5;
    internal const int DefaultGapAboveCard = 28;
    internal const int LargeHandRightEdgeGapAboveCard = 8;
    private const int LargeHandThreshold = 8;
    private const int LargeHandRightEdgeCount = 3;

    private enum RowMode
    {
        Play,
        Select
    }

    private readonly Control _host;
    private readonly ColorRect _cardShield;
    private readonly NCardHolder _holder;
    private readonly bool _parentedToHolder;
    private Container? _layout;
    private readonly List<Button> _buttons = new();
    private readonly Dictionary<ulong, Func<string?>> _buttonActions = new();
    private RowMode _rowMode;
    private bool _needsEnemyTargets;
    private int _enemyCount;
    private int _selectSlot;
    private int _buttonSize;
    private int _lastAppliedFontSize = -1;
    private float _lastAppliedOpacity = -1f;
    private Vector2 _lastButtonAnchor = new(-9999f, -9999f);
    private Vector2 _lastLayoutOrigin = new(-9999f, -9999f);
    private Vector2 _lastLayoutSize = Vector2.Zero;
    private int _gapAboveCard = DefaultGapAboveCard;
    private int _lastAppliedGap = -1;

    internal static int ResolveGapAboveCard(int handSize, int slotIndexFromRight)
    {
        if (handSize >= LargeHandThreshold && slotIndexFromRight < LargeHandRightEdgeCount)
            return LargeHandRightEdgeGapAboveCard;

        return DefaultGapAboveCard;
    }

    internal CardButtonRow(NCardHolder holder, Control? fallbackRoot)
    {
        _holder = holder;
        _parentedToHolder = holder is Control;

        _host = new Control
        {
            Name = $"DwellRow_{holder.GetInstanceId()}",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 200
        };

        if (holder is Control holderControl)
            holderControl.AddChild(_host);
        else if (fallbackRoot != null)
            fallbackRoot.AddChild(_host);

        _cardShield = new ColorRect
        {
            Name = "CardShield",
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 0
        };
        _host.AddChild(_cardShield);
    }

    internal void SyncSelect(int slotOneBased, NCardHolder holder, int buttonSize, int gapAboveCard = DefaultGapAboveCard)
    {
        SetGapAboveCard(gapAboveCard);

        if (_rowMode != RowMode.Select || slotOneBased != _selectSlot || buttonSize != _buttonSize || _buttons.Count == 0)
            RebuildSelectButton(slotOneBased, buttonSize);

        SetCardShieldBlocking(blocking: false);
        ApplyOverlayChromeVisibility();
        UpdatePosition(holder);
        RefreshButtonAppearance();
    }

    internal void SyncPlay(CardModel card, int enemyCount, NCardHolder holder, int buttonSize, int gapAboveCard = DefaultGapAboveCard)
    {
        SetGapAboveCard(gapAboveCard);

        bool needsTargets = CardPlayService.NeedsEnemyTarget(card);
        if (_rowMode != RowMode.Play
            || needsTargets != _needsEnemyTargets
            || enemyCount != _enemyCount
            || buttonSize != _buttonSize
            || _buttons.Count == 0)
        {
            RebuildPlayButtons(needsTargets, enemyCount, buttonSize);
        }

        SetCardShieldBlocking(blocking: false);
        ApplyOverlayChromeVisibility();
        UpdatePosition(holder);
        RefreshButtonAppearance();
    }

    private void ApplyOverlayChromeVisibility()
    {
        bool show = SettingsStore.Current.ShowOverlays;
        if (NodeQuery.IsLive(_host))
            _host.Visible = true;

        foreach (var button in _buttons)
        {
            if (button != null && NodeQuery.IsLive(button))
                button.Visible = show;
        }
    }

    private void SetGapAboveCard(int gapAboveCard)
    {
        if (gapAboveCard == _gapAboveCard)
            return;

        _gapAboveCard = gapAboveCard;
        _lastButtonAnchor = new(-9999f, -9999f);
        _lastLayoutOrigin = new(-9999f, -9999f);
    }

    private void RefreshButtonAppearance()
    {
        long tick = OverlayPerfDiagnostics.BeginTick();
        try
        {
            int fontSize = Math.Max(12, _buttonSize / 2);
            float opacity = SettingsStore.GetCardButtonOpacity();
            if (fontSize == _lastAppliedFontSize && Math.Abs(opacity - _lastAppliedOpacity) < 0.001f)
                return;

            _lastAppliedFontSize = fontSize;
            _lastAppliedOpacity = opacity;

            foreach (var button in _buttons)
            {
                if (!NodeQuery.IsLive(button))
                    continue;

                OverlayButtonFactory.ApplyCardStyle(button, fontSize, opacity);
            }
        }
        finally
        {
            OverlayPerfDiagnostics.AddCategory("styles", tick);
        }
    }

    private void SetCardShieldBlocking(bool blocking)
    {
        if (!NodeQuery.IsLive(_cardShield))
            return;

        _cardShield.MouseFilter = blocking
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }

    internal bool MatchesHolder(NCardHolder holder) =>
        _holder.GetInstanceId() == holder.GetInstanceId();

    internal bool TryHitCardBody(Vector2 globalPos, out NCardHolder? holder, out Rect2 cardBounds)
    {
        holder = null;
        cardBounds = default;

        if (!NodeQuery.IsLive(_host) || !_host.Visible)
            return false;

        if (!NodeQuery.IsLive(_cardShield) || !_cardShield.Visible)
            return false;

        cardBounds = _cardShield.GetGlobalRect();
        if (!cardBounds.HasPoint(globalPos))
            return false;

        if (TryHitAt(globalPos, out _))
            return false;

        holder = _holder;
        return true;
    }

    internal void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (!NodeQuery.IsLive(_host))
            return;

        foreach (var button in _buttons)
        {
            if (!NodeQuery.IsLive(button))
                continue;

            var rect = button.GetGlobalRect();
            string label = button.Text;
            if (!_buttonActions.TryGetValue(button.GetInstanceId(), out var action))
                continue;

            string cardName = _holder.CardModel?.Id.Entry ?? "?";
            targets.Add(DwellHoverService.Card(rect, () => ActivateButton(label, action), $"{label}:{cardName}"));
        }
    }

    private static void ActivateButton(string label, Func<string?> action)
    {
        ModLogger.Info($"Dwell button '{label}'");
        string? error = action();
        if (!string.IsNullOrEmpty(error))
            ModLogger.Warn($"Play failed: {error}");
    }

    internal bool TryHitAt(Vector2 globalPos, out string message) =>
        TryActivateAt(globalPos, out message, activate: false);

    internal bool TryActivateAt(Vector2 globalPos, out string message) =>
        TryActivateAt(globalPos, out message, activate: true);

    private bool TryActivateAt(Vector2 globalPos, out string message, bool activate)
    {
        message = string.Empty;
        if (!NodeQuery.IsLive(_host))
            return false;

        foreach (var button in _buttons)
        {
            if (!NodeQuery.IsLive(button))
                continue;
            if (!button.GetGlobalRect().HasPoint(globalPos))
                continue;

            message = activate
                ? $"GlobalClick button '{button.Text}' card={_holder.CardModel?.Id.Entry}"
                : $"Hit button '{button.Text}'";

            if (!activate)
                return true;

            if (_buttonActions.TryGetValue(button.GetInstanceId(), out var action))
            {
                string? error = action();
                if (!string.IsNullOrEmpty(error))
                {
                    message += $" failed: {error}";
                    ModLogger.Warn(message);
                }
            }

            return true;
        }

        return false;
    }

    internal void Dispose()
    {
        if (NodeQuery.IsLive(_host))
            _host.QueueFree();
        _buttons.Clear();
        _buttonActions.Clear();
        _layout = null;
    }

    private void UpdatePosition(NCardHolder holder)
    {
        if (_parentedToHolder && CardAnchorService.TryGetLocalCardPlacement(holder, out var localPlacement))
        {
            ApplyLayout(
                localPlacement.ButtonAnchor,
                localPlacement.Bounds.Position,
                localPlacement.Bounds.Size,
                localCoords: true);
            return;
        }

        if (CardAnchorService.TryGetCardPlacement(holder, out var globalPlacement))
        {
            ApplyLayout(
                globalPlacement.ButtonAnchor,
                globalPlacement.Bounds.Position,
                globalPlacement.Bounds.Size,
                localCoords: false);
        }
    }

    private void ApplyLayout(Vector2 buttonAnchor, Vector2 origin, Vector2 size, bool localCoords)
    {
        if (buttonAnchor.DistanceSquaredTo(_lastButtonAnchor) < 0.25f
            && origin.DistanceSquaredTo(_lastLayoutOrigin) < 0.25f
            && size.DistanceSquaredTo(_lastLayoutSize) < 0.25f
            && _gapAboveCard == _lastAppliedGap)
            return;

        _lastButtonAnchor = buttonAnchor;
        _lastLayoutOrigin = origin;
        _lastLayoutSize = size;
        _lastAppliedGap = _gapAboveCard;

        if (_parentedToHolder && localCoords)
        {
            _cardShield.Position = origin;
            _cardShield.Size = size;
        }
        else
        {
            _cardShield.GlobalPosition = origin;
            _cardShield.Size = size;
        }

        _cardShield.Visible = true;

        if (_layout == null || !NodeQuery.IsLive(_layout))
            return;

        _layout.ResetSize();
        var layoutSize = _layout.GetCombinedMinimumSize();
        if (layoutSize.X < 1 || layoutSize.Y < 1)
            return;

        var target = new Vector2(
            buttonAnchor.X - (layoutSize.X / 2f),
            buttonAnchor.Y - _gapAboveCard - layoutSize.Y);

        if (_parentedToHolder && localCoords)
            _layout.Position = target;
        else
            _layout.GlobalPosition = target;
    }

    private void RebuildSelectButton(int slotOneBased, int buttonSize)
    {
        ClearButtons();

        _rowMode = RowMode.Select;
        _selectSlot = slotOneBased;
        _buttonSize = buttonSize;
        _needsEnemyTargets = false;
        _enemyCount = 0;

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2
        };
        _layout = row;

        var button = CreateButton(slotOneBased.ToString(), buttonSize, () => CardSelectionService.TrySelect(_holder, slotOneBased));
        row.AddChild(button);
        _buttons.Add(button);
        _host.AddChild(_layout);
    }

    private void RebuildPlayButtons(bool needsTargets, int enemyCount, int buttonSize)
    {
        ClearButtons();

        _rowMode = RowMode.Play;
        _needsEnemyTargets = needsTargets;
        _enemyCount = enemyCount;
        _buttonSize = buttonSize;
        _selectSlot = 0;

        if (needsTargets && enemyCount == 4)
        {
            var grid = new GridContainer
            {
                Columns = 2,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 2
            };
            grid.AddThemeConstantOverride("h_separation", ButtonGap);
            grid.AddThemeConstantOverride("v_separation", ButtonGap);
            _layout = grid;

            for (int slot = 1; slot <= 4; slot++)
                AddTargetButton(grid, slot, buttonSize);
        }
        else
        {
            var row = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 2
            };
            row.AddThemeConstantOverride("separation", ButtonGap);
            _layout = row;

            if (needsTargets)
            {
                int count = Math.Clamp(enemyCount, 0, 4);
                for (int slot = 1; slot <= count; slot++)
                    AddTargetButton(row, slot, buttonSize);
            }
            else
            {
                var play = CreateButton("▶", buttonSize, () => CardPlayService.TryPlay(_holder, 0));
                row.AddChild(play);
                _buttons.Add(play);
            }
        }

        _host.AddChild(_layout);
    }

    private void ClearButtons()
    {
        if (_layout != null && NodeQuery.IsLive(_layout))
            _layout.QueueFree();
        _buttons.Clear();
        _buttonActions.Clear();
        _layout = null;
        InvalidateStyleCache();
        _lastButtonAnchor = new(-9999f, -9999f);
        _lastLayoutOrigin = new(-9999f, -9999f);
        _lastLayoutSize = Vector2.Zero;
        _lastAppliedGap = -1;
    }

    private void InvalidateStyleCache()
    {
        _lastAppliedFontSize = -1;
        _lastAppliedOpacity = -1f;
    }

    private void AddTargetButton(Container parent, int slot, int buttonSize)
    {
        int capturedSlot = slot;
        var button = CreateButton(slot.ToString(), buttonSize, () => CardPlayService.TryPlay(_holder, capturedSlot));
        parent.AddChild(button);
        _buttons.Add(button);
    }

    private Button CreateButton(string text, int buttonSize, Func<string?> onClick)
    {
        int fontSize = Math.Max(12, buttonSize / 2);

        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(buttonSize, buttonSize),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2
        };

        OverlayButtonFactory.ApplyCardStyle(button, fontSize, SettingsStore.GetCardButtonOpacity());
        _buttonActions[button.GetInstanceId()] = onClick;

        void Activate(string source)
        {
            ModLogger.Info($"Button '{text}' via {source} card={_holder.CardModel?.Id.Entry}");
            string? error = onClick();
            if (!string.IsNullOrEmpty(error))
                ModLogger.Warn($"Play failed: {error}");
        }

        button.Pressed += () => Activate("Pressed");
        button.ButtonDown += () => ModLogger.Info($"ButtonDown '{text}'");

        return button;
    }
}
