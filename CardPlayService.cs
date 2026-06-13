using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

internal static class CardPlayService
{
    internal static bool IsInCombatPlayMode()
    {
        if (!CombatManager.Instance.IsInProgress)
            return false;
        if (CombatManager.Instance.PlayerActionsDisabled)
            return false;

        var hand = NPlayerHand.Instance;
        if (hand == null)
            return false;
        if (hand.CurrentMode != NPlayerHand.Mode.Play)
            return false;

        return true;
    }

    internal static bool IsCardSelectedForTargeting()
    {
        if (!IsInCombatPlayMode())
            return false;

        return NPlayerHand.Instance!.InCardPlay;
    }

    internal static bool NeedsEnemyTarget(CardModel card) =>
        card.TargetType == TargetType.AnyEnemy;

    internal static string? TryPlay(CardModel card, int enemySlotOneBased)
    {
        ModLogger.Info($"TryPlay slot={enemySlotOneBased} card={card.Id.Entry}");
        return TryPlayInternal(card, enemySlotOneBased);
    }

    internal static string? TryPlay(NCardHolder holder, int enemySlotOneBased)
    {
        ModLogger.Info($"TryPlay slot={enemySlotOneBased} holder={holder.Name} id={holder.GetInstanceId()}");
        var card = holder.CardModel;
        if (card == null)
        {
            ModLogger.Warn("TryPlay blocked: holder has no CardModel.");
            return "No card on holder.";
        }

        return TryPlayInternal(card, enemySlotOneBased);
    }

    private static string? TryPlayInternal(CardModel card, int enemySlotOneBased)
    {
        if (!IsInCombatPlayMode())
        {
            ModLogger.Warn("TryPlay blocked: not in combat play mode.");
            return "Not in combat play mode.";
        }

        var hand = NPlayerHand.Instance;
        if (hand == null)
        {
            ModLogger.Warn("TryPlay blocked: no hand.");
            return "No hand.";
        }

        if (!RunManager.Instance.IsInProgress)
        {
            ModLogger.Warn("TryPlay blocked: no run in progress.");
            return "No run in progress.";
        }

        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState == null ? null : LocalContext.GetMe(runState);
        if (player == null)
        {
            ModLogger.Warn("TryPlay blocked: local player not found.");
            return "Local player not found.";
        }

        if (!card.CanPlay(out var reason, out _))
        {
            ModLogger.Warn($"TryPlay blocked: CanPlay=false reason={reason}");
            return $"Cannot play: {reason}";
        }

        Creature? target = null;
        if (NeedsEnemyTarget(card))
        {
            var enemies = EnemyOrderService.GetAliveEnemiesLeftToRight(player.Creature.CombatState);
            if (enemySlotOneBased < 1 || enemySlotOneBased > enemies.Count)
            {
                ModLogger.Warn($"TryPlay blocked: enemy slot {enemySlotOneBased} invalid (alive={enemies.Count}).");
                return $"Enemy slot {enemySlotOneBased} invalid ({enemies.Count} alive).";
            }

            target = enemies[enemySlotOneBased - 1];
            ModLogger.Info($"Target enemy slot {enemySlotOneBased}: combatId={target.CombatId}");
        }

        try
        {
            bool started = card.TryManualPlay(target);
            ModLogger.Info($"TryManualPlay({card.Id.Entry}) returned {started}");
            if (started)
                return null;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"TryManualPlay threw: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new PlayCardAction(card, target));
            ModLogger.Info($"PlayCardAction enqueued for {card.Id.Entry}");
            return null;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"PlayCardAction failed: {ex.GetType().Name}: {ex.Message}");
            return ex.Message;
        }
    }
}
