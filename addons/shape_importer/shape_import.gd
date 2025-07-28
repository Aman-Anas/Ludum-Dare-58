@tool
extends EditorScenePostImport

func _post_import(scene):
	# If the imported asset has only a single root node, that's the only node
	# we're interested in:
	if scene.get_child_count() == 1:
		print("Imported asset only contains a single root note; discarding outer root node.")
		return _transfer_to_root(scene)
		
	# If the asset contains animation, Godot's importer will put them into an
	# AnimationPlayer node. If there's only a single root object, but we also
	# have animations, we need to handle this explicitly:
	elif scene.get_child_count() == 2 and scene.get_child(1) is AnimationPlayer:
		print("Imported asset contains a single root note and an AnimationPlayer; discarding outer root node.")
		
		# First, convert the scene using our little trick.
		return _transfer_to_root(scene)

	# In all other cases, we will just return the scene as originally imported.
	else:
		return scene


func _transfer_to_root(scene: Node) -> Node:
	var root: Node = scene.get_child(0)
	
	scene.remove_child(root)
	root.owner = null

	for prop in root.get_property_list():
		var name: String = prop["name"]
		if name.begins_with("global"):
			continue
		scene.set(name, root.get(name))
	
	for child_node in root.get_children():
		root.remove_child(child_node)
		child_node.owner = null
		scene.add_child(child_node)
		child_node.owner = scene

	root.free()

	# Recursively set the owner of the new root and all its children
	_set_new_owner(scene, scene)

	# That's it!
	return scene

func _set_new_owner(node: Node, owner: Node):
	# If we set a node's owner to itself, we'll get an error
	if node != owner:
		node.owner = owner

	for child in node.get_children():
		_set_new_owner(child, owner)
