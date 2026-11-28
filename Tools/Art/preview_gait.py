"""Render a loop of the shipped FBX run or walk takes for motion review in Blender."""

import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector,Matrix

args = sys.argv[sys.argv.index("--")+1:]
assets, output = map(lambda x: Path(x).resolve(), args[:2])
output.mkdir(parents=True, exist_ok=True)
walking = "--walk" in args
falling = "--fall" in args

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

atlas = bpy.data.materials.new("Shipped character atlas")
atlas.use_nodes = True
nodes = atlas.node_tree.nodes
texture = nodes.new("ShaderNodeTexImage")
texture.image = bpy.data.images.load(str(assets/"CharacterAtlas.png"))
shader = nodes.get("Principled BSDF")
shader.inputs["Roughness"].default_value = .68
atlas.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
normals = nodes.new("ShaderNodeTexImage")
normals.image = bpy.data.images.load(str(assets/"CharacterAtlas_normal.png"))
normals.image.colorspace_settings.name = "Non-Color"
normal = nodes.new("ShaderNodeNormalMap")
normal.inputs["Strength"].default_value = .35
atlas.node_tree.links.new(normals.outputs["Color"], normal.inputs["Color"])
atlas.node_tree.links.new(normal.outputs["Normal"], shader.inputs["Normal"])

cast = (
    ("Eva",(-1.94,0,0),math.pi*.52), ("Max",(-.73,0,0),math.pi*.65),
    ("Walker",(.73,0,0),math.pi*.92), ("Runner",(1.94,0,0),math.pi*1.04),
)
if walking:
    cast = (("Eva",(-.79,0,0),math.pi*.51), ("Max",(.79,0,0),math.pi*.66))
if falling:
    cast = (("Walker",(-1.78,0,0),math.pi*.89), ("Runner",(0,0,0),math.pi*1.11),
            ("Brute",(1.80,0,0),math.pi*1.04))
for name, position, yaw in cast:
    old = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(assets/(name+".fbx")))
    actors = set(bpy.data.objects)-old
    rig, = (o for o in actors if o.type == "ARMATURE")
    skins = [o for o in actors if o.type == "MESH"]
    skin = next((o for o in skins if o.name.endswith("_LOD0")), skins[0])
    for lod in skins:
        if lod != skin: lod.hide_render = True
    skin.data.materials.clear()
    skin.data.materials.append(atlas)
    placement = bpy.data.objects.new(name+" preview placement",None)
    bpy.context.collection.objects.link(placement)
    rig.parent = placement
    placement.location = position
    placement.rotation_euler.z = yaw

    if name in ('Max','Eva'):
        for suffix,color in (('',(.045,.058,.065)),('_steel',(.19,.23,.25))):
            path=assets.parent/'Meshes'/('lux_weapon_rifle'+suffix+'.obj')
            points,faces=[],[]
            for line in path.read_text().splitlines():
                if line.startswith('v '):
                    x,y,z=map(float,line.split()[1:]);points.append((x*.51,z*.51,y*.51+.065))
                elif line.startswith('f '):faces.append([int(word.split('/')[0])-1 for word in line.split()[1:]])
            data=bpy.data.meshes.new(name+' rifle'+suffix);data.from_pydata(points,[],faces)
            gun=bpy.data.objects.new(data.name,data);bpy.context.collection.objects.link(gun);gun.parent=rig
            mat=bpy.data.materials.new(data.name);mat.diffuse_color=(*color,1);data.materials.append(mat)

            def follow_weapon(scene,depsgraph=None,obj=gun,actor=rig):
                grip=actor.pose.bones['WeaponGrip'].head
                forward=(actor.pose.bones['WeaponForward'].head-grip).normalized()
                right=forward.cross(Vector((0,0,1))).normalized();up=right.cross(forward).normalized()
                basis=Matrix((right,forward,up)).transposed().to_4x4();basis.translation=grip
                obj.matrix_local=basis
            bpy.app.handlers.frame_change_post.append(follow_weapon)
            follow_weapon(scene)

bpy.ops.mesh.primitive_plane_add(size=200)
floor = bpy.context.object
floor.location.z = -.02
surface = bpy.data.materials.new("Neutral stage")
surface.diffuse_color = (.07,.093,.128,1)
floor.data.materials.append(surface)

for location, color, power, size in (
    ((-4,-5,5),(1,.77,.54),1100,4),
    ((4,-2,6),(.58,.72,1),850,4.5),
    ((0,4,5),(.36,.58,1),1200,5),
):
    bpy.ops.object.light_add(type="AREA",location=location)
    lamp = bpy.context.object
    lamp.data.color = color
    lamp.data.energy = power
    lamp.data.size = size
    lamp.rotation_euler = (Vector((0,0,1))-lamp.location).to_track_quat("-Z","Y").to_euler()

scene.world = bpy.data.worlds.new("Stage ambient")
scene.world.use_nodes = True
ambient = scene.world.node_tree.nodes.get("Background")
ambient.inputs[0].default_value = (.21,.29,.48,1)
ambient.inputs[1].default_value = .35

bpy.ops.object.camera_add(location=(.02,-11,4.1))
camera = bpy.context.object
camera.rotation_euler = (Vector((0,0,.97))-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 6.2 if falling else 4.04 if walking else 5.44
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 20
scene.cycles.use_denoising = False
if falling:
    scene.cycles.samples = 8
    scene.cycles.use_denoising = True
scene.render.resolution_x,scene.render.resolution_y = 704,396
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.fps = 30
scene.frame_start,scene.frame_end = (219,252) if walking else (33,56)
if falling:
    scene.frame_start, scene.frame_end = 147, 168
if "--step" in args:
    scene.frame_step = int(args[args.index("--step")+1])
if "--still" in args:
    scene.cycles.samples = 40
    scene.render.resolution_x, scene.render.resolution_y = 1152, 648
    scene.frame_set(int(args[args.index("--still")+1]))
    scene.render.filepath = str(output/"still.png")
    bpy.ops.render.render(write_still=True)
else:
    scene.render.filepath = str(output)+"/"
    bpy.ops.render.render(animation=True)
