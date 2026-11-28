"""Render the actual OBJ field kit to transparent UI icons, not concept placeholders."""
import argparse,sys,math
from pathlib import Path
import bpy
from mathutils import Vector

parser=argparse.ArgumentParser()
parser.add_argument('--out',type=Path,required=True)
parser.add_argument('--only',default='medkit,grenade,drone')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/Resources/ProductionArt'
MESHES=ROOT/'Assets/Resources/Meshes'


def mesh(name,mat,pos=(0,0,0)):
    points,uvs,normals,faces=[],[],[],[]
    for line in (MESHES/(name+'.obj')).read_text().splitlines():
        if line.startswith('v '):
            x,y,z=map(float,line.split()[1:]);points.append((x,z,y))
        elif line.startswith('vt '):uvs.append(tuple(map(float,line.split()[1:])))
        elif line.startswith('vn '):
            x,y,z=map(float,line.split()[1:]);normals.append((x,z,y))
        elif line.startswith('f '):faces.append([tuple(int(n)-1 for n in word.split('/')) for word in line.split()[1:]][::-1])
    data=bpy.data.meshes.new(name);data.from_pydata(points,[],[[i for i,u,n in face] for face in faces])
    layer=data.uv_layers.new(name='Atlas')
    split=[]
    for polygon,face in zip(data.polygons,faces):
        polygon.use_smooth=True
        for loop,(i,u,n) in zip(polygon.loop_indices,face):layer.data[loop].uv=uvs[u];split.append(normals[n])
    data.normals_split_custom_set(split)
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    data.materials.append(mat);obj.location=pos
    return obj


def render(kind):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    mat=bpy.data.materials.new('Shared CombatAtlas');mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links;shader=nodes.get('Principled BSDF')
    tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ART/'CombatAtlas.png'))
    links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    packed=nodes.new('ShaderNodeTexImage');packed.image=bpy.data.images.load(str(ART/'CombatAtlas_surface.png'));packed.image.colorspace_settings.name='Non-Color'
    split=nodes.new('ShaderNodeSeparateColor');links.new(packed.outputs['Color'],split.inputs['Color'])
    links.new(split.outputs['Red'],shader.inputs['Metallic'])
    invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
    links.new(packed.outputs['Alpha'],invert.inputs[1]);links.new(invert.outputs[0],shader.inputs['Roughness'])
    mesh('combat_'+kind,mat)
    if kind=='drone':
        for x in (-1,1):
            for z in (-1,1):mesh('combat_rotor',mat,(x*.45,z*.38,.108))
    bpy.context.view_layer.update()
    points=[o.matrix_world@Vector(c) for o in bpy.data.objects if o.type=='MESH' for c in o.bound_box]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center=(lo+hi)*.5;span=max(hi-lo)
    for direction,power,size,color in (((-2,-3,5),500,3,(1,.94,.86)),((3,-2,2),250,3,(.72,.83,1)),((0,3,3),450,2,(.82,.91,1))):
        bpy.ops.object.light_add(type='AREA',location=center+Vector(direction)*span)
        lamp=bpy.context.object;lamp.data.energy=power*span*span;lamp.data.size=size*span;lamp.data.color=color
        lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
    scene.world=bpy.data.worlds.new('Neutral');scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.35
    bpy.ops.object.camera_add(location=center+Vector((1.5,-3,2.3 if kind=='drone' else 1.35))*span)
    cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO'
    scene.camera=cam
    bpy.context.view_layer.update()
    view=[cam.matrix_world.inverted()@point for point in points]
    cam.data.ortho_scale=max(max(p[i] for p in view)-min(p[i] for p in view) for i in (0,1))*1.16
    scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False
    scene.render.film_transparent=True;scene.render.resolution_x=scene.render.resolution_y=512;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=str(args.out/('kit_'+kind+'.png'));bpy.ops.render.render(write_still=True)


args.out.mkdir(parents=True,exist_ok=True)
for kind in args.only.split(','):render(kind)
