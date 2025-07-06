@tool
extends EditorScenePostImportPlugin

func _pre_process(scene: Node) -> void:
	update_node_materials(scene)

func _post_process(scene: Node):
	# Remove all Rigify controls	
	for node in scene.get_children():
		if node.name.begins_with("WGT"):
			scene.remove_child(node)
	return scene

func update_node_materials(node: Node) -> void:
	if node is ImporterMeshInstance3D:
		var mesh: ImporterMesh = node.mesh

		for index in mesh.get_surface_count():
			var material_name: String = mesh.get_surface_material(index).resource_name

			# Material found. Replace with our version
			if material_name.begins_with('ST_'):
				mesh.set_surface_material(index,
					get_standard_material(material_name.substr(3))
				)
	for child in node.get_children():
		update_node_materials(child)
				
func get_standard_material(material_name: String) -> Material:
	var mat_path = "res://assets/materials/%s.tres" % material_name
	
	if not ResourceLoader.exists(mat_path):
		# Try UID instead
		mat_path = "uid://%s" % material_name

		if not ResourceLoader.exists(mat_path):
			print("Material %s does not exist." % material_name)

	var mat = load(mat_path)
	
	return mat
