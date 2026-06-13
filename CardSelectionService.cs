using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace DwellTargeting;

internal static class CardSelectionService
{
    internal static string? TrySelect(NCardHolder holder, int slotOneBased)
    {
        if (!OverlayModeService.IsHandOverlayActive())
            return "Overlay not active.";

        if (OverlayModeService.GetMode() != OverlayMode.HandSelect)
            return "Not in hand select mode.";

        var card = holder.CardModel;
        if (card == null)
            return "No card on holder.";

        if (TrySelectViaShortcutKey(slotOneBased))
            return null;

        if (CardAnchorService.TryGetCardRect(holder, out var rect))
            InputForwardService.ClickDeferred(rect.GetCenter(), MouseButton.Left);

        ModLogger.Info($"Select card {card.Id.Entry} slot {slotOneBased} fallback click.");
        return null;
    }

    private static bool TrySelectViaShortcutKey(int slotOneBased)
    {
        if (slotOneBased < 1 || slotOneBased > 9)
            return false;

        var key = (Key)((int)Key.Key1 + slotOneBased - 1);
        InputForwardService.PressKey(key);
        ModLogger.Info($"Select slot {slotOneBased} via key {key}.");
        return true;
    }
}
