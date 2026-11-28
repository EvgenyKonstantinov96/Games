"""Check both MakeHuman proxy binding formats against the actual CC0 source pack.

blender -b --python-exit-code 1 -P Tools/Art/check_proxy_bindings.py -- /path/to/system-assets.zip
"""
import sys
from pathlib import Path
from zipfile import ZipFile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from makehuman_body import read_obj
from survivor_clothing import WARDROBE, read_proxy

pack_path = Path(sys.argv[sys.argv.index("--") + 1])
proxies = [("eyes", "low-poly", 1), ("hair", "short01", 3),
           ("hair", "ponytail01", 3), ("teeth", "teeth_base", 3)]
proxies += [("clothes", name, 3) for name in sorted({name for name, _ in WARDROBE.values()})]
with ZipFile(pack_path) as pack:
    for family, name, references in proxies:
        mapping, axes, _ = read_proxy(pack, name, family)
        points, uv, faces, _ = read_obj(pack.read(f"{family}/{name}/{name}.obj").decode().splitlines())
        label = (family, name)
        assert points and len(mapping) == len(points), (label, "binding count", len(mapping), len(points))
        assert set(axes) == {0, 1, 2} and all(reference > 0 for _, _, reference in axes.values()), label
        assert all(len(ids) == len(weights) == references and min(ids) >= 0 and
                   abs(sum(weights)-1) < .0001 for ids, weights, _ in mapping), (label, "bindings")
        if references == 1:
            assert all(weights == [1.] and offset.length == 0 for _, weights, offset in mapping), label
        assert faces and all(len(face) >= 3 and all(0 <= i < len(points) and 0 <= j < len(uv)
                                                   for i, j in face) for face in faces), (label, "indices")
        print(f"PROXY {family}/{name}: {len(mapping)} vertices, {references} reference(s), "
              f"{len(faces)} valid faces", flush=True)
