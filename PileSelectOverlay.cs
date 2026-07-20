using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DwellTargeting;

/// <summary>
/// Hover-to-select for cards on pile/grid/choose/card-reward selection screens.
/// </summary>
internal static class PileSelectOverlay
{
    internal static bool IsScrollableGridScreen(Node screen) =>
        screen is NCardGridSelectionScreen or NChooseACardSelectionScreen or NDeckUpgradeSelectScreen;

    internal static void PrepareForEntry()
    {
        _awaitingConfirm = false;
        _awaitingProceed = false;
        _confirmOnly = false;
        _cachedScreen = null;
        _pickScan.ScheduleRescan("Pile");
    }

    private static Node? _cachedScreen;
    private static ulong _cachedScreenId;
    private static List<NCardHolder>? _cachedHolders;
    private static List<Control>? _cachedCardControls;
    private static List<CardPickTargetQuery.CachedPickTarget>? _cachedDwellTargets;
    private static ScreenEntryScanState _pickScan;
    private static bool _awaitingConfirm;
    private static bool _confirmOnly;
    private static bool _awaitingProceed;
    private static long _proceedReadyAtMs;
    private static bool _loggedButtonTypes;

    internal static bool IsAwaitingConfirm() => _awaitingConfirm;

    internal static bool IsInConfirmOnlyPhase() => _awaitingConfirm || _confirmOnly || CardConfirmPhaseQuery.IsActive();

    internal static void RefreshConfirmPhaseLookups()
    {
        if (!IsInConfirmOnlyPhase())
            return;

        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        DeckViewOverlay.RefreshToggleLookup();
        _cachedDwellTargets = null;
        _confirmOnly = true;
        ModLogger.Info("[Pile] confirm phase lookups refreshed (cards stay suppressed).");
    }

    internal static void ForceRescan()
    {
        if (IsInConfirmOnlyPhase())
        {
            RefreshConfirmPhaseLookups();
            return;
        }

        _awaitingConfirm = false;
        _awaitingProceed = false;
        _confirmOnly = false;
        _cachedScreen = null;
        _cachedHolders = null;
        _cachedCardControls = null;
        _cachedDwellTargets = null;
        CardConfirmPhaseQuery.InvalidateCache();
        _pickScan.Force("Pile");
    }

    internal static void NotifyPickCompleted()
    {
        if (OverlayModeService.TryGetPileSelectScreen(out var screen)
            && screen is NDeckUpgradeSelectScreen)
        {
            EnterConfirmOnlyPhase();
            return;
        }

        if (CombatCardChoiceQuery.IsInstantPickFlow())
        {
            Hide();
            OverlayModeService.InvalidateCache();
            ModLogger.Info("[Pile] instant combat pick — returning to combat overlays.");
            return;
        }

        _awaitingConfirm = false;
        _confirmOnly = false;
        _awaitingProceed = true;
        _proceedReadyAtMs = 0;
        _cachedDwellTargets = null;
        _pickScan.Force("Pile");
    }

    /// <summary>Player pressed Back — leave confirm-only and restore card grid overlays.</summary>
    internal static void NotifyBackedOut()
    {
        if (!_awaitingConfirm && !_confirmOnly && !CardConfirmPhaseQuery.IsActive())
            return;

        ExitConfirmPhase("back");
    }

    internal static void Sync()
    {
        if (!OverlayModeService.TryGetPileSelectScreen(out Node screen))
        {
            Hide();
            return;
        }

        ulong screenId = screen.GetInstanceId();
        if (_cachedScreen != screen || _cachedScreenId != screenId)
        {
            ResetForScreen(screen, screenId);
            return;
        }

        if (CardConfirmPhaseQuery.IsActive())
        {
            if (!_confirmOnly)
                EnterConfirmOnlyPhase();
            return;
        }

        if (_awaitingConfirm || _confirmOnly)
            ExitConfirmPhase("confirm cleared");

        if (_awaitingProceed)
        {
            SyncProceedPhase(screen);
            return;
        }

        if (!_pickScan.ShouldScan(screenId))
            return;

        TakeLayoutSnapshot(screen);
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_confirmOnly || _awaitingConfirm || CardConfirmPhaseQuery.IsActive())
        {
            CardConfirmPhaseQuery.CollectDwellTargets(targets);
            return;
        }

        if (_cachedDwellTargets == null)
            return;

        CardPickTargetQuery.AppendCachedPickTargets(_cachedDwellTargets, targets);
    }

    internal static void Hide()
    {
        _awaitingConfirm = false;
        _confirmOnly = false;
        _awaitingProceed = false;
        _cachedScreen = null;
        _cachedScreenId = 0;
        _cachedHolders = null;
        _cachedCardControls = null;
        _cachedDwellTargets = null;
        _proceedReadyAtMs = 0;
        _loggedButtonTypes = false;
        _pickScan.OnHide();
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        return false;
    }

    private static void EnterConfirmOnlyPhase()
    {
        _awaitingConfirm = true;
        _confirmOnly = true;
        _awaitingProceed = false;
        _cachedDwellTargets = null;
        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        ModLogger.Info("[Pile] confirm-only phase — card pick overlays suppressed.");
    }

    private static void ExitConfirmPhase(string reason)
    {
        _awaitingConfirm = false;
        _confirmOnly = false;
        _awaitingProceed = false;
        _cachedDwellTargets = null;
        _proceedReadyAtMs = 0;
        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        _pickScan.Force("Pile");
        ModLogger.Info($"[Pile] confirm-only phase ended ({reason}) — card grid rescan scheduled.");
    }

    private static void ResetForScreen(Node screen, ulong screenId)
    {
        _cachedScreen = screen;
        _cachedScreenId = screenId;
        _cachedHolders = null;
        _cachedCardControls = null;
        _cachedDwellTargets = null;
        _awaitingConfirm = false;
        _confirmOnly = false;
        _awaitingProceed = false;
        _proceedReadyAtMs = 0;
        _loggedButtonTypes = false;
        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        DeckViewOverlay.PrepareForEntry();
        long settleMs = screen is NCardRewardSelectionScreen
            ? ScreenScanTiming.CardDraftSettleMs
            : ScreenScanTiming.LayoutSettleMs;
        _pickScan.ScheduleRescan("Pile", settleMs);
        ModLogger.Info($"[Pile] waiting {settleMs}ms for card fan-out before layout snapshot.");
    }

    private static void SyncProceedPhase(Node screen)
    {
        if (AreOfferedCardsStillVisible())
        {
            _proceedReadyAtMs = 0;
            return;
        }

        if (_proceedReadyAtMs == 0)
        {
            _proceedReadyAtMs = System.Environment.TickCount64 + ProceedTargetBuilder.SettleMs;
            ModLogger.Info($"[Pile] waiting {ProceedTargetBuilder.SettleMs}ms for proceed button before snapshot.");
            return;
        }

        if (System.Environment.TickCount64 < _proceedReadyAtMs)
            return;

        if (!_pickScan.ShouldScan(screen.GetInstanceId()))
            return;

        var rewards = OverlayModeService.GetCachedRewardsScreen();
        _cachedDwellTargets = ProceedTargetBuilder.TryBuildFromRewardsScreen(rewards);
        int count = _cachedDwellTargets?.Count ?? 0;
        _pickScan.MarkScanned(count, "Pile");

        if (count > 0)
        {
            _awaitingProceed = false;
            ModLogger.Info("[Pile] proceed snapshot after card pick.");
            return;
        }

        if (!AreOfferedCardsStillVisible())
        {
            _awaitingProceed = false;
            Hide();
            OverlayModeService.InvalidateCache();
            ModLogger.Info("[Pile] pick complete — no proceed button, resuming prior mode.");
        }
    }

    private static bool AreOfferedCardsStillVisible()
    {
        if (_cachedHolders is { Count: > 0 })
        {
            foreach (var holder in _cachedHolders)
            {
                if (!NodeQuery.IsLive(holder) || !NodeQuery.IsVisible(holder))
                    continue;

                if (holder.CardModel != null)
                    return true;
            }
        }

        if (_cachedCardControls is { Count: > 0 })
        {
            foreach (var card in _cachedCardControls)
            {
                if (NodeQuery.IsLive(card) && NodeQuery.IsVisible(card))
                    return true;
            }
        }

        return false;
    }

    private static void TakeLayoutSnapshot(Node screen)
    {
        ScanPickTargets(screen, out _cachedHolders, out _cachedCardControls, out var skipControls);
        _cachedDwellTargets = CardPickTargetQuery.BuildCachedPickTargets(
            _cachedHolders is { Count: > 0 } ? _cachedHolders : null,
            _cachedCardControls is { Count: > 0 } ? _cachedCardControls : null,
            skipControls);

        int count = _cachedDwellTargets.Count;
        _pickScan.MarkScanned(count, "Pile");

        if (count == 0)
            return;

        ModLogger.Info(
            $"[Pile] layout snapshot — dwell targets={count} " +
            $"holders={_cachedHolders?.Count ?? 0} cardControls={_cachedCardControls?.Count ?? 0} " +
            $"skip={skipControls?.Count ?? 0}");

        if ((skipControls?.Count ?? 0) == 0 && !_loggedButtonTypes)
        {
            _loggedButtonTypes = true;
            LogButtonLikeTypes(screen);
        }
    }

    private static void ScanPickTargets(
        Node screen,
        out List<NCardHolder> holders,
        out List<Control>? cardControls,
        out List<Control>? skipControls)
    {
        holders = CardPickTargetQuery.FindHolders(screen);
        if (holders.Count == 0 && MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress)
            holders = CombatCardChoiceQuery.FindOfferHolders();
        if (holders.Count == 0)
        {
            var rewards = OverlayModeService.GetCachedRewardsScreen();
            if (rewards != null)
                holders = CardPickTargetQuery.FindHolders(rewards);
        }

        cardControls = null;
        if (holders.Count == 0)
        {
            cardControls = CardPickTargetQuery.FindCardControls(screen);
            if (cardControls.Count == 0)
            {
                var rewards = OverlayModeService.GetCachedRewardsScreen();
                if (rewards != null)
                    cardControls = CardPickTargetQuery.FindCardControls(rewards);
            }
        }

        skipControls = CardPickTargetQuery.FindSkipControls(screen);
        if (skipControls.Count == 0)
        {
            var rewards = OverlayModeService.GetCachedRewardsScreen();
            if (rewards != null)
                skipControls = CardPickTargetQuery.FindSkipControls(rewards);
        }
    }

    private static void LogButtonLikeTypes(Node screen)
    {
        var seen = new HashSet<string>();
        CollectButtonLikeTypes(screen, seen);
        ModLogger.Info($"[Pile] button-like types={string.Join(", ", seen)}");
    }

    private static void CollectButtonLikeTypes(Node node, HashSet<string> seen)
    {
        if (!NodeQuery.IsLive(node))
            return;

        string name = node.GetType().Name;
        if (name.Contains("Button", StringComparison.Ordinal)
            || name.Contains("Skip", StringComparison.Ordinal)
            || name.Contains("Proceed", StringComparison.Ordinal)
            || name.Contains("Confirm", StringComparison.Ordinal)
            || name.Contains("Card", StringComparison.Ordinal))
        {
            seen.Add(name);
        }

        try
        {
            foreach (var child in node.GetChildren())
                CollectButtonLikeTypes(child, seen);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }
}
