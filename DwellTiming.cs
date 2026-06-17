namespace DwellTargeting;

internal static class DwellTiming
{
    internal static float CardDwellSeconds => SettingsStore.GetCardDwellSeconds();
    internal const float MenuDwellSeconds = 0.9f;
    internal static float EndTurnDwellSeconds => SettingsStore.GetEndTurnDwellSeconds();
    internal const float MenuCooldownSeconds = 1.75f;
}
