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



    internal readonly struct CardPlacement

    {

        internal Rect2 Bounds { get; init; }

        /// <summary>Top-center of the card art in the same coordinate space as <see cref="Bounds"/>.</summary>

        internal Vector2 ButtonAnchor { get; init; }

    }



    private readonly struct HolderAnchor

    {

        internal HolderAnchor(Control anchorItem, Vector2 lastItemPos, Rect2 lastRect, Vector2 lastButtonAnchor)

        {

            AnchorItem = anchorItem;

            LastItemPos = lastItemPos;

            LastRect = lastRect;

            LastButtonAnchor = lastButtonAnchor;

        }



        internal Control AnchorItem { get; }

        internal Vector2 LastItemPos { get; }

        internal Rect2 LastRect { get; }

        internal Vector2 LastButtonAnchor { get; }

    }



    private static readonly Dictionary<ulong, HolderAnchor> _anchors = new();



    internal static void ClearCache() => _anchors.Clear();



    internal static bool TryGetCardRect(NCardHolder holder, out Rect2 rect)
    {
        if (TryGetCardPlacement(holder, out var placement))
        {
            rect = placement.Bounds;
            return true;
        }

        rect = default;
        return false;
    }



    internal static bool TryGetCardPlacement(NCardHolder holder, out CardPlacement placement)

    {

        placement = default;

        if (!NodeQuery.IsLive(holder))

            return false;



        ulong id = holder.GetInstanceId();

        if (_anchors.TryGetValue(id, out var anchor)

            && NodeQuery.IsLive(anchor.AnchorItem)

            && anchor.AnchorItem.GlobalPosition.DistanceSquaredTo(anchor.LastItemPos) < PositionEpsilonSq)

        {

            placement = new CardPlacement

            {

                Bounds = anchor.LastRect,

                ButtonAnchor = anchor.LastButtonAnchor

            };

            return placement.Bounds.Size.X >= 1 && placement.Bounds.Size.Y >= 1;

        }



        if (_anchors.TryGetValue(id, out anchor)

            && NodeQuery.IsLive(anchor.AnchorItem)

            && TryMeasureControl(anchor.AnchorItem, out var measured))

        {

            placement = measured;

            _anchors[id] = new HolderAnchor(

                anchor.AnchorItem,

                anchor.AnchorItem.GlobalPosition,

                measured.Bounds,

                measured.ButtonAnchor);

            return true;

        }



        if (!TryResolveAnchor(holder, out Control? anchorItem, out placement))

            return false;



        if (anchorItem != null)

        {

            _anchors[id] = new HolderAnchor(

                anchorItem,

                anchorItem.GlobalPosition,

                placement.Bounds,

                placement.ButtonAnchor);

            return true;

        }



        _anchors.Remove(id);

        return placement.Bounds.Size.X >= 1;

    }



    internal static bool TryGetLocalCardPlacement(NCardHolder holder, out CardPlacement placement)

    {

        placement = default;

        if (!TryGetCardPlacement(holder, out CardPlacement globalPlacement))

            return false;



        if (holder is not Control holderControl)

            return false;



        var inverse = holderControl.GetGlobalTransformWithCanvas().AffineInverse();

        placement = new CardPlacement

        {

            Bounds = inverse * globalPlacement.Bounds,

            ButtonAnchor = inverse * globalPlacement.ButtonAnchor

        };

        return placement.Bounds.Size.X >= 1 && placement.Bounds.Size.Y >= 1;

    }



    internal static bool TryGetLocalCardRect(NCardHolder holder, out Rect2 rect)
    {
        if (TryGetLocalCardPlacement(holder, out var placement))
        {
            rect = placement.Bounds;
            return true;
        }

        rect = default;
        return false;
    }



    private static bool TryResolveAnchor(NCardHolder holder, out Control? anchorItem, out CardPlacement placement)

    {

        anchorItem = null;

        placement = default;



        if (holder is NHandCardHolder)

        {

            foreach (var visual in NodeQuery.FindAll<NCard>(holder))

            {

                if (visual is not Control cardControl || !NodeQuery.IsVisible(cardControl))

                    continue;



                if (TryMeasureOrientedCard(cardControl, out placement))

                {

                    anchorItem = cardControl;

                    return true;

                }

            }

        }



        foreach (var hitbox in NodeQuery.FindAll<NCardHolderHitbox>(holder))

        {

            if (hitbox is not Control hitboxControl || !NodeQuery.IsVisible(hitboxControl))

                continue;



            if (TryMeasureAxisAlignedControl(hitboxControl, out placement))

            {

                anchorItem = hitboxControl;

                return true;

            }

        }



        if (holder is NHandCardHolder handHolder && NodeQuery.IsVisible(handHolder))

        {

            if (TryMeasureAxisAlignedControl(handHolder, out placement))

            {

                anchorItem = handHolder;

                return true;

            }

        }



        foreach (var visual in NodeQuery.FindAll<NCard>(holder))

        {

            if (visual is not Control cardControl || !NodeQuery.IsVisible(cardControl))

                continue;



            if (TryMeasureOrientedCard(cardControl, out placement)

                || TryMeasureAxisAlignedControl(cardControl, out placement))

            {

                anchorItem = cardControl;

                return true;

            }

        }



        if (holder.GlobalPosition.Y < MinHandY && holder is NHandCardHolder)

            return false;



        var fallback = new Rect2(

            holder.GlobalPosition.X,

            holder.GlobalPosition.Y,

            DefaultCardHalfWidth * 2f,

            DefaultCardHeight);

        placement = new CardPlacement

        {

            Bounds = fallback,

            ButtonAnchor = new Vector2(fallback.Position.X + (fallback.Size.X / 2f), fallback.Position.Y)

        };

        return true;

    }



    private static bool TryMeasureControl(Control control, out CardPlacement placement)

    {

        if (control is NCard)

            return TryMeasureOrientedCard(control, out placement);



        return TryMeasureAxisAlignedControl(control, out placement);

    }



    /// <summary>

    /// Fan-layout cards rotate in the hand. Axis-aligned box centers drift right on outer cards; anchor

    /// buttons to the transformed top edge instead.

    /// </summary>

    private static bool TryMeasureOrientedCard(Control cardControl, out CardPlacement placement)

    {

        placement = default;



        var size = cardControl.Size;

        if (size.X < 1f || size.Y < 1f)

            size = cardControl.GetRect().Size;



        if (size.X < 24f || size.Y < 24f)

            return false;



        var transform = cardControl.GetGlobalTransformWithCanvas();

        var topLeft = transform * Vector2.Zero;

        var topRight = transform * new Vector2(size.X, 0f);

        var bottomRight = transform * new Vector2(size.X, size.Y);

        var bottomLeft = transform * new Vector2(0f, size.Y);



        float minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));

        float maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));

        float minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));

        float maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));



        var bounds = new Rect2(minX, minY, maxX - minX, maxY - minY);

        if (bounds.Size.X < 24f || bounds.Size.Y < 24f)

            return false;



        placement = new CardPlacement

        {

            Bounds = bounds,

            ButtonAnchor = (topLeft + topRight) * 0.5f

        };

        return true;

    }



    private static bool TryMeasureAxisAlignedControl(Control control, out CardPlacement placement)

    {

        placement = default;



        var rect = control.GetGlobalRect();

        if (rect.Size.X < 20f || rect.Size.Y < 20f)

            return false;



        placement = new CardPlacement

        {

            Bounds = rect,

            ButtonAnchor = new Vector2(rect.Position.X + (rect.Size.X / 2f), rect.Position.Y)

        };

        return true;

    }

}


