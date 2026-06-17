using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;

namespace DwellTargeting;

/// <summary>
/// Dwell-click on native draw/discard/exhaust piles, deck, map, and pause controls (no overlay buttons).
/// </summary>
internal static class UtilityBarOverlay
{
    private const int RescanIntervalFrames = 20;

    private static bool _active;
    private static NCombatUi? _cachedCombatUi;
    private static NTopBar? _cachedTopBar;
    private static int _framesSinceScan;

    internal static void Sync(bool visible)
    {
        _active = visible && RunManager.Instance.IsInProgress;
        if (!_active)
        {
            _cachedCombatUi = null;
            _cachedTopBar = null;
            _framesSinceScan = RescanIntervalFrames;
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (!_active)
            return;

        foreach (var id in GetEnabledIds())
        {
            if (!TryGetNativeControl(id, out var control))
                continue;

            var rect = control.GetGlobalRect();
            if (rect.Size.X < 8f || rect.Size.Y < 8f)
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

        foreach (var id in GetEnabledIds())
        {
            if (!TryGetNativeControl(id, out var control))
                continue;

            if (!control.GetGlobalRect().HasPoint(globalPos))
                continue;

            if (!DwellActivationCooldown.TryRunMenuAction(() => ActivateNative(id, control)))
                return false;

            message = $"Native utility {id} clicked";
            return true;
        }

        return false;
    }

    internal static bool ContainsPoint(Vector2 globalPos)
    {
        if (!_active)
            return false;

        foreach (var id in GetEnabledIds())
        {
            if (!TryGetNativeControl(id, out var control))
                continue;

            if (control.GetGlobalRect().HasPoint(globalPos))
                return true;
        }

        return false;
    }

    internal static void Hide()
    {
        _active = false;
        _cachedCombatUi = null;
        _cachedTopBar = null;
        _framesSinceScan = RescanIntervalFrames;
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

    private static void RefreshCacheIfNeeded()
    {
        _framesSinceScan++;
        if (_framesSinceScan < RescanIntervalFrames
            && _cachedTopBar != null
            && NodeQuery.IsLive(_cachedTopBar))
        {
            return;
        }

        _framesSinceScan = 0;
        _cachedCombatUi = null;
        _cachedTopBar = null;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
            return;

        foreach (var ui in NodeQuery.FindAll<NCombatUi>(tree.Root))
        {
            if (!NodeQuery.IsVisible(ui))
                continue;

            _cachedCombatUi = ui;
            break;
        }

        foreach (var bar in NodeQuery.FindAll<NTopBar>(tree.Root))
        {
            if (!NodeQuery.IsVisible(bar))
                continue;

            _cachedTopBar = bar;
            break;
        }
    }

    private static bool TryGetNativeControl(string id, out Control control)
    {
        control = null!;
        RefreshCacheIfNeeded();

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
