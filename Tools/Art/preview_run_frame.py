"""Render the shipped models and road materials from the Unity camera's lens in Blender.

This is an asset and composition check. Unity is still needed to test the actual scene.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(sys.argv[sys.argv.index("--")+1]).resolve()
OUT = Path(sys.argv[sys.argv.index("--")+2]).resolve()
ART = ROOT/"Assets/Resources/ProductionArt"
MESHES = ROOT/"Assets/Resources/Meshes"

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def paint(name, color, path=None, rough=.62, emissive=False):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color,1)
    shader.inputs["Roughness"].default_value = rough
    if path:
        image = nodes.new("ShaderNodeTexImage")
        image.image = bpy.data.images.load(str(path),check_existing=True)
        links.new(image.outputs["Color"], shader.inputs["Emission Color" if emissive else "Base Color"])
        if emissive:
            shader.inputs["Base Color"].default_value = (0,0,0,1)
            shader.inputs["Emission Strength"].default_value = .85
    elif emissive:
        shader.inputs["Emission Color"].default_value = (*color,1)
        shader.inputs["Emission Strength"].default_value = 2
    return material


def cube(name, loc, scale, mat):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object
    o.name=name
    o.scale=scale
    o.data.materials.append(mat)
    return o


def mesh_asset(name, position, material, yaw=0):
    path=MESHES/(name+".obj")
    if not path.exists():
        return None
    verts,faces=[],[]
    for line in path.read_text().splitlines():
        if line.startswith("v "):
            x,y,z=map(float,line.split()[1:4]);verts.append((x,z,y))
        if line.startswith("f "):
            faces.append([int(i.split("/")[0])-1 for i in line.split()[1:]][::-1])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    o=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(material)
    o.location=position
    o.rotation_euler.z=yaw
    return o


atlas=paint("Character atlas",(1,1,1),ART/"CharacterAtlas.png",.73)
metal=paint("Weathered blue steel",(.079,.115,.161),rough=.54)
asphalt=paint("Highway",(.04,.045,.053),ART/"Highway.png",.49)
road=cube("Eleven meter highway",(0,62,-.27),(11,151,.48),asphalt)
nodes,links=asphalt.node_tree.nodes,asphalt.node_tree.links
mapping=nodes.new("ShaderNodeVectorMath")
mapping.operation="MULTIPLY"
mapping.inputs[1].default_value=(2,26,1)
links.new(nodes.new("ShaderNodeTexCoord").outputs["Generated"],mapping.inputs[0])
separate=nodes.new("ShaderNodeSeparateXYZ")
links.new(mapping.outputs[0],separate.inputs[0])
combine=nodes.new("ShaderNodeCombineXYZ")
links.new(separate.outputs["X"],combine.inputs["X"])
links.new(separate.outputs["Y"],combine.inputs["Y"])
tex=next(n for n in nodes if n.type=="TEX_IMAGE")
links.new(combine.outputs[0],tex.inputs["Vector"])

stripe=paint("Worn road paint",(.48,.43,.37))
amber=paint("Rail amber",(1,.25,.035),emissive=True)
warm=paint("Windows",(.6,.23,.065),emissive=True)
for direction in (-1,1):
    x=direction*5.57
    cube("Guardrail",(x,58,.70),(.14,131,.18),metal)
    cube("Lower railing",(x,58,.22),(.13,131,.12),metal)
    for row in range(14):
        y=-4+9*row
        cube("Railing mount",(x,y,.41),(.16,.26,1.12),metal)
        cube("Marker",(x-direction*.09,y,.60),(.05,.62,.10),amber)
        if row>2:
            h=7+2.6*((row*7+direction)%6)
            tower_x=x+direction*(3.8+(row%4)*2.4)
            cube("Tower",(tower_x,y,h*.5),(4.4,5.7,h),metal)
            for floor in range(1, int(h//2)):
                if (row+floor)%3==0:
                    cube("Lit storeys",(tower_x-direction*2.23,y,2*floor),(.03,2.8,.33),warm)
    for k in range(27):
        cube("Lane dash",(direction*1.8,-4+k*3.65,-.016),(.085,1.65,.016),stripe)

sky=paint("Distant panorama",(1,1,1),ART/"Horizon.jpg",emissive=True)
mesh=bpy.data.meshes.new("Atmosphere UV")
mesh.from_pydata([(-125,175,-54),(125,175,-54),(125,175,112),(-125,175,112)],[],[(0,1,2,3)])
layer=mesh.uv_layers.new(name="Panorama")
for i, uv in enumerate(((0,0),(1,0),(1,1),(0,1))):
    layer.data[i].uv=uv
obj=bpy.data.objects.new("Horizon texture",mesh)
bpy.context.collection.objects.link(obj)
mesh.materials.append(sky)

def character(name, pos, yaw, scale=1):
    old=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(ART/(name+".fbx")))
    parts=set(bpy.data.objects)-old
    rig,=[o for o in parts if o.type=="ARMATURE"]
    skin,=[o for o in parts if o.type=="MESH"]
    skin.data.materials.clear()
    skin.data.materials.append(atlas)
    pivot=bpy.data.objects.new(name+" actor",None)
    bpy.context.collection.objects.link(pivot)
    rig.parent=pivot
    pivot.rotation_euler.z=yaw
    pivot.location=pos
    pivot.scale=(scale,)*3
    return rig, skin

eva,eva_skin=character("Eva",(0,0,0),0)
character("Walker",(-.8,11.4,0),math.pi)
character("Runner",(2.65,16.7,0),math.pi,.97)
character("Armored",(-2.7,19.5,0),math.pi,1.14)
mesh_asset("lux_wreck_body",(2.6,10.6,0),paint("Burnt steel",(.12,.061,.047)),.13)
mesh_asset("lux_wreck_wheels",(2.6,10.6,0),metal,.13)
mesh_asset("lux_spikes",(-2.75,7.7,0),metal,-.06)
mesh_asset("lux_spike_base",(-2.75,7.7,0),metal,-.06)

scene.frame_set(42)
bpy.context.view_layer.update()
grip=eva.matrix_world@eva.pose.bones["WeaponGrip"].head
forward=eva.matrix_world@eva.pose.bones["WeaponForward"].head-grip
gun_yaw=math.atan2(-forward.x,forward.y)
for part in ("lux_weapon_rifle", "lux_weapon_rifle_steel"):
    weapon = mesh_asset(part,grip+Vector((0,0,.065)),metal,gun_yaw)
    weapon.scale = (.51,)*3

bpy.ops.object.camera_add(location=(0,-8.35,3.48))
scene.camera=bpy.context.object
scene.camera.rotation_euler=(Vector((0,16.5,1.52))-scene.camera.location).to_track_quat("-Z","Y").to_euler()
scene.camera.data.sensor_fit="VERTICAL"
scene.camera.data.angle=math.radians(52)
scene.camera.data.clip_end=200
scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=390,844,100
bpy.context.view_layer.update()
scene.frame_set(42)
evaluated = eva_skin.evaluated_get(bpy.context.evaluated_depsgraph_get())
coords=[world_to_camera_view(scene,scene.camera,evaluated.matrix_world@v.co) for v in evaluated.data.vertices]
low=min(p.y for p in coords)
high=max(p.y for p in coords)
assert .12<high-low<.35 and 0<low<high<.70,(low,high)
print(f"FRAME CHECK Eva occupies {(high-low)*844:.0f} px of the 844 px portrait (y={low:.3f}..{high:.3f}).",flush=True)

def sun(rotation, energy, color):
    bpy.ops.object.light_add(type="SUN",rotation=tuple(map(math.radians,rotation)))
    lamp=bpy.context.object
    lamp.data.energy=energy;lamp.data.color=color;lamp.data.angle=.07

sun((36,-30,145),1.5,(1,.63,.36))
sun((55,20,12),.70,(.50,.66,1))
scene.world=bpy.data.worlds.new("Blue hour")
scene.world.use_nodes=True
background=scene.world.node_tree.nodes.get("Background")
background.inputs[0].default_value=(.20,.28,.42,1)
background.inputs[1].default_value=.49
scene.render.engine="CYCLES"
scene.cycles.samples=24
scene.cycles.use_denoising=False
scene.render.filepath=str(OUT)
bpy.ops.render.render(write_still=True)
