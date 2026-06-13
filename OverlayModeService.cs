using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

internal enum OverlayMode
{
    None,
    CombatPlay,
    HandSelect,
    PileSelect,
    Rewards
}

internal static class OverlayModeService
{
    internal static OverlayMode GetMode()
    {
        if (IsRewardsScreenActive())
            return OverlayMode.Rewards;

        if (!RunManager.Instance.IsInProgress)
            return OverlayMode.None;

        if (TryGetPileSelectScreen(out _))
            return OverlayMode.PileSelect;

        if (!CombatManager.Instance.IsInProgress)
            return OverlayMode.None;

        if (CombatManager.Instance.PlayerActionsDisabled)
            return OverlayMode.None;

        var hand = NPlayerHand.Instance;
        if (hand == null)
            return OverlayMode.None;

        if (hand.CurrentMode == NPlayerHand.Mode.Play)
            return OverlayMode.CombatPlay;

        if (hand.CurrentMode == NPlayerHand.Mode.SimpleSelect
            || hand.CurrentMode == NPlayerHand.Mode.UpgradeSelect)
        {
            return OverlayMode.HandSelect;
        }

        return OverlayMode.None;
    }

    internal static bool IsHandOverlayActive()
    {
        var mode = GetMode();
        return mode is OverlayMode.CombatPlay or OverlayMode.HandSelect;
    }

    internal static bool TryGetPileSelectScreen(out Node screen)
    {
        screen = null!;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        foreach (var candidate in NodeQuery.FindAll<NCombatPileCardSelectScreen>(tree.Root))
        {
            if (!NodeQuery.IsVisible(candidate))
                continue;
            screen = candidate;
            return true;
        }

        foreach (var candidate in NodeQuery.FindAll<NChooseACardSelectionScreen>(tree.Root))
        {
            if (!NodeQuery.IsVisible(candidate))
                continue;
            screen = candidate;
            return true;
        }

        foreach (var candidate in NodeQuery.FindAll<NCardGridSelectionScreen>(tree.Root))
        {
            if (!NodeQuery.IsVisible(candidate))
                continue;
            screen = candidate;
            return true;
        }

        foreach (var candidate in NodeQuery.FindAll<NSimpleCardSelectScreen>(tree.Root))
        {
            if (!NodeQuery.IsVisible(candidate))
                continue;
            screen = candidate;
            return true;
        }

        return false;
    }

    private static bool IsRewardsScreenActive()
    {
        if (!RunManager.Instance.IsInProgress)
            return false;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        foreach (var candidate in NodeQuery.FindAll<NRewardsScreen>(tree.Root))
        {
            if (NodeQuery.IsVisible(candidate))
                return true;
        }

        return false;
    }
}
