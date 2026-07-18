using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.TreasureRooms;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for non-combat interactive rooms: rest sites and treasure chests.
/// Rescans when room state changes so opened chests cannot be re-triggered via dwell.
/// </summary>
internal static class RoomOverlay
{
    private const long RescanMs = 250;
    private const float ButtonPadding = 10f;

    private static Node? _room;
    private static List<(Control control, bool isProceed)>? _cachedButtons;
    private static ulong _cachedRoomId;
    private static int _cachedLayoutKey;
    private static long _lastRescanTick;

    internal static void Sync()
    {
        _room = OverlayModeService.GetCachedRoomNode();
        if (_room == null)
        {
            Hide();
            return;
        }

        ulong roomId = _room.GetInstanceId();
        long now = System.Environment.TickCount64;
        int layoutKey = ComputeLayoutKey(_room);

        if (_cachedButtons != null
            && _cachedRoomId == roomId
            && _cachedLayoutKey == layoutKey
            && now - _lastRescanTick < RescanMs)
        {
            return;
        }

        _lastRescanTick = now;
        _cachedRoomId = roomId;
        _cachedLayoutKey = layoutKey;
        _cachedButtons = FindButtons(_room);
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

            if (!TryGetDwellRect(button, out var rect))
            {
                slot++;
                continue;
            }

            var captured = button;
            bool proceed = isProceed;
            targets.Add(DwellHoverService.Menu(
                rect,
                () => Activate(captured, proceed),
                proceed ? "RoomProceed" : $"RoomOption:{slot}"));

            slot++;
        }
    }

    internal static void Hide()
    {
        _room = null;
        _cachedButtons = null;
        _cachedRoomId = 0;
        _cachedLayoutKey = 0;
        _lastRescanTick = 0;
    }

    private static int ComputeLayoutKey(Node room)
    {
        int key = (int)room.GetInstanceId();
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        bool postOpenTreasure = room is NTreasureRoom && HasVisibleSkip(root);

        foreach (var button in NodeQuery.FindAll<NRestSiteButton>(room))
        {
            if (button is Control control)
                key = HashCode.Combine(key, (int)control.GetInstanceId(), control.IsVisible(), IsEnabled(control));
        }

        foreach (var button in NodeQuery.FindAll<NTreasureButton>(room))
        {
            if (button is Control control)
                key = HashCode.Combine(key, (int)control.GetInstanceId(), control.IsVisible(), IsEnabled(control), postOpenTreasure ? 1 : 0);
        }

        if (root != null)
        {
            foreach (var button in NodeQuery.FindAll<NProceedButton>(root))
            {
                if (button is Control control && NodeQuery.IsVisible(control))
                    key = HashCode.Combine(key, (int)control.GetInstanceId(), IsEnabled(control));
            }
        }

        return key;
    }

    private static bool TryGetDwellRect(Control button, out Rect2 rect)
    {
        if (ControlHitboxService.TryGetDwellRect(button, out rect))
        {
            rect = rect.Grow(ButtonPadding);
            return true;
        }

        rect = button.GetGlobalRect().Grow(ButtonPadding);
        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
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
            if (button is NProceedButton proceed)
            {
                RewardSelectionService.TryProceed(proceed);
                return;
            }

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
        if (!NodeQuery.IsLive(room))
            return list;

        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        bool postOpenTreasure = room is NTreasureRoom && HasVisibleSkip(root);

        foreach (var button in NodeQuery.FindAll<NRestSiteButton>(room))
            TryAdd(list, button, isProceed: false);

        if (room is NTreasureRoom)
        {
            if (!postOpenTreasure)
            {
                foreach (var button in NodeQuery.FindAll<NTreasureButton>(room))
                    TryAdd(list, button, isProceed: false);
            }
            else
            {
                foreach (var clickable in NodeQuery.FindAll<NClickableControl>(room))
                {
                    if (clickable is NTreasureButton)
                        continue;

                    TryAdd(list, clickable, isProceed: false);
                }
            }
        }

        if (root != null)
        {
            foreach (var button in NodeQuery.FindAll<NProceedButton>(root))
                TryAdd(list, button, isProceed: true);
        }

        return list;
    }

    private static bool HasVisibleSkip(Node? root)
    {
        if (root == null)
            return false;

        foreach (var skip in CardPickTargetQuery.FindSkipControls(root))
        {
            if (NodeQuery.IsLive(skip) && NodeQuery.IsVisible(skip))
                return true;
        }

        return false;
    }

    private static bool TryAdd(List<(Control, bool)> list, Node button, bool isProceed)
    {
        if (button is not Control control || !NodeQuery.IsVisible(control))
            return false;

        if (!IsEnabled(control))
            return false;

        if (list.Any(entry => entry.Item1 == control))
            return false;

        list.Add((control, isProceed));
        return true;
    }

    private static bool IsEnabled(Control control) =>
        control is not NClickableControl clickable || clickable.IsEnabled;
}
