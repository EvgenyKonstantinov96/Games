"""Render the shipped models and road materials from the Unity camera's lens in Blender.

This is an asset and composition check. Unity is still needed to test the actual scene.
"""
import math
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(sys.argv[sys.argv.index("--")+1]).resolve()
OUT = Path(sys.argv[sys.argv.index("--")+2]).resolve()
ART = ROOT/"Assets/Resources/ProductionArt"
MESHES = ROOT/"Assets/Resources/Meshes"
TEXTURES = ROOT/"Assets/Resources/Textures"
GAME_SOURCE = (ROOT/"Assets/Scripts/ZSRGame.cs").read_text()
WORLD_SOURCE = (ROOT/"Assets/Scripts/ZSRWorld.cs").read_text()


def number_setting(source, name):
    return float(re.search(r"\b" + name + r"\s*=\s*(-?[\d.]+)f;", source)[1])


def camera_setting(name):
    return number_setting(GAME_SOURCE,"RunCamera"+name)


ROAD_WIDTH = number_setting(WORLD_SOURCE,"DeckWidth")
RAIL_OFFSET = number_setting(WORLD_SOURCE,"RailX")

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
        normal_path=path.with_stem(path.stem+"_normal")
        if not emissive and normal_path.exists():
            normal=nodes.new("ShaderNodeTexImage")
            normal.image=bpy.data.images.load(str(normal_path),check_existing=True)
            normal.image.colorspace_settings.name="Non-Color"
            unpack=nodes.new("ShaderNodeNormalMap")
            unpack.inputs["Strength"].default_value=.35
            links.new(normal.outputs["Color"],unpack.inputs["Color"])
            links.new(unpack.outputs[0],shader.inputs["Normal"])
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


def mesh_asset(name, position, material, yaw=0, scale=1):
    path=MESHES/(name+".obj")
    verts,texcoords,faces=[],[],[]
    for line in path.read_text().splitlines():
        if line.startswith("v "):
            x,y,z=map(float,line.split()[1:4]);verts.append((x,z,y))
        if line.startswith("vt "):
            texcoords.append(tuple(map(float,line.split()[1:3])))
        if line.startswith("f "):
            faces.append([i.split("/") for i in line.split()[1:]][::-1])
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],[[int(i[0])-1 for i in face] for face in faces])
    if texcoords:
        uv_layer=mesh.uv_layers.new(name="Atlas")
        for polygon,face in zip(mesh.polygons,faces):
            for loop,corner in zip(polygon.loop_indices,face):
                if len(corner)>1 and corner[1]:
                    uv_layer.data[loop].uv=texcoords[int(corner[1])-1]
    o=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(material)
    o.location=position
    o.rotation_euler.z=yaw
    o.scale=scale if isinstance(scale,tuple) else (scale,)*3
    return o


atlas=paint("Character atlas",(1,1,1),ART/"CharacterAtlas.png",.73)
if (ART/"CharacterAtlas_emission.png").exists():
    nodes, links = atlas.node_tree.nodes, atlas.node_tree.links
    emission = nodes.new("ShaderNodeTexImage")
    emission.image = bpy.data.images.load(str(ART/"CharacterAtlas_emission.png"), check_existing=True)
    light = nodes.get("Principled BSDF")
    links.new(emission.outputs["Color"], light.inputs["Emission Color"])
    light.inputs["Emission Strength"].default_value = 1.25
metal=paint("Weathered blue steel",(.079,.115,.161),rough=.54)
asphalt=paint("Highway",(.04,.045,.053),ART/"Highway.png",.49)
road=cube("Elevated highway",(0,62,-.27),(ROAD_WIDTH,151,.48),asphalt)
cube("Slate distant bridge",(0,164,-.16),(ROAD_WIDTH,156,.20),paint("Distant deck",(.19,.23,.28)))
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
for node in nodes:
    if node.type=="TEX_IMAGE":
        links.new(combine.outputs[0],node.inputs["Vector"])

stripe=paint("Worn road paint",(.48,.43,.37))
amber=paint("Rail amber",(1,.25,.035),emissive=True)
props=paint("Track prop atlas",(1,1,1),ART/"TrackPropAtlas.png",.64)
shoulder_glow = paint("Soft bridge reflector spill",(.87,.37,.125),emissive=True)
nodes, links = shoulder_glow.node_tree.nodes, shoulder_glow.node_tree.links
soft = nodes.new("ShaderNodeTexImage")
soft.image = bpy.data.images.load(str(ART/"ParticleSoft.png"), check_existing=True)
alpha = nodes.new("ShaderNodeMath")
alpha.operation = "MULTIPLY"
alpha.inputs[1].default_value = .21
links.new(soft.outputs["Alpha"], alpha.inputs[0])
mix = nodes.new("ShaderNodeMixShader")
links.new(alpha.outputs[0], mix.inputs[0])
links.new(nodes.new("ShaderNodeBsdfTransparent").outputs[0], mix.inputs[1])
links.new(nodes.get("Principled BSDF").outputs[0], mix.inputs[2])
links.new(mix.outputs[0], nodes.get("Material Output").inputs[0])
facade=paint("Weathered city facade",(1,1,1),TEXTURES/"city_facades.png",.72)
emission=facade.node_tree.nodes.new("ShaderNodeTexImage")
emission.image=bpy.data.images.load(str(TEXTURES/"city_facades_emission.png"),check_existing=True)
facade_shader=facade.node_tree.nodes.get("Principled BSDF")
facade.node_tree.links.new(emission.outputs["Color"],facade_shader.inputs["Emission Color"])
facade_shader.inputs["Emission Strength"].default_value=1.5
for direction in (-1,1):
    x=direction*RAIL_OFFSET
    for center in (19, 65, 111):
        mesh_asset("track_guardrail",(x,center,0),props)
        for i in range(0,11,2):
            marker_z = center+46*((i+.5)/11-.5)-.69
            cube("Recessed amber guide",(x-direction*.105,marker_z,.695),(.019,.32,.041),amber)
            bpy.ops.mesh.primitive_plane_add(size=1, location=(x-direction*.65,marker_z,-.015))
            glow = bpy.context.object
            glow.scale = (1.14,2.36,1)
            glow.data.materials.append(shoulder_glow)
    cube("Distant bridge parapet",(x,164,.39),(.082,156,.13),metal)
    cube("Edge stripe",(direction*number_setting(WORLD_SOURCE,"EdgeStripeX"),58,-.019),(.07,131,.008),stripe)
    for row in range(14):
        y=-4+9*row
        if row>2:
            h=7+2.6*((row*7+direction)%6)
            tower_x=x+direction*(4.6+(row%4)*2.4)
            profile=(row+direction)%4
            mesh_asset(f"zsr_city_{profile}_{row%2}",(tower_x,y,0),facade,
                       0 if direction<0 else math.pi,(5.9,6.7,h))
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
max_rig,max_skin=character("Max",(0,0,0),0)
max_skin.hide_render=True
if "--boss" in sys.argv:
    boss, _ = character("Boss",(.15,12,0),math.pi,2.6)
    scene.frame_set(141)
    boss_pose = [bone.matrix_basis.copy() for bone in boss.pose.bones]
    boss.animation_data_clear()
    for bone, pose in zip(boss.pose.bones, boss_pose):
        bone.matrix_basis = pose
    warn = paint("Boss strike boundaries",(1,.17,.042),emissive=True)
    for x in (-2.75,3.05):
        cube("Strike boundary",(x,5.50,.026),(.060,12.2,.011),warn)
    cube("Strike boundary near",(.15,-.6,.026),(5.8,.060,.011),warn)
else:
    character("Walker",(-.8,11.4,0),math.pi)
    character("Runner",(2.65,16.7,0),math.pi,.97)
    character("Armored",(-2.7,19.5,0),math.pi,1.14)
mesh_asset("track_wreck",(2.9,13,0),props,.13,1.14)
mesh_asset("track_spikes",(-2.9,8.5,0),props,-.06,1.13)
mesh_asset("track_barrier",(2.9,31,0),props,.04,1.18)

def alpha_plane(name, center, size, material, uv):
    x,y,z=center
    w,h=(a*.5 for a in size)
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata([(x-w,y,z-h),(x+w,y,z-h),(x+w,y,z+h),(x-w,y,z+h)],[],[(0,1,2,3)])
    layer=mesh.uv_layers.new(name="Atlas")
    for i,value in enumerate(uv):
        layer.data[i].uv=value
    plane=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(plane)
    mesh.materials.append(material)


def make_gate(x, y, slot, color):
    for side in (-1,1):
        mesh_asset("track_gate_pylon",(x+side*1.33,y,0),props)
        cube("Pylon signal",(x+side*1.27,y-.19,1.42),(.06,.04,1.89),paint("Gate signal",color,emissive=True))
    veil=paint("Gate veil",color,emissive=True)
    nodes,links=veil.node_tree.nodes,veil.node_tree.links
    output=nodes.get("Material Output")
    transparent=nodes.new("ShaderNodeBsdfTransparent")
    mixed=nodes.new("ShaderNodeMixShader")
    mixed.inputs[0].default_value=.12
    links.new(transparent.outputs[0],mixed.inputs[1])
    links.new(nodes.get("Principled BSDF").outputs[0],mixed.inputs[2])
    links.new(mixed.outputs[0],output.inputs[0])
    full_uv=((0,0),(1,0),(1,1),(0,1))
    alpha_plane("Veil",(x,y-.015,1.26),(2.38,2.28),veil,full_uv)

    plaque=paint("Gate label atlas",(1,1,1),ART/"TrackGateLabels.png",emissive=True)
    nodes,links=plaque.node_tree.nodes,plaque.node_tree.links
    tex=next(node for node in nodes if node.type=="TEX_IMAGE")
    transparent=nodes.new("ShaderNodeBsdfTransparent")
    mixed=nodes.new("ShaderNodeMixShader")
    links.new(tex.outputs["Alpha"],mixed.inputs[0])
    links.new(transparent.outputs[0],mixed.inputs[1])
    links.new(nodes.get("Principled BSDF").outputs[0],mixed.inputs[2])
    links.new(mixed.outputs[0],nodes.get("Material Output").inputs[0])
    col,row=slot%2,slot//2
    uv=tuple(((col+u)*.5,1-(row+1-v)*.25) for u,v in full_uv)
    alpha_plane("Bonus plaque",(x,y-.26,2.65),(2.42,1.20),plaque,uv)


make_gate(-2.5,22,0,(1,.35,.06))
make_gate(2.5,22,3,(.08,.9,.43))

scene.frame_set(42)
bpy.context.view_layer.update()
grip=eva.matrix_world@eva.pose.bones["WeaponGrip"].head
forward=eva.matrix_world@eva.pose.bones["WeaponForward"].head-grip
gun_yaw=math.atan2(-forward.x,forward.y)
for part in ("lux_weapon_rifle", "lux_weapon_rifle_steel"):
    weapon = mesh_asset(part,grip+Vector((0,0,.065)),metal,gun_yaw)
    weapon.scale = (.51,)*3

if "--combat" in sys.argv:
    mesh_asset("combat_drone",(-.65,1.48,2.31),props)
    for x in (-1,1):
        for y in (-1,1):
            mesh_asset("combat_rotor",(-.65+x*.45,1.48+y*.38,2.418),props,(x+y)*.42)
    beacon=paint("Drone lamps",(.1,.70,1),emissive=True)
    cube("Rear beacon",(-.65,1.131,2.327),(.19,.022,.032),beacon)
    cube("Port beacon",(-1.10,.987,2.34),(.058,.026,.038),beacon)
    grenade=mesh_asset("combat_grenade",(.91,5.6,2.40),props,.2)
    grenade.rotation_euler.x=.72
    fuse=paint("Grenade fuse",(1,.43,.1),emissive=True)
    cube("Fuse",(.91,5.55,2.49),(.045,.045,.037),fuse)
    for i in range(4):
        t=i/4
        cube("Grenade flight trace",(.87-.56*t,5.4-1.44*t,2.39-.36*t),
             (.065-.047*t,.22-.12*t,.049-.031*t),fuse)

bpy.ops.object.camera_add()
scene.camera=bpy.context.object
scene.camera.data.sensor_fit="VERTICAL"
scene.camera.data.clip_end=230
scene.render.resolution_percentage=100

def frame_camera(hero_x, width, height):
    cam=scene.camera
    cam.location=(hero_x*camera_setting("Follow"),camera_setting("Depth"),camera_setting("Height"))
    focus=Vector((hero_x*camera_setting("Lead"),camera_setting("LookDepth"),camera_setting("LookHeight")))
    cam.rotation_euler=(focus-cam.location).to_track_quat("-Z","Y").to_euler()
    widening=max(1,camera_setting("ReferenceAspect")/(width/height))
    cam.data.angle_y=2*math.atan(math.tan(math.radians(camera_setting("Fov"))/2)*widening)
    scene.render.resolution_x,scene.render.resolution_y=width,height
    bpy.context.view_layer.update()


def check_framing():
    lane_limit=number_setting(GAME_SOURCE,"RunLaneLimit")
    for name,rig,skin in (("Eva",eva,eva_skin),("Max",max_rig,max_skin)):
        for width,height in ((390,844),(1080,2400),(1080,2640)):
            for hero_x in (-lane_limit,0,lane_limit):
                rig.parent.location.x=hero_x
                frame_camera(hero_x,width,height)
                left,low,right,high=1,1,0,0
                for frame in range(1,97,4):
                    scene.frame_set(frame)
                    evaluated=skin.evaluated_get(bpy.context.evaluated_depsgraph_get())
                    for vertex in evaluated.data.vertices:
                        point=world_to_camera_view(scene,scene.camera,evaluated.matrix_world@vertex.co)
                        left,low=min(left,point.x),min(low,point.y)
                        right,high=max(right,point.x),max(high,point.y)
                assert .015<left<right<.985 and .15<low<high<.70,(width,height,hero_x,left,right,low,high)
                assert .18<high-low<.36,(high-low)
                viewing_depth=16 if hero_x==0 else 20
                edges=[world_to_camera_view(scene,scene.camera,Vector((x,viewing_depth,.72))).x
                       for x in (-RAIL_OFFSET,RAIL_OFFSET)]
                assert .01<edges[0]<edges[1]<.99,(width,height,hero_x,edges)
                print(f"FRAME CHECK {name} {width}x{height} x={hero_x:+.1f}: "
                      f"silhouette x={left:.3f}..{right:.3f} y={low:.3f}..{high:.3f}, "
                      f"rails at {viewing_depth}m={edges[0]:.3f}..{edges[1]:.3f}.",flush=True)


check_framing()
if "--check-only" in sys.argv:
    sys.exit(0)
eva.parent.location.x=0
scene.frame_set(42)
frame_camera(0,390,844)

def sun(rotation, energy, color):
    bpy.ops.object.light_add(type="SUN",rotation=tuple(map(math.radians,rotation)))
    lamp=bpy.context.object
    lamp.data.energy=energy;lamp.data.color=color;lamp.data.angle=.07

sun((36,-30,145),1.56,(1,.65,.40))
sun((55,20,12),.84,(.58,.70,1))
scene.world=bpy.data.worlds.new("Blue hour")
scene.world.use_nodes=True
background=scene.world.node_tree.nodes.get("Background")
background.inputs[0].default_value=(.20,.28,.42,1)
background.inputs[1].default_value=.60
scene.render.engine="CYCLES"
scene.cycles.samples=24
scene.cycles.use_denoising=False
scene.render.filepath=str(OUT)
bpy.ops.render.render(write_still=True)
