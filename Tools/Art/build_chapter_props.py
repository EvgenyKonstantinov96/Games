"""Author three low-poly roadside structures. Coordinates are Y-up and in metres."""
from uuid import NAMESPACE_URL, uuid5

import numpy as np

from build_track_props import BED, CHEVRON, CONCRETE, MESHES, PYLON, REBAR, RIM, ROOT, SPIKE, Prop


def block(model, x, y, z, width, height, length, tile, bevel=.06):
    a, b = width*.5, length*.5
    bevel = min(bevel, a*.65, b*.65)
    waist = min(height*.16, bevel*.81)
    model.octagon([(y, a-waist, b-waist, bevel*.55, x, z),
                   (y+waist, a, b, bevel, x, z),
                   (y+height-waist, a, b, bevel, x, z),
                   (y+height, a-waist, b-waist, bevel*.55, x, z)], tile)


def truss(model, a, b, half_width, tile):
    a, b = np.array(a, dtype=float), np.array(b, dtype=float)
    axis = (b-a)/np.linalg.norm(b-a)
    guide = np.array((0.,1.,0.)) if abs(axis[1]) < .95 else np.array((0.,0.,1.))
    x = np.cross(guide, axis)
    x *= half_width / np.linalg.norm(x)
    y = np.cross(axis, x)
    points = [center+dx*x+dy*y for center in (a,b) for dx,dy in ((-1,-1),(1,-1),(1,1),(-1,1))]
    model.add(points, [[3,2,1,0],[4,5,6,7]]+
              [[j,(j+1)%4,4+(j+1)%4,4+j] for j in range(4)], tile)


def pipe(model, a, b, radius, tile):
    a, b = np.array(a, dtype=float), np.array(b, dtype=float)
    axis = (b-a)/np.linalg.norm(b-a)
    guide = np.array((0.,1.,0.)) if abs(axis[1]) < .95 else np.array((0.,0.,1.))
    x = np.cross(guide, axis)
    x *= radius / np.linalg.norm(x)
    y = np.cross(axis, x)
    points = [center+np.cos(t)*x+np.sin(t)*y for center in (a,b) for t in np.arange(10)*np.pi*.2]
    model.add(points, [list(reversed(range(10))),list(range(10,20))]+
              [[i,(i+1)%10,10+(i+1)%10,10+i] for i in range(10)], tile)


def ruins():
    model = Prop()
    for side in (-1,1):
        x = side*6.2
        block(model,x,0,0,1.48,.56,1.60,CONCRETE,.13)
        block(model,x,.56,0,.72,6.10,.84,PYLON,.08)
        block(model,x,6.66,0,1.18,.45,1.37,RIM,.09)
        for z in (-.42,.42):
            truss(model,(x,.73,z),(side*7.65,2.85,z),.093,BED)
            truss(model,(x,4.6,z),(side*5.25,7.27,z),.12,PYLON)
        block(model,x,.77,-.48,.59,.40,.12,CHEVRON)
        for k in range(3):
            block(model,side*(6.97+k*.33),0,-1.58+k*.40,.35,.16+k*.07,.43,CONCRETE)
    for z in (-.61,.61):
        for y in (7.21,8.35):
            truss(model,(-6.6,y,z),(6.6,y,z),.116,PYLON)
        for j in range(8):
            x=-6.4+j*1.60
            truss(model,(x,7.23,z),(x+1.60,8.35,z),.065,BED)
            truss(model,(x,7.23,z),(x,8.35,z),.056,REBAR)
    block(model,0,6.83,-.75,4.68,.57,.17,BED)
    block(model,0,6.84,-.86,4.43,.09,.06,CHEVRON)
    model.save('track_chapter_ruins')


def industrial():
    model = Prop()
    for side in (-1,1):
        x=side*7.25
        block(model,x,0,.22,2.14,.42,2.70,CONCRETE,.18)
        for step in range(2):
            z=-1.09+step*2.04
            top=8.8 if side<0 and step==1 else 7.3-step*.9
            pipe(model,(x,.42,z),(x,top,z),.48,REBAR)
            for y in (.63,2.25,4.41,top-.32):
                pipe(model,(x,y,z),(x,y+.17,z),.60,PYLON)
            pipe(model,(x,top-.12,z),(x,top+.21,z),.56,SPIKE)
            truss(model,(x,.35,z),(x+side*1.20,4.2,z),.105,BED)
            truss(model,(x,4.1,z),(x+side*.75,top-.7,z),.081,BED)
        block(model,x-side*.28,3.08,-1.49,1.02,.62,.16,CHEVRON)
    for z in (-1.04,.73):
        pipe(model,(-7.28,6.85,z),(7.28,6.85,z),.23,REBAR)
        for x in (-5.9,-3.3,0,3.3,5.9):
            pipe(model,(x-.09,6.85,z),(x+.09,6.85,z),.31,PYLON)
        for side in (-1,1):
            truss(model,(side*7.2,4.7,z),(side*5.14,6.78,z),.14,BED)
    model.save('track_chapter_industrial')


def evacuation():
    model = Prop()
    for side in (-1,1):
        x=side*6.35
        block(model,x,0,0,1.66,.54,2.17,CONCRETE,.18)
        for z in (-.8,.8):
            block(model,x,.54,z,.39,5.90,.45,PYLON,.072)
            truss(model,(x,.53,z),(side*7.49,2.47,z),.12,BED)
        for y in (1.41,2.4,3.39,4.38,5.37):
            truss(model,(x,y,-.8),(x,y+.93,.80),.069,RIM)
        block(model,x,6.43,-.15,1.87,.33,2.32,BED)
        block(model,x-side*.15,6.77,-.83,1.43,.62,.58,PYLON)
        for row in range(2):
            block(model,x+side*(1.14+row*.62),0,-1.7+row*.77,.83,.51,.78,BED,.12)
        pipe(model,(x+side*.55,6.61,.37),(x+side*.55,8.06,.37),.075,RIM)
        truss(model,(x+side*.55,7.87,-.44),(x+side*.55,7.87,1.19),.063,PYLON)
    block(model,0,6.31,0,11.95,.532,1.18,PYLON,.105)
    block(model,0,6.17,-.67,4.87,.34,.24,CHEVRON)
    for j in range(-3,4):
        block(model,j*1.45,6.79,-.61,.43,.18,.22,BED)
    model.save('track_chapter_evacuation')


if __name__ == '__main__':
    ruins()
    industrial()
    evacuation()
    template = (MESHES/'track_guardrail.obj.meta').read_text().splitlines()
    for mesh in MESHES.glob('track_chapter_*.obj'):
        template[1] = 'guid: ' + uuid5(NAMESPACE_URL,mesh.relative_to(ROOT).as_posix()).hex
        mesh.with_suffix('.obj.meta').write_text('\n'.join(template)+'\n')
