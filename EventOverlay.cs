using Godot;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for event option buttons (e.g. "Share Knowledge" / "Rip the Leech Off"). Options are
/// <see cref="NEventOptionButton"/> (NButton/NClickableControl) so we dwell over each and ForceClick it.
/// </summary>
internal static class EventOverlay
{
    private const int RescanIntervalFrames = 10;

    private static NEventRoom? _room;
    private static List<Control>? _cachedButtons;
    private static int _framesSinceScan;
    private static long _nextDiagTick;

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
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info($"[Event] sync options={_cachedButtons?.Count ?? -1}.");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedButtons == null)
            return;

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
        _framesSinceScan = 0;
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

        return list;
    }
}
