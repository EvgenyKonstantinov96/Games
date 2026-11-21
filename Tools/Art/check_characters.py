"""Round-trip the shipped FBX files in Blender and assert the skin and motion data."""
import sys
from pathlib import Path

import bpy

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
    if path.stem in {"Max","Eva"}:
        assert {"WeaponGrip","WeaponForward"} <= names, path
    assert any(m.type=="ARMATURE" and m.object==rig for m in skin.modifiers), path
    assert skin.data.uv_layers and len(skin.data.vertices) < 5500, path
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
    print(f"PASS {path.stem:7} {len(names)} bones, {len(skin.data.vertices)} verts, {tri_count} triangles, {start:g}–{end:g} frames",flush=True)
