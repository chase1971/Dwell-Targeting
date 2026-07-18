using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Dwell on native potion slots in the top bar (combat, rewards, card picks, etc.).
/// Popup choices stay in <see cref="PotionPopupOverlay"/>.
/// </summary>
internal static class PotionSlotOverlay
{
    private const float SlotPadding = 8f;
    private const long RescanMs = 500;

    private static List<(Rect2 Bounds, Control Slot, int SlotIndex)>? _targets;
    private static long _nextRescanTick;

    internal static void Sync()
    {
        if (!RunManager.Instance.IsInProgress)
        {
            Hide();
            return;
        }

        long now = System.Environment.TickCount64;
        if (_targets != null && now < _nextRescanTick)
            return;

        _nextRescanTick = now + RescanMs;
        RebuildTargets();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_targets == null)
            return;

        foreach (var (bounds, slot, slotIndex) in _targets)
        {
            if (!NodeQuery.IsLive(slot) || !NodeQuery.IsVisible(slot))
                continue;

            var captured = slot;
            int capturedIndex = slotIndex;
            targets.Add(DwellHoverService.Menu(
                bounds,
                () => Activate(captured, capturedIndex),
                $"PotionSlot:{capturedIndex}"));
        }
    }

    internal static void Hide()
    {
        _targets = null;
        _nextRescanTick = 0;
    }

    private static void RebuildTargets()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
        {
            _targets = null;
            return;
        }

        var holders = NodeQuery.FindAll<NPotionHolder>(root)
            .Where(IsSelectable)
            .Cast<Control>()
            .OrderBy(control => control.GlobalPosition.X)
            .ToList();

        if (holders.Count == 0)
        {
            _targets = null;
            return;
        }

        var list = new List<(Rect2, Control, int)>();
        for (int i = 0; i < holders.Count; i++)
        {
            var holder = holders[i];
            if (!TryMeasureSlotRect(holder, out var rect))
                continue;

            list.Add((rect, holder, i + 1));
        }

        _targets = list.Count > 0 ? list : null;
    }

    private static bool TryMeasureSlotRect(Control slot, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(slot) || !NodeQuery.IsVisible(slot))
            return false;

        if (TryGetClickableRect(slot, out rect))
            return true;

        rect = slot.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(SlotPadding);
        return true;
    }

    private static bool TryGetClickableRect(Control root, out Rect2 rect)
    {
        rect = default;
        float bestArea = 0f;

        foreach (var clickable in NodeQuery.FindAll<NClickableControl>(root))
        {
            if (clickable is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (clickable is NClickableControl { IsEnabled: false })
                continue;

            var candidate = control.GetGlobalRect();
            float area = candidate.Size.X * candidate.Size.Y;
            if (area > bestArea && candidate.Size.X >= 12f && candidate.Size.Y >= 12f)
            {
                bestArea = area;
                rect = candidate.Grow(SlotPadding);
            }
        }

        return bestArea > 0f;
    }

    private static bool IsSelectable(NPotionHolder holder) =>
        NodeQuery.IsLive(holder)
        && NodeQuery.IsVisible(holder)
        && holder is not NClickableControl { IsEnabled: false };

    private static void Activate(Control slot, int slotIndex)
    {
        if (!NodeQuery.IsLive(slot))
        {
            ModLogger.Warn($"[Potion] slot {slotIndex} not live.");
            return;
        }

        if (InputForwardService.TryActivateControl(slot))
            ModLogger.Info($"[Potion] slot {slotIndex} '{slot.Name}' activated.");
        else
            ModLogger.Warn($"[Potion] slot {slotIndex} activation failed.");
    }
}
