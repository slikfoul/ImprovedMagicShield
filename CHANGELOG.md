# Changelog

## 0.3.1
- Use native resource-row spacing for the fixed shield bar.
- Show protection on an alerted enemy's target notification without reacting to attack, block or bow inputs.

## 0.3.0
- Display the shield bar in a fixed lower HUD row, independent of other resource visibility and animations.
- Show shield protection during combat and briefly after casts or damage, with a smooth fade after five quiet seconds.

## 0.2.2
- Display protection values as numbers: remaining / maximum in the local bar and remaining protection above other characters.

## 0.2.1
- Render remaining protection within native character headers, using their font, canvas and placement.
- Support short shield bars with independently readable captions across the full protection-per-segment range.

## 0.2.0
- Scale the local shield bar's overall width with the applied shield's full protection.
- Configure protection per native HUD segment while keeping the remaining/full text readable.
- Show summoned skeleton protection above its native name and adjust overhead labels with a vertical-offset slider.

## 0.1.1
- Show Magic Shield Effect first in the Magic Shield configuration section.
- Place remaining-protection labels below the visible native name and health block, with space for larger label sizes.

## 0.1.0
- Customize shield color, glow, light brightness, opacity and ovality with a separate Magic Shield Effect switch, disabled by default.
- Use local visual settings for existing and newly created shield effects, with restoration when disabled and automatic suspension while other shield visual controls are active.

## 0.0.3
- Show overhead protection only on other characters, independently of the local shield bar.
- Anchor overhead labels to the rendered character and the current main-camera frame.

## 0.0.2
- Display the local shield bar using Valheim's native resource sprites, frame, fill and typography.

## 0.0.1
- Compare the new shield's full protection with the active shield's remaining protection.
- Restore full new protection and duration on accepted casts, including equal protection.
- Preserve the entire active shield when a weaker cast is rejected.
- Toggle the mod and optional remaining-protection labels in ConfigurationManager, with an integer label scale in percent.
- Show a separate local shield HUD bar with the current remaining/full capacity and proportional fill.
