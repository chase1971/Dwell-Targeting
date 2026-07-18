using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace DwellTargeting;

[ModInitializer("Initialize")]
public static class ModEntry
{
    public static void Initialize()
    {
        SettingsStore.Initialize();
        SettingsOverlay.EnsureInitialized();
        OverlayVisToggle.EnsureInitialized();
        HandTargetingOverlay.EnsureInitialized();
        ModConfigBridge.DeferredRegister();
        ModManagerSettingsBridge.ScheduleRegistration();
        ModLogger.Info("v0.10.64 loaded — per-category dwell settings; native End Turn dwell; clearer ModConfig labels.");
        LegacyOverlayCleanup.RunOnce();
        ModLogger.Info($"Settings file: {SettingsStore.SettingsFilePath}");
        ModLogger.Info($"Log file: {System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "logs", "dwell-targeting.log")}");
    }
}
