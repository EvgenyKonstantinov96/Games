"""Round-trip the shipped FBX files in Blender and assert the skin and motion data."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT=Path(sys.argv[sys.argv.index("--")+1])
CAST={"Max","Eva","Walker","Runner","Armored","Brute","Toxic","Crawler","Boss"}
assert {f.stem for f in ROOT.glob("*.fbx")}==CAST

for path in sorted(ROOT.glob("*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, anim_offset=0)
    rig,=[o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
    skin,=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    names=set(rig.data.bones.keys())
    assert {"Pelvis","Chest","Head","ForwardMarker","HandL","HandR"} <= names, path
    hero=path.stem in {"Max","Eva"}
    if hero:
        assert {"WeaponGrip","WeaponForward"} <= names, path
    assert any(m.type=="ARMATURE" and m.object==rig for m in skin.modifiers), path
    assert skin.data.uv_layers and len(skin.data.vertices) < 5500, path
    assert skin.data.has_custom_normals and all(p.use_smooth for p in skin.data.polygons), path
    assert all(abs(sum(g.weight for g in v.groups)-1.0) < .001 for v in skin.data.vertices), path
    action=rig.animation_data.action
    start,end=action.frame_range
    assert end-start >= 166, (path,action.frame_range)
    skin.data.calc_loop_triangles()
    tri_count=len(skin.data.loop_triangles)
    assert tri_count < 7500, (path,tri_count)
    scene=bpy.context.scene
    points=[]
    for offset in (0,38,48,60,93,120,135,157,166):
        scene.frame_set(round(start)+offset)
        points.append(rig.pose.bones["Pelvis"].matrix.copy())
    assert (points[1].translation-points[2].translation).length > .005, path
    assert (points[0].translation-points[-1].translation).length > .2, path
    if hero:
        assert end-start >= 215, action.frame_range
        poses=[]
        for frame in (1,183,207):
            scene.frame_set(frame)
            poses.append(rig.pose.bones["HandL"].head.copy()-rig.pose.bones["Chest"].head.copy())
        assert (poses[1]-poses[0]).length > .09 and (poses[2]-poses[0]).length > .09, path
        # Check the actual re-imported poses, including the rollover of the heel
        # and forefoot through a running cycle.
        heel, ball = Vector((0,-.073,-.085)), Vector((0,.163,-.085))
        for side,first in (("L",0),("R",12)):
            foot_name="Foot"+side
            foot_bone=rig.data.bones[foot_name]
            heel_rest,ball_rest=(foot_bone.matrix_local.inverted()@(foot_bone.head_local+p) for p in (heel,ball))
            pitches, travel, support = [],[],[]
            for index in range(25):
                frame=33+(first+index)%24
                if side=="L" and index==24: frame=57
                scene.frame_set(frame)
                foot=rig.pose.bones[foot_name]
                toe=foot.tail-foot.head
                pitches.append(math.degrees(math.atan2(toe.z,toe.y)))
                travel.append(foot.head.copy())
                support.append(min((foot.matrix@heel_rest).z,(foot.matrix@ball_rest).z))
                scene.frame_set(frame+26)
                assert (rig.pose.bones[foot_name].head-travel[-1]).length<.022, (path,side,frame)
            assert max(pitches)-min(pitches)>42, (path,side,pitches)
            assert max(support)>.16, (path,side,support)
            assert all(-.035<height<.055 for height in support[:10]), (path,side,support)
            scene.frame_set(33)
            loop_start=rig.pose.bones[foot_name].head.copy()
            scene.frame_set(57)
            loop_error=(loop_start-rig.pose.bones[foot_name].head).length
            assert loop_error<.015, (path,side,loop_error)
            assert travel[0].y-travel[9].y>.65, (path,side,travel[0],travel[9])
            assert max((a-b).length for a,b in zip(travel,travel[1:]))<.24, (path,side)
            print(f"GAIT {path.stem:7} {side} ankle-roll {max(pitches)-min(pitches):.1f} degrees, "
                  f"support {min(support[:10]):.3f}..{max(support[:10]):.3f} m, "
                  f"rearward travel {travel[0].y-travel[9].y:.3f} m, loop error {loop_error:.4f} m",flush=True)
    if path.stem in {"Walker", "Toxic", "Runner"}:
        strides = []
        for side in ("L", "R"):
            positions = []
            for frame in range(33, 57):
                scene.frame_set(frame)
                positions.append(rig.pose.bones["Foot"+side].head.copy())
            travel = max(p.y for p in positions)-min(p.y for p in positions)
            lift = max(p.z for p in positions)-min(p.z for p in positions)
            strides.append((travel, lift))
        left, right = strides
        if path.stem == "Runner":
            assert all(travel > .75 and lift > .15 for travel, lift in strides), (path, strides)
        else:
            assert right[0] < left[0]*.90 and right[1] < left[1]*.80, (path, strides)
        print(f"GAIT {path.stem:7} ankle travel L/R {left[0]:.3f}/{right[0]:.3f} m, "
              f"ankle lift L/R {left[1]:.3f}/{right[1]:.3f} m", flush=True)
    print(f"PASS {path.stem:7} {len(names)} bones, {len(skin.data.vertices)} verts, {tri_count} triangles, "
          f"{start:g}–{end:g} frames, smooth custom normals"
          + (", medkit and throw poses" if hero else ""),flush=True)
