using Godot;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// Hover-to-claim for loot/reward choices (no number buttons — dwell directly on the item, the way the
/// user wants), plus a direct native dwell on the Skip/Proceed button. Each item's dwell hitbox is
/// clipped to its own vertical band so hovering the 2nd item can never claim the 1st (the items'
/// generous hitboxes used to overlap and the topmost always won).
/// Card-pick drafts are handled by <see cref="PileSelectOverlay"/> when mode is PileSelect.
/// </summary>
internal static class RewardsOverlay
{
    private const string LegacyCanvasName = "DwellRewardsLayer";

    private const int PhaseLoot = 1;
    private const int PhaseProceed = 2;
    private const float LootItemPadding = 10f;

    private static readonly Dictionary<ulong, NRewardButton> _rewards = new();
    private static List<CardPickTargetQuery.CachedPickTarget>? _cachedDwellTargets;
    private static ulong _cachedScreenId;
    private static int _cachedPhase;
    private static long _proceedReadyAtMs;
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

        if (OverlayModeService.TryGetPileSelectScreen(out _))
        {
            ClearTargets();
            return;
        }

        ulong screenId = rewardsScreen.GetInstanceId();
        if (_cachedScreenId == screenId && _cachedDwellTargets is { Count: > 0 })
            return;

        _rewards.Clear();
        foreach (var reward in RewardsScreenQuery.GetRewardButtons(rewardsScreen))
        {
            if (RewardsScreenQuery.IsSelectableReward(reward))
                _rewards[reward.GetInstanceId()] = reward;
        }

        int phase = _rewards.Count > 0 ? PhaseLoot : PhaseProceed;

        if (phase == PhaseProceed)
        {
            if (ProceedTargetBuilder.TryBuildFromRewardsScreen(rewardsScreen) == null)
            {
                ClearTargets();
                return;
            }

            if (_proceedReadyAtMs == 0)
            {
                _proceedReadyAtMs = System.Environment.TickCount64 + ProceedTargetBuilder.SettleMs;
                ModLogger.Info(
                    $"[Rewards] waiting {ProceedTargetBuilder.SettleMs}ms for proceed button before snapshot.");
                return;
            }

            if (System.Environment.TickCount64 < _proceedReadyAtMs)
                return;
        }
        else
        {
            _proceedReadyAtMs = 0;
        }

        DestroyLegacySideButtonLayer();

        _cachedScreenId = screenId;
        _cachedPhase = phase;
        _cachedDwellTargets = phase == PhaseLoot
            ? BuildLootCachedTargets(rewardsScreen)
            : ProceedTargetBuilder.TryBuildFromRewardsScreen(rewardsScreen);

        ModLogger.Info(
            $"[Rewards] phase={phase} snapshot — dwell targets={_cachedDwellTargets?.Count ?? 0}");

        if (SettingsStore.Current.EnablePerfLogging)
            LogProceedDiagnostic();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_cachedDwellTargets == null)
            return;

        CardPickTargetQuery.AppendCachedPickTargets(_cachedDwellTargets, targets);
    }

    private static List<CardPickTargetQuery.CachedPickTarget> BuildLootCachedTargets(NRewardsScreen screen)
    {
        var items = new List<(Rect2 rect, NRewardButton reward)>();
        foreach (var reward in _rewards.Values)
        {
            if (TryMeasureItemRect(reward, out var rect))
                items.Add((rect, reward));
        }

        items.Sort((a, b) => a.rect.GetCenter().Y.CompareTo(b.rect.GetCenter().Y));

        var list = new List<CardPickTargetQuery.CachedPickTarget>();
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
            int slot = i + 1;
            list.Add(new CardPickTargetQuery.CachedPickTarget
            {
                Bounds = rect,
                Activate = () => Claim(captured),
                Name = $"RewardItem:{slot}",
                Menu = false
            });
        }

        ProceedTargetBuilder.AppendSkipOrProceedTargets(screen, list);
        return list;
    }

    internal static void Hide()
    {
        _rewards.Clear();
        ClearTargets();
    }

    private static void ClearTargets()
    {
        _cachedDwellTargets = null;
        _cachedScreenId = 0;
        _cachedPhase = 0;
        _proceedReadyAtMs = 0;
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
        if (_cachedDwellTargets == null)
            return false;

        foreach (var target in _cachedDwellTargets)
        {
            if (!target.Bounds.HasPoint(globalPos))
                continue;

            if (target.Menu && !DwellActivationCooldown.TryRunMenuAction(target.Activate))
                return false;

            if (!target.Menu)
                target.Activate();

            message = target.Menu ? "Native proceed clicked" : "Reward item claimed";
            return true;
        }

        return false;
    }

    internal static bool TryHitDwellButton(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (_cachedDwellTargets == null)
            return false;

        foreach (var target in _cachedDwellTargets)
        {
            if (!target.Bounds.HasPoint(globalPos))
                continue;

            message = target.Menu ? "Hit native proceed" : "Hit reward item";
            return true;
        }

        return false;
    }

    internal static bool ContainsPoint(Vector2 globalPos)
    {
        if (_cachedDwellTargets == null)
            return false;

        foreach (var target in _cachedDwellTargets)
        {
            if (target.Bounds.HasPoint(globalPos))
                return true;
        }

        return false;
    }

    private static void Claim(NRewardButton reward)
    {
        if (NodeQuery.IsLive(reward))
            RewardSelectionService.TryClaim(reward);

        ClearTargets();
    }

    private static bool TryMeasureItemRect(NRewardButton reward, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(reward) || !NodeQuery.IsVisible(reward))
            return false;

        // Descendant union on NRewardButton overshoots the row (icon/label children sit above the
        // button root), which skews loot overlays upward and breaks vertical band clipping.
        rect = reward.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(LootItemPadding);
        return true;
    }

    private static bool TryGetProceedRect(out Rect2 rect)
    {
        rect = default;
        if (_cachedDwellTargets == null)
            return false;

        foreach (var target in _cachedDwellTargets)
        {
            if (!target.Menu)
                continue;

            rect = target.Bounds;
            return true;
        }

        return false;
    }

    private static void LogProceedDiagnostic()
    {
        long now = System.Environment.TickCount64;

        if (!TryGetProceedRect(out var rect))
        {
            if (now < _nextProceedDiagTick)
                return;
            _nextProceedDiagTick = now + 1000;
            ModLogger.Info("[ProceedDiag] skip button = null (none found on this rewards screen).");
            _lastInsideProceed = false;
            return;
        }

        var mouse = DwellHoverService.GetMousePosition();
        bool inside = mouse != null && rect.HasPoint(mouse.Value);

        bool edge = inside != _lastInsideProceed;
        _lastInsideProceed = inside;
        if (!edge && now < _nextProceedDiagTick)
            return;
        _nextProceedDiagTick = now + 1000;

        ModLogger.Info(
            $"[ProceedDiag] hasRect=true mouse=({(mouse?.X ?? -1f):F0},{(mouse?.Y ?? -1f):F0}) " +
            $"insideProceed={inside} menuCooldown={DwellActivationCooldown.IsMenuBlocked} " +
            $"rewards={_rewards.Count} dwellTargets={_cachedDwellTargets?.Count ?? 0}");
    }
}
