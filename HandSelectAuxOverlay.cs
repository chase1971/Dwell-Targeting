using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// Peek (battlefield) and other auxiliary hand-select controls besides card picks and confirm.
/// </summary>
internal static class HandSelectAuxOverlay
{
    private const float ButtonPadding = 10f;
    private const long LookupRetryMs = 500;

    private static Control? _peekButton;
    private static long _lastLookupTick;

    internal static void Sync()
    {
        if (OverlayModeService.GetMode() != OverlayMode.HandSelect)
        {
            _peekButton = null;
            return;
        }

        if (_peekButton != null && NodeQuery.IsLive(_peekButton) && NodeQuery.IsVisible(_peekButton))
            return;

        long now = System.Environment.TickCount64;
        if (now - _lastLookupTick < LookupRetryMs)
            return;

        _lastLookupTick = now;
        _peekButton = FindPeekButton();
    }

    internal static void InvalidateLookup()
    {
        _peekButton = null;
        _lastLookupTick = 0;
    }

    internal static void CollectDwellTargets(List<DwellHoverService.Target> targets)
    {
        if (_peekButton == null || !NodeQuery.IsLive(_peekButton) || !NodeQuery.IsVisible(_peekButton))
            return;

        if (!CardPickTargetQuery.TryGetDwellRect(_peekButton, out var rect, ButtonPadding))
            return;

        var captured = _peekButton;
        targets.Add(DwellHoverService.Menu(
            rect,
            () => Activate(captured, "Peek"),
            "HandSelectPeek"));
    }

    internal static void Hide()
    {
        InvalidateLookup();
    }

    private static void Activate(Control button, string label)
    {
        if (!NodeQuery.IsLive(button))
            return;

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"[HandSelect] {label} '{button.Name}' activated.");
        else
            ModLogger.Warn($"[HandSelect] {label} '{button.Name}' activation failed.");
    }

    private static Control? FindPeekButton()
    {
        var hand = NPlayerHand.Instance;
        if (hand != null && NodeQuery.IsLive(hand))
        {
            var fromHand = FindPeekUnder(hand);
            if (fromHand != null)
                return fromHand;
        }

        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        return root == null ? null : FindPeekUnder(root);
    }

    private static Control? FindPeekUnder(Node root)
    {
        var list = new List<Control>();
        AddControlsByTypeName(root, list, "NPeekButton");
        foreach (var control in list)
        {
            if (NodeQuery.IsVisible(control))
                return control;
        }

        return null;
    }

    private static void AddControlsByTypeName(Node node, List<Control> list, string typeName)
    {
        if (!NodeQuery.IsLive(node))
            return;

        if (node is Control control
            && node.GetType().Name == typeName
            && control is not NClickableControl { IsEnabled: false }
            && NodeQuery.IsVisible(control)
            && !list.Contains(control))
        {
            list.Add(control);
        }

        try
        {
            foreach (var child in node.GetChildren())
                AddControlsByTypeName(child, list, typeName);
        }
        catch
        {
            /* disposed mid-walk */
        }
    }
}
