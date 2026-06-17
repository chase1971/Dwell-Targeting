using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// Reads live reward/proceed controls from NRewardsScreen via the game's own lists.
/// </summary>
internal static class RewardsScreenQuery
{
    private static readonly FieldInfo? RewardButtonsField =
        typeof(NRewardsScreen).GetField("_rewardButtons", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? ProceedButtonField =
        typeof(NRewardsScreen).GetField("_proceedButton", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static bool HasVisibleChoices(NRewardsScreen screen)
    {
        foreach (var reward in GetRewardButtons(screen))
        {
            if (IsLiveChoice(reward))
                return true;
        }

        var proceed = GetProceedButton(screen);
        return proceed != null && IsLiveChoice(proceed);
    }

    internal static List<NRewardButton> GetRewardButtons(NRewardsScreen screen)
    {
        var results = new List<NRewardButton>();
        if (!NodeQuery.IsLive(screen))
            return results;

        if (TryReadRewardList(screen, out var listed) && listed.Count > 0)
        {
            foreach (var reward in listed)
            {
                if (reward != null && NodeQuery.IsLive(reward))
                    results.Add(reward);
            }
        }
        else
        {
            results.AddRange(
                NodeQuery.FindAll<NRewardButton>(screen)
                    .Where(IsLiveChoice));
        }

        results.Sort((left, right) =>
        {
            int cmp = left.GlobalPosition.Y.CompareTo(right.GlobalPosition.Y);
            return cmp != 0 ? cmp : left.GlobalPosition.X.CompareTo(right.GlobalPosition.X);
        });

        return results;
    }

    internal static NProceedButton? GetProceedButton(NRewardsScreen screen)
    {
        if (!NodeQuery.IsLive(screen))
            return null;

        if (ProceedButtonField?.GetValue(screen) is NProceedButton reflected
            && NodeQuery.IsLive(reflected)
            && IsLiveChoice(reflected))
        {
            return reflected;
        }

        return NodeQuery.FindAll<NProceedButton>(screen)
            .FirstOrDefault(IsLiveChoice);
    }

    private static bool TryReadRewardList(NRewardsScreen screen, out List<NRewardButton> rewards)
    {
        rewards = new List<NRewardButton>();
        if (RewardButtonsField?.GetValue(screen) is not IEnumerable enumerable)
            return false;

        foreach (var item in enumerable)
        {
            if (item is NRewardButton reward)
                rewards.Add(reward);
        }

        return rewards.Count > 0;
    }

    private static bool IsLiveChoice(Control control) =>
        NodeQuery.IsVisible(control)
        && control is not MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl { IsEnabled: false };
}
