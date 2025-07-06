@tool
extends EditorScenePostImport

func _post_import(scene):
	# If the imported asset has only a single root node, that's the only node
	# we're interested in:
	if scene.get_child_count() == 1:
		print("Imported asset only contains a single root note; discarding outer root node.")
		return _remove_root_node(scene)
		
	# If the asset contains animation, Godot's importer will put them into an
	# AnimationPlayer node. If there's only a single root object, but we also
	# have animations, we need to handle this explicitly:
	elif scene.get_child_count() == 2 and scene.get_child(1) is AnimationPlayer:
		print("Imported asset contains a single root note and an AnimationPlayer; discarding outer root node.")
		
		# First, convert the scene using our little trick.
		var new_scene := _remove_root_node(scene)
		
		# Now grab the AnimationPlayer that was generated from the asset's animations.
		var anim_player: AnimationPlayer = scene.get_child(1)
		
		# The following might seem a little verbose, but we have to be this 
		# exact in order to not trigger various Godot warnings.
		scene.remove_child(anim_player)
		anim_player.owner = null
		new_scene.add_child(anim_player)
		anim_player.owner = new_scene
		
		return new_scene
		
	# In all other cases, we will just return the scene as originally imported.
	else:
		return scene


func _remove_root_node(scene: Node) -> Node:
	var new_root: Node = scene.get_child(0)

	# Keep the original name so instances of this scene will have the
	# imported asset's filename by default
	new_root.name = scene.name

	# Recursively set the owner of the new root and all its children
	_set_new_owner(new_root, new_root)

	# That's it!
	return new_root
	

func _set_new_owner(node: Node, owner: Node):
	# If we set a node's owner to itself, we'll get an error
	if node != owner:
		node.owner = owner

	for child in node.get_children():
		_set_new_owner(child, owner)