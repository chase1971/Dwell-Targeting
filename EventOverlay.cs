using Godot;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for event option buttons (e.g. "Share Knowledge" / "Rip the Leech Off"). Options
/// are <see cref="NEventOptionButton"/> (NButton/NClickableControl) so we dwell over each and
/// ForceClick it.
///
/// Ancient encounters are a special case: each option is a relic whose effect is only revealed by a
/// tooltip when you hover the option. Direct hover-to-select would pick before you can read it, so
/// for ancient events we instead place a small offset number button beside each option. Hovering the
/// option body shows its tooltip (no dwell target there); dwelling the number actually picks it.
/// </summary>
internal static class EventOverlay
{
    private const int RescanIntervalFrames = 10;
    private const int CanvasLayerOrder = 131;
    private const int NumberSize = 54;
    private const float NumberGap = 14f;
    private const float ScreenMargin = 10f;

    private static NEventRoom? _room;
    private static List<Control>? _cachedButtons;
    private static bool _isAncient;
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
            _isAncient = _cachedButtons.Any(IsAncientOption);
        }

        if (_isAncient)
            SyncNumberButtons();
        else
            HideNumberButtons();

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info($"[Event] sync options={_cachedButtons?.Count ?? -1} ancient={_isAncient}.");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedButtons == null)
            return;

        // Ancient events: the dwell target is the offset number, NOT the option body, so the user can
        // hover the option to read its relic tooltip without triggering a pick.
        if (_isAncient)
        {
            for (int i = 0; i < _numberButtons.Count && i < _cachedButtons.Count; i++)
            {
                var button = _numberButtons[i];
                var option = _cachedButtons[i];
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

            return;
        }

        int slot = 1;
        foreach (var button in _cachedButtons)
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
            {
                slot++;
                continue;
            }

            if (ControlHitboxService.TryGetDwellRect(button, out var rect))
            {
                var captured = button;
                targets.Add(DwellHoverService.Menu(
                    rect,
                    () => EventSelectionService.TrySelect(captured),
                    $"EventOption:{slot}"));
            }

            slot++;
        }
    }

    internal static void Hide()
    {
        _room = null;
        _cachedButtons = null;
        _isAncient = false;
        _framesSinceScan = 0;
        HideNumberButtons();
    }

    private static bool IsAncientOption(Control option) =>
        NodeQuery.IsLive(option) && option.Name.ToString().Contains("Ancient", StringComparison.OrdinalIgnoreCase);

    private static void SyncNumberButtons()
    {
        if (_cachedButtons == null)
            return;

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;

        while (_numberButtons.Count < _cachedButtons.Count)
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

            if (i >= _cachedButtons.Count
                || !NodeQuery.IsLive(_cachedButtons[i])
                || !NodeQuery.IsVisible(_cachedButtons[i])
                || !ControlHitboxService.TryGetDwellRect(_cachedButtons[i], out var optionRect))
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

            list.Add(control);
        }

        // Top-to-bottom so the offset numbers (1/2/3) match the on-screen option order.
        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
            return cmp != 0 ? cmp : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
        });

        return list;
    }
}
