"""Neutral front/back review of the actual shipped FBX and texture maps in Blender.

This is an asset render, not a Unity screenshot. The same script/lights can
render an older exported package with --assets for a controlled comparison.
"""
import argparse
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector,Matrix

parser=argparse.ArgumentParser()
parser.add_argument('--assets',type=Path,default=Path(__file__).resolve().parents[2]/'Assets/Resources/ProductionArt')
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--names',default='Max,Eva,Max,Eva')
parser.add_argument('--back',default='2,3',help='Comma-separated slot indices shown from behind')
parser.add_argument('--frame',type=int,default=1)
parser.add_argument('--samples',type=int,default=32)
parser.add_argument('--width',type=int,default=1280)
parser.add_argument('--height',type=int,default=800)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
names=args.names.split(',')
backs={int(x) for x in args.back.split(',') if x}
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=args.samples
scene.cycles.use_denoising=False
scene.render.resolution_x,scene.render.resolution_y=args.width,args.height
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'


def material(name,color,rough=.7):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    bsdf=mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Roughness'].default_value=rough
    return mat


atlas=material('Shipped atlas',(1,1,1))
nodes,links=atlas.node_tree.nodes,atlas.node_tree.links
shader=nodes.get('Principled BSDF')
for suffix,target in (('', 'Base Color'),('_normal','Normal'),('_surface','Metallic'),('_emission','Emission Color')):
    path=args.assets/('CharacterAtlas'+suffix+'.png')
    if not path.exists():continue
    tex=nodes.new('ShaderNodeTexImage')
    tex.image=bpy.data.images.load(str(path),check_existing=True)
    if suffix in ('_normal','_surface'):tex.image.colorspace_settings.name='Non-Color'
    if suffix=='_normal':
        normal=nodes.new('ShaderNodeNormalMap')
        normal.inputs['Strength'].default_value=.35
        links.new(tex.outputs['Color'],normal.inputs['Color'])
        links.new(normal.outputs['Normal'],shader.inputs[target])
    elif suffix=='_surface':
        split=nodes.new('ShaderNodeSeparateColor')
        links.new(tex.outputs['Color'],split.inputs['Color'])
        links.new(split.outputs['Red'],shader.inputs['Metallic'])
        invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
        links.new(tex.outputs['Alpha'],invert.inputs[1])
        links.new(invert.outputs[0],shader.inputs['Roughness'])
    else:links.new(tex.outputs['Color'],shader.inputs[target])
shader.inputs['Emission Strength'].default_value=.8
gunmat=material('Rifle dark housing',(.045,.058,.065),.45)
steel=material('Rifle metal',(.19,.23,.25),.36)
steel.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value=.65


def gun(rig,placement):
    bones=rig.pose.bones
    socket=bones['WeaponGrip'].head
    forward=(bones['WeaponForward'].head-socket).normalized()
    right=forward.cross(Vector((0,0,1))).normalized()
    up=right.cross(forward).normalized()
    for name,mat in (('lux_weapon_rifle',gunmat),('lux_weapon_rifle_steel',steel)):
        path=Path(__file__).resolve().parents[2]/'Assets/Resources/Meshes'/(name+'.obj')
        vertices,faces=[],[]
        for row in path.read_text().splitlines():
            if row.startswith('v '):
                x,y,z=map(float,row.split()[1:])
                vertices.append(socket+up*.065+(right*x+up*y+forward*z)*.51)
            elif row.startswith('f '):faces.append([int(i.split('/')[0])-1 for i in row.split()[1:]])
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces)
        obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
        obj.parent=rig;mesh.materials.append(mat)


for index,name in enumerate(names):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(args.assets/(name+'.fbx')),use_anim=True,anim_offset=0)
    imported=set(bpy.data.objects)-before
    rig,=(o for o in imported if o.type=='ARMATURE')
    for skin in (o for o in imported if o.type=='MESH'):
        skin.hide_render=not skin.name.split('.')[0].endswith('_LOD0')
        skin.data.materials.clear();skin.data.materials.append(atlas)
    placement=bpy.data.objects.new(name+' review placement',None)
    bpy.context.collection.objects.link(placement)
    rig.parent=placement
    placement.location.x=(index-(len(names)-1)*.5)*1.15
    placement.rotation_euler.z=math.pi*.10 if index in backs else math.pi*1.10
    scene.frame_set(args.frame)
    bpy.context.view_layer.update()
    if name in ('Max','Eva'):gun(rig,placement)

bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,0))
bpy.context.object.data.materials.append(material('Slate stage',(.055,.071,.095)))
scene.world=bpy.data.worlds.new('Neutral ambient');scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background')
bg.inputs[0].default_value=(.20,.23,.30,1);bg.inputs[1].default_value=.4
for pos,color,power,size in (((-3,-4,5),(1,.88,.76),1250,4),((4,-2,4),(.71,.82,1),850,4),((0,3,5),(.58,.73,1),1500,4)):
    bpy.ops.object.light_add(type='AREA',location=pos)
    lamp=bpy.context.object;lamp.data.energy=power;lamp.data.color=color;lamp.data.size=size
    lamp.rotation_euler=(Vector((0,0,1.1))-lamp.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-9,3.1))
cam=bpy.context.object
cam.rotation_euler=(Vector((0,0,1.05))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=max(4.75,len(names)*1.15+.15)
scene.camera=cam
scene.frame_set(args.frame)
args.out.parent.mkdir(parents=True,exist_ok=True)
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(args.out)
bpy.ops.render.render(write_still=True)
