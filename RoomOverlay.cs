using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.TreasureRooms;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for non-combat interactive rooms: rest sites (Rest / Smith / gain-strength /
/// hatch-egg = <see cref="NRestSiteButton"/>) and treasure chests (<see cref="NTreasureButton"/>),
/// plus the room's <see cref="NProceedButton"/>. Option buttons are NClickableControl so ForceClick
/// drives them; the proceed button responds to the E accept key (same path the rewards screen uses).
/// </summary>
internal static class RoomOverlay
{
    private const int RescanIntervalFrames = 10;

    private static Node? _room;
    private static List<(Control control, bool isProceed)>? _cachedButtons;
    private static int _framesSinceScan;
    private static long _nextDiagTick;
    private static string _diagExtra = string.Empty;

    internal static void Sync()
    {
        _room = OverlayModeService.GetCachedRoomNode();
        if (_room == null)
        {
            Hide();
            return;
        }

        _framesSinceScan++;
        if (_cachedButtons == null || _framesSinceScan >= RescanIntervalFrames)
        {
            _framesSinceScan = 0;
            _cachedButtons = FindButtons(_room);
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info($"[Room] {_room.GetType().Name} buttons={_cachedButtons?.Count ?? -1}.{_diagExtra}");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedButtons == null)
            return;

        int slot = 1;
        foreach (var (button, isProceed) in _cachedButtons)
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
            {
                slot++;
                continue;
            }

            if (ControlHitboxService.TryGetDwellRect(button, out var rect))
            {
                var captured = button;
                bool proceed = isProceed;
                targets.Add(DwellHoverService.Menu(
                    rect,
                    () => Activate(captured, proceed),
                    proceed ? "RoomProceed" : $"RoomOption:{slot}"));
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

    private static void Activate(Control button, bool isProceed)
    {
        if (!NodeQuery.IsLive(button))
        {
            ModLogger.Warn("Room button not live.");
            return;
        }

        if (isProceed)
        {
            // Proceed / leave-room buttons advance via the E accept key (ForceClick does not reliably
            // drive them — same behaviour as the rewards-screen Proceed/Skip).
            InputForwardService.PressAcceptKey();
            ModLogger.Info($"Room proceed '{button.Name}' via E accept key.");
            return;
        }

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"Room option '{button.Name}' activated.");
        else
            ModLogger.Warn($"Room option '{button.Name}' activation failed.");
    }

    private static List<(Control, bool)> FindButtons(Node room)
    {
        var list = new List<(Control, bool)>();
        _diagExtra = string.Empty;
        if (!NodeQuery.IsLive(room))
            return list;

        foreach (var button in NodeQuery.FindAll<NRestSiteButton>(room))
            TryAdd(list, button, isProceed: false);

        foreach (var button in NodeQuery.FindAll<NTreasureButton>(room))
            TryAdd(list, button, isProceed: false);

        // The chest's relic-claim node has an unknown type, so for treasure rooms offer every clickable
        // control inside the room (deduped) and log their type-names so we can wire it precisely. This
        // is what lets the relic inside the opened chest be selected (not just the Skip button).
        if (room is NTreasureRoom)
        {
            var types = new HashSet<string>();
            foreach (var clickable in NodeQuery.FindAll<NClickableControl>(room))
            {
                if (TryAdd(list, clickable, isProceed: false))
                    types.Add(clickable.GetType().Name);
            }

            if (types.Count > 0)
                _diagExtra = $" treasureClickables=[{string.Join(",", types)}]";
        }

        // The proceed/leave button is often a screen-level element (like the rewards Proceed), not a
        // descendant of the room node — so search from the scene root, filtered to visible+enabled.
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root != null)
        {
            foreach (var button in NodeQuery.FindAll<NProceedButton>(root))
                TryAdd(list, button, isProceed: true);
        }

        return list;
    }

    private static bool TryAdd(List<(Control, bool)> list, Node button, bool isProceed)
    {
        if (button is not Control control || !NodeQuery.IsVisible(control))
            return false;
        if (button is NClickableControl { IsEnabled: false })
            return false;
        if (list.Any(entry => entry.Item1 == control))
            return false;

        list.Add((control, isProceed));
        return true;
    }
}
