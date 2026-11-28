"""Build the five game-ready track meshes, weathered surface atlas and gate signs.

Run from the project with Python, NumPy, Pillow and DejaVuSansCondensed-Bold.
"""
from pathlib import Path
from argparse import ArgumentParser
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
MESHES = ROOT / "Assets/Resources/Meshes"
ART = ROOT / "Assets/Resources/ProductionArt"
SOURCE_MESHES = MESHES
SOURCE_ART = ART
ATLAS_TILE = 128
ATLAS_COLUMNS = 4
PALETTE = (
    (117, 119, 116), (143, 145, 138), (56, 68, 78), (104, 115, 121),
    (70, 80, 97), (37, 53, 62), (42, 43, 46), (132, 137, 139),
    (112, 67, 47), (153, 98, 44), (143, 49, 48), (60, 71, 81), (106, 116, 119),
)
CONCRETE, BARRIER, BED, SPIKE, BODY, GLASS, RUBBER, RIM, REBAR, CHEVRON, TAILLIGHT, PYLON, GALVANIZED = range(13)


def build_atlas():
    rng = np.random.default_rng(1309)
    y, x = np.mgrid[:ATLAS_TILE, :ATLAS_TILE].astype(float) / (ATLAS_TILE - 1)
    material_atlas = Image.new("RGB", (512, 512))
    normal_atlas = Image.new("RGB", (512, 512), (128,128,255))
    surface_atlas = Image.new("RGBA", (512, 512), (0,0,0,40))
    for index, base in enumerate(PALETTE):
        low = np.cos(8 * x + 7 * y) + np.sin(16 * (x - .17 * y)) * .52
        grit = rng.normal(0, 5, x.shape)
        grains = rng.random(x.shape)
        layer = np.broadcast_to(np.array(base, dtype=float), x.shape + (3,)).copy()
        relief = .0018*grit + .008*low
        metal,gloss = ((0,.15),(0,.24),(.36,.27),(.80,.58),(.12,.22),(0,.80),
                       (0,.14),(.76,.54),(.40,.25),(0,.24),(.04,.55),(.30,.34),(.78,.47))[index]
        metallic=np.full(x.shape,metal);smooth=np.full(x.shape,gloss)
        if index == BARRIER:
            # Big red and off-white diagonals on a central weathered paint band.
            mask = (y > .18) & (y < .70) & (x > .055) & (x < .945)
            red = ((x + y * .65) * 9).astype(int) % 2 != 0
            paint = np.where(red[..., None], (163, 54, 47), (181, 179, 160))
            mask &= (grains > .06) & (low > -.93)
            layer[mask] = paint[mask]
            relief += mask*.018
            smooth[mask] = .30
        elif index == CHEVRON:
            mask = (((x - y*.60) * 8).astype(int) % 2 != 0) & (grains > .065)
            layer[:] = (45, 49, 49)
            layer[mask] = (222, 153, 55)
        elif index == BODY:
            rust = np.sin(26*x+5*y)*np.cos(43*y-9*x) + low*.14
            burnt = (rust > .66) | ((y > .88) & (rust > -.25))
            layer[burnt] = (119, 76, 53)
            metallic[burnt]=.015;smooth[burnt]=.12
            relief -= burnt*.022
            grit += 9*np.maximum(0, np.cos(2*np.pi*(y-.23))*-.8)
        elif index == GLASS:
            glint = abs(x - y * .65 - .1)
            layer[glint < .10] += 29
            layer[glint < .012] += 20
            grit *= .55
        elif index == GALVANIZED:
            # Brushed zinc, darker water streaks and corrosion toward the road.
            drips = np.sin(73*x+.17*np.sin(18*y)) + .68*np.cos(127*x)
            zinc = (np.sin(41*x+33*y)*np.cos(38*y-13*x) > .62) & (grains > .23)
            layer[zinc] += 14
            grit += drips * (3 + 9*(1-y))
            corrosion = (y < .22) & (drips > .74) & (low > -.4)
            layer[corrosion] = (121, 83, 62)
            metallic[corrosion]=.06;smooth[corrosion]=.16
            relief += zinc*.006-corrosion*.018
        elif index == TAILLIGHT:
            layer += (12*np.sin(y*70))[..., None]
        elif index in (SPIKE, RIM):
            grit += 11 * np.sin(x*31)
        elif index in (BED, PYLON, REBAR):
            scratched = (abs(x + y*.16 - .3) < .009) | (abs(x + y*.15 - .65) < .006)
            layer[scratched & (grains > .33)] += 33
            relief -= scratched*.014
        if index in (CONCRETE,BARRIER):
            # Cast panel pores and chipped paint stay matte, unlike the zinc beam.
            pits=grains<.045
            relief-=pits*.028
            smooth[pits]=.10
        smooth=np.clip(smooth+low*.025+grit*.001, .08,.84)
        layer += (7 * low + grit)[..., None]
        layer[grains < .012] *= .58
        cell = Image.fromarray(np.uint8(np.clip(layer, 0, 255)), "RGB")
        origin=(index % ATLAS_COLUMNS * ATLAS_TILE, index // ATLAS_COLUMNS * ATLAS_TILE)
        material_atlas.paste(cell, origin)
        dy,dx=np.gradient(relief)
        vectors=np.stack((-dx*5,dy*5,np.ones_like(dx)),axis=-1)
        vectors/=np.linalg.norm(vectors,axis=-1,keepdims=True)
        normal_atlas.paste(Image.fromarray(np.uint8(np.clip(vectors*127.5+127.5,0,255))),origin)
        surface=np.zeros((*x.shape,4),dtype=np.uint8)
        surface[:,:,0]=np.uint8(metallic*255);surface[:,:,3]=np.uint8(smooth*255)
        surface_atlas.paste(Image.fromarray(surface),origin)
    material_atlas.save(ART / "TrackPropAtlas.png", optimize=True)
    normal_atlas.save(ART / "TrackPropAtlas_normal.png", optimize=True)
    surface_atlas.save(ART / "TrackPropAtlas_surface.png", optimize=True)


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
        # Cast housings and etched symbols remain distinct even in grayscale.
        outline = [(17,7),(239,7),(250,18),(250,106),(238,119),(18,119),(6,107),(6,19)]
        d.polygon(outline, fill="#405767", outline="#a0aeb0", width=2)
        inset = [(20,12),(236,12),(245,21),(245,103),(235,113),(21,113),(11,103),(11,22)]
        d.polygon(inset, fill="#18232c")
        d.rounded_rectangle((23,37,232,105), radius=8, fill="#0c1824", outline="#344655", width=1)
        d.line((39,26,106,26), fill=accent, width=3)
        d.line((150,26,217,26), fill=accent, width=3)
        for x in (17,239):
            for y in (23,103):
                d.ellipse((x-2,y-2,x+2,y+2), fill="#849599")
        if index == 0:
            for x in (115,125):
                d.line((x,17,x+9,25,x,33), fill=accent, width=3)
        elif index == 1:
            d.polygon([(128,15),(117,27),(126,27),(123,36),(139,21),(130,21),(135,15)], fill=accent)
        elif index == 2:
            d.polygon([(128,14),(139,19),(137,29),(128,36),(119,29),(117,19)], outline=accent, width=3)
        elif index == 3:
            d.rectangle((124,15,131,35), fill=accent)
            d.rectangle((118,21,137,28), fill=accent)
        elif index == 4:
            d.arc((118,15,138,35), 0, 180, fill=accent, width=4)
            for x in (119,136):
                d.line((x,15,x,26), fill=accent, width=4)
        else:
            d.line((117,20,128,29,139,20), fill=accent, width=3)
            for x in (117,139):
                d.ellipse((x-6,16,x+6,22), outline=accent, width=2)
            d.rounded_rectangle((123,25,133,31), radius=2, fill=accent)
        size = 63
        font = ImageFont.truetype(font_path, size)
        while font.getlength(word) > 194:
            size -= 1
            font = ImageFont.truetype(font_path, size)
        box = d.textbbox((0,0),word,font=font)
        d.text((128-(box[0]+box[2])/2,72-(box[1]+box[3])/2), word, font=font, fill="#f0f5f4")
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
        for line in (SOURCE_MESHES / f"{name}.obj").read_text().splitlines():
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
        length=np.linalg.norm(b-a)
        if length<.000001:raise ValueError("Rod needs distinct endpoints")
        normal = (b-a) / length
        reference=np.array([1.,0.,0.]) if abs(normal[1])>.95 else np.array([0.,1.,0.])
        u = np.cross(normal, reference)
        u /= np.linalg.norm(u)
        v = np.cross(normal, u)
        angles = np.arange(6) * np.pi / 3
        offsets = radius * (np.cos(angles[:, None])*u + np.sin(angles[:, None])*v)
        faces = [[i, (i+1)%6, (i+1)%6+6, i+6] for i in range(6)]
        faces += [list(reversed(range(6))),list(range(6,12))]
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
    for side in (-1,1):
        for x in (-.64,.64):
            # Anchor washers and recessed bolt heads on the sloping painted face.
            barrier.rod((x,1.10,side*.452),(x,1.10,side*.469),.032,RIM)
            barrier.rod((x,.33,side*.581),(x,.33,side*.598),.029,REBAR)
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
    for x in (-.16,.16):
        for z in (-.16,.16):
            pylon.rod((x,.16,z),(x,.18,z),.026,RIM)
    pylon.save("track_gate_pylon")

    rail = Prop()
    rail.octagon([(.60,.075,23,.033,0,0),(.631,.105,23,.037,0,0),
                  (.687,.081,23,.035,0,0),(.735,.106,23,.037,0,0),
                  (.763,.104,23,.037,0,0),(.790,.075,23,.033,0,0)], GALVANIZED)
    rail.octagon([(.28,.054,23,.035,0,0),(.311,.068,23,.037,0,0),
                  (.366,.068,23,.037,0,0),(.39,.051,23,.033,0,0)], BED)
    for i in range(11):
        z = 46*((i+.5)/11-.5)
        rail.octagon([(.05,.143,.121,.032,0,z),(.13,.143,.121,.032,0,z),
                      (.16,.084,.081,.024,0,z),(.73,.084,.081,.024,0,z),
                      (.80,.073,.072,.019,0,z)], REBAR)
        if i % 2 == 0:
            for x in (-.098,.098):
                rail.octagon([(.653,.024,.064,.012,x,z),(.687,.024,.064,.012,x,z)], RIM)
            rail.octagon([(.641,.018,.242,.012,0,z-.69),(.744,.018,.242,.012,0,z-.69)], CHEVRON)
    rail.save("track_guardrail")


def write_metas():
    names=('track_wreck','track_barrier','track_spikes','track_rubble','track_gate_pylon','track_guardrail')
    targets = [(MESHES/(name+'.obj'), SOURCE_MESHES/"lux_wreck_body.obj.meta") for name in names]
    targets += [(ART/"TrackPropAtlas.png", SOURCE_ART/"Highway.png.meta"),
                (ART/"TrackPropAtlas_normal.png", SOURCE_ART/"Highway_normal.png.meta"),
                (ART/"TrackPropAtlas_surface.png", SOURCE_ART/"Highway_surface.png.meta"),
                (ART/"TrackGateLabels.png", SOURCE_ART/"ParticleSoft.png.meta")]
    for path, template in targets:
        meta = path.with_suffix(path.suffix + ".meta")
        relative='Assets/Resources/'+('Meshes/' if path.suffix=='.obj' else 'ProductionArt/')+path.name
        existing=ROOT/(relative+'.meta')
        if existing.is_file():
            meta.write_text(existing.read_text())
            continue
        guid = uuid.uuid5(uuid.NAMESPACE_URL, relative).hex
        lines = [line.rstrip() for line in template.read_text().splitlines()]
        lines[1] = "guid: " + guid
        content = "\n".join(lines) + "\n"
        if path.suffix == ".obj":
            content = content.replace("importAnimation: 1", "importAnimation: 0")
        meta.write_text(content)


if __name__ == "__main__":
    parser=ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir',type=Path,help='Alternate Resources directory for isolated generation')
    args=parser.parse_args()
    if args.output_dir:
        MESHES=args.output_dir/'Meshes';ART=args.output_dir/'ProductionArt'
    MESHES.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
    build_atlas()
    build_gate_labels()
    prop_assets()
    write_metas()
