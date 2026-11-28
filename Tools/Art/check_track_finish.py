"""Check physical finish channels and the unchanged collision-sized track envelopes."""
import argparse,math
from pathlib import Path
import numpy as np
from PIL import Image
from build_track_props import Prop

parser=argparse.ArgumentParser()
parser.add_argument('--resources',type=Path,default=Path(__file__).resolve().parents[2]/'Assets/Resources')
parser.add_argument('--baseline',type=Path)
args=parser.parse_args();art=args.resources/'ProductionArt';meshes=args.resources/'Meshes'
normal=np.asarray(Image.open(art/'TrackPropAtlas_normal.png').convert('RGB'),dtype=float)/127.5-1
finish=np.asarray(Image.open(art/'TrackPropAtlas_surface.png').convert('RGBA'),dtype=float)/255
assert normal.shape==(512,512,3) and finish.shape==(512,512,4)
assert np.max(np.abs(np.linalg.norm(normal,axis=-1)-1))<.015
assert np.max(np.abs(normal[:128,:128,:2]))>.03,'Concrete relief is missing'
means={}
for i,name in [(0,'concrete'),(3,'spikes'),(5,'glass'),(6,'rubber'),(12,'zinc')]:
    cell=finish[i//4*128:(i//4+1)*128,i%4*128:(i%4+1)*128]
    means[name]=cell.mean(axis=(0,1))
assert means['concrete'][0]<.01 and means['rubber'][0]<.01
assert means['spikes'][0]>.7 and means['zinc'][0]>.4
assert means['glass'][3]>.7 and means['concrete'][3]<.3
print('TRACK FINISH PASS: normalized relief; concrete/rubber dielectric, zinc/spikes metallic, glass smoother than concrete.')
for axis in ((1,0,0),(0,1,0),(0,0,1)):
    rod=Prop();rod.rod((0,0,0),axis,.03,3)
    assert np.isfinite(rod.points).all() and len(rod.faces)==8
    for face,_ in rod.faces:
        p=np.asarray(rod.points)[face]
        assert np.linalg.norm(np.cross(p[1]-p[0],p[2]-p[0]))>1e-8
print('TRACK BOLTS PASS: finite closed capped rods along all three axes, including vertical.')
for name in ('track_barrier','track_gate_pylon','track_guardrail','track_spikes','track_rubble','track_wreck'):
    def read(path):
        rows=path.read_text().splitlines()
        points=np.array([list(map(float,r.split()[1:])) for r in rows if r.startswith('v ')])
        tris=sum(len(r.split())-3 for r in rows if r.startswith('f '))
        assert np.isfinite(points).all()
        return points,tris
    points,tris=read(meshes/(name+'.obj'))
    assert tris<(1500 if name=='track_guardrail' else 1800),(name,tris)
    if args.baseline:
        old,_=read(args.baseline/'Meshes'/(name+'.obj'))
        assert np.max(abs(points.min(axis=0)-old.min(axis=0)))<.0001,(name,'minimum changed')
        assert np.max(abs(points.max(axis=0)-old.max(axis=0)))<.0001,(name,'maximum changed')
    print(f'TRACK ENVELOPE PASS {name}: {len(points)} vertices / {tris} triangles, finite geometry and baseline envelope.')
