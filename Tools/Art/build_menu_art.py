"""Build the nine-slice button skins used by the Unity canvas."""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "Assets/Resources/UI"


def skin(name, top, middle, bottom, rim, highlight, radius):
    width, height, scale = 256, 96, 3
    size = (width * scale, height * scale)

    layer = Image.new("RGBA", size)
    rect = (3, 3, size[0] - 4, size[1] - 8)
    ImageDraw.Draw(layer).rounded_rectangle((3, 7, size[0] - 4, size[1] - 3),
                                            radius=radius * scale, fill=(1, 4, 10, 154))
    surface = Image.new("RGBA", size)
    draw = ImageDraw.Draw(surface)
    for y in range(size[1]):
        t = y / (size[1] - 1)
        a, b, blend = (top, middle, t / .66) if t < .66 else (middle, bottom, (t - .66) / .34)
        color = tuple(round(x + (z - x) * blend) for x, z in zip(a, b))
        draw.line((0, y, size[0], y), fill=(*color, 255))
    mask = Image.new("L", size)
    ImageDraw.Draw(mask).rounded_rectangle(rect, radius=radius * scale, fill=255)
    surface.putalpha(mask)
    draw.rounded_rectangle(rect, radius=radius * scale, outline=rim, width=3)
    inset = 8
    draw.arc((rect[0] + inset, rect[1] + inset, rect[2] - inset, rect[1] + radius * 3),
             start=185, end=355, fill=highlight, width=2)
    layer.alpha_composite(surface)
    layer.resize((width, height), Image.Resampling.LANCZOS).save(OUT / (name + ".png"), optimize=True)


if __name__ == "__main__":
    skin("btn_primary", (184, 79, 32), (151, 56, 24), (119, 41, 20), "#d78c55", "#e5a479", 18)
    skin("btn_secondary", (42, 65, 90), (29, 48, 69), (19, 32, 48), "#607a91", "#7694ad", 18)
    skin("btn_muted", (34, 45, 57), (27, 36, 47), (22, 29, 39), "#455668", "#516478", 18)
    skin("panel", (255, 255, 255), (235, 243, 250), (214, 225, 237), "#e3edf6", "#ffffff", 18)
