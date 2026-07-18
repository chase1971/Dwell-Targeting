using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace DwellTargeting;

internal static class RewardSelectionService
{
    private static readonly MethodInfo? ButtonOnPress =
        typeof(NButton).GetMethod("OnPress", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void TryClaim(NRewardButton button)
    {
        var screen = OverlayModeService.GetCachedRewardsScreen();
        if (screen == null || !NodeQuery.IsLive(button))
        {
            ModLogger.Warn("TryClaim: rewards screen or button unavailable.");
            return;
        }

        if (TryActivateButton(button))
        {
            OverlayModeService.InvalidateCache();
            return;
        }

        try
        {
            screen.RewardCollectedFrom(button);
            ModLogger.Info($"RewardCollectedFrom '{button.Name}'.");
            OverlayModeService.InvalidateCache();
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"RewardCollectedFrom failed: {ex.Message}");
            InputForwardService.TryActivateControl(button);
            OverlayModeService.InvalidateCache();
        }
    }

    internal static void TryProceed(NProceedButton proceed)
    {
        // Proceed / Skip responds to the E accept key (ForceClick does not reliably advance it).
        InputForwardService.PressAcceptKey();
        ModLogger.Info("Proceed via E accept key.");
        OverlayModeService.InvalidateCache();
    }

    private static bool TryActivateButton(Control button)
    {
        if (button is NClickableControl clickable)
        {
            clickable.ForceClick();
            ModLogger.Info($"Reward control '{button.Name}' via ForceClick.");
            return true;
        }

        if (button is NButton nButton && TryInvokeOnPress(nButton))
            return true;

        if (InputForwardService.TryActivateControl(button))
            return true;

        return false;
    }

    private static bool TryInvokeOnPress(NButton button)
    {
        if (ButtonOnPress == null)
            return false;

        try
        {
            ButtonOnPress.Invoke(button, null);
            ModLogger.Info($"Reward control '{button.Name}' via OnPress.");
            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"OnPress failed on '{button.Name}': {ex.Message}");
            return false;
        }
    }
}
