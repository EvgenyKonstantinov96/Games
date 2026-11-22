"""Fit smooth jeans meshes from the MakeHuman CC0 system pack to Max and Eva."""

from mathutils import Vector

PANTS = {"Max": "male_casualsuit04", "Eva": "female_casualsuit01"}


def append_pants(character, pack):
    name = PANTS[character.name]
    obj = pack.read(f"clothes/{name}/{name}.obj").decode()
    points, uv, faces = [], [], []
    for line in obj.splitlines():
        if line.startswith("v "):
            points.append(tuple(map(float, line.split()[1:])))
        elif line.startswith("vt "):
            uv.append(tuple(map(float, line.split()[1:])))
        elif line.startswith("f "):
            faces.append(tuple(reversed([
                tuple(int(i)-1 for i in word.split("/")[:2]) for word in line.split()[1:]
            ])))

    # Boots and trousers are separate from the other garments. Find the pants
    # shell from a cuff using OBJ connectivity and keep its original UV layout.
    links = [set() for p in points]
    for face in faces:
        vertices = [v for v,_ in face]
        for vertex in vertices:
            links[vertex].update(vertices)
    cuff = min(range(len(points)), key=lambda i: points[i][1])
    used, pending = {cuff}, [cuff]
    while pending:
        found = links[pending.pop()] - used
        used.update(found)
        pending.extend(found)

    h = character.h
    # (MakeHuman leg center X, Y, Z) ordered from cuff to waistband.
    bind_centers = [(2.196,-7.448,-.009), (1.581,-3.695,.32), (1.104,.486,.123)]
    index = {}

    for i in sorted(used):
        x,y,z = points[i]
        side = "L" if x < 0 else "R"
        sign = -1 if x < 0 else 1
        hip, knee, ankle = [character.bones[b+side][0] for b in ("Thigh","Shin","Foot")]
        if y < bind_centers[1][1]:
            src_a,src_b = bind_centers[:2]
            dst_a,dst_b = ankle,knee
        else:
            src_a,src_b = bind_centers[1:]
            dst_a,dst_b = knee,hip
        t=(y-src_a[1])/(src_b[1]-src_a[1])
        src = Vector(src_a).lerp(Vector(src_b),t)
        dst = dst_a.lerp(dst_b,t)
        taper = min(1,max(0,(.4-y)/2.3))
        body_x = x * (.081 if character.name=="Eva" else .09)
        leg_x = dst.x + (x-src.x*sign)*.103
        waist_rise = min(1,max(0,(y+1.55)/2.36)) * (.12 if character.name=="Eva" else .035)
        pos = (body_x+(leg_x-body_x)*taper, dst.y+(z-src.z)*.112, dst.z+waist_rise)
        shin = min(1,max(0,(-y-3.25)/.90))
        pelvis = min(1,max(0,(y+.9)/1.6))
        pair = {"Pelvis": pelvis, "Thigh"+side: (1-pelvis)*(1-shin), "Shin"+side: (1-pelvis)*shin}
        index[i] = len(character.vertices)
        character.vertices.append(pos)
        character.weights.append({key:value for key,value in pair.items() if value > 0})

    slot=32 if character.name=="Max" else 33
    for face in faces:
        if all(i in used for i,_ in face):
            character.faces.append(tuple(index[i] for i,_ in face))
            character.tiles.append((slot,tuple(uv[j] for _,j in face)))
