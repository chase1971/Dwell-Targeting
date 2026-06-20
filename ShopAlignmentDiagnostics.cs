using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace DwellTargeting;

/// <summary>
/// One-stop coordinate dump for diagnosing shop hitbox misalignment. Logs, for the first shop card, every
/// candidate rectangle (slot layout rect, NCard layout rect, NCard canvas-transform rect) alongside the live
/// mouse position and the viewport canvas transform — so we can see which coordinate space the card lives in.
/// Throttled; only runs while the hitbox overlay (debug aid) is enabled.
/// </summary>
internal static class ShopAlignmentDiagnostics
{
    private static long _nextTick;

    internal static void Log(Control slot)
    {
        if (!SettingsStore.Current.ShowHitboxOverlay)
            return;

        long now = System.Environment.TickCount64;
        if (now < _nextTick)
            return;
        _nextTick = now + 1000;

        var slotRect = slot.GetGlobalRect();
        ModLogger.Info(
            $"[ShopAlign] slot='{slot.Name}' " +
            $"slotGlobalRect=({slotRect.Position.X:F0},{slotRect.Position.Y:F0} {slotRect.Size.X:F0}x{slotRect.Size.Y:F0})");

        Control? card = null;
        foreach (var c in NodeQuery.FindAll<NCard>(slot))
        {
            if (c is Control cc && NodeQuery.IsVisible(cc))
            {
                card = cc;
                break;
            }
        }

        if (card == null)
        {
            ModLogger.Info("[ShopAlign] no visible NCard inside slot.");
        }
        else
        {
            var gr = card.GetGlobalRect();
            var gp = card.GlobalPosition;
            var xf = card.GetGlobalTransformWithCanvas();
            var sz = card.Size;
            float sx = xf.X.Length();
            float sy = xf.Y.Length();
            ModLogger.Info(
                $"[ShopAlign] ncard='{card.Name}' size({sz.X:F0}x{sz.Y:F0}) " +
                $"globalRect=({gr.Position.X:F0},{gr.Position.Y:F0}) globalPos=({gp.X:F0},{gp.Y:F0}) " +
                $"canvasOrigin=({xf.Origin.X:F0},{xf.Origin.Y:F0}) canvasScale=({sx:F2},{sy:F2})");
        }

        foreach (var clickable in NodeQuery.FindAll<NClickableControl>(slot))
        {
            if (clickable is not Control ctrl || !NodeQuery.IsVisible(ctrl))
                continue;

            var hr = ctrl.GetGlobalRect();
            var hsize = ctrl.Size.X < 1f ? ctrl.GetRect().Size : ctrl.Size;
            var screen = ctrl.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, hsize);
            ModLogger.Info(
                $"[ShopAlign] hitbox='{ctrl.Name}' layoutSize({hsize.X:F0}x{hsize.Y:F0}) " +
                $"globalRect=({hr.Position.X:F0},{hr.Position.Y:F0} {hr.Size.X:F0}x{hr.Size.Y:F0}) " +
                $"screenRect=({screen.Position.X:F0},{screen.Position.Y:F0} {screen.Size.X:F0}x{screen.Size.Y:F0})");
            break;
        }

        var tree = Engine.GetMainLoop() as SceneTree;
        var root = tree?.Root;
        if (root == null)
            return;

        var mw = root.GetMousePosition();
        var vp = root.GetViewport();
        var ct = vp?.CanvasTransform ?? Transform2D.Identity;
        var gct = vp?.GlobalCanvasTransform ?? Transform2D.Identity;
        ModLogger.Info(
            $"[ShopAlign] mouse=({mw.X:F0},{mw.Y:F0}) " +
            $"canvasXform origin=({ct.Origin.X:F0},{ct.Origin.Y:F0}) scale=({ct.X.Length():F2},{ct.Y.Length():F2}) " +
            $"globalCanvasXform origin=({gct.Origin.X:F0},{gct.Origin.Y:F0}) scale=({gct.X.Length():F2},{gct.Y.Length():F2})");
    }
}
