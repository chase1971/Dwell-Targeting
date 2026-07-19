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
        _cachedScreen = null;
    }

    private static Node? _cachedScreen;
    private static List<NCardHolder>? _cachedHolders;
    private static List<Control>? _cachedCardControls;
    private static List<Control>? _cachedSkipControls;
    private static List<CardPickTargetQuery.CachedPickTarget>? _cachedDwellTargets;
    private static ulong _cachedScreenId;
    private static long _layoutReadyAtMs;
    private static long _proceedReadyAtMs;
    private static bool _proceedSettleScheduled;
    private static bool _pickSnapshotTaken;
    private static bool _proceedSnapshotTaken;
    private static bool _confirmSnapshotTaken;
    private static bool _loggedButtonTypes;
    private static int _emptyLayoutRetries;
    private static bool _awaitingConfirm;

    internal static bool IsAwaitingConfirm() => _awaitingConfirm;

    internal static bool IsInConfirmOnlyPhase() => _awaitingConfirm || _confirmSnapshotTaken;

    internal static void RefreshConfirmPhaseLookups()
    {
        if (!IsInConfirmOnlyPhase())
            return;

        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        DeckViewOverlay.RefreshToggleLookup();
        _cachedDwellTargets = null;
        _confirmSnapshotTaken = true;
        _pickSnapshotTaken = true;
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
        _cachedScreen = null;
        CardConfirmPhaseQuery.InvalidateCache();
    }

    /// <summary>Card pick finished — drop card overlays and snapshot proceed once it appears.</summary>
    internal static void NotifyPickCompleted()
    {
        if (OverlayModeService.TryGetPileSelectScreen(out var screen)
            && screen is NDeckUpgradeSelectScreen)
        {
            EnterAwaitingConfirmPhase();
            return;
        }

        _awaitingConfirm = false;
        _pickSnapshotTaken = false;
        _proceedSnapshotTaken = false;
        _confirmSnapshotTaken = false;
        _proceedReadyAtMs = 0;
        _proceedSettleScheduled = false;
        _cachedDwellTargets = null;
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

        if (CardConfirmPhaseQuery.IsActive() || _awaitingConfirm)
        {
            if (!_confirmSnapshotTaken)
                TakeConfirmSnapshot();
            return;
        }

        if (_confirmSnapshotTaken)
        {
            _confirmSnapshotTaken = false;
            _pickSnapshotTaken = false;
            _proceedSnapshotTaken = false;
            _cachedDwellTargets = null;
        }

        if (_pickSnapshotTaken && _proceedSnapshotTaken)
            return;

        if (_pickSnapshotTaken && (_cachedDwellTargets == null || _cachedDwellTargets.Count == 0))
        {
            if (System.Environment.TickCount64 >= _layoutReadyAtMs)
                RetryLayoutSnapshot(screen);
            return;
        }

        if (_pickSnapshotTaken)
        {
            if (AreOfferedCardsStillVisible())
            {
                _proceedSettleScheduled = false;
                _proceedReadyAtMs = 0;
                return;
            }

            if (!_proceedSettleScheduled)
            {
                _proceedSettleScheduled = true;
                _proceedReadyAtMs = System.Environment.TickCount64 + ProceedTargetBuilder.SettleMs;
                ModLogger.Info(
                    $"[Pile] waiting {ProceedTargetBuilder.SettleMs}ms for proceed button before snapshot.");
                return;
            }

            if (System.Environment.TickCount64 < _proceedReadyAtMs)
                return;

            TryTakeProceedSnapshot();
            return;
        }

        if (System.Environment.TickCount64 < _layoutReadyAtMs)
            return;

        TakeLayoutSnapshot(screen);
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_confirmSnapshotTaken)
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
        _cachedScreen = null;
        _cachedHolders = null;
        _cachedCardControls = null;
        _cachedSkipControls = null;
        _cachedDwellTargets = null;
        _cachedScreenId = 0;
        _layoutReadyAtMs = 0;
        _proceedReadyAtMs = 0;
        _proceedSettleScheduled = false;
        _pickSnapshotTaken = false;
        _proceedSnapshotTaken = false;
        _confirmSnapshotTaken = false;
        _loggedButtonTypes = false;
    }

    private static void TakeConfirmSnapshot()
    {
        _cachedDwellTargets = null;
        _confirmSnapshotTaken = true;
        _pickSnapshotTaken = true;
        ModLogger.Info("[Pile] confirm-only phase — card pick overlays suppressed.");
    }

    private static void EnterAwaitingConfirmPhase()
    {
        _awaitingConfirm = true;
        _cachedDwellTargets = null;
        _pickSnapshotTaken = true;
        _confirmSnapshotTaken = false;
        _proceedSettleScheduled = false;
        _proceedReadyAtMs = 0;
        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        ModLogger.Info("[Pile] upgrade card picked — waiting for confirm/back overlays.");
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        return false;
    }

    private static void ResetForScreen(Node screen, ulong screenId)
    {
        _cachedScreen = screen;
        _cachedScreenId = screenId;
        _cachedHolders = null;
        _cachedCardControls = null;
        _cachedSkipControls = null;
        _cachedDwellTargets = null;
        _pickSnapshotTaken = false;
        _proceedSnapshotTaken = false;
        _confirmSnapshotTaken = false;
        _proceedReadyAtMs = 0;
        _proceedSettleScheduled = false;
        _loggedButtonTypes = false;
        _emptyLayoutRetries = 0;
        _layoutReadyAtMs = System.Environment.TickCount64 + ScreenScanTiming.LayoutSettleMs;
        CardConfirmPhaseQuery.InvalidateCache();
        BackButtonOverlay.InvalidateLookup();
        DeckViewOverlay.PrepareForEntry();
        ModLogger.Info($"[Pile] waiting {ScreenScanTiming.LayoutSettleMs}ms for card fan-out before layout snapshot.");
    }

    private static void RetryLayoutSnapshot(Node screen)
    {
        _ = screen;
        _pickSnapshotTaken = false;
        _cachedDwellTargets = null;
        if (_emptyLayoutRetries >= ScreenScanTiming.MaxEmptyRetries)
            return;

        _emptyLayoutRetries++;
        _layoutReadyAtMs = System.Environment.TickCount64 + ScreenScanTiming.EmptyRetryMs;
        ModLogger.Info($"[Pile] retrying layout snapshot ({_emptyLayoutRetries}/{ScreenScanTiming.MaxEmptyRetries}).");
    }

    /// <summary>True while the game is still offering pickable cards (not skip/proceed alone).</summary>
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

    private static void TryTakeProceedSnapshot()
    {
        var rewards = OverlayModeService.GetCachedRewardsScreen();
        _cachedDwellTargets = ProceedTargetBuilder.TryBuildFromRewardsScreen(rewards);
        if (_cachedDwellTargets is { Count: > 0 })
        {
            _proceedSnapshotTaken = true;
            ModLogger.Info("[Pile] proceed snapshot after card pick.");
            return;
        }

        _cachedDwellTargets = null;
    }

    private static void TakeLayoutSnapshot(Node screen)
    {
        ScanPickTargets(screen, out _cachedHolders, out _cachedCardControls, out _cachedSkipControls);
        _cachedDwellTargets = CardPickTargetQuery.BuildCachedPickTargets(
            _cachedHolders is { Count: > 0 } ? _cachedHolders : null,
            _cachedCardControls is { Count: > 0 } ? _cachedCardControls : null,
            _cachedSkipControls);

        if (_cachedDwellTargets.Count == 0)
        {
            RetryLayoutSnapshot(screen);
            return;
        }

        _pickSnapshotTaken = true;
        _proceedSnapshotTaken = false;
        _emptyLayoutRetries = 0;

        ModLogger.Info(
            $"[Pile] layout snapshot — dwell targets={_cachedDwellTargets.Count} " +
            $"holders={_cachedHolders?.Count ?? 0} cardControls={_cachedCardControls?.Count ?? 0} " +
            $"skip={_cachedSkipControls?.Count ?? 0}");

        if ((_cachedSkipControls?.Count ?? 0) == 0 && !_loggedButtonTypes)
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
        if (holders.Count == 0)
        {
            var rewards = OverlayModeService.GetCachedRewardsScreen();
            if (rewards != null)
                holders = CardPickTargetQuery.FindHolders(rewards);
        }

        if (holders.Count == 0)
        {
            var root = (Engine.GetMainLoop() as SceneTree)?.Root;
            if (root != null)
                holders = CardPickTargetQuery.FindHolders(root);
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

            if (cardControls.Count == 0)
            {
                var root = (Engine.GetMainLoop() as SceneTree)?.Root;
                if (root != null)
                    cardControls = CardPickTargetQuery.FindCardControls(root);
            }
        }

        skipControls = CardPickTargetQuery.FindSkipControls(screen);
        if (skipControls.Count == 0)
        {
            var rewards = OverlayModeService.GetCachedRewardsScreen();
            if (rewards != null)
                skipControls = CardPickTargetQuery.FindSkipControls(rewards);
        }

        if (skipControls.Count == 0)
        {
            var root = (Engine.GetMainLoop() as SceneTree)?.Root;
            if (root != null)
                skipControls = CardPickTargetQuery.FindSkipControls(root);
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
