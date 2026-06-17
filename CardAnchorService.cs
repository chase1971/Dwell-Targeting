using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace DwellTargeting;

internal static class CardAnchorService
{
    private const int DefaultCardHalfWidth = 80;
    private const int DefaultCardHeight = 220;
    private const float MinHandY = 400f;
    private const float PositionEpsilonSq = 0.25f;

    private readonly struct HolderAnchor
    {
        internal HolderAnchor(Control anchorItem, Vector2 lastItemPos, Rect2 lastRect)
        {
            AnchorItem = anchorItem;
            LastItemPos = lastItemPos;
            LastRect = lastRect;
        }

        internal Control AnchorItem { get; }
        internal Vector2 LastItemPos { get; }
        internal Rect2 LastRect { get; }
    }

    private static readonly Dictionary<ulong, HolderAnchor> _anchors = new();

    internal static void ClearCache() => _anchors.Clear();

    internal static bool TryGetCardRect(NCardHolder holder, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(holder))
            return false;

        ulong id = holder.GetInstanceId();
        if (_anchors.TryGetValue(id, out var anchor)
            && NodeQuery.IsLive(anchor.AnchorItem)
            && anchor.AnchorItem.GlobalPosition.DistanceSquaredTo(anchor.LastItemPos) < PositionEpsilonSq)
        {
            rect = anchor.LastRect;
            return rect.Size.X >= 1 && rect.Size.Y >= 1;
        }

        if (_anchors.TryGetValue(id, out anchor)
            && NodeQuery.IsLive(anchor.AnchorItem))
        {
            rect = anchor.AnchorItem.GetGlobalRect();
            if (rect.Size.X >= 20 && rect.Size.Y >= 20)
            {
                _anchors[id] = new HolderAnchor(anchor.AnchorItem, anchor.AnchorItem.GlobalPosition, rect);
                return true;
            }
        }

        if (!TryResolveAnchor(holder, out Control? anchorItem, out rect))
            return false;

        if (anchorItem != null)
        {
            _anchors[id] = new HolderAnchor(anchorItem, anchorItem.GlobalPosition, rect);
            return true;
        }

        _anchors.Remove(id);
        return rect.Size.X >= 1;
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

    private static bool TryResolveAnchor(NCardHolder holder, out Control? anchorItem, out Rect2 rect)
    {
        anchorItem = null;
        rect = default;

        foreach (var hitbox in NodeQuery.FindAll<NCardHolderHitbox>(holder))
        {
            if (hitbox is not Control hitboxControl || !NodeQuery.IsVisible(hitboxControl))
                continue;

            var hitRect = hitboxControl.GetGlobalRect();
            if (hitRect.Size.X >= 20 && hitRect.Size.Y >= 20)
            {
                anchorItem = hitboxControl;
                rect = hitRect;
                return true;
            }
        }

        if (holder is NHandCardHolder handHolder && NodeQuery.IsVisible(handHolder))
        {
            var holderRect = handHolder.GetGlobalRect();
            if (holderRect.Size.X >= 20 && holderRect.Size.Y >= 20)
            {
                anchorItem = handHolder;
                rect = holderRect;
                return true;
            }
        }

        foreach (var visual in NodeQuery.FindAll<NCard>(holder))
        {
            if (visual is not Control cardControl || !NodeQuery.IsVisible(cardControl))
                continue;

            var globalRect = cardControl.GetGlobalRect();
            if (globalRect.Size.X >= 24 && globalRect.Size.Y >= 24)
            {
                anchorItem = cardControl;
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
}
