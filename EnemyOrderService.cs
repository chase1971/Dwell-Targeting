using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

internal static class EnemyOrderService
{
    private const int NodeScanIntervalFrames = 30;

    private static readonly List<NCreature> _cachedNodes = new();
    private static int _framesSinceNodeScan;
    private static int _lastAliveCount = -1;

    /// <summary>
    /// Alive enemies left-to-right on screen, matching card target button order.
    /// </summary>
    internal static List<Creature> GetAliveEnemiesLeftToRight(ICombatState? combatState)
    {
        if (combatState == null)
            return [];

        var alive = combatState.Enemies
            .Where(creature => creature.IsAlive)
            .ToList();

        if (alive.Count <= 1)
            return alive;

        var nodes = GetVisibleEnemyNodesCached();
        return alive
            .OrderBy(creature => GetSortKeyX(creature, nodes))
            .ThenBy(creature => creature.CombatId ?? 0u)
            .ToList();
    }

    internal static IReadOnlyList<NCreature> GetVisibleEnemyNodesCached()
    {
        _framesSinceNodeScan++;
        if (_framesSinceNodeScan < NodeScanIntervalFrames && _cachedNodes.Count > 0)
            return _cachedNodes;

        _framesSinceNodeScan = 0;
        _cachedNodes.Clear();
        _cachedNodes.AddRange(FindVisibleEnemyNodes());
        return _cachedNodes;
    }

    internal static void InvalidateNodeCache()
    {
        _framesSinceNodeScan = NodeScanIntervalFrames;
        _lastAliveCount = -1;
    }

    internal static bool DidAliveCountChange(int aliveCount)
    {
        if (_lastAliveCount == aliveCount)
            return false;

        _lastAliveCount = aliveCount;
        InvalidateNodeCache();
        return true;
    }

    internal static List<NCreature> FindVisibleEnemyNodes()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return [];

        return NodeQuery.FindAll<NCreature>(tree.Root)
            .Where(IsVisibleEnemyNode)
            .ToList();
    }

    internal static NCreature? FindNodeForCreature(Creature creature, IReadOnlyList<NCreature>? cachedNodes = null)
    {
        var nodes = cachedNodes ?? GetVisibleEnemyNodesCached();
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Entity, creature))
                return node;
        }

        return null;
    }

    internal static bool TryGetLabelAnchor(NCreature creature, out Vector2 centerTop)
    {
        centerTop = default;
        if (!NodeQuery.IsLive(creature) || !NodeQuery.IsVisible(creature))
            return false;

        try
        {
            centerTop = creature.GetTopOfHitbox();
            if (centerTop != Vector2.Zero)
                return true;
        }
        catch
        {
            /* fall through */
        }

        if (creature.Hitbox != null && NodeQuery.IsLive(creature.Hitbox) && NodeQuery.IsVisible(creature.Hitbox))
        {
            var rect = creature.Hitbox.GetGlobalRect();
            centerTop = new Vector2(rect.GetCenter().X, rect.Position.Y);
            return true;
        }

        var fallback = creature.GetGlobalRect();
        if (fallback.Size.X < 1f || fallback.Size.Y < 1f)
            return false;

        centerTop = new Vector2(fallback.GetCenter().X, fallback.Position.Y);
        return true;
    }

    private static bool IsVisibleEnemyNode(NCreature node) =>
        NodeQuery.IsVisible(node)
        && node.Entity is { IsAlive: true, IsEnemy: true };

    private static float GetSortKeyX(Creature creature, IReadOnlyList<NCreature> nodes)
    {
        var node = FindNodeForCreature(creature, nodes);
        if (node == null)
            return float.MaxValue;

        if (node.Hitbox != null && NodeQuery.IsLive(node.Hitbox) && NodeQuery.IsVisible(node.Hitbox))
            return node.Hitbox.GetGlobalRect().GetCenter().X;

        return node.GetGlobalRect().GetCenter().X;
    }
}
