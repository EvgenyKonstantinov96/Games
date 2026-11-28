"""Measure visible gun motion and wrist-mounted geometry on real reimported FBX takes."""
import argparse,math,sys
from pathlib import Path
import bpy
from mathutils import Vector,Matrix
parser=argparse.ArgumentParser()
parser.add_argument('--assets',type=Path,required=True)
parser.add_argument('--baseline',action='store_true',help='Measure old marker mounting without accepting a frozen gun')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])

def frame(rig):
    point=rig.pose.bones['WeaponGrip'].head
    forward=(rig.pose.bones['WeaponForward'].head-point).normalized()
    right=forward.cross(Vector((0,0,1))).normalized();up=right.cross(forward)
    value=Matrix((right,forward,up)).transposed().to_4x4();value.translation=point
    return value

for name in ('Max','Eva'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.assets/(name+'.fbx')),use_anim=True,anim_offset=0)
    rig,=(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    scene=bpy.context.scene;scene.frame_set(1)
    calibration=rig.pose.bones['HandR'].matrix.inverted()@frame(rig)
    for state,start,count,minimum in (('run',33,24,3.),('fire',59,24,1.2),('walk',219,34,1.5),('walk-fire',255,34,.8)):
        poses=[];left=[]
        for step in range(count+1):
            scene.frame_set(start+step)
            pose=frame(rig) if args.baseline else rig.pose.bones['HandR'].matrix@calibration
            poses.append(pose.copy())
            left.append(pose.inverted()@rig.pose.bones['HandL'].head)
        arc=max(math.degrees(a.to_quaternion().rotation_difference(b.to_quaternion()).angle) for a in poses for b in poses)
        lift=max(p.translation.z for p in poses)-min(p.translation.z for p in poses)
        center=sum(left,Vector((0,0,0)))/len(left)
        drift=2*max((p-center).length for p in left)
        print(f'WEAPON {name}/{state}: angular travel {arc:.2f} deg, vertical travel {lift*1000:.2f} mm, support-hand span {drift*1000:.2f} mm; '+('old aim marker' if args.baseline else 'deforming wrist mount'),flush=True)
        assert minimum<arc<12,(name,state,'frozen or excessive weapon rotation',arc)
        assert drift<.024,(name,state,'support hand leaves mounted weapon',drift)
        assert (poses[0].translation-poses[-1].translation).length<.015,(name,state,'mount loop seam')
        assert math.degrees(poses[0].to_quaternion().rotation_difference(poses[-1].to_quaternion()).angle)<.1,(name,state,'rotation loop seam')
    print(f'WEAPON MOTION PASS {name}: all four carry cycles, angular motion, support hand and loop seams. Blender, not Unity.',flush=True)
