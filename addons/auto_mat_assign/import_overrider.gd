@tool
extends EditorScenePostImportPlugin
## Credit to PixPal-tools addon for original format


func _pre_process(scene: Node) -> void:
	
	iterate(scene)

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
