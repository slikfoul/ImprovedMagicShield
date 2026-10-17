# Improved Magic Shield

Refresh a magic shield when its new full protection is **at least** the protection remaining on the active shield.

- Accepted cast: remove previously absorbed damage, apply the new cast's full protection and item-level duration, and restart its timer.
- Weaker cast: keep the active shield's protection, absorbed damage, duration, timer and source unchanged.
- No active shield: use normal Valheim application.
- Applies to yourself and any target already eligible for the vanilla shield. It does not add eligible targets or change casting costs, range, damage absorption or shield-break behavior.

| Current full protection | Already absorbed | Remaining | New full protection | Result |
| ---: | ---: | ---: | ---: | --- |
| 500 | 450 | 50 | 100 | Full 100 protection and a fresh timer |
| 500 | 200 | 300 | 100 | Active shield unchanged |
| 500 | 200 | 300 | 500 | Full 500 protection and a fresh timer |
| 500 | 400 | 100 | 100 | Full 100 protection and a fresh timer |

New skill level and item level come from the incoming vanilla cast. World-level protection follows Valheim's normal calculation.

## Configuration

Open the in-game BepInEx ConfigurationManager (normally F1) and find **Improved Magic Shield**. Settings are also saved in `BepInEx/config/Slikfoul.ImprovedMagicShield.cfg`.

- **Enabled** (default: on): enable improved refreshes and labels. Turning this off restores vanilla behavior for subsequent casts and removes this mod's labels without removing active shields.
- **Show remaining protection** (default: off): display `Shield: 123` above shields within 60 metres of the camera. The number is the current remaining protection, rounded down to a whole number. This toggle controls your labels; it continues supplying values to other clients.
- **Show local shield bar** (default: on): show your own `Shield 250 / 500` bar above the native stamina/Eitr/adrenaline resources. The fill uses the actual remaining/full capacity of your current shield. It hides when the shield is exhausted or expired, you die/teleport, the HUD is hidden, or the mod is disabled. This setting is independent of overhead labels.
- **Label scale (%)** (default: 100; range: 50 to 300): change label size in whole percent.

The mod publishes remaining protection through the character owner's existing ZDO, with a timestamp and owner identity. Remote labels require this mod enabled on the owning peer, follow normal ZDO replication delay and expire after two seconds without fresh data. Ownership changes invalidate the previous owner's display data. An unmodified owner provides no reliable remote label. These labels do not synchronize or transfer the shield effect itself.

## Installation and multiplayer

Requires BepInExPack for Valheim. Install through r2modman, or place `ImprovedMagicShield.dll` under `BepInEx/plugins/ImprovedMagicShield` for a manual installation.

Install on every participating client for consistent player shield behavior. Valheim processes a player's received shield on that player's owning client: installing only on the caster or only on a dedicated server does not change an unmodified recipient's shield refresh.

Install on the dedicated server as well when server-owned eligible creatures should use this behavior. Creature ownership can move between peers, so installation on all clients and the server is recommended for consistent behavior on all vanilla eligible targets. A listen-server host uses its client installation. The mod adds no RPCs and no server-enforced version requirement; unmodified owners retain vanilla behavior.

## Development status

Version 0.0.1 is a local development build. Automated logic and installed-game IL checks do not establish live Unity or multiplayer correctness. Test self casts, casts between players with different skill/item levels, eligible creatures and ownership changes in game before relying on the mod. The native-canvas HUD follows the game's HUD scaling and reserves space above all three resource bars; its layout and lifecycle still need live checks at your resolution, UI scale, controller mode and with other HUD mods.
