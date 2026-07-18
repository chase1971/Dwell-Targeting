# Dwell Targeting — Session Log

## 2026-07-18 — Screen phases, events, chest exploit, quit dialog

**Files changed:** MainMenuOverlay.cs (new), CardConfirmPhaseQuery.cs (new), EventOverlay.cs, PileSelectOverlay.cs, HandSelectOverlay.cs, RoomOverlay.cs, DeckViewOverlay.cs, ViewScrollOverlay.cs, HandTargetingOverlay.cs, EventSelectionService.cs, plus prior session batch (ProceedTargetBuilder, PotionSlotOverlay, PauseMenuOverlay, UtilityBarOverlay, RewardsOverlay, ShopOverlay, etc.)

**What worked:** Main menu hitboxes tightened (no vertical bleed). Quit Yes/No via `NPopupYesNoButton` + dialog-only dwell. Event options rescan on choice/signature; root-tree fallback for `NEventOptionButton`. Upgrade/removal **confirm phase** — only Back + checkmark, no background PickCard. Treasure chest drops chest dwell after open (blocks infinite-gold re-trigger); Skip/proceed rescans. View Upgrades toggle + scroll strip on card-grid pick screens. Build green; installed to STS2 mods folder.

**Current state:** Green — installed; user smoke-test pending on quit dialog + event multi-step flows.

**File size flag:** HandTargetingOverlay.cs ~626 lines; ShopOverlay.cs ~535 lines — monitor before next feature batch.

**Next session:** User verify quit Yes/No + Tablet of Truth multi-step; if quit still fails, log dialog node types at runtime. Backlog B1 card-reward skip still open.

---

## 2026-06-20 — Shop purchases + hitbox alignment (v0.10.45 → v0.10.56)

**Files changed:** ShopOverlay.cs (~437 lines), ShopSelectionService.cs (new), ShopInventoryQuery.cs (new), ShopAlignmentDiagnostics.cs (new), DwellDebugOverlay.cs (new), HandTargetingOverlay.cs (+19), DeckViewOverlay.cs (new), MapOverlay.cs, EventOverlay.cs, DwellSettings/SettingsStore/ModConfigEntries (showHitboxOverlay toggle), mod_manifest/ModEntry v0.10.56

**What worked:** Shop fully buyable via dwell — cards (hitbox on slot clickable child), relics/potions (offset numbers), card removal, merchant rug. Purchase path: `OnTryPurchase(inventory)` on the specific slot (position-independent). Deck view scroll strip no longer hidden by map/shop sync. Hitbox debug overlay (green boxes + red mouse marker) diagnosed misalignment; fix confirmed by user — all shop cards buyable. Debug overlay off by default in v0.10.56.

**Current state:** Green — shop confirmed working; v0.10.56 installed to STS2 mods folder.

**File size flag:** HandTargetingOverlay.cs = **770 lines** (near 800 cap — extract mode coordinator before next feature batch). ShopOverlay.cs = **~530 lines** (over 500 — monitor). MapOverlay.cs grew significantly.

**Next session:** Implement planned batch (see plan): native End Turn button dwell, all event options → offset numbers, back/eye button hitbox fix, game over Continue, main menu buttons. Optional: shorten shop menu cooldown ("two hovers" feel). Refactor HandTargetingOverlay before adding modes.

---

## Backlog — known bugs & remaining screens (living list)

### Bugs — fix before new screens

| # | Issue | Symptom | Likely cause |
|---|--------|---------|--------------|
| B1 | **Loot screen — card reward** | Gold/relic numbered picks work. Card-reward option: dwell number **skips** the card instead of opening the 3-card draft. Clicking the real UI opens pick-1-of-3 correctly. | `RewardCollectedFrom` may be wrong for card-type `NRewardButton` (claims/skips vs opens draft). Card draft uses `NCardRewardSelectionScreen` + `NCardRewardAlternativeButton` — **not** in `IsPileSelectScreen`, so no numbered overlay after draft opens. May need `ForceClick` on card reward button to open picker, then numbered picks on draft screen via `OptionSelected` / card holders. |
| B2 | **Stale overlay buttons** | After leaving loot (or picking a reward), old **1 / 2 / GO** overlays remain visible on the next screen (e.g. card select). Can persist into **main menu** after quitting. | `OverlayModeService` prioritizes `Rewards` over `PileSelect`; `NRewardsScreen` may stay in tree while sub-screens open. `RewardsOverlay.Hide()` not called on every transition. Canvas layers on scene root may outlive run end if `TearDown` skipped. Need: aggressive hide when anchors gone; detect `NCardRewardSelectionScreen` and switch mode; full teardown when `!RunManager.IsInProgress` or rewards screen not visible. |
| B3 | **Enemy slot order mismatch** | New minions/creatures **are** detected and get a target button, but the number does **not** match left-to-right screen order (leftmost = 1, next = 2, …). After spawns/stacks, card button **3** may hit a different enemy than the one visually third from the left. | `EnemyOrderService` sorts by `CombatId`, not on-screen X. `CombatId` order ≠ visual left-to-right when minions spawn mid-fight or enemies overlap. Fix: map alive `Creature` → `NCreature` node (`Entity` prop), sort by `GlobalPosition.X` (or hitbox center), use same order for card buttons **and** targeting in `CardPlayService`. |
| B4 | **Large hand — card buttons overlap End Turn** | With 9–10 cards, rightmost (e.g. 9th) card button sits on top of End Turn button. Button row also **drifts upward** along the fan: ~10 px above left cards, ~30 px above right cards. | Buttons centered above card (`GapAboveCard = 28`); fan layout + anchor rect mismatch widens gap on outer cards. End Turn fixed at viewport center (~52% Y). Fix: keep buttons tighter to card top (consistent ~10 px); consider left-side offset (like loot screen) for outer cards; or nudge End Turn when `handSize >= 9`. Files: `CardButtonRow.cs`, `CardAnchorService.cs`, `EndTurnOverlay.cs`. |

### Combat UX — consistency (not started)

| # | Feature | Goal | Approach |
|---|---------|------|----------|
| C1 | **Enemy number labels on screen** | Show **1 / 2 / 3 / 4** above each alive enemy so on-screen slot matches card target buttons. | New `EnemyLabelOverlay` in combat play mode: find visible `NCreature` nodes, match `Entity` to `EnemyOrderService` list (after B3 screen-X sort), place small label at `GetTopOfHitbox()` / `Hitbox` rect. Hide when not in combat or enemy dead. Display-only (no dwell needed unless user wants it later). |

### New screens — not started

| # | Screen | Game types (starting points) |
|---|--------|------------------------------|
| N1 | Events | `NEventRoom`, `NEventOptionButton` |
| N2 | Chests / treasure | `NTreasureRoom`, `NTreasureButton`, relic holders |
| N3 | Shop | `NMerchantRoom`, `NMerchantButton` |
| N4 | Rest site / fire | `NRestSiteRoom`, `NRestSiteButton` |
| N5 | Ancient encounters | `NAncientEventLayout`, dialogue hitboxes; may reuse event + loot patterns |
| N6 | **Back button** (cross-cutting) | `NBackButton` — show whenever visible, any non-combat screen |
| N7 | **Main menu** | `MainMenuButton`, `MainMenuTextButton`, `MainMenuContinueButton` — dwell buttons for play/continue/settings/etc. when `!RunManager.IsInProgress` |
| N8 | **View upgrades checkbox** | `UpgradePreviewTickbox` / `NTickbox` on main menu or upgrade screen — dwell toggle for “view upgrades” preview |
| N9 | **Mid-combat “Choose a Card”** (e.g. Knowledge Demon boss) | During boss fight, modal **Choose a Card** — pick **1 of 2** (sometimes more) offered cards (e.g. Disintegration vs Mind Rot). No numbered overlays today. `NChooseACardSelectionScreen` is already in `IsPileSelectScreen`, but combat may stay in `CombatPlay` mode so `PileSelectOverlay` never wins; hand/End Turn overlays may still show. Need: detect this screen during combat, switch to pile-select overlay, numbered buttons on each offered card, hide combat overlays until choice resolves. |

### Optional / deferred

- Potion slot buttons (under each holder, before popup)
- Utility bar custom keybindings
- README update for v0.10.x
- v0.10.8: longer end-turn/menu dwell + menu activation cooldown (installed when game closed)

---

## 2026-06-19 — Async logger + perf, rooms, universal back button, combat bleed-through, ancient numbers (v0.10.31 → v0.10.37)

**Files changed:**
- New: `RoomOverlay.cs` (161), `BackButtonOverlay.cs` (85), `CombatViewSuppressionQuery.cs` (61)
- `ModLogger.cs` — async buffered queue + 250 ms background flush (per-line disk I/O was a combat-lag source); `GD.Print` only for WARN/ERROR
- `OverlayPerfDiagnostics.cs` — named ms + call-count buckets, 3 s summary; `NodeQuery.cs` — tree-walk counters (FindAll calls / nodes visited)
- `OverlayModeService.cs` (319) — travelable map now outranks a lingering rewards screen (bleed-through fix); added `Room` mode (`NRestSiteRoom` / `NTreasureRoom`, lowest priority)
- `RewardsOverlay.cs` — removed number buttons (direct hover-to-claim); per-item vertically-clipped hitboxes (hover item 2 no longer claims item 1)
- `PileSelectOverlay.cs` — reverted to direct card-body hover; card-reward Skip (`NCardRewardAlternativeButton`) now hoverable
- `EventOverlay.cs` (240) — ancient events (`AncientEventOptionButton`) use gold offset number buttons (hover option = relic tooltip, dwell the number = pick); normal events keep direct hover
- `RoomOverlay.cs` — rest-site options (`NRestSiteButton`), treasure/chest (`NTreasureButton` + generic clickable pass for the relic), proceed via E key
- `HandTargetingOverlay.cs` (755) — wired Room mode, universal Back button (`NBackButton`), combat-view suppression (deck/draw/exhaust/pile/map opened over combat)
- `ModEntry.cs` / `mod_manifest.json` → v0.10.37

**What worked (user-confirmed):**
- Lag: async logger removed the combat stall ("didn't stick out").
- Rest-site options + proceed (E key) confirmed working in the log; map/rewards bleed-through fixed.

**Pending user test:** universal back button, combat bleed-through suppression, chest **relic** selection (only Skip worked before), ancient offset numbers.

**Current state:** Green — compiles clean (0 warn / 0 err), installed v0.10.37.

**File size flag:** `HandTargetingOverlay.cs` = **755 lines** (over the 700 extract threshold, near the 800 cap) — extract the `Sync*Mode` / suppression helpers into a mode-coordinator before adding more. `OverlayModeService.cs` = 319.

**Next session:** Shop screen (`NMerchantRoom` / `NMerchantCard` / `NMerchantRelic` / `NMerchantPotion` + back/proceed). Refactor `HandTargetingOverlay` (>700). Capture a clean combat perf summary with logging ON.

---

## 2026-06-17 (evening) — Map, card-select, events, proceed/confirm hover + perf (v0.10.18 → v0.10.30)

**Files changed:**
- New: `MapOverlay.cs` (175), `MapSelectionService.cs` (24), `MapScrollService.cs` (25), `EventOverlay.cs` (82), `EventSelectionService.cs` (21)
- `OverlayModeService.cs` (214) — added `Map` + `Event` modes; ghost-rewards fix (rewards only counts with `HasVisibleChoices`); `DebugSnapshot()`
- `HandTargetingOverlay.cs` (525) — wired Map + Event sync paths, `ArmGrace` on screen open, `[Mode]` transition logging; removed per-frame `DwellHoverService.Reset()` from pile/rewards sync (the bug that kept native dwell from ever firing)
- `PileSelectOverlay.cs` (131) — **rewritten**: finds `NCardHolder` (not raw `NCard`), no number badges, card-body dwell at Menu timing, plus Confirm/Skip/Proceed buttons
- `PileCardSelectionService.cs` (49) — select via holder `Pressed` signal (NClickableControl hitbox has no Pressed; ForceClick didn't drive it)
- `DwellHoverService.cs` (147) — dwell grace/hysteresis + screen-open `ArmGrace` (suppress ~1 s + require cursor move)
- `RewardsOverlay.cs` (320) — loot-item hover-select; padded Proceed hitbox; proceed diagnostics
- `EnemyOrderService.cs` / `EnemyLabelOverlay.cs` / `HandLayoutDiagnostics.cs` — perf: idempotent Hide, prune-on-death vs full rescan, scan interval 30→90, gated diag disk writes
- `mod_manifest.json` / `ModEntry.cs` → v0.10.30

**What worked (user-confirmed):**
- **Map**: hover-to-select travelable nodes + off-to-the-side ▲/▼ hover-scroll arrows (default slowed to every 3rd frame).
- **Card-select hover** now works (root cause: cards are `NCardHolder`, select via holder `Pressed` signal). Badges removed; hover the card body. Dwell moved to Menu timing so picks aren't instant.
- **Loot card/item hover-select**.
- Ghost rewards screen no longer blocks pile/map modes.

**Shipped this session, not yet user-tested:**
- **Events (N1)**: `NEventRoom` → `Event` mode, hover option buttons (`NEventOptionButton`, ForceClick).
- **Pile Confirm/Skip/Proceed** buttons (`NConfirmButton`/`NChoiceSelectionSkipButton`/`NProceedButton`) as dwell targets (ForceClick, since "E" didn't work for card-reward proceed).

**Current state:** Green — builds 0 errors/0 warnings, installed v0.10.30.

**File size flag:** `HandTargetingOverlay.cs` 525 (>500, grew ~+90 this session — watch; extract a mode-dispatch helper if it keeps growing). `SettingsOverlay.cs` 659 (unchanged). All others <350.

**Next session:**
1. User to test event flow: options → choose-a-card → end Proceed/Confirm.
2. Optional: scroll-speed slider in F8 (map scroll currently a fixed slower default).
3. Backlog still open: shops (N3), rest sites (N4), chests (N2), back button (N6), main menu (N7), mid-combat Choose-a-Card (N9).

## 2026-06-17 — Loot Skip via E, enemy slot labels, dwell-time settings, perf trims (v0.10.17)

**Files changed:**
- New: `RewardSelectionService.cs`, `RewardsScreenQuery.cs`, `ControlHitboxService.cs`, `EnemyLabelOverlay.cs` (185)
- `RewardsOverlay.cs` (319) — reworked: numbered side buttons for reward choices + **native dwell on the real Skip/Proceed button** (utility-bar method)
- `EnemyOrderService.cs` (136) — sort alive enemies by on-screen X (B3 fix) + node cache (rescan every 30 frames)
- `DwellTiming.cs` / `DwellSettings.cs` / `SettingsStore.cs` / `ModConfigEntries.cs` / `ModConfigBridge.cs` — configurable **card** + **End Turn** hover times, **Enemy Slot Numbers** toggle, **Performance Logging** toggle
- `SettingsOverlay.cs` (659) — F8 panel +/- rows for the two hover times
- `HandTargetingOverlay.cs` (561) — enemy-label sync in combat play; hide on every transition
- `OverlayModeService.cs` — pile-select wins over rewards; `NCardRewardSelectionScreen` detected
- `UtilityBarOverlay.cs` — UI rescan 5→20 frames; `DwellHoverService.cs` — window mouse pos
- `mod_manifest.json` / `ModEntry.cs` → v0.10.17

**What worked:**
- **Enemy slot numbers (C1)** above each foe, sorted left-to-right matching card target buttons; toggle in settings; raised 32 px above hitbox.
- **Loot Skip / post-combat Proceed** now dwells the **real button and presses E** (ForceClick/RewardCollectedFrom did not advance it). User confirmed E is the working activation.
- **B3** enemy order now by hitbox X, not CombatId.
- Configurable hover times (card 0.5 s, End Turn 1.15 s defaults) via ModConfig + F8.
- Perf trims: enemy scan 8→30 frames, utility rescan 5→20, perf logging off by default + toggle. Measured in combat: total ≈6.5 ms/frame, handSync ≈3.4–3.9, getMode ≈0.3, dwell ≈1.8 — comfortably under 16.6 ms.

**Current state:** Green — builds clean (0 warn/0 err). **v0.10.17 DLL build is NOT yet copied in** (game was running, file locked); last installed in-game is v0.10.16. Re-run `install.ps1` with STS2 closed to land the E-key Proceed fix.

**File size flag:** `SettingsOverlay.cs` 659 lines and `HandTargetingOverlay.cs` 561 lines — both >500 (cap 800). Consider extracting before next growth (e.g. split SettingsOverlay panel-build vs input-routing).

**Next session:** Confirm loot Skip/Proceed E-press works in game after install; if loot row hover still wanted, revisit. Then optional: shave `handSync` (rebuild card buttons only on hand change); B1 card-reward draft; remaining screens N1/N6/N7.

---

## 2026-06-15 — v0.10.7 loot GO + potion popup (installed)

**What worked:** GO proceed via `ForceClick`; potion popup Use/Discard numbered buttons; installed v0.10.7.

**Current state:** Partial — loot gold/relic OK; card reward + stale overlays broken (B1, B2).

**Next session:** Fix B1/B2 (loot); B3 (enemy left-to-right order) + C1 (labels on enemies); B4 (large-hand button position); then N6 Back, N7 main menu, N8 view-upgrades tickbox.

---

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
