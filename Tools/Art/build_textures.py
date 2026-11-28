"""Build the fixed 2048 character atlas from existing CC0 source UV textures.

Use --characters-only --output-dir /tmp/review to compare maps without touching
shipped resources, unrelated highway textures, particles or accepted weapon art.
Requires NumPy and Pillow; Blender is not needed.
"""
from argparse import ArgumentParser
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile
import math
import numpy as np
from PIL import Image, ImageDraw
from character_atlas import SIZE, SLOTS
from character_surface_detail import (equipment_maps, garment_maps, hair_maps,
                                      inset, normal_from_height, normalize_normal, pixels, read_image,
                                      relief, rgb, skin_maps, surface)


OUT = Path(__file__).resolve().parents[2] / "Assets/Resources/ProductionArt"
PAL = [
    (67, 69, 53), (114, 41, 36), (49, 57, 63), (35, 39, 43), (64, 75, 84), (189, 103, 38),
    (106, 91, 108), (51, 87, 97), (104, 82, 59), (158, 110, 84), (165, 119, 102), (107, 110, 82),
    (124, 119, 79), (115, 82, 77), (101, 64, 68), (93, 47, 44), (179, 172, 145), (42, 28, 22),
    (74, 70, 53), (148, 135, 110), (41, 38, 31), (143, 193, 45), (235, 63, 37), (80, 76, 70),
]
SKINS = [
    ("middleage_caucasian_male", (.94, .90, .85)),
    ("young_caucasian_female", (1.00, .94, .89)),
    ("old_caucasian_male", (.73, .78, .66)),
    ("young_asian_male", (.73, .76, .66)),
    ("middleage_african_male", (.63, .66, .60)),
    ("old_asian_male", (.64, .92, .45)),
    ("old_caucasian_female", (.68, .72, .69)),
    ("middleage_caucasian_male", (.59, .45, .42)),
]
COLORS = {"Max": (74, 80, 58), "Eva": (117, 32, 29), "Walker": (98, 85, 70),
          "Runner": (86, 43, 35), "Toxic": (57, 69, 43), "Crawler": (65, 66, 83)}


def build_characters(skin_pack, output_dir):
    from survivor_clothing import WARDROBE
    atlas = Image.new("RGB", (SIZE, SIZE), "#252a2c")
    normals = Image.new("RGB", atlas.size, (128, 128, 255))
    packed = Image.new("RGBA", atlas.size, (0, 0, 0, 42))

    def paste(slot, maps):
        origin = SLOTS[slot][:2]
        for index, (target, image) in enumerate(zip((atlas, normals, packed), maps)):
            fitted = inset(image, 2 if slot < 24 else 3)
            # Interpolation of tangent vectors changes their lengths, even if
            # the source was normalized. Normalize after the UV inset resize.
            target.paste(normalize_normal(fitted) if index == 1 else fitted, origin)

    for slot, color in enumerate(PAL):
        paste(slot, equipment_maps(color, SLOTS[slot][2:], slot))
    with ZipFile(skin_pack) as pack:
        names = pack.namelist()
        for index, (folder, tint) in enumerate(SKINS):
            slot = 24+index
            name, = (name for name in names if name.startswith("skins/"+folder+"/") and name.endswith(".png"))
            source = Image.open(BytesIO(pack.read(name))).convert("RGB")
            paste(slot, skin_maps(source, SLOTS[slot][2:], tint, slot))
        for slot, name in ((32, "shoes02"), (33, "shoes05")):
            size = SLOTS[slot][2:]
            source = read_image(pack, f"clothes/{name}/{name}_diffuse.png", size)
            gray = pixels(source.convert("L"))/255
            # Preserve laces, stitched panels and creased toe boxes in the
            # native footwear UV. Dark leather and rubber retain separate gloss.
            paint = np.array([17, 19, 20])+gray[:, :, None]*np.array([49, 50, 51])
            gloss = np.clip(59+gray*53, 52, 112)
            paste(slot, (rgb(paint), normal_from_height(relief(source, .17), 9), surface(0, gloss)))
        for slot, name in ((34, "short01"), (35, "ponytail01")):
            paste(slot, hair_maps(pack, name, SLOTS[slot][2:]))
        for slot, name in ((36, "brown"), (37, "bluegreen"), (38, "brownlight")):
            size = SLOTS[slot][2:]
            source = read_image(pack, f"eyes/materials/{name}_eye.png", size)
            paste(slot, (source, Image.new("RGB", size, (128, 128, 255)),
                         Image.new("RGBA", size, (0, 0, 0, 210))))
        for index, (name, (garment, slot)) in enumerate(WARDROBE.items()):
            paste(slot, garment_maps(pack, garment, SLOTS[slot][2:], COLORS[name],
                                     name not in ("Max", "Eva"), 3200+index))
    # Original sewn insignia in the existing regions; no new material or art.
    for slot in (46, 47, 48):
        size = SLOTS[slot][2:]
        badge = Image.new("RGB", size, (32, 37, 39))
        draw = ImageDraw.Draw(badge)
        ink = (218, 116, 47) if slot == 46 else (202, 207, 196)
        draw.rounded_rectangle((8, 8, 119, 119), radius=21, outline=ink, width=4)
        if slot == 46:
            draw.ellipse((45, 45, 83, 83), outline=ink, width=6)
            for start in (30, 150, 270):
                cx = 64+24*math.cos(math.radians(start))
                cy = 64+24*math.sin(math.radians(start))
                draw.arc((cx-21, cy-21, cx+21, cy+21), start-10, start+260, fill=ink, width=7)
        elif slot == 47:
            draw.line((29, 86, 64, 38, 99, 86), fill=ink, width=9)
            draw.line((49, 86, 64, 65, 79, 86), fill=ink, width=6)
        else:
            draw.polygon((35, 30, 93, 30, 88, 80, 64, 103, 40, 80), outline=ink, width=5)
            draw.line((48, 62, 60, 75, 82, 49), fill=ink, width=7)
        paste(slot, (badge, normal_from_height(relief(badge, .12), 3),
                     Image.new("RGBA", size, (0, 0, 0, 38))))
    emission = Image.new("RGB", atlas.size)
    width, height = SLOTS[22][2:]
    yy, xx = np.mgrid[:height, :width]
    # The boss emissive fissures already own their geometry/UV. Keep the heat
    # across that region without adding glow to any infected skin or clothing.
    heat = .30+.70*np.exp(-((xx-width*.5)/(width*.4))**2-((yy-height*.5)/(height*.4))**2)
    emission.paste(inset(rgb(heat[:, :, None]*np.array([252, 174, 92])), 2), SLOTS[22][:2])
    output_dir.mkdir(parents=True, exist_ok=True)
    for suffix, image in (("", atlas), ("_normal", normals), ("_surface", packed), ("_emission", emission)):
        image.save(output_dir/("CharacterAtlas"+suffix+".png"), optimize=True)


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--skin-pack", type=Path, required=True)
    parser.add_argument("--characters-only", action="store_true")
    parser.add_argument("--output-dir", type=Path, help="Isolated character-map destination; requires --characters-only")
    args = parser.parse_args()
    if args.output_dir is not None and not args.characters_only:
        parser.error("--output-dir requires --characters-only; the separate highway builder owns its output paths")
    output_dir = args.output_dir if args.output_dir is not None else OUT
    build_characters(args.skin_pack, output_dir)
    if args.characters_only:
        return
    from build_highway import build as build_highway
    build_highway()
    # Single feathered mask shared by flame, sparks and smoke particles.
    n = 64
    yy, xx = np.mgrid[:n, :n]
    u, v = (xx+.5-n*.5)/(n*.5), (yy+.5-n*.5)/(n*.5)
    radius = np.hypot(u, v)+.075*np.cos(9*u+5*v)*np.sin(7*v)
    alpha = np.clip(1-radius, 0, 1)**1.8
    particle = np.full((n, n, 4), 255, dtype=np.uint8)
    particle[:, :, 3] = np.uint8(255*alpha)
    Image.fromarray(particle).save(output_dir/"ParticleSoft.png", optimize=True)


if __name__ == "__main__":
    main()
