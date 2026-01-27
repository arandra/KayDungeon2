@tool
extends EditorPlugin

const ANIMATOR_SCENE_PATH = "res://addons/kaykit_character_animator/CharacterAnimator.tscn"

var _menu_label := "Add CharacterAnimator"

func _enter_tree():
	add_tool_menu_item(_menu_label, Callable(self, "_on_add_character_animator"))

func _exit_tree():
	remove_tool_menu_item(_menu_label)

func _on_add_character_animator():
	var editor = get_editor_interface()
	var scene_root = editor.get_edited_scene_root()
	if scene_root == null:
		push_warning("Open a scene before adding CharacterAnimator.")
		return

	var selection = editor.get_selection().get_selected_nodes()
	if selection.is_empty():
		push_warning("Select a node to add CharacterAnimator under.")
		return

	var target = selection[0]
	if target.has_node("CharacterAnimator"):
		push_warning("CharacterAnimator already exists under the selected node.")
		return

	var animator_scene = load(ANIMATOR_SCENE_PATH)
	if animator_scene == null:
		push_error("Failed to load: " + ANIMATOR_SCENE_PATH)
		return

	var animator_instance = animator_scene.instantiate()
	animator_instance.name = "CharacterAnimator"
	target.add_child(animator_instance)
	animator_instance.owner = scene_root
