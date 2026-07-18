namespace DwellTargeting;

internal static class DwellTiming
{
    internal static float CardDwellSeconds => SettingsStore.GetCardDwellSeconds();
    internal static float MenuDwellSeconds => SettingsStore.GetMenuDwellSeconds();
    internal static float EndTurnDwellSeconds => SettingsStore.GetEndTurnDwellSeconds();
    internal const float MenuCooldownSeconds = 1.75f;
}
