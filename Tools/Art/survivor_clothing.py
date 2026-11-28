"""Fit the CC0 MakeHuman garments to the game's morphed bodies and existing rig."""
from collections import defaultdict
import re
import math


# Hero skin and clothing have dedicated 768 squares; enemies use 256/512.
WARDROBE = {
    "Max": ("male_casualsuit01", 40),
    "Eva": ("male_casualsuit05", 41),
    "Walker": ("male_casualsuit03", 42),
    "Runner": ("male_casualsuit02", 43),
    "Toxic": ("male_casualsuit05", 44),
    "Crawler": ("female_casualsuit01", 45),
}
OUTFITS={**WARDROBE,'Brute':('male_casualsuit03',42),'Boss':('male_casualsuit03',42)}
TORN = {'Walker':((-.095,.747,.047,.069),),
        'Runner':((.145,.772,.043,.058),),
        'Toxic':((-.075,.754,.055,.080),),
        'Crawler':((.075,.724,.046,.073),)}


def torn_opening(actor,point,margin=1.):
    if point.y < .055:return False
    for x,z,rx,rz in TORN.get(actor.name,()):
        u,v=(point.x-x)/rx,(point.z-z*actor.h)/rz
        jagged=1+.12*math.sin(math.atan2(v,u)*7)
        if u*u+v*v < (margin*jagged)**2:return True
    return False


def scout_opening(actor,point,margin=1.):
    if actor.name!='Eva' or point.y<.060:return False
    t=point.z/actor.h
    if not .60<t<.823:return False
    width=(.026+.029*math.sin(math.pi*(t-.60)/.223))*margin
    return abs(point.x)<width


def exposed_arm(actor, point, weights, overlap=0.):
    if not actor.hero:
        return False
    side = "L" if point.x < 0 else "R"
    arm_weight=sum(weights.get(part+side,0.) for part in
                   ("UpperArm","Forearm","Hand"))
    shoulder=actor.bones["UpperArm"+side][0]
    elbow=actor.bones["Forearm"+side][0]
    axis=elbow-shoulder
    reach=(point-shoulder).dot(axis)/axis.length_squared
    return arm_weight>.52 and reach > ( .70 if actor.name=="Max" else 1.12 )-overlap


def read_proxy(pack, name, family="clothes"):
    from mathutils import Vector
    text = pack.read(f"{family}/{name}/{name}.mhclo").decode()
    lines = text.splitlines()
    mapping = []
    active = False
    scales = {}
    hidden = set()
    for line in lines:
        words = line.split()
        if not words or line.startswith("#"):
            continue
        if words[0] in ("x_scale", "y_scale", "z_scale"):
            scales["xyz".index(words[0][0])] = (int(words[1]), int(words[2]), float(words[3]))
        elif words[0] == "verts":
            active = True
        elif words[0] == "delete_verts":
            active = False
        elif active and len(words) == 9:
            mapping.append(([int(x) for x in words[:3]], [float(x) for x in words[3:6]],
                            Vector(tuple(float(x) for x in words[6:9]))))
    if "delete_verts" in text:
        for match in re.finditer(r"(\d+)\s*-\s*(\d+)|(\d+)", text.split("delete_verts", 1)[1]):
            if match[3]:
                hidden.add(int(match[3]))
            else:
                hidden.update(range(int(match[1]), int(match[2]) + 1))
    return mapping, scales, hidden


def hidden_body(actor, pack):
    if actor.name not in OUTFITS:
        return set()
    hidden = read_proxy(pack, OUTFITS[actor.name][0])[2]
    if actor.tank:
        hidden={i for i in hidden if i in actor.body.geometry and actor.h*.39<actor.body.geometry[i].z<actor.h*.54}
    for index in tuple(hidden):
        point=actor.body.geometry.get(index)
        if point is not None and (torn_opening(actor,point,1.45) or scout_opening(actor,point,1.65)):
            hidden.remove(index)
    if actor.hero:
        for index in tuple(hidden):
            point=actor.body.geometry.get(index)
            if point is not None and (exposed_arm(actor,point,actor.body.bind_weights[index],.13) or
               actor.h*.54 < point.z < actor.h*.615):
                hidden.remove(index)
    if actor.name in ("Runner","Toxic","Crawler"):
        for index in tuple(hidden):
            point = actor.body.geometry.get(index)
            if point is not None and ((actor.name=="Crawler" and point.z<.42) or
               (actor.name in ("Runner","Toxic") and point.x>.02 and point.z<.44)):
                hidden.remove(index)
    return hidden


def append_garment(actor, pack):
    from mathutils import Vector
    from makehuman_body import read_obj, append_reduced
    if actor.name not in OUTFITS:
        return
    name, slot = OUTFITS[actor.name]
    mapping, axes, _ = read_proxy(pack, name)
    source, uv, faces, _ = read_obj(pack.read(f"clothes/{name}/{name}.obj").decode().splitlines())
    assert len(source) == len(mapping), (actor.name, len(source), len(mapping))
    body = actor.body
    scale = Vector((1, 1, 1))
    for axis, (a, b, reference) in axes.items():
        scale[axis] = abs(body.source[a][axis] - body.source[b][axis]) / reference
    positions, weights = [], []
    for indices, amounts, offset in mapping:
        point = sum((body.source[i]*w for i, w in zip(indices, amounts)), Vector((0, 0, 0)))
        point += Vector(tuple(offset[i]*scale[i] for i in range(3)))
        fitted = body.shape(point)
        if fitted.z < .38:
            fitted = body.normalized_foot(fitted, "L" if fitted.x < 0 else "R")
        fitted.y += .004 if fitted.y > .04 else -.004
        positions.append(fitted)
        mixed = defaultdict(float)
        for index, amount in zip(indices, amounts):
            for bone, weight in body.bind_weights[index].items():
                mixed[bone] += max(0, amount)*weight
        ranked = sorted(mixed.items(), key=lambda item: item[1], reverse=True)[:4]
        total = sum(weight for _, weight in ranked)
        weights.append({bone: weight/total for bone, weight in ranked if weight > .00001})
    # Shirt and trousers are separate connected surfaces in the CC0 proxies.
    # Shorten only the torso hem; never lift trousers or the sleeve vertices.
    if actor.hero:
        parent=list(range(len(positions)))
        def root(index):
            while parent[index]!=index:
                parent[index]=parent[parent[index]]
                index=parent[index]
            return index
        for face in faces:
            first=root(face[0][0])
            for index,_ in face[1:]:parent[root(index)]=first
        components=defaultdict(list)
        for index in range(len(positions)):components[root(index)].append(index)
        for entries in components.values():
            if sum(positions[i].z for i in entries)/len(entries)<actor.h*.60:
                continue
            hem=min(positions[i].z for i in entries)
            desired=actor.h*(.584 if actor.name=="Eva" else .568)
            if hem>=desired:continue
            for i in entries:
                p=positions[i]
                side="L" if p.x<0 else "R"
                if sum(weights[i].get(part+side,0) for part in ("UpperArm","Forearm","Hand"))>.35:
                    continue
                t=max(0,min(1,(p.z-hem)/(actor.h*.69-hem)))
                p.z+=(desired-hem)*(1-t*t*(3-2*t))
    # Proxy fitting can assign adjacent armpit or crotch vertices to dissimilar
    # base-body triangles. Smooth that transition before reducing the garment.
    neighbors=[set() for _ in positions]
    for face in faces:
        ids=[index for index,_ in face]
        for a,b in zip(ids,ids[1:]+ids[:1]):
            neighbors[a].add(b)
            neighbors[b].add(a)
    for _ in range(6):
        softened=[]
        for index,entries in enumerate(weights):
            nearby=neighbors[index]
            mixed=defaultdict(float)
            for bone,value in entries.items():mixed[bone]+=value*.50
            for adjacent in nearby:
                for bone,value in weights[adjacent].items():
                    mixed[bone]+=value*.50/max(1,len(nearby))
            ranked=sorted(mixed.items(),key=lambda pair:pair[1],reverse=True)[:4]
            total=sum(value for _,value in ranked)
            softened.append({bone:value/total for bone,value in ranked})
        weights=softened
    accepted = []
    for face in faces:
        center = sum((positions[i] for i, _ in face), Vector((0, 0, 0)))/len(face)
        if actor.tank:
            hem=actor.h*(.36 if actor.name=='Boss' else .22 if center.x<0 else .33)
            hem+=.016*math.sin(center.x*63+center.y*37)
            if not hem<center.z<actor.h*.55:continue
        mixed=defaultdict(float)
        for i,_ in face:
            for bone,value in weights[i].items():mixed[bone]+=value/len(face)
        if exposed_arm(actor,center,mixed):
            continue
        if torn_opening(actor,center):
            continue
        if scout_opening(actor,center):
            continue
        # Keep the previously authored shoes and clear their articulated ankle region.
        if center.z < .19:
            continue
        # Infected clothing has real missing sections and asymmetric ragged hems.
        if not actor.hero and center.z < actor.h*.64:
            if actor.name == "Crawler" and center.z < .42:
                continue
            if actor.name in ("Toxic", "Runner") and center.x > .02 and center.z < .44:
                continue
        accepted.append((tuple(i for i, _ in face), tuple(uv[j] for _, j in face)))
    append_reduced(actor, actor.name+" fitted garment", positions, accepted,
                   [slot]*len(accepted), weights, .62 if actor.hero else .43)

def append_hair(actor, pack):
    from mathutils import Vector
    from makehuman_body import read_obj, append_reduced
    name = "short01" if actor.name in ("Max","Walker") else "ponytail01"
    mapping, axes, _ = read_proxy(pack,name,"hair")
    _, uv, faces, _ = read_obj(pack.read(f"hair/{name}/{name}.obj").decode().splitlines())
    body = actor.body
    scale = Vector((1,1,1))
    for axis,(a,b,reference) in axes.items():
        scale[axis] = abs(body.source[a][axis]-body.source[b][axis])/reference
    points=[]
    for indices,amounts,offset in mapping:
        point=sum((body.source[i]*weight for i,weight in zip(indices,amounts)),Vector((0,0,0)))
        point += Vector(tuple(offset[i]*scale[i] for i in range(3)))
        points.append(body.shape(point))
    geometry=[(tuple(i for i,_ in face),tuple(uv[j] for _,j in face)) for face in faces]
    if not actor.hero:
        # Sparse, irregular remaining locks rather than the survivor's groomed cap.
        geometry=[face for index,face in enumerate(geometry) if index%7 not in (0,1)]
    weights=[]
    for point in points:
        if actor.name=="Eva" and point.y<-.10 and point.z<actor.h*.96:
            t=min(1,max(0,(actor.h*.94-point.z)/(actor.h*.14)))
            weights.append({"HairTie":1-t,"HairTail":t})
        else:
            weights.append({"Head":1.0})
    tile=34 if name=="short01" else 35
    append_reduced(actor,actor.name+" swept hair",points,geometry,[tile]*len(geometry),weights,.58)


def append_fitted_head_part(actor,pack,family,name,tile,ratio):
    from mathutils import Vector
    from makehuman_body import read_obj,append_reduced
    mapping,axes,_=read_proxy(pack,name,family)
    _,uv,faces,_=read_obj(pack.read(f'{family}/{name}/{name}.obj').decode().splitlines())
    scale=Vector((1,1,1))
    for axis,(a,b,reference) in axes.items():
        scale[axis]=abs(actor.body.source[a][axis]-actor.body.source[b][axis])/reference
    points=[]
    for ids,amounts,offset in mapping:
        point=sum((actor.body.source[i]*weight for i,weight in zip(ids,amounts)),Vector((0,0,0)))
        point+=Vector(tuple(offset[i]*scale[i] for i in range(3)))
        points.append(actor.body.shape(point))
    geometry=[(tuple(i for i,_ in face),tuple(uv[j] for _,j in face)) for face in faces]
    append_reduced(actor,actor.name+' '+name,points,geometry,[tile]*len(geometry),[{'Head':1.} for _ in points],ratio)
