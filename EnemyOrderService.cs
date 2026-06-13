using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace DwellTargeting;

internal static class EnemyOrderService
{
    /// <summary>
    /// Alive enemies in combat order. The game list is typically left-to-right on screen.
    /// </summary>
    internal static List<Creature> GetAliveEnemiesLeftToRight(ICombatState? combatState)
    {
        if (combatState == null)
            return [];

        return combatState.Enemies
            .Where(creature => creature.IsAlive)
            .OrderBy(creature => creature.CombatId ?? 0u)
            .ToList();
    }
}
