using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Per-card target buttons parented to card holders when possible.
/// </summary>
internal static class HandTargetingOverlay
{
    private static bool _hooked;
    private static bool _isTornDown = true;
    private static OverlayMode _activeOverlayMode = OverlayMode.None;

    /// <summary>How a screen mode treats the left / hover scroll strips when it becomes active.</summary>
    private enum ScrollStripHide
    {
        None,            // leave the scroll strips alone (Map, Event)
        Both,            // hide both the left and hover scroll strips
        ShopConditional, // hide the left strip; hide the hover strip only when no deck view is open
    }

    /// <summary>
    /// A full-screen mode overlay (rewards, pile-select, map, event, shop, room) and the
    /// auxiliary behavior its sync needs. Replaces the six near-identical Sync*Mode methods
    /// and the three hand-written mode switches (collect / process-frame / sync).
    /// </summary>
    private sealed record ScreenOverlay(
        OverlayMode Mode,
        Action Sync,
        Action Hide,
        Action<List<DwellHoverService.Target>> Collect,
        ScrollStripHide ScrollStrips = ScrollStripHide.None,
        bool ResyncUtilityBar = false);

    /// <summary>
    /// The registry. Adding a new screen mode is now a single entry here plus an overlay class
    /// exposing Sync()/Hide()/CollectDwellTargets() — no edits to the per-frame dispatch.
    /// </summary>
    private static readonly Dictionary<OverlayMode, ScreenOverlay> ScreenOverlays = new()
    {
        [OverlayMode.Rewards] = new(
            OverlayMode.Rewards, RewardsOverlay.Sync, RewardsOverlay.Hide, RewardsOverlay.CollectDwellTargets,
            ScrollStripHide.Both),
        [OverlayMode.PileSelect] = new(
            OverlayMode.PileSelect, PileSelectOverlay.Sync, PileSelectOverlay.Hide, PileSelectOverlay.CollectDwellTargets,
            ScrollStripHide.Both, ResyncUtilityBar: true),
        [OverlayMode.Map] = new(
            OverlayMode.Map, MapOverlay.Sync, MapOverlay.Hide, MapOverlay.CollectDwellTargets),
        [OverlayMode.Event] = new(
            OverlayMode.Event, EventOverlay.Sync, EventOverlay.Hide, EventOverlay.CollectDwellTargets),
        [OverlayMode.Shop] = new(
            OverlayMode.Shop, ShopOverlay.Sync, ShopOverlay.Hide, ShopOverlay.CollectDwellTargets,
            ScrollStripHide.ShopConditional),
        [OverlayMode.Room] = new(
            OverlayMode.Room, RoomOverlay.Sync, RoomOverlay.Hide, RoomOverlay.CollectDwellTargets,
            ScrollStripHide.Both, ResyncUtilityBar: true),
    };

    internal static void EnsureInitialized()
    {
        if (_hooked)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
            return;

        tree.Connect(SceneTree.SignalName.ProcessFrame, Callable.From(OnProcessFrame));
        _hooked = true;
        ModLogger.Info("Overlay ProcessFrame hook connected.");
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (GameOverlayVisibility.ShouldHideOverlays())
            return;

        if (SettingsOverlay.IsOpen)
        {
            SettingsOverlay.CollectDwellTargets(targets);
            return;
        }

        SettingsOverlay.TryAddGearTarget(targets);

        UtilityBarOverlay.CollectDwellTargets(targets);

        BackButtonOverlay.CollectDwellTargets(targets);

        DeckViewOverlay.CollectDwellTargets(targets);

        var confirm = ConfirmOverlay.GetDwellTarget();
        if (confirm != null)
            targets.Add(confirm.Value);

        var endTurn = EndTurnOverlay.GetDwellTarget();
        if (endTurn != null)
            targets.Add(endTurn.Value);

        var mode = OverlayModeService.GetMode();
        if (ScreenOverlays.TryGetValue(mode, out var screen))
        {
            screen.Collect(targets);
        }
        else
        {
            PotionPopupOverlay.CollectDwellTargets(targets);
            CombatRowsCoordinator.CollectDwellTargets(targets);
        }
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (SettingsOverlay.TryRouteClick(globalPos, out message))
            return true;

        if (!OverlayModeService.IsHandOverlayActive())
        {
            if (UtilityBarOverlay.TryActivateAt(globalPos, out message))
                return true;

            return false;
        }

        if (UtilityBarOverlay.TryActivateAt(globalPos, out message))
            return true;

        if (ConfirmOverlay.TryActivateAt(globalPos, out message))
            return true;

        if (EndTurnOverlay.TryActivateAt(globalPos, out message))
            return true;

        if (PotionPopupOverlay.TryRouteClick(globalPos, out message))
            return true;

        if (CombatRowsCoordinator.TryActivateAt(globalPos, out message))
            return true;

        return false;
    }

    internal static bool TryConsumeHandClick(Vector2 globalPos)
    {
        var mode = OverlayModeService.GetMode();
        if (!HandBlockPolicy.ShouldConsumeHandClicks(mode))
            return false;

        if (!OverlayModeService.IsHandOverlayActive())
            return false;

        if (ConfirmOverlay.ContainsPoint(globalPos))
            return false;

        if (EndTurnOverlay.ContainsPoint(globalPos))
            return false;

        if (UtilityBarOverlay.ContainsPoint(globalPos))
            return false;

        if (TryHitDwellButton(globalPos, out _))
            return false;

        return CombatRowsCoordinator.HandBlockContains(globalPos);
    }

    internal static bool TryHitDwellButton(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (ConfirmOverlay.TryHitAt(globalPos))
        {
            message = "Hit Confirm";
            return true;
        }

        if (EndTurnOverlay.TryHitAt(globalPos))
        {
            message = "Hit EndTurn";
            return true;
        }

        if (UtilityBarOverlay.ContainsPoint(globalPos))
        {
            message = "Hit Utility";
            return true;
        }

        if (PotionPopupOverlay.TryHitDwellButton(globalPos, out message))
            return true;

        if (CombatRowsCoordinator.TryHitAt(globalPos, out message))
            return true;

        return false;
    }

    private static void OnProcessFrame()
    {
        long frameStart = OverlayPerfDiagnostics.BeginTick();
        try
        {
            SettingsOverlay.UpdateFrame();
            ModManagerSettingsBridge.TryHydrateFromPersistedValues();
            SettingsStore.MaybeReload();

            if (GameOverlayVisibility.ShouldHideOverlays())
            {
                SuppressForMenu();
                FinalizeDwellTargets();
                return;
            }

            long getModeStart = OverlayPerfDiagnostics.BeginTick();
            var mode = OverlayModeService.GetMode();
            OverlayPerfDiagnostics.AddCategory("getMode", getModeStart);

            bool showUtility = RunManager.Instance.IsInProgress;
            UtilityBarOverlay.Sync(showUtility);
            BackButtonOverlay.Sync();

            if (mode == OverlayMode.None)
            {
                if (!_isTornDown)
                    TearDown();
                UtilityBarOverlay.Sync(showUtility);
                if (showUtility)
                    OverlayCanvasHost.EnsureInputRouter();
                FinalizeDwellTargets();
                return;
            }

            if (_activeOverlayMode != mode)
            {
                ModLogger.Info($"[Mode] {_activeOverlayMode} -> {mode} ({OverlayModeService.DebugSnapshot()})");

                // A selection screen just appeared — block accidental instant picks until the
                // cursor settles and moves again.
                if (ScreenOverlays.ContainsKey(mode))
                    DwellHoverService.ArmGrace(1.0f);

                _isTornDown = false;
                _activeOverlayMode = mode;
            }

            OverlayCanvasHost.EnsureInputRouter();

            // A deck / draw / exhaust / card-pile view (or the map) opened on top of combat: combat is
            // still in progress underneath, so suppress the combat overlay to stop its play buttons
            // bleeding through the viewed pile. The Back button stays available to close the view.
            if (mode is OverlayMode.CombatPlay or OverlayMode.HandSelect
                && CombatViewSuppressionQuery.IsViewingScreenOpen())
            {
                SuppressCombatForView();
                FinalizeDwellTargets();
                return;
            }

            if (ScreenOverlays.TryGetValue(mode, out var screenOverlay))
            {
                SyncScreenMode(screenOverlay);
                FinalizeDwellTargets();
                return;
            }

            // Combat / hand-select path: make sure every screen overlay is hidden first.
            foreach (var screen in ScreenOverlays.Values)
                screen.Hide();

            var hand = NPlayerHand.Instance;
            if (hand == null)
            {
                if (!_isTornDown)
                    TearDown();
                return;
            }

            _isTornDown = false;

            long handSyncStart = OverlayPerfDiagnostics.BeginTick();
            if (HandBlockPolicy.ShouldBlockHandInput(mode))
                HandInputBlocker.Sync(hand, shouldBlock: true);
            else
                HandInputBlocker.Release();

            if (HandBlockPolicy.ShouldBlockMouseCardPlay(mode))
                MouseCardPlayBlocker.Sync(shouldBlock: true);
            else
                MouseCardPlayBlocker.Release();

            OverlayCanvasHost.EnsureFallbackCanvas();

            if (mode == OverlayMode.HandSelect)
            {
                EndTurnOverlay.Hide();
                EnemyLabelOverlay.Hide();
                ConfirmOverlay.Sync(visible: true);
                CombatRowsCoordinator.SyncHandSelect(hand, OverlayCanvasHost.FallbackRoot);
            }
            else
            {
                ConfirmOverlay.Hide();
                EndTurnOverlay.Sync(visible: true);
                CombatRowsCoordinator.SyncCombatPlay(hand, OverlayCanvasHost.FallbackRoot);
            }

            long potionStart = OverlayPerfDiagnostics.BeginTick();
            PotionPopupOverlay.Sync();
            OverlayPerfDiagnostics.Add("potion.sync", potionStart);

            OverlayPerfDiagnostics.AddCategory("handSync", handSyncStart);

            FinalizeDwellTargets();
        }
        finally
        {
            OverlayPerfDiagnostics.EndFrame(frameStart);
        }
    }

    private static void FinalizeDwellTargets()
    {
        long dwellStart = OverlayPerfDiagnostics.BeginTick();
        try
        {
            // Run after mode-specific sync so map/shop handlers cannot hide deck scroll mid-frame.
            DeckViewOverlay.Sync();

            var dwellTargets = new List<DwellHoverService.Target>();

            long collectStart = OverlayPerfDiagnostics.BeginTick();
            CollectDwellTargets(dwellTargets);
            OverlayPerfDiagnostics.Add("dwell.collect", collectStart);

            DwellDebugOverlay.Render(dwellTargets);

            long processStart = OverlayPerfDiagnostics.BeginTick();
            DwellHoverService.ProcessFrame(dwellTargets, GetProcessDelta());
            OverlayPerfDiagnostics.Add("dwell.process", processStart);
        }
        finally
        {
            OverlayPerfDiagnostics.AddCategory("dwell", dwellStart);
        }
    }

    /// <summary>
    /// Sync a single full-screen mode overlay and hide everything else. Replaces the six
    /// Sync*Mode methods.
    ///
    /// NOTE: do NOT call DwellHoverService.Reset() here — this runs every frame, and resetting
    /// every frame zeroes the dwell timer before it can reach the activation threshold (that bug
    /// made native dwell unusable on the rewards / pile-select / map / event / shop / room screens).
    ///
    /// The old per-mode methods each hand-listed which overlays to hide, and the lists disagreed —
    /// e.g. SyncRewardsMode never hid PileSelect. Hiding *every* other screen overlay uniformly
    /// closes that class of stale-button bleed-through (backlog B2).
    /// </summary>
    private static void SyncScreenMode(ScreenOverlay active)
    {
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        CombatRowsCoordinator.Clear();
        EnemyLabelOverlay.Hide();

        foreach (var screen in ScreenOverlays.Values)
        {
            if (screen.Mode != active.Mode)
                screen.Hide();
        }

        switch (active.ScrollStrips)
        {
            case ScrollStripHide.Both:
                LeftHoverScrollOverlay.Hide();
                HoverScrollStripOverlay.Hide();
                break;
            case ScrollStripHide.ShopConditional:
                LeftHoverScrollOverlay.Hide();
                if (!CombatViewSuppressionQuery.IsDeckViewOpen())
                    HoverScrollStripOverlay.Hide();
                break;
        }

        active.Sync();

        if (active.ResyncUtilityBar)
            UtilityBarOverlay.Sync(RunManager.Instance.IsInProgress);
    }

    private static void SuppressCombatForView()
    {
        // Hide only the combat-specific overlay (rows / End Turn / Confirm / enemy labels / potion
        // popup) and release the input blockers. Utility bar + Back button are still collected in
        // CollectDwellTargets so the pile/map view can be closed hands-free.
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        EnemyLabelOverlay.Hide();
        PotionPopupOverlay.Hide();
        CombatRowsCoordinator.Clear();
    }

    private static double GetProcessDelta()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        return tree?.Root?.GetProcessDeltaTime() ?? (1.0 / 60.0);
    }

    private static void SuppressForMenu()
    {
        DwellHoverService.Reset();
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        UtilityBarOverlay.Hide();
        PotionPopupOverlay.Hide();
        EnemyLabelOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        HoverScrollStripOverlay.Hide();
        DeckViewOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        BackButtonOverlay.Hide();
        CombatRowsCoordinator.Clear();
        _isTornDown = true;
        _activeOverlayMode = OverlayMode.None;
    }

    private static void TearDown()
    {
        DwellHoverService.Reset();
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        CardAnchorService.ClearCache();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        RewardsOverlay.Hide();
        PileSelectOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        HoverScrollStripOverlay.Hide();
        DeckViewOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        BackButtonOverlay.Hide();
        PotionPopupOverlay.Hide();
        EnemyLabelOverlay.Hide();
        CombatRowsCoordinator.Clear();
        _isTornDown = true;
        _activeOverlayMode = OverlayMode.None;
    }
}
