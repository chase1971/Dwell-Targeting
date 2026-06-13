using Godot;

namespace DwellTargeting;

/// <summary>
/// Fires button actions after the cursor dwells on a target for half a second.
/// </summary>
internal static class DwellHoverService
{
    private const float DwellSeconds = 0.5f;

    internal readonly struct Target
    {
        internal Target(Rect2 bounds, Action activate, string name)
        {
            Bounds = bounds;
            Activate = activate;
            Name = name;
        }

        internal Rect2 Bounds { get; }
        internal Action Activate { get; }
        internal string Name { get; }
    }

    private static string? _activeName;
    private static float _dwellSeconds;
    private static bool _firedThisVisit;

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
        if (_firedThisVisit || _dwellSeconds < DwellSeconds)
            return;

        _firedThisVisit = true;
        ModLogger.Info($"Dwell activate '{hit.Value.Name}' after {_dwellSeconds:F2}s");
        hit.Value.Activate();
    }

    private static Vector2? GetMouseGlobalPosition()
    {
        var viewport = (Engine.GetMainLoop() as SceneTree)?.Root?.GetViewport();
        return viewport?.GetMousePosition();
    }
}
