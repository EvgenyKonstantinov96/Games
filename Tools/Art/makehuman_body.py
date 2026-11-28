"""Continuous, clothed character meshes from the MakeHuman CC0 base and joints."""

from collections import defaultdict
from functools import lru_cache
import json
import math

import bpy
from mathutils import Vector

STYLE = {
    "Max": ("caucasian-male-young", "maxmuscle-averageweight", .40, 0),
    "Eva": ("caucasian-female-young", "maxmuscle-averageweight", .26, 1),
    "Walker": ("caucasian-male-old", "minmuscle-minweight", .38, 2),
    "Runner": ("asian-male-young", "averagemuscle-minweight", .32, 3),
    "Armored": ("african-male-young", "maxmuscle-averageweight", .50, 4),
    "Brute": ("african-male-old", "maxmuscle-maxweight", .82, 4),
    "Toxic": ("asian-male-old", "averagemuscle-minweight", .30, 5),
    "Crawler": ("caucasian-female-old", "minmuscle-minweight", .35, 6),
    "Boss": ("caucasian-male-old", "maxmuscle-maxweight", 1.10, 7),
}
SHOE_ASSETS = ("shoes02", "shoes05")
SIDES = (("L", -1), ("R", 1))


def smooth(a, b, value):
    t = min(1., max(0., (value-a)/(b-a)))
    return t*t*(3-2*t)


def read_obj(lines, body_only=False):
    points, uv, faces = [], [], []
    joints = defaultdict(set)
    group = "body"
    for line in lines:
        if line.startswith("v "):
            points.append(Vector(tuple(map(float, line.split()[1:4]))))
        elif line.startswith("vt "):
            uv.append(tuple(map(float, line.split()[1:3])))
        elif line.startswith("g "):
            group = line[2:]
        elif line.startswith("f "):
            face = tuple(tuple(int(i)-1 for i in word.split("/")[:2]) for word in line.split()[1:])
            if group.startswith("joint-"):
                joints[group[6:]].update(i for i, _ in face)
            elif group == "body" or not body_only:
                # The OBJ's Y-up coordinates become Z-up in Blender.
                faces.append(tuple(reversed(face)))
    return points, uv, faces, joints


@lru_cache(maxsize=1)
def read_base(data):
    return read_obj((data / "3dobjs/base.obj").read_text().splitlines(), body_only=True)


def apply_target(points, target, strength):
    for line in target.read_text().splitlines():
        if line.strip() and not line.startswith("#"):
            i, *delta = line.split()
            points[int(i)] += Vector(tuple(map(float, delta))) * strength


def deform_bone(name):
    stem, _, suffix = name.partition(".")
    side = "R" if suffix == "L" else "L"
    for prefix, bone in (("upperleg", "Thigh"), ("lowerleg", "Shin"), ("foot", "Foot"),
                         ("toe", "Forefoot"), ("upperarm", "UpperArm"), ("lowerarm", "Forearm"),
                         ("clavicle", "Clavicle"), ("shoulder", "Clavicle"),
                         ("wrist", "Hand"), ("metacarpal", "Hand")):
        if stem.startswith(prefix):
            return bone+side
    if stem.startswith("finger"):
        segment=int(stem.split('-')[1])
        return "Finger"+stem[6]+('' if segment==1 else 'Middle' if segment==2 else 'Tip')+side
    if stem in ("root", "pelvis"):
        return "Pelvis"
    if stem in ("spine05", "spine04", "spine03"):
        return "Spine"
    if stem in ("spine02", "spine01", "breast", "neck01"):
        return "Chest"
    return "Head"


@lru_cache(maxsize=1)
def read_weights(data):
    path = data / "rigs/default_weights.mhw"
    if not path.is_file():
        raise FileNotFoundError(f"Missing CC0 MakeHuman vertex weights: {path}")
    source = json.loads(path.read_text())["weights"]
    weights = defaultdict(lambda:defaultdict(float))
    for source_bone, entries in source.items():
        key = deform_bone(source_bone)
        for index, weight in entries:
            weights[index][key] += weight
    return weights


def append_reduced(character, name, points, faces, tiles, weights, ratio):
    """Reduce the mesh in bind pose with normalized skin weights and face-corner UVs."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], [f for f, _ in faces])
    source = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(source)
    groups = {key:source.vertex_groups.new(name=key) for key in character.bones}
    for i, entries in enumerate(weights):
        for key, weight in entries.items():
            if weight > .00001:
                groups[key].add([i], weight, "REPLACE")
    slots = sorted(set(tiles))
    # Separate indices preserve atlas regions while decimating. The final FBX
    # combines them into one material and one skinned renderer.
    for slot in slots:
        material = bpy.data.materials.get(f"Atlas slot {slot}")
        mesh.materials.append(material or bpy.data.materials.new(f"Atlas slot {slot}"))
    layer = mesh.uv_layers.new(name="Atlas")
    for polygon, (_, coords), tile in zip(mesh.polygons, faces, tiles):
        polygon.material_index = slots.index(tile)
        for loop, uv in zip(polygon.loop_indices, coords):
            layer.data[loop].uv = uv
    simplifier = source.modifiers.new("Mobile character topology", "DECIMATE")
    simplifier.ratio = ratio
    evaluated = source.evaluated_get(bpy.context.evaluated_depsgraph_get())
    reduced = evaluated.to_mesh()
    base = len(character.vertices)
    names = [group.name for group in source.vertex_groups]
    for vertex in reduced.vertices:
        entries = [(names[group.group], group.weight) for group in vertex.groups]
        entries = [entry for entry in sorted(entries, key=lambda entry: entry[1], reverse=True)[:4]
                   if entry[1] > .00001]
        total = sum(amount for _, amount in entries)
        assert total > 0, (character.name, name, vertex.index)
        character.vertices.append(tuple(vertex.co))
        character.weights.append({key:amount/total for key, amount in entries})
    layer = reduced.uv_layers.active
    first_face = len(character.faces)
    for polygon in reduced.polygons:
        character.faces.append(tuple(base+i for i in polygon.vertices))
        character.tiles.append((slots[polygon.material_index],
                                tuple(tuple(layer.data[i].uv) for i in polygon.loop_indices)))
    evaluated.to_mesh_clear()
    bpy.data.objects.remove(source, do_unlink=True)
    bpy.data.meshes.remove(mesh)
    return base, first_face


class Body:
    def __init__(self, character, data):
        self.actor = character
        name, self.height = character.name, character.h
        morph, build, amount, skin_slot = STYLE[name]
        self.skin_slot = 24 + skin_slot
        source, self.uv, self.faces, groups = read_base(data)
        self.source = [p.copy() for p in source]
        macro = data / "targets/macrodetails"
        apply_target(self.source, macro / (morph + ".target"), 1)
        if name=='Max':
            # Mature the brother's facial/body landmarks without a separate,
            # unfitted head. Proxy garments and joints use the same blended base.
            apply_target(self.source,macro/'caucasian-male-young.target',-.14)
            apply_target(self.source,macro/'caucasian-male-old.target',.14)
        body_type = morph.split("-", 1)[1]
        apply_target(self.source, macro / f"universal-{body_type}-{build}.target", amount)
        if not character.hero and name != "Armored":
            apply_target(self.source, data / "targets/expression/units/caucasian/mouth-open.target",
                         {"Boss":.93,"Toxic":.79,"Walker":.65,"Runner":.59,
                          "Crawler":.68,"Brute":.66}.get(name,.35))
        self.bind_weights = read_weights(data)
        self.used = sorted({i for face in self.faces for i, _ in face})
        self.marks = {key: sum((self.source[i] for i in sorted(group)), Vector((0, 0, 0))) / len(group)
                      for key, group in groups.items()}
        self.floor = min(self.source[i].y for i in self.used)
        self.top = max(self.source[i].y for i in self.used)
        ankle = self.midpoint("ankle")
        neck = self.marks["neck"]
        self.horizontal = self.height/(self.top-self.floor)
        self.vertical = ((self.floor, .010), (ankle.y, .095), (neck.y, self.height*.86),
                         (self.top, self.height))
        self.depth_origin = neck.z
        self.geometry = {i:self.shape(self.source[i]) for i in self.used}
        if character.tank:
            # Add radial forearm mass and enlarged hands around anatomical axes.
            # The skeleton and joint anchors retain their existing positions.
            for index,p in self.geometry.items():
                weights=self.bind_weights[index]
                side='L' if p.x<0 else 'R'
                key='r-' if side=='L' else 'l-'
                elbow,wrist=self.joint(key+'elbow'),self.joint(key+'hand')
                axis=wrist-elbow
                projection=elbow+axis*max(0,min(1,(p-elbow).dot(axis)/axis.length_squared))
                p+=(p-projection)*weights.get('Forearm'+side,0.)*(.38 if character.name=='Boss' else .24)
                hand=weights.get('Hand'+side,0.)+sum(value for bone,value in weights.items() if bone.startswith('Finger') and bone.endswith(side))
                p+=(p-wrist)*hand*(.48 if character.name=='Boss' else .24)

    def midpoint(self, name):
        return (self.marks["l-"+name]+self.marks["r-"+name])*.5

    def shape(self, source):
        x, y, depth = source
        height = self.height
        lower, upper = self.vertical[:2]
        for a, b in zip(self.vertical, self.vertical[1:]):
            lower, upper = a, b
            if y <= b[0]:
                break
        z = lower[1]+(y-lower[0])*(upper[1]-lower[1])/(upper[0]-lower[0])
        width = 1.0
        if self.actor.tank:
            width += (.35 if self.actor.name == "Brute" else .57)*smooth(.40,.83,z/height)*(1-smooth(.84,.93,z/height))
        if self.actor.name == "Eva":
            width -= .055*smooth(.63,.80,z/height)*(1-smooth(.84,.93,z/height))
        depth_scale=1.
        if self.actor.tank:
            depth_scale+=(.14 if self.actor.name=='Brute' else .22)*smooth(.50,.75,z/height)*(1-smooth(.86,.93,z/height))
        return Vector((x*self.horizontal*width, (depth-self.depth_origin)*self.horizontal*depth_scale+.04, z))

    def back_at(self, height):
        return min(p.y for p in self.geometry.values() if abs(p.x) < .075 and abs(p.z-height) < .025)

    def chest_surface(self, width, height):
        return max(p.y for p in self.geometry.values() if abs(p.x-width) < .046 and abs(p.z-height) < .037)

    def joint(self, key):
        point=self.shape(self.marks[key])
        if self.actor.tank and 'finger-' in key:
            wrist=self.shape(self.marks[key[:2]+'hand'])
            point=wrist+(point-wrist)*(1.48 if self.actor.name=='Boss' else 1.24)
        return point

    def foot_bone(self, side):
        ankle = self.joint(("r-" if side == "L" else "l-")+"ankle")
        # Keep the authored gait pivot at the ball of the new sole.
        return Vector((ankle.x, -.018, .095))

    def normalized_foot(self, p, side):
        key = "r" if side == "L" else "l"
        ankle = self.joint(key+"-ankle")
        result = p.copy()
        result.y += (self.foot_bone(side).y-ankle.y)*(1-smooth(.15,.36,p.z))
        relative = result.y+.018
        result.y = -.018 + relative*(.83 if relative > 0 else 1.)
        return result

    def bare(self, side):
        return self.actor.crawler or self.actor.name=='Boss' or (side == "R" and self.actor.name in ("Runner", "Toxic", "Brute"))

    def append(self):
        actor = self.actor
        hip_z = actor.bones["Pelvis"][0].z
        ref = {}
        points, weights, regions = [], [], []
        for i in self.used:
            p = self.geometry[i].copy()
            side = "L" if p.x < 0 else "R"
            if p.z < .38:
                p = self.normalized_foot(p, side)
            ref[i] = len(points)
            w = self.bind_weights[i]
            region = self.clothing(p, side, w)
            if region == "trousers":
                # Smooth the front of the clothed pelvis and add slight cloth slack.
                seam = (1-smooth(.02,.10,abs(p.x)))*math.exp(-((p.z-(hip_z-.105))/.095)**2)
                if p.y > .035:
                    p.y += (.073-p.y)*seam*.82
                p.y += .007 if p.y > .04 else -.007
            elif region == "shirt":
                p.y += .008 if p.y > .035 else -.008
            points.append(p)
            weights.append(w)
            regions.append(region)

        from survivor_clothing import hidden_body
        hidden = hidden_body(actor, actor.clothing_pack)
        atlas_faces, tiles = [], []
        for face in self.faces:
            if hidden and any(index in hidden for index, _ in face):
                continue
            ids = tuple(ref[i] for i, _ in face)
            p = sum((points[i] for i in ids), Vector((0,0,0)))/len(ids)
            side = "L" if p.x < 0 else "R"
            if p.z < .145 and not self.bare(side):
                continue
            choices = [regions[i] for i in ids]
            region = max(choices, key=choices.count)
            if region == "trousers":
                tile = actor.trousers if not actor.hero else 2
            elif region == "shirt":
                tile = 16 if actor.name=="Eva" else actor.cloth
            elif region == "glove":
                tile = 3
            else:
                tile = self.skin_slot
            from survivor_clothing import torn_opening
            if not actor.hero and torn_opening(actor,p,.70):
                tile=15
            if not actor.hero and p.z>actor.h*.88 and p.y>.060:
                # Surface-bound cuts on cheeks and jaw, never floating plates.
                mouth=actor.body.joint('mouth')
                line=.061+(p.z-mouth.z-.027)*.22
                if mouth.z+.004<p.z<mouth.z+.061 and abs(abs(p.x)-line)<.006:
                    tile=15
            if tile == self.skin_slot:
                coords = tuple(self.uv[j] for _, j in face)
            else:
                coords = tuple(self.textile_uv(points[i], region) for i in ids)
            atlas_faces.append((ids, coords))
            tiles.append(tile)

        used = sorted({i for face, _ in atlas_faces for i in face})
        indices = {i:k for k,i in enumerate(used)}
        atlas_faces = [(tuple(indices[i] for i in face), coords) for face, coords in atlas_faces]
        base, first_face = append_reduced(actor, actor.name+" anatomical mesh", [points[i] for i in used],
                                          atlas_faces, tiles, [weights[i] for i in used], .53 if actor.hero else .40)
        if actor.hero:
            self.hair(base, first_face)

    @staticmethod
    def textile_uv(p, region):
        # Continuous mapping across fabric, with enough repetition for the weave.
        if region == "trousers":
            span, height = .47, 1.46
        elif region == "glove":
            span, height = .68, 1.54
        else:
            span, height = .78, 1.90
        return (min(.97, max(.03, .5 + (p.x + (.10 if p.y < 0 else -.06))*1.1/span)),
                min(.97, max(.03, p.z/height)))

    def clothing(self, p, side, weights):
        actor = self.actor
        from survivor_clothing import torn_opening
        if torn_opening(actor,p,1.5):return "skin"
        x, y, z = p
        height = self.height
        shoulder, elbow = (actor.bones[name+side][0] for name in ("UpperArm", "Forearm"))
        digits = sum(value for bone,value in weights.items() if bone.startswith('Finger') and bone.endswith(side))
        hand = weights.get("Hand"+side,0.)+digits
        upper = weights.get("UpperArm"+side,0.)
        lower = weights.get("Forearm"+side,0.)
        if weights.get("Head",0.) > .5:
            return "skin"
        if actor.tank:
            arm=sum(value for bone,value in weights.items() if bone.startswith(('UpperArm','Forearm','Hand','Finger')))
            if arm>.30 or z>height*.55:return 'skin'
            return 'trousers' if z>height*.39 else 'skin'
        if hand > .48:
            return "glove" if actor.name in ("Max","Eva","Armored") and digits < .72 else "skin"
        if upper+lower+hand > .45:
            sleeve = {"Max":.64, "Eva":1.06, "Walker":.48 if side=="R" else .13,
                      "Runner":.75 if side=="L" else .14,"Armored":1.08,
                      "Brute":.04, "Toxic":.39 if side=="R" else .16,
                      "Crawler":.49 if side=="R" else .27, "Boss":.04}[actor.name]
            upper_arm = elbow-shoulder
            limb_position = (p-shoulder).dot(upper_arm)/upper_arm.length_squared
            hem = 0 if actor.hero else .035*math.sin(y*53+z*38)
            if limb_position < sleeve+hem:
                return "shirt"
            return "glove" if actor.name == "Armored" and lower > .60 else "skin"
        if z < height*(.55 if actor.hero else .562):
            if not self.bare(side):
                return "trousers"
            cut = .38 if actor.name == "Crawler" else .46 if actor.name in ("Runner","Toxic") else .23
            return "trousers" if z > cut+.025*math.sin(x*63+y*47) else "skin"
        neck = actor.bones["Head"][0].z
        collar = neck-(.018 if actor.hero else .048)-max(0.,y-.088)*.38
        bare_torso = actor.tank and z > height*.582
        return "shirt" if z < collar and not bare_torso else "skin"

    def hair(self, base, first_face):
        actor = self.actor
        height = self.height
        scalp = {}
        end = len(actor.faces)
        def hairline(point):
            x,y,_=point
            front=smooth(-.030,.108,y)
            temple=smooth(.042,.088,abs(x))
            # Coverage belongs beneath the native groom, above the forehead.
            # A low, symmetric edge reads as blunt bangs even when every hair
            # card is correctly textured. Preserve the scout's high side part.
            ear_clearance=.010*smooth(.086,.116,abs(x))*(1-.3*front)
            if actor.name=='Eva':
                part=.004*math.exp(-((x-.028)/.022)**2)*front
                sweep=.004*smooth(0,.075,-x)*front
                return height*(.923+.051*front-.013*front*temple+part-sweep+ear_clearance)
            return height*(.923+.046*front-.008*front*temple+ear_clearance)
        for face in actor.faces[first_face:end]:
            if not all(i>=base for i in face):
                continue
            center=sum((Vector(actor.vertices[i]) for i in face),Vector((0,0,0)))/len(face)
            if center.z<hairline(center) or center.z<height*.92:
                continue
            for i in face:
                if i not in scalp:
                    p = Vector(actor.vertices[i])
                    p.z=max(p.z,hairline(p))
                    center = Vector((0,.035,height*.946))
                    scalp[i] = len(actor.vertices)
                    actor.vertices.append(tuple(p+(p-center).normalized()*.0055))
                    actor.weights.append({"Head":1.})
            actor.faces.append(tuple(scalp[i] for i in face))
            if actor.name=='Eva':
                # Sample one uninterrupted native chestnut strand island;
                # the scalp stays continuous without sharing Max's dark tile.
                coords=tuple((.24+actor.vertices[i][0]*.9,
                              .68+(actor.vertices[i][2]-height*.97)*1.4) for i in face)
                actor.tiles.append((35,coords))
            else:
                actor.tiles.append((17,tuple((.5+actor.vertices[i][0]*4.,
                                             .5+(actor.vertices[i][2]-height*.97)*5.) for i in face)))

    def append_shoes(self, pack):
        actor = self.actor
        shoe_slot = 1 if actor.name in ("Eva", "Runner", "Crawler", "Toxic", "Walker") else 0
        name = SHOE_ASSETS[shoe_slot]
        obj = pack.read(f"clothes/{name}/{name}.obj").decode().splitlines()
        points, uv, faces, _ = read_obj(obj)
        for side, sign in SIDES:
            if self.bare(side):
                continue
            ankle = self.foot_bone(side)
            used = [i for i,p in enumerate(points) if p.x*sign>0]
            index = {i:k for k,i in enumerate(used)}
            sample = [points[i] for i in used]
            bottom, top = min(p.y for p in sample), max(p.y for p in sample)
            west, east = min(p.x*sign for p in sample), max(p.x*sign for p in sample)
            heel, tip = min(p.z for p in sample), max(p.z for p in sample)
            width = .144 if actor.name in ("Eva", "Walker", "Runner", "Toxic") else .161
            positions = [Vector((ankle.x+sign*((p.x*sign-west)/(east-west)-.5)*width,
                                 ankle.y-.078+(p.z-heel)/(tip-heel)*.284,
                                 .009+(p.y-bottom)/(top-bottom)*.221)) for p in sample]
            geom = [(tuple(index[i] for i,_ in face), tuple(uv[j] for _,j in face))
                    for face in faces if all(i in index for i,_ in face)]
            weights = []
            for p in positions:
                toe = smooth(ankle.y+.114,ankle.y+.185,p.y)
                shin = smooth(.129,.217,p.z)*(1-toe)
                weights.append({"Foot"+side:1-toe-shin,"Forefoot"+side:toe,"Shin"+side:shin})
            append_reduced(actor, actor.name+" footwear "+side, positions, geom,
                           [32+shoe_slot]*len(geom), weights, .23)
