using System.Reflection;
using Godot;

namespace DwellTargeting;

/// <summary>
/// Optional integration with ModManagerSettings (reflection — no hard DLL dependency).
/// </summary>
internal static class ModManagerSettingsBridge
{
    internal const string ModId = "DwellTargeting";

    private static bool _registered;
    private static bool _hydrated;
    private static int _registerAttempts;
    private static Type? _registryType;

    internal static void ScheduleRegistration()
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
            return;

        var timer = tree.CreateTimer(0.0);
        timer.Timeout += TryRegister;
    }

    internal static void TryRegister()
    {
        if (_registered)
            return;

        _registerAttempts++;
        if (!TryResolveRegistryType(out _registryType))
        {
            if (_registerAttempts <= 120)
                ScheduleRegistration();

            return;
        }

        try
        {
            RegisterSettings(_registryType!);
            _registered = true;
            ModLogger.Info("Registered settings with ModManagerSettings.");
            TryHydrateFromPersistedValues(force: true);
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"ModManagerSettings registration failed: {ex.Message}");
        }
    }

    internal static void TryHydrateFromPersistedValues(bool force = false)
    {
        if (_hydrated && !force)
            return;

        if (_registryType == null && !TryResolveRegistryType(out _registryType))
            return;

        try
        {
            var isReadyMethod = _registryType!.GetMethod("IsPersistenceReady", BindingFlags.Public | BindingFlags.Static);
            if (isReadyMethod == null)
                return;

            var isReady = isReadyMethod.Invoke(null, null) is true;
            if (!isReady)
                return;

            var restoreMethod = _registryType.GetMethod(
                "RestorePersistedValues",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            restoreMethod?.Invoke(null, new object[] { ModId });
            _hydrated = true;
            ModLogger.Info($"ModManagerSettings hydrated hideEndTurn={SettingsStore.Current.HideEndTurnButton}");
        }
        catch (Exception ex)
        {
            ModLogger.Warn($"ModManagerSettings hydration failed: {ex.Message}");
        }
    }

    private static bool TryResolveRegistryType(out Type? registryType)
    {
        registryType = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "ModManagerSettings", StringComparison.OrdinalIgnoreCase))
            ?.GetType("ModManagerSettings.Api.ModSettingsRegistry");

        return registryType != null;
    }

    private static void RegisterSettings(Type registryType)
    {
        var asm = registryType.Assembly;
        var registrationType = asm.GetType("ModManagerSettings.Api.ModSettingsRegistration", throwOnError: true)!;
        var toggleType = asm.GetType("ModManagerSettings.Api.ModSettingToggleDefinition", throwOnError: true)!;

        var toggle = CreateHideEndTurnToggle(toggleType);
        var registration = Activator.CreateInstance(registrationType)!;

        SetProperty(registration, "ModPckName", ModId);
        SetProperty(registration, "DisplayName", "Dwell Targeting");
        SetProperty(registration, "Description", "Accessibility overlays for dwell-mouse card play.");
        SetProperty(registration, "ShowSettingsButtonInModdingMenu", true);
        SetProperty(registration, "ToggleSettings", CreateToggleList(toggleType, toggle));
        SetProperty(registration, "OnRestoreDefaults", new Action(SettingsStore.RestoreDefaults));

        var registerMethod = registryType.GetMethod(
            "Register",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { registrationType },
            modifiers: null);

        registerMethod?.Invoke(null, new[] { registration });
    }

    private static object CreateHideEndTurnToggle(Type toggleType)
    {
        var toggle = Activator.CreateInstance(toggleType)!;
        SetProperty(toggle, "Key", "hide_end_turn_button");
        SetProperty(toggle, "Label", "Hide End Turn Button");
        SetProperty(
            toggle,
            "Description",
            "Hides the mod E END overlay during combat. Use the game's End Turn button instead.");
        SetProperty(toggle, "Path", "Settings/Combat");
        SetProperty(toggle, "DefaultValue", false);
        SetProperty(toggle, "AllowMultiplayerOverwrite", false);
        SetProperty(toggle, "GetCurrentValue", new Func<bool>(() => SettingsStore.Current.HideEndTurnButton));
        SetProperty(toggle, "OnApply", new Action<bool>(SettingsStore.SetHideEndTurnButton));
        return toggle;
    }

    private static object CreateToggleList(Type toggleType, object toggle)
    {
        var listType = typeof(List<>).MakeGenericType(toggleType);
        var list = Activator.CreateInstance(listType)!;
        listType.GetMethod("Add")!.Invoke(list, new[] { toggle });
        return list;
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            throw new MissingMemberException(target.GetType().FullName, propertyName);

        property.SetValue(target, value);
    }
}
