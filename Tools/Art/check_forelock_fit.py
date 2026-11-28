"""Check Eva's forelock against the reduced anatomical head, before FBX export.

Requires the same CC0 MakeHuman inputs as build_characters.py. This measures
mesh clearance, not native Unity deformation or visual acceptance.
"""
import argparse
import json
from pathlib import Path
import sys
import tempfile
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument('--makehuman-data', type=Path, required=True)
parser.add_argument('--skin-pack', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
sys.path.insert(0, str(Path(__file__).resolve().parent))
with tempfile.TemporaryDirectory() as output:
    sys.argv = [__file__, '--', output, '--makehuman-data', str(args.makehuman_data),
                '--skin-pack', str(args.skin_pack)]
    from build_characters import Character, CAST
    from survivor_clothing import append_groomed_locks
    bpy.ops.wm.read_factory_settings(use_empty=True)
    actor = Character(next(spec for spec in CAST if spec[0] == 'Eva'))
    actor.skeleton()
    actor.body.append()
    faces = [face for face, (slot, _) in zip(actor.faces, actor.tiles)
             if slot == actor.body.skin_slot and all(actor.vertices[i][2] > actor.h * .88 for i in face)]
    head = BVHTree.FromPolygons(actor.vertices, faces, all_triangles=False)
    first_vertex, first_face = len(actor.vertices), len(actor.faces)
    append_groomed_locks(actor)
    points = [Vector(p) for p in actor.vertices]
    probes = list(points[first_vertex:])
    for face in actor.faces[first_face:]:
        for triangle in ((face[0], face[i], face[i + 1]) for i in range(1, len(face) - 1)):
            a, b, c = (points[i] for i in triangle)
            for j in range(9):
                for k in range(9 - j):
                    u, v = j / 8, k / 8
                    probes.append(a * (1 - u - v) + b * u + c * v)
    gaps = []
    for point in probes:
        hit, _, _, _ = head.ray_cast(Vector((point.x, .60, point.z)), Vector((0, -1, 0)), 1.2)
        assert hit is not None, ('Forelock misses head', tuple(point))
        gaps.append(point.y - hit.y)
    result = dict(vertices=len(points) - first_vertex, faces=len(actor.faces) - first_face,
                  probes=len(probes), minimum_gap_mm=min(gaps) * 1000, maximum_gap_mm=max(gaps) * 1000)
    print('FORELOCK FIT', json.dumps(result), flush=True)
    assert .0005 < min(gaps) and max(gaps) < .006, result
    print('PASS fitted vertices and triangle interiors, reduced head clearance; not Unity.', flush=True)
