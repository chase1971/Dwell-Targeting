using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// One-time proceed/skip dwell snapshot on rewards and post-card-pick screens.
/// </summary>
internal static class ProceedTargetBuilder
{
    internal const float DefaultPadding = 24f;
    internal const long SettleMs = 1000;

    internal static void AppendSkipOrProceedTargets(
        NRewardsScreen screen,
        List<CardPickTargetQuery.CachedPickTarget> list,
        float padding = DefaultPadding)
    {
        var targets = TryBuildFromRewardsScreen(screen, padding);
        if (targets == null)
            return;

        list.AddRange(targets);
    }

    internal static List<CardPickTargetQuery.CachedPickTarget>? TryBuildFromRewardsScreen(
        NRewardsScreen? screen,
        float padding = DefaultPadding)
    {
        if (screen == null || !NodeQuery.IsLive(screen))
            return null;

        var proceed = RewardsScreenQuery.GetProceedButton(screen);
        if (proceed != null)
            return BuildFromProceed(proceed, padding);

        var skip = RewardsScreenQuery.GetSkipOrProceedControl(screen);
        if (skip == null)
            return null;

        return BuildFromSkip(skip, padding);
    }

    internal static List<CardPickTargetQuery.CachedPickTarget>? BuildFromProceed(
        NProceedButton proceed,
        float padding = DefaultPadding)
    {
        if (!NodeQuery.IsLive(proceed) || !NodeQuery.IsVisible(proceed))
            return null;

        if (!TryGetProceedDwellRect(proceed, out var rect, padding))
            return null;

        var captured = proceed;
        return
        [
            new CardPickTargetQuery.CachedPickTarget
            {
                Bounds = rect,
                Activate = () => RewardSelectionService.TryProceed(captured),
                Name = "RewardProceed",
                Menu = true
            }
        ];
    }

    /// <summary>
    /// Use the proceed control's own global rect (like EventOverlay). Descendant union skews right when
    /// label/arrow children lay out separately during the slide-in animation.
    /// </summary>
    internal static bool TryGetProceedDwellRect(NProceedButton proceed, out Rect2 rect, float padding = DefaultPadding)
    {
        rect = default;
        if (!NodeQuery.IsLive(proceed) || !NodeQuery.IsVisible(proceed))
            return false;

        if (TryGetLargestClickableRect(proceed, out rect, padding))
            return true;

        rect = proceed.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        if (padding > 0f)
            rect = rect.Grow(padding);

        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static bool TryGetLargestClickableRect(Control root, out Rect2 rect, float padding)
    {
        rect = default;
        float bestArea = 0f;

        foreach (var clickable in NodeQuery.FindAll<NClickableControl>(root))
        {
            if (clickable is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (clickable is NClickableControl { IsEnabled: false })
                continue;

            var candidate = control.GetGlobalRect();
            float area = candidate.Size.X * candidate.Size.Y;
            if (area <= bestArea || candidate.Size.X < 12f || candidate.Size.Y < 12f)
                continue;

            bestArea = area;
            rect = padding > 0f ? candidate.Grow(padding) : candidate;
        }

        return bestArea > 0f;
    }

    private static List<CardPickTargetQuery.CachedPickTarget>? BuildFromSkip(
        Control skip,
        float padding)
    {
        if (skip is NProceedButton proceed)
            return BuildFromProceed(proceed, padding);

        if (!TryGetSkipDwellRect(skip, out var rect, padding))
            return null;

        var captured = skip;
        return
        [
            new CardPickTargetQuery.CachedPickTarget
            {
                Bounds = rect,
                Activate = () => ActivateSkip(captured),
                Name = $"RewardSkip:{skip.Name}",
                Menu = true
            }
        ];
    }

    private static void ActivateSkip(Control skip)
    {
        if (skip is NProceedButton proceed)
        {
            RewardSelectionService.TryProceed(proceed);
            return;
        }

        InputForwardService.TryActivateControl(skip);
    }

    private static bool TryGetSkipDwellRect(Control skip, out Rect2 rect, float padding)
    {
        rect = default;
        if (!NodeQuery.IsLive(skip) || !NodeQuery.IsVisible(skip))
            return false;

        rect = skip.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        if (padding > 0f)
            rect = rect.Grow(padding);

        return true;
    }
}
