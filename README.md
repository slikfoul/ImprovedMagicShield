# Improved Magic Shield

Improved Magic Shield refreshes a magic shield when the new cast's full protection is **at least** the protection remaining on the active shield.

- **Accepted cast:** discard previously absorbed damage, apply the new cast's full protection and item-level duration, and restart its timer.
- **Weaker cast:** preserve the active shield's capacity, absorbed damage, duration, timer, parameters and source.
- Applies to yourself and any target already eligible for the vanilla shield. Casting costs, range, target eligibility, damage absorption and shield-break behavior follow vanilla.

| Current full protection | Already absorbed | Remaining | New full protection | Result |
| ---: | ---: | ---: | ---: | --- |
| 500 | 450 | 50 | 100 | Full 100 protection and a fresh timer |
| 500 | 200 | 300 | 100 | Active shield unchanged |
| 500 | 200 | 300 | 500 | Absorbs up to 500 damage; duration refreshed |
| 500 | 400 | 100 | 100 | Full 100 protection and a fresh timer |

Incoming skill and item levels come from the new vanilla cast. The world-level bonus follows Valheim's normal shield calculation. Equal protection is accepted.

## Screenshot

The lower bar shows your remaining and full shield protection. The optional number above another character shows their remaining protection.

![Local shield protection bar and another player's remaining protection](https://raw.githubusercontent.com/slikfoul/ImprovedMagicShield/main/assets/screenshots/shield-displays.png)

## Configuration

Open the in-game BepInEx ConfigurationManager (normally F1) and find **Improved Magic Shield**. Settings are saved in `BepInEx/config/Slikfoul.ImprovedMagicShield.cfg`; saved values take precedence over defaults.

Display switches are independent:

| Setting | Default | Behavior |
| --- | --- | --- |
| **Enabled** | On | Master switch for improved refreshes and displays; each display also has its own switch. Turning this off returns subsequent casts to vanilla behavior and hides the mod's displays without removing active shields. |
| **Show local shield bar** | On | Show your own `250 / 500` bar in a fixed row above the normal lower resource bars. The fill uses the actual remaining/full capacity of your current shield. It fades out after five quiet seconds, even when damaged, and returns on a cast, shield damage or combat activity. |
| **Show remaining protection** | Off | Show `123` above player and summoned skeleton names and below the visible name/health block of other shielded creatures within 60 metres of the camera. Never shown on your own character, even when the local HUD bar is off. Appears on that character's shield cast, shield damage or combat, then fades after five quiet seconds. Your own combat does not reveal other characters' labels. This does not enable or disable the local HUD bar. |
| **Show shield health outline** | Off | Draw a purple shell with a semicircular outer edge and a rectangular inner opening around other shielded players' and creatures' visible health bars. The visible contour spans the full bar at full shield protection and its left half at 50%, without drawing a vertical line through health. It does not change native health or enable overhead numbers. |

The health outline uses the current shield's actual remaining/full protection, including another caster's shield: `200 / 200` and `700 / 700` both cover the whole bar, while `150 / 300` covers half. It follows the native health bar's position, scale and visibility, remains visible while that bar and the shield are active, and disappears when the shield runs out. It is independent of the number display's combat fade. The owning peer needs this mod's capacity snapshots (version 1.2.0 or newer); missing or stale data hides the outline rather than inventing a percentage. Its switch is independent of **Magic Shield Effect**.

**Label scale (%)** controls the overhead label size: default **80**, integer range **50–300**. It does not change the lower resource bar's size; that bar inherits the game's HUD canvas scaling.

**Label vertical offset** moves overhead labels from their automatic position: default **−20**, integer range **−200–200**. Positive values move them up; negative values move them down. The offset follows the native header canvas scale and does not move the local shield bar.

**Protection per segment** controls the lower bar's length: default **50**, integer range **10–500**. Each native 32-unit HUD segment represents this much full shield protection. At the default scale, a 200-protection shield spans four segments and a 700-protection shield spans fourteen. The bar uses the actual applied shield's maximum, including shields cast by someone else. Damage changes the fill, while an accepted replacement changes the overall length to match the new shield. The frame has no minimum width imposed by the text. On short bars, the caption remains centered at its native font size and may extend beyond the frame; other resource values do not affect the shield bar's length.

The lower bar is local-player-only and reuses the native Eitr resource artwork: frame, background, tiled fill, slow damage indicator and text font/material. It inherits the native resource thickness and HUD scaling, with a purple shield fill. Its position stays fixed when stamina, Eitr or adrenaline appears, disappears, changes width or animates, with the same row spacing as the native resource bars. A new shield or accepted recast shows it for five seconds; shield damage and vanilla notifications that an alerted enemy targets you keep it visible or reveal it again. Attack, block and bow inputs alone do not reveal it; neither does a non-alerted detection report. After five seconds without these events it fades out over half a second, including damaged shields. A rejected weaker cast does not reset the shield timer. After an accepted replacement with a 100-protection shield it shows `100 / 100`, even if the previous shield's capacity was 500. It hides when no protection remains, the effect expires, the player dies or teleports, the vanilla HUD is hidden, or the mod is disabled. The native-style layout still needs live validation at different resolutions, UI scales and with other HUD mods.

Overhead labels show remaining protection rounded down to a whole number. They use the same five-second display period and half-second fade as the lower bar, including damaged shields. Each label follows the shielded character's own activity: casts, absorbed damage and alerted-enemy target reports for players; shield casts, damage and active alerted AI targets for creatures such as summoned skeletons. The observing player's combat state does not reveal other characters' numbers. Player and summoned skeleton labels sit above the top of their native name; other creature labels sit below the visible native name and health block. The gap follows the HUD canvas scale. Increasing label size extends away from the native text rather than over it. Labels reuse the native name font and share its canvas and transform hierarchy, so camera movement, HUD scaling and pixel alignment are applied together. A native label hides with its header instead of jumping to a different anchor. If no native header has been available, a rendered-character fallback is used. Positions are prepared for the current main-camera frame. Your own overhead label is always hidden, independently of the local bar setting. Remote numbers and the last shield/combat activity time are display-only values supplied through the character owner's ZDO with a snapshot timestamp and owner identity. They follow normal replication delay, expire after two seconds without fresh data, and reject data from a previous owner. The owning peer must have this mod enabled to supply current numbers and activity. Update participating clients to the same build: older builds cannot supply the activity snapshots needed by these labels. Hiding your overhead labels does not stop supplying values to other modded clients.

### Magic Shield appearance

**Magic Shield Effect** is a separate switch, **off by default**. It enables all five appearance controls below on this client, including color. There is no separate Change color switch.

| Setting | Default | Range |
| --- | ---: | ---: |
| Color | 269 | 0–360 hue |
| Glow | 72 | 0–100 |
| Light brightness | 35 | 0–100 |
| Opacity | 30 | 0–100 |
| Ovality | 50 | 0–100 |

All sliders use whole numbers. Glow, light brightness and opacity are percentages of the original appearance. Ovality narrows the bubble horizontally while preserving height and colliders. These controls affect only appearance, including visible shields on other characters; protection, duration and the shield HUD are unchanged. Turning the switch off restores the captured appearance of active effects.

To use this block alongside EffectsControl, turn off **Magic shield** in EffectsControl, then turn on **Magic Shield Effect** here. While EffectsControl's shield controls or SubtleShield are active, this block is automatically suspended. Each client chooses its own appearance settings.

## Installation and multiplayer

Requires **BepInExPack for Valheim**. Download the ZIP from [GitHub Releases](https://github.com/slikfoul/ImprovedMagicShield/releases/latest) and import it as a local mod in r2modman, or place `ImprovedMagicShield.dll` under `BepInEx/plugins/ImprovedMagicShield`. ConfigurationManager is optional and provides the in-game settings UI.

Install on **every participating client** for consistent player shield behavior. Valheim processes a player's received shield on that player's owning client. Installing only on the caster does not change an unmodified recipient's shield refresh.

For player-owned characters, the **recipient's client** decides which refresh rules apply:

| Recipient has the mod enabled | Caster has the mod | Result on the recipient |
| --- | --- | --- |
| Yes | Yes or no | Improved refresh rules apply. |
| No | Yes or no | Vanilla refresh rules apply. |

A modded recipient therefore benefits even when the caster has no mod. An unmodified recipient remains vanilla even when the caster has the mod.

The mod adds no custom RPCs or server-enforced version requirement. Unmodified owners retain vanilla refresh behavior. It does not synchronize or transfer the status effect itself.

## Building and testing

Use the .NET SDK, an installed Valheim client and BepInEx core assemblies from an existing r2modman profile. The plugin targets .NET Framework 4.6.2; the managed check runner targets .NET 9.

```powershell
dotnet build ImprovedMagicShield.csproj -c Release
./Test.ps1
./BuildPackage.ps1 -SkipBuild
```

Override the game and BepInEx profile paths when needed:

```powershell
dotnet build ImprovedMagicShield.csproj -c Release "-p:ValheimDir=D:\Games\Valheim" "-p:ModProfile=D:\ModProfiles\Default"
./Test.ps1 -ValheimDir 'D:\Games\Valheim'
```

The check runner references Harmony/Cecil from the default r2modman profile; its `BepInExCore` MSBuild property can be overridden for a different reference location. No game assemblies or BepInEx binaries are included in this repository.

`BuildAndInstall.ps1` builds, checks, packages and registers the local build in r2modman Default. It accepts explicit `DataRoot`, `Profile` and `ValheimDir` overrides. Close Valheim and r2modman before installing; the script stops if either is running. It preserves configuration, enabled state, existing trust and other package entries, backs up replaced files, and refuses downgrades. Package creation or local installation does not publish anything.

## License

[MIT](LICENSE).
