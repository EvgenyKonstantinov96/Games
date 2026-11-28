"""Render unclipped full-body contact/stride views of the shipped FBX, not Unity frames."""
import argparse,math,sys
from pathlib import Path
import bpy
from mathutils import Vector,Matrix

parser=argparse.ArgumentParser()
parser.add_argument('--assets',type=Path,required=True)
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--cast',default='Max,Eva')
parser.add_argument('--view',choices=('front','side','back','threequarter'),default='side')
parser.add_argument('--frame',type=int,default=39)
parser.add_argument('--animate',action='store_true')
parser.add_argument('--authored-only',action='store_true',help='Do not apply the offline support-hand correction')
parser.add_argument('--samples',type=int,default=0)
parser.add_argument('--small',action='store_true')
parser.add_argument('--eevee',action='store_true',help='Try headless Eevee if supported by the machine')
parser.add_argument('--weapon',choices=('rifle','pistol','shotgun','minigun'),default='rifle')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
args.out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
atlas=bpy.data.materials.new('Actual shipped atlas');atlas.use_nodes=True
nodes=atlas.node_tree.nodes;links=atlas.node_tree.links
shader=nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.73
for name,socket in [('CharacterAtlas.png','Base Color')]:
    tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(args.assets/name))
    links.new(tex.outputs['Color'],shader.inputs[socket])
tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(args.assets/'CharacterAtlas_normal.png'))
tex.image.colorspace_settings.name='Non-Color'
normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.35
links.new(tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],shader.inputs['Normal'])

cast=args.cast.split(',');spacing=1.8
yaw={'front':math.pi,'side':math.pi*.52,'back':0,'threequarter':math.pi*.74}[args.view]
for i,name in enumerate(cast):
    old=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(args.assets/(name+'.fbx')),anim_offset=0)
    objects=set(bpy.data.objects)-old
    rig,=(o for o in objects if o.type=='ARMATURE')
    for skin in (o for o in objects if o.type=='MESH'):
        skin.hide_render=not skin.name.endswith('_LOD0')
        skin.data.materials.clear();skin.data.materials.append(atlas)
    placement=bpy.data.objects.new(name+' placement',None);bpy.context.collection.objects.link(placement)
    rig.parent=placement;placement.location.x=(i-(len(cast)-1)*.5)*spacing
    placement.rotation_euler.z=yaw
    if name not in ('Max','Eva'):continue
    scene.frame_set(1)
    grip=rig.pose.bones['WeaponGrip'].head
    forward=(rig.pose.bones['WeaponForward'].head-grip).normalized()
    right=forward.cross(Vector((0,0,1))).normalized();up=right.cross(forward)
    frame=Matrix((right,forward,up)).transposed().to_4x4();frame.translation=grip
    mount=rig.pose.bones['HandR'].matrix.inverted()@frame
    point=frame.inverted()@rig.pose.bones['HandL'].head
    orientation=frame.to_quaternion().inverted()@rig.pose.bones['HandL'].matrix.to_quaternion()
    def basis(forward,normal):
        forward=forward.normalized();normal=(normal-forward*normal.dot(forward)).normalized()
        return Matrix((forward.cross(normal),forward,normal)).transposed().to_quaternion()
    if args.weapon=='pistol':
        point+=Vector((.035,-.250,-.015))
        orientation=basis(Vector((.03,.22,-1)),Vector((1,0,.03)))@basis(Vector((1,.12,.08)),Vector((-.08,0,1))).inverted()@orientation
    elif args.weapon=='shotgun':point+=Vector((0,.030,0))
    elif args.weapon=='minigun':point+=Vector((-.010,.040,0))
    def support(scene,depsgraph=None,actor=rig,offset=mount.copy(),local_point=point.copy(),local_rotation=orientation.copy()):
        # Offline counterpart for viewing the support targets. Production C# is
        # executed separately by check_upper_aim.py; this is not its execution.
        gun=actor.pose.bones['HandR'].matrix@offset
        upper=actor.pose.bones['UpperArmL'];lower=actor.pose.bones['ForearmL'];hand=actor.pose.bones['HandL']
        shoulder=upper.head.copy();elbow=lower.head.copy();wrist=hand.head.copy()
        target=gun@local_point;direction=(target-shoulder).normalized()
        a=(elbow-shoulder).length;b=(wrist-elbow).length
        distance=min(a+b-.005,max(abs(a-b)+.008,(target-shoulder).length))
        pole=(elbow-shoulder)-direction*(elbow-shoulder).dot(direction)
        along=(a*a+distance*distance-b*b)/(2*distance)
        joint=shoulder+direction*along+pole.normalized()*math.sqrt(max(0,a*a-along*along))
        end=shoulder+direction*distance
        def aim(bone,point):
            origin=bone.head.copy();q=(bone.tail-bone.head).rotation_difference(point-origin)
            m=q.to_matrix().to_4x4()@bone.matrix;m.translation=origin;bone.matrix=m
            bpy.context.view_layer.update()
        aim(upper,joint);aim(lower,end)
        m=(gun.to_quaternion()@local_rotation).to_matrix().to_4x4();m.translation=hand.head.copy();hand.matrix=m
        bpy.context.view_layer.update()
    if not args.authored_only:bpy.app.handlers.frame_change_post.append(support)
    scale={'rifle':.51,'pistol':.37,'shotgun':.55,'minigun':.60}[args.weapon]
    for suffix,color in [('',(.055,.065,.072)),('_steel',(.20,.23,.25))]:
        points=[];faces=[]
        path=args.assets.parent/'Meshes'/('lux_weapon_'+args.weapon+suffix+'.obj')
        for line in path.read_text().splitlines():
            if line.startswith('v '):
                x,y,z=map(float,line.split()[1:]);points.append((x*scale,z*scale,y*scale+.065))
            elif line.startswith('f '):faces.append([int(w.split('/')[0])-1 for w in line.split()[1:]])
        data=bpy.data.meshes.new(name+' '+args.weapon+suffix);data.from_pydata(points,[],faces)
        gun=bpy.data.objects.new(data.name,data);bpy.context.collection.objects.link(gun);gun.parent=rig
        mat=bpy.data.materials.new(data.name);mat.diffuse_color=(*color,1);data.materials.append(mat)
        def follow(scene,depsgraph=None,actor=rig,obj=gun,offset=mount.copy()):
            obj.matrix_local=actor.pose.bones['HandR'].matrix@offset
        bpy.app.handlers.frame_change_post.append(follow);follow(scene)

bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.location.z=-.025
mat=bpy.data.materials.new('Matte stage');mat.diffuse_color=(.048,.065,.083,1);floor.data.materials.append(mat)
for point,color,power,size in [((-3,-4,6),(1,.84,.69),950,5),((4,-2,5),(.65,.80,1),800,4),((0,4,5),(.54,.73,1),1100,4)]:
    bpy.ops.object.light_add(type='AREA',location=point);lamp=bpy.context.object
    lamp.data.color=color;lamp.data.energy=power;lamp.data.size=size
    lamp.rotation_euler=(Vector((0,0,1))-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('Neutral ambient');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.22,.31,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.40
bpy.ops.object.camera_add(location=(0,-9,3.0));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,1.02))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=max(4.2,len(cast)*spacing+.55)
scene.camera=camera;scene.render.engine='CYCLES'
scene.cycles.samples=args.samples or (40 if args.animate else 96)
# This headless Blender build has no OpenImageDenoise support.
scene.cycles.use_denoising=False
scene.render.resolution_x=960 if len(cast)<3 else 1440
scene.render.resolution_y=720 if len(cast)<3 else 800
scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
if args.small:
    scene.render.resolution_x=720 if len(cast)<3 else 1080
    scene.render.resolution_y=540 if len(cast)<3 else 600
if args.eevee:
    scene.render.engine='BLENDER_EEVEE_NEXT'
    scene.eevee.taa_render_samples=args.samples or 32
scene.render.fps=30
if args.animate:
    scene.frame_start=33;scene.frame_end=56;scene.frame_step=2
    scene.render.filepath=str(args.out)+'/'
    bpy.ops.render.render(animation=True)
else:
    scene.frame_set(args.frame);scene.render.filepath=str(args.out/(args.view+'.png'))
    bpy.ops.render.render(write_still=True)
