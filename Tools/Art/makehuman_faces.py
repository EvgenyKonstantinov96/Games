"""Adapt CC0 MakeHuman head geometry to the game's authored character rigs."""

from functools import lru_cache
from pathlib import Path

import bpy
from mathutils import Vector

MORPHS = {
    "Max": "caucasian-male-young",
    "Eva": "caucasian-female-young",
    "Walker": "caucasian-male-old",
    "Runner": "asian-male-young",
    "Armored": "african-male-young",
    "Brute": "african-male-old",
    "Toxic": "asian-male-old",
    "Crawler": "caucasian-female-old",
    "Boss": "caucasian-male-old",
}

# These eight squares occupy the bottom half of the shared character atlas.
FACE_SLOTS = {
    "Max": 0, "Eva": 1, "Walker": 2, "Runner": 3, "Armored": 4,
    "Brute": 4, "Toxic": 5, "Crawler": 6, "Boss": 7,
}


@lru_cache(maxsize=1)
def read_base(data):
    points, uv, faces = [], [], []
    group = ""
    for line in (data / "3dobjs/base.obj").read_text().splitlines():
        if line.startswith("v "):
            points.append(tuple(map(float, line.split()[1:])))
        elif line.startswith("vt "):
            uv.append(tuple(map(float, line.split()[1:3])))
        elif line.startswith("g "):
            group = line[2:]
        elif line.startswith("f ") and group == "body":
            face = [tuple(int(i) - 1 for i in word.split("/")[:2]) for word in line.split()[1:]]
            if all(points[i][1] >= 5.85 and abs(points[i][0]) < 1.2 for i, _ in face):
                faces.append(tuple(reversed(face)))
    return points, uv, faces


def targets(file):
    return {int(n): tuple(map(float, (x, y, z))) for n, x, y, z in
            (line.split() for line in file.read_text().splitlines()
             if line.strip() and not line.lstrip().startswith("#"))}


def append_face(character, data):
    points, uv, faces = read_base(data)
    name, h = character.name, character.h
    morph = targets(data / "targets/macrodetails" / (MORPHS[name] + ".target"))
    jaw = {} if character.hero or name == "Armored" else targets(
        data / "targets/expression/units/caucasian/mouth-open.target")
    used = sorted({i for face in faces for i, _ in face})
    index = {i:k for k,i in enumerate(used)}
    width = .114 if name == "Eva" else .122 if not character.tank else .148
    jaw_amount = .72 if name == "Boss" else .43
    vertices = []
    for i in used:
        x, y, z = points[i]
        dx, dy, dz = morph.get(i, (0, 0, 0))
        jx, jy, jz = jaw.get(i, (0, 0, 0))
        asymmetry = .008 if not character.hero and x > .24 and 6.3 < y < 7.5 else 0
        vertices.append(((x + dx + jx * jaw_amount) * width,
                         (z + dz + jz * jaw_amount - .54) * .108 + .031,
                         h * (.852 + (y + dy * .40 + jy * jaw_amount - 5.85) * .065) - asymmetry))
    mesh = bpy.data.meshes.new(name + " head source")
    mesh.from_pydata(vertices, [], [[index[i] for i, _ in face] for face in faces])
    layer = mesh.uv_layers.new(name="Face")
    for polygon, face in zip(mesh.polygons, faces):
        for loop, (_, j) in zip(polygon.loop_indices, face):
            layer.data[loop].uv = uv[j]
    source = bpy.data.objects.new(name + " face", mesh)
    bpy.context.collection.objects.link(source)
    simplifier = source.modifiers.new("Mobile facial topology", "DECIMATE")
    simplifier.ratio = .15 if character.hero else .16
    evaluated = source.evaluated_get(bpy.context.evaluated_depsgraph_get())
    reduced = evaluated.to_mesh()
    base = len(character.vertices)
    character.vertices.extend(tuple(p.co) for p in reduced.vertices)
    character.weights.extend({"Head":1.0} for _ in reduced.vertices)
    slot = 24 + FACE_SLOTS[name]
    layer = reduced.uv_layers.active
    for polygon in reduced.polygons:
        character.faces.append(tuple(base+i for i in polygon.vertices))
        character.tiles.append((slot, tuple(tuple(layer.data[i].uv) for i in polygon.loop_indices)))
    if character.hero:
        append_scalp(character, reduced)
    evaluated.to_mesh_clear()
    bpy.data.objects.remove(source, do_unlink=True)
    bpy.data.meshes.remove(mesh)


def append_scalp(character, face_mesh):
    # Fit the hairline to the morphed skull and raise the shell a few millimeters.
    h = character.h
    center = Vector((0,.028,h*.966))
    scalp = []
    for face in face_mesh.polygons:
        points = [face_mesh.vertices[i].co for i in face.vertices]
        if all(p.z > h*(.973+.038*max(-1,min(1,(p.y-.031)/.095))) for p in points):
            scalp.append(face)
    seen = {}
    for face in scalp:
        for i in face.vertices:
            if i not in seen:
                p = face_mesh.vertices[i].co
                obj = p + (p-center).normalized()*.0055
                seen[i] = len(character.vertices)
                character.vertices.append(tuple(obj))
                character.weights.append({"Head": 1.0})
        character.faces.append(tuple(seen[i] for i in face.vertices))
        character.tiles.append((17, tuple(((face_mesh.vertices[i].co.x+.13)/.26,
                                          (face_mesh.vertices[i].co.z/h-.88)/.21) for i in face.vertices)))
