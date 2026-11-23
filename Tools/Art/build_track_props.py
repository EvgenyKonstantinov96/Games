"""Build the five game-ready track meshes, weathered surface atlas and gate signs.

Run from the project with Python, NumPy, Pillow and DejaVuSansCondensed-Bold.
"""
from pathlib import Path
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
MESHES = ROOT / "Assets/Resources/Meshes"
ART = ROOT / "Assets/Resources/ProductionArt"
ATLAS_TILE = 128
ATLAS_COLUMNS = 4
PALETTE = (
    (117, 119, 116), (143, 145, 138), (56, 68, 78), (104, 115, 121),
    (70, 80, 97), (37, 53, 62), (42, 43, 46), (132, 137, 139),
    (112, 67, 47), (153, 98, 44), (143, 49, 48), (60, 71, 81),
)
CONCRETE, BARRIER, BED, SPIKE, BODY, GLASS, RUBBER, RIM, REBAR, CHEVRON, TAILLIGHT, PYLON = range(12)


def build_atlas():
    rng = np.random.default_rng(1309)
    y, x = np.mgrid[:ATLAS_TILE, :ATLAS_TILE].astype(float) / (ATLAS_TILE - 1)
    material_atlas = Image.new("RGB", (512, 512))
    for index, base in enumerate(PALETTE):
        low = np.cos(8 * x + 7 * y) + np.sin(16 * (x - .17 * y)) * .52
        grit = rng.normal(0, 5, x.shape)
        grains = rng.random(x.shape)
        layer = np.broadcast_to(np.array(base, dtype=float), x.shape + (3,)).copy()
        if index == BARRIER:
            # Big red and off-white diagonals on a central weathered paint band.
            mask = (y > .18) & (y < .70) & (x > .055) & (x < .945)
            red = ((x + y * .65) * 9).astype(int) % 2 != 0
            paint = np.where(red[..., None], (163, 54, 47), (181, 179, 160))
            mask &= (grains > .06) & (low > -.93)
            layer[mask] = paint[mask]
        elif index == CHEVRON:
            mask = (((x - y*.60) * 8).astype(int) % 2 != 0) & (grains > .065)
            layer[:] = (45, 49, 49)
            layer[mask] = (222, 153, 55)
        elif index == BODY:
            rust = np.sin(26*x+5*y)*np.cos(43*y-9*x) + low*.14
            burnt = (rust > .66) | ((y > .88) & (rust > -.25))
            layer[burnt] = (119, 76, 53)
            grit += 9*np.maximum(0, np.cos(2*np.pi*(y-.23))*-.8)
        elif index == GLASS:
            glint = abs(x - y * .65 - .1)
            layer[glint < .10] += 29
            layer[glint < .012] += 20
            grit *= .55
        elif index == TAILLIGHT:
            layer += (12*np.sin(y*70))[..., None]
        elif index in (SPIKE, RIM):
            grit += 11 * np.sin(x*31)
        elif index in (BED, PYLON, REBAR):
            scratched = (abs(x + y*.16 - .3) < .009) | (abs(x + y*.15 - .65) < .006)
            layer[scratched & (grains > .33)] += 33
        layer += (7 * low + grit)[..., None]
        layer[grains < .012] *= .58
        cell = Image.fromarray(np.uint8(np.clip(layer, 0, 255)), "RGB")
        material_atlas.paste(cell, (index % ATLAS_COLUMNS * ATLAS_TILE, index // ATLAS_COLUMNS * ATLAS_TILE))
    material_atlas.save(ART / "TrackPropAtlas.png", optimize=True)


def build_gate_labels():
    font_path = "/usr/share/fonts/truetype/dejavu/DejaVuSansCondensed-Bold.ttf"
    cards = (
        ("×2", "#ff9a60"), ("ТЕМП", "#71e7ff"), ("ЩИТ", "#88a8ff"),
        ("HP", "#71faae"), ("МАГНИТ", "#ffd27a"), ("ДРОН", "#d9aaff"),
    )
    atlas = Image.new("RGBA", (512, 512))
    for index, (word, accent) in enumerate(cards):
        card = Image.new("RGBA", (256, 128))
        d = ImageDraw.Draw(card)
        d.rounded_rectangle((7, 7, 249, 120), radius=17, fill="#0d1722", outline="#5e7485", width=3)
        d.rounded_rectangle((19, 20, 237, 108), radius=10, fill="#151f2b")
        d.line((27, 15, 229, 15), fill=accent, width=4)
        d.line((90, 112, 166, 112), fill=accent, width=3)
        size = min(74, int(285 / len(word)))
        font = ImageFont.truetype(font_path, size)
        d.text((128, 64), word, font=font, anchor="mm", fill="#f5faff", stroke_width=1, stroke_fill="#0c111b")
        atlas.alpha_composite(card, (index % 2 * 256, index // 2 * 128))
    atlas.save(ART / "TrackGateLabels.png", optimize=True)


class Prop:
    def __init__(self):
        self.points, self.faces = [], []

    def add(self, points, faces, tile, front=None):
        points = np.array(points)
        lo, span = points.min(axis=0), np.maximum(np.ptp(points, axis=0), .0001)
        start = len(self.points)
        self.points.extend(points)
        for f in faces:
            face = points[f]
            normal = np.abs(np.cross(face[1]-face[0], face[2]-face[0]))
            axis = np.argmax(normal)
            slot = front if axis == 2 and front is not None else tile
            # Long, box-projected islands keep the damage continuous across flat surfaces.
            axes = [(2, 1), (0, 2), (0, 1)][axis]
            local = (face-lo) / span
            col, row = slot % ATLAS_COLUMNS, slot // ATLAS_COLUMNS
            uv = [((col*128 + 5 + u*118)/512, 1-(row*128 + 123 - v*118)/512)
                  for u, v in local[:, axes]]
            self.faces.append(([i+start for i in f], uv))

    def existing(self, name, tile, scale=(1., 1., 1.)):
        points, faces = [], []
        for line in (MESHES / f"{name}.obj").read_text().splitlines():
            if line.startswith("v "):
                points.append([float(x) for x in line.split()[1:4]])
            if line.startswith("f "):
                faces.append([int(x.split("/")[0])-1 for x in line.split()[1:]])
        self.add(np.array(points)*np.array(scale), faces, tile)

    def octagon(self, rings, tile, front=None):
        # Each ring is a rectangle with bevels at the corners.
        pts = []
        for y, sx, sz, bevel, cx, cz in rings:
            pts += [(cx+x, y, cz+z) for x,z in (
                (-sx+bevel,-sz), (sx-bevel,-sz), (sx,-sz+bevel), (sx,sz-bevel),
                (sx-bevel,sz), (-sx+bevel,sz), (-sx,sz-bevel), (-sx,-sz+bevel),
            )]
        # Positive Y-up normal on top. Tangents are populated by Unity from UVs.
        n = len(rings)
        faces = [list(range(8))]
        faces += [[r*8+i, (r+1)*8+i, (r+1)*8+(i+1)%8, r*8+(i+1)%8]
                  for r in range(n-1) for i in range(8)]
        faces += [list(reversed(range((n-1)*8, n*8)))]
        self.add(pts, faces, tile, front)

    def rod(self, a, b, radius, tile):
        a, b = np.array(a), np.array(b)
        normal = (b-a) / np.linalg.norm(b-a)
        u = np.cross(normal, np.array([0., 1., 0.]))
        u /= np.linalg.norm(u)
        v = np.cross(normal, u)
        angles = np.arange(6) * np.pi / 3
        offsets = radius * (np.cos(angles[:, None])*u + np.sin(angles[:, None])*v)
        faces = [[i, (i+1)%6, (i+1)%6+6, i+6] for i in range(6)]
        self.add(np.concatenate([a+offsets,b+offsets]), faces, tile)

    def save(self, name):
        points = np.array(self.points)
        weighted = []
        neighbors = [[] for _ in self.points]
        for index, (vertices, _) in enumerate(self.faces):
            polygon = points[vertices]
            cross = np.cross(polygon, np.roll(polygon, -1, axis=0)).sum(axis=0)
            weighted.append(cross)
            for vertex in vertices:
                neighbors[vertex].append(index)
        weighted = np.array(weighted)
        normals = weighted / np.maximum(np.linalg.norm(weighted, axis=1, keepdims=True), .00001)

        with (MESHES / f"{name}.obj").open("w") as fh:
            print(f"# Zombie Survival Run track art\no {name}", file=fh)
            for point in self.points:
                print("v {:.5f} {:.5f} {:.5f}".format(*point), file=fh)
            for _, corners in self.faces:
                for corner in corners:
                    print("vt {:.6f} {:.6f}".format(*corner), file=fh)
            for i, (vertices, _) in enumerate(self.faces):
                for vertex in vertices:
                    adjacent = [j for j in neighbors[vertex] if normals[j] @ normals[i] >= .78]
                    normal = weighted[adjacent].sum(axis=0)
                    length = np.linalg.norm(normal)
                    normal = normal / length if length > .00001 else (0., 1., 0.)
                    print("vn {:.6f} {:.6f} {:.6f}".format(*normal), file=fh)
            offset = 1
            for vertices, corners in self.faces:
                print("f", " ".join(f"{i+1}/{j}/{j}" for i,j in zip(vertices, range(offset, offset+len(corners)))), file=fh)
                offset += len(corners)


def prop_assets():
    car = Prop()
    for name, tile in (("body",BODY),("wheels",RUBBER),("hubs",RIM),("windows",GLASS),("lights",TAILLIGHT)):
        car.existing("lux_wreck_" + name, tile)
    car.octagon([(.36,.81,.12,.06,0,-1.82),(.56,.84,.12,.06,0,-1.82),(.61,.77,.09,.05,0,-1.82)], PYLON)
    car.save("track_wreck")

    barrier = Prop()
    barrier.octagon([(0,1.27,.58,.09,0,0),(.18,1.38,.63,.10,0,0),(.46,1.19,.57,.10,0,0),
                     (1.47,.91,.40,.09,0,0),(1.60,.80,.34,.075,0,0)], CONCRETE, BARRIER)
    barrier.save("track_barrier")

    trap = Prop()
    trap.octagon([(.01,1.18,1.06,.10,0,0),(.08,1.325,1.14,.11,0,0),(.23,1.24,1.08,.09,0,0)], BED, CHEVRON)
    for z in (-.75, 0, .75):
        for i, x in enumerate((-1., -.5, 0, .5, 1.)):
            h = (.84, .94, 1.00, .94, .84)[i]
            trap.octagon([(.23,.155,.155,.055,x,z),(.32,.14,.14,.055,x,z),
                          (.62,.082,.079,.030,x,z), (h,.013,.011,.004,x*.98,z-.065)], SPIKE)
    trap.save("track_spikes")

    rubble = Prop()
    rubble.existing("lux_rubble", CONCRETE, (.86, 1., .92))
    for k in range(3):
        start = (-.72+k*.61,.53,-.87)
        low = (-.68+k*.60,.78,-.25)
        end = (-.58+k*.59,1.16,.45)
        rubble.rod(start, low, .032, REBAR)
        rubble.rod(low, end, .028, RIM)
    rubble.save("track_rubble")

    pylon = Prop()
    pylon.octagon([(0,.23,.24,.045,0,0),(.16,.23,.24,.045,0,0),(.29,.15,.165,.032,0,0),
                   (2.35,.15,.165,.032,0,0),(2.47,.19,.199,.035,0,0),(2.57,.145,.157,.033,0,0)], PYLON)
    pylon.octagon([(.47,.175,.055,.026,0,-.137),(.61,.175,.055,.026,0,-.137)], RIM)
    pylon.octagon([(2.25,.165,.048,.023,0,-.142),(2.33,.165,.048,.023,0,-.142)], RIM)
    pylon.save("track_gate_pylon")


def write_metas():
    targets = [(path, MESHES/"lux_wreck_body.obj.meta") for path in MESHES.glob("track_*.obj")]
    targets += [(ART/"TrackPropAtlas.png", ART/"Highway.png.meta"),
                (ART/"TrackGateLabels.png", ART/"ParticleSoft.png.meta")]
    for path, template in targets:
        meta = path.with_suffix(path.suffix + ".meta")
        guid = uuid.uuid5(uuid.NAMESPACE_URL, path.relative_to(ROOT).as_posix()).hex
        lines = [line.rstrip() for line in template.read_text().splitlines()]
        lines[1] = "guid: " + guid
        content = "\n".join(lines) + "\n"
        if path.suffix == ".obj":
            content = content.replace("importAnimation: 1", "importAnimation: 0")
        meta.write_text(content)


if __name__ == "__main__":
    build_atlas()
    build_gate_labels()
    prop_assets()
    write_metas()
