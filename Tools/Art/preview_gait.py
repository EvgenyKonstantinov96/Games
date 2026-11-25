"""Render a loop of the actual imported run takes for motion review in Blender."""

import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--")+1:]
assets, output = map(lambda x: Path(x).resolve(), args[:2])
output.mkdir(parents=True, exist_ok=True)

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

for name, position, yaw in (
    ("Eva",(-1.94,0,0),math.pi*.52), ("Max",(-.73,0,0),math.pi*.65),
    ("Walker",(.73,0,0),math.pi*.92), ("Runner",(1.94,0,0),math.pi*1.04),
):
    old = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(assets/(name+".fbx")))
    actors = set(bpy.data.objects)-old
    rig, = (o for o in actors if o.type == "ARMATURE")
    skin, = (o for o in actors if o.type == "MESH")
    skin.data.materials.clear()
    skin.data.materials.append(atlas)
    placement = bpy.data.objects.new(name+" preview placement",None)
    bpy.context.collection.objects.link(placement)
    rig.parent = placement
    placement.location = position
    placement.rotation_euler.z = yaw

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
camera.data.ortho_scale = 5.44
scene.camera = camera
scene.render.engine = "CYCLES"
scene.cycles.samples = 20
scene.cycles.use_denoising = False
scene.render.resolution_x,scene.render.resolution_y = 704,396
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.fps = 30
scene.frame_start,scene.frame_end = 33,56
scene.render.filepath = str(output)+"/"
bpy.ops.render.render(animation=True)
