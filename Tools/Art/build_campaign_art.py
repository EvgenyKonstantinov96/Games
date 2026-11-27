"""Prepare campaign skies and portrait cards from the project's artwork."""
from pathlib import Path
from uuid import NAMESPACE_URL, uuid5

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageOps

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT/'Assets/Resources'


def texture_meta(target, template, linear=False):
    lines = [line.rstrip() for line in template.read_text().splitlines()]
    lines[1] = 'guid: ' + uuid5(NAMESPACE_URL, target.relative_to(ROOT).as_posix()).hex
    metadata = '\n'.join(lines)+'\n'
    if linear:
        metadata = metadata.replace('sRGBTexture: 1','sRGBTexture: 0')
    target.with_suffix(target.suffix+'.meta').write_text(metadata)


def sky(filename, source, mode):
    source = Image.open(source).convert('RGB').resize((1024, 512), Image.Resampling.LANCZOS)
    pixels = np.asarray(source, dtype=float)/255
    height, width = pixels.shape[:2]
    y,x = np.mgrid[:height,:width]
    band = np.exp(-((y/height-.63)/.27)**2)
    glow = band*np.exp(-((x/width-.48)/.43)**2)
    if mode == 'industrial':
        tone = pixels * (1.20,.76,.68)
        tone += glow[...,None] * np.array([.21,.057,.035])
        soot = np.maximum(0,np.sin(x*.017+y*.027)*np.cos(x*.011-y*.013))
        tone *= (1-.22*soot*band)[...,None]
    else:
        luminance = pixels @ np.array([.26,.57,.17])
        tone = np.stack((luminance*.66, luminance*1.04, luminance*1.15), axis=-1)
        tone += glow[...,None] * np.array([.009,.10,.112])
        tone += (pixels - luminance[...,None])*.15
    output = ASSETS/'ProductionArt'/filename
    Image.fromarray(np.uint8(np.clip(tone*255,0,255))).save(output, quality=91, optimize=True)
    texture_meta(output, ASSETS/'ProductionArt/Horizon.jpg.meta')


def hero_cover(hero):
    width,height = 700,790
    image = Image.open(ASSETS/'Artwork'/f'{hero}.png').convert('RGB')
    starting_y = 26 if hero=='eva' else 33
    image = ImageOps.fit(image.crop((0,starting_y,image.width,image.height-25)), (width,height),
                         Image.Resampling.LANCZOS, centering=(.47,.28))
    image = ImageEnhance.Color(image).enhance(1.11)
    y,x = np.mgrid[:height,:width]
    light = np.clip((1-((x/width-.51)*1.74)**2)*.98,.39,1.)
    top_bottom = np.clip(np.minimum(y/125, (height-y)/330),.08,1.)
    shade = np.asarray(image,dtype=float)*light[...,None]
    shade = shade * top_bottom[...,None] + (1-top_bottom[...,None]) * np.array([17,23,35])
    output = Image.fromarray(np.uint8(np.clip(shade,0,255))).convert('RGBA')
    mask = Image.new('L',output.size)
    ImageDraw.Draw(mask).rounded_rectangle((0,0,width-1,height-1),radius=31,fill=255)
    output.putalpha(mask)
    target = ASSETS/'UI'/f'campaign_cover_{hero}.png'
    output.save(target,optimize=True)
    texture_meta(target, ASSETS/'UI/menu_wallpaper.png.meta')


if __name__ == '__main__':
    sky('Horizon_Industrial.jpg',ASSETS/'Skyline/city_chapter_2_v100.png','industrial')
    sky('Horizon_Evacuation.jpg',ASSETS/'Skyline/city_chapter_3_v100.png','evacuation')
    for name in ('max','eva'):
        hero_cover(name)
