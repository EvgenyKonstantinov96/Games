"""Surface details sampled from the CC0 anatomy, with the original skinning.

These are additions to a real fitted body, not replacement primitive bodies.
The small shell patches keep their source topology and the narrow scars are
projected onto source triangles rather than placed at guessed chest depths.
"""
from collections import Counter, defaultdict
import math

from mathutils import Vector


def mixed_weights(entries):
    values = defaultdict(float)
    for weights, amount in entries:
        for bone, weight in weights.items():
            values[bone] += weight * amount
    ranked = sorted(((bone, value) for bone, value in values.items() if value > .00001),
                    key=lambda item: item[1], reverse=True)[:4]
    total = sum(value for _, value in ranked)
    assert total > 0, 'Surface sample has no deforming weights'
    return {bone: value / total for bone, value in ranked}


class AnatomicalSurface:
    def __init__(self, actor):
        self.actor = actor
        body = actor.body
        self.points = body.geometry
        self.torso_widths = {}
        self.normals = {index: Vector((0, 0, 0)) for index in self.points}
        self.triangles = []
        for face in body.faces:
            ids = [index for index, _ in face]
            a = self.points[ids[0]]
            for i in range(1, len(ids) - 1):
                indices = (ids[0], ids[i], ids[i + 1])
                b, c = (self.points[index] for index in indices[1:])
                normal = (b - a).cross(c - a)
                for index in indices:
                    self.normals[index] += normal
                self.triangles.append((indices, normal.normalized()))
        for normal in self.normals.values():
            normal.normalize()
        # The source surface has thousands of triangles. A fixed spatial grid
        # makes repeated projected seams cheap without a Blender raycast mesh.
        self.projected = defaultdict(list)
        for indices, normal in self.triangles:
            points = [self.points[index] for index in indices]
            for x in range(math.floor(min(p.x for p in points) / .06),
                           math.floor(max(p.x for p in points) / .06) + 1):
                for z in range(math.floor(min(p.z for p in points) / .06),
                               math.floor(max(p.z for p in points) / .06) + 1):
                    self.projected[x, z].append((indices, normal))

    @staticmethod
    def torso_weight(weights):
        return sum(weights.get(bone, 0.) for bone in ('Chest', 'Spine', 'Pelvis'))

    def torso_halfwidth(self, z):
        """Measure the chest cross-section without admitting adjacent upper arms."""
        key = round(z, 5)
        if key in self.torso_widths:
            return self.torso_widths[key]
        widths = []
        for indices, normal in self.triangles:
            if normal.y < .08:
                continue
            amounts = [self.torso_weight(self.actor.body.bind_weights[i]) for i in indices]
            if min(amounts) < .50 or sum(amounts) / 3 < .72:
                continue
            points = [self.points[i] for i in indices]
            for a, b in zip(points, points[1:] + points[:1]):
                if (a.z - z) * (b.z - z) > 0 or abs(b.z - a.z) < 1e-8:
                    continue
                p = a.lerp(b, (z - a.z) / (b.z - a.z))
                if p.y > .025:
                    widths.append(abs(p.x))
        assert widths, ('No torso cross-section', self.actor.name, z)
        self.torso_widths[key] = max(widths)
        return self.torso_widths[key]

    def sample(self, x, z, front=True, torso=False):
        hits = []
        for indices, normal in self.projected[math.floor(x / .06), math.floor(z / .06)]:
            a, b, c = (self.points[index] for index in indices)
            determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z)
            if abs(determinant) < 1e-10:
                continue
            u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / determinant
            v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / determinant
            amounts = (u, v, 1 - u - v)
            if min(amounts) < -.0001:
                continue
            point = sum((p * t for p, t in zip((a, b, c), amounts)), Vector())
            n = sum((self.normals[i] * t for i, t in zip(indices, amounts)), Vector()).normalized()
            weights = mixed_weights((self.actor.body.bind_weights[i], t) for i, t in zip(indices, amounts))
            if torso and self.torso_weight(weights) < .65:
                continue
            hits.append((point, n, weights))
        if hits:
            return (max if front else min)(hits, key=lambda entry: entry[0].y)
        # Outline endpoints can fall just outside the projected mesh. Snap to
        # the nearest source vertex, retaining that vertex's real skin weights.
        candidates = self.points if not torso else [i for i in self.points
                         if self.torso_weight(self.actor.body.bind_weights[i]) >= .75]
        index = min(candidates, key=lambda i: (self.points[i].x - x) ** 2 + (self.points[i].z - z) ** 2)
        return (self.points[index].copy(), self.normals[index].copy(),
                mixed_weights(((self.actor.body.bind_weights[index], 1.),)))


def surface(actor):
    if not hasattr(actor, '_infected_surface'):
        actor._infected_surface = AnatomicalSurface(actor)
    return actor._infected_surface


def clamp_reduced_uvs(actor, first_face):
    """Decimate can extrapolate a seam UV past a source edge by a few percent."""
    for i in range(first_face, len(actor.tiles)):
        tile, coords = actor.tiles[i]
        actor.tiles[i] = (tile, tuple((max(0., min(1., u)), max(0., min(1., v)))
                                     for u, v in coords))


def shell_patch(actor, name, center, radii, tile=None, lift=.004, bulge=.018,
                facing=(0, 1, 0), ratio=.42, irregularity=.10):
    """Raise a bounded region of the actual anatomy with a rolled organic rim."""
    from makehuman_body import append_reduced
    field = surface(actor)
    center, radii, facing = Vector(center), Vector(radii), Vector(facing).normalized()

    def distance(point):
        d = point - center
        angle = math.atan2(d.z / radii.z, d.x / radii.x)
        edge = 1 + irregularity * (math.sin(angle * 5 + .8) + .4 * math.sin(angle * 9))
        return sum((d[i] / radii[i]) ** 2 for i in range(3)) ** .5 / edge

    chosen = []
    edge_counts = Counter()
    uv_by_id = {}
    for face in actor.body.faces:
        ids = tuple(index for index, _ in face)
        point = sum((field.points[i] for i in ids), Vector()) / len(ids)
        normal = sum((field.normals[i] for i in ids), Vector()).normalized()
        if distance(point) > 1 or normal.dot(facing) < .04:
            continue
        chosen.append(face)
        for index, uv_index in face:
            uv_by_id[index] = actor.body.uv[uv_index]
        for a, b in zip(ids, ids[1:] + ids[:1]):
            edge_counts[tuple(sorted((a, b)))] += 1
    if not chosen:
        return 0
    used = sorted(uv_by_id)
    remap = {index: i for i, index in enumerate(used)}
    boundary = {i for edge, count in edge_counts.items() if count == 1 for i in edge}
    points, weights = [], []
    for index in used:
        p = field.points[index]
        t = max(0, 1 - min(1, distance(p)) ** 2)
        # Irregular low relief is strongest in the center; the contour returns
        # to the skin so the growth reads as fused tissue rather than a bead.
        ridges = 1 + .13 * math.sin(p.x * 66 + p.z * 31) * math.cos(p.z * 54 - p.y * 33)
        height = lift + bulge * t * t * ridges
        if index in boundary:
            height = lift
        points.append(p + field.normals[index] * height)
        weights.append(actor.body.bind_weights[index])
    geometry, tiles = [], []
    for face in chosen:
        ids = tuple(index for index, _ in face)
        if tile is None:
            coords = tuple(actor.body.uv[uv_index] for _, uv_index in face)
        else:
            coords = tuple((.5 + (field.points[i].x - center.x) / (radii.x * 2.2),
                            .5 + (field.points[i].z - center.z) / (radii.z * 2.2)) for i in ids)
            coords = tuple((max(.025, min(.975, u)), max(.025, min(.975, v))) for u, v in coords)
        geometry.append((tuple(remap[i] for i in ids), coords))
        tiles.append(actor.body.skin_slot if tile is None else tile)
    # Close each cut edge down to the unraised skin; no exposed wafer edges.
    lower = {}
    for index in sorted(boundary):
        lower[index] = len(points)
        points.append(field.points[index] + field.normals[index] * .001)
        weights.append(actor.body.bind_weights[index])
    for face in chosen:
        ids = tuple(index for index, _ in face)
        for a, b in zip(ids, ids[1:] + ids[:1]):
            if edge_counts[tuple(sorted((a, b)))] == 1:
                if tile is None:
                    native_uv = {index: actor.body.uv[uv_index] for index, uv_index in face}
                    coords = (native_uv[b], native_uv[a], native_uv[a], native_uv[b])
                else:
                    coords = ((1, 1), (0, 1), (0, 0), (1, 0))
                geometry.append(((remap[b], remap[a], lower[a], lower[b]),
                                 coords))
                tiles.append(tile if tile is not None else actor.body.skin_slot)
    before = len(actor.faces)
    append_reduced(actor, name, points, geometry, tiles, weights, ratio)
    clamp_reduced_uvs(actor, before)
    return len(actor.faces) - before


def projected_ribbon(actor, path, width, tile, front=True, offset=.004, rim=False, torso=False):
    """A tapered skin crease, wound or fissure hugging sampled anatomical triangles."""
    field = surface(actor)
    first = len(actor.vertices)
    count = len(path)
    for j, (x, z) in enumerate(path):
        previous = Vector(path[max(0, j - 1)])
        following = Vector(path[min(count - 1, j + 1)])
        tangent = (following - previous).normalized()
        sideways = Vector((tangent.y, -tangent.x))
        taper = .20 + .80 * math.sin(math.pi * j / (count - 1)) ** .5
        for side in (-1, 1):
            q = Vector((x, z)) + sideways * (side * width * taper)
            point, normal, weights = field.sample(q.x, q.y, front, torso)
            if torso:
                weights = mixed_weights((({bone: weight for bone, weight in weights.items()
                                          if bone in ('Chest', 'Spine', 'Pelvis')}, 1.),))
            actor.vertices.append(tuple(point + normal * offset))
            actor.weights.append(weights)
    for j in range(count - 1):
        a = first + j * 2
        face = (a, a + 1, a + 3, a + 2)
        actor.faces.append(face[::-1] if front else face)
        uv = ((0, j / (count - 1)), (1, j / (count - 1)),
              (1, (j + 1) / (count - 1)), (0, (j + 1) / (count - 1)))
        actor.tiles.append((tile, uv[::-1] if front else uv))
    if rim:
        for side in (-1, 1):
            shifted = []
            for j, point in enumerate(path):
                previous = Vector(path[max(0, j - 1)])
                following = Vector(path[min(count - 1, j + 1)])
                tangent = (following - previous).normalized()
                shifted.append(tuple(Vector(point) + Vector((tangent.y, -tangent.x)) * side * width * .94))
            projected_ribbon(actor, shifted, width * .20, 15, front, offset + .001, torso=torso)


def distress_garment(actor):
    """Pull and finish the actual garment tear boundaries, preserving body/rig."""
    edges = Counter()
    directions = {}
    for face, (tile, _) in zip(actor.faces, actor.tiles):
        if tile not in (42, 43, 44, 45):
            continue
        for a, b in zip(face, face[1:] + face[:1]):
            edge = tuple(sorted((a, b)))
            edges[edge] += 1
            directions[edge] = (a, b)
    boundary = []
    for edge, count in edges.items():
        if count != 1:
            continue
        a, b = directions[edge]
        p, q = Vector(actor.vertices[a]), Vector(actor.vertices[b])
        middle = (p + q) * .5
        # Finish torso tears, the shirt hem and the exposed shortened sleeves;
        # leave the original neck, waistband and ankle openings unobstructed.
        if not actor.h * .55 < middle.z < actor.h * .82 or abs(middle.y) < .04:
            continue
        boundary.append((a, b))
    for index in {i for edge in boundary for i in edge}:
        p = Vector(actor.vertices[index])
        p.z += .006 * math.sin(p.x * 91 + p.z * 57)
        p.y += .004 * math.sin(p.x * 58 - p.z * 81)
        actor.vertices[index] = tuple(p)
    # An irregular folded edge uses one narrow quad per source edge. Frayed
    # thread ends are sparse and short, not large freestanding triangle flaps.
    for j, (a, b) in enumerate(boundary):
        p, q = Vector(actor.vertices[a]), Vector(actor.vertices[b])
        first = len(actor.vertices)
        actor.vertices.extend((tuple(p), tuple(q), tuple(q + Vector((0, .002, -.004))),
                               tuple(p + Vector((0, .002, -.004)))))
        actor.weights.extend((dict(actor.weights[a]), dict(actor.weights[b]),
                              dict(actor.weights[b]), dict(actor.weights[a])))
        actor.faces.append((first, first + 1, first + 2, first + 3))
        actor.tiles.append((18, ((0, 1), (1, 1), (1, 0), (0, 0))))
        if j % 7 == 0 and (p - q).length > .012:
            weights = actor.weights[a]
            bone = max(weights, key=weights.get)
            actor.surface(p, p + Vector((.003, .002, -.012 - .005 * (j % 3))),
                          [(0, .0012, .0012), (1, .0005, .0005)], 18, bone, 4)
