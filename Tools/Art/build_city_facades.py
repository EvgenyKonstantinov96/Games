"""Build the original city facade and window emission atlases for Unity."""

from argparse import ArgumentParser
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


TILE = 256
PADDING = 10
SURFACE = TILE - 2 * PADDING
DEFAULT_OUTPUT = Path(__file__).resolve().parents[2] / "Assets/Resources/Textures"

# Each envelope has a different floor rhythm and mullion profile.
FACADES = (
    ((74, 84, 92), 5, 7, "office"),
    ((68, 80, 92), 4, 8, "office"),
    ((86, 77, 73), 4, 6, "brick"),
    ((79, 78, 77), 5, 7, "brick"),
    ((94, 98, 96), 4, 7, "concrete"),
    ((80, 87, 87), 5, 6, "concrete"),
    ((61, 74, 90), 6, 7, "glass"),
    ((58, 68, 82), 5, 9, "glass"),
)
SOLIDS = (
    ((94, 99, 100), "concrete"),
    ((57, 65, 71), "roof"),
    ((58, 65, 67), "plinth"),
    ((78, 66, 58), "roof"),
    ((57, 64, 64), "shutters"),
    ((59, 72, 85), "skylight"),
    ((65, 71, 74), "vent"),
    ((72, 76, 77), "concrete"),
)


def photograph_grain(rng, color):
    broad = Image.fromarray(rng.integers(0, 255, (7, 8), dtype=np.uint8))
    weather = np.asarray(broad.resize((SURFACE, SURFACE), Image.Resampling.BICUBIC), dtype=float)
    grain = rng.integers(-3, 4, (SURFACE, SURFACE))
    shade = np.round(0.08 * (weather - 127) + grain)
    return Image.fromarray(np.uint8(np.clip(np.asarray(color)[None, None, :] + shade[:, :, None], 0, 255)))


def shade(color, amount):
    return tuple(max(0, min(255, value + amount)) for value in color)


def brickwork(draw, rng, color):
    for top in range(0, SURFACE, 8):
        offset = 12 if top % 16 else 0
        for left in range(offset - 24, SURFACE, 24):
            tint = shade(color, int(rng.integers(-12, 8)))
            draw.rectangle((left, top, left + 22, top + 6), tint)
            draw.line((left, top + 6, left + 22, top + 6), shade(tint, -15))
    draw.line((0, 0, 0, SURFACE - 1), shade(color, -12), width=2)


def glazing(draw, emission, rng, box, active, fractured):
    left, top, right, bottom = box
    glass = (43, 56, 70)
    glow = (184, 112, 60) if rng.random() < .86 else (89, 137, 161)
    if active:
        glass = shade(glow, -18)
    draw.rectangle(box, shade(glass, -18))
    if right - left > 5 and bottom - top > 5:
        for y in range(top + 1, bottom):
            amount = int((bottom - y) * (22 if active else 13) / (bottom - top))
            draw.line((left + 1, y, right - 1, y), shade(glass, amount - 8))
    if active:
        emission.rectangle((left + 1, top + 1, right - 1, bottom - 1), glow)
        if bottom - top > 8:
            emission.line((left + 1, top + 1, right - 1, top + 1), shade(glow, 18))
    elif fractured:
        # Glazed splinters stay against the frame, while the recess reads as a dark hole.
        draw.polygon(((left + 2, top + 1), (right - 1, top + 1), (right - 1, bottom - 2),
                      (right - 5, (top + bottom) // 2)), (31, 38, 46))
        draw.line((left + 3, top + 2, left + 6, (top + bottom) // 2, right - 2, bottom - 3),
                  (82, 100, 115))
    else:
        draw.line((left + 2, top + 2, right - 2, top + 2), shade(glass, 19))
    if right - left > 17:
        center = (left + right) // 2
        draw.line((center, top, center, bottom), (31, 37, 42))
        emission.line((center, top, center, bottom), (0, 0, 0))


def facade_tile(rng, color, columns, floors, kind):
    albedo = photograph_grain(rng, color)
    mask = Image.new("RGB", albedo.size)
    draw, emit = ImageDraw.Draw(albedo), ImageDraw.Draw(mask)
    if kind == "brick":
        brickwork(draw, rng, color)

    top_trim = 9 if kind in ("brick", "concrete") else 6
    span = SURFACE - top_trim * 2
    for floor in range(floors):
        top = top_trim + floor * span // floors
        bottom = top_trim + (floor + 1) * span // floors
        if kind in ("concrete", "glass"):
            draw.line((0, top, SURFACE - 1, top), shade(color, -19), width=2)
            draw.line((0, top + 2, SURFACE - 1, top + 2), shade(color, 13))
        for col in range(columns):
            left = 8 + col * (SURFACE - 16) // columns
            right = 8 + (col + 1) * (SURFACE - 16) // columns
            inset = 8 if kind == "brick" else 4 if kind in ("office", "concrete") else 1
            head, sill = top + (4 if kind == "glass" else 6), bottom - (3 if kind == "glass" else 5)
            pane = (left + inset, head, right - inset, sill)
            draw.rectangle((pane[0] - 2, head - 1, pane[2] + 2, sill + 2), shade(color, -27))
            active = rng.random() < .17
            glazing(draw, emit, rng, pane, active, rng.random() < .24)
            if kind in ("brick", "concrete"):
                draw.line((pane[0] - 2, sill + 2, pane[2] + 2, sill + 2), shade(color, 18), width=2)

    draw.rectangle((0, 0, SURFACE - 1, top_trim - 2), shade(color, -7))
    draw.line((0, 2, SURFACE - 1, 2), shade(color, 16), width=2)
    draw.rectangle((0, SURFACE - top_trim + 1, SURFACE - 1, SURFACE - 1), shade(color, -15))
    return albedo, mask


def solid_tile(rng, color, kind):
    albedo = photograph_grain(rng, color)
    draw = ImageDraw.Draw(albedo)
    if kind in ("roof", "vent"):
        for x in range(10, SURFACE, 28):
            draw.line((x, 0, x, SURFACE - 1), shade(color, -23), width=2)
            draw.line((x + 2, 0, x + 2, SURFACE - 1), shade(color, 14))
    if kind in ("vent", "shutters"):
        for y in range(4, SURFACE, 10):
            draw.line((2, y, SURFACE - 3, y), shade(color, -20), width=2)
            draw.line((2, y + 2, SURFACE - 3, y + 2), shade(color, 12))
    if kind in ("skylight", "plinth"):
        for col in range(3):
            left = 9 + col * 76
            draw.rectangle((left, 24, left + 62, SURFACE - 20), shade(color, -23),
                           outline=shade(color, -32), width=3)
            for y in range(32, SURFACE - 22, 16):
                draw.line((left + 4, y, left + 58, y + 3), shade(color, -9))
    if kind == "concrete":
        for y in (60, 119, 178):
            draw.line((0, y, SURFACE - 1, y), shade(color, -17))
    return albedo, Image.new("RGB", albedo.size)


def weather_tile(albedo, rng):
    streaks = Image.new("L", albedo.size)
    draw = ImageDraw.Draw(streaks)
    for _ in range(32):
        x, y = rng.integers(0, SURFACE, 2)
        length = int(rng.integers(12, 96))
        draw.polygon(((int(x) - 2, int(y)), (int(x) + 2, int(y)),
                      (int(x) + 1, min(int(y) + length, SURFACE - 1))),
                     int(rng.integers(12, 33)))
    grime = np.asarray(streaks.filter(ImageFilter.GaussianBlur(1.4)), dtype=float)
    pixels = np.asarray(albedo, dtype=float) - grime[:, :, None] * np.asarray((.75, .9, 1.0))
    return Image.fromarray(np.uint8(np.clip(pixels, 0, 255)))


def with_padding(tile):
    pixels = np.pad(np.asarray(tile), ((PADDING, PADDING), (PADDING, PADDING), (0, 0)), mode="edge")
    return Image.fromarray(pixels)


def save_atlases(output):
    albedo = Image.new("RGB", (TILE * 4, TILE * 4))
    emission = Image.new("RGB", albedo.size)
    for index in range(16):
        rng = np.random.default_rng(1027 + index * 11)
        if index < len(FACADES):
            color, mask = facade_tile(rng, *FACADES[index])
        else:
            color, mask = solid_tile(rng, *SOLIDS[index - len(FACADES)])
        origin = index % 4 * TILE, index // 4 * TILE
        albedo.paste(with_padding(weather_tile(color, rng)), origin)
        emission.paste(with_padding(mask), origin)
    output.mkdir(parents=True, exist_ok=True)
    albedo.save(output / "city_facades.png", optimize=True)
    emission.save(output / "city_facades_emission.png", optimize=True)


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    save_atlases(parser.parse_args().output)
