namespace DwellTargeting;

/// <summary>
/// Blocks rapid repeat activation on menu / end-turn / confirm buttons (dwell + dwell-click double fire).
/// </summary>
internal static class DwellActivationCooldown
{
    private static long _menuBlockedUntilTick;

    internal static bool IsMenuBlocked =>
        Environment.TickCount64 < _menuBlockedUntilTick;

    internal static bool TryRunMenuAction(Action action)
    {
        if (IsMenuBlocked)
            return false;

        action();
        _menuBlockedUntilTick = Environment.TickCount64 + (long)(DwellTiming.MenuCooldownSeconds * 1000);
        ModLogger.Info($"Menu activation cooldown {DwellTiming.MenuCooldownSeconds:F2}s.");
        return true;
    }
}
