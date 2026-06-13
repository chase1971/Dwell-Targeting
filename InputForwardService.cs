using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace DwellTargeting;

internal static class InputForwardService
{
    internal static void Click(Vector2 globalPos, MouseButton button)
    {
        try
        {
            var viewport = (Engine.GetMainLoop() as SceneTree)?.Root?.GetViewport();
            if (viewport == null)
                return;

            var down = new InputEventMouseButton
            {
                ButtonIndex = button,
                Pressed = true,
                GlobalPosition = globalPos,
                Position = globalPos
            };
            var up = new InputEventMouseButton
            {
                ButtonIndex = button,
                Pressed = false,
                GlobalPosition = globalPos,
                Position = globalPos
            };
            viewport.PushInput(down);
            viewport.PushInput(up);
            ModLogger.Info($"Forwarded {button} click at ({globalPos.X:F0},{globalPos.Y:F0}).");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Forward click failed: {ex.Message}");
        }
    }

    internal static void ClickDeferred(Vector2 globalPos, MouseButton button)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            Click(globalPos, button);
            return;
        }

        var timer = tree.CreateTimer(0.05);
        timer.Timeout += () => Click(globalPos, button);
    }

    internal static void ClickHolder(NCardHolder holder, MouseButton button)
    {
        Control? target = FindClickTarget(holder);
        if (target == null)
        {
            ModLogger.Warn("ClickHolder: no click target on holder.");
            return;
        }

        ClickControlDeferred(target, button);
    }

    internal static void ClickCard(NCard card, MouseButton button)
    {
        if (card is not Control control || !NodeQuery.IsLive(control))
        {
            ModLogger.Warn("ClickCard: card is not a live control.");
            return;
        }

        ClickControlDeferred(control, button);
    }

    internal static void PressKey(Key key)
    {
        try
        {
            var down = CreateKeyEvent(key, pressed: true);
            var up = CreateKeyEvent(key, pressed: false);
            Input.ParseInputEvent(down);
            Input.ParseInputEvent(up);
            ModLogger.Info($"Parsed key {key}.");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Forward key failed: {ex.Message}");
        }
    }

    internal static void PressAcceptKey() => PressKey(Key.E);

    private static InputEventKey CreateKeyEvent(Key key, bool pressed) =>
        new()
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed,
            Echo = false
        };

    private static Control? FindClickTarget(NCardHolder holder)
    {
        foreach (var card in NodeQuery.FindAll<NCard>(holder))
        {
            if (card is Control cardControl && NodeQuery.IsVisible(cardControl))
                return cardControl;
        }

        if (holder is Control holderControl && NodeQuery.IsLive(holderControl))
            return holderControl;

        return null;
    }

    private static void ClickControlDeferred(Control control, MouseButton button)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            ClickControl(control, button);
            return;
        }

        var timer = tree.CreateTimer(0.02);
        timer.Timeout += () => ClickControl(control, button);
    }

    private static void ClickControl(Control control, MouseButton button)
    {
        try
        {
            var globalPos = control.GetGlobalRect().GetCenter();
            var localPos = control.GetGlobalTransformWithCanvas().AffineInverse() * globalPos;

            var down = new InputEventMouseButton
            {
                ButtonIndex = button,
                Pressed = true,
                GlobalPosition = globalPos,
                Position = localPos
            };
            var up = new InputEventMouseButton
            {
                ButtonIndex = button,
                Pressed = false,
                GlobalPosition = globalPos,
                Position = localPos
            };

            control.EmitSignal(Control.SignalName.GuiInput, down);
            control.EmitSignal(Control.SignalName.GuiInput, up);
            ModLogger.Info($"GuiInput {button} on {control.Name} at ({globalPos.X:F0},{globalPos.Y:F0}).");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"ClickControl failed: {ex.Message}");
        }
    }
}
