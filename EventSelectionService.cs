using Godot;

namespace DwellTargeting;

internal static class EventSelectionService
{
    /// <summary>
    /// Activates an event option button. These are NButton/NClickableControl, so ForceClick drives the
    /// same path as a real click (unlike card holders, which need their own Pressed signal).
    /// </summary>
    internal static void TrySelect(Control button)
    {
        if (!NodeQuery.IsLive(button))
        {
            ModLogger.Warn("Event option not live.");
            return;
        }

        if (InputForwardService.TryActivateControl(button))
            ModLogger.Info($"Event option '{button.Name}' activated.");
        else
            ModLogger.Warn($"Event option '{button.Name}' activation failed.");

        OverlayModeService.InvalidateCache();
        EventOverlay.InvalidateCache();
    }
}
