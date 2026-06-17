using Godot;

namespace DwellTargeting;

/// <summary>
/// Fires button actions after the cursor dwells on a target for a category-specific duration.
/// </summary>
internal static class DwellHoverService
{
    internal readonly struct Target
    {
        internal Target(Rect2 bounds, Action activate, string name, float dwellSeconds, bool useMenuCooldown)
        {
            Bounds = bounds;
            Activate = activate;
            Name = name;
            DwellSeconds = dwellSeconds;
            UseMenuCooldown = useMenuCooldown;
        }

        internal Rect2 Bounds { get; }
        internal Action Activate { get; }
        internal string Name { get; }
        internal float DwellSeconds { get; }
        internal bool UseMenuCooldown { get; }
    }

    private static string? _activeName;
    private static float _dwellSeconds;
    private static bool _firedThisVisit;

    internal static Target Card(Rect2 bounds, Action activate, string name) =>
        new(bounds, activate, name, DwellTiming.CardDwellSeconds, useMenuCooldown: false);

    internal static Target Menu(Rect2 bounds, Action activate, string name) =>
        new(bounds, activate, name, DwellTiming.MenuDwellSeconds, useMenuCooldown: true);

    internal static Target EndTurn(Rect2 bounds, Action activate) =>
        new(bounds, activate, "EndTurn", DwellTiming.EndTurnDwellSeconds, useMenuCooldown: true);

    internal static void Reset()
    {
        _activeName = null;
        _dwellSeconds = 0f;
        _firedThisVisit = false;
    }

    internal static void ProcessFrame(IReadOnlyList<Target> targets, double deltaSeconds)
    {
        if (targets.Count == 0)
        {
            Reset();
            return;
        }

        var mouse = GetMouseGlobalPosition();
        if (mouse == null)
        {
            Reset();
            return;
        }

        Target? hit = null;
        foreach (var target in targets)
        {
            if (target.Bounds.HasPoint(mouse.Value))
            {
                hit = target;
                break;
            }
        }

        if (hit == null)
        {
            Reset();
            return;
        }

        if (_activeName != hit.Value.Name)
        {
            _activeName = hit.Value.Name;
            _dwellSeconds = 0f;
            _firedThisVisit = false;
        }

        _dwellSeconds += (float)deltaSeconds;
        if (_firedThisVisit || _dwellSeconds < hit.Value.DwellSeconds)
            return;

        _firedThisVisit = true;

        if (hit.Value.UseMenuCooldown)
        {
            if (!DwellActivationCooldown.TryRunMenuAction(hit.Value.Activate))
                ModLogger.Info($"Dwell blocked '{hit.Value.Name}' — menu cooldown active.");
            else
                ModLogger.Info($"Dwell activate '{hit.Value.Name}' after {_dwellSeconds:F2}s");
            return;
        }

        ModLogger.Info($"Dwell activate '{hit.Value.Name}' after {_dwellSeconds:F2}s");
        hit.Value.Activate();
    }

    private static Vector2? GetMouseGlobalPosition()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root is Window window)
            return window.GetMousePosition();

        return tree?.Root?.GetViewport()?.GetMousePosition();
    }
}
