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
    private const int CacheTtlMs = 150;

    private static OverlayMode _cachedMode = OverlayMode.None;
    private static ulong _cachedFrame;
    private static long _cachedTick;
    private static int _cachedInvalidationKey = int.MinValue;
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
        if (key == _cachedInvalidationKey && now - _cachedTick < CacheTtlMs)
        {
            _cachedFrame = frame;
            return _cachedMode;
        }

        RefreshMode(key, frame, now);
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
        _cachedRewardsScreen = null;
        _cachedPileSelectScreen = null;
        _cachedMapScreen = null;
        _cachedEventRoom = null;
        _cachedShopNode = null;
        _cachedRoomNode = null;
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

    private static void RefreshMode(int key, ulong frame, long now)
    {
        _cachedInvalidationKey = key;
        _cachedFrame = frame;
        _cachedTick = now;
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
            && mapScreen is { IsOpen: true, IsTravelEnabled: true })
        {
            _cachedMapScreen = mapScreen;
        }
        else if (_cachedPileSelectScreen == null && node is CanvasItem canvas && NodeQuery.IsVisible(canvas) && IsPileSelectScreen(node))
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

    private static bool IsPileSelectScreen(Node node) =>
        node is NCombatPileCardSelectScreen
            or NChooseACardSelectionScreen
            or NCardGridSelectionScreen
            or NSimpleCardSelectScreen
            or NCardRewardSelectionScreen;

    private static bool TryCaptureShopNode(Node node)
    {
        if (!NodeQuery.IsLive(node) || node is not CanvasItem canvas || !NodeQuery.IsVisible(canvas))
            return false;

        if (node is NMerchantRoom or NFakeMerchant)
            return true;

        return false;
    }
}
