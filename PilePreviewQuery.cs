using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// Detects upgrade-preview layout (large centered cards + confirm/back ribbon) on pile screens.
/// </summary>
internal static class PilePreviewQuery
{
    internal static bool HasLargeCenterPreviewCards(Node? searchRoot = null)
    {
        searchRoot ??= (Engine.GetMainLoop() as SceneTree)?.Root;
        if (searchRoot == null)
            return false;

        Rect2 viewport = GetViewportRect();
        float minCenterX = viewport.Size.X * 0.25f;
        float maxCenterX = viewport.Size.X * 0.75f;
        float maxCenterY = viewport.Size.Y * 0.60f;

        foreach (var card in NodeQuery.FindAll<NCard>(searchRoot))
        {
            if (card is not Control control || !NodeQuery.IsVisible(control))
                continue;

            Rect2 rect = control.GetGlobalRect();
            if (rect.Size.X < 120f || rect.Size.Y < 160f)
                continue;

            Vector2 center = rect.GetCenter();
            if (center.X >= minCenterX && center.X <= maxCenterX && center.Y <= maxCenterY)
                return true;
        }

        foreach (var container in NodeQuery.FindAll<NSelectedHandCardContainer>(searchRoot))
        {
            if (container is not Control control || !NodeQuery.IsVisible(control))
                continue;

            Rect2 rect = control.GetGlobalRect();
            if (rect.Size.X >= 80f && rect.Size.Y >= 100f)
                return true;
        }

        return false;
    }

    internal static bool HasVisibleConfirmAndBack(Node? searchRoot = null)
    {
        searchRoot ??= (Engine.GetMainLoop() as SceneTree)?.Root;
        if (searchRoot == null)
            return false;

        return FindVisibleConfirm(searchRoot) != null && FindVisibleBack(searchRoot) != null;
    }

    internal static bool IsUpgradeConfirmFlowActive(Node? searchRoot = null) =>
        HasVisibleConfirmAndBack(searchRoot) && HasLargeCenterPreviewCards(searchRoot);

    internal static Control? FindVisibleConfirm(Node root)
    {
        foreach (var confirm in NodeQuery.FindAll<NConfirmButton>(root))
        {
            if (confirm is Control control && IsUsableControl(control))
                return control;
        }

        foreach (var confirm in NodeQuery.FindAll<NMiscConfirmButton>(root))
        {
            if (confirm is Control control && IsUsableControl(control))
                return control;
        }

        return null;
    }

    internal static Control? FindVisibleBack(Node root)
    {
        foreach (var button in NodeQuery.FindAll<NBackButton>(root))
        {
            if (button is Control control && IsUsableControl(control))
                return control;
        }

        return null;
    }

    private static bool IsUsableControl(Control control)
    {
        if (!NodeQuery.IsLive(control) || !NodeQuery.IsVisible(control))
            return false;

        if (control is NClickableControl { IsEnabled: false })
            return false;

        Rect2 rect = control.GetGlobalRect();
        return rect.Size.X >= 8f && rect.Size.Y >= 8f;
    }

    private static Rect2 GetViewportRect()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        return tree?.Root?.GetViewport()?.GetVisibleRect() ?? new Rect2(0, 0, 1920, 1080);
    }
}
