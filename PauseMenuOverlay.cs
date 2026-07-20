using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;

namespace DwellTargeting;

/// <summary>
/// Direct dwell on pause-menu buttons (Resume, Settings, etc.) — no offset numbers.
/// </summary>
internal static class PauseMenuOverlay
{
    private const long LookupRescanMs = 500;

    private const float MenuButtonPadding = 8f;
    private const float BackButtonPadding = 10f;

    private static List<(Rect2 Bounds, Control Button, int Slot)>? _dwellTargets;
    private static bool _wasOpen;
    private static NPauseMenu? _cachedPauseMenu;
    private static bool _lookupCached;
    private static long _lastLookupTick;

    internal static void InvalidateLookupCache()
    {
        _lookupCached = false;
        _lastLookupTick = 0;
        _cachedPauseMenu = null;
    }

    internal static bool IsOpen()
    {
        if (_cachedPauseMenu != null && NodeQuery.IsLive(_cachedPauseMenu))
            return NodeQuery.IsVisible(_cachedPauseMenu);

        long now = System.Environment.TickCount64;
        if (_lookupCached && now - _lastLookupTick < LookupRescanMs)
            return _cachedPauseMenu != null && NodeQuery.IsVisible(_cachedPauseMenu);

        // Cache the negative result too — otherwise the common "no pause menu open" case re-walks the
        // entire tree every single frame during normal play.
        _cachedPauseMenu = FindPauseMenu();
        _lookupCached = true;
        _lastLookupTick = now;

        return _cachedPauseMenu != null && NodeQuery.IsVisible(_cachedPauseMenu);
    }

    internal static void Sync()
    {
        bool open = IsOpen();
        if (!open)
        {
            _wasOpen = false;
            Hide();
            return;
        }

        LegacyOverlayCleanup.RemovePauseMenuCanvas();

        if (_wasOpen && _dwellTargets is { Count: > 0 })
            return;

        _wasOpen = true;
        RebuildTargets();
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
            targets.Add(DwellHoverService.Menu(
                bounds,
                () => Activate(captured, capturedSlot),
                $"PauseMenu:{capturedSlot}"));
        }
    }

    internal static void Hide()
    {
        _dwellTargets = null;
        _wasOpen = false;
        _cachedPauseMenu = null;
        _lookupCached = false;
        _lastLookupTick = 0;
        LegacyOverlayCleanup.RemovePauseMenuCanvas();
    }

    private static void RebuildTargets()
    {
        var menu = _cachedPauseMenu ?? FindPauseMenu();
        if (menu == null)
        {
            _dwellTargets = null;
            return;
        }

        var buttons = FindMenuButtons(menu);
        var targets = new List<(Rect2, Control, int)>();
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            if (!TryMeasureMenuButtonRect(button, out var rect))
                continue;

            targets.Add((rect, button, i + 1));
        }

        _dwellTargets = targets.Count > 0 ? targets : null;
    }

    private static bool TryMeasureMenuButtonRect(Control button, out Rect2 rect)
    {
        rect = default;
        if (!NodeQuery.IsLive(button) || !NodeQuery.IsVisible(button))
            return false;

        rect = button.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return false;

        rect = rect.Grow(MenuButtonPadding);
        return true;
    }

    private static NPauseMenu? FindPauseMenu()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var menu in NodeQuery.FindAllVisible<NPauseMenu>(root))
        {
            if (NodeQuery.IsLive(menu) && NodeQuery.IsVisible(menu))
                return menu;
        }

        return null;
    }

    private static List<Control> FindMenuButtons(NPauseMenu menu)
    {
        var list = new List<Control>();
        foreach (var button in NodeQuery.FindAll<NPauseMenuButton>(menu))
        {
            if (button is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;

            list.Add(control);
        }

        list.Sort((a, b) =>
        {
            int cmp = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
            return cmp != 0 ? cmp : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
        });

        return list;
    }

    private static void Activate(Control button, int slot)
    {
        if (!NodeQuery.IsLive(button))
        {
            ModLogger.Warn($"[Pause] option {slot} not live.");
            return;
        }

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"[Pause] option {slot} '{button.Name}' activated.");
        else
            ModLogger.Warn($"[Pause] option {slot} activation failed.");
    }
}
