using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for event option buttons. Ancient relic rows get offset number buttons (hover the
/// row to read the tooltip, dwell the number to pick). Proceed/Skip always uses a direct padded dwell
/// on the native control — never an offset number.
/// </summary>
internal static class EventOverlay
{
    private const int RescanIntervalFrames = 10;
    private const int CanvasLayerOrder = 131;
    private const int NumberSize = 54;
    private const float NumberGap = 14f;
    private const float ScreenMargin = 10f;
    private const float ProceedHitboxPadding = 24f;

    private static NEventRoom? _room;
    private static List<Control>? _cachedButtons;
    private static List<Control>? _cachedAncientButtons;
    private static Control? _proceedControl;
    private static int _framesSinceScan;
    private static long _nextDiagTick;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly List<Button> _numberButtons = new();

    internal static void Sync()
    {
        _room = OverlayModeService.GetCachedEventRoom();
        if (_room == null)
        {
            Hide();
            return;
        }

        _framesSinceScan++;
        if (_cachedButtons == null || _framesSinceScan >= RescanIntervalFrames)
        {
            _framesSinceScan = 0;
            _cachedButtons = FindOptionButtons(_room);
            _cachedAncientButtons = _cachedButtons.Where(IsAncientOption).ToList();
            _proceedControl = FindProceedControl();
        }

        if (_cachedAncientButtons is { Count: > 0 })
            SyncNumberButtons();
        else
            HideNumberButtons();

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info(
                $"[Event] sync options={_cachedButtons?.Count ?? -1} ancient={_cachedAncientButtons?.Count ?? -1} " +
                $"proceed={(_proceedControl != null)}.");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedButtons == null)
            return;

        if (_cachedAncientButtons is { Count: > 0 })
        {
            for (int i = 0; i < _numberButtons.Count && i < _cachedAncientButtons.Count; i++)
            {
                var button = _numberButtons[i];
                var option = _cachedAncientButtons[i];
                if (button == null || !NodeQuery.IsLive(button) || !button.Visible)
                    continue;
                if (!NodeQuery.IsLive(option) || !NodeQuery.IsVisible(option))
                    continue;

                var captured = option;
                targets.Add(DwellHoverService.Menu(
                    button.GetGlobalRect(),
                    () => EventSelectionService.TrySelect(captured),
                    $"AncientOption:{i + 1}"));
            }
        }

        foreach (var button in _cachedButtons.Where(b => !IsAncientOption(b)))
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                continue;

            if (ControlHitboxService.TryGetDwellRect(button, out var rect))
            {
                var captured = button;
                targets.Add(DwellHoverService.Menu(
                    rect,
                    () => EventSelectionService.TrySelect(captured),
                    $"EventOption:{button.Name}"));
            }
        }

        if (TryGetProceedRect(out var proceedRect))
            targets.Add(DwellHoverService.Menu(proceedRect, ActivateProceed, "EventProceed"));
    }

    internal static void Hide()
    {
        _room = null;
        _cachedButtons = null;
        _cachedAncientButtons = null;
        _proceedControl = null;
        _framesSinceScan = 0;
        HideNumberButtons();
    }

    private static bool IsAncientOption(Control option) =>
        NodeQuery.IsLive(option)
        && !IsProceedLike(option)
        && option.Name.ToString().Contains("Ancient", StringComparison.OrdinalIgnoreCase);

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
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var button in NodeQuery.FindAll<NProceedButton>(root))
        {
            if (button is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;

            return control;
        }

        foreach (var button in NodeQuery.FindAll<NEventOptionButton>(root))
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

    private static void SyncNumberButtons()
    {
        if (_cachedAncientButtons == null)
            return;

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;

        while (_numberButtons.Count < _cachedAncientButtons.Count)
        {
            var button = OverlayButtonFactory.CreateMenuButton(
                $"AncientPick{_numberButtons.Count + 1}",
                (_numberButtons.Count + 1).ToString(),
                NumberSize,
                new Color(0.10f, 0.08f, 0.02f, 0.95f),
                new Color(1f, 0.82f, 0.30f, 1f),
                () => { });
            button.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(button);
            _numberButtons.Add(button);
        }

        for (int i = 0; i < _numberButtons.Count; i++)
        {
            var button = _numberButtons[i];
            if (button == null || !NodeQuery.IsLive(button))
                continue;

            if (i >= _cachedAncientButtons.Count
                || !NodeQuery.IsLive(_cachedAncientButtons[i])
                || !NodeQuery.IsVisible(_cachedAncientButtons[i])
                || !ControlHitboxService.TryGetDwellRect(_cachedAncientButtons[i], out var optionRect))
            {
                button.Visible = false;
                continue;
            }

            OverlayButtonFactory.ApplySize(button, NumberSize);

            float x = optionRect.Position.X - NumberSize - NumberGap;
            if (x < ScreenMargin)
                x = optionRect.End.X + NumberGap;

            float y = optionRect.GetCenter().Y - (NumberSize / 2f);
            button.GlobalPosition = new Vector2(x, y);
            button.Visible = true;
        }
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
        var list = new List<Control>();
        if (!NodeQuery.IsLive(room))
            return list;

        foreach (var button in NodeQuery.FindAll<NEventOptionButton>(room))
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
