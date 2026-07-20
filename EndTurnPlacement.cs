using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace DwellTargeting;

/// <summary>
/// Picks where the large center End Turn overlay sits. Wide enemy formations (e.g. a back-row
/// creature far right) leave empty space near the player — shift E left instead of viewport center.
/// When that lane overlaps the player model, nudge E upward.
/// </summary>
internal static class EndTurnPlacement
{
    private const float DefaultCenterXFraction = 0.5f;
    private const float DefaultCenterYFraction = 0.52f;
    private const float MinPlayerLaneFraction = 0.24f;
    private const float MaxShiftedCenterFraction = 0.42f;
    private const float GapBeforeLeftmostEnemy = 56f;
    private const float WideSpreadFraction = 0.38f;
    private const float FarRightEnemyFraction = 0.68f;
    private const float PlayerLaneUpwardNudgePx = 28f;
    private const float GapAbovePlayerHitbox = 16f;
    private const float PlayerOverlapPadding = 8f;

    private static NCreature? _cachedPlayerNode;

    internal static void InvalidatePlayerCache() => _cachedPlayerNode = null;

    internal static Vector2 ResolveCenter(Vector2 viewportSize, Vector2 buttonSize)
    {
        float centerX = viewportSize.X * DefaultCenterXFraction;
        float centerY = viewportSize.Y * DefaultCenterYFraction;
        bool shiftedLeft = TryGetWideFormationCenter(viewportSize, buttonSize, out float shiftedX);

        if (shiftedLeft)
        {
            centerX = shiftedX;
            centerY -= PlayerLaneUpwardNudgePx;
        }

        if (TryNudgeAbovePlayer(centerX, centerY, buttonSize, out float abovePlayerY))
            centerY = abovePlayerY;

        float minCenterY = (buttonSize.Y / 2f) + 12f;
        centerY = Math.Max(minCenterY, centerY);
        return new Vector2(centerX, centerY);
    }

    private static bool TryNudgeAbovePlayer(float centerX, float centerY, Vector2 buttonSize, out float adjustedCenterY)
    {
        adjustedCenterY = centerY;
        if (!TryGetPlayerHitboxRect(out Rect2 playerRect))
            return false;

        var buttonRect = new Rect2(
            centerX - (buttonSize.X / 2f),
            centerY - (buttonSize.Y / 2f),
            buttonSize.X,
            buttonSize.Y);

        if (!buttonRect.Intersects(playerRect.Grow(PlayerOverlapPadding)))
            return false;

        adjustedCenterY = playerRect.Position.Y - GapAbovePlayerHitbox - (buttonSize.Y / 2f);
        return true;
    }

    private static bool TryGetPlayerHitboxRect(out Rect2 rect)
    {
        rect = default;
        if (_cachedPlayerNode != null
            && (!NodeQuery.IsLive(_cachedPlayerNode)
                || !NodeQuery.IsVisible(_cachedPlayerNode)
                || _cachedPlayerNode.Entity is not { IsAlive: true, IsEnemy: false }))
        {
            _cachedPlayerNode = null;
        }

        if (_cachedPlayerNode == null)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree?.Root == null)
                return false;

            foreach (var node in NodeQuery.FindAll<NCreature>(tree.Root))
            {
                if (node.Entity is not { IsAlive: true, IsEnemy: false })
                    continue;

                if (!NodeQuery.IsVisible(node))
                    continue;

                _cachedPlayerNode = node;
                break;
            }
        }

        if (_cachedPlayerNode == null)
            return false;

        if (_cachedPlayerNode.Hitbox != null
            && NodeQuery.IsLive(_cachedPlayerNode.Hitbox)
            && NodeQuery.IsVisible(_cachedPlayerNode.Hitbox))
        {
            rect = _cachedPlayerNode.Hitbox.GetGlobalRect();
            return rect.Size.X >= 1f && rect.Size.Y >= 1f;
        }

        rect = _cachedPlayerNode.GetGlobalRect();
        return rect.Size.X >= 1f && rect.Size.Y >= 1f;
    }

    private static bool TryGetWideFormationCenter(Vector2 viewportSize, Vector2 buttonSize, out float centerX)
    {
        centerX = viewportSize.X * DefaultCenterXFraction;
        var nodes = EnemyOrderService.GetVisibleEnemyNodesCached();
        if (nodes.Count == 0)
            return false;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (var node in nodes)
        {
            if (!NodeQuery.IsLive(node) || !NodeQuery.IsVisible(node))
                continue;

            Rect2 rect = node.Hitbox != null && NodeQuery.IsLive(node.Hitbox) && NodeQuery.IsVisible(node.Hitbox)
                ? node.Hitbox.GetGlobalRect()
                : node.GetGlobalRect();

            float x = rect.GetCenter().X;
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }

        if (minX == float.MaxValue)
            return false;

        float viewportW = viewportSize.X;
        float spread = maxX - minX;
        if (spread <= viewportW * WideSpreadFraction && maxX <= viewportW * FarRightEnemyFraction)
            return false;

        float minCenter = viewportW * MinPlayerLaneFraction + (buttonSize.X / 2f);
        float maxCenter = viewportW * MaxShiftedCenterFraction;
        float leftOfEnemies = minX - GapBeforeLeftmostEnemy - (buttonSize.X / 2f);
        centerX = Math.Clamp(leftOfEnemies, minCenter, maxCenter);
        return true;
    }
}
