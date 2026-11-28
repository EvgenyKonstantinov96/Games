"""Bake the two-sided relief token carried in runners' supply caches."""
import math
from pathlib import Path
import re
from uuid import NAMESPACE_URL, uuid5

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

RESOURCES = Path(__file__).resolve().parents[2] / "Assets/Resources"
ART = RESOURCES / "ProductionArt"
MESHES = RESOURCES / "Meshes"


def import_settings(path, source):
    meta = source.with_name(source.name + ".meta").read_text()
    guid = uuid5(NAMESPACE_URL, "zsr/" + path.name).hex
    meta = re.sub("^guid: .*", "guid: " + guid, meta, count=1, flags=re.MULTILINE)
    path.with_name(path.name + ".meta").write_text("\n".join(s.rstrip() for s in meta.splitlines()) + "\n")


def stamp():
    size = 256
    mask = Image.new("L", (size, size))
    d = ImageDraw.Draw(mask)
    for inset, thickness, intensity in [(9, 5, 190), (19, 2, 155), (42, 2, 126)]:
        d.ellipse((inset, inset, 255 - inset, 255 - inset), outline=intensity, width=thickness)
    for i in range(40):
        phi = math.tau * i / 40
        long = i % 5 == 0
        d.line([(128 + math.cos(phi)*r, 128 + math.sin(phi)*r)
                for r in (94 if long else 99, 104)], fill=168, width=3 if long else 2)
    # A split beacon cut into the die. Large counterforms survive at pickup scale.
    d.polygon([(69,157), (112,74), (128,74), (98,137), (119,137), (109,157)], fill=215)
    d.polygon([(188,157), (144,74), (129,74), (158,137), (136,137), (146,157)], fill=215)
    d.polygon([(112,174), (128,137), (144,174)], fill=205)
    d.ellipse((120,109,136,125), fill=245)
    y, x = np.mgrid[:size,:size]
    die = np.asarray(mask.filter(ImageFilter.GaussianBlur(1.0)), dtype=float) / 255
    rng = np.random.default_rng(21)
    scratches = .6*np.sin(x*.54 + y*.17) + rng.normal(0,1.,(size,size))
    relief = die * 4.2 + scratches * .095
    dy, dx = np.gradient(relief)
    value = 14*die - 20*dx - 18*dy + scratches*2.2
    radius = np.hypot(x-127.5, y-127.5)
    value -= 14*np.clip((radius-83)/12,0,1) * np.clip((112-radius)/10,0,1)
    rgb = np.array([206.,154.,71.]) + value[...,None] * np.array([1.,.81,.53])
    normal = np.stack([-dx, dy, np.ones_like(dx)],axis=-1)
    normal /= np.linalg.norm(normal,axis=-1,keepdims=True)
    for name, pixels, template in [
        ("SalvageToken", rgb, "TrackPropAtlas"),
        ("SalvageToken_normal", (normal*.5+.5)*255, "Highway_normal"),
    ]:
        dest = ART/(name+".png")
        Image.fromarray(np.uint8(np.clip(pixels,0,255))).save(dest, optimize=True)
        import_settings(dest, ART/(template+".png"))


def token_mesh():
    segments = 32
    rings = [(.240,-.034),(.263,-.034),(.275,-.021),(.275,.021),(.263,.034),(.240,.034)]
    vertices, normals, uvs, faces = [], [], [], []

    def vertex(x,y,z,nx,ny,nz):
        vertices.append((x,y,z))
        normals.append((nx,ny,nz))
        uvs.append((.5 + x*.49/.275, .5 + y*.49/.275))
        return len(vertices)

    def loop(radius, depth, normal_r, normal_z):
        return [vertex(radius*math.cos(a),radius*math.sin(a),depth,
                       normal_r*math.cos(a),normal_r*math.sin(a),normal_z)
                for a in (i*math.tau/segments for i in range(segments))]

    for (r0,z0),(r1,z1) in zip(rings,rings[1:]):
        dr, dz = r1-r0, z1-z0
        length = math.hypot(dr,dz)
        a,b = [loop(r,z,dz/length,-dr/length) for r,z in [(r0,z0),(r1,z1)]]
        if r0 == r1:
            # Unwrap the reeded edge, including one duplicate at the texture seam.
            for indices, v in ((a,.023),(b,.055)):
                ix = indices[0]-1
                indices.append(vertex(*vertices[ix], *normals[ix]))
                for i, idx in enumerate(indices):
                    uvs[idx-1] = (.01+.98*i/segments,v)
        for i in range(segments):
            j = i+1 if r0 == r1 else (i+1)%segments
            faces.append((a[i],a[j],b[j],b[i]))
    for side in [-1,1]:
        face = loop(.240,.034*side,0,side)
        center = vertex(0,0,.034*side,0,0,side)
        for i in range(segments):
            j = (i+1)%segments
            faces.append((center,face[i],face[j]) if side>0 else (center,face[j],face[i]))
    dest = MESHES/"salvage_token.obj"
    with dest.open("w") as stream:
        print("o salvage_token",file=stream)
        for prefix,data in [("v",vertices),("vn",normals),("vt",uvs)]:
            for point in data:
                print(prefix,*(f"{value:.6f}" for value in point),file=stream)
        for face in faces:
            print("f",*(f"{i}/{i}/{i}" for i in face),file=stream)
    import_settings(dest, MESHES/"track_gate_pylon.obj")


if __name__ == "__main__":
    stamp()
    token_mesh()
