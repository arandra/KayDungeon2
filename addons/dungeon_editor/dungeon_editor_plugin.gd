@tool
extends EditorPlugin

var _dock: VBoxContainer
var _target_path: LineEdit
var _status: Label
var _export_dialog: EditorFileDialog
var _import_dialog: EditorFileDialog

func _enter_tree() -> void:
	_build_ui()
	add_control_to_dock(DOCK_SLOT_RIGHT_UL, _dock)

func _exit_tree() -> void:
	if _dock:
		remove_control_from_docks(_dock)
		_dock.queue_free()
		_dock = null

func _build_ui() -> void:
	_dock = VBoxContainer.new()
	_dock.name = "Dungeon Editor"

	var title = Label.new()
	title.text = "Dungeon Editor"
	_dock.add_child(title)

	var hint = Label.new()
	hint.text = "GridMap 편집은 기본 에디터 도구를 사용하세요."
	_dock.add_child(hint)

	var row = HBoxContainer.new()
	_dock.add_child(row)

	_target_path = LineEdit.new()
	_target_path.placeholder_text = "대상 DungeonGenerator 노드 경로"
	_target_path.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(_target_path)

	var use_selected = Button.new()
	use_selected.text = "선택 노드 사용"
	use_selected.pressed.connect(_on_use_selected)
	row.add_child(use_selected)

	_status = Label.new()
	_status.text = "대상 노드를 선택하세요."
	_dock.add_child(_status)

	var export_button = Button.new()
	export_button.text = "JSON 내보내기"
	export_button.pressed.connect(_on_export_pressed)
	_dock.add_child(export_button)

	var import_button = Button.new()
	import_button.text = "JSON 가져오기"
	import_button.pressed.connect(_on_import_pressed)
	_dock.add_child(import_button)

	_export_dialog = EditorFileDialog.new()
	_export_dialog.file_mode = EditorFileDialog.FILE_MODE_SAVE_FILE
	_export_dialog.access = EditorFileDialog.ACCESS_RESOURCES
	_export_dialog.filters = PackedStringArray(["*.json ; JSON"])
	_export_dialog.file_selected.connect(_on_export_file_selected)
	_dock.add_child(_export_dialog)

	_import_dialog = EditorFileDialog.new()
	_import_dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	_import_dialog.access = EditorFileDialog.ACCESS_RESOURCES
	_import_dialog.filters = PackedStringArray(["*.json ; JSON"])
	_import_dialog.file_selected.connect(_on_import_file_selected)
	_dock.add_child(_import_dialog)

func _on_use_selected() -> void:
	var selection = get_editor_interface().get_selection().get_selected_nodes()
	if selection.is_empty():
		_status.text = "선택된 노드가 없습니다."
		return
	var node = selection[0]
	if not node.has_method("ExportLayoutToJson"):
		_status.text = "선택한 노드가 DungeonGenerator가 아닙니다."
		return
	var root = get_editor_interface().get_edited_scene_root()
	if root != null:
		_target_path.text = root.get_path_to(node)
	else:
		_target_path.text = node.get_path()
	_status.text = "대상 노드를 설정했습니다."

func _on_export_pressed() -> void:
	if _get_target() == null:
		_status.text = "유효한 DungeonGenerator를 찾을 수 없습니다."
		return
	_export_dialog.popup_centered_ratio()

func _on_import_pressed() -> void:
	if _get_target() == null:
		_status.text = "유효한 DungeonGenerator를 찾을 수 없습니다."
		return
	_import_dialog.popup_centered_ratio()

func _on_export_file_selected(path: String) -> void:
	var target = _get_target()
	if target == null:
		_status.text = "내보내기 실패: 대상이 없습니다."
		return
	target.ExportLayoutToJson(path)
	_status.text = "JSON 내보내기 완료: %s" % path

func _on_import_file_selected(path: String) -> void:
	var target = _get_target()
	if target == null:
		_status.text = "가져오기 실패: 대상이 없습니다."
		return
	var ok = target.ImportLayoutFromJson(path)
	if ok:
		_status.text = "JSON 가져오기 완료: %s" % path
	else:
		_status.text = "JSON 가져오기 실패: %s" % path

func _get_target() -> Node:
	var root = get_editor_interface().get_edited_scene_root()
	if root == null:
		return null
	var path = _target_path.text.strip_edges()
	if path.is_empty():
		return null
	var node := root.get_node_or_null(path)
	if node == null and path == root.name:
		node = root
	if node == null and path.begins_with("/"):
		var tree_root = root.get_tree().root
		node = tree_root.get_node_or_null(path)
	if node != null and node.has_method("ExportLayoutToJson"):
		return node
	return null
