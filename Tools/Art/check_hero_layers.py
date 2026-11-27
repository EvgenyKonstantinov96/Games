"""Check the lower-body mask on the actual FBX tracks offline in Blender.

blender -b -t 2 -P Tools/Art/check_hero_layers.py -- Assets/Resources/ProductionArt
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

assets = Path(sys.argv[sys.argv.index("--") + 1])
actions = {"reload": (85, 30), "hit": (117, 8), "medkit": (171, 24), "throw": (199, 18)}
gaits = {"run": (33, 24), "walk": (219, 34)}


def rifle_vertices(name):
    mesh_path = assets.parent / "Meshes" / f"lux_weapon_rifle{name}.obj"
    return [Vector(tuple(map(float, line.split()[1:4]))) for line in mesh_path.read_text().splitlines()
            if line.startswith("v ")]


grip_vertices = [vertex for vertex in rifle_vertices("") if -.08 <= vertex.z <= .106 and vertex.y < -.050]
barrel = rifle_vertices("_steel")
tip = max(vertex.z for vertex in barrel)
end_ring = [vertex for vertex in barrel if abs(vertex.z - tip) < .0001]
muzzle = sum(end_ring, Vector((0, 0, 0))) / len(end_ring)
assert (muzzle - Vector((0, .04, 1.27))).length < .0001


def sample(scene, rig, frame):
    whole = math.floor(frame)
    scene.frame_set(whole, subframe=frame - whole)
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


for name in ("Max", "Eva"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(assets / (name + ".fbx")), use_anim=True, anim_offset=0)
    rig, = (obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    scene = bpy.context.scene
    pose = rig.pose.bones
    legs = {"Pelvis", *(bone.name for bone in pose["Pelvis"].children_recursive)}
    legs -= {"Spine", *(bone.name for bone in pose["Spine"].children_recursive)}
    assert {"FootL", "FootR", "ForefootL", "ForefootR"} <= legs
    assert legs.isdisjoint(("Chest", "WeaponGrip", "HandL", "HandR"))

    sampled = []
    offsets = []
    for gait, (start, duration) in gaits.items():
        for action, (begin, length) in actions.items():
            # An item may be used at any phase, including while the opposite foot is planted.
            for index in range(12):
                phase = (.37 + index / 12) % 1.
                lower = sample(scene, rig, start + phase * duration)
                look_at = ("FootL", "FootR", "ForefootL", "ForefootR")
                reference = {bone: pose[bone].head.copy() for bone in look_at}
                upper = sample(scene, rig, begin + index * length / 12)
                offsets.append(max((pose[bone].head - reference[bone]).length for bone in look_at))
                hand = pose["Spine"].matrix.inverted() @ pose["HandL"].head
                sampled.append((lower, upper, reference, hand))
    rig.animation_data_clear()
    worst = grip_gap = 0.
    for lower, upper, reference, hand in sampled:
        for bone in pose:
            bone.matrix_basis = (lower if bone.name in legs else upper)[bone.name]
        bpy.context.view_layer.update()
        error = max((pose[bone].head - location).length for bone, location in reference.items())
        worst = max(error, worst)
        hand_r = pose["HandR"]
        palm = hand_r.head.lerp(hand_r.tail, .69)
        socket = pose["WeaponGrip"].head
        forward = (pose["WeaponForward"].head - socket).normalized()
        right = forward.cross(Vector((0, 0, 1))).normalized()
        up = right.cross(forward).normalized()
        # Match the imported rifle scale and offset at its animated grip socket.
        fitted = [socket + up * .065 + (right * v.x + up * v.y + forward * v.z) * .51
                  for v in grip_vertices]
        grip_gap = max(grip_gap, min((palm - point).length for point in fitted))
        assert error < .0001, (name, error)
        assert ((pose["Spine"].matrix.inverted() @ pose["HandL"].head) - hand).length < .0001
    assert max(offsets) > .25 and grip_gap < .09, (name, grip_gap)
    print(f"RIFLE {name}: maximum palm-to-grip vertex distance {grip_gap:.3f} m; muzzle ring centered on authored axis.")
    print(f"HERO MASK {name}: {len(sampled)} action/gait samples, prior maximum foot mismatch "
          f"{max(offsets):.3f} m, layered error {worst:.6f} m. Hand pose relative to spine preserved.", flush=True)
