"""Measure reimported knee recovery, limb lengths and cycle seams. Not a PlayMode test."""
import argparse,math,sys
from pathlib import Path
import bpy

parser=argparse.ArgumentParser()
parser.add_argument('--assets',type=Path,required=True)
parser.add_argument('--compare',type=Path)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])

def measure(folder,name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(folder/(name+'.fbx')),use_anim=True,anim_offset=0)
    rig,=(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    scene=bpy.context.scene
    maxima=[]
    for side in ('L','R'):
        values=[];lengths=[];positions=[]
        for index in range(49):
            frame=33+index*.5;scene.frame_set(int(frame),subframe=frame%1)
            hip=rig.pose.bones['Thigh'+side].head
            knee=rig.pose.bones['Shin'+side].head
            ankle=rig.pose.bones['Foot'+side].head
            values.append(180-math.degrees((hip-knee).angle(ankle-knee)))
            lengths.append(((hip-knee).length,(ankle-knee).length))
            positions.append(ankle.copy())
        assert 0<min(values)<max(values)<155,(name,side,values)
        assert max(values)>85,(name,side,'insufficient recovery flexion',max(values))
        assert max(max(pair[i] for pair in lengths)-min(pair[i] for pair in lengths) for i in (0,1))<.003,(name,side,'bone stretch')
        assert (positions[0]-positions[-1]).length<.015,(name,side,'cycle seam')
        maxima.append(max(values))
        print(f'KNEE {folder.name}/{name}/{side}: {min(values):.1f}..{max(values):.1f} degrees, 49 samples, leg lengths invariant, ankle seam {(positions[0]-positions[-1]).length:.4f} m',flush=True)
    if folder==args.assets:
        scene.frame_set(1)
        articulated=0
        for side in ('L','R'):
            for digit in range(1,6):
                bends=[]
                for part in ('Middle','Tip'):
                    bone=rig.pose.bones[f'Finger{digit}{part}{side}']
                    relative=(bone.parent.matrix.inverted()@bone.matrix).to_quaternion()
                    rest=(bone.parent.bone.matrix_local.inverted()@bone.bone.matrix_local).to_quaternion()
                    bends.append(math.degrees(relative.rotation_difference(rest).angle))
                if min(bends)>1:articulated+=1
                assert max(bends)<100,(name,side,digit,'overfolded phalanx',bends)
        assert articulated>=8,(name,'rigid finger segments',articulated)
        print(f'FINGERS {name}: {articulated}/10 digits have independently flexed middle and tip joints in the holding pose; no segment exceeds 100 degrees.',flush=True)
    return maxima

for name in ('Max','Eva'):
    current=measure(args.assets,name)
    if args.compare:
        previous=measure(args.compare,name)
        print(f'RECOVERY COMPARISON {name}: peak knee flexion change L/R {current[0]-previous[0]:+.1f}/{current[1]-previous[1]:+.1f} degrees. Measurement only, not visual acceptance.',flush=True)
