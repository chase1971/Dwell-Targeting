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

            Separator(entryType, configTypeEnum),
            Header("Card Number Buttons", entryType, configTypeEnum),
            Slider("cardButtonScale", "Number Button Size", SettingsStore.Current.CardButtonScale,
                "1.0 = smallest (default). Up to 1.5 = larger.",
                v => SettingsStore.ApplyCardButtonScale(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.CardMinScale, DwellSettings.CardMaxScale, entryType, configTypeEnum),
            Slider("cardButtonOpacity", "Number Button Opacity", SettingsStore.Current.CardButtonOpacity,
                "Transparency for numbered card buttons and play targets.",
                v => SettingsStore.ApplyCardButtonOpacity(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MinOpacity, DwellSettings.MaxOpacity, entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("Menu / Pile / E Buttons", entryType, configTypeEnum),
            Slider("actionButtonScale", "E / End Turn Size", SettingsStore.Current.ActionButtonScale,
                "1.0 = default (largest). Down to 0.5 = smallest.",
                v => SettingsStore.ApplyActionButtonScale(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MenuMinScale, DwellSettings.MenuMaxScale, entryType, configTypeEnum),
            Slider("utilityButtonScale", "Pile / Map Size", SettingsStore.Current.UtilityButtonScale,
                "1.0 = default (largest). Down to 0.5 = smallest.",
                v => SettingsStore.ApplyUtilityButtonScale(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MenuMinScale, DwellSettings.MenuMaxScale, entryType, configTypeEnum),
            Slider("menuButtonOpacity", "Menu Button Opacity", SettingsStore.Current.MenuButtonOpacity,
                "Transparency for E buttons, pile buttons, map, and menu.",
                v => SettingsStore.ApplyMenuButtonOpacity(Convert.ToSingle(v), persist: true, syncModConfig: false),
                DwellSettings.MinOpacity, DwellSettings.MaxOpacity, entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("Combat Buttons", entryType, configTypeEnum),
            Toggle("hideEndTurnButton", "Hide End Turn Button", SettingsStore.Current.HideEndTurnButton,
                "Hide the mod E END overlay during combat.",
                v => SettingsStore.ApplyHideEndTurnButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("hideConfirmButton", "Hide Confirm Button", SettingsStore.Current.HideConfirmButton,
                "Hide the mod E OK overlay during card selection (discard, etc.).",
                v => SettingsStore.ApplyHideConfirmButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),

            Separator(entryType, configTypeEnum),
            Header("Left Utility Bar", entryType, configTypeEnum),
            Toggle("showDrawPileButton", "Show Draw Pile (A)", SettingsStore.Current.ShowDrawPileButton,
                "View draw pile — default game key A.",
                v => SettingsStore.ApplyShowDrawPileButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showDiscardPileButton", "Show Discard Pile (S)", SettingsStore.Current.ShowDiscardPileButton,
                "View discard pile — default game key S.",
                v => SettingsStore.ApplyShowDiscardPileButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showDeckButton", "Show Deck (D)", SettingsStore.Current.ShowDeckButton,
                "View full deck — default game key D.",
                v => SettingsStore.ApplyShowDeckButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showExhaustPileButton", "Show Exhaust Pile (X)", SettingsStore.Current.ShowExhaustPileButton,
                "View exhaust pile — default game key X.",
                v => SettingsStore.ApplyShowExhaustPileButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showMapButton", "Show Map (M)", SettingsStore.Current.ShowMapButton,
                "Open map — default game key M.",
                v => SettingsStore.ApplyShowMapButton(Convert.ToBoolean(v), persist: true, syncModConfig: false),
                entryType, configTypeEnum),
            Toggle("showMenuButton", "Show Menu (Esc)", SettingsStore.Current.ShowMenuButton,
                "Open pause/menu — default game key Escape.",
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
            Set(cfg, "Step", 0.05f);
            Set(cfg, "Format", "F2");
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
