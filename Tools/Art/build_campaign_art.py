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
        tone = np.stack((luminance*.78, luminance*.91, luminance*1.08), axis=-1)
        tone += glow[...,None] * np.array([.11,.045,.025])
        tone += (pixels - luminance[...,None])*.15
    output = ASSETS/'ProductionArt'/filename
    Image.fromarray(np.uint8(np.clip(tone*255,0,255))).save(output, quality=91, optimize=True)
    texture_meta(output, ASSETS/'ProductionArt/Horizon.jpg.meta')


def hero_cover(hero):
    # The home card has a fixed 360 x 369 viewport. Keep hair, face and shoulders
    # in that exact crop so NPOT imports and viewport changes cannot recenter it.
    image = Image.open(ASSETS/'Artwork'/f'{hero}.png').convert('RGB')
    height = round(image.width * 369 / 360)
    output = image.crop((0, 0, image.width, height)).convert('RGBA')
    output = output.resize((720, 738), Image.Resampling.LANCZOS)
    target = ASSETS/'UI'/f'campaign_cover_{hero}.png'
    output.save(target, optimize=True)
    # Tiny dialogue avatars use a dedicated face crop at native source detail.
    box = (90, 0, 300, 210) if hero == 'max' else (66, 36, 276, 246)
    face = image.crop(box).resize((256, 256), Image.Resampling.LANCZOS)
    avatar = ASSETS/'UI'/f'dialogue_face_{hero}.png'
    face.save(avatar, optimize=True)
    if not avatar.with_suffix('.png.meta').exists():
        texture_meta(avatar, ASSETS/'UI/campaign_cover_max.png.meta')



if __name__ == '__main__':
    sky('Horizon_Industrial.jpg',ASSETS/'Skyline/city_chapter_2_v100.png','industrial')
    sky('Horizon_Evacuation.jpg',ASSETS/'Skyline/city_chapter_3_v100.png','evacuation')
    for name in ('max','eva'):
        hero_cover(name)
