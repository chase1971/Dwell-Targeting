using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// Universal hover-to-activate for the game's Back button (<see cref="NBackButton"/>), which appears
/// on many screens (card / deck / pile views, upgrade &amp; removal screens, shops, chests) and was
/// never wired. We scan (throttled) for a visible+enabled back button and offer it as a dwell target
/// regardless of mode, so any screen with a Back arrow can be dismissed hands-free.
/// </summary>
internal static class BackButtonOverlay
{
    private const int RescanIntervalFrames = 10;

    private static Control? _backButton;
    private static int _framesSinceScan;
    private static long _nextDiagTick;

    internal static void Sync()
    {
        _framesSinceScan++;
        if (_framesSinceScan < RescanIntervalFrames)
            return;

        _framesSinceScan = 0;
        _backButton = FindBackButton();

        long now = System.Environment.TickCount64;
        if (_backButton != null && now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info($"[Back] visible back button '{_backButton.Name}'.");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_backButton == null || !NodeQuery.IsLive(_backButton) || !NodeQuery.IsVisible(_backButton))
            return;

        if (!ControlHitboxService.TryGetDwellRect(_backButton, out var rect))
            return;

        var captured = _backButton;
        targets.Add(DwellHoverService.Menu(rect, () => Activate(captured), "BackButton"));
    }

    internal static void Hide()
    {
        _backButton = null;
        _framesSinceScan = 0;
    }

    private static void Activate(Control button)
    {
        if (!NodeQuery.IsLive(button))
            return;

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"Back button '{button.Name}' activated.");
        else
            ModLogger.Warn($"Back button '{button.Name}' activation failed.");
    }

    private static Control? FindBackButton()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var button in NodeQuery.FindAll<NBackButton>(root))
        {
            if (button is not Control control || !NodeQuery.IsVisible(control))
                continue;
            if (button is NClickableControl { IsEnabled: false })
                continue;

            return control;
        }

        return null;
    }
}
