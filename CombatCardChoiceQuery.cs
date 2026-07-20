using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DwellTargeting;

/// <summary>
/// Mid-combat card offers (Choice Paradox, boss "choose a card", etc.) — holders live outside
/// <see cref="NPlayerHand"/> and may not register as a standard pile-select screen node.
/// </summary>
internal static class CombatCardChoiceQuery
{
    private const int MinOfferCount = 1;

    internal static bool IsInstantPickFlow()
    {
        if (!CombatManager.Instance.IsInProgress)
            return false;

        return TryFindOfferScanRoot(out _) && !PilePreviewQuery.IsUpgradeConfirmFlowActive();
    }

    internal static bool TryFindOfferScanRoot(out Node scanRoot)
    {
        scanRoot = null!;
        if (!CombatManager.Instance.IsInProgress)
            return false;

        var offers = FindOfferHolders();
        if (offers.Count < MinOfferCount)
            return false;

        foreach (var holder in offers)
        {
            for (Node? node = holder; node != null; node = node.GetParent())
            {
                if (!PileSelectScreenMatcher.IsPileSelectScreen(node))
                    continue;

                if (node is CanvasItem canvas && NodeQuery.IsLive(canvas) && NodeQuery.IsVisible(canvas))
                {
                    scanRoot = node;
                    return true;
                }
            }
        }

        var parent = offers[0].GetParent();
        if (parent == null || !NodeQuery.IsLive(parent))
            return false;

        scanRoot = parent;
        return true;
    }

    internal static List<NCardHolder> FindOfferHolders()
    {
        var results = new List<NCardHolder>();
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return results;

        var hand = NPlayerHand.Instance;
        foreach (var holder in NodeQuery.FindAll<NCardHolder>(tree.Root))
        {
            if (!IsOfferHolder(holder, hand))
                continue;

            results.Add(holder);
        }

        results.Sort((left, right) =>
        {
            int cmp = left.GlobalPosition.X.CompareTo(right.GlobalPosition.X);
            return cmp != 0 ? cmp : left.GlobalPosition.Y.CompareTo(right.GlobalPosition.Y);
        });

        return results;
    }

    private static bool IsOfferHolder(NCardHolder holder, NPlayerHand? hand)
    {
        if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder) || holder.CardModel == null)
            return false;

        if (hand != null && NodeQuery.IsLive(hand) && hand.IsAncestorOf(holder))
            return false;

        if (!CardAnchorService.TryGetCardRect(holder, out var rect))
            return false;

        return rect.Size.X >= 40f && rect.Size.Y >= 60f;
    }
}
