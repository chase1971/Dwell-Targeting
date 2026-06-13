# Dwell Targeting — STS2 accessibility mod

Large **1 / 2 / 3 / 4** buttons appear above each playable card in combat. Click a number to play that card on that enemy (left-to-right). Non-targeting cards show a single **▶ Play** button.

No mouse dragging, no screen-coordinate learning — the mod uses the game's own `PlayCardAction` API.

## Your run data (enemy counts)

From your save history (`114` runs analyzed):

| Enemies | Fights |
|---------|--------|
| 1 | 1,108 |
| 2 | 384 |
| 3 | 313 |
| 4 | 84 (max) |

The mod reads **live** enemy list each combat and shows only the buttons needed (1–4).

## Install this mod

```powershell
cd "C:\Users\chase\Documents\Programs\sts2-dwell-targeting"
.\install.ps1
```

Requires .NET 9 SDK (you already have it). Game path defaults to `D:\SteamLibrary\steamapps\common\Slay the Spire 2`.

Then launch Slay the Spire 2 → **Play Modded** → enable **Dwell Targeting**.

## Keep vanilla + modded saves in sync

Modded runs save to a separate folder:

- Vanilla: `%APPDATA%\SlayTheSpire2\steam\<SteamID>\profile1\saves\`
- Modded: `%APPDATA%\SlayTheSpire2\steam\<SteamID>\modded\profile1\saves\`

Recommended: install **BetterSaves** from [Nexus Mods #372](https://www.nexusmods.com/slaythespire2/mods/372). It bidirectionally syncs vanilla ↔ modded saves. Use **Full Sync** if you want progress and run history shared.

Alternatives:

- **UnifiedSavePath** (Nexus #6) — one shared folder always (simpler, merges everything)
- **SlaySP2Manager** — external save pairing/sync tool

After first BetterSaves launch it asks **Use Vanilla** or **Use Modded** as the source of truth — pick vanilla to keep your current profile.

## Logs

Mod log (easier to read than godot.log):

`%APPDATA%\SlayTheSpire2\logs\dwell-targeting.log`

Also mirrored to `godot.log` with `[DwellTargeting]` prefix. After a combat test, search for `TryPlay`, `ButtonDown`, or `TryManualPlay`.

## Rebuild after code changes

```powershell
.\install.ps1
```

Restart the game (close fully before reinstalling).
