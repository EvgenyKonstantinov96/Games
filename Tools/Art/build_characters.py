"""Build the original Zombie Survival Run character pack with Blender 4.3.

blender -b -t 4 --python Tools/Art/build_characters.py -- /path/to/Assets/Resources/ProductionArt
"""
import math
import random
import sys
from pathlib import Path
from zipfile import ZipFile

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from gait import eased_keys, foot_target, carry_pose
from makehuman_body import Body, SIDES
from character_atlas import uv as atlas_uv

OUT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT.mkdir(parents=True, exist_ok=True)
FPS = 30
if "--makehuman-data" not in sys.argv:
    raise SystemExit("Pass --makehuman-data /path/to/makehuman/data to build the anatomical meshes.")
HUMAN_DATA = Path(sys.argv[sys.argv.index("--makehuman-data") + 1])
if "--skin-pack" not in sys.argv:
    raise SystemExit("Pass --skin-pack /path/to/makehuman_system_assets_cc0.zip.")
CLOTHING = ZipFile(sys.argv[sys.argv.index("--skin-pack") + 1])

# Name, height, shoulder width, hip width, base textile, skin, detail.
CAST = [
    ("Max", 1.94, .53, .34, 0, 9, 4),
    ("Eva", 1.81, .43, .37, 1, 10, 5),
    ("Walker", 1.89, .40, .31, 6, 11, 7),
    ("Runner", 1.86, .38, .30, 7, 12, 8),
    ("Armored", 1.98, .56, .36, 0, 11, 5),
    ("Brute", 2.10, .76, .48, 13, 13, 8),
    ("Toxic", 1.84, .47, .34, 6, 12, 21),
    ("Crawler", 1.72, .44, .31, 7, 11, 8),
    ("Boss", 2.14, .92, .50, 14, 14, 22),
]
IDLE, MOVE, FIRE, RELOAD, HIT, ATTACK, DEATH, MEDKIT, THROW = (1, 33, 59, 85, 117, 127, 147, 171, 199)


def v(x, y, z):
    return Vector((x, y, z))


class Character:
    def __init__(self, spec):
        self.name, self.h, self.shoulder, self.hips, self.cloth, self.skin, self.trim = spec
        self.hero = self.name in ("Max", "Eva")
        self.crawler = self.name == "Crawler"
        self.tank = self.name in ("Brute", "Boss")
        self.trousers = {"Walker": 2, "Runner": 8, "Toxic": 2, "Crawler": 8,"Armored":2,
                         "Brute": 20}.get(self.name, self.cloth)
        self.random = random.Random(self.name)
        self.bones = {}
        self.vertices, self.faces, self.weights, self.tiles = [], [], [], []
        self.clothing_pack = CLOTHING
        self.body = Body(self, HUMAN_DATA)
        self.rig = bpy.data.objects.new("Skeleton", bpy.data.armatures.new("Skeleton"))
        bpy.context.collection.objects.link(self.rig)

    def bone(self, name, head, tail, parent=None):
        self.bones[name] = (Vector(head), Vector(tail), parent)

    def skeleton(self):
        body = self.body
        hips = body.midpoint("upper-leg")
        waist = body.shape(hips)
        chest = body.joint("spine-2")
        neck = body.joint("neck")
        spine = body.joint("spine-4")
        self.bone("Pelvis", waist, spine)
        self.bone("Spine", spine, chest, "Pelvis")
        self.bone("Chest", chest, neck, "Spine")
        self.bone("Head", neck, body.joint("head-2"), "Chest")
        self.bone("ForwardMarker", waist+v(0,.40,0), waist+v(0,.48,0), "Pelvis")
        for side, sign in SIDES:
            key = "r-" if sign < 0 else "l-"
            hip, knee = (body.joint(key + joint) for joint in ("upper-leg", "knee"))
            ankle = body.foot_bone(side)
            ball = ankle + v(0,.145,-.056)
            self.bone("Thigh"+side, hip, knee, "Pelvis")
            self.bone("Shin"+side, knee, ankle, "Thigh"+side)
            self.bone("Foot"+side, ankle, ankle+v(0,.16,-.06), "Shin"+side)
            self.bone("Forefoot"+side, ball, ball+v(0,.07,0), "Foot"+side)
            shoulder, elbow, wrist, knuckles = (body.joint(key + joint) for joint in
                                                 ("shoulder", "elbow", "hand", "finger-3-1"))
            collar = body.joint(key+"clavicle")
            self.bone("Clavicle"+side, collar, shoulder, "Chest")
            self.bone("UpperArm"+side, shoulder, elbow, "Clavicle"+side)
            self.bone("Forearm"+side, elbow, wrist, "UpperArm"+side)
            self.bone("Hand"+side, wrist, knuckles, "Forearm"+side)
            for digit in range(1, 6):
                joints=[body.joint(key+f"finger-{digit}-{i}") for i in range(1,5)]
                parent='Hand'+side
                for i,suffix in enumerate(('', 'Middle', 'Tip')):
                    name=f'Finger{digit}{suffix}{side}'
                    self.bone(name,joints[i],joints[i+1],parent)
                    parent=name
        if self.hero:
            for side,_ in SIDES:
                knee=self.bones['Shin'+side][0]
                self.bone('KneeGuard'+side,knee,knee+v(0,0,-.12),'Shin'+side)
            pack=v(0,self.body.back_at(self.h*.735)-.10,self.h*.735)
            self.bone('Pack',pack,pack+v(0,0,.16),'Chest')
            wrist = self.bones["HandR"][0]
            self.bone("WeaponGrip", wrist, wrist+v(0,.11,0), "HandR")
            self.bone("WeaponForward", wrist+v(0,.32,0), wrist+v(0,.40,0), "HandR")
        if self.name == "Eva":
            tie = v(.015,self.body.back_at(self.h*.92)-.028,self.h*.93)
            self.bone("HairTie",tie,tie+v(.012,-.044,-.091),"Head")
            self.bone("HairTail",tie+v(.012,-.044,-.091),tie+v(.025,-.066,-.261),"HairTie")
        bpy.context.view_layer.objects.active = self.rig
        self.rig.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        for name, (head, tail, parent) in self.bones.items():
            bone = self.rig.data.edit_bones.new(name)
            bone.head, bone.tail = head, tail
            if parent:
                bone.parent = self.rig.data.edit_bones[parent]
        bpy.ops.object.mode_set(mode="OBJECT")

    def loft(self, rings, materials, sides, ripple, lateral=None):
        """Build a shared vertex ring at each section and put the UV seam on loops."""
        base = len(self.vertices)
        for j, (point, rx, ry, weights) in enumerate(rings):
            tangent = (rings[min(j + 1, len(rings) - 1)][0] - rings[max(j - 1, 0)][0]).normalized()
            if lateral is None:
                width = tangent.cross(v(0,1,0))
                if width.length_squared < .04:
                    width = tangent.cross(v(0,0,1))
            else:
                width = lateral - tangent * lateral.dot(tangent)
            width.normalize()
            depth = tangent.cross(width)
            for k in range(sides):
                u = k / sides
                turn = math.tau * u
                wrinkle = 1 + ripple * (math.sin(turn * 3 + j*.48) + .42*math.sin(turn * 7 - j*.61))
                p = point + width*(rx*math.cos(turn)*wrinkle) + depth*(ry*math.sin(turn)*wrinkle)
                self.vertices.append(tuple(p))
                self.weights.append(weights)
        for j, material in enumerate(materials):
            for k in range(sides):
                u, u_next = k/sides, (k+1)/sides
                t, t_next = j/(len(rings)-1), (j+1)/(len(rings)-1)
                row = base + j*sides
                next_k = (k+1)%sides
                self.faces.append((row+k,row+next_k,row+sides+next_k,row+sides+k))
                self.tiles.append((material, ((u,t),(u_next,t),(u_next,t_next),(u,t_next))))

    def surface(self, start, end, rings, tile, bone, sides=10, seam=None):
        """Form a tapered, optionally creased and dual-weighted surface along an axis."""
        start, end = Vector(start), Vector(end)
        sections = []
        for j, (t, rx, ry) in enumerate(rings):
            weight = .32 if seam and (j <= 1 if seam[1] else j >= len(rings)-2) else 0
            weights = {bone:1-weight, seam[0]:weight} if weight else {bone:1}
            sections.append((start.lerp(end,t),rx,ry,weights))
        self.loft(sections,[tile]*(len(sections)-1),sides,.012)

    def volume(self, p, radius, tile, bone, sides=10):
        x, y, z = radius
        self.surface(v(*p)-v(0,0,z), v(*p)+v(0,0,z),
                     [(0,.001,.001),(.15,x*.70,y*.70),(.38,x,y),(.61,x*.96,y*.97),(.84,x*.73,y*.73),(1,.001,.001)],
                     tile, bone, sides)

    def panel(self, p, size, tile, bone, slant=0):
        """Rounded corner silhouette with a rolled edge on armor, webbing and packs."""
        x,y,z = p
        w,d,h = [a*.5 for a in size]
        radius = min(w,h)*.43
        outline = []
        angular_steps = 3 if min(w,h) > .047 else 2
        for sx,sz,angle in ((1,-1,-90),(1,1,0),(-1,1,90),(-1,-1,180)):
            for i in range(angular_steps):
                turn = math.radians(angle + 90*i/(angular_steps-1))
                outline.append((sx*(w-radius)+radius*math.cos(turn),sz*(h-radius)+radius*math.sin(turn)))
        base = len(self.vertices)
        profiles = ((-d,.94),(d,.94)) if d < .013 else ((-d,.84),(-d*.73,1),(d*.73,1),(d,.84))
        for offset, scale in profiles:
            for a,b in outline:
                self.vertices.append((x+a*scale+slant*b,y+offset,z+b*scale))
                self.weights.append({bone:1.})
        sides = len(outline)
        last = sides*(len(profiles)-1)
        indices = [tuple(range(sides)),tuple(reversed(range(last,last+sides)))]
        for r in range(len(profiles)-1):
            row = r*sides
            for i in range(sides):
                following = (i+1)%sides
                indices.append((row+i,row+sides+i,row+sides+following,row+following))
        for face in indices:
            self.faces.append(tuple(base+i for i in face))
            self.tiles.append((tile, tuple((.5+(outline[i%sides][0]/w)*.48,
                                            .5+(outline[i%sides][1]/h)*.48) for i in face)))

    def sculpt(self):
        self.body.append()
        from survivor_clothing import append_garment
        append_garment(self, CLOTHING)
        self.body.append_shoes(CLOTHING)
        for side, sign in SIDES:
            if self.name == "Armored":
                knee = self.bones["Shin"+side][0]
                self.panel(knee+v(0,.064,-.025), (.098,.029,.114), 4, "Shin"+side)
                self.panel(knee+v(0,.083,.012), (.065,.012,.014), self.trim, "Shin"+side)
        from survivor_clothing import append_fitted_head_part
        append_fitted_head_part(self,CLOTHING,'eyes','low-poly',
                                36 if self.name=='Max' else 37 if self.name=='Eva' else 38,.85)
        if self.hero:
            from survivor_clothing import append_hair
            append_hair(self,CLOTHING)
            self.hero_kit()
        else:
            self.infection()
            from infected_detail import append_infected_details
            append_infected_details(self,CLOTHING)

    def hero_kit(self):
        from tactical_wardrobe import append_field_gear
        append_field_gear(self)
        for side,sign in SIDES:
            ankle=self.bones["Foot"+side][0]
            # A close fitting ankle boot, not a rigid mid-calf bucket. Blend
            # the lower collar into the same ankle bend as the shoe upper.
            rings=[]
            for height,rx,ry,shin in ((.025,.061,.066,.52),(.064,.063,.067,.82),
                                      (.115,.062,.061,1.),(.158,.061,.059,1.),
                                      (.171,.062,.060,1.),(.177,.059,.057,1.)):
                rings.append((ankle+v(0,-.006,height),rx,ry,
                              {"Shin"+side:shin,"Foot"+side:1-shin}))
            self.loft(rings,[3]*5,16,.006)
            for stitch in range(4):
                height=.069+stitch*.024
                front=.071-height*.10
                for sign in (-1,1):
                    a=ankle+v(-.024,front,height+(.017 if sign<0 else 0))
                    b=ankle+v(.024,front-.001,height+(.017 if sign>0 else 0))
                    self.surface(a,b,[(0,.0022,.0022),(1,.0022,.0022)],8,'Shin'+side,5)
    def fabric_flap(self, top, low, tile, bone):
        """A double-sided irregular fabric edge draped off the skinned cloth."""
        points = tuple(top)+tuple(low)
        first=len(self.vertices)
        self.vertices.extend(tuple(point) for point in points)
        self.vertices.extend(tuple(point+v(0,-.002,0)) for point in points)
        self.weights.extend({bone:1.0} for _ in range(8))
        for face in ((0,1,2),(0,2,3),(6,5,4),(7,6,4)):
            self.faces.append(tuple(first+i for i in face))
            uv=((.04,.94),(.96,.89),(.88,.12),(.17,.04))
            self.tiles.append((tile,tuple(uv[i%4] for i in face)))

    def infection(self):
        h,w=self.h,self.shoulder
        if self.name=="Armored":
            from infected_detail import append_fitted_helmet
            append_fitted_helmet(self)
            from infected_detail import fitted_bib
            fitted_bib(self,.195,.655,.815,4)
            for row in range(3):
                z=h*(.69+row*.032)
                for sign in (-1,1):
                    x=sign*.093
                    front=self.body.chest_surface(x,z)+.042
                    self.panel((x,front,z),(.14,.012,.015),3,'Chest')
            for s in (-1,1):
                side='L' if s<0 else 'R'
                shoulder=self.bones['UpperArm'+side][0];elbow=self.bones['Forearm'+side][0]
                # Rounded shoulder shell follows the upper arm, not a block on the torso.
                self.surface(shoulder,shoulder.lerp(elbow,.38),
                    [(0,.074,.068),(.15,.097,.090),(.72,.091,.082),(1,.079,.070)],4,'UpperArm'+side,14)
                z=h*.79;x=s*.10
                self.surface(v(x,self.body.chest_surface(x,z)+.030,z),v(x,-.10,h*.836),
                    [(0,.020,.010),(.5,.023,.011),(1,.020,.009)],3,'Chest',8)
        elif self.tank:
            for side,sign in SIDES:
                for digit in range(1,6):
                    key=f'Finger{digit}Tip{side}'
                    a,b,_=self.bones[key]
                    direction=(b-a).normalized()
                    reach=.052 if self.name=='Boss' else .028
                    self.surface(b-direction*.007,b+direction*reach+v(0,.008,-.006),
                                 [(0,.009,.007),(.28,.008,.006),(.76,.004,.003),(1,.001,.001)],
                                 19,key,8)
            if self.name == "Boss":
                for side, sign in SIDES:
                    shoulder = self.bones["UpperArm"+side][0]
                    for i in range(3):
                        start=shoulder+v(sign*(.015+.060*i),-.07+.085*i,.004)
                        rings=[]
                        for t,rx,ry in ((0,.047,.040),(.22,.044,.035),(.55,.029,.026),(.82,.014,.012),(1,.001,.001)):
                            point=start+v(sign*(.14+.020*i)*t*t,.026*t,.14*t-.030*t*t)
                            rings.append((point,rx,ry,{'Chest':1.}))
                        self.loft(rings,[14,15,19,19],10,.025)
                core_z = h*.733
                core_front = self.body.chest_surface(0,core_z)
                # Fitted branching fissures replace the floating round chest badge.
                for sign in (-1,1):
                    last=v(0,core_front+.009,core_z)
                    for i,(x,dz) in enumerate(((.036,.016),(.078,.049),(.108,.040),(.151,.078))):
                        x*=sign;z=core_z+dz
                        point=v(x,self.body.chest_surface(x,z)+.009,z)
                        self.surface(last,point,[(0,.010-i*.002,.003),(1,.007-i*.001,.002)],22,'Chest',7)
                        last=point
                    last=v(sign*.017,self.body.chest_surface(sign*.017,core_z)+.009,core_z)
                    for x,dz in ((.035,-.043),(.018,-.086),(.055,-.125)):
                        x*=sign;z=core_z+dz;point=v(x,self.body.chest_surface(x,z)+.009,z)
                        self.surface(last,point,[(0,.006,.003),(1,.004,.002)],22,'Chest',6);last=point
            else:
                from infected_detail import tank_harness
                tank_harness(self)
                region = self.bones["Chest"][0] + v(-.167,.106,.205)
                for i in range(3):
                    self.volume(region+v(-i*.021,i*.006,-i*.041),(.045,.035,.047),14,"Chest",9)
                self.fabric_flap((v(-.19,.130,h*.604),v(.01,.149,h*.605)),
                                 (v(.10,.160,h*.552),v(-.21,.158,h*.55)),20,"Chest")
        elif self.name=="Toxic":
            neck = self.bones["Head"][0]
            for i in range(3):
                start = neck + v(-.048+.019*i,-.071,.011)
                end = start + v(-.034,-.020,-.071-.018*i)
                self.surface(start,end,[(0,.010,.008),(.54,.012,.007),(1,.002,.001)],
                             21,"Chest",6)
            for i in range(8):
                ang=2.4*i
                p=(math.cos(ang)*w*.38,-.106+math.sin(ang)*.083,h*(.664+.023*i))
                r=.036+self.random.random()*.040
                self.volume(p,(r,r*.96,r*1.17),21,"Chest")
            self.volume((w*.40,.010,h*.785),(.094,.106,.073),12,"Chest",10)
            for i in range(3):
                self.volume((w*(.41+i*.08),.068+i*.026,h*(.784-i*.018)),
                            (.026,.022,.024),21,"Chest",8)
        else:
            # Open, irregular hems and contrasting undersides read as civilian
            # fabric at portrait distance, where narrow surface marks disappear.
            for i in range(3):
                left=-.17+i*.106
                y=.119+(.02 if i==1 else 0)
                self.fabric_flap((v(left,y,h*.625),v(left+.1,y,h*.639)),
                                 (v(left+.086,y+.013,h*(.574+i*.013)),
                                  v(left-.036,y+.028,h*(.535+.028*(i%2)))),self.cloth,"Chest")
            if self.name in ("Walker", "Runner"):
                for j in range(4):
                    z = h*(.65+j*.034)
                    front = self.body.chest_surface(.018,z)
                    self.panel((.018,front+.009,z),(.023,.009,.010),3,"Chest")
                    self.volume((.018,front+.017,z),(.006,.004,.006),16,"Chest",6)
            if self.name=="Walker":
                self.fabric_flap((v(-.23,.042,h*.798),v(-.134,.128,h*.807)),
                                 (v(-.18,.133,h*.687),v(-.273,.061,h*.742)),6,"Chest")
                # Rolled, mismatched sleeve and dangling work-shirt seam.
                origin = self.bones["UpperArmR"][0]
                self.surface(origin+v(.013,-.010,-.059),origin+v(.019,-.008,-.102),
                             [(0,.076,.074),(.38,.079,.074),(1,.072,.066)],9,"UpperArmR",10)
                self.fabric_flap((v(.048,.145,h*.721),v(.068,.140,h*.719)),
                                 (v(.094,.142,h*.593),v(.074,.162,h*.604)),9,"Chest")
            if self.crawler:
                for i in range(6):
                    self.volume((0,-.125,h*(.57+.043*i)),(.053,.033,.044),19,"Chest",8)
            if self.name=="Runner":
                self.surface(v(-.125,-.050,h*.841),v(.103,-.104,h*.754),
                             [(0,.026,.017),(.17,.052,.052),(.58,.058,.049),(1,.02,.018)],7,"Chest",12)
                self.fabric_flap((v(.14,.13,h*.806),v(.24,.049,h*.808)),
                                 (v(.252,.088,h*.707),v(.17,.146,h*.745)),7,"Chest")
                for x in (-.048,.043):
                    self.surface(v(x,.112,h*.813),v(x*1.72,.140,h*.695),
                                 [(0,.007,.007),(.52,.006,.007),(1,.004,.004)],18,"Chest",5)
    def skin_mesh(self):
        data=bpy.data.meshes.new(self.name+" skin")
        data.from_pydata(self.vertices,[],self.faces)
        data.update()
        skin=bpy.data.objects.new(self.name+" skinned body",data)
        bpy.context.collection.objects.link(skin)
        for polygon in data.polygons:
            polygon.use_smooth=True
        uv = data.uv_layers.new(name="Atlas")
        for p,(tile,coords) in zip(data.polygons,self.tiles):
            for loop,(x,y) in zip(p.loop_indices,coords):
                uv.data[loop].uv = atlas_uv(tile,x,y)
        groups={name:skin.vertex_groups.new(name=name) for name in self.bones}
        for i, weights in enumerate(self.weights):
            for key,weight in weights.items():
                groups[key].add([i],weight,"REPLACE")
        modifier=skin.modifiers.new("Deforming skeleton","ARMATURE")
        modifier.object=self.rig
        skin.parent=self.rig
        self.mesh=skin
        material=bpy.data.materials.get("Character atlas")
        if material is None:
            material=bpy.data.materials.new("Character atlas")
            material.use_nodes=True
            nodes=material.node_tree.nodes
            shader=nodes.get("Principled BSDF")
            shader.inputs["Roughness"].default_value=.71
            atlas=OUT/"CharacterAtlas.png"
            if atlas.exists():
                tex=nodes.new("ShaderNodeTexImage")
                tex.image=bpy.data.images.load(str(atlas),check_existing=True)
                material.node_tree.links.new(tex.outputs["Color"],shader.inputs["Base Color"])
            normal_atlas=OUT/"CharacterAtlas_normal.png"
            if normal_atlas.exists():
                normal_texture=nodes.new("ShaderNodeTexImage")
                normal_texture.image=bpy.data.images.load(str(normal_atlas),check_existing=True)
                normal_texture.image.colorspace_settings.name="Non-Color"
                normal_map=nodes.new("ShaderNodeNormalMap")
                normal_map.inputs["Strength"].default_value=.35
                material.node_tree.links.new(normal_texture.outputs["Color"],normal_map.inputs["Color"])
                material.node_tree.links.new(normal_map.outputs["Normal"],shader.inputs["Normal"])
        skin.data.materials.append(material)

    def aim_bone(self, name, origin, direction, twist=0):
        bone=self.rig.pose.bones[name]
        rest=bone.bone.matrix_local
        rotation=(rest.to_quaternion() @ v(0,1,0)).rotation_difference(direction.normalized())
        orientation = rotation @ rest.to_quaternion()
        if twist:
            orientation = Quaternion(direction.normalized(), twist) @ orientation
        target=orientation.to_matrix().to_4x4()
        target.translation=origin
        bone.matrix=target
        bpy.context.view_layer.update()

    def bend_chain(self, upper, lower, target, pole):
        origin = self.rig.pose.bones[upper].head.copy()
        a = (self.bones[upper][1]-self.bones[upper][0]).length
        b = (self.bones[lower][1]-self.bones[lower][0]).length
        offset = target-origin
        direction = offset.normalized()
        distance = min(max(offset.length,abs(a-b)+.002),a+b-.002)
        end = origin+direction*distance
        reach = (a*a+distance*distance-b*b)/(2*distance)
        outward = (pole-direction*pole.dot(direction)).normalized()
        joint = origin+direction*reach+outward*math.sqrt(max(0,a*a-reach*reach))
        self.aim_bone(upper,origin,joint-origin)
        self.aim_bone(lower,joint,end-joint)
        return end

    def leg_pose(self, side, foot, pole, toe=None, toe_pitch=0):
        foot = self.bend_chain("Thigh"+side,"Shin"+side,foot,pole)
        direction = toe if toe is not None else v(0,.16,-.06)
        self.aim_bone("Foot"+side,foot,direction)
        forefoot = self.rig.pose.bones["Forefoot"+side]
        if toe is not None:
            # Let the ball of the foot stay flat when the heel comes up.
            angle = math.radians(toe_pitch)
            self.aim_bone(forefoot.name,forefoot.head.copy(),v(0,math.cos(angle),math.sin(angle)))

    def fit_knee_guard(self,side):
        """Attach the rigid padding to the actual skinned cloth, not a guessed pivot offset."""
        indices,back=self.knee_mounts[side]
        rest=sum((Vector(self.vertices[i]) for i in indices),v(0,0,0))/len(indices)
        cloth=v(0,0,0)
        for i in indices:
            point=Vector(self.vertices[i])
            for name,weight in self.weights[i].items():
                bone=self.rig.pose.bones[name]
                cloth+=(bone.matrix@bone.bone.matrix_local.inverted()@point)*weight/len(indices)
        guard=self.rig.pose.bones['KneeGuard'+side]
        delta=guard.matrix.to_quaternion()@guard.bone.matrix_local.to_quaternion().inverted()
        target=cloth+delta@(back-rest)
        current=guard.matrix@guard.bone.matrix_local.inverted()@back
        placed=guard.matrix.copy();placed.translation+=target-current
        guard.matrix=placed
        bpy.context.view_layer.update()

    def rest_limb(self, name, heading, height, weight):
        bone = self.rig.pose.bones[name]
        direction = bone.tail - bone.head
        vertical = max(-.92, min(.92, (height - bone.head.z) / direction.length))
        target = heading.normalized() * math.sqrt(1 - vertical * vertical) + v(0, 0, vertical)
        turn = direction.normalized().rotation_difference(target)
        partial = Quaternion().slerp(turn, weight).to_matrix().to_4x4() @ bone.matrix
        partial.translation = bone.head.copy()
        bone.matrix = partial
        bpy.context.view_layer.update()

    def settle_infected(self, t):
        # The knees give first. The shoulder catches the pavement, rebounds
        # slightly, and settles with the full skinned surface touching the road.
        roll, pitch = {"Walker": (98, -23), "Runner": (-81, -47),
                       "Armored": (105, 6), "Brute": (-99, -12),
                       "Toxic": (-95, -26), "Crawler": (108, -30),
                       "Boss": (97, -18)}[self.name]
        fall = eased_keys(((0, 0), (.24, .035), (.48, .29), (.76, 1.),
                           (.87, .945), (1., .975)), t)
        pelvis = self.rig.pose.bones["Pelvis"]
        posed = pelvis.matrix.copy()
        rotation = (Matrix.Rotation(math.radians(roll) * fall, 4, "Y") @
                    Matrix.Rotation(math.radians(pitch) * fall, 4, "X"))
        turned = rotation @ posed
        side_travel = .29 if self.crawler else .18
        turned.translation = posed.translation + v(side_travel * math.copysign(fall, roll), .12 * fall, 0)
        pelvis.matrix = turned
        bpy.context.view_layer.update()
        graph = bpy.context.evaluated_depsgraph_get()
        mesh = self.mesh.evaluated_get(graph)
        if not hasattr(self, "trunk_vertices"):
            groups = self.mesh.vertex_groups
            self.trunk_vertices = [vertex.index for vertex in self.mesh.data.vertices if
                groups[max(vertex.groups, key=lambda group: group.weight).group].name
                in ("Pelvis", "Spine", "Chest", "Head")]
        floor = min(mesh.data.vertices[i].co.z for i in self.trunk_vertices)
        give = eased_keys(((0, 0), (.32, 0), (.55, .15), (.74, .69), (.92, 1), (1, 1)), t)
        if give:
            along = math.copysign(1, roll)
            for side, sign in SIDES:
                # The top arm crosses the body while the lower arm unfolds.
                # Bent knees turn out in opposite directions as the hip lands.
                across = sign * along
                self.rest_limb("UpperArm"+side, v(-along*.70, across*.81, 0), floor+.11, give)
                self.rest_limb("Forearm"+side, v(along*.42, across*.88, 0), floor+.066, give)
                self.rest_limb("Hand"+side, v(along*.27, across, 0), floor+.043, give)
                self.rest_limb("Thigh"+side, v(-along*.88, across*.61, 0), floor+.14, give)
                self.rest_limb("Shin"+side, v(-along*.94, -across*.36, 0), floor+.10, give)
                self.rest_limb("Foot"+side, v(-along*.31, -across, 0), floor+.062, give)
            mesh = self.mesh.evaluated_get(graph)
        lowest = min(vertex.co.z for vertex in mesh.data.vertices)
        if give > .8:
            contact = (give - .8) / .2
            for side, sign in SIDES:
                across = sign * along
                self.rest_limb("Forearm"+side, v(along*.42, across*.88, 0), lowest+.040, contact)
                self.rest_limb("Hand"+side, v(along*.27, across, 0), lowest+.023, contact)
                self.rest_limb("Shin"+side, v(-along*.94, -across*.36, 0), lowest+.055, contact)
                self.rest_limb("Foot"+side, v(-along*.31, -across, 0), lowest+.030, contact)
            mesh = self.mesh.evaluated_get(graph)
            lowest = min(vertex.co.z for vertex in mesh.data.vertices)
        turned.translation.z -= lowest
        pelvis.matrix = turned
        bpy.context.view_layer.update()

    def hero_arms(self, swing, care, toss, reload, fire, hit, death, phase, walking, shooting):
        chest = self.rig.pose.bones["Chest"]
        base = self.bones["Chest"][0]
        rise = chest.head.z-base.z
        offset,rotation=carry_pose(phase,walking,shooting)
        action=max(care,abs(toss),reload,hit,death)
        weight=max(0,1-min(1,action))
        rotation=Quaternion().slerp(rotation,weight);offset*=weight
        pivot=v(.075,.310,self.h*.730+rise)
        pivot+=v(.004*swing,.004*abs(swing)-fire*.008-hit*.09,death*-.29+hit*.043)
        for side, sign in SIDES:
            right = side == "R"
            active_hand = 0 if right else 1
            # The firing palm lies beside the pistol grip. The support palm is
            # UP beneath the fore-end; fingers cross the barrel, not point along it.
            wrist = pivot+v(.045 if right else -.080,-.035 if right else .215,.040 if right else .045)
            wrist += v(sign*.15*reload*active_hand,
                       -reload*.13*active_hand-.20*care*active_hand+.13*toss*active_hand,
                       -reload*.17*active_hand-.19*care*active_hand+.40*toss*active_hand)
            wrist=pivot+rotation@(wrist-pivot)+offset
            pole = v(sign*.65,-.42,-1+.56*max(0,toss)*active_hand)
            end = self.bend_chain("UpperArm"+side,"Forearm"+side,wrist,pole)
            direction=v(-.10,.30,-1) if right else v(1,.12,.08+.65*toss)
            palm=v(-1,0,.10) if right else v(-.08,0,1)
            self.orient_hand(side,end,rotation@direction,rotation@palm)
        aim = rotation@v(.03*hit,1,.025*fire+.32*hit-.49*death).normalized()
        grip = pivot+offset
        self.aim_bone("WeaponGrip",grip,aim)
        self.aim_bone("WeaponForward",grip+aim*.32,aim)

    def palm_normal(self,side):
        forward=(self.bones['Hand'+side][1]-self.bones['Hand'+side][0]).normalized()
        across=(self.bones['Finger2'+side][0]-self.bones['Finger5'+side][0]).normalized()
        normal=forward.cross(across).normalized()
        if normal.z>0:normal.negate()
        return forward,normal

    def orient_hand(self,side,origin,forward,palm):
        """Map anatomical palm axes, rather than an arbitrary bone roll, onto the grip."""
        old_f,old_n=self.palm_normal(side)
        forward=forward.normalized();palm=(palm-forward*palm.dot(forward)).normalized()
        old=Matrix((old_f.cross(old_n),old_f,old_n)).transposed()
        new=Matrix((forward.cross(palm),forward,palm)).transposed()
        hand=self.rig.pose.bones['Hand'+side]
        posed=(new@old.inverted()).to_4x4()@hand.bone.matrix_local
        posed.translation=origin
        hand.matrix=posed
        bpy.context.view_layer.update()

    def grip_fingers(self, side, curl):
        hand = self.rig.pose.bones["Hand"+side]
        wrist_rotation = hand.matrix.to_quaternion() @ hand.bone.matrix_local.to_quaternion().inverted()
        _,palm=self.palm_normal(side)
        palm=wrist_rotation@palm
        for digit in range(1,6):
            first=self.bones[f'Finger{digit}{side}']
            direction=wrist_rotation@(first[1]-first[0]).normalized()
            axis=direction.cross(palm).normalized()
            amount=0
            for suffix,degrees in (('',48),('Middle',70),('Tip',38)):
                bone = self.rig.pose.bones[f"Finger{digit}{suffix}{side}"]
                factor=.66 if digit==1 else .63 if self.hero and side=='R' and digit==2 else 1.
                amount+=math.radians(degrees*curl*factor)
                start,end,_=self.bones[bone.name]
                direction=Quaternion(axis,amount)@(wrist_rotation@(end-start))
                self.aim_bone(bone.name,bone.head.copy(),direction)

    def infected_arm(self,side,sign,phase,moving,attack,death,boss_strike):
        arm=self.rig.pose.bones['UpperArm'+side];origin=arm.head.copy()
        swing=math.cos(math.tau*(phase+.14)) if moving else -.06*sign
        if boss_strike:
            # Keep the telegraphed two-arm strike and its established contact time.
            self.aim_bone(arm.name,origin,v(sign*(.45-.20*attack),.34+.91*attack,-.79+.48*attack))
            forearm=self.rig.pose.bones['Forearm'+side]
            self.aim_bone(forearm.name,forearm.head.copy(),v(sign*.13,.39+.72*attack,-.88+.39*attack))
            return
        if self.crawler:
            support=(phase+.5)%1
            if support<.67:
                reach=.65-.18*support/.67;lift=0
            else:
                t=(support-.67)/.33;t=t*t*(3-2*t)
                reach=.47+.18*t;lift=.10*math.sin(math.pi*t)
            target=v(sign*self.shoulder*.58,reach+.17*max(0,attack),.035+lift)
            end=self.bend_chain(arm.name,'Forearm'+side,target,v(sign*.9,-.4,-.2))
            self.orient_hand(side,end,v(0,1,.04),v(0,.04,-1))
            return
        # Reach targets and elbow poles are independent. Extending an arm no
        # longer straightens its elbow into the same stiff soldier-like swing.
        if self.name=='Runner':
            offset=v(sign*.06,.12+.21*swing,-.36+.045*swing)
        elif self.name=='Toxic':
            offset=v(-sign*.025,.30+.07*swing if side=='R' else .055+.04*swing,
                     -.27 if side=='R' else -.47)
        elif self.name in ('Brute','Boss'):
            offset=v(sign*.065,.10+.11*swing,-.54)
        elif self.name=='Armored':
            offset=v(sign*.05,.16+.07*swing,-.43)
        else:
            offset=v(sign*.025,.26+.09*swing if side=='L' else .06+.10*swing,
                     -.36 if side=='L' else -.48)
        offset+=v(sign*.08*death,.38*max(0,attack),.16*max(0,attack)-.09*death)
        end=self.bend_chain(arm.name,'Forearm'+side,origin+offset,v(sign*.15,-1,-.4))
        self.orient_hand(side,end,v(-sign*.08,.14+.70*max(0,attack),-1),v(-sign,.10,0))

    def animate(self):
        # Contact, compression, passing and extension poses. Feet are solved against
        # a contact plane, with class-specific stride, crouch, and upper-body poses.
        h=self.h
        motions = [("Idle",IDLE,30),("Move",MOVE,24),("Fire",FIRE,24),("Reload",RELOAD,30),
                   ("Hit",HIT,8),("Attack",ATTACK,18),("Death",DEATH,21)]
        if self.hero:
            motions += [("Medkit",MEDKIT,24),("Throw",THROW,18),("Walk",219,34),("WalkFire",255,34)]
        for state,start,count in motions:
            for step in range(count+1):
                frame=start+step
                bpy.context.scene.frame_set(frame)
                t=step/count
                for bone in self.rig.pose.bones:
                    bone.matrix_basis=Matrix.Identity(4)
                    bone.rotation_mode="QUATERNION"
                bpy.context.view_layer.update()
                walking=state in ("Walk","WalkFire")
                moving=walking or state in ("Move","Fire","Reload","Medkit","Throw")
                f=t%1
                impact=eased_keys([(0,0),(.16,1),(.36,.46),(1,0)],t)
                death=eased_keys([(0,0),(.19,.12),(.52,.61),(.8,.97),(1,1)],t) if state=="Death" else 0
                fire=eased_keys([(0,1),(.12,.57),(.32,.12),(.45,0),(1,0)],t) if state in ("Fire","WalkFire") else 0
                rear=impact if state=="Hit" else 0
                # The boss windup is 0.49 seconds for an 18-frame attack take.
                key={"Toxic":.70,"Boss":.81,"Brute":.63}.get(self.name,.54)
                attack=eased_keys([(0,0),(.25,-.48),(key,1),(1,0)],t) if state=="Attack" else 0
                boss_strike = self.name == "Boss" and state == "Attack"
                reload=eased_keys([(0,0),(.24,1),(.67,1),(1,0)],t) if state=="Reload" else 0
                care=eased_keys([(0,0),(.24,1),(.70,1),(1,0)],t) if state=="Medkit" else 0
                swing_throw=eased_keys([(0,0),(.17,-.38),(.31,-1),(.42,.85),(.61,.64),(1,0)],t) if state=="Throw" else 0
                crouch=.55 if self.crawler else .035 if walking else .058 if moving and self.hero else .098 if moving else .025
                crouch += (1 if self.tank else .70)*.59*death
                if boss_strike: crouch += .085*max(0.,attack) - .041*min(0.,attack)
                swing = eased_keys(((0,0),(.23,.96),(.50,0),(.73,-.96),(1,0)),f) if moving else 0.
                # The pelvis yields under the new support leg and rises
                # through push-off. Counter-rotation keeps the chest balanced.
                contact=eased_keys(((0,-.003),(.17,-.018),(.59,.017),(1,-.003)),(f*2)%1) if moving else 0
                if walking: contact *= .46
                pelvis=self.bones["Pelvis"][0]+v(.025*swing,.015-.17*death,-crouch+contact)
                placements = {}
                for side,s in (("L",-1),("R",1)):
                    foot=self.bones["Foot"+side][0].copy()
                    toe_pitch=0
                    if moving:
                        if self.hero:
                            foot.x=self.bones['Thigh'+side][0].x*(.85 if walking else .76)
                        else:foot.x-=s*.027
                        foot,toe,phase,toe_pitch=foot_target(self.name,side,f,foot,walking)
                        hip,knee,_=self.bones["Thigh"+side]
                        ankle=self.bones["Shin"+side][1]
                        span=(knee-hip).length+(ankle-knee).length-.026
                        shift = pelvis-self.bones["Pelvis"][0]
                        horizontal=(foot.x-hip.x-shift.x)**2+(foot.y-hip.y-shift.y)**2
                        if span*span>horizontal:
                            pelvis.z=min(pelvis.z,foot.z+math.sqrt(span*span-horizontal))
                    else:
                        phase=0
                        toe=None
                        foot.y+=s*.045-.35*death
                        if boss_strike: foot.x += s*.096
                        if self.crawler: foot.y-=.32
                    placements[side]=(foot,toe,phase,toe_pitch)
                pelvis_roll = .068*swing if self.hero else .17*swing
                tilt=.014*math.sin(math.tau*(f*2-.12)) if self.hero and moving else 0
                pelvis_axis=v(pelvis_roll,1.8,.50) if self.crawler else v(pelvis_roll+.037*attack,.047+tilt+.47*death,.99)
                self.aim_bone("Pelvis",pelvis,pelvis_axis,.067*swing)
                pitch={"Crawler":3.0,"Runner":.31,"Walker":.24,"Brute":.18,
                       "Toxic":.28,"Boss":.13,"Armored":.155}.get(self.name,.11)
                droop = .14 if self.name in ("Walker","Toxic") else 0
                spine = self.rig.pose.bones["Spine"]
                spine_direction = (spine.tail-spine.head).normalized()
                spine_target=v(0,1.9,1) if self.crawler else spine_direction+v(droop*.33,.10*pitch+.19*death,0)
                self.aim_bone("Spine",spine.head.copy(),spine_target,.026*swing)
                chest=self.rig.pose.bones["Chest"].head.copy()
                if walking: pitch *= .54
                self.aim_bone("Chest",chest,v(droop-.067*swing+.085*rear,
                              pitch+.46*attack-.30*rear-.10*fire+.90*death,1-.71*death),-.05*swing)
                if droop and not death:
                    head=self.rig.pose.bones["Head"]
                    self.aim_bone("Head",head.head.copy(),v(-.20+.045*swing,.21+.082*swing,.99))
                elif not self.hero and not death:
                    head=self.rig.pose.bones['Head']
                    self.aim_bone('Head',head.head.copy(),v(.045*swing,.80 if self.crawler else .17,1))
                if self.hero:
                    # Shoulder girdle yields slightly under the rifle/backpack.
                    # Solve wrists after this adjustment so the weapon stays gripped.
                    shoulder_sway=eased_keys(((0,0),(.23,.96),(.50,0),(.73,-.96),(1,0)),(f+.08)%1) if moving else 0
                    for side,s in SIDES:
                        clavicle=self.rig.pose.bones['Clavicle'+side]
                        direction=clavicle.tail-clavicle.head
                        self.aim_bone(clavicle.name,clavicle.head.copy(),
                                      direction+v(0,-s*.006*shoulder_sway,s*.004*shoulder_sway))
                    self.hero_arms(swing * (.55 if walking else 1.),care,swing_throw,reload,fire,rear,death,
                                   f if moving else 0,walking,state in ('Fire','WalkFire'))
                for side,s in SIDES:
                    foot,toe,phase,toe_pitch=placements[side]
                    self.leg_pose(side,foot,v(s*.08,1,.24 if self.crawler else 0),toe,toe_pitch)
                    if self.hero:
                        thigh=self.rig.pose.bones['Thigh'+side]
                        shin=self.rig.pose.bones['Shin'+side]
                        direction=(thigh.tail-thigh.head).normalized().lerp((shin.tail-shin.head).normalized(),.74)
                        self.aim_bone('KneeGuard'+side,shin.head.copy(),direction)
                        self.fit_knee_guard(side)
                    if not self.hero:
                        self.infected_arm(side,s,phase,moving,attack,death,boss_strike)
                    curl = (1. if side=='R' else .8) if self.hero else 0. if self.crawler else .27+.45*max(0.,attack)
                    if self.hero and side == "L":
                        curl -= .65*care+.80*max(0.,swing_throw)
                    self.grip_fingers(side,curl)
                    if self.crawler:
                        # A planted palm has an abducted thumb on the ground
                        # plane; the neutral grasp's thumb points below it.
                        for suffix,z in (('',.10),('Middle',.05),('Tip',.02)):
                            thumb=self.rig.pose.bones['Finger1'+suffix+side]
                            self.aim_bone(thumb.name,thumb.head.copy(),v(-s*.85,.50,z))
                if state == "Death" and not self.hero:
                    self.settle_infected(t)
                for bone in self.rig.pose.bones:
                    if bone.name == "ForwardMarker":
                        continue
                    bone.keyframe_insert("location",frame=frame,group=bone.name)
                    bone.keyframe_insert("rotation_quaternion",frame=frame,group=bone.name)
        self.rig.animation_data.action.name=self.name+"_Motion"

    def make_lods(self):
        """Keep the detailed hero silhouette nearby and cut skinning work in distant waves."""
        self.mesh.name = self.name+"_LOD0"
        self.lod_meshes = [self.mesh]
        for index, ratio in ((1,.48),(2,.23)):
            proxy = self.mesh.copy()
            proxy.data = self.mesh.data.copy()
            proxy.name = self.name+"_LOD"+str(index)
            bpy.context.collection.objects.link(proxy)
            proxy.modifiers.clear()
            # The shared material has multiple atlas islands. Split UV seams
            # before collapse so distant meshes cannot interpolate skin pixels
            # through another garment or the unrelated opposite atlas corner.
            bm=bmesh.new()
            bm.from_mesh(proxy.data)
            uv_layer=bm.loops.layers.uv.active
            seams=[]
            for edge in bm.edges:
                if len(edge.link_faces)!=2:continue
                a,b=edge.link_faces
                first={loop.vert:loop[uv_layer].uv.copy() for loop in a.loops if loop.vert in edge.verts}
                second={loop.vert:loop[uv_layer].uv.copy() for loop in b.loops if loop.vert in edge.verts}
                if any((first[vertex]-second[vertex]).length_squared>1e-12 for vertex in edge.verts):
                    seams.append(edge)
            bmesh.ops.split_edges(bm,edges=seams)
            bm.to_mesh(proxy.data)
            bm.free()
            reduction = proxy.modifiers.new("Distance topology","DECIMATE")
            reduction.ratio = ratio
            evaluated = proxy.evaluated_get(bpy.context.evaluated_depsgraph_get())
            reduced = bpy.data.meshes.new_from_object(evaluated)
            old = proxy.data
            proxy.modifiers.clear()
            proxy.data = reduced
            bpy.data.meshes.remove(old)
            for vertex in reduced.vertices:
                entries = sorted(((g.group,g.weight) for g in vertex.groups),
                                 key=lambda item:item[1],reverse=True)
                total = sum(weight for _,weight in entries[:4])
                for group,_ in entries:
                    proxy.vertex_groups[group].remove([vertex.index])
                for group,weight in entries[:4]:
                    proxy.vertex_groups[group].add([vertex.index],weight/total,"REPLACE")
            for polygon in reduced.polygons:
                polygon.use_smooth = True
            reduced.update()
            reduced.normals_split_custom_set_from_vertices([v.normal for v in reduced.vertices])
            armature = proxy.modifiers.new("Shared skeleton","ARMATURE")
            armature.object = self.rig
            self.lod_meshes.append(proxy)

    def export(self):
        bpy.ops.object.select_all(action="DESELECT")
        self.rig.select_set(True)
        for skin in self.lod_meshes:
            skin.select_set(True)
        bpy.context.view_layer.objects.active=self.rig
        bpy.context.scene.frame_set(1)
        bpy.ops.export_scene.fbx(filepath=str(OUT/(self.name+".fbx")),use_selection=True,
            object_types={"ARMATURE","MESH"},axis_forward="-Z",axis_up="Y",add_leaf_bones=False,
            bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
            bake_anim_simplify_factor=1.0, mesh_smooth_type="FACE",path_mode="STRIP")
        print(f"BUILT {self.name}: {len(self.vertices)} vertices, {len(self.faces)} faces")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.render.fps=FPS
    scene.frame_start,scene.frame_end=1,168
    roster=[]
    requested = set(sys.argv[sys.argv.index("--only") + 1].split(",")) if "--only" in sys.argv else None
    for spec in CAST:
        if requested is not None and spec[0] not in requested:
            continue
        actor=Character(spec)
        actor.skeleton()
        actor.sculpt()
        actor.skin_mesh()
        scene.frame_end=289 if actor.hero else 168
        actor.animate()
        actor.make_lods()
        actor.export()
        roster.append(actor)
    if "--preview" in sys.argv:
        preview(scene,roster)


def preview_weapon(actor):
    grip = actor.rig.matrix_world @ actor.rig.pose.bones["WeaponGrip"].head
    barrel = actor.rig.matrix_world @ actor.rig.pose.bones["WeaponForward"].head
    steel = bpy.data.materials.get("Preview rifle")
    if steel is None:
        steel = bpy.data.materials.new("Preview rifle")
        steel.diffuse_color = (.07,.08,.09,1)
    for part in ("lux_weapon_rifle","lux_weapon_rifle_steel"):
        path = OUT.parent / ("Meshes/" + part + ".obj")
        if not path.exists():
            continue
        points,faces = [],[]
        for line in path.read_text().splitlines():
            if line.startswith("v "):
                x,y,z=map(float,line.split()[1:4])
                points.append((x,z,y))
            elif line.startswith("f "):
                faces.append([int(i.split("/")[0])-1 for i in line.split()[1:]][::-1])
        mesh = bpy.data.meshes.new(part)
        mesh.from_pydata(points,[],faces)
        gun = bpy.data.objects.new(part,mesh)
        bpy.context.collection.objects.link(gun)
        gun.location = grip + v(0,0,.065)
        direction = barrel - grip
        gun.rotation_euler.z = math.atan2(-direction.x,direction.y)
        gun.scale = (.51,)*3
        gun.data.materials.append(steel)


def preview(scene,roster):
    layouts = {
        "Max": (-1.15, 0, .16), "Eva": (1.15, 0, -.18),
        "Walker": (-4.35, 0, -.19), "Runner": (4.33, 0, .21),
        "Armored": (-4.55, -3.65, .16), "Brute": (-2.10, -3.75, .08),
        "Boss": (.24, -3.86, -.03), "Toxic": (2.58, -3.74, -.20),
        "Crawler": (4.70, -3.70, -.23),
    }
    for actor in roster:
        x,y,yaw=layouts[actor.name]
        actor.rig.location = (x,y,0)
        actor.rig.rotation_euler.z = yaw
        if actor.name in ("Brute","Boss"):
            actor.rig.scale *= 1.16
    scene.frame_set(40)
    bpy.context.view_layer.update()
    for actor in roster:
        if actor.hero:
            preview_weapon(actor)
    scene.render.engine="CYCLES"
    scene.cycles.samples=32
    scene.cycles.use_denoising=False
    scene.world=bpy.data.worlds.new("Slate")
    scene.world.color=(.16,.19,.24)
    bpy.ops.mesh.primitive_plane_add(size=200)
    bpy.context.object.location.z=-.04
    floor=bpy.data.materials.new("Charcoal concrete")
    floor.diffuse_color=(.045,.053,.063,1)
    bpy.context.object.data.materials.append(floor)
    for p, color, energy, size in (
        ((-5,4,6), (1,.62,.35), 1700,5),
        ((6,2,6), (.44,.64,1), 1400,5),
        ((0,-7,5), (.33,.57,1), 1900,7),
    ):
        bpy.ops.object.light_add(type="AREA", location=p)
        lamp=bpy.context.object
        lamp.data.color,lamp.data.energy,lamp.data.shape,lamp.data.size=color,energy,"DISK",size
        lamp.rotation_euler=(v(0,-1,1)-lamp.location).to_track_quat("-Z","Y").to_euler()
    bpy.ops.object.camera_add(location=(1.15,15,7.6))
    scene.camera=bpy.context.object
    scene.camera.rotation_euler=(v(0,-1.7,1.0)-scene.camera.location).to_track_quat("-Z","Y").to_euler()
    scene.camera.data.type="ORTHO"
    scene.camera.data.ortho_scale=11.8
    scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=1280,850,100
    scene.render.filepath=str(OUT.parents[1].parent/"zsr-character-preview.png")
    bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
