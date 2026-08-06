import bpy
from pathlib import Path

# Run in Blender's Scripting workspace. It exports every visible mesh.
project = Path(bpy.data.filepath).parent
target = project / "Assets" / "level.obj"
target.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.obj_export(
    filepath=str(target),
    export_selected_objects=False,
    export_materials=False,
    forward_axis='NEGATIVE_Z',
    up_axis='Y',
)
print(f"3DLight level exported to: {target}")
