# Constants to change import settings
RECENTER = False


# exports each selected object into its own file

import bpy
import os

SAVE_LOCATION = "/batch_exports/"

# export to blend file location
basedir = os.path.dirname(bpy.data.filepath)
file_name = os.path.splitext(os.path.basename(bpy.data.filepath))[0]

save_path = os.path.dirname(basedir) + SAVE_LOCATION
print("Save path:", save_path)

if not basedir:
    raise Exception("Blend file is not saved")

view_layer = bpy.context.view_layer

obj_active = view_layer.objects.active

initial_selection = bpy.context.selected_objects
selection = initial_selection  # bpy.context.scene.objects

bpy.ops.object.select_all(action="DESELECT")

for obj in selection:
    print("Exporting", obj, obj.name)

    if obj.parent is not None:
        print(obj.parent)
        continue

    obj.select_set(True)
    for child in obj.children_recursive:
        child.select_set(True)

    # some exporters only use the active object
    view_layer.objects.active = obj

    name: str = bpy.path.clean_name(obj.name)
    name = name.split("-")[0]

    fn = os.path.join(save_path + f"/{file_name}/", name)

    print(fn)

    saved_loc = tuple(obj.location)

    if RECENTER:
        obj.location = (0, 0, 0)

    bpy.ops.export_scene.gltf(
        filepath=fn + ".gltf",
        export_format="GLTF_SEPARATE",
        export_apply=True,
        use_selection=True,
    )

    obj.location = saved_loc

    obj.select_set(False)
    for child in obj.children_recursive:
        child.select_set(False)

    print("written:", fn)


view_layer.objects.active = obj_active

for obj in initial_selection:
    obj.select_set(True)
