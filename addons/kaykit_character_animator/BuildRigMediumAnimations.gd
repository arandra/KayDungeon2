@tool
extends EditorScript

const SOURCE_DIR := "res://addons/kaykit_character_animator/Animations/gltf/Rig_Medium"
const OUTPUT_PATH := "res://addons/kaykit_character_animator/RigMedium_Animations.tres"

func _run() -> void:
	var library := AnimationLibrary.new()
	var sources := _list_animation_sources(SOURCE_DIR)
	var added := 0
	var skipped := 0

	for path in sources:
		var packed = load(path)
		if packed == null or not (packed is PackedScene):
			push_warning("Skip non-scene: %s" % path)
			continue
		var instance = packed.instantiate()
		var player = _find_animation_player(instance)
		if player == null:
			push_warning("No AnimationPlayer in: %s" % path)
			instance.queue_free()
			continue
		for name in player.get_animation_list():
			if library.has_animation(name):
				skipped += 1
				continue
			var anim = player.get_animation(name)
			if anim == null:
				continue
			library.add_animation(name, anim.duplicate())
			added += 1
		instance.queue_free()

	var err = ResourceSaver.save(library, OUTPUT_PATH)
	if err != OK:
		push_error("Failed to save AnimationLibrary: %s (code %s)" % [OUTPUT_PATH, err])
		return
	print("AnimationLibrary rebuilt: %s (added %d, skipped %d)" % [OUTPUT_PATH, added, skipped])

func _list_animation_sources(dir_path: String) -> Array[String]:
	var results: Array[String] = []
	var dir = DirAccess.open(dir_path)
	if dir == null:
		push_error("Missing animation dir: %s" % dir_path)
		return results
	dir.list_dir_begin()
	while true:
		var name = dir.get_next()
		if name == "":
			break
		if name.begins_with("."):
			continue
		var full = dir_path.path_join(name)
		if dir.current_is_dir():
			continue
		var lower = name.to_lower()
		if lower.ends_with(".glb") or lower.ends_with(".gltf"):
			results.append(full)
	dir.list_dir_end()
	results.sort()
	return results

func _find_animation_player(root: Node) -> AnimationPlayer:
	if root is AnimationPlayer:
		return root
	for child in root.get_children():
		var found = _find_animation_player(child)
		if found != null:
			return found
	return null
