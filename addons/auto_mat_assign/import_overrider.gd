@tool
extends EditorScenePostImportPlugin
## Credit to PixPal-tools addon for original format

func _pre_process(scene: Node) -> void:
	
	iterate(scene)

func _post_process(scene: Node):
	if scene.get_child_count() == 0:
		return scene
		
	# In case it's needed
	if scene.name.contains("nopost"):
		return scene
	
	# Remove all Rigify controls	
	for node in scene.get_children():
		if node.name.begins_with("WGT"):
			scene.remove_child(node)
	
	if not scene is PhysicsBody3D:
		return scene
	
	# Replace all our staticbody3D children with the actual collision shape
	# This improves the workflow for CharacterBody3Ds and RigidBody3Ds
	for node in scene.get_children():
		if node is StaticBody3D:
			var body: StaticBody3D = node

			for child in body.get_children():
				body.remove_child(child)
				scene.add_child(child)
				child.position += body.position

			scene.remove_child(body)
			
	return scene

func iterate(node: Node) -> void:
	#print("hi")
	#print(node)
	#print(str(typeof(node)))
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
		iterate(child)
				
func get_standard_material(material_name: String) -> Material:
	var mat_path = "res://assets/materials/%s.tres" % material_name
	
	if not ResourceLoader.exists(mat_path):
		print("Material path %s does not exist." % mat_path)
	
	var mat = load(mat_path)
	
	return mat
