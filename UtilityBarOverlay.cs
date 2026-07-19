using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Dwell-click on native draw/discard/exhaust piles, deck, map, and pause controls (no overlay buttons).
/// Utility rects are measured once per control when first found — Collect never re-reads layout.
/// </summary>
internal static class UtilityBarOverlay
{
    private static bool _active;
    private static NCombatUi? _cachedCombatUi;
    private static NTopBar? _cachedTopBar;
    private static readonly Dictionary<string, Rect2> _cachedRects = new();
    private static readonly Dictionary<string, Control> _cachedControls = new();

    internal static void InvalidateDiscoveryCache()
    {
        _cachedCombatUi = null;
        _cachedTopBar = null;
        _cachedRects.Clear();
        _cachedControls.Clear();
    }

    internal static void Sync(bool visible)
    {
        bool wasActive = _active;
        _active = visible && RunManager.Instance.IsInProgress;
        if (!_active)
        {
            ClearUtilityCache();
            return;
        }

        if (!wasActive)
            ClearUtilityCache();

        EnsureTopBarCached();
        EnsureCombatUiCached();
        WarmUtilityRects();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (!_active)
            return;

        var mode = OverlayModeService.GetMode();
        foreach (var id in GetEnabledIds())
        {
            if (!ShouldIncludeUtility(id, mode))
                continue;

            if (!_cachedRects.TryGetValue(id, out var rect))
                continue;

            if (!_cachedControls.TryGetValue(id, out var control) || !NodeQuery.IsLive(control))
                continue;

            string capturedId = id;
            Control capturedControl = control;
            targets.Add(DwellHoverService.Menu(
                rect,
                () => ActivateNative(capturedId, capturedControl),
                $"NativeUtility:{id}"));
        }
    }

    internal static bool TryActivateAt(Vector2 globalPos, out string message)
    {
        message = string.Empty;
        if (!_active)
            return false;

        var mode = OverlayModeService.GetMode();
        foreach (var id in GetEnabledIds())
        {
            if (!ShouldIncludeUtility(id, mode))
                continue;

            if (!_cachedRects.TryGetValue(id, out var rect) || !rect.HasPoint(globalPos))
                continue;

            if (!_cachedControls.TryGetValue(id, out var control) || !NodeQuery.IsLive(control))
                continue;

            if (!DwellActivationCooldown.TryRunMenuAction(() => ActivateNative(id, control)))
                return false;

            message = $"Native utility {id} clicked";
            return true;
        }

        return false;
    }

    /// <summary>Screen rect for a native top-bar / pile control (map, deck, menu, etc.).</summary>
    internal static bool TryGetAnchorRect(string id, out Rect2 rect)
    {
        rect = default;
        return _cachedRects.TryGetValue(id, out rect) && rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    internal static bool ContainsPoint(Vector2 globalPos)
    {
        if (!_active)
            return false;

        var mode = OverlayModeService.GetMode();
        foreach (var id in GetEnabledIds())
        {
            if (!ShouldIncludeUtility(id, mode))
                continue;

            if (_cachedRects.TryGetValue(id, out var rect) && rect.HasPoint(globalPos))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Draw/discard/exhaust piles are combat-only. Deck, map, and pause menu stay available on
    /// events, shops, rest sites, rewards, etc. (v0.10.58 accidentally blocked all utilities on
    /// those screens).
    /// </summary>
    private static bool ShouldIncludeUtility(string id, OverlayMode mode)
    {
        if (mode == OverlayMode.None)
            return false;

        if (id is "draw" or "discard" or "exhaust")
            return mode is OverlayMode.CombatPlay or OverlayMode.HandSelect;

        return true;
    }

    internal static void Hide()
    {
        _active = false;
        ClearUtilityCache();
    }

    private static void ClearUtilityCache()
    {
        InvalidateDiscoveryCache();
        _cachedRects.Clear();
        _cachedControls.Clear();
    }

    private static IEnumerable<string> GetEnabledIds()
    {
        var settings = SettingsStore.Current;
        if (settings.ShowDrawPileButton)
            yield return "draw";
        if (settings.ShowDiscardPileButton)
            yield return "discard";
        if (settings.ShowDeckButton)
            yield return "deck";
        if (settings.ShowExhaustPileButton)
            yield return "exhaust";
        if (settings.ShowMapButton)
            yield return "map";
        if (settings.ShowMenuButton)
            yield return "menu";
    }

    private static void WarmUtilityRects()
    {
        foreach (var id in GetEnabledIds())
        {
            if (_cachedControls.TryGetValue(id, out var existing)
                && NodeQuery.IsLive(existing)
                && _cachedRects.ContainsKey(id))
            {
                continue;
            }

            _cachedRects.Remove(id);
            _cachedControls.Remove(id);

            if (!TryGetNativeControl(id, out var control))
                continue;

            var rect = control.GetGlobalRect();
            if (rect.Size.X < 8f || rect.Size.Y < 8f)
                continue;

            rect = rect.Grow(6f);

            _cachedControls[id] = control;
            _cachedRects[id] = rect;
        }
    }

    private static void EnsureTopBarCached()
    {
        if (_cachedTopBar != null && NodeQuery.IsLive(_cachedTopBar))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        foreach (var bar in NodeQuery.FindAll<NTopBar>(tree.Root))
        {
            if (!NodeQuery.IsVisible(bar))
                continue;

            _cachedTopBar = bar;
            ModLogger.Info("Utility bar: cached NTopBar for run.");
            return;
        }
    }

    private static void EnsureCombatUiCached()
    {
        if (_cachedCombatUi != null && NodeQuery.IsLive(_cachedCombatUi))
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        foreach (var ui in NodeQuery.FindAll<NCombatUi>(tree.Root))
        {
            if (!NodeQuery.IsVisible(ui))
                continue;

            _cachedCombatUi = ui;
            ModLogger.Info("Utility bar: cached NCombatUi for combat.");
            return;
        }
    }

    private static bool TryGetNativeControl(string id, out Control control)
    {
        control = null!;
        var mode = OverlayModeService.GetMode();
        if (id is "draw" or "discard" or "exhaust")
        {
            if (mode is not (OverlayMode.CombatPlay or OverlayMode.HandSelect))
                return false;

            EnsureCombatUiCached();
        }
        else
        {
            EnsureTopBarCached();
        }

        Control? found = id switch
        {
            "draw" => _cachedCombatUi?.DrawPile as Control,
            "discard" => _cachedCombatUi?.DiscardPile as Control,
            "exhaust" => _cachedCombatUi?.ExhaustPile as Control,
            "deck" => _cachedTopBar?.Deck as Control,
            "map" => _cachedTopBar?.Map as Control,
            "menu" => _cachedTopBar?.Pause as Control,
            _ => null
        };

        if (found == null || !NodeQuery.IsLive(found) || !NodeQuery.IsVisible(found))
            return false;

        if (found is NClickableControl { IsEnabled: false })
            return false;

        control = found;
        return true;
    }

    private static void ActivateNative(string id, Control control)
    {
        ModLogger.Info($"Native utility '{id}' -> {control.Name}");
        if (id is "deck" or "draw" or "discard" or "exhaust")
            ViewScreenQuery.RequestScan();

        if (control is NClickableControl clickable)
        {
            clickable.ForceClick();
            return;
        }

        PressFallbackKey(id);
    }

    private static void PressFallbackKey(string id)
    {
        Key key = id switch
        {
            "draw" => Key.A,
            "discard" => Key.S,
            "deck" => Key.D,
            "exhaust" => Key.X,
            "map" => Key.M,
            "menu" => Key.Escape,
            _ => Key.None
        };

        if (key == Key.None)
            return;

        ModLogger.Info($"Native utility '{id}' fallback key {key}");
        InputForwardService.PressKey(key);
    }
}
