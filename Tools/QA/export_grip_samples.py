"""Export shipped FBX bone poses for the managed grip probe; no rendering.

blender -b -t 1 -P Tools/QA/export_grip_samples.py -- \
    --assets Assets/Resources/ProductionArt --out /tmp/zsr-grip-samples.json

This is Blender's FBX import, not Unity's native importer or Animator execution.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

parser = argparse.ArgumentParser()
parser.add_argument("--assets", type=Path, required=True)
parser.add_argument("--out", type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])

# FBX samples are Z-up/forward-Y in Blender. Conjugate the entire bone frame
# when mapping to Unity's Y-up/forward-Z convention, including its handedness.
basis = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
frames = (1, 33, 39, 45, 51, 57, 59, 65, 71, 77, 83,
          85, 99, 114, 171, 183, 194, 199, 207, 216,
          219, 227, 236, 244, 253, 255, 263, 272, 280, 289)
models = []
for name in ("Max", "Eva"):
    path = args.assets / (name + ".fbx")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, anim_offset=0)
    rig, = (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    poses = []
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        bones = []
        for bone in rig.pose.bones:
            local = bone.parent.matrix.inverted() @ bone.matrix if bone.parent else rig.matrix_world @ bone.matrix
            position, rotation, scale = (basis @ local @ basis).decompose()
            bones.append({"name": bone.name, "parent": bone.parent.name if bone.parent else None,
                          "position": list(position), "rotation": [rotation.x, rotation.y, rotation.z, rotation.w],
                          "scale": list(scale)})
        poses.append({"frame": frame, "bones": bones})
    models.append({"name": name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "poses": poses})
args.out.parent.mkdir(parents=True, exist_ok=True)
args.out.write_text(json.dumps({"source": "Blender FBX poses; Unity scene/rotation stand-ins", "models": models}))
print(f"EXPORTED: {len(models)} shipped FBX rigs x {len(frames)} poses to {args.out}; no rendering.", flush=True)
