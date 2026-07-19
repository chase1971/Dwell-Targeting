using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Direct dwell on title-menu buttons — no offset numbers. When a blocking submenu dialog
/// (e.g. Quit Game Yes/No) is open, menu targets are replaced with dialog confirm buttons only.
/// Scans once when the title menu appears; rescan only after a menu button is activated (dialog/submenu).
/// </summary>
internal static class MainMenuOverlay
{
    private static NMainMenu? _cachedMenu;
    private static List<(Rect2 Bounds, Control Button, int Slot)>? _dwellTargets;
    private static ulong _cachedMenuId;
    private static bool _needsRescan = true;
    private static bool _inDialogMode;

    internal static bool IsActive()
    {
        if (RunManager.Instance.IsInProgress)
            return false;

        return _cachedMenu != null
            && NodeQuery.IsLive(_cachedMenu)
            && NodeQuery.IsVisible(_cachedMenu);
    }

    internal static bool IsDialogOpen() => _inDialogMode;

    internal static void InvalidateCache()
    {
        _needsRescan = true;
        _dwellTargets = null;
    }

    internal static void Sync()
    {
        if (RunManager.Instance.IsInProgress)
        {
            Hide();
            return;
        }

        if (!EnsureMenuCached())
        {
            Hide();
            return;
        }

        LegacyOverlayCleanup.RemoveMainMenuCanvas();

        ulong menuId = _cachedMenu!.GetInstanceId();
        if (_dwellTargets is { Count: > 0 } && _cachedMenuId == menuId && !_needsRescan)
            return;

        _needsRescan = false;
        _cachedMenuId = menuId;

        if (TryFindBlockingDialogButtons(_cachedMenu, out var dialogButtons))
            RebuildDialogTargets(dialogButtons);
        else
            RebuildMenuTargets(_cachedMenu);
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_dwellTargets == null)
            return;

        foreach (var (bounds, button, slot) in _dwellTargets)
        {
            if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
                continue;

            var captured = button;
            int capturedSlot = slot;
            string label = ResolveTargetLabel(captured, capturedSlot);
            targets.Add(DwellHoverService.Menu(
                bounds,
                () => Activate(captured, capturedSlot),
                label));
        }
    }

    internal static void Hide()
    {
        _cachedMenu = null;
        _dwellTargets = null;
        _cachedMenuId = 0;
        _needsRescan = true;
        _inDialogMode = false;
        LegacyOverlayCleanup.RemoveMainMenuCanvas();
    }

    private static bool EnsureMenuCached()
    {
        if (_cachedMenu != null && NodeQuery.IsLive(_cachedMenu) && NodeQuery.IsVisible(_cachedMenu))
            return true;

        _cachedMenu = FindMainMenu();
        if (_cachedMenu == null)
            return false;

        _needsRescan = true;
        _dwellTargets = null;
        return true;
    }

    private static string ResolveTargetLabel(Control button, int slot)
    {
        if (!_inDialogMode)
            return $"MainMenu:{slot}";

        if (IsYesButton(button))
            return "MainMenuYes";

        if (IsNoButton(button))
            return "MainMenuNo";

        return slot switch
        {
            1 => "MainMenuNo",
            2 => "MainMenuYes",
            _ => $"MainMenuConfirm:{slot}"
        };
    }

    private static bool IsYesButton(Control button)
    {
        string text = ReadButtonText(button);
        return text.Contains("yes", StringComparison.OrdinalIgnoreCase)
            || button.Name.ToString().Contains("Yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoButton(Control button)
    {
        string text = ReadButtonText(button);
        return text.Contains("no", StringComparison.OrdinalIgnoreCase)
            || button.Name.ToString().Contains("No", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadButtonText(Control button)
    {
        if (button is Button { Text: var text } && !string.IsNullOrWhiteSpace(text))
            return text;

        return button.Name;
    }

    private static void RebuildMenuTargets(NMainMenu menu)
    {
        _inDialogMode = false;

        if (menu == null)
        {
            _dwellTargets = null;
            return;
        }

        var buttons = FindMenuButtons(menu);
        _dwellTargets = BuildTargetList(buttons, dialogMode: false);
    }

    private static void RebuildDialogTargets(List<Control> dialogButtons)
    {
        _inDialogMode = true;
        _dwellTargets = BuildTargetList(dialogButtons, dialogMode: true);
    }

    private static List<(Rect2, Control, int)>? BuildTargetList(List<Control> buttons, bool dialogMode)
    {
        var targets = new List<(Rect2, Control, int)>();
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!TryGetButtonRect(button, dialogMode, out var rect))
                continue;

            targets.Add((rect, button, i + 1));
        }

        return targets.Count > 0 ? targets : null;
    }

    private static bool TryGetButtonRect(Control button, bool dialogMode, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
            return false;

        rect = button.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        if (dialogMode)
            rect = rect.Grow(6f);

        return true;
    }

    private static NMainMenu? FindMainMenu()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var menu in NodeQuery.FindAll<NMainMenu>(root))
        {
            if (NodeQuery.IsLive(menu) && NodeQuery.IsVisible(menu))
                return menu;
        }

        return null;
    }

    private static bool TryFindBlockingDialogButtons(NMainMenu menu, out List<Control> buttons)
    {
        buttons = new List<Control>();

        foreach (var stack in NodeQuery.FindAll<NMainMenuSubmenuStack>(menu))
        {
            if (!NodeQuery.IsVisible(stack))
                continue;

            CollectDialogButtons(stack, buttons);
        }

        if (buttons.Count < 2)
            CollectDialogButtons(menu, buttons);

        if (buttons.Count < 2)
        {
            buttons.Clear();
            return false;
        }

        SortDialogButtons(buttons);
        return true;
    }

    private static void CollectDialogButtons(Node root, List<Control> list)
    {
        foreach (var yesNo in NodeQuery.FindAll<NPopupYesNoButton>(root))
            TryAddDialogButton(root, list, yesNo);

        foreach (var confirm in NodeQuery.FindAll<NMiscConfirmButton>(root))
            TryAddDialogButton(root, list, confirm);

        foreach (var confirm in NodeQuery.FindAll<NConfirmButton>(root))
            TryAddDialogButton(root, list, confirm);
    }

    private static void TryAddDialogButton(Node searchRoot, List<Control> list, Control control)
    {
        if (!NodeQuery.IsVisible(control))
            return;

        if (control is NClickableControl { IsEnabled: false })
            return;

        if (!IsInsideVisibleDialogContainer(searchRoot, control))
            return;

        TryAddButton(list, control);
    }

    private static bool IsInsideVisibleDialogContainer(Node searchRoot, Control control)
    {
        for (var node = control.GetParent(); node != null; node = node.GetParent())
        {
            if (node == searchRoot)
                return true;

            if (node is CanvasItem canvas && NodeQuery.IsVisible(canvas))
            {
                string typeName = node.GetType().Name;
                if (typeName.Contains("Popup", StringComparison.Ordinal)
                    || typeName.Contains("Dialog", StringComparison.Ordinal)
                    || typeName.Contains("Submenu", StringComparison.Ordinal)
                    || node is NMainMenuSubmenuStack)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void SortDialogButtons(List<Control> buttons)
    {
        buttons.Sort((a, b) =>
        {
            bool aNo = IsNoButton(a);
            bool bNo = IsNoButton(b);
            if (aNo != bNo)
                return aNo ? -1 : 1;

            bool aYes = IsYesButton(a);
            bool bYes = IsYesButton(b);
            if (aYes != bYes)
                return aYes ? 1 : -1;

            int cmp = a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
            return cmp != 0 ? cmp : a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
        });
    }

    private static List<Control> FindMenuButtons(NMainMenu menu)
    {
        var list = new List<Control>();
        CollectButtons(menu, list);
        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
            return cmp != 0 ? cmp : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
        });
        return list;
    }

    private static void CollectButtons(NMainMenu menu, List<Control> list)
    {
        foreach (var button in NodeQuery.FindAll<NMainMenuTextButton>(menu))
            TryAddMenuButton(menu, list, button);
        foreach (var button in NodeQuery.FindAll<NMainMenuContinueButton>(menu))
            TryAddMenuButton(menu, list, button);
        foreach (var button in NodeQuery.FindAll<NSubmenuButton>(menu))
            TryAddMenuButton(menu, list, button);
        foreach (var button in NodeQuery.FindAll<NShortSubmenuButton>(menu))
            TryAddMenuButton(menu, list, button);
    }

    private static void TryAddMenuButton(NMainMenu menu, List<Control> list, Control control)
    {
        if (IsInsideVisibleSubmenuStack(menu, control))
            return;

        TryAddButton(list, control);
    }

    private static bool IsInsideVisibleSubmenuStack(NMainMenu menu, Control control)
    {
        foreach (var stack in NodeQuery.FindAll<NMainMenuSubmenuStack>(menu))
        {
            if (!NodeQuery.IsVisible(stack))
                continue;

            if (stack.IsAncestorOf(control))
                return true;
        }

        return false;
    }

    private static void TryAddButton(List<Control> list, Control control)
    {
        if (!NodeQuery.IsVisible(control))
            return;
        if (control is NClickableControl { IsEnabled: false })
            return;

        foreach (var existing in list)
        {
            if (existing == control)
                return;
        }

        list.Add(control);
    }

    private static void Activate(Control button, int slot)
    {
        if (!NodeQuery.IsLive(button))
        {
            ModLogger.Warn($"[MainMenu] option {slot} not live.");
            return;
        }

        if (InputForwardService.TryActivateControl(button))
        {
            ModLogger.Info($"[MainMenu] option {slot} '{button.Name}' activated.");
            InvalidateCache();
        }
        else
            ModLogger.Warn($"[MainMenu] option {slot} activation failed.");
    }
}
