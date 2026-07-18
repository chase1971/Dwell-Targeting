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
    private static ulong _menuStateFrame;
    private static bool _pauseMenuOpenThisFrame;
    private static bool _blockingMenuOpenThisFrame;
    private static bool _wasPauseMenuOpenForDwell;

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
        RefreshMenuStateThisFrame();

        if (MainMenuOverlay.IsActive())
        {
            MainMenuOverlay.CollectDwellTargets(targets);
            if (MainMenuOverlay.IsDialogOpen())
                return;

            if (!RunManager.Instance.IsInProgress)
                return;
        }

        OverlayVisToggle.CollectDwellTargets(targets);

        if (GameOverlayVisibility.ShouldHideOverlays(_blockingMenuOpenThisFrame))
            return;

        if (SettingsOverlay.IsOpen)
        {
            SettingsOverlay.CollectDwellTargets(targets);
            return;
        }

        SettingsOverlay.TryAddGearTarget(targets);

        MainMenuOverlay.CollectDwellTargets(targets);
        CharacterSelectOverlay.CollectDwellTargets(targets);

        UtilityBarOverlay.CollectDwellTargets(targets);

        PotionSlotOverlay.CollectDwellTargets(targets);
        PotionPopupOverlay.CollectDwellTargets(targets);

        BackButtonOverlay.CollectDwellTargets(targets);

        if (!CardConfirmPhaseQuery.IsActive())
            DeckViewOverlay.CollectDwellTargets(targets);

        var confirm = ConfirmOverlay.GetDwellTarget();
        if (confirm != null)
            targets.Add(confirm.Value);

        var endTurn = EndTurnOverlay.GetDwellTarget();
        if (endTurn != null)
            targets.Add(endTurn.Value);

        EndTurnOverlay.CollectNativeDwellTargets(targets);
        ConfirmOverlay.CollectNativeDwellTargets(targets);

        var mode = OverlayModeService.GetMode();
        if (ScreenOverlays.TryGetValue(mode, out var screen))
        {
            screen.Collect(targets);
        }
        else
        {
            if (mode == OverlayMode.HandSelect)
                HandSelectOverlay.CollectDwellTargets(targets);
            else
                CombatRowsCoordinator.CollectDwellTargets(targets);
        }
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (SettingsOverlay.TryRouteClick(globalPos, out message))
            return true;

        if (OverlayVisToggle.TryRouteClick(globalPos, out message))
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

    private static void RefreshMenuStateThisFrame()
    {
        ulong frame = Engine.GetProcessFrames();
        if (_menuStateFrame == frame)
            return;

        _menuStateFrame = frame;
        _pauseMenuOpenThisFrame = PauseMenuOverlay.IsOpen();
        _blockingMenuOpenThisFrame = GameOverlayVisibility.ComputeBlockingMenuOpen(_pauseMenuOpenThisFrame);
    }

    private static void InvalidateDiscoveryCaches()
    {
        GameOverlayVisibility.InvalidateCache();
        PauseMenuOverlay.InvalidateLookupCache();
        UtilityBarOverlay.InvalidateDiscoveryCache();
    }

    private static void OnProcessFrame()
    {
        long frameStart = OverlayPerfDiagnostics.BeginTick();
        try
        {
            RefreshMenuStateThisFrame();

            SettingsOverlay.UpdateFrame();
            OverlayVisToggle.UpdateFrame();
            ModManagerSettingsBridge.TryHydrateFromPersistedValues();
            SettingsStore.MaybeReload();
            ModConfigScrollHarmonizer.Sync(_blockingMenuOpenThisFrame);

            if (_pauseMenuOpenThisFrame)
            {
                if (!_wasPauseMenuOpenForDwell)
                    DwellHoverService.Reset();

                _wasPauseMenuOpenForDwell = true;
                SuppressDuringPause();
                PauseMenuOverlay.Sync();
                BackButtonOverlay.Sync();
                FinalizePauseMenuDwellTargets();
                return;
            }

            _wasPauseMenuOpenForDwell = false;
            PauseMenuOverlay.Hide();

            if (GameOverlayVisibility.ShouldHideOverlays(_blockingMenuOpenThisFrame))
            {
                SuppressForMenu();
                FinalizeDwellTargets();
                return;
            }

            long getModeStart = OverlayPerfDiagnostics.BeginTick();
            var mode = OverlayModeService.GetMode();
            OverlayPerfDiagnostics.AddCategory("getMode", getModeStart);

            bool showUtility = RunManager.Instance.IsInProgress;
            bool showBackButton = showUtility
                || MainMenuOverlay.IsActive()
                || CharacterSelectOverlay.IsActive();
            if (showBackButton)
                BackButtonOverlay.Sync();
            else
                BackButtonOverlay.Hide();

            if (mode == OverlayMode.None)
            {
                if (!showUtility && MainMenuOverlay.IsActive())
                {
                    _isTornDown = false;
                    MainMenuOverlay.Sync();
                    CharacterSelectOverlay.Hide();
                }
                else if (!showUtility && CharacterSelectOverlay.IsActive())
                {
                    _isTornDown = false;
                    CharacterSelectOverlay.Sync();
                    MainMenuOverlay.Hide();
                }
                else
                {
                    MainMenuOverlay.Hide();
                    CharacterSelectOverlay.Hide();
                    if (!_isTornDown)
                        TearDown();
                }

                UtilityBarOverlay.Sync(showUtility);
                if (showUtility)
                    OverlayCanvasHost.EnsureInputRouter();
                FinalizeDwellTargets();
                return;
            }

            MainMenuOverlay.Hide();
            CharacterSelectOverlay.Hide();
            UtilityBarOverlay.Sync(showUtility);

            if (_activeOverlayMode != mode)
            {
                ModLogger.Info($"[Mode] {_activeOverlayMode} -> {mode} ({OverlayModeService.DebugSnapshot()})");

                // A selection screen just appeared — block accidental instant picks until the
                // cursor settles and moves again. Skip when opening a card draft on top of loot
                // so map/deck/pause utilities stay responsive.
                if (ScreenOverlays.ContainsKey(mode)
                    && !(mode == OverlayMode.PileSelect && _activeOverlayMode == OverlayMode.Rewards))
                {
                    DwellHoverService.ArmGrace(1.0f);
                }

                if (mode == OverlayMode.HandSelect)
                    ConfirmOverlay.RefreshNativeConfirmCache();
                else if (mode == OverlayMode.CombatPlay)
                    EndTurnOverlay.EnsureNativeEndTurnCached();

                ViewScreenQuery.Invalidate();
                InvalidateDiscoveryCaches();

                _isTornDown = false;
                _activeOverlayMode = mode;
            }

            OverlayCanvasHost.EnsureInputRouter();

            // A deck / draw / exhaust / card-pile view (or the map) opened on top of combat: combat is
            // still in progress underneath, so suppress the combat overlay to stop its play buttons
            // bleeding through the viewed pile. The Back button stays available to close the view.
            if (mode is OverlayMode.CombatPlay or OverlayMode.HandSelect
                && ViewScreenQuery.IsViewingScreenOpen())
            {
                SuppressCombatForView();
                FinalizeDwellTargets();
                return;
            }

            if (ScreenOverlays.TryGetValue(mode, out var screenOverlay))
            {
                SyncScreenMode(screenOverlay);
                PotionSlotOverlay.Sync();
                PotionPopupOverlay.Sync();
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
                CombatRowsCoordinator.Clear();
                if (CardConfirmPhaseQuery.IsActive())
                {
                    ConfirmOverlay.Hide();
                    HandSelectOverlay.Hide();
                }
                else
                {
                    ConfirmOverlay.Sync(visible: true);
                    HandSelectOverlay.Sync(hand);
                }
            }
            else
            {
                HandSelectOverlay.Hide();
                ConfirmOverlay.Hide();
                EndTurnOverlay.Sync(visible: true);
                CombatRowsCoordinator.SyncCombatPlay(hand, OverlayCanvasHost.FallbackRoot);
            }

            long potionStart = OverlayPerfDiagnostics.BeginTick();
            PotionSlotOverlay.Sync();
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

    private static void FinalizePauseMenuDwellTargets()
    {
        long dwellStart = OverlayPerfDiagnostics.BeginTick();
        try
        {
            var dwellTargets = new List<DwellHoverService.Target>();
            OverlayVisToggle.CollectDwellTargets(dwellTargets);
            SettingsOverlay.TryAddGearTarget(dwellTargets);
            BackButtonOverlay.CollectDwellTargets(dwellTargets);
            PauseMenuOverlay.CollectDwellTargets(dwellTargets);
            PotionSlotOverlay.CollectDwellTargets(dwellTargets);
            DwellDebugOverlay.Render(dwellTargets);
            DwellHoverService.ProcessFrame(dwellTargets, GetProcessDelta());
        }
        finally
        {
            OverlayPerfDiagnostics.AddCategory("dwell", dwellStart);
        }
    }

    private static void FinalizeDwellTargets()
    {
        long dwellStart = OverlayPerfDiagnostics.BeginTick();
        try
        {
            // Run after mode-specific sync so map/shop handlers cannot hide deck scroll mid-frame.
            DeckViewOverlay.Sync();
            ViewScrollOverlay.Sync();

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
        HandSelectOverlay.Hide();
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
                if (!ViewScreenQuery.IsDeckViewOpen())
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

    private static void SuppressDuringPause()
    {
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        UtilityBarOverlay.Hide();
        PotionPopupOverlay.Hide();
        PotionSlotOverlay.Hide();
        EnemyLabelOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        ViewScrollOverlay.Hide();
        DeckViewOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        HandSelectOverlay.Hide();
        CombatRowsCoordinator.Clear();
        _isTornDown = true;
        _activeOverlayMode = OverlayMode.None;
    }

    private static void SuppressForMenu()
    {
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        UtilityBarOverlay.Hide();
        PotionPopupOverlay.Hide();
        PotionSlotOverlay.Hide();
        EnemyLabelOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        ViewScrollOverlay.Hide();
        DeckViewOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        BackButtonOverlay.Hide();
        HandSelectOverlay.Hide();
        PauseMenuOverlay.Hide();
        MainMenuOverlay.Hide();
        CharacterSelectOverlay.Hide();
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
        ViewScrollOverlay.Hide();
        DeckViewOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        BackButtonOverlay.Hide();
        HandSelectOverlay.Hide();
        PauseMenuOverlay.Hide();
        MainMenuOverlay.Hide();
        CharacterSelectOverlay.Hide();
        PotionPopupOverlay.Hide();
        PotionSlotOverlay.Hide();
        EnemyLabelOverlay.Hide();
        CombatRowsCoordinator.Clear();
        _isTornDown = true;
        _activeOverlayMode = OverlayMode.None;
    }
}
