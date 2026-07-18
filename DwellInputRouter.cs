using Godot;

namespace DwellTargeting;

/// <summary>
/// Routes mouse input: dwell buttons and combat hand lockout.
/// </summary>
internal partial class DwellInputRouter : Node
{
    public override void _Input(InputEvent @event)
    {
        HandleMouse(@event, allowPlay: true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        HandleMouse(@event, allowPlay: false);
    }

    private static void HandleMouse(InputEvent @event, bool allowPlay)
    {
        if (@event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
            return;

        var viewport = (Engine.GetMainLoop() as SceneTree)?.Root?.GetViewport();
        if (viewport == null)
            return;

        if (SettingsOverlay.TryRouteClick(mouseButton.GlobalPosition, out string settingsMessage))
        {
            ModLogger.Info(settingsMessage);
            viewport.SetInputAsHandled();
            return;
        }

        if (SettingsOverlay.BlocksUnderlyingInput(mouseButton.GlobalPosition))
        {
            viewport.SetInputAsHandled();
            return;
        }

        var mode = OverlayModeService.GetMode();

        if (mode == OverlayMode.Rewards)
        {
            if (allowPlay && OverlayVisToggle.TryRouteClick(mouseButton.GlobalPosition, out string visMessage))
            {
                ModLogger.Info(visMessage);
                viewport.SetInputAsHandled();
                return;
            }

            if (allowPlay && RewardsOverlay.TryRouteClick(mouseButton.GlobalPosition, out string rewardMessage))
            {
                ModLogger.Info(rewardMessage);
                viewport.SetInputAsHandled();
            }

            return;
        }

        if (mode == OverlayMode.PileSelect)
        {
            if (allowPlay && OverlayVisToggle.TryRouteClick(mouseButton.GlobalPosition, out string visMessage))
            {
                ModLogger.Info(visMessage);
                viewport.SetInputAsHandled();
                return;
            }

            if (allowPlay && PileSelectOverlay.TryRouteClick(mouseButton.GlobalPosition, out string pileMessage))
            {
                ModLogger.Info(pileMessage);
                viewport.SetInputAsHandled();
            }

            return;
        }

        if (mode == OverlayMode.Shop)
        {
            if (allowPlay && OverlayVisToggle.TryRouteClick(mouseButton.GlobalPosition, out string visMessage))
            {
                ModLogger.Info(visMessage);
                viewport.SetInputAsHandled();
                return;
            }

            if (allowPlay && ShopOverlay.TryRouteClick(mouseButton.GlobalPosition, out string shopMessage))
            {
                ModLogger.Info(shopMessage);
                viewport.SetInputAsHandled();
            }

            return;
        }

        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (allowPlay && HandTargetingOverlay.TryRouteClick(mouseButton.GlobalPosition, out string message))
        {
            ModLogger.Info(message);
            viewport.SetInputAsHandled();
            return;
        }

        if (HandTargetingOverlay.TryConsumeHandClick(mouseButton.GlobalPosition))
        {
            ModLogger.Info("Hand click blocked (card pick-up prevented).");
            viewport.SetInputAsHandled();
        }
    }
}
