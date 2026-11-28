"""Authored infected clothing, security armor and surface-bound mutation detail.

Every class keeps the fitted MakeHuman body and the existing deforming rig.
Additional flesh/keratin is sampled from that anatomy; primitive decorations do
not stand in for a body, a shoulder or a muscle.
"""
import math
from mathutils import Vector
from infected_surface import clamp_reduced_uvs, distress_garment, mixed_weights, projected_ribbon, shell_patch, surface


def fitted_bib(actor, width, bottom, top, tile, stand_off=0.):
    """Thick garment/vest panel fitted to source chest triangles, including edges."""
    field = surface(actor)
    first = len(actor.vertices)
    nu, nv = 10, 7
    torso_only = actor.name == 'Armored'
    for layer in range(2):
        for j in range(nv + 1):
            t = j / nv
            for i in range(nu + 1):
                u = i / nu * 2 - 1
                # Neck relief and clipped lower corners keep a real torso
                # silhouette, rather than a flat rectangle in front of it.
                span = width * (1 - .13 * t - .08 * (1 - t) ** 5)
                z = actor.h * (bottom + (top - bottom) * t)
                z -= .027 * (1 - u * u) * t ** 7
                z += .014 * abs(u) ** 6 * (1 - t) ** 6
                if torso_only:
                    span = min(span, field.torso_halfwidth(z) * .88)
                x = u * span
                p, normal, weights = field.sample(x, z, torso=torso_only)
                if torso_only:
                    weights = mixed_weights((({bone: weight for bone, weight in weights.items()
                                               if bone in ('Chest', 'Spine', 'Pelvis')}, 1.),))
                p += normal * (stand_off + ((.031 + .010 * math.sin(math.pi * t) * (1 - u * u)) if layer == 0 else .012))
                actor.vertices.append(tuple(p))
                actor.weights.append(weights)
    count = (nu + 1) * (nv + 1)
    for layer in range(2):
        for j in range(nv):
            for i in range(nu):
                a = first + layer * count + j * (nu + 1) + i
                face = (a, a + 1, a + nu + 2, a + nu + 1)
                coords = ((i / nu, j / nv), ((i + 1) / nu, j / nv),
                          ((i + 1) / nu, (j + 1) / nv), (i / nu, (j + 1) / nv))
                actor.faces.append(face[::-1] if layer == 0 else face)
                actor.tiles.append((tile, coords[::-1] if layer == 0 else coords))
    border = list(range(nu + 1)) + [j * (nu + 1) + nu for j in range(1, nv + 1)]
    border += [nv * (nu + 1) + i for i in range(nu - 1, -1, -1)]
    border += [j * (nu + 1) for j in range(nv - 1, 0, -1)]
    for a, b in zip(border, border[1:] + border[:1]):
        actor.faces.append((first + a, first + b, first + b + count, first + a + count))
        actor.tiles.append((3, ((0, 0), (1, 0), (1, 1), (0, 1))))


def tank_harness(actor):
    h = actor.h
    fitted_bib(actor, .205, .555, .711, 20)
    field = surface(actor)
    for sign, upper in ((-1, .832), (1, .766)):
        path = [(sign * (.132 + .020 * t), h * (.58 + (upper - .58) * t)) for t in (0, .26, .56, .82, 1)]
        projected_ribbon(actor, path, .021, 20, offset=.029)
        projected_ribbon(actor, [(x + sign * .014, z) for x, z in path], .0017, 18, offset=.032)
        p, normal, _ = field.sample(sign * .143, h * .701)
        actor.panel(p + normal * .042, (.050, .014, .061), 4, 'Chest')
    # A distinct bib pocket with thick stitched opening, not a floating full vest.
    z = h * .649
    p, normal, _ = field.sample(-.032, z)
    actor.panel(p + normal * .051, (.133, .013, .091), 20, 'Chest', -.06)
    projected_ribbon(actor, [(-.094, z + .046), (-.03, z + .047), (.032, z + .044)],
                     .003, 18, offset=.063)


def _scar(actor, x, z, length=.095, width=.006, diagonal=.03, front=True):
    path = [(x - diagonal * .45, z + length * .50), (x + diagonal * .12, z + length * .24),
            (x - diagonal * .08, z + length * .02), (x + diagonal * .53, z - length * .24),
            (x + diagonal * .40, z - length * .50)]
    projected_ribbon(actor, path, width, 17, front, .004, rim=True)


def _claws(actor):
    for side, _ in (('L', -1), ('R', 1)):
        for digit in range(1, 6):
            key = f'Finger{digit}Tip{side}'
            a, b, _ = actor.bones[key]
            direction = (b - a).normalized()
            reach = .052 if actor.name == 'Boss' else .028
            actor.surface(b - direction * .007, b + direction * reach + Vector((0, .008, -.006)),
                          [(0, .009, .007), (.28, .008, .006), (.76, .004, .003), (1, .0007, .0007)],
                          19, key, 8)


def _curve_spur(actor, start, normal, length, width, bone, lean=0):
    """An irregular curved keratin tip rooted in the fitted carapace surface."""
    normal = Vector(normal).normalized()
    sideways = normal.cross(Vector((0, 0, 1)))
    if sideways.length < .1:
        sideways = Vector((1, 0, 0))
    sideways.normalize()
    rings = []
    for t, scale in ((0, 1), (.15, .96), (.48, .68), (.76, .35), (1, .015)):
        point = start + normal * length * t + Vector((lean * t * t, -.032 * t * t, .027 * t * t))
        rings.append((point, width * scale, width * .72 * scale, {bone: 1.}))
    actor.loft(rings, [14, 15, 19, 19], 9, .04, sideways)


def append_fitted_helmet(actor):
    from makehuman_body import append_reduced
    body = actor.body
    eye = body.joint('l-eye')
    cutoff = eye.z + .013
    scalp = [i for i, p in body.geometry.items() if p.z > cutoff]
    center = sum((body.geometry[i] for i in scalp), Vector()) / len(scalp)
    points = {i: body.geometry[i] + (body.geometry[i] - center) * .115 + Vector((0, 0, .020)) for i in scalp}
    faces, used = [], set()
    for face in body.faces:
        ids = [i for i, _ in face]
        if all(i in points for i in ids):
            faces.append(ids)
            used.update(ids)
    ordered = sorted(used)
    remap = {index: i for i, index in enumerate(ordered)}
    geometry = [(tuple(remap[i] for i in face),
                 tuple((max(.03, min(.97, .5 + points[i].x * 3)),
                        max(.03, min(.97, .5 + (points[i].z - cutoff) * 3))) for i in face)) for face in faces]
    first_face = len(actor.faces)
    append_reduced(actor, 'Fitted ballistic security helmet', [points[i] for i in ordered], geometry,
                   [4] * len(geometry), [{'Head': 1.} for _ in ordered], .60)
    clamp_reduced_uvs(actor, first_face)
    # Segmented helmet rim has a shallow peak and rounded temple protection.
    # It follows the real forehead surface all the way to the outer brows.
    path = [(-.103, cutoff + .006), (-.074, cutoff - .003), (-.034, cutoff - .005),
            (.012, cutoff - .004), (.059, cutoff - .003), (.102, cutoff + .005)]
    projected_ribbon(actor, path, .009, 3, offset=.035)
    for x in (-.094, .094):
        p, n, _ = surface(actor).sample(x, cutoff + .050)
        actor.panel(p + n * .026, (.023, .009, .019), 23, 'Head')
    # A fitted chin strap follows jaw landmarks instead of crossing empty air.
    mouth = body.joint('mouth')
    for sign in (-1, 1):
        projected_ribbon(actor, [(sign * .103, cutoff), (sign * .096, mouth.z + .021),
                                 (sign * .068, mouth.z - .028)], .006, 3, offset=.009)


def _security_kit(actor):
    h = actor.h
    append_fitted_helmet(actor)
    fitted_bib(actor, .223, .648, .824, 3)
    # Staggered overlapping front plates have beveled edges and webbing between
    # them. Their widths follow the pectoral taper instead of a thin apron.
    for bottom, top, width in ((.735, .807, .207), (.683, .747, .211), (.650, .695, .191)):
        fitted_bib(actor, width, bottom, top, 4, .016)
    for j in range(3):
        z = h * (.668 + j * .047)
        for sign in (-1, 1):
            path = [(sign * .020, z), (sign * .071, z + .001), (sign * .130, z - .003)]
            projected_ribbon(actor, path, .006, 3, offset=.069, torso=True)
    chest = h * .773
    p, n, _ = surface(actor).sample(0, chest)
    actor.panel(p + n * .075, (.155, .009, .056), 48, 'Chest')
    for side, sign in (('L', -1), ('R', 1)):
        shoulder = actor.bones['UpperArm' + side][0]
        elbow = actor.bones['Forearm' + side][0]
        wrist = actor.bones['Hand' + side][0]
        shell_patch(actor, side + ' fitted deltoid armor', shoulder.lerp(elbow, .19),
                    (.135, .15, .15), 4, .012, .020, (sign * .7, .5, .2), .46)
        shell_patch(actor, side + ' shaped forearm guard', elbow.lerp(wrist, .55),
                    (.104, .125, .17), 4, .012, .018, (0, 1, 0), .44)
        # Torso shoulder straps and lower vest buckles remain recognizable at
        # portrait distance and are weighted to the unchanged chest skeleton.
        projected_ribbon(actor, [(sign * .119, h * .824), (sign * .129, h * .788),
                                 (sign * .134, h * .747)], .015, 3, offset=.070, torso=True)
        p, n, _ = surface(actor).sample(sign * .122, h * .681, torso=True)
        actor.panel(p + n * .075, (.044, .014, .036), 23, 'Chest')
    _scar(actor, .053, h * .927, .056, .004, -.018)


def _mutated_boss(actor):
    h = actor.h
    _claws(actor)
    # Continuous anatomical relief covers the scapula, deltoids and pectorals;
    # asymmetry comes from patch contours and depth, not loose balls on skin.
    for side, sign in (('L', -1), ('R', 1)):
        shoulder = actor.bones['UpperArm' + side][0]
        elbow = actor.bones['Forearm' + side][0]
        wrist = actor.bones['Hand' + side][0]
        shell_patch(actor, side + ' shoulder carapace', shoulder.lerp(elbow, .12),
                    (.185, .20, .18), None, .004, .043 if sign < 0 else .030,
                    (sign * .65, .3, .4), .31, .14)
        z = h * (.784 if sign < 0 else .772)
        front = actor.body.chest_surface(sign * .138, z)
        shell_patch(actor, side + ' fused pectoral scar tissue', (sign * .137, front - .010, z),
                    (.146, .13, .135), None, .003, .024, (0, 1, .12), .29, .14)
        shell_patch(actor, side + ' forearm keratin ridge', elbow.lerp(wrist, .52),
                    (.145, .16, .22), 14, .003, .029 if sign < 0 else .019,
                    (sign * .3, .9, .1), .30, .14)
        # Three unequal spurs grow out of each actual deltoid; no detached
        # conical shoulder ornaments or regular comb silhouette.
        for i, (t, length) in enumerate(((.03, .135), (.15, .108), (.26, .077))):
            center = shoulder.lerp(elbow, t)
            z = center.z + (.024 if i == 0 else -.008)
            p, normal, _ = surface(actor).sample(center.x + sign * .026, z)
            normal = (normal + Vector((sign * .35, -.1, .48))).normalized()
            _curve_spur(actor, p + normal * .021, normal, length * (1.08 if sign < 0 else .91),
                        .031 if i == 0 else .025, 'UpperArm' + side, sign * .022)
    # A restrained broken sternum/rib fissure follows the contours of the chest.
    core = h * .734
    for sign in (-1, 1):
        path = [(0, core), (sign * .044, core + .026), (sign * .082, core + .018),
                (sign * .13, core + .065), (sign * .185, core + .088)]
        projected_ribbon(actor, path, .0038, 22, offset=.009)
        for j in range(3):
            z = core - .044 * j
            projected_ribbon(actor, [(sign * .018, z), (sign * .063, z - .022),
                                     (sign * .111, z - .019), (sign * .151, z - .041)],
                             .0023, 22, offset=.008)
        _scar(actor, sign * .215, h * .651, .14, .008, sign * .035)
    _scar(actor, -.057, h * .934, .090, .006, -.025)


def _open_back(actor, tile, z_min, z_max, width):
    """Remove real shirt faces around the rupture and expose matching anatomy."""
    kept_faces, kept_tiles = [], []
    for face, entry in zip(actor.faces, actor.tiles):
        p = sum((Vector(actor.vertices[i]) for i in face), Vector()) / len(face)
        t = (p.z / actor.h - z_min) / (z_max - z_min)
        span = width * (.72 + .28 * math.sin(math.pi * max(0, min(1, t))))
        cut = (entry[0] == tile and p.y < -.018 and 0 < t < 1 and
               abs(p.x) < span * (1 + .09 * math.sin(p.z * 67 + p.x * 49)))
        if not cut:
            kept_faces.append(face)
            kept_tiles.append(entry)
    actor.faces, actor.tiles = kept_faces, kept_tiles
    z = actor.h * (z_min + z_max) * .5
    y = actor.body.back_at(z)
    shell_patch(actor, 'Exposed anatomical back beneath torn shirt', (0, y + .020, z),
                (width * 1.24, .20, actor.h * (z_max - z_min) * .63),
                None, .006, .006, (0, -1, 0), .45, .10)


def _toxic_growth(actor):
    h = actor.h
    _open_back(actor, 44, .660, .838, .150)
    center_z = h * .762
    back = actor.body.back_at(center_z)
    shell_patch(actor, 'Ruptured toxic scapular tissue', (-.035, back + .021, center_z),
                (.216, .205, .197), 12, .011, .025, (0, -1, .15), .38, .16)
    # Three broad, differently shaped lobes share a base on the exposed back.
    # Their perimeter returns to real shoulder skin rather than floating as beads.
    for x, dz, rx, rz, height in ((-.111, .084, .107, .116, .071),
                                 (.048, .021, .117, .100, .057),
                                 (-.089, -.080, .089, .118, .047)):
        z = center_z + dz
        y = actor.body.back_at(z)
        shell_patch(actor, 'Fused toxic growth', (x, y + .009, z), (rx, .20, rz),
                    21, .020, height, (0, -1, .22), .39, .18)
    # The back growth must also have a readable front silhouette. Rupture one
    # existing lapel, restore the matching anatomical skin, then raise an
    # asymmetric collarbone/neck fold from those real source faces.
    kept_faces, kept_tiles = [], []
    for face, entry in zip(actor.faces, actor.tiles):
        p = sum((Vector(actor.vertices[i]) for i in face), Vector()) / len(face)
        opening = ((p.x + .095) / .106) ** 2 + ((p.z - h * .824) / .125) ** 2
        if entry[0] == 44 and p.y > .045 and opening < 1:
            continue
        kept_faces.append(face)
        kept_tiles.append(entry)
    actor.faces, actor.tiles = kept_faces, kept_tiles
    field = surface(actor)
    p, _, _ = field.sample(-.095, h * .820)
    shell_patch(actor, 'Exposed toxic clavicle beneath ruptured lapel', p,
                (.153, .20, .154), None, .008, .014, (0, 1, .3), .43, .13)
    p, _, _ = field.sample(-.077, h * .846)
    shell_patch(actor, 'Fused asymmetric toxic neck fold', p,
                (.095, .17, .105), 12, .014, .048, (-.2, 1, .3), .43, .16)
    p, _, _ = field.sample(-.145, h * .811)
    shell_patch(actor, 'Acidic collarbone rupture', p,
                (.060, .17, .076), 21, .020, .033, (-.3, 1, .2), .43, .18)
    # Narrow dark ruptures and slime rivulets conform to the new skin surface.
    for x, z, length in ((-.08, h * .773, .107), (.046, h * .748, .081)):
        _scar(actor, x, z, length, .007, .024, front=False)
    mouth = actor.body.joint('mouth')
    for i in range(3):
        p = mouth + Vector((-.018 + i * .015, .010, -.010))
        actor.surface(p, p + Vector((.006, .010, -.055 - .015 * i)),
                      [(0, .0032, .0032), (.65, .0042, .0035), (1, .0006, .0006)], 21, 'Head', 6)
    _scar(actor, -.061, h * .928, .066, .005, -.022)


def _civilian_clothing(actor):
    h = actor.h
    # Finishing the real open tear boundaries makes the silhouette legible
    # without disconnected triangular sheets in front of the shirt.
    if actor.name != 'Crawler':
        distress_garment(actor)
    if actor.name == 'Walker':
        for sign in (-1, 1):
            projected_ribbon(actor, [(sign * .070, h * .824), (sign * .094, h * .789),
                                     (sign * .050, h * .745)], .022, 6, offset=.026)
        # One dangling placket and a rolled uneven sleeve distinguish the worker.
        projected_ribbon(actor, [(-.031, h * .778), (-.019, h * .710), (-.025, h * .648)],
                         .009, 18, offset=.022)
        shoulder = actor.bones['UpperArmR'][0]
        elbow = actor.bones['ForearmR'][0]
        shell_patch(actor, 'Worn right sleeve cuff', shoulder.lerp(elbow, .43),
                    (.105, .12, .043), 6, .018, .006, (0, 1, 0), .48)
        _scar(actor, -.061, h * .929, .073, .005, .024)
    elif actor.name == 'Runner':
        _hood(actor)
        for x in (-.047, .045):
            path = [(x, h * .826), (x * 1.16, h * .779), (x * 1.50, h * .739)]
            projected_ribbon(actor, path, .003, 18, offset=.032)
        # Diagonal gathered hoodie fold and the ragged surviving pocket opening.
        projected_ribbon(actor, [(-.125, h * .690), (-.081, h * .704), (-.023, h * .687)],
                         .005, 18, offset=.025)
        _scar(actor, .063, h * .931, .061, .004, -.022)
    elif actor.name == 'Crawler':
        _open_back(actor, 45, .638, .838, .125)
        distress_garment(actor)
        # Continuous tendons/scapular relief replace the old string of spheres.
        z = h * .742
        back = actor.body.back_at(z)
        shell_patch(actor, 'Exposed crawler scapulae', (0, back + .018, z),
                    (.147, .18, .190), None, .007, .018, (0, -1, .15), .36)
        for sign in (-1, 1):
            projected_ribbon(actor, [(sign * .025, h * .661), (sign * .032, h * .722),
                                     (sign * .052, h * .777), (sign * .085, h * .813)],
                             .0032, 15, False, .009)
        _scar(actor, .054, h * .928, .071, .004, -.018)


def _hood(actor):
    """A closed crown, open face and thick ragged rim on the existing head."""
    h = actor.h
    first = len(actor.vertices)
    rings = ((h * .839, .132, .132, -.004), (h * .889, .147, .150, -.012),
             (h * .951, .149, .154, -.017), (h * .995, .119, .135, -.020),
             (h * 1.018, .056, .068, -.020), (h * 1.023, .002, .004, -.020))
    columns = 20
    for j, (z, rx, ry, cy) in enumerate(rings):
        for i in range(columns + 1):
            a = math.pi * (.77 + 1.46 * i / columns)
            uneven = 1 + .018 * math.sin(i * 1.9 + j * .7)
            actor.vertices.append((rx * math.cos(a) * uneven, cy + ry * math.sin(a) * uneven, z))
            actor.weights.append({'Chest': .52, 'Head': .48} if j == 0 else {'Head': 1.})
    for j in range(len(rings) - 1):
        for i in range(columns):
            a = first + j * (columns + 1) + i
            actor.faces.append((a, a + 1, a + columns + 2, a + columns + 1))
            actor.tiles.append((1, ((i / columns, j / 5), ((i + 1) / columns, j / 5),
                                   ((i + 1) / columns, (j + 1) / 5), (i / columns, (j + 1) / 5))))
    for edge in (0, columns):
        for j in range(len(rings) - 2):
            a = Vector(actor.vertices[first + j * (columns + 1) + edge])
            b = Vector(actor.vertices[first + (j + 1) * (columns + 1) + edge])
            actor.surface(a, b, [(0, .006, .006), (.5, .007, .007), (1, .006, .006)], 8, 'Head', 6)
    # Cowl bridges the hood to the fitted garment at the rear neck, weighted
    # across neck/chest so it follows the same existing attack and run cycles.
    rings = []
    for t, rx, ry in ((0, .113, .105), (.35, .139, .120), (1, .152, .121)):
        rings.append((Vector((0, -.017, h * (.845 - t * .048))), rx, ry,
                      {'Head': .30 * (1 - t), 'Chest': .70 + .30 * t}))
    # Only the rear half is needed: the face and throat remain unobstructed.
    base = len(actor.vertices)
    for p, rx, ry, weights in rings:
        for i in range(13):
            a = math.pi + math.pi * i / 12
            actor.vertices.append(tuple(p + Vector((rx * math.cos(a), ry * math.sin(a), 0))))
            actor.weights.append(weights)
    for j in range(2):
        for i in range(12):
            a = base + j * 13 + i
            actor.faces.append((a + 13, a + 14, a + 1, a))
            actor.tiles.append((1, ((i / 12, (j + 1) / 2), ((i + 1) / 12, (j + 1) / 2),
                                   ((i + 1) / 12, j / 2), (i / 12, j / 2))))


def append_infection(actor):
    """The single build hook for all seven authored infected variants."""
    if actor.name == 'Armored':
        _security_kit(actor)
    elif actor.name == 'Boss':
        _mutated_boss(actor)
    elif actor.name == 'Brute':
        _claws(actor)
        tank_harness(actor)
        # Dense healed wounds sit on actual chest/forearm anatomy.
        z = actor.h * .789
        front = actor.body.chest_surface(-.144, z)
        shell_patch(actor, 'Brute shoulder scar tissue', (-.143, front - .009, z),
                    (.128, .15, .116), None, .004, .015, (0, 1, .25), .34, .15)
        _scar(actor, -.136, z, .137, .009, -.044)
        _scar(actor, .166, actor.h * .721, .149, .006, .039)
        _scar(actor, -.061, actor.h * .932, .077, .005, -.017)
    elif actor.name == 'Toxic':
        _toxic_growth(actor)
        distress_garment(actor)
    else:
        _civilian_clothing(actor)


def append_infected_details(actor, pack):
    if actor.name in ('Walker', 'Crawler'):
        from survivor_clothing import append_hair
        append_hair(actor, pack)
    # Teeth are fitted to the existing open-mouth morph, never a replacement jaw.
    from survivor_clothing import append_fitted_head_part
    append_fitted_head_part(actor, pack, 'teeth', 'teeth_base', 16, .25)
