using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

internal static class PileCardSelectionService
{
    internal static void TrySelect(NCard card, int slotOneBased)
    {
        if (slotOneBased >= 1 && slotOneBased <= 9)
        {
            var key = (Key)((int)Key.Key1 + slotOneBased - 1);
            InputForwardService.PressKey(key);
            ModLogger.Info($"Pile select slot {slotOneBased} via key {key}.");
        }

        InputForwardService.ClickCard(card, MouseButton.Left);
        ModLogger.Info($"Pile select slot {slotOneBased} card click.");
    }
}
