using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// Blocks mouse from reaching hand/card controls while dwell buttons are active.
/// Skips controls owned by this mod (parented under DwellRow_* hosts).
/// </summary>
internal static class HandInputBlocker
{
    private static readonly List<(Control Control, Control.MouseFilterEnum Original)> _blocked = new();
    private static readonly HashSet<ulong> _blockedIds = new();
    private static bool _active;
    private static NPlayerHand? _blockedHand;
    private static int _lastHandChildCount = -1;
    private static int _framesSinceRescan;

    internal static bool IsDwellOverlayNode(Node node)
    {
        for (var current = node; current != null; current = current.GetParent())
        {
            string name = current.Name;
            if (name.StartsWith("DwellRow_", StringComparison.Ordinal)
                || name.StartsWith("DwellUtility_", StringComparison.Ordinal)
                || name == "DwellUtilityBar"
                || name == "DwellTargetingRoot"
                || name == "DwellEndTurnButton"
                || name == "DwellConfirmButton"
                || name == "DwellHandShield")
            {
                return true;
            }
        }

        return false;
    }

    internal static void Sync(NPlayerHand hand, bool shouldBlock)
    {
        if (!shouldBlock)
        {
            Release();
            return;
        }

        if (!_active || _blockedHand != hand)
        {
            _blockedHand = hand;
            _lastHandChildCount = -1;
            _framesSinceRescan = 0;
            ApplyFullBlock(hand);
            _active = true;
            return;
        }

        _framesSinceRescan++;
        int childCount = hand.GetChildCount();
        if (childCount != _lastHandChildCount || _framesSinceRescan >= ScanInterval.Frames())
        {
            _lastHandChildCount = childCount;
            _framesSinceRescan = 0;
            BlockNewControls(hand);
        }
    }

    internal static void Release()
    {
        if (!_active && _blocked.Count == 0)
            return;

        NodeQuery.RestoreMouseFilters(_blocked);
        _blocked.Clear();
        _blockedIds.Clear();
        _active = false;
        _blockedHand = null;
        _lastHandChildCount = -1;
        _framesSinceRescan = 0;
        ModLogger.Info("Hand input restored.");
    }

    private static void ApplyFullBlock(NPlayerHand hand)
    {
        if (hand is Control handControl && !IsDwellOverlayNode(handControl))
            BlockControlOnce(handControl);

        BlockNewControls(hand);
        _lastHandChildCount = hand.GetChildCount();
    }

    private static void BlockControlOnce(Control control)
    {
        if (IsDwellOverlayNode(control))
            return;

        ulong id = control.GetInstanceId();
        if (_blockedIds.Contains(id))
            return;

        _blockedIds.Add(id);
        _blocked.Add((control, control.MouseFilter));
        control.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static void BlockNewControls(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (IsDwellOverlayNode(node))
            return;

        if (node is Control control)
            BlockControlOnce(control);

        try
        {
            foreach (var child in node.GetChildren())
                BlockNewControls(child);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }
}
