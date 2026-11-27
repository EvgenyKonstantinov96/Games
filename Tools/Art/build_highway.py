"""Bake a seamless, lane-aligned bridge surface. Requires NumPy and Pillow."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ART = Path(__file__).resolve().parents[2] / "Assets/Resources/ProductionArt"
SIZE = 1024
WIDTH = 9.7
LENGTH = 11.5


def periodic_noise(rng, grid_x, grid_y):
    """Smoothly interpolate the last lattice point back to the first on either axis."""
    lattice = rng.normal(0, 1, (grid_y, grid_x))
    x = np.arange(SIZE) * grid_x / SIZE
    y = np.arange(SIZE) * grid_y / SIZE
    ix, iy = x.astype(int), y.astype(int)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    upper = lattice[iy[:, None], ix] * (1 - fx) + lattice[iy[:, None], (ix + 1) % grid_x] * fx
    lower = lattice[(iy[:, None] + 1) % grid_y, ix] * (1 - fx) + lattice[(iy[:, None] + 1) % grid_y, (ix + 1) % grid_x] * fx
    return upper * (1 - fy[:, None]) + lower * fy[:, None]


def fractures():
    """Hand-position main fissures; midpoint displacement and tapered branches erode their edges."""
    rng = np.random.default_rng(371)
    canvas = Image.new("L", (SIZE, SIZE))
    draw = ImageDraw.Draw(canvas)

    def stroke(start, end, width, intensity):
        points = [np.array(start) * SIZE, np.array(end) * SIZE]
        for _ in range(6):
            refined = [points[0]]
            for p, q in zip(points, points[1:]):
                direction = (q - p)[::-1] * [-1, 1]
                mid = (p + q) * .5 + direction * rng.uniform(-.25, .25)
                refined.extend((mid, q))
            points = refined
        for shift in (-SIZE, 0, SIZE):
            path = [(p[0], p[1] + shift) for p in points]
            draw.line(path, fill=intensity, width=width)
        return points

    for start, end, width in [
        ((.06, .06), (.34, .44), 3), ((.34, .44), (.45, .5), 2),
        ((.54, .72), (.97, .86), 3), ((.84, .22), (1.02, .31), 3),
        ((.29, .83), (.18, 1.12), 2), ((.49, .05), (.63, .20), 2),
    ]:
        points = stroke(start, end, width, 240)
        for i in (16, 34, 50):
            p = points[i] / SIZE
            q = p + (np.array(end) - start)[::-1] * rng.uniform(.13, .30) * [-1, 1]
            stroke(p, q, 1, 170)
    return np.asarray(canvas.filter(ImageFilter.GaussianBlur(.5)), dtype=float) / 255


def build():
    rng = np.random.default_rng(5489)
    v, u = (np.mgrid[:SIZE, :SIZE] + .5) / SIZE
    x, z = (u - .5) * WIDTH, v * LENGTH
    broad = periodic_noise(rng, 8, 9)
    stones = periodic_noise(rng, 182, 197)
    grain = rng.normal(size=(SIZE, SIZE))
    freckle = np.maximum(grain - .72, 0)
    cracks = fractures()
    # Sealer darkens the ragged lip around an open fissure.
    expanded = Image.fromarray(np.uint8(cracks * 255)).filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(1.6))
    seal = np.asarray(expanded, dtype=float) / 255
    wheel_tracks = np.zeros_like(u)
    for wheel in (-3.36, -2.13, -.64, .64, 2.13, 3.36):
        wander = .052 * np.sin(v * np.pi * 2 + wheel) + .028 * np.cos(v * np.pi * 4)
        wheel_tracks += np.exp(-((x - wheel + wander) / .20) ** 2)
    shoulder = np.clip((abs(x) - 3.7) / .9, 0, 1)
    # Damp shallow depressions get darker aggregate, flatter normals and higher smoothness.
    basin = periodic_noise(rng, 9, 4) + .28 * periodic_noise(rng, 28, 18)
    damp = np.clip((basin + .5 * wheel_tracks + shoulder * .26 - .11) * .94, 0, 1)
    water = damp * damp * (3 - 2 * damp)
    grit = 6.3 * stones + 3.2 * grain + 5.8 * freckle
    base = (69 + 4 * broad + grit * (1 - .45 * water) - 6 * wheel_tracks
            - 11 * water - 13 * seal - 23 * cracks)
    road = np.stack((base - 3 + 4 * shoulder, base, base + 5 + 3.5 * water), axis=-1)
    rough_height = (stones * .14 + grain * .058) * (1 - .77 * water) - .66 * cracks
    gloss = .19 + .08 * np.clip(stones, 0, 1) + .65 * water + .12 * seal

    # Road markings share the surface relief and lighting. Three dashes per 11.5 m tile.
    wear = periodic_noise(rng, 112, 95) + .36 * grain + .22 * broad
    jitter = periodic_noise(rng, 9, 99) * .006
    dashed = (abs(abs(x) - 1.80 + jitter) < .052) & (np.abs((z + LENGTH / 6) % (LENGTH / 3) - LENGTH / 6) < .79)
    edges = abs(abs(x) - 4.65 + jitter) < .044
    opacity = (dashed | edges) * np.clip(1.06 + .36 * wear, .20, 1) * (1 - .84 * cracks)
    paint = np.clip((180 + 15 * stones + 10 * grain - 28 * water)[..., None] * np.array([1., .965, .86]), 0, 245)
    road += opacity[..., None] * (paint - road)
    rough_height += .20 * opacity
    gloss += opacity * (.36 + .40 * water - gloss)

    metallic = np.zeros_like(gloss)
    # Cast-iron drainage pockets stay outside the wheel paths. Bent oxidation and wet
    # sediment collect in each recess, with reflected cool metal on the slat edges.
    for side, center in ((-1, 3.15), (1, 8.85)):
        dx, dz = x - side * 4.35, z - center
        edge = np.maximum(abs(dx) - .165, abs(dz) - .398)
        frame = edge < 0
        pocket = (abs(dx) < .137) & (abs(dz) < .368)
        slat = ((dz + .36) % .062 < .023) | (abs(dx) < .015)
        shade = np.where(pocket & ~slat, 28 + 2.9 * stones,
                         70 + 4.8 * stones + 4 * grain + 9 * (dz % .062 < .014))
        iron = np.stack((shade + 4, shade + 2, shade + 1), axis=-1)
        road[frame] = iron[frame]
        metallic[frame] = np.where(pocket & ~slat, .03, .48)[frame]
        gloss[frame] = np.where(pocket & ~slat, .23, .57)[frame]
        rough_height[frame] = np.where(pocket & ~slat, -.58, .15)[frame]

    nx = (np.roll(rough_height, 1, 1) - np.roll(rough_height, -1, 1)) * 1.12
    ny = (np.roll(rough_height, -1, 0) - np.roll(rough_height, 1, 0)) * .97
    # Filter slopes before normalizing and encoding the half-resolution map.
    def half(a):
        return a.reshape(SIZE // 2, 2, SIZE // 2, 2).mean(axis=(1, 3))
    normals = np.stack((half(nx), half(ny), np.ones((SIZE // 2, SIZE // 2))), axis=-1)
    normals /= np.linalg.norm(normals, axis=-1, keepdims=True)
    packed = np.zeros((SIZE // 2, SIZE // 2, 4), dtype=np.uint8)
    packed[:, :, 0] = np.uint8(half(metallic) * 255)
    packed[:, :, 3] = np.uint8(half(np.clip(gloss, .12, .94)) * 255)
    Image.fromarray(np.uint8(np.clip(road, 0, 255))).save(ART / "Highway.png", optimize=True)
    Image.fromarray(np.uint8(normals * 127.5 + 127.5)).save(ART / "Highway_normal.png", optimize=True)
    Image.fromarray(packed).save(ART / "Highway_surface.png", optimize=True)

    # The guide fixtures catch on wet aggregate as broken transverse streaks.
    n = 128
    sy, sx = (np.mgrid[:n, :n] + .5 - n / 2) / (n / 2)
    wobble = .07 * np.sin(13 * sy) + .028 * np.cos(39 * sy)
    radius = ((sx + wobble) / .93) ** 2 + sy ** 2
    fade = np.clip(1 - radius, 0, 1) ** 2
    ripples = .5 + .5 * np.sin(84 * sy + 1.18 * np.sin(9 * sy + 3 * sx) + 2.7 * np.cos(4 * sx))
    alpha = fade * (.11 + .70 * ripples ** 2 + .19 * np.exp(-13 * (sx + wobble) ** 2))
    spill = np.full((n, n, 4), 255, dtype=np.uint8)
    spill[:, :, 3] = np.uint8(alpha * 255)
    Image.fromarray(spill).save(ART / "RoadReflection.png", optimize=True)



if __name__ == "__main__":
    build()
