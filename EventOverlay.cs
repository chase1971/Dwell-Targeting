using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DwellTargeting;

/// <summary>
/// All event options use offset number buttons — hover the row to read tooltips, dwell the number
/// to pick. Ancient encounters use gold numbers; normal events use the standard card-button style.
/// Proceed/Skip always uses a direct padded dwell on the native control — never an offset number.
/// </summary>
internal static class EventOverlay
{
    private const int CanvasLayerOrder = 131;
    private const int NumberSize = 54;
    private const float NumberGap = 14f;
    private const float ScreenMargin = 10f;
    private const float ProceedHitboxPadding = 24f;

    private static NEventRoom? _room;
    private static List<Control>? _cachedButtons;
    private static bool _usesOffsetNumbers;
    private static bool _ancientGoldStyle;
    private static List<(Rect2 Bounds, Control Option, int Slot)>? _dwellTargets;
    private static Control? _proceedControl;
    private static ulong _cachedRoomId;
    private static int _cachedOptionSignature;
    private static bool _lastShowVisuals = true;
    private const long RescanMs = 250;
    private static long _lastRescanTick;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly List<Button> _numberButtons = new();

    internal static void InvalidateCache()
    {
        _cachedOptionSignature = int.MinValue;
        _lastRescanTick = 0;
    }

    internal static void Sync()
    {
        _room = OverlayModeService.GetCachedEventRoom();
        if (_room == null)
        {
            Hide();
            return;
        }

        bool showChanged = _lastShowVisuals != SettingsStore.Current.ShowOverlays;
        if (showChanged)
            _lastShowVisuals = SettingsStore.Current.ShowOverlays;

        ulong roomId = _room.GetInstanceId();
        long now = System.Environment.TickCount64;
        var freshButtons = FindOptionButtons(_room);
        int optionSignature = ComputeOptionSignature(freshButtons);
        bool hasOptionTargets = _usesOffsetNumbers && _dwellTargets is { Count: > 0 };
        bool hasProceedOnly = freshButtons.Count == 0
            && _proceedControl != null
            && NodeQuery.IsLive(_proceedControl)
            && NodeQuery.IsVisible(_proceedControl);

        if (_cachedRoomId == roomId
            && !showChanged
            && optionSignature == _cachedOptionSignature
            && (hasOptionTargets || hasProceedOnly)
            && now - _lastRescanTick < RescanMs)
        {
            if (!hasOptionTargets)
                HideNumberButtons();
            return;
        }

        _lastRescanTick = now;
        _cachedOptionSignature = optionSignature;

        _cachedRoomId = roomId;
        _cachedButtons = freshButtons;
        _usesOffsetNumbers = _cachedButtons is { Count: > 0 };
        _ancientGoldStyle = UsesAncientGoldStyle(_room);
        _proceedControl = FindProceedControl();
        RebuildNumberLayout();

        if (!_usesOffsetNumbers)
            HideNumberButtons();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_usesOffsetNumbers && _dwellTargets != null)
        {
            foreach (var (bounds, option, slot) in _dwellTargets)
            {
                if (!NodeQuery.IsLive(option) || !NodeQuery.IsVisible(option))
                    continue;

                var captured = option;
                targets.Add(DwellHoverService.Menu(
                    bounds,
                    () => EventSelectionService.TrySelect(captured),
                    $"EventOption:{slot}"));
            }
        }

        if (TryGetProceedRect(out var proceedRect))
            targets.Add(DwellHoverService.Menu(proceedRect, ActivateProceed, "EventProceed"));
    }

    internal static void Hide()
    {
        _room = null;
        _cachedButtons = null;
        _usesOffsetNumbers = false;
        _ancientGoldStyle = false;
        _dwellTargets = null;
        _proceedControl = null;
        _cachedRoomId = 0;
        _cachedOptionSignature = int.MinValue;
        _lastShowVisuals = true;
        _lastRescanTick = 0;
        HideNumberButtons();
    }

    private static int ComputeOptionSignature(List<Control> buttons)
    {
        int key = buttons.Count;
        foreach (var button in buttons)
            key = HashCode.Combine(key, (int)button.GetInstanceId());

        return key;
    }

    private static bool UsesAncientGoldStyle(NEventRoom room)
    {
        foreach (var layout in NodeQuery.FindAll<NAncientEventLayout>(room))
        {
            if (NodeQuery.IsLive(layout) && NodeQuery.IsVisible(layout))
                return true;
        }

        return false;
    }

    private static void RebuildNumberLayout()
    {
        if (_cachedButtons == null || _cachedButtons.Count == 0 || !_usesOffsetNumbers)
        {
            _dwellTargets = null;
            HideNumberButtons();
            return;
        }

        bool showVisuals = SettingsStore.Current.ShowOverlays;
        var targets = new List<(Rect2, Control, int)>();
        EnsureCanvas();
        if (_root == null)
            return;

        while (_numberButtons.Count < _cachedButtons.Count)
        {
            int index = _numberButtons.Count + 1;
            Color bg = _ancientGoldStyle
                ? new Color(0.10f, 0.08f, 0.02f, 0.95f)
                : new Color(0.08f, 0.10f, 0.14f, 0.95f);
            Color border = _ancientGoldStyle
                ? new Color(1f, 0.82f, 0.30f, 1f)
                : new Color(0.55f, 0.75f, 0.95f, 1f);

            var button = OverlayButtonFactory.CreateMenuButton(
                $"EventPick{index}",
                index.ToString(),
                NumberSize,
                bg,
                border,
                () => { });
            button.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(button);
            _numberButtons.Add(button);
        }

        _root.Visible = showVisuals;

        for (int i = 0; i < _numberButtons.Count; i++)
        {
            var button = _numberButtons[i];
            if (button == null || !NodeQuery.IsLive(button))
                continue;

            if (i >= _cachedButtons.Count
                || !NodeQuery.IsLive(_cachedButtons[i])
                || !NodeQuery.IsVisible(_cachedButtons[i]))
            {
                button.Visible = false;
                continue;
            }

            if (!ControlHitboxService.TryGetDwellRect(_cachedButtons[i], out var optionRect))
                optionRect = _cachedButtons[i].GetGlobalRect();

            if (optionRect.Size.X < 8f || optionRect.Size.Y < 8f)
            {
                button.Visible = false;
                continue;
            }

            optionRect = optionRect.Grow(6f);

            OverlayButtonFactory.ApplySize(button, NumberSize);
            button.Text = (i + 1).ToString();

            float x = optionRect.Position.X - NumberSize - NumberGap;
            if (x < ScreenMargin)
                x = optionRect.End.X + NumberGap;

            float y = optionRect.GetCenter().Y - (NumberSize / 2f);
            var bounds = new Rect2(x, y, NumberSize, NumberSize);

            button.GlobalPosition = bounds.Position;
            button.Size = bounds.Size;
            button.Visible = showVisuals;

            targets.Add((bounds, _cachedButtons[i], i + 1));
        }

        _dwellTargets = targets;
    }

    private static bool IsProceedLike(Control control)
    {
        if (!NodeQuery.IsLive(control))
            return false;

        if (control is NProceedButton)
            return true;

        if (ContainsProceedText(control.Name.ToString()))
            return true;

        if (control is Button { Text: var text } && ContainsProceedText(text))
            return true;

        return ContainsProceedTextInTree(control);
    }

    private static bool ContainsProceedTextInTree(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return false;

        if (node is Label { Text: var labelText } && ContainsProceedText(labelText))
            return true;

        if (node is Button { Text: var buttonText } && ContainsProceedText(buttonText))
            return true;

        try
        {
            foreach (var child in node.GetChildren())
            {
                if (ContainsProceedTextInTree(child))
                    return true;
            }
        }
        catch
        {
            /* disposed mid-walk */
        }

        return false;
    }

    private static bool ContainsProceedText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("Proceed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Skip", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Continue", StringComparison.OrdinalIgnoreCase);
    }

    private static Control? FindProceedControl()
    {
        if (_room == null || !NodeQuery.IsLive(_room))
            return null;

        return FindProceedInSubtree(_room);
    }

    private static Control? FindProceedInSubtree(Node start)
    {
        foreach (var button in NodeQuery.FindAll<NProceedButton>(start))
        {
            if (button is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;

            return control;
        }

        foreach (var button in NodeQuery.FindAll<NEventOptionButton>(start))
        {
            if (button is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;
            if (!IsProceedLike(control))
                continue;

            return control;
        }

        return null;
    }

    private static bool TryGetProceedRect(out Rect2 rect)
    {
        rect = default;
        if (_proceedControl == null || !NodeQuery.IsLive(_proceedControl) || !NodeQuery.IsVisible(_proceedControl))
            return false;

        if (_proceedControl is NClickableControl { IsEnabled: false })
            return false;

        rect = _proceedControl.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(ProceedHitboxPadding);
        return true;
    }

    private static void ActivateProceed()
    {
        if (_proceedControl == null || !NodeQuery.IsLive(_proceedControl))
        {
            ModLogger.Warn("[Event] Proceed dwell fired but proceed control missing/dead.");
            return;
        }

        if (_proceedControl is NProceedButton proceed)
        {
            RewardSelectionService.TryProceed(proceed);
            return;
        }

        InputForwardService.PressAcceptKey();
        ModLogger.Info($"[Event] Proceed '{_proceedControl.Name}' via E accept key.");
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

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellEventLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellEventRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _numberButtons.Clear();
    }

    private static List<Control> FindOptionButtons(NEventRoom room)
    {
        var list = CollectOptionButtons(room);
        if (list.Count > 0)
            return list;

        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return list;

        return CollectOptionButtons(root);
    }

    private static List<Control> CollectOptionButtons(Node root)
    {
        var list = new List<Control>();
        if (!NodeQuery.IsLive(root))
            return list;

        foreach (var button in NodeQuery.FindAll<NEventOptionButton>(root))
        {
            if (button is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;
            if (IsProceedLike(control))
                continue;

            list.Add(control);
        }

        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
            return cmp != 0 ? cmp : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
        });

        return list;
    }
}
