"""Build the original Zombie Survival Run character pack with Blender 4.3.

blender -b -t 4 --python Tools/Art/build_characters.py -- /path/to/Assets/Resources/ProductionArt
"""
import math
import random
import sys
from pathlib import Path
from zipfile import ZipFile

import bpy
from mathutils import Matrix, Vector
sys.path.insert(0, str(Path(__file__).resolve().parent))
from makehuman_faces import append_face
from survivor_clothing import append_pants

OUT = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
OUT.mkdir(parents=True, exist_ok=True)
FPS = 30
if "--makehuman-data" not in sys.argv:
    raise SystemExit("Pass --makehuman-data /path/to/makehuman/data to build the anatomically shaped heads.")
FACE_DATA = Path(sys.argv[sys.argv.index("--makehuman-data") + 1])
if "--skin-pack" not in sys.argv:
    raise SystemExit("Pass --skin-pack /path/to/makehuman_system_assets_cc0.zip.")
CLOTHING = ZipFile(sys.argv[sys.argv.index("--skin-pack") + 1])

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
IDLE, MOVE, FIRE, RELOAD, HIT, ATTACK, DEATH, MEDKIT, THROW = (1, 33, 59, 85, 117, 127, 147, 171, 199)


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

    def limb(self, side, leg):
        if leg:
            names = ("Thigh", "Shin", "Foot")
            widths = (.108,.071,.048)
            first_material = self.cloth if self.name != "Boss" else 14
            second_material = first_material if self.name not in ("Crawler","Toxic") else self.skin
        else:
            names = ("UpperArm", "Forearm", "Hand")
            widths = (.080,.056,.037)
            first_material = self.cloth if self.hero or self.name in ("Armored","Walker","Runner") else self.skin
            second_material = 3 if self.name=="Armored" else self.skin
            if self.name == "Walker" and side == "L":
                first_material = self.skin
        upper, lower, tip = (name+side for name in names)
        a,b,_ = self.bones[upper]
        c = self.bones[lower][1]
        multiplier = 1.22 if self.tank else .88 if self.name=="Eva" else 1
        if self.tank and not leg:
            multiplier *= 1.32
        peak,joint,end = (r*multiplier for r in widths)
        start = .004 if leg else .025
        shape = (
            (a.lerp(b,-.16), start, start, 0, 0),
            (a.lerp(b,-.05), peak*.84, peak*.83, 0, 0),
            (a.lerp(b,.17), peak, peak*.90, 0, 0),
            (a.lerp(b,.43), peak*.89, peak*.79, 0, 0),
            (a.lerp(b,.72), (peak+joint)*.49, joint*.97, 0, 0),
            (a.lerp(b,.92), joint*.98, joint*.91, .26, 0),
            (b, joint*.96, joint*.94, .50, 0),
            (b.lerp(c,.12), joint*.99, joint*.92, .76, 0),
            (b.lerp(c,.31), joint*1.02, joint*.87, 1, 0),
            (b.lerp(c,.63), (joint+end)*.49, (joint+end)*.40, 1, 0),
            (b.lerp(c,.87), end*1.08, end*.89, 1, 0),
            (c, end, end*.85, 1, .28),
            (b.lerp(c,1.06), end*.76, end*.64, 1, .54),
        )
        rings = []
        for point,rx,ry,mix,tip_mix in shape:
            weights = {upper:1-mix,lower:mix-tip_mix,tip:tip_mix}
            rings.append((point,rx,ry,{key:weight for key,weight in weights.items() if weight > 0}))
        stripe = 6 if self.name == "Toxic" and leg else second_material
        mats = [first_material]*6+[stripe]+[second_material]*5
        self.loft(rings,mats,10 if self.hero else 11,.016,v(1,0,0))

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

    def limbs(self):
        for side,s in (("L",-1),("R",1)):
            self.limb(side, leg=False)
            if not self.hero:
                self.limb(side, leg=True)
            knee = self.bones["Shin"+side][0]
            foot = self.bones["Foot"+side][0]
            # Keep a sculpted heel and a flatter, tapered toe under the same
            # skin/triangle budget as the old oval boot.
            self.surface(foot+v(0,-.079,-.045),foot+v(0,.200,-.045),
                         [(0,.031,.018),(.13,.054,.045),(.33,.067,.052),
                          (.56,.069,.044),(.84,.061,.033),(1,.027,.016)],3,"Foot"+side,10)
            self.panel(foot+v(0,.050,-.074), (.137,.242,.022), 8, "Foot"+side)
            hand = self.bones["Hand"+side][0]
            fingers = self.bones["Hand"+side][1]
            mitt = 3 if self.hero or self.name=="Armored" else self.skin
            self.surface(hand,fingers,[(0,.033,.031),(.25,.041,.033),(.56,.044,.032),
                         (.82,.035,.024),(1,.005,.007)],mitt,"Hand"+side,10)
            if self.hero or self.name=="Armored":
                self.panel(knee+v(0,.067,0), (.139,.044,.154), 3, "Shin"+side)
                self.panel(knee+v(0,.092,.013), (.111,.015,.108), 4, "Shin"+side)
                self.panel(knee+v(0,.101,.034), (.072,.008,.012), self.trim, "Shin"+side)
                self.panel(foot+v(0,.053,-.008),(.097,.125,.055),3,"Foot"+side)
                self.panel(foot+v(0,.066,.021),(.078,.046,.009),8,"Foot"+side)
                thigh=self.bones["Thigh"+side][0]
                self.panel(thigh+v(s*.069,-.010,-.16),(.103,.135,.156),0,"Thigh"+side,.10*s)
                self.panel(hand+v(0,.016,.026),(.063,.028,.020),4,"Hand"+side)
            else:
                for finger in range(3):
                    p = fingers + v((finger-1)*.021,.008,-.014)
                    self.surface(p,p+v((finger-1)*.006,.012,-.044),
                                 [(0,.012,.010),(.42,.011,.009),(.78,.008,.007),(1,.001,.001)],
                                 self.skin,"Hand"+side,6)

    def sculpt(self):
        h,w,hip=self.h,self.shoulder,self.hips
        if self.hero:
            append_pants(self, CLOTHING)
        else:
            self.surface(v(0,0,h*.43),v(0,0,h*.66),
                         [(0,hip*.30,.078),(.14,hip*.48,.122),(.43,hip*.58,.133),(.68,hip*.54,.127),
                          (.84,hip*.45,.105),(1,hip*.44,.112)],self.cloth,"Pelvis",14,("Chest",False))
        waist = .34 if self.hero else .26 if self.name in ("Runner","Crawler") else .30
        back = .143 if self.hero else .126 if self.name=="Runner" else .167
        self.surface(v(0,0,h*.57),v(0,.03,h*.86),
                     [(0,w*waist,.096),(.13,w*(waist+.036),.108),(.32,w*.405,back*.96),
                      (.53,w*.467,back),(.69,w*.50,back*.98),(.83,w*.49,back*.87),
                      (.93,w*.389,back*.71),(1,w*.22,.068)],self.cloth,"Chest",18,("Pelvis",True))
        self.volume((0,.039,h*.866),(.056,.058,.072),self.skin,"Head")
        head_z=h*.944
        append_face(self, FACE_DATA)
        eyeY = .121
        for side in (-1,1):
            x=side*(.034 if self.name=="Eva" else .038)
            self.volume((x,eyeY,head_z),(.020,.015,.014),16,"Head",8)
            self.volume((x,eyeY+.013,head_z),(.008,.003,.008),3 if self.hero else self.trim,"Head",6)
        if self.hero:
            for i in range(5):
                x=(i-2)*.032
                self.surface(v(x,.094,h*1.001),v(x*.47-.025,.116,h*(.952+.008*i)),
                    [(0,.022,.012),(.34,.022,.010),(.74,.013,.009),(1,.001,.001)],17,"Head",6)
            for s in (-1,1):
                self.panel((s*.035,.132,head_z+.025),(.032,.007,.006),17,"Head",.12*s)
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
        self.panel((0,.126,h*.719),(w*.61,.082,.287),4,"Chest")
        pack_center = v(0,-.151,h*.711)
        self.panel(pack_center,(w*.69,.154,.277),0,"Chest")
        self.panel(pack_center+v(0,-.078,-.022),(w*.54,.027,.186),2,"Chest")
        self.panel(pack_center+v(0,-.096,.036),(w*.43,.012,.020),self.trim,"Chest")
        self.panel(pack_center+v(0,-.098,.018),(w*.31,.005,.008),8,"Chest")
        self.panel(pack_center+v(0,-.036,-.142),(w*.57,.071,.034),3,"Chest")
        if self.name == "Max":
            self.panel(pack_center+v(-w*.30,-.015,.054),(.075,.058,.131),3,"Chest")
            mast = pack_center+v(-w*.34,-.015,.113)
            self.surface(mast,mast+v(-.016,-.006,.110),
                         [(0,.009,.009),(.16,.008,.008),(.88,.005,.005),(1,.002,.002)],
                         4,"Chest",6)
        else:
            self.panel(pack_center+v(-w*.17,-.096,-.044),(.070,.008,.054),1,"Chest")
            self.panel(pack_center+v(-w*.17,-.101,-.044),(.032,.004,.009),16,"Chest")
            self.panel(pack_center+v(-w*.17,-.101,-.044),(.009,.004,.032),16,"Chest")
        for s in (-1,1):
            self.panel((s*w*.25,-.234,h*.720),(.030,.016,.238),3,"Chest",s*.14)
            self.panel((s*w*.228,-.245,h*.685),(.025,.012,.023),4,"Chest",s*.14)
            self.panel((s*w*.318,-.198,h*.671),(.089,.061,.107),8 if self.name=="Eva" else 0,"Chest")
            self.panel((s*w*.318,-.232,h*.702),(.080,.014,.028),4,"Chest")
            self.panel((s*w*.22,-.251,h*.664),(.087,.006,.012),3,"Chest")
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
            self.panel((0,.143,h*.745),(w*.72,.092,.267),4,"Chest")
            self.panel((0,.184,h*.699),(w*.63,.038,.095),3,"Chest")
            self.panel((0,.202,h*.681),(w*.50,.018,.060),4,"Chest")
            for s in (-1,1):
                self.panel((s*w*.48,.031,h*.807),(.185,.228,.134),4,"Chest",s*.28)
                self.panel((s*w*.51,.135,h*.775),(.141,.034,.065),3,"Chest",s*.28)
                self.panel((s*w*.50,.156,h*.818),(.083,.012,.026),5,"Chest",s*.24)
        elif self.tank:
            for s in (-1,1):
                for rib in range(3):
                    self.volume((s*w*(.166+.031*rib),.151,h*(.641+.032*rib)),
                        (.066,.029,.027),self.skin,"Chest",8)
                self.volume((s*w*.425,.002,h*.793),(w*.243,.153,.150),self.skin,"Chest",14)
                if self.name=="Boss":
                    self.panel((s*w*.263,.156,h*.745),(.093,.064,.350),4,"Chest",s*.54)
                    for i in range(3):
                        a=v(s*w*(.335+i*.073),-.151,h*(.744+i*.034))
                        b=a+v(s*(.094+i*.026),-.072,.057+i*.067)
                        self.surface(a,b,[(0,.052,.047),(.21,.045,.040),(.46,.028,.027),
                                     (.74,.016,.014),(1,.001,.001)],19,"Chest",9)
                else:
                    self.panel((s*w*.241,.148,h*.721),(.100,.051,.313),8,"Chest",s*.52)
            if self.name=="Brute":
                self.panel((-.323,.017,h*.794),(.236,.261,.168),4,"Chest",-.29)
                self.panel((-.366,.142,h*.779),(.145,.033,.073),5,"Chest",-.24)
                self.panel((.101,-.131,h*.702),(.225,.090,.168),0,"Chest",.08)
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
                self.panel((0,.152,h*.768),(.298,.023,.044),8,"Chest",.18)
                self.panel((.081,.159,h*.726),(.086,.018,.039),16,"Chest",.18)
                self.panel((.035,.131,h*.566),(.136,.028,.081),7,"Pelvis")
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
                if tile < 24:
                    uv.data[loop].uv=((tile%8+.035+x*.93)/8, (7-tile//8+.035+y*.93)/8)
                elif tile < 32:
                    slot=tile-24
                    uv.data[loop].uv=((slot%4+.013+x*.974)/4, (1-slot//4+.013+y*.974)/4)
                else:
                    uv.data[loop].uv=((tile-32+.013+x*.974)/4, (.035+y*.93)/8+.50)
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

        motions = [("Idle",IDLE,30),("Move",MOVE,24),("Fire",FIRE,24),("Reload",RELOAD,30),
                   ("Hit",HIT,8),("Attack",ATTACK,18),("Death",DEATH,21)]
        if self.hero:
            motions += [("Medkit",MEDKIT,24),("Throw",THROW,18)]
        for state,start,count in motions:
            for step in range(count+1):
                frame=start+step
                bpy.context.scene.frame_set(frame)
                t=step/count
                for bone in self.rig.pose.bones:
                    bone.matrix_basis=Matrix.Identity(4)
                    bone.rotation_mode="QUATERNION"
                bpy.context.view_layer.update()
                moving=state in ("Move","Fire","Medkit","Throw")
                f=t%1
                impact=interp([(0,0),(.16,1),(.36,.46),(1,0)],t,1)
                death=interp([(0,0),(.19,.12),(.52,.61),(.8,.97),(1,1)],t,1) if state=="Death" else 0
                fire=interp([(0,1),(.12,.57),(.32,.12),(.45,0),(1,0)],t,1) if state=="Fire" else 0
                rear=impact if state=="Hit" else 0
                key={"Toxic":.42,"Boss":.68,"Brute":.63}.get(self.name,.54)
                attack=interp([(0,0),(.25,-.48),(key,1),(1,0)],t,1) if state=="Attack" else 0
                reload=interp([(0,0),(.24,1),(.67,1),(1,0)],t,1) if state=="Reload" else 0
                care=interp([(0,0),(.24,1),(.70,1),(1,0)],t,1) if state=="Medkit" else 0
                swing_throw=interp([(0,0),(.17,-.38),(.31,-1),(.42,.85),(.61,.64),(1,0)],t,1) if state=="Throw" else 0
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
                        if side=="L" and (care or swing_throw):
                            origin=arm.head.copy()
                            direction=(arm.tail-origin).normalized()
                            direction += v(-.58*care-.34*abs(swing_throw),.35*swing_throw-.36*care,-.56*swing_throw)
                            self.aim_bone(arm.name,origin,direction)
                            wrist=self.rig.pose.bones["ForearmL"]
                            elbow=wrist.head.copy()
                            wrist_dir=(wrist.tail-elbow).normalized()
                            wrist_dir += v(-.49*care-.24*abs(swing_throw),.12*care+.62*swing_throw,.26*care-.85*swing_throw)
                            self.aim_bone(wrist.name,elbow,wrist_dir)
                        elif reload:
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
            bake_anim_simplify_factor=1.0, mesh_smooth_type="FACE",path_mode="STRIP")
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
        scene.frame_end=217 if actor.hero else 168
        actor.animate()
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
