# Improved Magic Shield

**Version 0.1.0**

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

## Configuration

Open the in-game BepInEx ConfigurationManager (normally F1) and find **Improved Magic Shield**. Settings are saved in `BepInEx/config/Slikfoul.ImprovedMagicShield.cfg`; saved values take precedence over defaults.

The three switches are independent:

| Setting | Default | Behavior |
| --- | --- | --- |
| **Enabled** | On | Master switch for improved refreshes and displays; each display also has its own switch. Turning this off returns subsequent casts to vanilla behavior and hides the mod's displays without removing active shields. |
| **Show local shield bar** | On | Show your own `Shield 250 / 500` bar above the native stamina, Eitr and adrenaline resources. The fill uses the actual remaining/full capacity of your current shield. |
| **Show remaining protection** | Off | Show `Shield: 123` above other shielded characters within 60 metres of the camera, never your own character, even when the local HUD bar is off. This does not enable or disable the local HUD bar. |

**Label scale (%)** controls the overhead label size: default **100**, integer range **50–300**. It does not change the lower resource bar's size; that bar inherits the game's HUD canvas scaling.

The lower bar is local-player-only and reuses the native Eitr resource artwork: frame, background, tiled fill, slow damage indicator and text font/material. It inherits the native resource thickness and HUD scaling, with a purple shield fill. After an accepted replacement with a 100-protection shield it shows `100 / 100`, even if the previous shield's capacity was 500. It hides when no protection remains, the effect expires, the player dies or teleports, the vanilla HUD is hidden, or the mod is disabled. The native-style layout in 0.0.2 still needs live validation at different resolutions, UI scales and with other HUD mods.

Overhead labels show remaining protection rounded down to a whole number. They follow the rendered top of each other character, with screen positions calculated for the current main-camera frame. Your own overhead label is always hidden, independently of the local bar setting. Remote numbers are display-only values supplied through the character owner's ZDO with a timestamp and owner identity. They follow normal replication delay, expire after two seconds without fresh data, and reject data from a previous owner. The owning peer must have this mod enabled to supply current numbers. Hiding your overhead labels does not stop supplying values to other modded clients.

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

Requires **BepInExPack for Valheim**. To test a local build, place `ImprovedMagicShield.dll` under `BepInEx/plugins/ImprovedMagicShield`, or import a locally built package into r2modman. ConfigurationManager is optional and provides the in-game settings UI.

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
