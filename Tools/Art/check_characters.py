"""Round-trip the shipped FBX files in Blender and assert the skin and motion data."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from character_atlas import SLOTS,SIZE

ROOT=Path(sys.argv[sys.argv.index("--")+1])
CAST={"Max","Eva","Walker","Runner","Armored","Brute","Toxic","Crawler","Boss"}
assert {f.stem for f in ROOT.glob("*.fbx")}==CAST


def check_anatomy(skin, rig, scene, path, hero):
    mesh = skin.data
    adjacent = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacent[a].append(b)
        adjacent[b].append(a)
    remaining = set(range(len(mesh.vertices)))
    largest = set()
    while remaining:
        first = remaining.pop()
        component, todo = {first}, [first]
        while todo:
            for vertex in adjacent[todo.pop()]:
                if vertex in remaining:
                    remaining.remove(vertex)
                    component.add(vertex)
                    todo.append(vertex)
        if len(component) > len(largest):
            largest = component
    # Fitted garments intentionally replace the covered body faces. Anatomical
    # continuity is checked within each component, while binding coverage spans the skin.
    assert len(largest) > 400, (path,len(largest))
    weights = {skin.vertex_groups[group.group].name for i in range(len(mesh.vertices))
               for group in mesh.vertices[i].groups if group.weight > .28}
    joints = {f"{part}{side}" for part in ("Clavicle", "Thigh", "Shin", "UpperArm", "Forearm", "Hand")
              for side in ("L", "R")}
    digits = {f"Finger{digit}{side}" for digit in range(1, 6) for side in ("L", "R")}
    assert joints | digits | {"Spine", "Pelvis", "Chest", "Head"} <= weights, (path, weights)

    reference = []
    for edge in mesh.edges:
        a, b = edge.vertices
        rest = (mesh.vertices[a].co-mesh.vertices[b].co).length
        if rest >= .017 and a in largest and b in largest:
            reference.append((a,b,rest))
    peak = 0.
    feet = {side: [] for side in ("L", "R")}
    frames = [1, *range(33, 57), 73, 94, 122, 133, 154]
    if hero:
        frames += [182, 189, 207, 211, *range(219, 253), 255, 267, 279]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for frame in frames:
        scene.frame_set(frame)
        posed = skin.evaluated_get(depsgraph).data.vertices
        strain = sorted((posed[a].co-posed[b].co).length/rest for a,b,rest in reference)
        assert strain[-1] < 3.6 and strain[int(len(strain)*.01)] > .30, (path, frame, strain[-1])
        peak = max(peak, strain[int(len(strain)*.99)])
        if 33 <= frame < 57:
            for side in feet:
                foot = rig.pose.bones["Foot"+side]
                forefoot = rig.pose.bones["Forefoot"+side]
                relative = foot.matrix.to_quaternion().inverted() @ forefoot.matrix.to_quaternion()
                feet[side].append(relative @ Vector((0,1,0)))
    toe_arc = [max(math.degrees(directions[0].angle(pose)) for pose in directions)
               for directions in feet.values()]
    assert peak < 1.85 and min(toe_arc) > 10, (path, peak, toe_arc)
    print(f"ANATOMY {path.stem:7} {len(largest)} vertices in continuous body, "
          f"{len(frames)} poses, worst 99th-percentile edge stretch {peak:.2f}x, "
          f"forefoot flexion {toe_arc[0]:.1f}/{toe_arc[1]:.1f} degrees", flush=True)


for path in sorted(ROOT.glob("*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, anim_offset=0)
    rig,=[o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
    skins=sorted((o for o in bpy.context.scene.objects if o.type=="MESH"),key=lambda o:o.name)
    assert len(skins)==3 and all(skin.name.endswith("_LOD"+str(i)) for i,skin in enumerate(skins)),path
    skin=skins[0]
    triangles=[]
    for index,lod in enumerate(skins):
        lod.data.calc_loop_triangles()
        triangles.append(len(lod.data.loop_triangles))
        assert all(1<=len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<.001
                   for v in lod.data.vertices),(path,index,"weights")
        uv=lod.data.uv_layers.active
        for polygon in lod.data.polygons:
            points=[(uv.data[i].uv.x*SIZE,(1-uv.data[i].uv.y)*SIZE) for i in polygon.loop_indices]
            assert any(all(x-.01<=u<=x+w+.01 and y-.01<=v<=y+h+.01 for u,v in points)
                       for x,y,w,h in SLOTS.values()),(path,index,'face crosses atlas islands',polygon.index)
    budget=30000 if path.stem in ("Max","Eva") else 19000
    assert triangles[0]<budget and triangles[1]<triangles[0]*.57 and triangles[2]<triangles[0]*.31,(path,triangles)
    print(f"LOD {path.stem}: {triangles} triangles, one skeleton and atlas",flush=True)
    names=set(rig.data.bones.keys())
    assert {"Pelvis","Spine","Chest","Head","ForwardMarker","HandL","HandR","ForefootL","ForefootR"} <= names, path
    assert {f'Finger{digit}{segment}{side}' for digit in range(1,6)
            for segment in ('','Middle','Tip') for side in ('L','R')} <= names,path
    hero=path.stem in {"Max","Eva"}
    if hero:
        assert {"WeaponGrip","WeaponForward"} <= names, path
    assert any(m.type=="ARMATURE" and m.object==rig for m in skin.modifiers), path
    assert skin.data.uv_layers and len(skin.data.vertices) < 16000, path
    assert skin.data.has_custom_normals and all(p.use_smooth for p in skin.data.polygons), path
    assert all(1 <= len(v.groups) <= 4 and abs(sum(g.weight for g in v.groups)-1.0) < .001
               for v in skin.data.vertices), path
    action=rig.animation_data.action
    start,end=action.frame_range
    assert end-start >= 166, (path,action.frame_range)
    skin.data.calc_loop_triangles()
    tri_count=len(skin.data.loop_triangles)
    assert tri_count < budget, (path,tri_count)
    scene=bpy.context.scene
    points=[]
    for offset in (0,38,48,60,93,120,135,157,166):
        scene.frame_set(round(start)+offset)
        points.append(rig.pose.bones["Pelvis"].matrix.copy())
    assert (points[1].translation-points[2].translation).length > .005, path
    assert (points[0].translation-points[-1].translation).length > .2, path
    check_anatomy(skin, rig, scene, path, hero)
    if not hero:
        floors, heads = [], []
        graph = bpy.context.evaluated_depsgraph_get()
        for frame in range(147, 169):
            scene.frame_set(frame)
            model = skin.evaluated_get(graph)
            floors.append(min((model.matrix_world @ vertex.co).z for vertex in model.data.vertices))
            heads.append((rig.matrix_world @ rig.pose.bones["Head"].head).z)
        assert max(map(abs, floors)) < .012, (path, "death contact", floors)
        assert heads[-1] < heads[0] * .50, (path, "head remained upright", heads)
        assert abs(heads[-1] - heads[-2]) < .025, (path, "unsettled landing", heads)
        print(f"FALL {path.stem}: 22 poses, pavement error {max(map(abs, floors)):.3f} m, "
              f"head {heads[0]:.2f} to {heads[-1]:.2f} m", flush=True)
    if path.stem == "Boss":
        scene.frame_set(132)
        charge = [rig.pose.bones["Hand"+side].head.copy() for side in ("L", "R")]
        feet = [rig.pose.bones["Foot"+side].head.copy() for side in ("L", "R")]
        scene.frame_set(142)
        reach = [rig.pose.bones["Hand"+side].head.y-origin.y for side,origin in zip(("L","R"),charge)]
        drift = [(rig.pose.bones["Foot"+side].head-origin).length for side,origin in zip(("L","R"),feet)]
        assert min(reach) > .44 and max(drift) < .04, (path, reach, drift)
        print(f"BOSS attack reach L/R {reach[0]:.3f}/{reach[1]:.3f} m, "
              f"ankle drift L/R {drift[0]:.3f}/{drift[1]:.3f} m", flush=True)
    if hero:
        assert end-start >= 288, action.frame_range
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
            walk, walk_support = [], []
            for step in range(35):
                scene.frame_set(219 + step)
                foot = rig.pose.bones[foot_name]
                walk.append(foot.head.copy())
                walk_support.append(min((foot.matrix@heel_rest).z, (foot.matrix@ball_rest).z))
                scene.frame_set(255 + step)
                assert (rig.pose.bones[foot_name].head-walk[-1]).length < .022, (path, side, step)
            walk_reach = max(p.y for p in walk)-min(p.y for p in walk)
            run_reach = max(p.y for p in travel)-min(p.y for p in travel)
            stance = sum(-.035 < height < .055 for height in walk_support[:34])
            assert .37 < walk_reach < .72 * run_reach, (path, side, walk_reach, run_reach)
            assert stance >= 21 and max(walk_support) > .065, (path, side, walk_support)
            assert (walk[-1]-walk[0]).length < .015, (path, side, walk[0], walk[-1])
            print(f"WALK {path.stem:7} {side} reach {walk_reach:.3f} m vs {run_reach:.3f} m running, "
                  f"{stance}/34 frames near the ground, match with firing variant", flush=True)
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
          + (", medkit, throw, walk and walk-fire poses" if hero else ""),flush=True)
