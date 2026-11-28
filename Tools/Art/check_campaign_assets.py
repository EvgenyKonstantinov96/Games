"""Check authored landmark clearance and campaign texture data without Unity."""
from pathlib import Path
import math
import re

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT/'Assets/Resources'


def inspect_landmarks():
    # Reserve usable width for the hero and height for the scaled boss. Test all
    # vertices and every face that could cross the measured corridor.
    source = (ROOT/'Assets/Scripts/ZSRGame.cs').read_text()
    half_width = float(re.search(r'RunLaneLimit\s*=\s*([\d.]+)f', source)[1]) + .55
    headroom = 5.8
    for name in ('ruins','industrial','evacuation'):
        path = RESOURCES/'Meshes'/f'track_chapter_{name}.obj'
        positions, faces = [], []
        normals = texcoords = 0
        for row in path.read_text().splitlines():
            entry,*fields = row.split()
            if entry == 'v':
                positions.append(tuple(map(float, fields)))
            elif entry == 'f':
                faces.append([tuple(map(int,ref.split('/'))) for ref in fields])
            elif entry == 'vn':
                normals += 1
                assert math.isclose(math.hypot(*map(float, fields)),1.,abs_tol=.001)
            elif entry == 'vt':
                texcoords += 1
                assert all(0 < float(value) < 1 for value in fields)
        positions=np.asarray(positions)
        assert len(positions) and normals == texcoords > 0
        min_clearance = math.inf
        for face in faces:
            p = np.asarray([positions[i-1] for i, uv, normal in face])
            assert all(0 < i <= len(positions) and 0 < uv <= texcoords and 0 < normal <= normals for i,uv,normal in face)
            assert np.linalg.norm(np.cross(p,np.roll(p,-1,axis=0)).sum(axis=0)) > .00004
            if p[:,0].max() >= -half_width and p[:,0].min() <= half_width:
                min_clearance = min(min_clearance,p[:,1].min())
        assert min_clearance >= headroom, (path,min_clearance)
        triangles = sum(len(face)-2 for face in faces)
        assert triangles < 1800,(path,triangles)
        print(f'LANDMARK PASS {name:10} {triangles} triangles, {min_clearance:.3f} m road headroom.')


def inspect_highway():
    track = (ROOT/'Assets/Scripts/ZSRTrackArt.cs').read_text()
    world = (ROOT/'Assets/Scripts/ZSRWorld.cs').read_text()
    width = float(re.search(r'DeckWidth = ([\d.]+)f', world)[1])
    length = float(re.search(r'Length = ([\d.]+)f', world)[1])
    repeat = float(re.search(r'SurfaceRepeatLength = ([\d.]+)f', track)[1])
    assert length / repeat == round(length / repeat)
    art = RESOURCES/'ProductionArt'
    color = np.asarray(Image.open(art/'Highway.png'), dtype=float)
    normal = np.asarray(Image.open(art/'Highway_normal.png'), dtype=float) / 127.5 - 1
    packed = np.asarray(Image.open(art/'Highway_surface.png'), dtype=float)
    assert color.shape == (1024,1024,3) and normal.shape == (512,512,3) and packed.shape == (512,512,4)
    assert np.max(np.abs(np.linalg.norm(normal, axis=2) - 1)) < .012
    # Abraded paint must resolve on both outside edges and alternate at both lane boundaries.
    lightness = color.mean(axis=2)
    for metres in (-4.65, -1.80, 1.80, 4.65):
        column = round((.5 + metres/width) * color.shape[1])
        paint = lightness[:,column-2:column+2].mean(axis=1)
        bright = np.mean(paint > 110)
        if abs(metres) > 4:
            assert bright > .91, (metres, bright)
        else:
            assert .35 < bright < .48, (metres, bright)
            # Count whole dashes in a 64-row mip, allowing small chips up close.
            adjacent_asphalt = lightness[:,column+16:column+20].mean(axis=1)
            filtered = (paint-adjacent_asphalt).reshape(64,16).mean(axis=1) > 32
            assert np.count_nonzero(filtered & ~np.roll(filtered, 1)) == 3
    metal_x = np.where(packed[...,0] > 20)[1]
    assert .37 * 512 < np.abs(metal_x - 255.5).min()
    for channel in (color, packed[...,3:]):
        across_joint = np.abs(channel[0] - channel[-1]).mean()
        typical_step = np.abs(np.diff(channel, axis=0)).mean()
        assert across_joint < 1.6 * typical_step, (across_joint, typical_step)
    spill = np.asarray(Image.open(art/'RoadReflection.png'))[...,3]
    assert not any((spill[0].any(), spill[-1].any(), spill[:,0].any(), spill[:,-1].any()))
    assert spill.max() > 212 and .06 < spill.mean() / 255 < .20
    print(f'BRIDGE SURFACE PASS: {length:g} m module, {length/repeat:g} seamless repeats, three dashes per repeat, '
          'PBR maps registered, two shoulder drains and feathered lamp mask.')



def inspect_chapter_textures():
    hues = {}
    for name in ('Industrial','Evacuation'):
        with Image.open(RESOURCES/'ProductionArt'/f'Horizon_{name}.jpg') as image:
            assert image.size == (1024,512)
            middle = np.asarray(image,dtype=float)[192:383,220:800]
            hues[name] = middle.mean(axis=(0,1))
    warm,cool=hues['Industrial'],hues['Evacuation']
    assert warm[0] > warm[1]+12 and cool[1] > cool[0]+12
    with Image.open(RESOURCES/'ProductionArt/Highway_surface.png') as road:
        alpha=np.asarray(road.getchannel('A'))
        assert road.size == (512,512) and alpha.min() < 70 and alpha.max() > 220
        assert np.abs(alpha[0,:].astype(int)-alpha[-1,:]).mean() < 6
        assert np.abs(alpha[:,0].astype(int)-alpha[:,-1]).mean() < 6
    for name in ('eva','max'):
        with Image.open(RESOURCES/'UI'/f'campaign_cover_{name}.png') as cover:
            assert cover.size == (720,738) and cover.mode == 'RGBA'
            assert cover.getpixel((0,0))[3] == 255 and cover.getpixel((350,395))[3] == 255
    print('CAMPAIGN ART PASS: distinct warm/cool skies, low-gloss road and wet patches, two portrait covers.')


if __name__ == '__main__':
    inspect_landmarks()
    inspect_highway()
    inspect_chapter_textures()
