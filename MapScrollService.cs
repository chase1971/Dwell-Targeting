using Godot;

namespace DwellTargeting;

/// <summary>
/// Scrolls the map view by feeding the game a synthetic mouse-wheel event (the map already handles
/// wheel scrolling natively, so this drives its own logic — including clamping at the edges).
/// </summary>
internal static class MapScrollService
{
    internal static void Scroll(bool up)
    {
        var viewport = (Engine.GetMainLoop() as SceneTree)?.Root?.GetViewport();
        if (viewport == null)
            return;

        var center = viewport.GetVisibleRect().Size / 2f;
        var wheel = new InputEventMouseButton
        {
            ButtonIndex = up ? MouseButton.WheelUp : MouseButton.WheelDown,
            Pressed = true,
            Factor = 1f,
            Position = center,
            GlobalPosition = center
        };

        viewport.PushInput(wheel);
    }
}
