"""Check joint continuity, loaded-run shape and absolute rifle/palm contact on reimported FBX."""
import argparse,math,sys
from pathlib import Path
import bpy
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

parser=argparse.ArgumentParser()
parser.add_argument('--assets',type=Path,required=True)
parser.add_argument('--report-only',action='store_true',help='Print measurements without declaring a pass, e.g. for old assets')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])

def check(value,detail):
    if not args.report_only:assert value,detail

def mount_frame(rig):
    grip=rig.pose.bones['WeaponGrip'].head
    forward=(rig.pose.bones['WeaponForward'].head-grip).normalized()
    right=forward.cross(Vector((0,0,1))).normalized();up=right.cross(forward)
    matrix=Matrix((right,forward,up)).transposed().to_4x4();matrix.translation=grip
    return matrix

points=[];faces=[]
for line in (args.assets.parent/'Meshes/lux_weapon_rifle.obj').read_text().splitlines():
    if line.startswith('v '):
        x,y,z=map(float,line.split()[1:]);points.append(Vector((x*.51,z*.51,y*.51+.065)))
    elif line.startswith('f '):
        face=[int(w.split('/')[0])-1 for w in line.split()[1:]]
        if .15<sum(points[i].y for i in face)/len(face)<.37:faces.append(face)
foreend=BVHTree.FromPolygons(points,faces)

for path in sorted(args.assets.glob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),anim_offset=0)
    rig,=(o for o in bpy.context.scene.objects if o.type=='ARMATURE');pose=rig.pose.bones
    scene=bpy.context.scene;scene.frame_set(1)
    hero=path.stem in ('Max','Eva')
    skin=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.endswith('_LOD0'))
    crawler=path.stem=='Crawler'
    hands={side:[v.index for v in skin.data.vertices if any(
        (skin.vertex_groups[g.group].name.startswith(('Hand','Finger')) and
         skin.vertex_groups[g.group].name.endswith(side) and g.weight>.70) for g in v.groups)] for side in ('L','R')} if crawler else {}
    floor=[];hand_floor={side:[] for side in hands}
    mount=pose['HandR'].matrix.inverted()@mount_frame(rig) if hero else None
    watched=['Pelvis','Chest','Head']+[part+side for part in ('Thigh','Shin','Foot','UpperArm','Forearm','Hand') for side in ('L','R')]
    samples=[];knee=[];width=[];palm_gap=[];palm_up=[];curls=[];bend=[]
    for frame in range(33,58):
        scene.frame_set(frame)
        samples.append({name:pose[name].matrix.copy() for name in watched})
        for side in ('L','R'):
            a=pose['Thigh'+side];b=pose['Shin'+side]
            bend.append(math.degrees((a.tail-a.head).angle(b.tail-b.head)))
            knee.append(b.head.z)
        width.append(abs(pose['FootR'].head.x-pose['FootL'].head.x))
        if crawler:
            mesh=skin.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
            floor.append(min(v.co.z for v in mesh.vertices))
            for side,indices in hands.items():hand_floor[side].append(min(mesh.vertices[i].co.z for i in indices))
        if not hero:continue
        weapon=pose['HandR'].matrix@mount;inverse=weapon.inverted()
        wrist=inverse@pose['HandL'].head
        index=inverse@pose['Finger2L'].head;little=inverse@pose['Finger5L'].head
        middle=inverse@pose['Finger3L'].head
        normal=-(index-wrist).cross(little-wrist).normalized()
        palm=wrist.lerp(middle,.55)+normal*.014
        palm_gap.append(foreend.find_nearest(palm)[3]);palm_up.append(normal.z)
        for digit in range(2,6):
            first=pose[f'Finger{digit}L'];second=pose[f'Finger{digit}MiddleL']
            curls.append(math.degrees((first.tail-first.head).angle(second.tail-second.head)))
    def arc(a,b):
        angle=math.degrees(a.to_quaternion().rotation_difference(b.to_quaternion()).angle)
        return min(angle,360-angle) # q and -q represent the same orientation
    jump=max(arc(a[name],b[name]) for a,b in zip(samples,samples[1:]) for name in watched)
    loop=max((samples[0][name].translation-samples[-1][name].translation).length for name in watched)
    angle_loop=max(arc(samples[0][name],samples[-1][name]) for name in watched)
    check(jump<42,(path.stem,'abrupt joint rotation',jump))
    check(loop<.015 and angle_loop<.4,(path.stem,'open run cycle',loop,angle_loop))
    check(all(math.isfinite(value) for value in knee+bend+width),(path.stem,'nonfinite pose'))
    print(f'JOINTS {path.stem}: peak adjacent rotation {jump:.2f} deg, loop {loop*1000:.2f} mm / {angle_loop:.3f} deg, knee flexion {min(bend):.1f}..{max(bend):.1f} deg.',flush=True)
    if hero:
        height=1.94 if path.stem=='Max' else 1.81
        check(max(knee)<height*.35,(path.stem,'loaded run lifts knees too high',max(knee)))
        check(max(width)<.26,(path.stem,'feet excessively wide',max(width)))
        check(max(palm_gap)<.025 and min(palm_up)>.80,(path.stem,'palm does not support rifle',max(palm_gap),min(palm_up)))
        check(min(curls)>35,(path.stem,'fingers do not wrap around fore-end',min(curls)))
        print(f'CONTACT {path.stem}: peak knee height {max(knee):.3f} m, foot separation {max(width):.3f} m; maximum palm-pad/fore-end distance {max(palm_gap)*1000:.2f} mm, minimum upward palm normal {min(palm_up):.3f}, minimum finger middle-joint bend {min(curls):.1f} deg.',flush=True)
    if crawler:
        check(min(floor)>-.012,(path.stem,'skin penetrates contact plane',min(floor)))
        for side,heights in hand_floor.items():
            grounded=sum(value<.035 for value in heights[:-1])
            check(min(heights)<.025 and max(heights)>.07 and grounded>=12,
                  (path.stem,side,'missing hand support/recovery',min(heights),max(heights),grounded))
            print(f'CRAWLER {side}: skin floor {min(floor)*1000:.2f} mm, lowest hand {min(heights)*1000:.2f} mm, recovery {max(heights)*1000:.2f} mm; {grounded}/24 frames within 35 mm of contact plane.',flush=True)
    print(('MEASURE ONLY' if args.report_only else 'CONTACT/CONTINUITY PASS')+' '+path.stem+' — Blender FBX, not Unity.',flush=True)
