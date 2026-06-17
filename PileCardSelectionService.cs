using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace DwellTargeting;

internal static class PileCardSelectionService
{
    private static MethodInfo? _emitPressed;

    /// <summary>
    /// Selects a card on a pile/choose/card-reward screen. These screens connect to the holder's own
    /// <c>Pressed</c> signal (NClickableControl on the hitbox has no Pressed signal and ForceClick on it
    /// does not drive the holder's press/release pair), so we emit the holder's Pressed signal directly.
    /// </summary>
    internal static void TrySelect(NCardHolder holder, int slotOneBased)
    {
        if (!NodeQuery.IsLive(holder))
        {
            ModLogger.Warn($"Pile select slot {slotOneBased}: holder not live.");
            return;
        }

        try
        {
            holder.EmitSignal("Pressed", holder);
            ModLogger.Info($"Pile select slot {slotOneBased} via holder Pressed signal.");
            return;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Pile select slot {slotOneBased} EmitSignal failed: {ex.Message}");
        }

        try
        {
            _emitPressed ??= typeof(NCardHolder).GetMethod(
                "EmitPressed",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_emitPressed != null)
            {
                _emitPressed.Invoke(holder, null);
                ModLogger.Info($"Pile select slot {slotOneBased} via EmitPressed().");
            }
            else
            {
                ModLogger.Warn("Pile select: EmitPressed method not found.");
            }
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Pile select slot {slotOneBased} EmitPressed failed: {ex.Message}");
        }
    }
}
