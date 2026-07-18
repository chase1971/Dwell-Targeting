using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// Universal hover-to-activate for the game's Back button (<see cref="NBackButton"/>), which appears
/// on many screens (card / deck / pile views, upgrade &amp; removal screens, shops, chests) and was
/// never wired. Found once when it first appears; refound only if it disappears.
/// </summary>
internal static class BackButtonOverlay
{
    private const float BackButtonPadding = 10f;

    private static Control? _backButton;

    internal static void Sync()
    {
        if (_backButton != null && NodeQuery.IsLive(_backButton) && NodeQuery.IsVisible(_backButton))
            return;

        _backButton = FindBackButton();
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_backButton == null || !NodeQuery.IsLive(_backButton) || !NodeQuery.IsVisible(_backButton))
            return;

        var rect = _backButton.GetGlobalRect();
        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return;

        rect = rect.Grow(BackButtonPadding);

        var captured = _backButton;
        targets.Add(DwellHoverService.Menu(rect, () => Activate(captured), "BackButton"));
    }

    internal static void Hide()
    {
        _backButton = null;
    }

    private static void Activate(Control button)
    {
        if (!NodeQuery.IsLive(button))
            return;

        ViewScreenQuery.Invalidate();
        DeckViewOverlay.NotifyClosed();

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
