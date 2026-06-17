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

    // Brief drop-outs (head-mouse wobble, a button's hover/pulse animation flickering the cursor in
    // and out of its rect) must not wipe accumulated dwell progress. Hold progress for this long
    // off-target before giving up.
    private const float GraceSeconds = 0.35f;

    // When a selection screen first opens, ignore dwell until BOTH a short delay has passed AND the
    // cursor has actually moved — so a stationary cursor that happens to sit over a card/item can't
    // instantly trigger a pick.
    private const float RearmMoveThreshold = 20f;

    private static string? _activeName;
    private static float _dwellSeconds;
    private static float _graceSeconds;
    private static bool _firedThisVisit;

    private static long _suppressUntilTick;
    private static bool _needsRearmMove;
    private static Vector2 _rearmAnchor;

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
        _graceSeconds = 0f;
        _firedThisVisit = false;
    }

    /// <summary>
    /// Call when a selection screen appears: blocks dwell activation for <paramref name="suppressSeconds"/>
    /// and then until the cursor moves, preventing an accidental instant pick.
    /// </summary>
    internal static void ArmGrace(float suppressSeconds)
    {
        _suppressUntilTick = System.Environment.TickCount64 + (long)(suppressSeconds * 1000f);
        _needsRearmMove = true;
        _rearmAnchor = GetMouseGlobalPosition() ?? Vector2.Zero;
        Reset();
    }

    private static bool IsArmed(Vector2 mouse)
    {
        if (System.Environment.TickCount64 < _suppressUntilTick)
            return false;

        if (_needsRearmMove)
        {
            if (mouse.DistanceTo(_rearmAnchor) < RearmMoveThreshold)
                return false;

            _needsRearmMove = false;
        }

        return true;
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

        if (!IsArmed(mouse.Value))
        {
            // Screen just opened and the cursor hasn't moved yet — keep the dwell from charging.
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
            // Off every target. If we had unfired progress on a target, keep it briefly so a
            // momentary wobble/animation flicker doesn't restart the dwell from zero.
            if (_activeName != null && !_firedThisVisit)
            {
                _graceSeconds += (float)deltaSeconds;
                if (_graceSeconds <= GraceSeconds)
                    return;
            }

            Reset();
            return;
        }

        if (_activeName != hit.Value.Name)
        {
            _activeName = hit.Value.Name;
            _dwellSeconds = 0f;
            _firedThisVisit = false;
        }

        _graceSeconds = 0f;
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

    /// <summary>Mouse position in the same coordinate space dwell hit-testing uses (for diagnostics).</summary>
    internal static Vector2? GetMousePosition() => GetMouseGlobalPosition();

    private static Vector2? GetMouseGlobalPosition()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root is Window window)
            return window.GetMousePosition();

        return tree?.Root?.GetViewport()?.GetMousePosition();
    }
}
