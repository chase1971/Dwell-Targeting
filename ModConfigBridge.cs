using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace DwellTargeting;

/// <summary>
/// Optional ModConfig integration via reflection (no hard DLL dependency).
/// </summary>
internal static class ModConfigBridge
{
    internal const string ModId = "DwellTargeting";

    private static bool _available;
    private static bool _registered;
    private static int _registerAttempts;
    private static Type? _apiType;
    private static Type? _entryType;
    private static Type? _configTypeEnum;

    internal static bool IsAvailable => _available;

    internal static bool IsRegistered => _registered;

    internal static void DeferredRegister()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
            return;

        var timer = tree.CreateTimer(0.0);
        timer.Timeout += TryRegister;
    }

    internal static void SetValue(string key, object value)
    {
        if (!_available)
            return;

        try
        {
            _apiType!.GetMethod("SetValue", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { ModId, key, value });
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"ModConfig SetValue failed: {ex.Message}");
        }
    }

    internal static void SyncAllFromStore()
    {
        if (!_registered)
            return;

        var s = SettingsStore.Current;
        ApplyFromModConfig("hideOverlaysInMenus", s.HideOverlaysInMenus, v => SettingsStore.ApplyHideOverlaysInMenus(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("enablePerfLogging", s.EnablePerfLogging, v => SettingsStore.ApplyEnablePerfLogging(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("hideEndTurnButton", s.HideEndTurnButton, v => SettingsStore.ApplyHideEndTurnButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("hideConfirmButton", s.HideConfirmButton, v => SettingsStore.ApplyHideConfirmButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("cardDwellSeconds", s.CardDwellSeconds, v => SettingsStore.ApplyCardDwellSeconds(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("endTurnDwellSeconds", s.EndTurnDwellSeconds, v => SettingsStore.ApplyEndTurnDwellSeconds(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("menuDwellSeconds", s.MenuDwellSeconds, v => SettingsStore.ApplyMenuDwellSeconds(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("treeScanIntervalFrames", s.TreeScanIntervalFrames, v => SettingsStore.ApplyTreeScanIntervalFrames(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("hoverScrollSpeedScale", s.HoverScrollSpeedScale, v => SettingsStore.ApplyHoverScrollSpeedScale(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("cardButtonScale", s.CardButtonScale, v => SettingsStore.ApplyCardButtonScale(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("actionButtonScale", s.ActionButtonScale, v => SettingsStore.ApplyActionButtonScale(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("cardButtonOpacity", s.CardButtonOpacity, v => SettingsStore.ApplyCardButtonOpacity(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showEnemyLabels", s.ShowEnemyLabels, v => SettingsStore.ApplyShowEnemyLabels(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("menuButtonOpacity", s.MenuButtonOpacity, v => SettingsStore.ApplyMenuButtonOpacity(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showDrawPileButton", s.ShowDrawPileButton, v => SettingsStore.ApplyShowDrawPileButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showDiscardPileButton", s.ShowDiscardPileButton, v => SettingsStore.ApplyShowDiscardPileButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showDeckButton", s.ShowDeckButton, v => SettingsStore.ApplyShowDeckButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showExhaustPileButton", s.ShowExhaustPileButton, v => SettingsStore.ApplyShowExhaustPileButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showMapButton", s.ShowMapButton, v => SettingsStore.ApplyShowMapButton(v, persist: false, syncModConfig: false));
        ApplyFromModConfig("showMenuButton", s.ShowMenuButton, v => SettingsStore.ApplyShowMenuButton(v, persist: false, syncModConfig: false));
    }

    private static void TryRegister()
    {
        if (_registered)
            return;

        _registerAttempts++;
        Detect();

        if (_available)
        {
            Register();
            if (_registered)
            {
                SyncAllFromStore();
                ModLogger.Info("ModConfig registration complete.");
                return;
            }
        }

        if (_registerAttempts <= 120)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var timer = tree?.CreateTimer(0.0);
            if (timer != null)
                timer.Timeout += TryRegister;
        }
        else
        {
            ModLogger.Info("ModConfig not found — using built-in settings panel / settings.json.");
        }
    }

    private static void Detect()
    {
        try
        {
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Type.EmptyTypes;
                    }
                })
                .ToArray();

            _apiType = allTypes.FirstOrDefault(t => t.FullName == "ModConfig.ModConfigApi");
            _entryType = allTypes.FirstOrDefault(t => t.FullName == "ModConfig.ConfigEntry");
            _configTypeEnum = allTypes.FirstOrDefault(t => t.FullName == "ModConfig.ConfigType");
            _available = _apiType != null && _entryType != null && _configTypeEnum != null;
        }
        catch
        {
            _available = false;
        }
    }

    private static void Register()
    {
        if (_registered || _entryType == null || _apiType == null)
            return;

        try
        {
            var list = ModConfigEntries.BuildList(_entryType, _configTypeEnum!);
            Type entryArrayType = _entryType.MakeArrayType();
            var entries = Array.CreateInstance(_entryType, list.Count);
            for (int i = 0; i < list.Count; i++)
                entries.SetValue(list[i], i);

            var displayNames = new Dictionary<string, string>
            {
                ["en"] = "Dwell Targeting",
                ["zhs"] = "Dwell Targeting"
            };

            var registerMethod = _apiType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Register")
                .FirstOrDefault(m => m.GetParameters().Last().ParameterType == entryArrayType)
                ?? _apiType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Register")
                    .OrderByDescending(m => m.GetParameters().Length)
                    .First();

            if (registerMethod.GetParameters().Length == 4)
            {
                registerMethod.Invoke(null, new object[] { ModId, displayNames["en"], displayNames, entries });
            }
            else
            {
                registerMethod.Invoke(null, new object[] { ModId, displayNames["en"], entries });
            }

            _registered = true;
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"ModConfig registration failed: {ex.Message}");
            if (ex.InnerException != null)
                ModLogger.Warn($"ModConfig registration inner: {ex.InnerException.Message}");
        }
    }

    private static void ApplyFromModConfig<T>(string key, T current, Action<T> apply)
    {
        T value = GetValue(key, current);
        apply(value);
    }

    private static T GetValue<T>(string key, T fallback)
    {
        if (!_available)
            return fallback;

        try
        {
            var result = _apiType!.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(typeof(T))
                ?.Invoke(null, new object[] { ModId, key });

            return result != null ? (T)result : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
