using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace DwellTargeting;

internal static class CardAnchorService
{
    private const int DefaultCardHalfWidth = 80;
    private const int DefaultCardHeight = 220;
    private const float MinHandY = 400f;

    internal static bool TryGetCardRect(NCardHolder holder, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(holder))
            return false;

        foreach (var hitbox in NodeQuery.FindAll<NCardHolderHitbox>(holder))
        {
            if (!NodeQuery.IsVisible(hitbox))
                continue;

            var hitRect = hitbox.GetGlobalRect();
            if (hitRect.Size.X >= 20 && hitRect.Size.Y >= 20)
            {
                rect = hitRect;
                return true;
            }
        }

        if (holder is NHandCardHolder handHolder && NodeQuery.IsVisible(handHolder))
        {
            var holderRect = handHolder.GetGlobalRect();
            if (holderRect.Size.X >= 20 && holderRect.Size.Y >= 20)
            {
                rect = holderRect;
                return true;
            }
        }

        foreach (var visual in NodeQuery.FindAll<NCard>(holder))
        {
            if (!NodeQuery.IsVisible(visual))
                continue;

            var globalRect = visual.GetGlobalRect();
            if (globalRect.Size.X >= 24 && globalRect.Size.Y >= 24)
            {
                rect = globalRect;
                return true;
            }
        }

        if (holder.GlobalPosition.Y < MinHandY)
            return false;

        rect = new Rect2(
            holder.GlobalPosition.X,
            holder.GlobalPosition.Y,
            DefaultCardHalfWidth * 2f,
            DefaultCardHeight);
        return true;
    }

    internal static bool TryGetLocalCardRect(NCardHolder holder, out Rect2 rect)
    {
        rect = default;
        if (!TryGetCardRect(holder, out Rect2 globalRect))
            return false;

        if (holder is not Control holderControl)
            return false;

        rect = holderControl.GetGlobalTransformWithCanvas().AffineInverse() * globalRect;
        return rect.Size.X >= 1 && rect.Size.Y >= 1;
    }
}
