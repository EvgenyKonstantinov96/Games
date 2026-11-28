"""Registered surface maps for the existing CC0 MakeHuman UVs.

No atlas rectangles or model UVs are changed here. Garment wear follows the
source proxy's actual knee/elbow/hem positions and authored AO/normal seams.
Skin landmarks were measured on data/3dobjs/base.obj, not guessed in atlas space.
"""
from io import BytesIO
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def pixels(image):
    return np.asarray(image, dtype=np.float32)


def rgb(array):
    return Image.fromarray(np.uint8(np.clip(array, 0, 255)))


def read_image(pack, path, size, mode="RGB"):
    return Image.open(BytesIO(pack.read(path))).convert(mode).resize(size, Image.Resampling.LANCZOS)


def blur(field, radius):
    # Pillow's floating point mode does not support GaussianBlur. Rescale signed
    # fields explicitly so this helper also works for normal-map components.
    low, high = float(field.min()), float(field.max())
    if high-low < 1e-7:
        return np.full_like(field, low)
    image = Image.fromarray(np.uint8(np.clip((field-low)*255/(high-low), 0, 255)))
    return pixels(image.filter(ImageFilter.GaussianBlur(radius)))*(high-low)/255+low


def grain(size, seed, radius):
    field = np.random.default_rng(seed).random((size[1], size[0]), dtype=np.float32)
    field = blur(field, radius)
    return (field-field.mean()) / max(float(field.std()), .0001)


def normal_from_height(height, strength=3):
    dy, dx = np.gradient(height)
    vectors = np.stack((-dx*strength, dy*strength, np.ones_like(height)), axis=-1)
    vectors /= np.linalg.norm(vectors, axis=-1, keepdims=True)
    return rgb(vectors*127.5+127.5)


def normalize_normal(image):
    vectors = pixels(image)/127.5-1
    # Some CC0 normal maps contain black/white unused UV background. They are
    # not tangent normals and must not bleed into a mipmapped seam border.
    invalid = (vectors[:, :, 2] < .20) | (vectors.min(axis=-1) > .95)
    vectors[invalid] = (0, 0, 1)
    vectors /= np.maximum(np.linalg.norm(vectors, axis=-1, keepdims=True), .0001)
    return rgb(vectors*127.5+127.5)


def relief(source, strength=.07):
    gray = pixels(source.convert("L"))/255
    return (gray-blur(gray, 1.7))*strength


def combine_normal(source, height, strength=2):
    original = pixels(source)/127.5-1
    bump = pixels(normal_from_height(height, strength))/127.5-1
    combined = np.dstack((original[:, :, 0]+bump[:, :, 0],
                          original[:, :, 1]+bump[:, :, 1],
                          original[:, :, 2]*bump[:, :, 2]))
    combined /= np.maximum(np.linalg.norm(combined, axis=-1, keepdims=True), .0001)
    return rgb(combined*127.5+127.5)


def surface(metallic, smoothness):
    packed = np.zeros((*smoothness.shape, 4), dtype=np.uint8)
    packed[:, :, 0] = np.uint8(np.clip(metallic, 0, 255))
    packed[:, :, 3] = np.uint8(np.clip(smoothness, 0, 255))
    return Image.fromarray(packed)


def proxy_fields(pack, garment, size):
    """Rasterize native OBJ UVs to source physical XYZ; no Blender dependency.

    The existing 3px atlas inset rescales these UVs uniformly during export. Its
    coordinates must be applied to the image too (see inset()).
    """
    points, uvs, faces = [], [], []
    for line in pack.read(f"clothes/{garment}/{garment}.obj").decode().splitlines():
        words = line.split()
        if not words:
            continue
        if words[0] == "v":
            points.append(tuple(map(float, words[1:4])))
        elif words[0] == "vt":
            uvs.append(tuple(map(float, words[1:3])))
        elif words[0] == "f":
            face = [tuple(int(x)-1 for x in word.split("/")[:2]) for word in words[1:]]
            faces.extend((face[0], face[i], face[i+1]) for i in range(1, len(face)-1))
    points, uvs = np.array(points), np.array(uvs)
    xyz = np.zeros((size[1], size[0], 3), dtype=np.float32)
    covered = np.zeros((size[1], size[0]), dtype=bool)
    for triangle in faces:
        ids, indices = zip(*triangle)
        coords = uvs[list(indices)] * np.array([size[0]-1, -(size[1]-1)])
        coords[:, 1] += size[1]-1
        a, b, c = coords
        left, top = np.maximum(np.floor(coords.min(axis=0)).astype(int), 0)
        right, bottom = np.minimum(np.ceil(coords.max(axis=0)).astype(int), np.array(size)-1)
        if left > right or top > bottom:
            continue
        yy, xx = np.mgrid[top:bottom+1, left:right+1]
        denominator = (b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(denominator) < 1e-7:
            continue
        wa = ((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1])) / denominator
        wb = ((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1])) / denominator
        wc = 1-wa-wb
        mask = (wa >= -.001) & (wb >= -.001) & (wc >= -.001)
        positions = (wa[:, :, None]*points[ids[0]]+wb[:, :, None]*points[ids[1]]+
                     wc[:, :, None]*points[ids[2]])
        xyz[top:bottom+1, left:right+1][mask] = positions[mask]
        covered[top:bottom+1, left:right+1] |= mask
    return xyz, covered


def garment_maps(pack, garment, size, color, infected, seed):
    path = f"clothes/{garment}/{garment}"
    source = read_image(pack, path+"_diffuse.png", size)
    source_pixels = pixels(source)/255
    gray = source_pixels @ np.array([.26, .58, .16])
    ao = pixels(read_image(pack, path+"_ao.png", size, "L"))/255
    normal = normalize_normal(read_image(pack, path+"_normal.png", size))
    normal_vectors = pixels(normal)/127.5-1
    xyz, coverage = proxy_fields(pack, garment, size)
    # Native +Y is up. Values are measured on the CC0 base's helper joints.
    height = xyz[:, :, 1]
    front = np.clip((xyz[:, :, 2]-.05)*1.4, 0, 1)
    knees = np.exp(-((height+3.694825)/.58)**2)*front*(abs(xyz[:, :, 0]) < 2.6)
    elbows = np.exp(-((height-3.493188)/.48)**2)*(abs(xyz[:, :, 0]) > 2.6)
    hem = np.clip((-5.3-height)/2.2, 0, 1)
    pressure = np.clip(knees+elbows+.28*hem, 0, 1)*coverage
    # The source's denim islands are retained as trousers; its seam/pocket/lapel
    # shading is preserved instead of replacing it with a flat silhouette tint.
    denim = ((source_pixels[:, :, 2] > source_pixels[:, :, 0]*1.12) &
             (source_pixels[:, :, 2] > source_pixels[:, :, 1]*1.03) & (height < 1.2))
    fabric_color = np.where(denim[:, :, None], np.array([43, 49, 51]), np.array(color))
    paint = gray[:, :, None]*fabric_color*2.18
    source_chroma = source_pixels-gray[:, :, None]
    paint += source_chroma*18
    # Existing source normals and AO identify actual folded and sewn edges. A
    # small high pass makes their thread and worn crest readable after mipmaps.
    seam = np.clip(np.linalg.norm(normal_vectors[:, :, :2], axis=-1)-.12, 0, .55)
    ridge = np.maximum(gray-blur(gray, max(1, size[0]/230)), 0)
    wrinkle = np.clip(1-ao, 0, 1)
    weave = grain(size, seed, .40)
    patina = np.clip(.48+grain(size, seed+1, size[0]/60)*.20, 0, 1)
    abrasion = pressure*(.42+.58*patina)+ridge*2.4+seam*.35
    paint += abrasion[:, :, None]*np.array([17, 17, 14])
    paint *= (1-wrinkle*.22)[:, :, None]
    paint += weave[:, :, None]*1.05
    dirt = (hem*.22+wrinkle*.20+pressure*.08)*patina*coverage
    if infected:
        dirt += (np.clip(patina-.43, 0, .6)*(.45+.55*hem)+wrinkle*.24)*coverage
        paint *= (1-dirt*.78)[:, :, None]
        # Mud desaturates fibres. Dried blood is confined to the source's chest
        # fabric, near the existing torn-open torso region, not screen stripes.
        chest = np.exp(-((height-4.2)/1.0)**2)*front
        stain = chest*np.clip(patina-.38, 0, .6)*.50
        paint = paint*(1-stain[:, :, None])+np.array([54, 30, 26])*stain[:, :, None]
    else:
        paint *= (1-dirt*.35)[:, :, None]
    fine_height = weave*.0028+ridge*.026-seam*.008
    final_normal = combine_normal(normal, fine_height, 2.5)
    gloss = np.clip(37+abrasion*13-dirt*25+gray*5, 18, 62)
    return rgb(paint), final_normal, surface(0, gloss)


# UVs on MakeHuman data/3dobjs/base.obj, bottom-left convention. The face island
# is rotated: UV u is approximately anatomical height, v runs across the face.
FACE = {"eye_l": (.83350, .51523), "eye_r": (.83350, .45111),
        "cheek_l": (.87502, .52945), "cheek_r": (.87502, .43688),
        "temple": (.83406, .58209), "nose": (.87184, .48317),
        "chin": (.95704, .48317), "forearm_l": (.70787, .81846),
        "forearm_r": (.05319, .81846)}


def spot(size, uv, radii, angle=0):
    yy, xx = np.mgrid[:size[1], :size[0]]
    dx, dy = xx/size[0]-uv[0], 1-yy/size[1]-uv[1]
    cosine, sine = np.cos(angle), np.sin(angle)
    a, b = (dx*cosine+dy*sine)/radii[0], (-dx*sine+dy*cosine)/radii[1]
    return np.exp(-(a*a+b*b)*2)


def stroke(size, coords, width):
    image = Image.new("L", size)
    draw = ImageDraw.Draw(image)
    line = [(round(u*size[0]), round((1-v)*size[1])) for u, v in coords]
    draw.line(line, fill=255, width=max(1, round(width*size[0])), joint="curve")
    return pixels(image.filter(ImageFilter.GaussianBlur(max(.35, size[0]*width*.30))))/255


def skin_maps(source, size, tint, slot):
    source = source.resize(size, Image.Resampling.LANCZOS)
    paint = pixels(source)*np.array(tint)
    source_gray = pixels(source.convert("L"))/255
    # Native pores, folds, age marks and lip detail stay registered to the mesh.
    detail = source_gray-blur(source_gray, max(1.0, size[0]/250))
    paint += detail[:, :, None]*np.array([28, 24, 21])
    pores = grain(size, 1890+slot, .35)
    height = relief(source, .12)+pores*.0013
    gloss = np.full((size[1], size[0]), 82, dtype=np.float32)+detail*32
    if slot < 26:
        # Restrained exposure/freckling rather than a coat of dirt over eyes.
        cheeks = spot(size, FACE["cheek_l"], (.025, .033))+spot(size, FACE["cheek_r"], (.025, .033))
        paint += cheeks[:, :, None]*np.array([7, 1, -2])
        if slot == 24:
            scar = stroke(size, ((.855, .541), (.869, .545), (.882, .543), (.892, .548)), .0021)
            scar_halo = blur(scar, size[0]/340)
            paint += scar_halo[:, :, None]*np.array([9, -3, -4])
            paint += scar[:, :, None]*np.array([17, 9, 6])
            height += scar*.014
    else:
        # Coherent source features remain visible under diffuse, nonperiodic
        # discoloration; the previous global sine waves crossed eyes and lips.
        patina = grain(size, 1900+slot, size[0]/70)
        fine = grain(size, 2950+slot, size[0]/180)
        mottling = np.clip(patina*.55+fine*.20, -1.5, 1.5)
        paint += mottling[:, :, None]*np.array([10, 8, 4])
        eyes = sum(spot(size, FACE[key], (.014, .022)) for key in ("eye_l", "eye_r"))
        cheeks = sum(spot(size, FACE[key], (.029, .023), -.25) for key in ("cheek_l", "cheek_r"))
        paint -= eyes[:, :, None]*np.array([33, 32, 25])
        paint -= cheeks[:, :, None]*np.array([16, 16, 11])
        # Nasolabial folds follow the source's rotated face UV, from either
        # side of the nose towards the mouth. These are skin folds, not decals.
        fold = stroke(size, ((.869, .499), (.891, .507), (.915, .500)), .0025)
        fold += stroke(size, ((.869, .467), (.891, .459), (.915, .466)), .0025)
        paint -= fold[:, :, None]*np.array([24, 22, 15])
        height -= fold*.018
        scar_uv = ((.848, .553), (.866, .545), (.884, .553), (.901, .549))
        if slot in (27, 29, 31):
            scar_uv = ((.859, .434), (.879, .427), (.899, .435), (.921, .426))
        lesion = stroke(size, scar_uv, .0045)
        halo = blur(lesion, max(.9, size[0]/160))
        paint = paint*(1-lesion[:, :, None]*.68)+np.array([66, 34, 30])*lesion[:, :, None]*.68
        paint += halo[:, :, None]*np.array([8, -8, -6])
        height -= lesion*.035
        # Forearm abrasion uses the original source arm UV islands; it will
        # remain on the arm when the character raises its weapon or attacks.
        arm = spot(size, FACE["forearm_l" if slot%2 else "forearm_r"], (.035, .016), .3)
        arm *= np.clip(.65+fine*.22, 0, 1)
        paint += arm[:, :, None]*np.array([-18, -27, -20])
        height -= arm*.016
        gloss = 60+detail*28+lesion*41+eyes*7
        if slot == 29:
            paint += halo[:, :, None]*np.array([-3, 12, -3])
            gloss += halo*25
    return rgb(paint), normal_from_height(height, 9), surface(0, np.clip(gloss, 36, 126))


def hair_maps(pack, name, size):
    source = read_image(pack, f"hair/{name}/{name}_diffuse.png", size, "RGBA")
    data = pixels(source)
    alpha = data[:, :, 3]/255
    gray = data[:, :, :3] @ np.array([.26, .58, .16])
    # One opaque URP material cannot express source hair-card transparency.
    # Match the under-scalp pigment, and remove the original light matte before
    # compositing. Eva retains the source's warm chestnut highlights and swept
    # strands instead of sharing Max's near-black crop. The matching anatomical
    # scalp underlay samples this same ponytail region, so it cannot look bald.
    chestnut = name == "ponytail01"
    pigment = np.array([82, 48, 31] if chestnut else [42, 28, 22])
    level = (np.clip(.32+gray/118, .40, 1.90) if chestnut
             else np.clip(gray/58, .28, 1.65))
    strands = pigment*level[:, :, None]
    base = pigment*((.72 if chestnut else .76)+.05*grain(size, 9371, 1))[:, :, None]
    paint = base*(1-alpha[:, :, None])+strands*alpha[:, :, None]
    source_detail = (gray-blur(gray, 1.2))*alpha/255
    return (rgb(paint), normal_from_height(source_detail*(.18 if chestnut else .12), 9 if chestnut else 7),
            surface(0, (74 if chestnut else 69)+alpha*25))


def inset(image, pad=3):
    """Match atlas_uv's inset and extend border texels into the reserved gutter.

    Source UV(0,0) was previously sampled 3px inside an unscaled texture, causing
    a visible offset at the small eye/hair islands and clothing seam edges.
    """
    width, height = image.size
    inner = image.resize((width-pad*2, height-pad*2), Image.Resampling.LANCZOS)
    result = image.copy()
    result.paste(inner, (pad, pad))
    result.paste(inner.crop((0, 0, 1, inner.height)).resize((pad, inner.height)), (0, pad))
    result.paste(inner.crop((inner.width-1, 0, inner.width, inner.height)).resize((pad, inner.height)), (width-pad, pad))
    result.paste(result.crop((0, pad, width, pad+1)).resize((width, pad)), (0, 0))
    result.paste(result.crop((0, height-pad-1, width, height-pad)).resize((width, pad)), (0, height-pad))
    return result


def equipment_maps(rgb_color, size, slot):
    """Small original gear tiles with distinct woven, hide and coated surfaces."""
    broad = grain(size, 7300+slot, 3.2)
    fine = grain(size, 6300+slot, .35)
    yy, xx = np.mgrid[:size[1], :size[0]]
    u, v = xx/max(1, size[0]-1), yy/max(1, size[1]-1)
    edge = np.maximum(abs(u-.5), abs(v-.5))*2
    paint = np.broadcast_to(np.array(rgb_color), (size[1], size[0], 3)).astype(np.float32).copy()
    paint += broad[:, :, None]*2.4+fine[:, :, None]*.7
    height = fine*.002
    gloss = np.full((size[1], size[0]), 55, dtype=np.float32)
    metallic = np.zeros_like(gloss)
    if slot in (0, 1, 2, 6, 7, 18, 20):
        # Warp/weft are textile structure, not decorative diagonal dirt bands.
        weave = ((xx%3 == 0).astype(float)-(yy%3 == 0).astype(float))
        seam = np.exp(-((u-.105)/.016)**2)+np.exp(-((u-.895)/.016)**2)
        stitch = seam*((yy%5) < 3)
        paint += (weave*.95+stitch*8)[:, :, None]
        height += weave*.007+stitch*.017
        gloss[:] = 35
    elif slot in (3, 8):
        # Boot collars, straps and pouch trim use pebbled hide, not bare steel.
        pores = np.clip(fine-.25, 0, 1.8)
        crease = np.maximum(broad-.6, 0)
        wear = np.maximum(edge-.78, 0)*30
        paint += (wear-pores*2.5-crease*3.5)[:, :, None]
        height += pores*.008-crease*.011
        gloss = 91+pores*8-crease*19+wear*.3
    elif slot in (4, 5, 23):
        abrasion = np.maximum(edge-.72, 0)**1.5*50
        scratch_image = Image.new("L", size)
        draw = ImageDraw.Draw(scratch_image)
        generator = np.random.default_rng(8300+slot)
        for _ in range(21):
            x, y = generator.integers(2, min(size)-2, size=2)
            length = int(generator.integers(2, 10))
            draw.line((int(x), int(y), min(size[0]-1, int(x)+length), max(0, int(y)-2)), fill=128, width=1)
        scratches = pixels(scratch_image)/255
        paint += (abrasion+scratches*14)[:, :, None]
        height -= scratches*.020
        # Armor is painted/coated; metallic only where coating has worn away.
        metallic = np.clip(22+abrasion*8+scratches*130, 0, 205)
        if slot == 23:
            metallic = np.clip(155+abrasion*2, 0, 225)
        gloss = 103+abrasion*.9+scratches*34
    elif slot in (11, 12, 13, 14, 15, 21):
        folds = np.maximum(broad-.45, 0)
        paint += broad[:, :, None]*np.array([6, 4, 2])
        height -= folds*.017
        if slot in (14, 15):
            paint -= folds[:, :, None]*np.array([8, 8, 6])
        gloss = 61+folds*18 if slot != 21 else 124+broad*10
    elif slot == 17:
        # Same dark brown as the native hair cards and the fitted scalp underlay.
        strands = blur(fine, .6)+blur(broad, 1.3)
        paint = np.array([42, 28, 22])*(.78+strands*.055)[:, :, None]
        height = strands*.007
        gloss[:] = 76
    elif slot == 19:
        paint += broad[:, :, None]*np.array([5, 4, 2])
        height = broad*.008+fine*.001
        gloss[:] = 65
    elif slot in (9, 10):
        height = fine*.0015
        gloss[:] = 82
    return rgb(paint), normal_from_height(height, 9), surface(metallic, gloss)
