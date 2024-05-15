@tool
extends EditorPlugin

var import_plugin := preload("import_overrider.gd").new()

func _enter_tree():
	# Initialization of the plugin goes here.
	add_scene_post_import_plugin(import_plugin)


func _exit_tree():
	# Clean-up of the plugin goes here.
	remove_scene_post_import_plugin(import_plugin)
