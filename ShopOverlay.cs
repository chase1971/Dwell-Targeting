using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace DwellTargeting;

/// <summary>
/// Hover-to-buy for regular merchants (<see cref="NMerchantRoom"/>) and the fake/Timu merchant
/// (<see cref="Events.Custom.NFakeMerchant"/>). Covers wares, the merchant character, and Proceed.
/// </summary>
internal static class ShopOverlay
{
    private const float ProceedHitboxPadding = 24f;
    private const int RescanIntervalFrames = 10;

    private static Node? _shopRoot;
    private static List<Control>? _wareControls;
    private static List<Control>? _characterControls;
    private static NProceedButton? _proceedButton;
    private static int _framesSinceScan;
    private static long _nextDiagTick;

    internal static void Sync()
    {
        _shopRoot = OverlayModeService.GetCachedShopNode();
        if (_shopRoot == null)
        {
            Hide();
            return;
        }

        _framesSinceScan++;
        if (_wareControls == null || _characterControls == null || _framesSinceScan >= RescanIntervalFrames)
        {
            _framesSinceScan = 0;
            Rescan(_shopRoot);
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info(
                $"[Shop] {_shopRoot.GetType().Name} wares={_wareControls?.Count ?? -1} " +
                $"characters={_characterControls?.Count ?? -1} proceed={(_proceedButton != null)}");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_shopRoot == null)
            return;

        int slot = 1;
        if (_characterControls != null)
        {
            foreach (var control in _characterControls)
            {
                if (!IsSelectable(control))
                    continue;

                if (ControlHitboxService.TryGetDwellRect(control, out var rect))
                {
                    var captured = control;
                    targets.Add(DwellHoverService.Menu(
                        rect,
                        () => ActivateCharacter(captured),
                        $"ShopCharacter:{slot}"));
                }

                slot++;
            }
        }

        slot = 1;
        if (_wareControls != null)
        {
            foreach (var control in _wareControls)
            {
                if (!IsSelectable(control))
                    continue;

                if (ControlHitboxService.TryGetDwellRect(control, out var rect))
                {
                    var captured = control;
                    int capturedSlot = slot;
                    targets.Add(DwellHoverService.Menu(
                        rect,
                        () => ActivateWare(captured, capturedSlot),
                        $"ShopWare:{slot}"));
                }

                slot++;
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
        _wareControls = null;
        _characterControls = null;
        _proceedButton = null;
        _framesSinceScan = 0;
    }

    internal static bool TryRouteClick(Vector2 globalPos, out string message)
    {
        message = string.Empty;

        if (_characterControls != null)
        {
            foreach (var control in _characterControls)
            {
                if (!IsSelectable(control))
                    continue;
                if (!ControlHitboxService.TryGetDwellRect(control, out var rect) || !rect.HasPoint(globalPos))
                    continue;

                ActivateCharacter(control);
                message = "Shop character clicked";
                return true;
            }
        }

        if (_wareControls != null)
        {
            foreach (var control in _wareControls)
            {
                if (!IsSelectable(control))
                    continue;
                if (!ControlHitboxService.TryGetDwellRect(control, out var rect) || !rect.HasPoint(globalPos))
                    continue;

                ActivateWare(control, 0);
                message = "Shop ware clicked";
                return true;
            }
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

    private static void Rescan(Node shopRoot)
    {
        var searchRoot = ResolveSearchRoot(shopRoot);
        var wares = new List<Control>();
        var characters = new List<Control>();

        foreach (var button in NodeQuery.FindAllSortedByPosition<NMerchantButton>(searchRoot))
            TryAddUnique(wares, button);

        foreach (var card in NodeQuery.FindAllSortedByPosition<NMerchantCard>(searchRoot))
            TryAddUnique(wares, card);

        foreach (var relic in NodeQuery.FindAllSortedByPosition<NMerchantRelic>(searchRoot))
            TryAddUnique(wares, relic);

        foreach (var potion in NodeQuery.FindAllSortedByPosition<NMerchantPotion>(searchRoot))
            TryAddUnique(wares, potion);

        foreach (var removal in NodeQuery.FindAllSortedByPosition<NMerchantCardRemoval>(searchRoot))
            TryAddUnique(wares, removal);

        foreach (var slot in NodeQuery.FindAllSortedByPosition<NMerchantSlot>(searchRoot))
            TryAddUnique(wares, slot);

        CollectCharacterControls(searchRoot, characters);

        _wareControls = wares;
        _characterControls = characters;
        _proceedButton = FindProceedButton(searchRoot);
    }

    /// <summary>Proceed and inventory often live above the room node — search the whole scene.</summary>
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
            if (!IsSelectable(button))
                continue;

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

    /// <summary>NMerchantCharacter is not a Control — resolve its clickable child for dwell/ForceClick.</summary>
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

    private static void ActivateCharacter(Control control)
    {
        if (!NodeQuery.IsLive(control))
            return;

        if (InputForwardService.TryActivateControl(control))
            ModLogger.Info($"Shop character '{control.Name}' activated.");
        else
            ModLogger.Warn($"Shop character '{control.Name}' activation failed.");
    }

    private static void ActivateWare(Control control, int slot)
    {
        if (!NodeQuery.IsLive(control))
            return;

        if (InputForwardService.TryActivateControl(control))
            ModLogger.Info($"Shop ware #{slot} '{control.Name}' activated.");
        else
            ModLogger.Warn($"Shop ware #{slot} '{control.Name}' activation failed.");
    }

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
