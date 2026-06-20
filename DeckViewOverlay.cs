using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

/// <summary>
/// Deck / card-grid views opened from the top bar: left-side hover scroll plus dwell on the
/// "View Upgrades" tickbox.
/// </summary>
internal static class DeckViewOverlay
{
    private const int RescanIntervalFrames = 10;
    private const float ToggleHitboxPadding = 12f;

    private static NDeckViewScreen? _screen;
    private static Control? _viewUpgradesToggle;
    private static int _framesSinceScan;
    private static long _nextDiagTick;

    internal static void Sync()
    {
        _screen = FindOpenDeckView();
        if (_screen == null)
        {
            Hide();
            return;
        }

        HoverScrollStripOverlay.Sync("Deck");

        _framesSinceScan++;
        if (_viewUpgradesToggle == null || _framesSinceScan >= RescanIntervalFrames)
        {
            _framesSinceScan = 0;
            _viewUpgradesToggle = FindViewUpgradesToggle(_screen);
        }

        long now = System.Environment.TickCount64;
        if (now >= _nextDiagTick)
        {
            _nextDiagTick = now + 2000;
            ModLogger.Info(
                $"[DeckView] sync toggle={(_viewUpgradesToggle != null ? _viewUpgradesToggle.GetType().Name : "null")}.");
        }
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_screen == null || !NodeQuery.IsLive(_screen))
            return;

        if (_viewUpgradesToggle == null || !NodeQuery.IsLive(_viewUpgradesToggle) || !NodeQuery.IsVisible(_viewUpgradesToggle))
            return;

        if (_viewUpgradesToggle is NClickableControl { IsEnabled: false })
            return;

        if (!ControlHitboxService.TryGetDwellRect(_viewUpgradesToggle, out var rect))
            rect = _viewUpgradesToggle.GetGlobalRect();

        if (rect.Size.X < 8f || rect.Size.Y < 8f)
            return;

        rect = rect.Grow(ToggleHitboxPadding);
        var captured = _viewUpgradesToggle;
        targets.Add(DwellHoverService.Menu(rect, () => ActivateToggle(captured), "DeckViewUpgrades"));
    }

    internal static void Hide()
    {
        _screen = null;
        _viewUpgradesToggle = null;
        _framesSinceScan = 0;
        HoverScrollStripOverlay.Hide();
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
