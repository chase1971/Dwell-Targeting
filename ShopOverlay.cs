using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace DwellTargeting;

/// <summary>
/// Shop dwell targets: cards and card-removal use direct hover-to-buy; relics and potions use offset
/// number buttons so the user can hover the item for its tooltip before dwelling the number to buy.
/// </summary>
internal static class ShopOverlay
{
    private const int CanvasLayerOrder = 132;
    private const int NumberSize = 54;
    private const float NumberGap = 14f;
    private const float ScreenMargin = 10f;
    private const float ProceedHitboxPadding = 24f;
    private const float RemovalHitboxPadding = 20f;

    private static Node? _shopRoot;
    private static List<Control>? _cardControls;
    private static List<Control>? _numberedControls;
    private static List<Control>? _removalControls;
    private static List<Control>? _merchantButtonControls;
    private static List<Control>? _characterControls;
    private static NProceedButton? _proceedButton;
    private static ulong _cachedShopId;
    private static bool _cachedInventoryOpen;
    private static ScreenEntryScanState _entryScan;

    private static CanvasLayer? _layer;
    private static Control? _root;
    private static readonly List<Button> _numberButtons = new();

    internal static void Sync()
    {
        _shopRoot = OverlayModeService.GetCachedShopNode();
        if (_shopRoot == null)
        {
            Hide();
            return;
        }

        ulong shopId = _shopRoot.GetInstanceId();
        bool inventoryOpen = ShopInventoryQuery.IsInventoryOpen(ResolveSearchRoot(_shopRoot));
        bool hasValidCache = HasValidDiscovery()
            && _cachedShopId == shopId
            && _cachedInventoryOpen == inventoryOpen;

        if (!_entryScan.ShouldScanNow(shopId, hasValidCache, out _))
        {
            if (_numberedControls is { Count: > 0 })
                SyncNumberButtons();
            else
                HideNumberButtons();
            return;
        }

        _cachedShopId = shopId;
        _cachedInventoryOpen = inventoryOpen;
        Rescan(_shopRoot);

        if (!_entryScan.RegisterScanResult(CountDiscoveryTargets(), "Shop"))
            return;

        if (_numberedControls is { Count: > 0 })
            SyncNumberButtons();
        else
            HideNumberButtons();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_shopRoot == null)
            return;

        CollectDirectTargets(targets, _merchantButtonControls, "ShopMerchant", c => ActivateWare(c, 0));
        CollectDirectTargets(targets, _characterControls, "ShopCharacter", ActivateCharacter);
        CollectDirectTargets(targets, _cardControls, "ShopCard", c => ActivateWare(c, 0));
        CollectDirectTargets(targets, _removalControls, "ShopRemoval", c => ActivateWare(c, 0), RemovalHitboxPadding);

        if (_numberedControls != null)
        {
            for (int i = 0; i < _numberButtons.Count && i < _numberedControls.Count; i++)
            {
                var button = _numberButtons[i];
                var ware = _numberedControls[i];
                if (button == null || !NodeQuery.IsLive(button) || !button.Visible)
                    continue;
                if (!IsSelectable(ware))
                    continue;

                var captured = ware;
                int capturedSlot = i + 1;
                targets.Add(DwellHoverService.Menu(
                    button.GetGlobalRect(),
                    () => ActivateWare(captured, capturedSlot),
                    $"ShopNumber:{capturedSlot}"));
            }
        }

        if (TryGetProceedRect(out var proceedRect))
        {
            targets.Add(DwellHoverService.Menu(
                proceedRect,
                ActivateProceed,
                "ShopProceed"));
        }
    }

    internal static void Hide()
    {
        _shopRoot = null;
        _cardControls = null;
        _numberedControls = null;
        _removalControls = null;
        _merchantButtonControls = null;
        _characterControls = null;
        _proceedButton = null;
        _cachedShopId = 0;
        _cachedInventoryOpen = false;
        _entryScan.OnHide();
        HideNumberButtons();
    }

    internal static void InvalidateDiscovery()
    {
        _cardControls = null;
        _numberedControls = null;
        _removalControls = null;
        _merchantButtonControls = null;
        _characterControls = null;
        _proceedButton = null;
        _entryScan.ScheduleRescan("Shop");
    }

    private static bool HasValidDiscovery() =>
        _cardControls != null
        && (CountDiscoveryTargets() > 0 || _proceedButton != null);

    private static int CountDiscoveryTargets() =>
        (_merchantButtonControls?.Count ?? 0)
        + (_characterControls?.Count ?? 0)
        + (_cardControls?.Count ?? 0)
        + (_removalControls?.Count ?? 0)
        + (_numberedControls?.Count ?? 0);

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (TryRouteDirectClick(globalPos, _merchantButtonControls, c => ActivateWare(c, 0), 0f, "Shop merchant clicked"))
        {
            message = "Shop merchant clicked";
            return true;
        }

        if (TryRouteDirectClick(globalPos, _characterControls, ActivateCharacter, 0f, "Shop character clicked"))
        {
            message = "Shop character clicked";
            return true;
        }

        if (TryRouteDirectClick(globalPos, _cardControls, c => ActivateWare(c, 0), 0f, "Shop card clicked"))
        {
            message = "Shop card clicked";
            return true;
        }

        if (TryRouteDirectClick(globalPos, _removalControls, c => ActivateWare(c, 0), RemovalHitboxPadding, "Shop removal clicked"))
        {
            message = "Shop removal clicked";
            return true;
        }

        for (int i = 0; i < _numberButtons.Count && _numberedControls != null && i < _numberedControls.Count; i++)
        {
            var button = _numberButtons[i];
            if (button == null || !NodeQuery.IsLive(button) || !button.Visible)
                continue;
            if (!button.GetGlobalRect().HasPoint(globalPos))
                continue;

            ActivateWare(_numberedControls[i], i + 1);
            message = "Shop numbered ware clicked";
            return true;
        }

        if (TryGetProceedRect(out var proceedRect) && proceedRect.HasPoint(globalPos))
        {
            if (!DwellActivationCooldown.TryRunMenuAction(ActivateProceed))
                return false;

            message = "Shop proceed clicked";
            return true;
        }

        return false;
    }

    private static void CollectDirectTargets(
        List<DwellHoverService.Target> targets,
        List<Control>? controls,
        string labelPrefix,
        Action<Control> activate,
        float padding = 0f)
    {
        if (controls == null)
            return;

        int slot = 1;
        foreach (var control in controls)
        {
            if (!IsSelectable(control))
            {
                slot++;
                continue;
            }

            if (!TryGetControlRect(control, padding, out var rect))
            {
                slot++;
                continue;
            }

            var captured = control;
            targets.Add(DwellHoverService.Menu(
                rect,
                () => activate(captured),
                $"{labelPrefix}:{slot}"));
            slot++;
        }
    }

    private static bool TryRouteDirectClick(
        Vector2 globalPos,
        List<Control>? controls,
        Action<Control> activate,
        float padding,
        string _)
    {
        if (controls == null)
            return false;

        foreach (var control in controls)
        {
            if (!IsSelectable(control))
                continue;
            if (!TryGetControlRect(control, padding, out var rect) || !rect.HasPoint(globalPos))
                continue;

            activate(control);
            return true;
        }

        return false;
    }

    private static bool TryGetControlRect(Control control, float padding, out Rect2 rect)
    {
        rect = default;

        // Shop cards and card-removal render inner visuals offset from the slot layout rect (low/right).
        // Anchor dwell boxes to the slot's clickable hitbox — same canvas-transform path as combat hand cards.
        if (control is NMerchantSlot merchantSlot
            && (ShopSelectionService.SlotContains<NMerchantCard>(merchantSlot)
                || ShopSelectionService.SlotContains<NMerchantCardRemoval>(merchantSlot))
            && TryGetMerchantSlotClickRect(merchantSlot, out rect))
        {
            if (padding > 0f)
                rect = rect.Grow(padding);

            return rect.Size.X >= 8f && rect.Size.Y >= 8f;
        }

        if (!ControlHitboxService.TryGetDwellRect(control, out rect))
            rect = control.GetGlobalRect();

        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        if (padding > 0f)
            rect = rect.Grow(padding);

        return true;
    }

    private static bool TryGetMerchantSlotClickRect(Node slot, out Rect2 rect)
    {
        rect = default;

        // Inner merchant visuals are centre-pivoted/scaled; layout rects sit offset from the art. The slot's
        // clickable hitbox (what the game hit-tests) carries the real local offset and scale.
        float bestArea = 0f;
        foreach (var clickable in NodeQuery.FindAll<NClickableControl>(slot))
        {
            if (clickable is not Control ctrl || !NodeQuery.IsVisible(ctrl))
                continue;

            if (!TryScreenRect(ctrl, out var candidate))
                continue;

            float area = candidate.Size.X * candidate.Size.Y;
            if (area > bestArea && candidate.Size.X >= 24f && candidate.Size.Y >= 24f)
            {
                bestArea = area;
                rect = candidate;
            }
        }

        return bestArea > 0f;
    }

    private static bool TryScreenRect(Control control, out Rect2 rect)
    {
        var size = control.Size;
        if (size.X < 1f || size.Y < 1f)
            size = control.GetRect().Size;

        rect = control.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, size);
        return rect.Size.X >= 1f && rect.Size.Y >= 1f;
    }

    private static void Rescan(Node shopRoot)
    {
        var searchRoot = ResolveSearchRoot(shopRoot);
        var cards = new List<Control>();
        var numbered = new List<Control>();
        var removal = new List<Control>();
        var merchantButtons = new List<Control>();
        var characters = new List<Control>();
        bool inventoryOpen = ShopInventoryQuery.IsInventoryOpen(searchRoot);

        if (!inventoryOpen)
        {
            foreach (var button in NodeQuery.FindAllSortedByPosition<NMerchantButton>(searchRoot))
                TryAddUnique(merchantButtons, button);
        }
        else
        {
            foreach (var slot in NodeQuery.FindAllSortedByPosition<NMerchantSlot>(searchRoot))
            {
                if (!IsSelectable(slot))
                    continue;

                if (ShopSelectionService.SlotContains<NMerchantCardRemoval>(slot))
                    TryAddUnique(removal, slot);
                else if (ShopSelectionService.SlotContains<NMerchantCard>(slot))
                    TryAddUnique(cards, slot);
                else if (ShopSelectionService.SlotContains<NMerchantRelic>(slot)
                         || ShopSelectionService.SlotContains<NMerchantPotion>(slot))
                    TryAddUnique(numbered, slot);
            }

            numbered.Sort((a, b) =>
            {
                int cmp = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
                return cmp != 0 ? cmp : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            });
        }

        CollectCharacterControls(searchRoot, characters);

        _cardControls = cards;
        _numberedControls = numbered;
        _removalControls = removal;
        _merchantButtonControls = merchantButtons;
        _characterControls = characters;
        _proceedButton = FindProceedButton(searchRoot);
    }

    private static Node ResolveSearchRoot(Node shopRoot)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        return tree?.Root ?? shopRoot;
    }

    private static NProceedButton? FindProceedButton(Node _)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return null;

        foreach (var button in NodeQuery.FindAll<NProceedButton>(tree.Root))
        {
            if (IsSelectable(button))
                return button;
        }

        return null;
    }

    private static void CollectCharacterControls(Node searchRoot, List<Control> characters)
    {
        foreach (var character in NodeQuery.FindAll<NMerchantCharacter>(searchRoot))
        {
            if (!NodeQuery.IsLive(character))
                continue;

            var clickTarget = ResolveCharacterClickTarget(character);
            if (clickTarget != null)
                TryAddUnique(characters, clickTarget);
        }
    }

    private static Control? ResolveCharacterClickTarget(NMerchantCharacter character)
    {
        foreach (var clickable in NodeQuery.FindAll<NClickableControl>(character))
        {
            if (IsSelectable(clickable))
                return clickable;
        }

        Control? best = null;
        float bestArea = 0f;
        foreach (var control in NodeQuery.FindAll<Control>(character))
        {
            if (!IsSelectable(control))
                continue;

            var size = control.GetGlobalRect().Size;
            float area = size.X * size.Y;
            if (area > bestArea)
            {
                bestArea = area;
                best = control;
            }
        }

        return best;
    }

    private static void SyncNumberButtons()
    {
        if (_numberedControls == null)
            return;

        EnsureCanvas();
        if (_root == null)
            return;

        _root.Visible = true;

        while (_numberButtons.Count < _numberedControls.Count)
        {
            var button = OverlayButtonFactory.CreateMenuButton(
                $"ShopPick{_numberButtons.Count + 1}",
                (_numberButtons.Count + 1).ToString(),
                NumberSize,
                new Color(0.10f, 0.08f, 0.02f, 0.95f),
                new Color(1f, 0.82f, 0.30f, 1f),
                () => { });
            button.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(button);
            _numberButtons.Add(button);
        }

        for (int i = 0; i < _numberButtons.Count; i++)
        {
            var button = _numberButtons[i];
            if (button == null || !NodeQuery.IsLive(button))
                continue;

            if (i >= _numberedControls.Count
                || !IsSelectable(_numberedControls[i])
                || !ControlHitboxService.TryGetDwellRect(_numberedControls[i], out var wareRect))
            {
                button.Visible = false;
                continue;
            }

            OverlayButtonFactory.ApplySize(button, NumberSize);

            float x = wareRect.Position.X - NumberSize - NumberGap;
            if (x < ScreenMargin)
                x = wareRect.End.X + NumberGap;

            float y = wareRect.GetCenter().Y - (NumberSize / 2f);
            button.GlobalPosition = new Vector2(x, y);
            button.Visible = true;
        }
    }

    private static void HideNumberButtons()
    {
        foreach (var button in _numberButtons)
        {
            if (button != null && NodeQuery.IsLive(button))
                button.Visible = false;
        }

        if (_root != null && NodeQuery.IsLive(_root))
            _root.Visible = false;
    }

    private static void EnsureCanvas()
    {
        if (_layer != null && NodeQuery.IsLive(_layer) && _root != null && NodeQuery.IsLive(_root))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        _layer = new CanvasLayer { Layer = CanvasLayerOrder, Name = "DwellShopLayer" };
        tree.Root.AddChild(_layer);

        _root = new Control
        {
            Name = "DwellShopRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(_root);

        _numberButtons.Clear();
    }

    private static bool TryGetProceedRect(out Rect2 rect)
    {
        rect = default;
        if (_proceedButton == null || !IsSelectable(_proceedButton))
            return false;

        rect = _proceedButton.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(ProceedHitboxPadding);
        return true;
    }

    private static void ActivateProceed()
    {
        if (_proceedButton != null && InputForwardService.TryActivateControl(_proceedButton))
        {
            ModLogger.Info($"Shop proceed '{_proceedButton.Name}' via ForceClick.");
            return;
        }

        InputForwardService.PressAcceptKey();
        ModLogger.Info("Shop proceed via E accept key.");
    }

    private static void ActivateCharacter(Control control) => ActivateWare(control, 0);

    private static void ActivateWare(Control control, int slot) =>
        ShopSelectionService.TryPurchase(control, slot);

    private static bool IsSelectable(Control control) =>
        NodeQuery.IsLive(control)
        && NodeQuery.IsVisible(control)
        && control is not NClickableControl { IsEnabled: false };

    private static void TryAddUnique(List<Control> list, Control control)
    {
        if (!IsSelectable(control) || list.Contains(control))
            return;

        list.Add(control);
    }
}
