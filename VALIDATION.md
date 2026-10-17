# Improved Magic Shield 0.0.1 verification

## Automated coverage

The Release build has no compiler warnings or errors. Run `./Test.ps1` for production helper and UI lifecycle checks using managed game/Unity test doubles, plus installed-game IL checks. The exact game assembly and compiled DLL SHA-256 are recorded in `artifacts/checks-0.0.1.json`.

Covered behavior includes:

- All requested shield replacement examples, equality, full reset of absorbed damage and time, and rejection without changing capacity, damage, time, duration, source or variant.
- Incoming skill/item-level parameters and the positive-world-level bonus. A small interpreter executes the installed game's numeric `SE_Shield.SetLevel` IL and compares the result with the mod's capacity calculation.
- Existing shield branches in both SEMan overloads; new/non-shield effects and `resetTime=false` follow vanilla.
- The installed Character refresh block is matched and transformed; its entry branch label is preserved, its source update is kept for accepted casts, and an emitted managed version executes rejection without calling SetAttacker. Changed or ambiguous hook layouts fail closed.
- The installed Character and SEMan hash/RPC paths retain vanilla ownership checks. No target-filtering, casting or ownership methods are patched.
- Disabled mechanics follow vanilla for subsequent operations without removing the active effect.
- Overhead labels use actual remaining protection, respond to damage and scale, and hide when disabled. Network display test doubles reject old-owner and expired snapshots.
- The actual local HUD implementation is exercised with UI test doubles: native canvas parent, space above stamina/Eitr/adrenaline, proportional fill, current capacity after recast, independent toggle, death, teleport, hidden HUD, no shield, no local player, HUD recreation and cleanup.
- Compiled defaults are Enabled=true, Show local shield bar=true and Show remaining protection=false. The scale is an integer percent setting (50 to 300, default 100). Configuration uses BepInEx Bind and does not overwrite saved values.

Local packaging and installation are checked separately from gameplay. BuildPackage.ps1 verifies package structure, version agreement, icon dimensions and archive content hashes. RegisterLocalMod.ps1 checks closed applications, preserves profile configuration and package state, backs up replaced files, and validates the installed DLL. Generated reports, profiles, backups and binary packages are excluded from source control.

## Remaining live checks

No live Unity gameplay, actual HUD rendering or multiplayer session has been tested. The managed tests do not establish those behaviors.

1. With the mod enabled, cast on yourself and verify the damage/recast examples in README, including equality and a weaker refused cast near expiry. Confirm the refused cast does not extend duration.
2. Toggle Enabled off with a shield active. Confirm the effect remains, subsequent casts follow vanilla, and both mod displays disappear. Re-enable without resetting saved display settings.
3. Leave overhead labels off and enable only the local bar. Verify current remaining/full values and fill after damage, accepted replacement, rejected replacement, expiry and break. Test death/respawn, teleport, main menu, hidden HUD and HUD recreation.
4. Check native HUD layout with stamina, Eitr and adrenaline visible together, at different resolutions, HUD scales and controller layouts. Other HUD mods may reposition these resources; the bar reserves space using their current transforms, but live compatibility is unverified.
5. Enable overhead labels separately and change their scale. Verify the local label updates immediately after damage and remote values follow actual ZDO replication. The owner publishes changed values at most ten times per second and a heartbeat twice per second while shielded. Expire stale data after two seconds and invalidate data from a previous owner.
6. Use two modded clients with different skill/item levels. Cast on each other in both orders and verify the recipient owner's outcome. Test eligible creatures with server ownership and peer ownership changes. An unmodified recipient remains vanilla; a server-only installation cannot change that player's shield processing.

## Compatibility boundary

The strength calculation matches the inspected installed vanilla SE_Shield.SetLevel. Another mod that changes shield calculation or the same refresh blocks needs compatibility review. The mod adds no custom RPC or version enforcement and does not synchronize the status effect itself. Overhead numbers are display-only owner-published data.

## Source and release status

Version 0.0.1 is under testing. This repository contains source, tests, build/package/install tools and documentation. No binary release, tag or Thunderstore publication is included. The archive manifest links to the actual GitHub repository. Building or registering a local package does not publish it.
