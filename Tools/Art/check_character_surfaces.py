"""Inspect generated character maps without claiming a Unity visual test.

Examples:
  python -B Tools/Art/check_character_surfaces.py --assets /tmp/maps --isolated
  python -B Tools/Art/check_character_surfaces.py --assets /tmp/maps \
      --repeat /tmp/maps-repeat --skin-pack /path/to/system-assets.zip
"""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile
import json
import numpy as np
from PIL import Image
from character_atlas import SIZE, SLOTS, box
from character_surface_detail import inset, read_image


NAMES = tuple("CharacterAtlas"+suffix+".png" for suffix in ("", "_normal", "_surface", "_emission"))


def inspect(assets, repeat=None, skin_pack=None, isolated=False):
    if isolated:
        assert {path.name for path in assets.iterdir()} == set(NAMES), "Character-only output touched unrelated assets"
    images = [Image.open(assets/name) for name in NAMES]
    assert all(image.size == (2048, 2048) for image in images)
    assert [image.mode for image in images] == ["RGB", "RGB", "RGBA", "RGB"]
    assert SIZE == 2048
    assert SLOTS[24] == (0, 0, 768, 768) and SLOTS[25] == (768, 0, 768, 768)
    occupied = np.zeros((SIZE, SIZE), dtype=bool)
    for slot, (x, y, width, height) in SLOTS.items():
        assert 0 <= x < x+width <= SIZE and 0 <= y < y+height <= SIZE, slot
        assert not occupied[y:y+height, x:x+width].any(), f"Overlapping slot {slot}"
        occupied[y:y+height, x:x+width] = True
    data = [np.asarray(image) for image in images]
    # Source UV endpoints must sample the extended border, not an adjacent
    # island or an unrelated texel 3px inside the unscaled source.
    for slot, (x, y, width, height) in SLOTS.items():
        pad = 2 if slot < 24 else 3
        for array in data[:3]:
            tile = array[y:y+height, x:x+width]
            assert np.array_equal(tile[:, 0], tile[:, pad]), (slot, "left gutter")
            assert np.array_equal(tile[:, -1], tile[:, -pad-1]), (slot, "right gutter")
            assert np.array_equal(tile[0], tile[pad]), (slot, "top gutter")
            assert np.array_equal(tile[-1], tile[-pad-1]), (slot, "bottom gutter")
    vectors = data[1].astype(float)/127.5-1
    error = abs(np.linalg.norm(vectors, axis=-1)-1)
    assert float(error.max()) < .018, f"Nonunit normal vector error {error.max()}"
    assert (vectors[:, :, 2] > .19).all(), "Invalid/inverted tangent normal background"
    assert images[3].getbbox() == box(22), "Boss emission leaked into other surfaces"
    packed = data[2]
    assert not packed[:, :, 1:3].any(), "Surface channels must be metallic R / smoothness A"

    def tile(array, slot):
        x, y, width, height = SLOTS[slot]
        return array[y:y+height, x:x+width]

    dielectric = tuple(range(9, 22))+tuple(range(24, 39))+tuple(range(40, 49))+(0, 1, 2, 3, 6, 7, 8)
    for slot in dielectric:
        if slot in SLOTS:
            assert not tile(packed, slot)[:, :, 0].any(), f"Metallic skin/cloth/hair: slot {slot}"
    smoothness = {slot: float(np.median(tile(packed, slot)[:, :, 3])) for slot in SLOTS}
    assert smoothness[40] < smoothness[24] < smoothness[36]
    assert smoothness[41] < smoothness[3] < smoothness[36]
    assert smoothness[0] < smoothness[19] < smoothness[21]
    assert smoothness[36] > 180 and smoothness[40] < 60
    assert tile(packed, 4)[:, :, 0].max() > 100, "Bare armor abrasion lost metal mask"
    assert np.median(tile(packed, 23)[:, :, 0]) > 100, "Bare equipment metal lost mask"
    for slot in (34, 35):
        hair = tile(data[0], slot)
        ceiling = 165 if slot == 35 else 85
        assert hair.max() < ceiling and np.percentile(hair.mean(axis=-1), 1) > 7, "Hair contains pale matte/black void"
    eva_hair = tile(data[0], 35).astype(float)
    max_hair = tile(data[0], 34).astype(float)
    assert eva_hair[:, :, 0].mean() > max_hair[:, :, 0].mean()*1.5, "Eva chestnut lost visual distinction from Max"
    assert np.median(eva_hair[:, :, 0]-eva_hair[:, :, 2]) > 20, "Eva chestnut lost warm pigment"
    if skin_pack:
        with ZipFile(skin_pack) as pack:
            for slot, name in ((34, "short01"), (35, "ponytail01")):
                source = read_image(pack, f"hair/{name}/{name}_diffuse.png", SLOTS[slot][2:], "RGBA")
                alpha = np.asarray(inset(source))[:, :, 3]
                opaque_hair = tile(data[0], slot)[alpha < 4]
                assert len(opaque_hair) > 100
                ceiling = 90 if slot == 35 else 50
                assert opaque_hair.max() < ceiling and opaque_hair.mean() > 15, "Empty hair cards do not match scalp pigment"
    digests = {name: sha256((assets/name).read_bytes()).hexdigest() for name in NAMES}
    if repeat:
        for name, expected in digests.items():
            assert sha256((repeat/name).read_bytes()).hexdigest() == expected, f"Nondeterministic map {name}"
    return {"result": "PASS", "normal_max_unit_error": round(float(error.max()), 6),
            "smoothness_medians": {str(slot): smoothness[slot] for slot in (0, 3, 4, 19, 21, 24, 26, 36, 40, 41)},
            "deterministic_repeat": repeat is not None, "sha256": digests,
            "scope": "Python asset checks only; Unity import/render/device acceptance not tested"}


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--repeat", type=Path)
    parser.add_argument("--skin-pack", type=Path)
    parser.add_argument("--isolated", action="store_true")
    args = parser.parse_args()
    print(json.dumps(inspect(args.assets, args.repeat, args.skin_pack, args.isolated), indent=2))


if __name__ == "__main__":
    main()
