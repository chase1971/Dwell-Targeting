# Dwell Targeting — Session Log

## 2026-06-13 — ModConfig, utility bar, button scale/opacity (v0.10.2)

**Files changed:** ModConfigBridge.cs, ModConfigEntries.cs, UtilityBarOverlay.cs (new), GameOverlayVisibility.cs (new), OverlayButtonFactory.cs (new), InputForwardService.cs, ConfirmOverlay.cs, EndTurnOverlay.cs, CardButtonRow.cs, HandTargetingOverlay.cs (~401 lines), SettingsStore.cs, DwellSettings.cs, mod_manifest.json / ModEntry.cs (v0.10.2)

**What worked:**
- ModConfig integration (reflection bridge + proper ConfigEntry[] registration) — settings UX confirmed working
- Fixed E/Confirm during discard: `ParseInputEvent` key sim; removed bad off-screen NConfirmButton click
- Left utility bar (draw/discard/deck/exhaust/map/menu) with per-button show/hide + size sliders
- Hide all dwell overlays when pause/settings/ModConfig open (`hideOverlaysInMenus`)
- Card button scale (1.0–1.5) + opacity; menu/action buttons scale inverted (1.0 = max, 0.5 = min) + shared opacity
- Built and installed via `install.ps1` through v0.10.2

**Current state:** Green — v0.10.2 installed; combat/hand-select stable per user (do not re-lock hand without asking)

**File size flag:** HandTargetingOverlay.cs ~401 lines; SettingsOverlay.cs ~370 lines — both under cap but worth watching

**Next session:** Update README for ModConfig/utility bar/v0.10.x; optional potion buttons; consider dedicated git repo for release; custom keybindings for utility bar if remapped keys matter
