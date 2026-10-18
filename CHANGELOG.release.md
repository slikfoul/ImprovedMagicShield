# Changelog

## 1.2.2
- Add an independent, optional purple health-bar shell for other shielded players and creatures, with a semicircular outer edge and rectangular opening; its external contour shows the remaining fraction of the actual applied shield.

## 1.1.1
- Show player protection above native player names, matching the summoned skeleton layout and vertical-offset controls.
- Fade overhead protection with the shielded character's own casts, damage and combat activity, using the same five-second display and half-second fade as the local bar.

## 1.0.0
- Refresh a magic shield when the new cast's full protection is at least its remaining protection, restoring the new capacity and duration while preserving stronger active shields.
- Display your remaining / maximum protection in a native-style purple resource bar, with capacity-based length, adjustable segment scale, a fixed HUD position and automatic fading outside combat.
- Show optional remaining-protection numbers above summoned skeleton names and below other characters' native name and health blocks, with adjustable size and vertical offset.
- Share current protection values between modded clients while keeping your own overhead number hidden and each display independently configurable.
- Customize shield color, glow, light brightness, opacity and ovality through a separate Magic Shield Effect switch, disabled by default, with automatic suspension alongside active EffectsControl shield settings or SubtleShield.
- Enable or disable improved shield behavior and displays through ConfigurationManager while preserving active shields and saved settings.
