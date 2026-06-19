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
    private const int GapAboveCard = 28;
    private const int CanvasLayerOrder = 128;
    private const float HandBlockPadding = 24f;

    private static DwellInputRouter? _inputRouter;
    private static CanvasLayer? _layer;
    private static Control? _fallbackRoot;
    private static Rect2 _handBlockBounds;
    private static readonly Dictionary<ulong, CardButtonRow> _rows = new();
    private static bool _hooked;
    private static bool _isTornDown = true;
    private static OverlayMode _activeOverlayMode = OverlayMode.None;

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

        var confirm = ConfirmOverlay.GetDwellTarget();
        if (confirm != null)
            targets.Add(confirm.Value);

        var endTurn = EndTurnOverlay.GetDwellTarget();
        if (endTurn != null)
            targets.Add(endTurn.Value);

        var mode = OverlayModeService.GetMode();
        if (mode == OverlayMode.Rewards)
            RewardsOverlay.CollectDwellTargets(targets);
        else if (mode == OverlayMode.PileSelect)
            PileSelectOverlay.CollectDwellTargets(targets);
        else if (mode == OverlayMode.Map)
            MapOverlay.CollectDwellTargets(targets);
        else if (mode == OverlayMode.Event)
            EventOverlay.CollectDwellTargets(targets);
        else if (mode == OverlayMode.Shop)
            ShopOverlay.CollectDwellTargets(targets);
        else if (mode == OverlayMode.Room)
            RoomOverlay.CollectDwellTargets(targets);
        else
        {
            PotionPopupOverlay.CollectDwellTargets(targets);
            foreach (var row in _rows.Values)
                row.CollectDwellTargets(targets);
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

        foreach (var row in _rows.Values)
        {
            if (row.TryActivateAt(globalPos, out message))
                return true;
        }

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

        if (_handBlockBounds.Size.X < 1 || _handBlockBounds.Size.Y < 1)
            return false;

        return _handBlockBounds.HasPoint(globalPos);
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

        foreach (var row in _rows.Values)
        {
            if (row.TryHitAt(globalPos, out message))
                return true;
        }

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

            bool showUtility = RunManager.Instance.IsInProgress
                && mode is not OverlayMode.Rewards and not OverlayMode.PileSelect and not OverlayMode.Map and not OverlayMode.Event and not OverlayMode.Shop;
            UtilityBarOverlay.Sync(showUtility);
            BackButtonOverlay.Sync();

            if (mode == OverlayMode.None)
            {
                if (!_isTornDown)
                    TearDown();
                UtilityBarOverlay.Sync(showUtility);
                if (showUtility)
                    EnsureInputRouter();
                FinalizeDwellTargets();
                return;
            }

            if (_activeOverlayMode != mode)
            {
                ModLogger.Info($"[Mode] {_activeOverlayMode} -> {mode} ({OverlayModeService.DebugSnapshot()})");

                // A selection screen just appeared — block accidental instant picks until the
                // cursor settles and moves again.
                if (mode is OverlayMode.Rewards or OverlayMode.PileSelect or OverlayMode.Map or OverlayMode.Event or OverlayMode.Shop or OverlayMode.Room)
                    DwellHoverService.ArmGrace(1.0f);

                _isTornDown = false;
                _activeOverlayMode = mode;
            }

            EnsureInputRouter();

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

            if (mode == OverlayMode.Rewards)
            {
                SyncRewardsMode();
                FinalizeDwellTargets();
                return;
            }

            if (mode == OverlayMode.PileSelect)
            {
                SyncPileSelectMode();
                FinalizeDwellTargets();
                return;
            }

            if (mode == OverlayMode.Map)
            {
                SyncMapMode();
                FinalizeDwellTargets();
                return;
            }

            if (mode == OverlayMode.Event)
            {
                SyncEventMode();
                FinalizeDwellTargets();
                return;
            }

            if (mode == OverlayMode.Shop)
            {
                SyncShopMode();
                FinalizeDwellTargets();
                return;
            }

            if (mode == OverlayMode.Room)
            {
                SyncRoomMode();
                FinalizeDwellTargets();
                return;
            }

            RewardsOverlay.Hide();
            PileSelectOverlay.Hide();
            MapOverlay.Hide();
            EventOverlay.Hide();
            ShopOverlay.Hide();
            RoomOverlay.Hide();

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

            EnsureFallbackCanvas();

            if (mode == OverlayMode.HandSelect)
            {
                EndTurnOverlay.Hide();
                EnemyLabelOverlay.Hide();
                ConfirmOverlay.Sync(visible: true);
                SyncHandSelectRows(hand);
            }
            else
            {
                ConfirmOverlay.Hide();
                EndTurnOverlay.Sync(visible: true);
                SyncCombatPlayRows(hand);
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
            var dwellTargets = new List<DwellHoverService.Target>();

            long collectStart = OverlayPerfDiagnostics.BeginTick();
            CollectDwellTargets(dwellTargets);
            OverlayPerfDiagnostics.Add("dwell.collect", collectStart);

            long processStart = OverlayPerfDiagnostics.BeginTick();
            DwellHoverService.ProcessFrame(dwellTargets, GetProcessDelta());
            OverlayPerfDiagnostics.Add("dwell.process", processStart);
        }
        finally
        {
            OverlayPerfDiagnostics.AddCategory("dwell", dwellStart);
        }
    }

    private static void SyncPileSelectMode()
    {
        // NOTE: do NOT call DwellHoverService.Reset() here — this runs every frame, and resetting
        // every frame zeroes the dwell timer before it can ever reach the activation threshold
        // (that bug made native dwell unusable on the rewards / pile-select screens).
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        ClearHandRows();
        EnemyLabelOverlay.Hide();
        RewardsOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        PileSelectOverlay.Sync();
        UtilityBarOverlay.Sync(RunManager.Instance.IsInProgress);
    }

    private static void SyncMapMode()
    {
        // NOTE: like the rewards/pile screens, do NOT reset the dwell service every frame here.
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        ClearHandRows();
        EnemyLabelOverlay.Hide();
        RewardsOverlay.Hide();
        PileSelectOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        MapOverlay.Sync();
    }

    private static void SyncEventMode()
    {
        // NOTE: like the rewards/pile/map screens, do NOT reset the dwell service every frame here.
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        ClearHandRows();
        EnemyLabelOverlay.Hide();
        RewardsOverlay.Hide();
        PileSelectOverlay.Hide();
        MapOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        EventOverlay.Sync();
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
        ClearHandRows();
    }

    private static void SyncShopMode()
    {
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        ClearHandRows();
        EnemyLabelOverlay.Hide();
        RewardsOverlay.Hide();
        PileSelectOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        EventOverlay.Hide();
        RoomOverlay.Hide();
        ShopOverlay.Sync();
    }

    private static void SyncRoomMode()
    {
        // NOTE: like the rewards/pile/map/event screens, do NOT reset the dwell service every frame.
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        ClearHandRows();
        EnemyLabelOverlay.Hide();
        RewardsOverlay.Hide();
        PileSelectOverlay.Hide();
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Sync();
        UtilityBarOverlay.Sync(RunManager.Instance.IsInProgress);
    }

    private static void SyncRewardsMode()
    {
        // NOTE: do NOT call DwellHoverService.Reset() here — see SyncPileSelectMode. Resetting every
        // frame is what kept the native Proceed/Skip dwell from ever firing.
        MapOverlay.Hide();
        LeftHoverScrollOverlay.Hide();
        EventOverlay.Hide();
        ShopOverlay.Hide();
        HandInputBlocker.Release();
        MouseCardPlayBlocker.Release();
        HandLayoutDiagnostics.Reset();
        EndTurnOverlay.Hide();
        ConfirmOverlay.Hide();
        ClearHandRows();
        EnemyLabelOverlay.Hide();
        RoomOverlay.Hide();
        RewardsOverlay.Sync();
    }

    private static void SyncCombatPlayRows(NPlayerHand hand)
    {
        long enemyStart = OverlayPerfDiagnostics.BeginTick();
        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = runState == null ? null : MegaCrit.Sts2.Core.Context.LocalContext.GetMe(runState);
        var enemies = EnemyOrderService.GetAliveEnemiesLeftToRight(player?.Creature.CombatState);
        int enemyCount = enemies.Count;
        OverlayPerfDiagnostics.Add("combat.enemyFetch", enemyStart);

        long holderStart = OverlayPerfDiagnostics.BeginTick();
        var holders = GetPlayModeHolders(hand);
        OverlayPerfDiagnostics.Add("combat.holderFetch", holderStart);

        HandLayoutDiagnostics.MaybeLog(hand, holders);

        int handSize = holders.Count(h => h.CardModel != null && NodeQuery.IsVisible(h));
        int buttonSize = SettingsStore.GetCardButtonSize(handSize);

        long boundsStart = OverlayPerfDiagnostics.BeginTick();
        Rect2 handBounds = ComputeHandBounds(holders);
        OverlayPerfDiagnostics.Add("combat.handBounds", boundsStart);

        long rowsStart = OverlayPerfDiagnostics.BeginTick();
        var liveIds = new HashSet<ulong>();
        foreach (var holder in holders)
        {
            var card = holder.CardModel;
            if (card == null || !NodeQuery.IsVisible(holder))
                continue;
            if (!card.CanPlay(out _, out _))
                continue;

            ulong id = holder.GetInstanceId();
            liveIds.Add(id);

            if (!_rows.TryGetValue(id, out var row))
            {
                row = new CardButtonRow(holder, _fallbackRoot);
                _rows[id] = row;
                ModLogger.Info($"Button row for {card.Id.Entry} holder={id} parented={(holder is Control)}");
            }

            row.SyncPlay(card, enemyCount, holder, buttonSize);
        }

        UpdateHandBlockBounds(handBounds);
        RemoveStaleRows(liveIds);
        OverlayPerfDiagnostics.Add("combat.rowsSync", rowsStart);

        long labelStart = OverlayPerfDiagnostics.BeginTick();
        if (SettingsStore.Current.ShowEnemyLabels)
            EnemyLabelOverlay.Sync(enemies, handSize);
        else
            EnemyLabelOverlay.Hide();
        OverlayPerfDiagnostics.Add("combat.labels", labelStart);
    }

    private static void SyncHandSelectRows(NPlayerHand hand)
    {
        var holders = GetPlayModeHolders(hand);
        HandLayoutDiagnostics.MaybeLog(hand, holders);

        int handSize = holders.Count(h => h.CardModel != null && NodeQuery.IsVisible(h));
        int buttonSize = SettingsStore.GetCardButtonSize(handSize);

        var liveIds = new HashSet<ulong>();
        int slot = 1;
        foreach (var holder in holders)
        {
            var card = holder.CardModel;
            if (card == null || !NodeQuery.IsVisible(holder))
                continue;

            ulong id = holder.GetInstanceId();
            liveIds.Add(id);

            if (!_rows.TryGetValue(id, out var row))
            {
                row = new CardButtonRow(holder, _fallbackRoot);
                _rows[id] = row;
                ModLogger.Info($"Select row for {card.Id.Entry} holder={id}");
            }

            row.SyncSelect(slot, holder, buttonSize);
            slot++;
        }

        UpdateHandBlockBounds(default);
        RemoveStaleRows(liveIds);
    }

    private static void RemoveStaleRows(HashSet<ulong> liveIds)
    {
        foreach (var pair in _rows.ToList())
        {
            if (!liveIds.Contains(pair.Key))
            {
                pair.Value.Dispose();
                _rows.Remove(pair.Key);
            }
        }
    }

    private static void ClearHandRows()
    {
        foreach (var row in _rows.Values)
            row.Dispose();
        _rows.Clear();
        _handBlockBounds = new Rect2(0, 0, 0, 0);
    }

    private static double GetProcessDelta()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        return tree?.Root?.GetProcessDeltaTime() ?? (1.0 / 60.0);
    }

    private static Rect2 ComputeHandBounds(IReadOnlyList<NCardHolder> holders)
    {
        if (holders.Count == 0)
            return new Rect2(0, 0, 0, 0);

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var holder in holders)
        {
            if (!NodeQuery.IsVisible(holder))
                continue;

            if (!CardAnchorService.TryGetCardRect(holder, out Rect2 cardRect))
                continue;

            minX = Math.Min(minX, cardRect.Position.X);
            maxX = Math.Max(maxX, cardRect.End.X);
            minY = Math.Min(minY, cardRect.Position.Y);
            maxY = Math.Max(maxY, cardRect.End.Y);
        }

        if (minX == float.MaxValue)
            return new Rect2(0, 0, 0, 0);

        float topPad = GapAboveCard + 90f;
        return new Rect2(
            minX - HandBlockPadding,
            minY - topPad,
            (maxX - minX) + (HandBlockPadding * 2f),
            (maxY - minY) + topPad + HandBlockPadding);
    }

    private static void UpdateHandBlockBounds(Rect2 handBounds)
    {
        _handBlockBounds = handBounds;
    }

    private static List<NCardHolder> GetPlayModeHolders(NPlayerHand hand)
    {
        var holders = new List<NCardHolder>();
        foreach (var holderObj in hand.ActiveHolders)
        {
            if (holderObj is NCardHolder typed)
                holders.Add(typed);
        }

        if (holders.Count == 0)
            holders.AddRange(NodeQuery.FindAllSortedByPosition<NCardHolder>(hand));

        holders.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            return cmp != 0 ? cmp : a.GetInstanceId().CompareTo(b.GetInstanceId());
        });

        return holders;
    }

    internal static void EnsureInputRouterAlways() => EnsureInputRouter();

    private static void EnsureInputRouter()
    {
        if (_inputRouter != null && NodeQuery.IsLive(_inputRouter))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _inputRouter = new DwellInputRouter { Name = "DwellTargetingInputRouter", ProcessMode = Node.ProcessModeEnum.Always };
        tree.Root.AddChild(_inputRouter);
        ModLogger.Info("Input router attached to scene root.");
    }

    private static void EnsureFallbackCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _fallbackRoot != null && NodeQuery.IsLive(_fallbackRoot))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellTargetingLayer" };
        tree.Root.AddChild(_layer);

        _fallbackRoot = new Control
        {
            Name = "DwellTargetingRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _fallbackRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_fallbackRoot);
        ModLogger.Info($"CanvasLayer created at layer {CanvasLayerOrder}.");
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
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        BackButtonOverlay.Hide();
        ClearHandRows();
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
        EventOverlay.Hide();
        ShopOverlay.Hide();
        RoomOverlay.Hide();
        BackButtonOverlay.Hide();
        PotionPopupOverlay.Hide();
        EnemyLabelOverlay.Hide();
        ClearHandRows();
        _isTornDown = true;
        _activeOverlayMode = OverlayMode.None;
    }
}
