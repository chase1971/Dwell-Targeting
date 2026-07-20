using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

internal enum OverlayMode
{
    None,
    CombatPlay,
    HandSelect,
    PileSelect,
    Rewards,
    Map,
    Event,
    Shop,
    Room
}

internal static class OverlayModeService
{
    private const long ModeRescanIntervalMs = 250;

    private static OverlayMode _cachedMode = OverlayMode.None;
    private static ulong _cachedFrame;
    private static int _cachedInvalidationKey = int.MinValue;
    private static long _lastScanTick;
    private static NRewardsScreen? _cachedRewardsScreen;
    private static Node? _cachedPileSelectScreen;
    private static NMapScreen? _cachedMapScreen;
    private static NEventRoom? _cachedEventRoom;
    private static Node? _cachedShopNode;
    private static Node? _cachedRoomNode;

    internal static OverlayMode GetMode()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _cachedFrame)
            return _cachedMode;

        int key = ComputeInvalidationKey();
        long now = System.Environment.TickCount64;
        if (key == _cachedInvalidationKey)
        {
            if (_cachedMode is OverlayMode.Room or OverlayMode.Event or OverlayMode.Shop
                && HasVisibleMapScreen())
            {
                RefreshMode(key, frame);
                ViewScreenQuery.Invalidate();
                return _cachedMode;
            }

            if (_cachedMode is OverlayMode.Rewards or OverlayMode.CombatPlay or OverlayMode.HandSelect
                && HasVisiblePileSelectScreen())
            {
                RefreshMode(key, frame);
                ViewScreenQuery.Invalidate();
                return _cachedMode;
            }

            if (_cachedMode is OverlayMode.Room or OverlayMode.Event
                && now - _lastScanTick >= ModeRescanIntervalMs
                && HasVisiblePileSelectScreen())
            {
                RefreshMode(key, frame);
                ViewScreenQuery.Invalidate();
                return _cachedMode;
            }

            if (_cachedMode is OverlayMode.Room or OverlayMode.Event
                && now - _lastScanTick >= ModeRescanIntervalMs
                && HasVisibleRewardsScreen())
            {
                RefreshMode(key, frame);
                ViewScreenQuery.Invalidate();
                return _cachedMode;
            }

            if (!CombatManager.Instance.IsInProgress && now - _lastScanTick >= ModeRescanIntervalMs)
            {
                if (!IsCachedModeStillValid())
                {
                    RefreshMode(key, frame);
                    ViewScreenQuery.Invalidate();
                }
                else
                {
                    _cachedFrame = frame;
                    _lastScanTick = now;
                }

                return _cachedMode;
            }

            _cachedFrame = frame;
            return _cachedMode;
        }

        RefreshMode(key, frame);
        ViewScreenQuery.Invalidate();
        return _cachedMode;
    }

    internal static bool IsHandOverlayActive()
    {
        var mode = GetMode();
        return mode is OverlayMode.CombatPlay or OverlayMode.HandSelect;
    }

    internal static bool TryGetPileSelectScreen(out Node screen)
    {
        GetMode();
        if (_cachedPileSelectScreen != null && NodeQuery.IsLive(_cachedPileSelectScreen))
        {
            screen = _cachedPileSelectScreen;
            return true;
        }

        screen = null!;
        return false;
    }

    internal static NMapScreen? GetCachedMapScreen()
    {
        GetMode();
        if (_cachedMapScreen != null && NodeQuery.IsLive(_cachedMapScreen))
            return _cachedMapScreen;

        return null;
    }

    internal static NEventRoom? GetCachedEventRoom()
    {
        GetMode();
        if (_cachedEventRoom != null && NodeQuery.IsLive(_cachedEventRoom))
            return _cachedEventRoom;

        return null;
    }

    internal static Node? GetCachedShopNode()
    {
        GetMode();
        if (_cachedShopNode != null && NodeQuery.IsLive(_cachedShopNode))
            return _cachedShopNode;

        return null;
    }

    internal static Node? GetCachedRoomNode()
    {
        GetMode();
        if (_cachedRoomNode != null && NodeQuery.IsLive(_cachedRoomNode))
            return _cachedRoomNode;

        return null;
    }

    internal static NRewardsScreen? GetCachedRewardsScreen()
    {
        GetMode();
        if (_cachedRewardsScreen != null && NodeQuery.IsLive(_cachedRewardsScreen))
        {
            if (NodeQuery.IsVisible(_cachedRewardsScreen) || RewardsScreenQuery.HasVisibleChoices(_cachedRewardsScreen))
                return _cachedRewardsScreen;
        }

        return null;
    }

    internal static string DebugSnapshot() =>
        $"mode={_cachedMode} rewards={_cachedRewardsScreen != null} pile={_cachedPileSelectScreen != null} map={_cachedMapScreen != null} event={_cachedEventRoom != null} shop={_cachedShopNode != null} room={_cachedRoomNode != null}";

    internal static void InvalidateCache()
    {
        _cachedInvalidationKey = int.MinValue;
        _cachedFrame = 0;
        _lastScanTick = 0;
        _cachedRewardsScreen = null;
        _cachedPileSelectScreen = null;
        _cachedMapScreen = null;
        _cachedEventRoom = null;
        _cachedShopNode = null;
        _cachedRoomNode = null;
    }

    private static bool IsCachedModeStillValid()
    {
        long now = System.Environment.TickCount64;
        switch (_cachedMode)
        {
            case OverlayMode.PileSelect:
                if (IsLiveVisibleScreen(_cachedPileSelectScreen))
                    return true;
                return PilePreviewQuery.IsUpgradeConfirmFlowActive();
            case OverlayMode.Rewards:
                if (HasVisiblePileSelectScreen())
                    return false;
                return _cachedRewardsScreen != null
                    && NodeQuery.IsLive(_cachedRewardsScreen)
                    && (NodeQuery.IsVisible(_cachedRewardsScreen)
                        || RewardsScreenQuery.HasVisibleChoices(_cachedRewardsScreen));
            case OverlayMode.Map:
                return _cachedMapScreen != null
                    && NodeQuery.IsLive(_cachedMapScreen)
                    && NodeQuery.IsVisible(_cachedMapScreen)
                    && _cachedMapScreen.IsOpen;
            case OverlayMode.Shop:
                if (HasVisibleMapScreen())
                    return false;
                return IsLiveVisibleScreen(_cachedShopNode);
            case OverlayMode.Event:
                if (HasVisibleMapScreen())
                    return false;
                if (now - _lastScanTick >= ModeRescanIntervalMs
                    && (HasVisiblePileSelectScreen() || HasVisibleRewardsScreen()))
                    return false;
                return IsLiveVisibleScreen(_cachedEventRoom);
            case OverlayMode.Room:
                if (HasVisibleMapScreen())
                    return false;
                if (now - _lastScanTick >= ModeRescanIntervalMs
                    && (HasVisiblePileSelectScreen() || HasVisibleRewardsScreen()))
                    return false;
                return IsLiveVisibleScreen(_cachedRoomNode);
            case OverlayMode.CombatPlay:
            case OverlayMode.HandSelect:
                if (now - _lastScanTick >= ModeRescanIntervalMs
                    && (HasVisiblePileSelectScreen() || HasVisibleRewardsScreen()))
                    return false;
                return CombatManager.Instance.IsInProgress;
            default:
                return false;
        }
    }

    private static bool IsLiveVisibleScreen(Node? node) =>
        node != null
        && NodeQuery.IsLive(node)
        && node is CanvasItem canvas
        && NodeQuery.IsVisible(canvas);

    /// <summary>
    /// Rest/event rooms stay visible under card/reward sub-screens — force a mode rescan so those modes win.
    /// </summary>
    private static bool HasVisiblePileSelectScreen()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        return FindVisiblePileSelectScreen(tree.Root) != null
            || CombatCardChoiceQuery.TryFindOfferScanRoot(out _);
    }

    private static void TryCaptureCombatOfferScreen()
    {
        if (_cachedPileSelectScreen != null || !CombatManager.Instance.IsInProgress)
            return;

        if (CombatCardChoiceQuery.TryFindOfferScanRoot(out Node scanRoot))
            _cachedPileSelectScreen = scanRoot;
    }

    private static bool HasVisibleRewardsScreen()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        return FindVisibleRewardsScreen(tree.Root) != null;
    }

    private static bool HasVisibleMapScreen()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return false;

        return FindVisibleMapScreen(tree.Root) != null;
    }

    private static NMapScreen? FindVisibleMapScreen(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return null;

        if (node is CanvasItem canvas && !NodeQuery.IsVisible(canvas))
            return null;

        if (node is NMapScreen mapScreen
            && NodeQuery.IsLive(mapScreen)
            && NodeQuery.IsVisible(mapScreen)
            && mapScreen.IsOpen)
        {
            return mapScreen;
        }

        try
        {
            foreach (var child in node.GetChildren())
            {
                var found = FindVisibleMapScreen(child);
                if (found != null)
                    return found;
            }
        }
        catch
        {
            /* disposed mid-walk */
        }

        return null;
    }

    private static Node? FindVisiblePileSelectScreen(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return null;

        if (node is CanvasItem canvas && NodeQuery.IsVisible(canvas) && PileSelectScreenMatcher.IsPileSelectScreen(node))
            return node;

        try
        {
            foreach (var child in node.GetChildren())
            {
                var found = FindVisiblePileSelectScreen(child);
                if (found != null)
                    return found;
            }
        }
        catch
        {
            /* disposed mid-walk */
        }

        return null;
    }

    private static NRewardsScreen? FindVisibleRewardsScreen(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return null;

        if (node is CanvasItem canvas && !NodeQuery.IsVisible(canvas))
            return null;

        if (node is NRewardsScreen rewards && NodeQuery.IsLive(rewards)
            && NodeQuery.IsVisible(rewards) && RewardsScreenQuery.HasVisibleChoices(rewards))
        {
            return rewards;
        }

        try
        {
            foreach (var child in node.GetChildren())
            {
                var found = FindVisibleRewardsScreen(child);
                if (found != null)
                    return found;
            }
        }
        catch
        {
            /* disposed mid-walk */
        }

        return null;
    }

    private static int ComputeInvalidationKey()
    {
        int key = 0;
        if (RunManager.Instance.IsInProgress)
            key ^= 1;

        if (CombatManager.Instance.IsInProgress)
            key ^= 2;

        if (CombatManager.Instance.PlayerActionsDisabled)
            key ^= 4;

        var hand = NPlayerHand.Instance;
        if (hand != null && NodeQuery.IsLive(hand))
        {
            key ^= (int)(hand.GetInstanceId() & 0xFFFF);
            key ^= ((int)hand.CurrentMode) << 16;
        }

        return key;
    }

    private static void RefreshMode(int key, ulong frame)
    {
        _lastScanTick = System.Environment.TickCount64;
        _cachedInvalidationKey = key;
        _cachedFrame = frame;
        _cachedRewardsScreen = null;
        _cachedPileSelectScreen = null;
        _cachedMapScreen = null;
        _cachedEventRoom = null;
        _cachedShopNode = null;
        _cachedRoomNode = null;
        _cachedMode = OverlayMode.None;

        if (!RunManager.Instance.IsInProgress)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        ScanTree(tree.Root);
        TryCaptureCombatOfferScreen();

        if (_cachedPileSelectScreen == null && PilePreviewQuery.IsUpgradeConfirmFlowActive())
        {
            _cachedMode = OverlayMode.PileSelect;
            return;
        }

        if (_cachedPileSelectScreen != null)
        {
            _cachedMode = OverlayMode.PileSelect;
            return;
        }

        // An open, travel-enabled map is proof the rewards step is finished — the game only enables
        // travel once nothing is blocking it. So a travelable map outranks a lingering/ghost rewards
        // screen, otherwise a claimed rewards screen that still reports "has choices" keeps Rewards
        // mode active and its number buttons bleed through on top of the map.
        if (_cachedMapScreen != null)
        {
            _cachedMode = OverlayMode.Map;
            return;
        }

        if (_cachedRewardsScreen != null)
        {
            _cachedMode = OverlayMode.Rewards;
            return;
        }

        // Merchant rooms (regular shop + fake/Timu merchant) outrank generic event rooms — the fake
        // merchant inherits event-room structure but uses shop inventory nodes, not NEventOptionButton.
        if (_cachedShopNode != null && !CombatManager.Instance.IsInProgress)
        {
            _cachedMode = OverlayMode.Shop;
            return;
        }

        // Events are the lowest-priority screen: only show event option dwell when no card/reward/map
        // screen is up and we're not inside an embedded event combat.
        if (_cachedEventRoom != null && !CombatManager.Instance.IsInProgress)
        {
            _cachedMode = OverlayMode.Event;
            return;
        }

        // Rest sites and treasure (chest) rooms are the lowest-priority interactive screens: a card
        // grid (PileSelect, e.g. the rest-site upgrade picker) or a relic rewards screen (Rewards, e.g.
        // an opened chest) outranks them, so Room only wins once those sub-screens are gone.
        if (_cachedRoomNode != null && !CombatManager.Instance.IsInProgress)
        {
            _cachedMode = OverlayMode.Room;
            return;
        }

        if (!CombatManager.Instance.IsInProgress || CombatManager.Instance.PlayerActionsDisabled)
            return;

        var hand = NPlayerHand.Instance;
        if (hand == null || !NodeQuery.IsLive(hand))
            return;

        if (hand.CurrentMode == NPlayerHand.Mode.Play)
        {
            _cachedMode = OverlayMode.CombatPlay;
            return;
        }

        if (hand.CurrentMode == NPlayerHand.Mode.SimpleSelect
            || hand.CurrentMode == NPlayerHand.Mode.UpgradeSelect)
        {
            _cachedMode = OverlayMode.HandSelect;
        }
    }

    private static void ScanTree(Node node)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (node is CanvasItem canvasItem && !NodeQuery.IsVisible(canvasItem))
            return;

        if (node is NRewardsScreen rewards && NodeQuery.IsLive(rewards)
            && NodeQuery.IsVisible(rewards) && RewardsScreenQuery.HasVisibleChoices(rewards))
        {
            // Only treat a rewards screen as active if it still has real choices (reward buttons or
            // a live proceed/skip). A claimed/empty ghost screen left in the tree must NOT win, or it
            // blocks pile-select (card draft) and map modes from ever activating.
            _cachedRewardsScreen = rewards;
        }
        else if (_cachedMapScreen == null && node is NMapScreen mapScreen
            && NodeQuery.IsLive(mapScreen) && NodeQuery.IsVisible(mapScreen)
            && mapScreen.IsOpen)
        {
            _cachedMapScreen = mapScreen;
        }
        else if (_cachedPileSelectScreen == null && node is CanvasItem canvas && NodeQuery.IsVisible(canvas) && PileSelectScreenMatcher.IsPileSelectScreen(node))
            _cachedPileSelectScreen = node;
        else if (_cachedShopNode == null && TryCaptureShopNode(node))
            _cachedShopNode = node;
        else if (_cachedEventRoom == null && node is NEventRoom eventRoom
            && NodeQuery.IsLive(eventRoom) && NodeQuery.IsVisible(eventRoom)
            && node is not NFakeMerchant)
            _cachedEventRoom = eventRoom;
        else if (_cachedRoomNode == null && node is (NRestSiteRoom or NTreasureRoom)
            && node is CanvasItem roomCanvas && NodeQuery.IsVisible(roomCanvas))
            _cachedRoomNode = node;

        if (_cachedRewardsScreen != null && _cachedPileSelectScreen != null)
            return;

        try
        {
            foreach (var child in node.GetChildren())
            {
                ScanTree(child);
                if (_cachedRewardsScreen != null && _cachedPileSelectScreen != null)
                    return;
            }
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static bool TryCaptureShopNode(Node node)
    {
        if (!NodeQuery.IsLive(node) || node is not CanvasItem canvas || !NodeQuery.IsVisible(canvas))
            return false;

        if (node is NMerchantRoom or NFakeMerchant)
            return true;

        return false;
    }
}
