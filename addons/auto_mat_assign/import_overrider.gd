@tool
extends EditorScenePostImportPlugin
## Credit to PixPal-tools addon for original format

func _pre_process(scene: Node) -> void:
	
	update_node_materials(scene)

func _post_process(scene: Node):
	# In case it's needed
	if scene.name.contains("nopost"):
		return scene

	# Remove all Rigify controls	
	for node in scene.get_children():
		if node.name.begins_with("WGT"):
			scene.remove_child(node)

	# Only affect physics bodies
	if not scene is PhysicsBody3D:
		return scene
	
	for node in scene.get_children():
		if node is PhysicsBody3D:
			if node.name.contains("nopost"):
				continue

			var body_to_remove = node
			scene.remove_child(body_to_remove)
			body_to_remove.owner = null

			for body_child in body_to_remove.get_children():
				body_to_remove.remove_child(body_child)
				body_child.owner = null
				scene.add_child(body_child)
				body_child.owner = scene
				
				body_child.position += body_to_remove.position
				body_child.rotation += body_to_remove.rotation
				body_child.scale *= body_to_remove.scale

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
		print("Material path %s does not exist." % mat_path)
	
	var mat = load(mat_path)
	
	return mat
