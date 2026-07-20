using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DwellTargeting;

internal static class PileSelectScreenMatcher
{
    internal static bool IsPileSelectScreen(Node node)
    {
        if (node is NCombatPileCardSelectScreen
            or NChooseACardSelectionScreen
            or NCardGridSelectionScreen
            or NDeckUpgradeSelectScreen
            or NSimpleCardSelectScreen
            or NCardRewardSelectionScreen)
        {
            return true;
        }

        string typeName = node.GetType().Name;
        return typeName.Contains("CardSelection", StringComparison.Ordinal)
            || typeName.Contains("ChooseACard", StringComparison.Ordinal)
            || typeName.Contains("CardReward", StringComparison.Ordinal)
            || typeName.Contains("UpgradeSelect", StringComparison.Ordinal)
            || typeName.Contains("SelectionScreen", StringComparison.Ordinal);
    }
}
