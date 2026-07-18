using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// Deck / card-grid views: dwell on the "View Upgrades" tickbox when visible.
/// </summary>
internal static class DeckViewOverlay
{
    private const float ToggleHitboxPadding = 12f;

    private static NDeckViewScreen? _screen;
    private static Control? _viewUpgradesToggle;

    internal static bool IsOpen =>
        _screen != null && NodeQuery.IsLive(_screen) && NodeQuery.IsVisible(_screen);

    internal static void NotifyClosed()
    {
        if (_screen == null)
            return;

        Hide();
        ViewScreenQuery.Invalidate();
    }

    internal static void Sync()
    {
        if (_screen != null)
        {
            if (!NodeQuery.IsLive(_screen) || !NodeQuery.IsVisible(_screen))
            {
                NotifyClosed();
                return;
            }

            if (_viewUpgradesToggle == null)
                _viewUpgradesToggle = FindViewUpgradesToggle(_screen);

            return;
        }

        if (ViewScreenQuery.ConsumeDeckLookupRequest())
        {
            _screen = FindOpenDeckView();
            if (_screen != null)
                _viewUpgradesToggle = FindViewUpgradesToggle(_screen);
        }

        if (_viewUpgradesToggle == null
            && OverlayModeService.TryGetPileSelectScreen(out var pileScreen))
        {
            _viewUpgradesToggle = FindViewUpgradesToggle(pileScreen);
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        TryCollectToggle(_viewUpgradesToggle, targets);

        if (_viewUpgradesToggle != null)
            return;

        if (OverlayModeService.TryGetPileSelectScreen(out var pileScreen))
            TryCollectToggle(FindViewUpgradesToggle(pileScreen), targets);
    }

    internal static void Hide()
    {
        _screen = null;
        _viewUpgradesToggle = null;
    }

    private static void TryCollectToggle(Control? toggle, List<DwellHoverService.Target> targets)
    {
        if (toggle == null || !NodeQuery.IsLive(toggle) || !NodeQuery.IsVisible(toggle))
            return;

        if (toggle is NClickableControl { IsEnabled: false })
            return;

        if (!ControlHitboxService.TryGetDwellRect(toggle, out var rect))
            rect = toggle.GetGlobalRect();

        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return;

        rect = rect.Grow(ToggleHitboxPadding);
        var captured = toggle;
        targets.Add(DwellHoverService.Menu(rect, () => ActivateToggle(captured), "DeckViewUpgrades"));
    }

    private static void ActivateToggle(Control toggle)
    {
        if (!NodeQuery.IsLive(toggle))
            return;

        if (InputForwardService.TryActivateControl(toggle))
            ModLogger.Info($"[DeckView] toggled '{toggle.Name}' ({toggle.GetType().Name}).");
        else
            ModLogger.Warn($"[DeckView] toggle '{toggle.Name}' activation failed.");
    }

    private static NDeckViewScreen? FindOpenDeckView()
    {
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null)
            return null;

        foreach (var screen in NodeQuery.FindAll<NDeckViewScreen>(root))
        {
            if (NodeQuery.IsVisible(screen))
                return screen;
        }

        return null;
    }

    private static Control? FindViewUpgradesToggle(Node root)
    {
        Control? best = null;
        CollectUpgradeToggleCandidates(root, ref best);
        return best;
    }

    private static void CollectUpgradeToggleCandidates(Node node, ref Control? best)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (node is Control control && NodeQuery.IsVisible(control))
        {
            string typeName = node.GetType().Name;
            string nodeName = control.Name.ToString();

            if (IsUpgradePreviewToggle(typeName, nodeName))
            {
                if (best == null || control.GlobalPosition.Y > best.GlobalPosition.Y)
                    best = control;
            }
        }

        try
        {
            foreach (var child in node.GetChildren())
                CollectUpgradeToggleCandidates(child, ref best);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }

    private static bool IsUpgradePreviewToggle(string typeName, string nodeName) =>
        typeName.Contains("UpgradePreview", StringComparison.OrdinalIgnoreCase)
        || nodeName.Contains("Upgrade", StringComparison.OrdinalIgnoreCase)
        || nodeName.Contains("showUpgrades", StringComparison.OrdinalIgnoreCase);
}
