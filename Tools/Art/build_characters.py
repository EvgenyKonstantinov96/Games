"""Build the original Zombie Survival Run character pack with Blender 4.3.

blender -b -t 4 --python Tools/Art/build_characters.py -- /path/to/Assets/Resources/ProductionArt
"""
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

OUT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT.mkdir(parents=True, exist_ok=True)
FPS = 30

# Name, height, shoulder width, hip width, base textile, skin, detail.
CAST = [
    ("Max", 1.94, .53, .34, 0, 9, 4),
    ("Eva", 1.81, .43, .37, 1, 10, 5),
    ("Walker", 1.89, .40, .31, 6, 11, 7),
    ("Runner", 1.86, .38, .30, 7, 12, 8),
    ("Armored", 1.98, .56, .36, 4, 11, 5),
    ("Brute", 2.10, .76, .48, 13, 13, 8),
    ("Toxic", 1.84, .47, .34, 6, 12, 21),
    ("Crawler", 1.72, .44, .31, 7, 11, 8),
    ("Boss", 2.14, .92, .50, 14, 14, 22),
]
IDLE, MOVE, FIRE, RELOAD, HIT, ATTACK, DEATH = (1, 33, 59, 85, 117, 127, 147)


def v(x, y, z):
    return Vector((x, y, z))


class Character:
    def __init__(self, spec):
        self.name, self.h, self.shoulder, self.hips, self.cloth, self.skin, self.trim = spec
        self.hero = self.name in ("Max", "Eva")
        self.crawler = self.name == "Crawler"
        self.tank = self.name in ("Brute", "Boss")
        self.random = random.Random(self.name)
        self.bones = {}
        self.vertices, self.faces, self.weights, self.tiles = [], [], [], []
        self.rig = bpy.data.objects.new("Skeleton", bpy.data.armatures.new("Skeleton"))
        bpy.context.collection.objects.link(self.rig)

    def bone(self, name, head, tail, parent=None):
        self.bones[name] = (Vector(head), Vector(tail), parent)

    def skeleton(self):
        h = self.h
        waist = v(0, 0, h*.52)
        chest = v(0, .025, h*.69)
        neck = v(0, .04, h*.86)
        self.bone("Pelvis", waist, chest)
        self.bone("Chest", chest, neck, "Pelvis")
        self.bone("Head", neck, v(0, .06, h), "Chest")
        self.bone("ForwardMarker", v(0, .40, h*.52), v(0, .48, h*.52), "Pelvis")
        for side, s in (("L", -1), ("R", 1)):
            hip = v(s*self.hips*.29, 0, h*.52)
            knee = v(s*self.hips*.32, .024, h*.285)
            ankle = v(s*self.hips*.34, -.018, .095)
            foot = ankle + v(0, .16, -.06)
            self.bone("Thigh"+side, hip, knee, "Pelvis")
            self.bone("Shin"+side, knee, ankle, "Thigh"+side)
            self.bone("Foot"+side, ankle, foot, "Shin"+side)
            shoulder = v(s*self.shoulder*.49, .03, h*.817)
            if self.hero:
                elbow = v(s*self.shoulder*.65, -.025 if s == 1 else .06, h*.665)
                hand = v(.105 if s == 1 else .125, .235 if s == 1 else .47, h*.735)
                fingers = hand + v(0,.080,0)
            else:
                elbow = v(s*self.shoulder*.64, .075, h*.63)
                hand = v(s*self.shoulder*.69, .19, h*.49)
                fingers = hand + v(s*.015,.035,-.075)
            self.bone("UpperArm"+side, shoulder, elbow, "Chest")
            self.bone("Forearm"+side, elbow, hand, "UpperArm"+side)
            self.bone("Hand"+side, hand, fingers, "Forearm"+side)
        if self.hero:
            grip = self.bones["HandR"][0] + v(0,.045,.048)
            self.bone("WeaponGrip", grip, grip+v(0,.11,0), "HandR")
            self.bone("WeaponForward", grip+v(0,.32,0), grip+v(0,.40,0), "HandR")
        bpy.context.view_layer.objects.active = self.rig
        self.rig.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        for name, (head, tail, parent) in self.bones.items():
            bone = self.rig.data.edit_bones.new(name)
            bone.head, bone.tail = head, tail
            if parent:
                bone.parent = self.rig.data.edit_bones[parent]
        bpy.ops.object.mode_set(mode="OBJECT")

    def surface(self, start, end, rings, tile, bone, sides=10, seam=None):
        """Form a tapered, optionally creased and dual-weighted surface along an axis."""
        start, end = Vector(start), Vector(end)
        axis = (end-start).normalized()
        width = axis.cross(v(0,1,0))
        if width.length < .2:
            width = axis.cross(v(0,0,1))
        width.normalize()
        depth = axis.cross(width).normalized()
        base = len(self.vertices)
        for j, (t, rx, ry) in enumerate(rings):
            for k in range(sides+1):
                turn = math.tau * (k%sides)/sides
                wrinkle = 1 + .042*math.sin(k%sides*2.8+j*7.1)
                p = start.lerp(end,t) + width*(rx*math.cos(turn)*wrinkle) + depth*(ry*math.sin(turn)*wrinkle)
                self.vertices.append(tuple(p))
                mix = .32 if seam and (j <= 1 if seam[1] else j >= len(rings)-2) else 0
                self.weights.append({bone:1-mix, seam[0]:mix} if mix else {bone:1})
        for j in range(len(rings)-1):
            for k in range(sides):
                a = base+j*(sides+1)+k
                self.faces.append((a,a+1,a+sides+2,a+sides+1))
                self.tiles.append((tile, ((k/sides,j/(len(rings)-1)), ((k+1)/sides,j/(len(rings)-1)),
                    ((k+1)/sides,(j+1)/(len(rings)-1)), (k/sides,(j+1)/(len(rings)-1)))))

    def volume(self, p, radius, tile, bone, sides=10):
        x, y, z = radius
        self.surface(v(*p)-v(0,0,z), v(*p)+v(0,0,z),
                     [(0,.001,.001),(.15,x*.70,y*.70),(.38,x,y),(.61,x*.96,y*.97),(.84,x*.73,y*.73),(1,.001,.001)],
                     tile, bone, sides)

    def panel(self, p, size, tile, bone, slant=0):
        """Chamfered equipment panel with its own face UVs."""
        x,y,z = p
        w,d,h = [a*.5 for a in size]
        edge = min(w,h)*.23
        outline = [(-w+edge,-h),(w-edge,-h),(w,-h+edge),(w,h-edge),
                   (w-edge,h),(-w+edge,h),(-w,h-edge),(-w,-h+edge)]
        base=len(self.vertices)
        for offset, scale in ((-d,.85),(-d*.66,1),(d*.66,1),(d,.85)):
            for a,b in outline:
                self.vertices.append((x+a*scale+slant*b, y+offset, z+b*scale))
                self.weights.append({bone:1.})
        indices = [tuple(reversed(range(8))), tuple(range(24,32))]
        indices += [(r*8+i,r*8+(i+1)%8,(r+1)*8+(i+1)%8,(r+1)*8+i)
                    for r in range(3) for i in range(8)]
        for face in indices:
            self.faces.append(tuple(base+i for i in reversed(face)))
            self.tiles.append((tile, tuple((.5+(outline[i%8][0]/w)*.49,
                                            .5+(outline[i%8][1]/h)*.49) for i in reversed(face))))

    def limbs(self):
        for side,s in (("L",-1),("R",1)):
            for name, widths, material, seam in (
                ("Thigh", (.104,.099,.071), 2 if self.hero else self.cloth, ("Shin"+side, False)),
                ("Shin", (.072,.076,.048), 2 if self.hero else self.cloth, ("Thigh"+side, True)),
                ("UpperArm", (.083,.077,.053), self.cloth if self.hero or self.name=="Armored" else self.skin, ("Forearm"+side, False)),
                ("Forearm", (.062,.068,.039), self.skin, ("UpperArm"+side, True)),
                ("Hand", (.044,.049,.032), 3 if self.hero else self.skin, None),
            ):
                bone = name+side
                a,b,_=self.bones[bone]
                m = 1.22 if self.tank else .88 if self.name == "Eva" else 1
                if self.tank and name in ("UpperArm","Forearm","Hand"):
                    m *= 1.36
                u,w,e = [r*m for r in widths]
                self.surface(a,b,[(-.12,u*.38,u*.45),(-.025,u,u*.84),(.20,u*1.02,u*.85),
                    (.40,w,w*.81),(.56,w*1.05,w*.78),(.72,(w+e)*.51,(w+e)*.40),
                    (.91,e,e*.88),(1.09,e*.79,e*.70),(1.14,.001,.001)],material,bone,10,seam)
            knee = self.bones["Shin"+side][0]
            foot = self.bones["Foot"+side][0]
            self.volume(foot+v(0,.058,-.042), (.070,.135,.050), 3, "Foot"+side)
            self.panel(foot+v(0,.046,-.076), (.139,.25,.028), 8, "Foot"+side)
            if self.hero or self.name=="Armored":
                self.panel(knee+v(0,.071,0), (.133,.041,.147), 4, "Shin"+side)
                self.panel(knee+v(0,.094,.006), (.096,.018,.089), 5, "Shin"+side)
                self.panel(knee+v(0,.107,.006), (.080,.007,.064), 4, "Shin"+side)
                self.panel(foot+v(0,.046,-.013),(.101,.119,.062),3,"Foot"+side)
                for lace in range(3):
                    self.panel(foot+v(0,.058+lace*.021,.024-lace*.010),
                               (.081,.011,.008),8,"Foot"+side)
                thigh=self.bones["Thigh"+side][0]
                self.panel(thigh+v(s*.065,-.015,-.16),(.120,.18,.19),0,"Thigh"+side,.10*s)
                wrist=self.bones["Hand"+side][0]
                self.volume(wrist+v(0,.027,.009),(.047,.063,.035),3,"Hand"+side)
            if not self.hero:
                hand=self.bones["Hand"+side][1]
                for finger in range(3):
                    self.volume(hand+v((finger-1)*.022,.012,-.018),(.011,.023,.035),self.skin,"Hand"+side,6)

    def sculpt(self):
        h,w,hip=self.h,self.shoulder,self.hips
        self.surface(v(0,0,h*.43),v(0,0,h*.66),
                     [(0,hip*.30,.078),(.14,hip*.48,.122),(.43,hip*.58,.133),(.68,hip*.54,.127),
                      (.84,hip*.45,.105),(1,hip*.44,.112)],2 if self.hero else self.cloth,"Pelvis",14,("Chest",False))
        self.surface(v(0,.0,h*.59),v(0,.03,h*.86),
                     [(0,w*.28,.095),(.16,w*.31,.115),(.39,w*.42,.147),(.60,w*.48,.156),
                      (.77,w*.51,.144),(.90,w*.46,.109),(1,w*.23,.069)],self.cloth,"Chest",16,("Pelvis",True))
        self.volume((0,.039,h*.866),(.056,.058,.072),self.skin,"Head")
        head_z=h*.939
        face_w=.087 if self.name=="Eva" else .098 if not self.tank else .118
        self.surface(v(0,.061,h*.867),v(0,.032,h*1.014),
                     [(0,.027,.027),(.12,face_w*.72,.061),(.28,face_w*.81,.075),(.41,face_w,.089),
                      (.55,face_w*.95,.094),(.69,face_w,.091),(.84,face_w*.79,.082),
                      (.95,face_w*.53,.052),(1,.001,.001)],self.skin,"Head",16)
        self.volume((0,.123,head_z-.018),(.023,.035,.031),self.skin,"Head",8)
        self.panel((0,.156,head_z-.052),(.058,.014,.013),15,"Head")
        for side in (-1,1):
            self.volume((side*face_w*.90,.036,head_z-.017),(.021,.027,.036),self.skin,"Head",8)
            self.panel((side*.038,.150,head_z+.004),(.035,.013,.013),16 if self.hero else self.trim,"Head",side*.10)
            self.panel((side*.038,.147,head_z+.019),(.045,.014,.009),17,"Head",side*.15)
        if self.hero:
            self.volume((0,.012,h*.985),(face_w*1.08,.105,.063),17,"Head",14)
            self.volume((0,-.035,h*.939),(face_w*.97,.070,.084),17,"Head")
            for i in range(5):
                x=(i-2)*.032
                self.surface(v(x,.094,h*1.001),v(x*.47-.025,.116,h*(.952+.008*i)),
                    [(0,.022,.012),(.34,.022,.010),(.74,.013,.009),(1,.001,.001)],17,"Head",6)
            for s in (-1,1):
                self.volume((s*.045,.158,head_z+.003),(.008,.005,.007),3,"Head",6)
            if self.name=="Eva":
                self.surface(v(0,-.065,h*.964),v(.04,-.14,h*.774),
                             [(0,.038,.032),(.20,.045,.038),(.48,.039,.03),(.68,.03,.026),
                              (.85,.022,.019),(1,.002,.002)],17,"Head")
                self.panel((-.133,.126,h*.535),(.088,.051,.104),1,"Pelvis")
                self.panel((-.133,.154,h*.535),(.052,.005,.014),16,"Pelvis")
                self.panel((-.133,.155,h*.535),(.014,.005,.052),16,"Pelvis")
            self.hero_kit()
        else:
            self.infection()
        self.limbs()

    def hero_kit(self):
        h,w=self.h,self.shoulder
        self.panel((0,.122,h*.719),(w*.60,.094,.288),4,"Chest")
        self.panel((0,-.131,h*.717),(w*.68,.181,.281),0,"Chest")
        self.panel((0,-.227,h*.695),(w*.58,.034,.194),2,"Chest")
        self.panel((0,-.249,h*.696),(w*.48,.012,.036),self.trim,"Chest")
        for s in (-1,1):
            self.panel((s*w*.25,-.230,h*.724),(.031,.021,.256),3,"Chest",s*.14)
            self.panel((s*w*.225,-.242,h*.715),(.028,.016,.027),4,"Chest",s*.14)
            self.panel((s*w*.316,-.193,h*.672),(.107,.068,.116),8 if self.name=="Eva" else 0,"Chest")
            self.panel((s*w*.316,-.232,h*.710),(.091,.017,.031),4,"Chest")
            for i in range(3):
                self.panel((s*w*.22,-.252,h*(.655+i*.020)),(.106,.006,.010),3,"Chest")
            self.panel((s*w*.26,.156,h*.704),(.069,.055,.155),0,"Chest",s*.05)
            self.volume((s*w*.458,.023,h*.806),(.081,.103,.064),self.cloth,"Chest",10)
            self.panel((s*w*.44,.022,h*.815),(.092,.181,.069),4,"Chest",s*.61)
            self.panel((s*w*.43,-.070,h*.806),(.066,.012,.025),5,"Chest",s*.22)
        self.panel((0,0,h*.533),(self.hips*1.20,.252,.079),3,"Pelvis")
        self.panel((0,.136,h*.536),(.066,.027,.045),5,"Pelvis")

    def infection(self):
        h,w=self.h,self.shoulder
        if self.name=="Armored":
            self.volume((0,.018,h*.977),(.118,.125,.092),4,"Head",14)
            self.panel((0,.127,h*.957),(.178,.028,.054),3,"Head")
            self.panel((0,.146,h*.956),(.125,.012,.017),5,"Head")
            self.panel((0,.145,h*.73),(w*.75,.103,.30),4,"Chest")
            for s in (-1,1):
                self.panel((s*w*.48,.029,h*.804),(.216,.248,.145),4,"Chest",s*.38)
                self.panel((s*w*.50,.155,h*.803),(.125,.016,.044),5,"Chest",s*.38)
        elif self.tank:
            for s in (-1,1):
                for rib in range(4):
                    self.volume((s*w*(.14+.031*rib),.142,h*(.63+.028*rib)),
                        (.071,.044,.025),self.skin,"Chest",8)
                self.volume((s*w*.43,.005,h*.798),(w*.25,.166,.156),self.skin,"Chest",14)
                self.panel((s*w*.26,.16,h*.746),(.091,.065,.39),4,"Chest",s*.48)
                for i in range(3):
                    a=v(s*w*(.36+i*.073),-.151,h*(.75+i*.031))
                    b=a+v(s*(.10+i*.03),-.063,.13+i*.02)
                    self.surface(a,b,[(0,.058,.053),(.28,.045,.039),(.62,.026,.024),(1,.001,.001)],19,"Chest")
            if self.name=="Boss":
                for s in (-1,1):
                    self.volume((s*.059,.136,h*.966),(.057,.029,.034),14,"Head")
                    self.panel((s*.047,.165,h*.944),(.049,.021,.016),22,"Head")
                self.volume((0,.154,h*.906),(.071,.037,.055),15,"Head",12)
                for x in (-.052,-.033,-.014,.014,.033,.052):
                    self.panel((x,.187,h*.925),(.013,.019,.021),19,"Head")
                self.panel((0,.182,h*.711),(.246,.091,.245),4,"Chest")
                self.volume((0,.238,h*.716),(.077,.035,.076),22,"Chest",12)
                self.panel((0,.232,h*.607),(.180,.052,.082),22,"Chest")
        elif self.name=="Toxic":
            for i in range(10):
                ang=2.4*i
                p=(math.cos(ang)*w*.40,-.105+math.sin(ang)*.097,h*(.64+.021*i))
                r=.035+self.random.random()*.050
                self.volume(p,(r,r*.93,r*1.22),21,"Chest")
        else:
            for i in range(5):
                self.panel((.115-i*.047,.155,h*(.68+i*.024)),(.069,.020,.023),15,"Chest",.42)
            if self.name=="Walker":
                self.panel((-.081,.164,h*.724),(.103,.027,.164),6,"Chest",-.28)
                self.panel((.063,.142,h*.655),(.085,.020,.123),11,"Chest",.33)
                self.panel((.089,.123,h*.908),(.026,.057,.046),15,"Head",.4)
            if self.crawler:
                for i in range(6):
                    self.volume((0,-.125,h*(.57+.043*i)),(.053,.033,.044),19,"Chest",8)
            if self.name=="Runner":
                self.panel((0,.154,h*.773),(.31,.025,.063),8,"Chest")
                self.panel((0,.139,h*.566),(.13,.038,.11),7,"Pelvis")
        if self.name not in ("Armored","Boss"):
            self.panel((0,.155,h*.903),(.083,.019,.034),15,"Head")
            for i in range(4):
                self.volume((-.033+i*.020,.170,h*.907),(.009,.009,.012),19,"Head",6)

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
                uv.data[loop].uv=((tile%8+.035+x*.93)/8, (3-tile//8+.035+y*.93)/4)
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
        skin.data.materials.append(material)

    def aim_bone(self, name, origin, direction):
        bone=self.rig.pose.bones[name]
        rest=bone.bone.matrix_local
        rotation=(rest.to_quaternion() @ v(0,1,0)).rotation_difference(direction.normalized())
        target=(rotation @ rest.to_quaternion()).to_matrix().to_4x4()
        target.translation=origin
        bone.matrix=target
        bpy.context.view_layer.update()

    def leg_pose(self, side, foot, pole):
        thigh,shin="Thigh"+side,"Shin"+side
        hip=self.rig.pose.bones[thigh].head.copy()
        a=(self.bones[thigh][1]-self.bones[thigh][0]).length
        b=(self.bones[shin][1]-self.bones[shin][0]).length
        axis=(foot-hip).normalized()
        distance=min((foot-hip).length,a+b-.0008)
        foot=hip+axis*distance
        along=(a*a+distance*distance-b*b)/(2*distance)
        bend=(pole-axis*pole.dot(axis)).normalized()
        knee=hip+axis*along+bend*math.sqrt(max(.0,a*a-along*along))
        self.aim_bone(thigh,hip,knee-hip)
        self.aim_bone(shin,knee,foot-knee)
        self.aim_bone("Foot"+side,foot,v(0,.16,-.06))

    def animate(self):
        # Contact, compression, passing and extension poses. Feet are solved against
        # a contact plane, with class-specific stride, crouch, and upper-body poses.
        h=self.h
        stride={"Max":.53,"Eva":.48,"Walker":.30,"Runner":.60,"Armored":.31,
                "Brute":.36,"Toxic":.31,"Crawler":.25,"Boss":.35}[self.name]
        locomotion=[(.00,.00,0),(.20,-.055,0),(.48,-.026,0),(.56,.015,.030),
                    (.67,.036,.145),(.83,.019,.200),(1,.00,0)]
        footfall=[(.0,1.0),(.20,.34),(.48,-1.0),(.56,-.98),(.67,-.74),(.83,.18),(1,1.0)]
        def interp(table,t,col):
            for a,b in zip(table,table[1:]):
                if t<=b[0]:
                    return a[col]+(b[col]-a[col])*(t-a[0])/(b[0]-a[0])
            return table[-1][col]

        for state,start,count in (("Idle",IDLE,30),("Move",MOVE,24),("Fire",FIRE,24),
                                  ("Reload",RELOAD,30),("Hit",HIT,8),("Attack",ATTACK,18),("Death",DEATH,21)):
            for step in range(count+1):
                frame=start+step
                bpy.context.scene.frame_set(frame)
                t=step/count
                for bone in self.rig.pose.bones:
                    bone.matrix_basis=Matrix.Identity(4)
                    bone.rotation_mode="QUATERNION"
                bpy.context.view_layer.update()
                moving=state in ("Move","Fire")
                f=t%1
                impact=interp([(0,0),(.16,1),(.36,.46),(1,0)],t,1)
                death=interp([(0,0),(.19,.12),(.52,.61),(.8,.97),(1,1)],t,1) if state=="Death" else 0
                fire=interp([(0,1),(.12,.57),(.32,.12),(.45,0),(1,0)],t,1) if state=="Fire" else 0
                rear=impact if state=="Hit" else 0
                key={"Toxic":.42,"Boss":.68,"Brute":.63}.get(self.name,.54)
                attack=interp([(0,0),(.25,-.48),(key,1),(1,0)],t,1) if state=="Attack" else 0
                reload=interp([(0,0),(.24,1),(.67,1),(1,0)],t,1) if state=="Reload" else 0
                crouch=.45 if self.crawler else .072 if moving else .044
                crouch += (1 if self.tank else .70)*.59*death
                contact=interp(locomotion,(f*2)%1,1) if moving else 0
                pelvis=self.bones["Pelvis"][0]+v(0,-.17*death,-crouch+contact)
                self.aim_bone("Pelvis",pelvis,v(.037*attack,.52*death,.99))
                chest=self.rig.pose.bones["Chest"].head.copy()
                pitch=.79 if self.crawler else .22 if self.name=="Runner" else .065
                self.aim_bone("Chest",chest,v(.085*rear,pitch+.46*attack-.30*rear-.10*fire+.90*death,1-.71*death))
                for side,s in (("L",-1),("R",1)):
                    phase=(f+(0 if s==-1 else .5))%1
                    foot=self.bones["Foot"+side][0].copy()
                    foot.y+=interp(footfall,phase,1)*stride*.53 if moving else s*.045
                    foot.z+=interp(locomotion,phase,2) if moving else 0
                    foot.y-=.35*death
                    if self.crawler:
                        foot.y-=.32
                    self.leg_pose(side,foot,v(0,1,0))
                    if self.hero:
                        arm=self.rig.pose.bones["UpperArm"+side]
                        if reload:
                            origin=arm.head.copy()
                            self.aim_bone(arm.name,origin,v(-s*.22,.32,-.40-.23*reload))
                        elif fire or rear:
                            origin=arm.head.copy()
                            arm_dir=arm.tail-origin
                            self.aim_bone(arm.name,origin,arm_dir+v(0,-fire*.04,.09*fire-.05*rear))
                    else:
                        arm=self.rig.pose.bones["UpperArm"+side]
                        origin=arm.head.copy()
                        swing=interp(footfall,(phase+.5)%1,1) if moving else -.06*s
                        if self.crawler:
                            hand=v(s*self.shoulder*.72,.34+stride*swing,.14+.17*max(0.,swing))
                            self.aim_bone(arm.name,origin,(hand-origin)+v(s*.12,0,-.14))
                            elbow=self.rig.pose.bones["Forearm"+side].head.copy()
                            self.aim_bone("Forearm"+side,elbow,hand-elbow)
                        else:
                            reach={"Boss":1.1,"Brute":.93,"Toxic":.28}.get(self.name,.78)
                            self.aim_bone(arm.name,origin,
                                v(s*(.16+.36*death+.25*attack),.20+.52*swing+reach*attack,-.81+.45*attack))
                for bone in self.rig.pose.bones:
                    if bone.name in ("ForwardMarker","WeaponGrip","WeaponForward"):
                        continue
                    bone.keyframe_insert("location",frame=frame,group=bone.name)
                    bone.keyframe_insert("rotation_quaternion",frame=frame,group=bone.name)
        self.rig.animation_data.action.name=self.name+"_Motion"

    def export(self):
        bpy.ops.object.select_all(action="DESELECT")
        self.rig.select_set(True)
        self.mesh.select_set(True)
        bpy.context.view_layer.objects.active=self.rig
        bpy.context.scene.frame_set(1)
        bpy.ops.export_scene.fbx(filepath=str(OUT/(self.name+".fbx")),use_selection=True,
            object_types={"ARMATURE","MESH"},axis_forward="-Z",axis_up="Y",add_leaf_bones=False,
            bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
            bake_anim_simplify_factor=1.0, mesh_smooth_type="OFF",path_mode="STRIP")
        print(f"BUILT {self.name}: {len(self.vertices)} vertices, {len(self.faces)} faces")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    scene.render.fps=FPS
    scene.frame_start,scene.frame_end=1,168
    roster=[]
    for spec in CAST:
        actor=Character(spec)
        actor.skeleton()
        actor.sculpt()
        actor.skin_mesh()
        actor.animate()
        actor.export()
        roster.append(actor)
    if "--preview" in sys.argv:
        preview(scene,roster)


def preview(scene,roster):
    for i, actor in enumerate(roster):
        actor.rig.location = ((i%5-2)*2.2, 0 if i<5 else -3.6,0)
        if actor.name in ("Brute","Boss"):
            actor.rig.scale *= 1.16
    scene.frame_set(40)
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
    bpy.ops.object.camera_add(location=(.1,12,10.5))
    scene.camera=bpy.context.object
    scene.camera.rotation_euler=(v(0,-1.7,1.0)-scene.camera.location).to_track_quat("-Z","Y").to_euler()
    scene.camera.data.type="ORTHO"
    scene.camera.data.ortho_scale=12.3
    scene.render.resolution_x,scene.render.resolution_y,scene.render.resolution_percentage=1280,850,100
    scene.render.filepath=str(OUT.parents[1].parent/"zsr-character-preview.png")
    bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    main()
