using System;
using System.Collections.Generic;
using Godot;

namespace DwellTargeting;

internal static class ModConfigEntries
{
    internal static List<object> BuildList(Type entryType, Type configTypeEnum)
    {
        var list = new List<object>
        {
            Header("General", entryType, configTypeEnum),
            Toggle("hideOverlaysInMenus", "Hide Overlays in Menus", SettingsStore.Current.HideOverlaysInMenus,
                "Hide dwell buttons while pause, settings, or mod config screens are open.",
                v => SettingsStore.ApplyHideOverlaysInMenus(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("enablePerfLogging", "Performance Logging", SettingsStore.Current.EnablePerfLogging,
                "Write frame timing to dwell-targeting.log every 3s (for diagnosing frame drops). Leave off normally.",
                v => SettingsStore.ApplyEnablePerfLogging(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("Hover Time (seconds)", entryType, configTypeEnum),
            Slider("cardDwellSeconds", "Card Hover Time", SettingsStore.Current.CardDwellSeconds,
                "How long to hover on card number buttons before they activate.",
                v => SettingsStore.ApplyCardDwellSeconds(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds, 0.05f, "F2", entryType, configTypeEnum),
            Slider("endTurnDwellSeconds", "End Turn Hover Time", SettingsStore.Current.EndTurnDwellSeconds,
                "How long to hover on the End Turn button before it activates.",
                v => SettingsStore.ApplyEndTurnDwellSeconds(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MinDwellSeconds, DwellSettings.MaxDwellSeconds, 0.05f, "F2", entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("Card Number Buttons", entryType, configTypeEnum),
            Slider("cardButtonScale", "Number Button Size", SettingsStore.Current.CardButtonScale,
                "1.0 = smallest (default). Up to 1.5 = larger.",
                v => SettingsStore.ApplyCardButtonScale(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.CardMinScale, DwellSettings.CardMaxScale, 0.05f, "F2", entryType, configTypeEnum),
            Slider("cardButtonOpacity", "Number Button Opacity", SettingsStore.Current.CardButtonOpacity,
                "Transparency for numbered card buttons and play targets.",
                v => SettingsStore.ApplyCardButtonOpacity(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MinOpacity, DwellSettings.MaxOpacity, 0.05f, "F2", entryType, configTypeEnum),
            Toggle("showEnemyLabels", "Enemy Slot Numbers", SettingsStore.Current.ShowEnemyLabels,
                "Show 1 / 2 / 3 above enemies to match card target buttons.",
                v => SettingsStore.ApplyShowEnemyLabels(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("End Turn / Confirm Buttons", entryType, configTypeEnum),
            Slider("actionButtonScale", "Button Size", SettingsStore.Current.ActionButtonScale,
                "1.0 = default (largest). Down to 0.5 = smallest.",
                v => SettingsStore.ApplyActionButtonScale(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MenuMinScale, DwellSettings.MenuMaxScale, 0.05f, "F2", entryType, configTypeEnum),
            Slider("menuButtonOpacity", "Button Opacity", SettingsStore.Current.MenuButtonOpacity,
                "Transparency for End Turn and Confirm overlay buttons.",
                v => SettingsStore.ApplyMenuButtonOpacity(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MinOpacity, DwellSettings.MaxOpacity, 0.05f, "F2", entryType, configTypeEnum),
            Toggle("hideEndTurnButton", "Hide End Turn Button", SettingsStore.Current.HideEndTurnButton,
                "Hide the mod E END overlay during combat.",
                v => SettingsStore.ApplyHideEndTurnButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("hideConfirmButton", "Hide Confirm Button", SettingsStore.Current.HideConfirmButton,
                "Hide the mod E OK overlay during card selection (discard, etc.).",
                v => SettingsStore.ApplyHideConfirmButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("Native Utility Dwell", entryType, configTypeEnum),
            Toggle("showDrawPileButton", "Draw Pile Dwell", SettingsStore.Current.ShowDrawPileButton,
                "Dwell the native draw pile button in combat.",
                v => SettingsStore.ApplyShowDrawPileButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showDiscardPileButton", "Discard Pile Dwell", SettingsStore.Current.ShowDiscardPileButton,
                "Dwell the native discard pile button in combat.",
                v => SettingsStore.ApplyShowDiscardPileButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showDeckButton", "Deck Dwell", SettingsStore.Current.ShowDeckButton,
                "Dwell the native deck button in the top bar.",
                v => SettingsStore.ApplyShowDeckButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showExhaustPileButton", "Exhaust Pile Dwell", SettingsStore.Current.ShowExhaustPileButton,
                "Dwell the native exhaust pile button when visible.",
                v => SettingsStore.ApplyShowExhaustPileButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showMapButton", "Map Dwell", SettingsStore.Current.ShowMapButton,
                "Dwell the native map button in the top bar.",
                v => SettingsStore.ApplyShowMapButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showMenuButton", "Pause Menu Dwell", SettingsStore.Current.ShowMenuButton,
                "Dwell the native pause button in the top bar.",
                v => SettingsStore.ApplyShowMenuButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum)
        };

        return list;
    }

    private static object Header(string label, Type entryType, Type configTypeEnum) =>
        Entry(entryType, cfg =>
        {
            Set(cfg, "Label", label);
            Set(cfg, "Labels", L(label, label));
            Set(cfg, "Type", EnumVal("Header", configTypeEnum));
        });

    private static object Separator(Type entryType, Type configTypeEnum) =>
        Entry(entryType, cfg => Set(cfg, "Type", EnumVal("Separator", configTypeEnum)));

    private static object Toggle(
        string key,
        string label,
        bool defaultValue,
        string description,
        Action<object> onChanged,
        Type entryType,
        Type configTypeEnum) =>
        Entry(entryType, cfg =>
        {
            Set(cfg, "Key", key);
            Set(cfg, "Label", label);
            Set(cfg, "Labels", L(label, label));
            Set(cfg, "Type", EnumVal("Toggle", configTypeEnum));
            Set(cfg, "DefaultValue", (object)defaultValue);
            Set(cfg, "Description", description);
            Set(cfg, "Descriptions", L(description, description));
            Set(cfg, "OnChanged", onChanged);
        });

    private static object Slider(
        string key,
        string label,
        float defaultValue,
        string description,
        Action<object> onChanged,
        float min,
        float max,
        float step,
        string format,
        Type entryType,
        Type configTypeEnum) =>
        Entry(entryType, cfg =>
        {
            Set(cfg, "Key", key);
            Set(cfg, "Label", label);
            Set(cfg, "Labels", L(label, label));
            Set(cfg, "Type", EnumVal("Slider", configTypeEnum));
            Set(cfg, "DefaultValue", (object)defaultValue);
            Set(cfg, "Min", min);
            Set(cfg, "Max", max);
            Set(cfg, "Step", step);
            Set(cfg, "Format", format);
            Set(cfg, "Description", description);
            Set(cfg, "Descriptions", L(description, description));
            Set(cfg, "OnChanged", onChanged);
        });

    private static object Entry(Type entryType, Action<object> configure)
    {
        var inst = Activator.CreateInstance(entryType)!;
        configure(inst);
        return inst;
    }

    private static void Set(object obj, string name, object value) =>
        obj.GetType().GetProperty(name)?.SetValue(obj, value);

    private static Dictionary<string, string> L(string en, string zhs) =>
        new() { ["en"] = en, ["zhs"] = zhs };

    private static object EnumVal(string name, Type configTypeEnum) =>
        Enum.Parse(configTypeEnum, name);
}
