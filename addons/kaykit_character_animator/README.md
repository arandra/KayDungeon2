# KayKit Character Animator

This folder packages the modular animation setup for reuse across projects.

## Install (copy)
1. Copy `addons/kaykit_character_animator` into your project's `addons/` folder.
2. In Godot, open `Project Settings > Plugins` and enable **KayKit Character Animator**.

## Use
- Add `CharacterAnimator.tscn` as a child of your character root.
- Play animations through the `AnimationPlayer` named `CharacterAnimator`.
- The animation library lives at `res://addons/kaykit_character_animator/RigMedium_Animations.tres`.

## Notes
- The editor plugin provides a menu item: **Add CharacterAnimator**.
