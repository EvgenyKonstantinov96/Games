"""Measure source-implied foot/pavement drift, not native Unity foot sliding.

Use export_runtime_cadence.py first. No physical pass/fail is asserted: this
diagnostic records mismatch even when geometry/loop/contact checks pass.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from gait import PROFILES

parser = argparse.ArgumentParser()
parser.add_argument('--assets', type=Path, required=True)
parser.add_argument('--cadence', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
runtime = json.loads(args.cadence.read_text())
samples, results = [], []
for name, height in [('Max', 1.94), ('Eva', 1.81)]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = args.assets / f'{name}.fbx'
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, anim_offset=0)
    rig, = (o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    bones, pose, scene = rig.data.bones, rig.pose.bones, bpy.context.scene
    def rest(key): return rig.matrix_world @ bones[key].head_local
    ankles = (rest('FootL') + rest('FootR')) * .5
    rise = rest('Head') - ankles
    up = rise.normalized()
    forward = rest('ForwardMarker') - rest('Pelvis')
    forward = (forward - up * forward.dot(up)).normalized()
    scale = (height * .86 - .095) / rise.length
    for motion, start, count in [('Run', 33, 24), ('Walk', 219, 34)]:
        stance = .65 if motion == 'Walk' else PROFILES[name][2]
        right_phase = .5 if motion == 'Walk' else PROFILES[name][4]
        for side in ['L', 'R']:
            bone = bones['Foot' + side]
            # The two modeled sole pivots used by gait.foot_target. Integrate
            # the same patch at each interval's ends to avoid a heel/toe jump.
            local = [bone.matrix_local.inverted() @ (bone.head_local + Vector(p))
                     for p in [(0, -.073, -.085), (0, .163, -.085)]]
            positions = []
            for i in range(201):
                phase = (stance * i / 200 - (right_phase if side == 'R' else 0)) % 1
                frame = start + count * phase
                scene.frame_set(int(frame), subframe=frame % 1)
                points = [rig.matrix_world @ (pose['Foot' + side].matrix @ p) for p in local]
                positions.append((rig.matrix_world @ pose['Foot' + side].head, points))
            ankle_distance = -(positions[-1][0] - positions[0][0]).dot(forward) * scale
            sole_distance, patch_changes, previous = 0, 0, None
            for a, b in zip(positions, positions[1:]):
                patch = min(range(2), key=lambda k: ((a[1][k] + b[1][k]) * .5).dot(up))
                patch_changes += previous is not None and patch != previous
                previous = patch
                sole_distance -= (b[1][patch] - a[1][patch]).dot(forward) * scale
            samples.append(dict(model=name, motion=motion, foot=side, stance_share=stance,
                                frames=count, align_scale=scale, patch_changes=patch_changes,
                                ankle_stance_distance_m=ankle_distance,
                                sole_virtual_contact_distance_m=sole_distance,
                                fbx_sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            for settings in runtime:
                if settings['model'] != name or (settings['mode'] == 'Walk') != (motion == 'Walk'):
                    continue
                seconds = count / 30 * stance / settings['runtime_cadence']
                pavement = settings['road_speed'] * seconds
                results.append(dict(**settings, foot=side, stance_s=seconds,
                                    sole_virtual_travel_mm=sole_distance * 1000,
                                    pavement_travel_mm=pavement * 1000,
                                    residual_sole_mm=(pavement - sole_distance) * 1000))
metadata = dict(method='Blender FBX; geometric AlignRig normalization; 201 virtual sole samples '
                'per stance; actual managed campaign/cadence, steady modes and zero lateral speed. '
                'Not native Unity importer/Animator/IK or rendered sliding.', samples=samples, results=results)
args.out.parent.mkdir(parents=True, exist_ok=True)
args.out.write_text(json.dumps(metadata, indent=2) + '\n')
print('MODEL FOOT LEVEL MODE CADENCE STANCE_MS SOLE_MM ROAD_MM RESIDUAL_MM')
for row in results:
    print(f"{row['model']} {row['foot']} {row['level']} {row['mode']} "
          f"{row['runtime_cadence']:.5f} {row['stance_s']*1000:.2f} "
          f"{row['sole_virtual_travel_mm']:.1f} {row['pavement_travel_mm']:.1f} "
          f"{row['residual_sole_mm']:.1f}", flush=True)
