using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// Logs hand layout snapshots when card count changes (for calibrating button placement).
/// </summary>
internal static class HandLayoutDiagnostics
{
    private static int _lastHandSize = -1;

    internal static void MaybeLog(NPlayerHand hand, IReadOnlyList<NCardHolder> holders)
    {
        // Calibration-only dump. It writes several lines to disk synchronously on the game thread
        // every time the hand size changes (i.e. while drawing/playing cards), which caused the
        // intermittent card-play stutter. Keep it off unless perf/debug logging is explicitly on.
        if (!SettingsStore.Current.EnablePerfLogging)
            return;

        int handSize = holders.Count(h => h.CardModel != null && NodeQuery.IsVisible(h));
        if (handSize == _lastHandSize)
            return;

        _lastHandSize = handSize;
        ModLogger.Info($"=== HandLayout handSize={handSize} holders={holders.Count} ===");

        for (int i = 0; i < holders.Count; i++)
        {
            var holder = holders[i];
            var card = holder.CardModel;
            if (card == null || !NodeQuery.IsVisible(holder))
                continue;

            if (!CardAnchorService.TryGetCardPlacement(holder, out var placement))
                continue;

            ModLogger.Info(
                $"  slot={i + 1} card={card.Id.Entry} holderPos=({holder.GlobalPosition.X:F0},{holder.GlobalPosition.Y:F0}) " +
                $"rect=({placement.Bounds.Position.X:F0},{placement.Bounds.Position.Y:F0},{placement.Bounds.Size.X:F0}x{placement.Bounds.Size.Y:F0}) " +
                $"anchor=({placement.ButtonAnchor.X:F0},{placement.ButtonAnchor.Y:F0})");
        }
    }

    internal static void Reset()
    {
        _lastHandSize = -1;
    }
}
