"""Build the nine-slice skins and the field-kit pictograms for the Unity canvas."""
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


def symbols():
    # Draw at four times the final resolution. Each 64 px cell has its own gutter
    # so bilinear sampling at the smallest navigation size never reaches a neighbour.
    scale = 4
    cell = 64
    atlas = Image.new("RGBA", (cell * 4, cell * 4))
    for slot in range(13):
        mask = Image.new("L", (cell * scale, cell * scale))
        draw = ImageDraw.Draw(mask)
        def line(points, width=4):
            points = [(x * scale, y * scale) for x, y in points]
            draw.line(points, fill=255, width=width * scale, joint="curve")
            radius = width * scale // 2
            for x, y in (points[0], points[-1]):
                draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=255)

        def box(coords, radius=3, width=4):
            draw.rounded_rectangle(tuple(n*scale for n in coords), radius=radius*scale,
                                   outline=255, width=width*scale)

        def oval(coords, width=4):
            draw.ellipse(tuple(n*scale for n in coords), outline=255, width=width*scale)

        if slot == 0:  # shelter
            line([(8,29),(32,10),(56,29)])
            line([(15,28),(15,52),(26,52),(26,39),(38,39),(38,52),(49,52),(49,28)])
        elif slot == 1:  # two survivors
            oval((13,11,33,32)); line([(8,51),(10,43),(15,38),(29,38),(36,44),(37,51)])
            line([(39,14),(45,14),(49,18),(49,24),(44,29),(40,29)])
            line([(41,36),(48,36),(54,41),(56,48)])
        elif slot == 2:  # magazine and rounds
            for x, top in ((12,16),(27,9),(42,14)):
                line([(x,44),(x,top+9),(x+4,top),(x+8,top+9),(x+8,44)])
            box((9,42,54,52),2,4)
        elif slot == 3:  # field pack
            line([(23,17),(23,12),(27,9),(37,9),(41,13),(41,17)])
            box((13,16,51,55),8); line([(15,32),(49,32)])
            box((24,39,40,47),2,3)
        elif slot == 4:  # folded route map
            line([(8,16),(23,10),(39,17),(55,11),(55,48),(40,54),(24,47),(8,53),(8,16)])
            line([(23,12),(23,45)],3); line([(40,19),(40,51)],3)
        elif slot == 5:  # currency token
            oval((9,9,55,55)); oval((15,15,49,49),2)
            line([(32,21),(41,32),(32,43),(23,32),(32,21)],3)
        elif slot == 6:  # step through a gate
            line([(10,51),(10,18),(20,11),(23,11),(23,51)])
            line([(41,51),(41,11),(44,11),(54,18),(54,51)])
            for y in (21,30,39): line([(29,y),(35,y-2)],3)
        elif slot == 7:  # soundtrack
            line([(23,44),(23,19),(48,13),(48,38)])
            line([(24,25),(47,19)],3)
            oval((10,38,25,51)); oval((35,32,50,45))
        elif slot == 8:  # first aid
            box((9,19,55,51),5); line([(23,18),(23,12),(41,12),(41,18)])
            line([(25,35),(39,35)],5); line([(32,28),(32,42)],5)
        elif slot == 9:  # grenade
            oval((14,22,43,54)); box((22,14,35,23),2,3)
            line([(25,13),(39,11),(51,23),(52,33)],4)
            line([(19,35),(38,35)],3); line([(18,41),(36,41)],2)
        elif slot == 10:  # drone
            box((23,26,41,38),4,3)
            for x,y in ((15,17),(49,17),(15,47),(49,47)):
                oval((x-9,y-5,x+9,y+5),3)
                line([(x,y),(27 if x<32 else 37,30 if y<32 else 34)],3)
        elif slot == 11:  # protective plate
            line([(32,8),(51,17),(48,36),(40,47),(32,54),(24,47),(16,36),(13,17),(32,8)])
            line([(23,29),(30,36),(42,23)],4)
        else:  # directional chevron
            line([(23,12),(43,32),(23,52)],5)
        icon = Image.new("RGBA", mask.size, "white")
        icon.putalpha(mask)
        atlas.paste(icon.resize((cell, cell), Image.Resampling.LANCZOS),
                    (cell * (slot % 4), cell * (slot // 4)))
    atlas.save(OUT / "menu_symbols.png", optimize=True)


if __name__ == "__main__":
    skin("btn_primary", (184, 79, 32), (151, 56, 24), (119, 41, 20), "#d78c55", "#e5a479", 18)
    skin("btn_secondary", (42, 65, 90), (29, 48, 69), (19, 32, 48), "#607a91", "#7694ad", 18)
    skin("btn_muted", (34, 45, 57), (27, 36, 47), (22, 29, 39), "#455668", "#516478", 18)
    skin("panel", (255, 255, 255), (235, 243, 250), (214, 225, 237), "#e3edf6", "#ffffff", 18)
    symbols()
