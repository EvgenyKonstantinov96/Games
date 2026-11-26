"""Build the rounded nine-slice surfaces shared by the HUD and menus."""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / 'Assets/Resources/UI'


def pill(name, top, bottom, border, radius):
    width, height = 256, 96
    supersample = 2
    size = (width * supersample, height * supersample)
    fill = Image.new('RGBA', size)
    draw = ImageDraw.Draw(fill)
    for y in range(size[1]):
        t = y / (size[1] - 1)
        color = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom))
        draw.line((0, y, size[0], y), fill=(*color, 255))
    mask = Image.new('L', size)
    rect = (2, 2, size[0] - 3, size[1] - 3)
    ImageDraw.Draw(mask).rounded_rectangle(rect, radius=radius * supersample, fill=255)
    fill.putalpha(mask)
    ImageDraw.Draw(fill).rounded_rectangle(rect, radius=radius * supersample, outline=border, width=2)
    fill.resize((width, height), Image.Resampling.LANCZOS).save(OUT / (name + '.png'), optimize=True)


if __name__ == '__main__':
    pill('btn_primary', (170, 66, 27), (120, 38, 18), '#e49a6b', 14)
    pill('btn_secondary', (38, 57, 78), (22, 36, 54), '#56748d', 14)
    pill('btn_muted', (32, 41, 54), (24, 32, 44), '#415365', 14)
    pill('panel', (255, 255, 255), (223, 232, 242), '#e1edf4', 17)
