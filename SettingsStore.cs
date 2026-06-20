using System.Text.Json;

namespace DwellTargeting;

internal static class SettingsStore
{
    private const int BaseActionButtonSize = 110;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "mods",
        "DwellTargeting");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static DwellSettings _current = new();
    private static DateTime _lastWriteUtc = DateTime.MinValue;
    private static long _lastCheckTicks;

    internal static DwellSettings Current => _current;

    internal static string SettingsFilePath => SettingsPath;

    internal static void Initialize()
    {
        Directory.CreateDirectory(SettingsDir);
        Reload(force: true);
        ClampValues();
    }

    internal static void MaybeReload()
    {
        long now = Environment.TickCount64;
        if (now - _lastCheckTicks < 1000)
            return;

        _lastCheckTicks = now;

        if (!File.Exists(SettingsPath))
            return;

        var writeTime = File.GetLastWriteTimeUtc(SettingsPath);
        if (writeTime <= _lastWriteUtc)
            return;

        Reload(force: true);
    }

    internal static int GetCardButtonSize(int handCardCount)
    {
        int baseSize = handCardCount >= 9 ? 26
            : handCardCount >= 7 ? 30
            : handCardCount >= 5 ? 34
            : 38;

        return ScaleCardSize(baseSize, _current.CardButtonScale);
    }

    internal static int GetActionButtonSize() =>
        ScaleMenuSize(BaseActionButtonSize, _current.ActionButtonScale);

    internal static float GetCardButtonOpacity() =>
        Math.Clamp(_current.CardButtonOpacity, DwellSettings.MinOpacity, DwellSettings.MaxOpacity);

    internal static float GetMenuButtonOpacity() =>
        Math.Clamp(_current.MenuButtonOpacity, DwellSettings.MinOpacity, DwellSettings.MaxOpacity);

    internal static float GetCardDwellSeconds() =>
        Math.Clamp(_current.CardDwellSeconds, DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds);

    internal static float GetEndTurnDwellSeconds() =>
        Math.Clamp(_current.EndTurnDwellSeconds, DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds);

    internal static void SetHideEndTurnButton(bool value) =>
        ApplyHideEndTurnButton(value, persist: true, syncModConfig: true);

    internal static void ApplyHideEndTurnButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.HideEndTurnButton = v, _current.HideEndTurnButton, value, "hideEndTurnButton", persist, syncModConfig);

    internal static void ApplyHideOverlaysInMenus(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.HideOverlaysInMenus = v, _current.HideOverlaysInMenus, value, "hideOverlaysInMenus", persist, syncModConfig);

    internal static void ApplyHideConfirmButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.HideConfirmButton = v, _current.HideConfirmButton, value, "hideConfirmButton", persist, syncModConfig);

    internal static void ApplyCardButtonScale(float value, bool persist = true, bool syncModConfig = true) =>
        ApplyFloat(v => _current.CardButtonScale = v, _current.CardButtonScale, value, DwellSettings.CardMinScale, DwellSettings.CardMaxScale, "cardButtonScale", persist, syncModConfig);

    internal static void ApplyActionButtonScale(float value, bool persist = true, bool syncModConfig = true) =>
        ApplyFloat(v => _current.ActionButtonScale = v, _current.ActionButtonScale, value, DwellSettings.MenuMinScale, DwellSettings.MenuMaxScale, "actionButtonScale", persist, syncModConfig);

    internal static void ApplyCardDwellSeconds(float value, bool persist = true, bool syncModConfig = true) =>
        ApplyFloat(v => _current.CardDwellSeconds = v, _current.CardDwellSeconds, value, DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds, "cardDwellSeconds", persist, syncModConfig);

    internal static void ApplyEndTurnDwellSeconds(float value, bool persist = true, bool syncModConfig = true) =>
        ApplyFloat(v => _current.EndTurnDwellSeconds = v, _current.EndTurnDwellSeconds, value, DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds, "endTurnDwellSeconds", persist, syncModConfig);

    internal static void ApplyCardButtonOpacity(float value, bool persist = true, bool syncModConfig = true) =>
        ApplyFloat(v => _current.CardButtonOpacity = v, _current.CardButtonOpacity, value, DwellSettings.MinOpacity, DwellSettings.MaxOpacity, "cardButtonOpacity", persist, syncModConfig);

    internal static void ApplyMenuButtonOpacity(float value, bool persist = true, bool syncModConfig = true) =>
        ApplyFloat(v => _current.MenuButtonOpacity = v, _current.MenuButtonOpacity, value, DwellSettings.MinOpacity, DwellSettings.MaxOpacity, "menuButtonOpacity", persist, syncModConfig);

    internal static void ApplyShowDrawPileButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowDrawPileButton = v, _current.ShowDrawPileButton, value, "showDrawPileButton", persist, syncModConfig);

    internal static void ApplyShowDiscardPileButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowDiscardPileButton = v, _current.ShowDiscardPileButton, value, "showDiscardPileButton", persist, syncModConfig);

    internal static void ApplyShowDeckButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowDeckButton = v, _current.ShowDeckButton, value, "showDeckButton", persist, syncModConfig);

    internal static void ApplyShowExhaustPileButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowExhaustPileButton = v, _current.ShowExhaustPileButton, value, "showExhaustPileButton", persist, syncModConfig);

    internal static void ApplyShowMapButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowMapButton = v, _current.ShowMapButton, value, "showMapButton", persist, syncModConfig);

    internal static void ApplyShowMenuButton(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowMenuButton = v, _current.ShowMenuButton, value, "showMenuButton", persist, syncModConfig);

    internal static void ApplyShowEnemyLabels(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowEnemyLabels = v, _current.ShowEnemyLabels, value, "showEnemyLabels", persist, syncModConfig);

    internal static void ApplyEnablePerfLogging(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.EnablePerfLogging = v, _current.EnablePerfLogging, value, "enablePerfLogging", persist, syncModConfig);

    internal static void ApplyShowHitboxOverlay(bool value, bool persist = true, bool syncModConfig = true) =>
        ApplyBool(v => _current.ShowHitboxOverlay = v, _current.ShowHitboxOverlay, value, "showHitboxOverlay", persist, syncModConfig);

    internal static void RestoreDefaults()
    {
        _current = new DwellSettings();
        Save();
        ModConfigBridge.SyncAllFromStore();
        ModLogger.Info("Settings restored to defaults.");
    }

    private static void ApplyBool(
        Action<bool> setter,
        bool current,
        bool value,
        string key,
        bool persist,
        bool syncModConfig)
    {
        if (current == value)
            return;

        setter(value);

        if (persist)
            Save();

        if (syncModConfig && ModConfigBridge.IsRegistered)
            ModConfigBridge.SetValue(key, value);

        ModLogger.Info($"{key}={value}");
    }

    private static void ApplyFloat(
        Action<float> setter,
        float current,
        float value,
        float min,
        float max,
        string key,
        bool persist,
        bool syncModConfig)
    {
        value = Math.Clamp(value, min, max);
        if (Math.Abs(current - value) < 0.001f)
            return;

        setter(value);

        if (persist)
            Save();

        if (syncModConfig && ModConfigBridge.IsRegistered)
            ModConfigBridge.SetValue(key, value);

        ModLogger.Info($"{key}={value:F2}");
    }

    private static int ScaleCardSize(int baseSize, float scale)
    {
        float clamped = Math.Clamp(scale, DwellSettings.CardMinScale, DwellSettings.CardMaxScale);
        return Math.Clamp((int)Math.Round(baseSize * clamped), baseSize, (int)Math.Round(baseSize * DwellSettings.CardMaxScale));
    }

    private static int ScaleMenuSize(int baseSize, float scale)
    {
        float clamped = Math.Clamp(scale, DwellSettings.MenuMinScale, DwellSettings.MenuMaxScale);
        int minSize = (int)Math.Round(baseSize * DwellSettings.MenuMinScale);
        return Math.Clamp((int)Math.Round(baseSize * clamped), minSize, baseSize);
    }

    private static void ClampValues()
    {
        _current.CardButtonScale = Math.Clamp(_current.CardButtonScale, DwellSettings.CardMinScale, DwellSettings.CardMaxScale);
        _current.ActionButtonScale = Math.Clamp(_current.ActionButtonScale, DwellSettings.MenuMinScale, DwellSettings.MenuMaxScale);
        _current.CardButtonOpacity = Math.Clamp(_current.CardButtonOpacity, DwellSettings.MinOpacity, DwellSettings.MaxOpacity);
        _current.MenuButtonOpacity = Math.Clamp(_current.MenuButtonOpacity, DwellSettings.MinOpacity, DwellSettings.MaxOpacity);
        _current.CardDwellSeconds = Math.Clamp(_current.CardDwellSeconds, DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds);
        _current.EndTurnDwellSeconds = Math.Clamp(_current.EndTurnDwellSeconds, DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds);
    }

    private static void Reload(bool force)
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                _current = new DwellSettings();
                _lastWriteUtc = DateTime.MinValue;
                return;
            }

            var writeTime = File.GetLastWriteTimeUtc(SettingsPath);
            if (!force && writeTime <= _lastWriteUtc)
                return;

            var json = File.ReadAllText(SettingsPath);
            _current = JsonSerializer.Deserialize<DwellSettings>(json) ?? new DwellSettings();
            ClampValues();
            _lastWriteUtc = writeTime;
            ModLogger.Info($"Settings loaded cardScale={_current.CardButtonScale:F2} menuOpacity={_current.MenuButtonOpacity:F2}");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Settings load failed: {ex.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(_current);
            File.WriteAllText(SettingsPath, json);
            _lastWriteUtc = File.GetLastWriteTimeUtc(SettingsPath);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"Settings save failed: {ex.Message}");
        }
    }
}
