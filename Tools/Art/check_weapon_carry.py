"""Measure the support hand in the actual FBX weapon frame and the pack binding."""
import argparse,sys
from pathlib import Path
import bpy
from mathutils import Vector
parser=argparse.ArgumentParser();parser.add_argument('--assets',type=Path,required=True);parser.add_argument('--compare',type=Path)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])

def measure(folder,name,validate):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(folder/(name+'.fbx')),use_anim=True,anim_offset=0)
    rig,=(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    if validate:
        assert rig.data.bones['Pack'].parent.name=='Chest'
        assert all(rig.data.bones['KneeGuard'+side].parent.name=='Shin'+side for side in ('L','R'))
        for skin in (o for o in bpy.context.scene.objects if o.type=='MESH'):
            group=skin.vertex_groups['Pack'].index
            bound=[v for v in skin.data.vertices if any(g.group==group and g.weight>.99 for g in v.groups)]
            assert len(bound)>100,(name,skin.name,'missing pack weights',len(bound))
            print(f'PACK {name}/{skin.name}: {len(bound)} vertices rigidly bound to Pack below Chest.',flush=True)
            if skin.name.endswith('_LOD0'):
                for side in ('L','R'):
                    guard=skin.vertex_groups['KneeGuard'+side].index
                    vertices={v.index for v in skin.data.vertices if any(g.group==guard and g.weight>.99 for g in v.groups)}
                    edges={}
                    for polygon in skin.data.polygons:
                        if not all(i in vertices for i in polygon.vertices):continue
                        ids=list(polygon.vertices)
                        for a,b in zip(ids,ids[1:]+ids[:1]):
                            coords=[tuple(round(n,5) for n in skin.data.vertices[i].co) for i in (a,b)]
                            edge=tuple(sorted(coords));edges[edge]=edges.get(edge,0)+1
                    assert edges and all(count==2 for count in edges.values()),(name,side,'open guard shell')
                print(f'GUARDS {name}: closed LOD0 shells with sidewalls/back caps after UV seam welding.',flush=True)
    worst=0
    for state,start,count in (('run',33,24),('fire',59,24),('walk',219,34),('walk-fire',255,34)):
        points=[]
        for step in range(count+1):
            bpy.context.scene.frame_set(start+step)
            if validate:
                # The fitted pad slides with the patella cloth instead of
                # rotating around an offset suspended in front of the shin.
                assert all((rig.pose.bones['KneeGuard'+side].head-rig.pose.bones['Shin'+side].head).length<.12
                           for side in ('L','R')),(name,state,step,'detached guard pivot')
            hand=rig.pose.bones['HandL'].head
            points.append(rig.pose.bones['WeaponGrip'].matrix.inverted()@hand)
        center=sum(points,Vector((0,0,0)))/len(points)
        span=2*max((p-center).length for p in points);worst=max(span,worst)
        if validate:assert span<.024,(name,state,'support hand leaving weapon',span)
        print(f'CARRY {folder.name}/{name}/{state}: support-hand span {span*1000:.2f} mm in weapon frame.',flush=True)
    return worst

for name in ('Max','Eva'):
    current=measure(args.assets,name,True)
    if args.compare:
        before=measure(args.compare,name,False)
        print(f'CARRY CHANGE {name}: maximum span {before*1000:.2f} -> {current*1000:.2f} mm. FBX measurement, not native gameplay.',flush=True)
