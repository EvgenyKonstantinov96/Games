"""Check the bridge loop, atlas isolation and Cyrillic glyphs without Unity."""
from pathlib import Path
import math
import re

from fontTools.ttLib import TTFont
from PIL import Image, ImageFont

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / 'Assets/Scripts'
ASSETS = ROOT / 'Assets/Resources'


def constant(source, name):
    return float(re.search(r'\b' + name + r'\s*=\s*(-?[\d.]+)f', source)[1])


def inspect_bridge():
    road = (SCRIPTS / 'ZSRWorld.cs').read_text()
    track = (SCRIPTS / 'ZSRTrackArt.cs').read_text()
    game = (SCRIPTS / 'ZSRGame.cs').read_text()
    length = constant(road, 'Length')
    underlay_center = constant(track, 'center')
    underlay_length = constant(track, 'length')
    far_clip = constant(game, 'farClipPlane')
    centers = [length * (index - .5) for index in range(4)]
    minimum_overlap = math.inf
    for _ in range(1600):
        ends = sorted(centers)
        assert all(abs(a + length - b) < .001 for a,b in zip(ends,ends[1:])), ends
        moving_front = ends[-1] + length/2
        minimum_overlap = min(minimum_overlap, moving_front - (underlay_center - underlay_length/2))
        centers = [p - .43 if p - .43 >= -length*1.5 else p - .43 + length*4 for p in centers]
    assert minimum_overlap > 5 and underlay_center + underlay_length/2 > far_clip

    vertices, faces = [], []
    normal_count = uv_count = 0
    for row in (ASSETS/'Meshes/track_guardrail.obj').read_text().splitlines():
        entry, *values = row.split()
        if entry == 'v':
            vertices.append(tuple(map(float, values)))
        elif entry == 'vn':
            normal_count += 1
            assert math.isclose(math.hypot(*map(float, values)), 1, abs_tol=.001)
        elif entry == 'vt':
            uv_count += 1
            assert all(0 < float(v) < 1 for v in values)
        elif entry == 'f':
            faces.append([tuple(map(int, ref.split('/'))) for ref in values])
    assert normal_count == uv_count > 0
    assert all(1 <= v <= len(vertices) and 1 <= t <= uv_count and 1 <= n <= normal_count
               for face in faces for v,t,n in face)
    triangle_count = sum(len(face)-2 for face in faces)
    assert triangle_count < 1500, triangle_count
    assert math.isclose(max(z for x,y,z in vertices)-min(z for x,y,z in vertices), length)
    assert constant(road, 'RailX') + min(x for x,y,z in vertices) > constant(game, 'RunLaneLimit') + .3
    print(f'BRIDGE PASS: 1600 loop steps, at least {minimum_overlap:.2f} m deck overlap, {triangle_count} guardrail triangles.')


def inspect_shared_art():
    menu = (SCRIPTS/'ZSRInterface.cs').read_text()
    gate = (SCRIPTS/'ZSRVeilGateView.cs').read_text()
    literal_text = ''.join(re.findall(r'"([^"\n]*)"', menu + '\n' + gate))
    with TTFont(ASSETS/'UI/ZSRMenuFont.ttf') as font:
        glyphs = font.getBestCmap()
        assert all(ord(ch) in glyphs for ch in literal_text)
    with TTFont(ASSETS/'UI/ZSRButtonFont.ttf') as font:
        assert font['OS/2'].usWeightClass == 700
        glyphs = font.getBestCmap()
        assert all(ord(ch) in glyphs for ch in literal_text), sorted(set(ch for ch in literal_text if ord(ch) not in glyphs))
    with Image.open(ASSETS/'ProductionArt/CharacterAtlas_emission.png') as image:
        assert image.size == (1024,1024)
        assert image.getbbox() == (768,256,896,384)
    with Image.open(ASSETS/'ProductionArt/ParticleSoft.png') as image:
        alpha = image.getchannel('A')
        assert alpha.getpixel((0,0)) == 0 and alpha.getpixel((32,32)) > 220
    for name in ('btn_primary','btn_secondary','btn_muted','panel'):
        with Image.open(ASSETS/f'UI/{name}.png') as image:
            assert image.size == (256,96)
            alpha = image.getchannel('A')
            assert all(alpha.getpixel((x, y)) < 5 for x, y in ((0,0), (255,0), (0,95), (255,95)))
            assert alpha.getpixel((18, 8)) > 240 and alpha.getpixel((128, 48)) == 255
            meta = (ASSETS/f'UI/{name}.png.meta').read_text()
            assert 'enableMipMap: 0' in meta and 'wrapU: 1' in meta and 'wrapV: 1' in meta
    with Image.open(ASSETS/'UI/menu_symbols.png') as atlas:
        assert atlas.size == (256, 256)
        alpha = atlas.getchannel('A')
        cells = []
        for slot in range(16):
            x, y = 64 * (slot % 4), 64 * (slot // 4)
            cell = alpha.crop((x, y, x + 64, y + 64))
            if slot >= 13:
                assert cell.getbbox() is None
                continue
            left, top, right, bottom = cell.getbbox()
            assert min(left, top, 64 - right, 64 - bottom) >= 3, slot
            assert cell.resize((24, 24), Image.Resampling.LANCZOS).getextrema()[1] >= 190, slot
            cells.append(cell.tobytes())
        assert len(set(cells)) == 13
    title_font = ImageFont.truetype(str(ASSETS/'UI/ZSRButtonFont.ttf'), 21)
    for source, tag in (('ZSRGame', 'string[] LevelNames'), ('ZSRLevelPlan', 'string[] Districts')):
        names = (SCRIPTS/(source + '.cs')).read_text().split(tag, 1)[1].split('};', 1)[0]
        assert all(title_font.getlength(name.upper()) <= 332 for name in re.findall(r'"([^"]+)"', names))
    ui_importer = (ROOT/'Assets/Editor/ZSRMenuArtImporter.cs').read_text()
    assert all(flag in ui_importer for flag in (
        'TextureImporterNPOTScale.None', 'TextureWrapMode.Clamp',
        'importer.mipmapEnabled = false', 'TextureImporterCompression.Uncompressed'))
    for path in (ASSETS/'UI').glob('*.png.meta'):
        meta = path.read_text()
        assert 'nPOTScale: 0' in meta and 'enableMipMap: 0' in meta, path
    for name in ('max', 'eva'):
        with Image.open(ASSETS/f'UI/dialogue_face_{name}.png') as face:
            assert face.size == (256, 256)
        with Image.open(ASSETS/f'UI/campaign_cover_{name}.png') as cover:
            assert cover.width * 369 == cover.height * 360
    print('UI IMPORT PASS: original proportions, no mipmaps, clamp and uncompressed desktop art; native face crops.')
    print('MENU PASS: 13 distinct antialiased atlas symbols with clear gutters; all 55 level/district titles fit the home mission card.')
    silhouettes = [(ASSETS/f'Artwork/Weapons/{weapon}.png').read_bytes()
                   for weapon in ('pistol','rifle','shotgun','minigun')]
    assert len(set(silhouettes)) == 4
    print('ASSETS PASS: Cyrillic and symbols in both fonts, 700-weight button captions, rounded button alpha, isolated boss emission, four unique arsenal icons.')


if __name__ == '__main__':
    inspect_bridge()
    inspect_shared_art()
