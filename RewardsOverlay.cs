using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// Hover-to-claim for loot/reward choices (no number buttons — dwell directly on the item, the way the
/// user wants), plus a direct native dwell on the Skip/Proceed button. Each item's dwell hitbox is
/// clipped to its own vertical band so hovering the 2nd item can never claim the 1st (the items'
/// generous hitboxes used to overlap and the topmost always won).
/// </summary>
internal static class RewardsOverlay
{
    private const float ProceedHitboxPadding = 24f;
    private const string LegacyCanvasName = "DwellRewardsLayer";

    private static readonly Dictionary<ulong, NRewardButton> _rewards = new();
    private static NProceedButton? _proceedButton;
    private static long _nextProceedDiagTick;
    private static bool _lastInsideProceed;

    internal static void Sync()
    {
        var rewardsScreen = OverlayModeService.GetCachedRewardsScreen();
        if (rewardsScreen == null)
        {
            Hide();
            return;
        }

        DestroyLegacySideButtonLayer();

        _rewards.Clear();
        foreach (var reward in RewardsScreenQuery.GetRewardButtons(rewardsScreen))
        {
            if (RewardsScreenQuery.IsSelectableReward(reward))
                _rewards[reward.GetInstanceId()] = reward;
        }

        _proceedButton = RewardsScreenQuery.GetProceedButton(rewardsScreen);

        LogProceedDiagnostic();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        // Build each reward's generous hitbox, then clip vertically against neighbours so adjacent
        // items never overlap (the cause of "hover item 2, claim item 1").
        var items = new List<(Rect2 rect, NRewardButton reward)>();
        foreach (var reward in _rewards.Values)
        {
            if (TryGetItemRect(reward, out var rect))
                items.Add((rect, reward));
        }

        items.Sort((a, b) => a.rect.GetCenter().Y.CompareTo(b.rect.GetCenter().Y));

        for (int i = 0; i < items.Count; i++)
        {
            var rect = items[i].rect;

            if (i > 0)
            {
                float midPrev = (items[i - 1].rect.GetCenter().Y + items[i].rect.GetCenter().Y) / 2f;
                if (rect.Position.Y < midPrev)
                    rect = new Rect2(rect.Position.X, midPrev, rect.Size.X, rect.End.Y - midPrev);
            }

            if (i < items.Count - 1)
            {
                float midNext = (items[i].rect.GetCenter().Y + items[i + 1].rect.GetCenter().Y) / 2f;
                if (rect.End.Y > midNext)
                    rect = new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, midNext - rect.Position.Y);
            }

            if (rect.Size.Y < 8f)
                continue;

            var captured = items[i].reward;
            targets.Add(DwellHoverService.Card(rect, () => Claim(captured), $"RewardItem:{i + 1}"));
        }

        if (TryGetProceedRect(out var prect))
            targets.Add(DwellHoverService.Menu(prect, ActivateProceed, "NativeProceed:Skip"));
    }

    internal static void Hide()
    {
        _rewards.Clear();
        _proceedButton = null;
        DestroyLegacySideButtonLayer();
    }

    /// <summary>Remove numbered side buttons from older builds (v0.10.30 and earlier).</summary>
    private static void DestroyLegacySideButtonLayer()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        try
        {
            foreach (var child in tree.Root.GetChildren())
            {
                if (child is CanvasLayer layer
                    && NodeQuery.IsLive(layer)
                    && layer.Name == LegacyCanvasName)
                {
                    layer.QueueFree();
                    ModLogger.Info("Removed legacy reward side-button layer.");
                }
            }
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        foreach (var reward in _rewards.Values)
        {
            if (TryGetItemRect(reward, out var rect) && rect.HasPoint(globalPos))
            {
                Claim(reward);
                message = "Reward item claimed";
                return true;
            }
        }

        if (TryGetProceedRect(out var prect) && prect.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(ActivateProceed))
                return false;

            message = "Native proceed clicked";
            return true;
        }

        return false;
    }

    internal static bool TryHitDwellButton(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        foreach (var reward in _rewards.Values)
        {
            if (TryGetItemRect(reward, out var rect) && rect.HasPoint(globalPos))
            {
                message = "Hit reward item";
                return true;
            }
        }

        if (TryGetProceedRect(out var prect) && prect.HasPoint(globalPos))
        {
            message = "Hit native proceed";
            return true;
        }

        return false;
    }

    internal static bool ContainsPoint(Vector2 globalPos)
    {
        foreach (var reward in _rewards.Values)
        {
            if (TryGetItemRect(reward, out var rect) && rect.HasPoint(globalPos))
                return true;
        }

        return TryGetProceedRect(out var prect) && prect.HasPoint(globalPos);
    }

    private static void Claim(NRewardButton reward)
    {
        if (NodeQuery.IsLive(reward))
            RewardSelectionService.TryClaim(reward);
    }

    private static bool TryGetItemRect(NRewardButton reward, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(reward) || !NodeQuery.IsVisible(reward))
            return false;

        if (!ControlHitboxService.TryGetDwellRect(reward, out rect))
            return false;

        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static bool TryGetProceedRect(out Rect2 rect)
    {
        rect = default;
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton) || !NodeQuery.IsVisible(_proceedButton))
            return false;

        if (_proceedButton is NClickableControl { IsEnabled: false })
            return false;

        rect = _proceedButton.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        // The button has a hover/pulse animation (~±7 px) and the head-mouse wobbles; pad the dwell
        // hitbox so the cursor stays "inside" instead of flickering across the edge.
        rect = rect.Grow(ProceedHitboxPadding);
        return true;
    }

    private static void ActivateProceed()
    {
        if (_proceedButton == null || !NodeQuery.IsLive(_proceedButton))
        {
            ModLogger.Warn("[ProceedDiag] ActivateProceed fired but proceed button missing/dead.");
            return;
        }

        ModLogger.Info($"[ProceedDiag] ActivateProceed firing on '{_proceedButton.Name}'.");
        RewardSelectionService.TryProceed(_proceedButton);
    }

    /// <summary>
    /// Throttled trace of the Proceed/Skip button state + whether the cursor is inside its dwell rect.
    /// </summary>
    private static void LogProceedDiagnostic()
    {
        long now = System.Environment.TickCount64;

        if (_proceedButton == null)
        {
            if (now < _nextProceedDiagTick)
                return;
            _nextProceedDiagTick = now + 1000;
            ModLogger.Info("[ProceedDiag] proceed button = null (none found on this rewards screen).");
            _lastInsideProceed = false;
            return;
        }

        bool hasRect = TryGetProceedRect(out var rect);
        var mouse = DwellHoverService.GetMousePosition();
        bool inside = hasRect && mouse != null && rect.HasPoint(mouse.Value);

        bool edge = inside != _lastInsideProceed;
        _lastInsideProceed = inside;
        if (!edge && now < _nextProceedDiagTick)
            return;
        _nextProceedDiagTick = now + 1000;

        ModLogger.Info(
            $"[ProceedDiag] name={_proceedButton.Name} hasRect={hasRect} " +
            $"mouse=({(mouse?.X ?? -1f):F0},{(mouse?.Y ?? -1f):F0}) insideProceed={inside} " +
            $"menuCooldown={DwellActivationCooldown.IsMenuBlocked} rewards={_rewards.Count}");
    }
}
