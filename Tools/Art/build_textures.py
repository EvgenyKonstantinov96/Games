"""Build original mobile textures. Requires NumPy and Pillow."""
from argparse import ArgumentParser
from io import BytesIO
from pathlib import Path
from zipfile import ZipFile
import numpy as np
from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "Assets/Resources/ProductionArt"
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(7341)
PAL = [
    (67,69,53), (114,41,36), (49,57,63), (35,39,43), (64,75,84), (189,103,38),
    (68,69,52), (57,52,52), (104,82,59), (158,110,84), (165,119,102), (107,110,82),
    (124,119,79), (115,82,77), (101,64,68), (93,47,44), (179,172,145), (49,31,25),
    (74,70,53), (148,135,110), (41,38,31), (143,193,45), (235,63,37), (80,76,70),
]
parser = ArgumentParser()
parser.add_argument("--skin-pack", type=Path, required=True)
args = parser.parse_args()
N = 64
atlas = Image.new("RGB", (1024,1024), "#302e2c")
y,x = np.mgrid[:N,:N]
for i, rgb in enumerate(PAL):
    noise = rng.normal(0,3.4,(N,N)) + 3*np.sin(x*.24+y*.15)
    if i in (0,1,2,6,7,8):
        noise += 3*((x+y)%3==0) - 1.9*((x-y)%4==0)
    if i in (9,10,11,12,13,14):
        noise += 2.2*np.sin(x*.32+y*.17) + 1.4*np.cos(y*.27)
    tile = np.uint8(np.clip(np.array(rgb)[None,None,:] + noise[:,:,None],0,255))
    im=Image.fromarray(tile)
    draw=ImageDraw.Draw(im)
    if i in (0,1,2,3,4,5,6,7,8):
        for _ in range(16):
            a,b=rng.integers(2,62,size=2)
            d=int(rng.integers(2,10))
            c=tuple(min(255, int(v+d)) for v in rgb)
            draw.line((a,b,min(62,a+d//2),max(0,b-d)),fill=c,width=1)
    atlas.paste(im.resize((128,128),Image.Resampling.BILINEAR),((i%8)*128,(i//8)*128))
skins = [
    ("young_caucasian_male", (1.00,.93,.87)),
    ("young_caucasian_female", (1.00,.94,.89)),
    ("old_caucasian_male", (.73,.78,.66)),
    ("young_asian_male", (.73,.76,.66)),
    ("middleage_african_male", (.84,.85,.80)),
    ("old_asian_male", (.64,.92,.45)),
    ("old_caucasian_female", (.68,.72,.69)),
    ("middleage_caucasian_male", (.94,.62,.60)),
]
with ZipFile(args.skin_pack) as pack:
    for i, (folder, tint) in enumerate(skins):
        name, = (f for f in pack.namelist()
                 if f.startswith("skins/" + folder + "/") and f.endswith(".png"))
        source = Image.open(BytesIO(pack.read(name))).convert("RGB").resize((256,256), Image.Resampling.LANCZOS)
        pixels = np.asarray(source) * np.asarray(tint)
        if i >= 2:
            # Kill the even studio skin finish on infected variants.
            y,x = np.mgrid[:256,:256]
            mottling = np.sin(x*.17+y*.23)*np.cos(y*.22-x*.08)
            pixels *= 1 + .044*mottling[:,:,None]
        atlas.paste(Image.fromarray(np.uint8(np.clip(pixels,0,255))), (i%4*256,512+i//4*256))
    for i, name in enumerate(("male_casualsuit04", "female_casualsuit01")):
        path=f"clothes/{name}/{name}_diffuse.png"
        source=Image.open(BytesIO(pack.read(path))).convert("L").resize((256,128),Image.Resampling.LANCZOS)
        grays = np.asarray(source,dtype=float) / 255
        cloth=np.uint8(28 + grays[:,:,None] * np.asarray([48,53,58]))
        atlas.paste(Image.fromarray(cloth),(i*256,384))
atlas.save(OUT/"CharacterAtlas.png",optimize=True)

n=512
y,x=np.mgrid[:n,:n]
xx=x+5*np.sin(2*np.pi*y/n*4)+2.6*np.sin(2*np.pi*(x+y)/n*11)
yy=y+5*np.sin(2*np.pi*x/n*3)+2.7*np.sin(2*np.pi*(x-y)/n*13)
near=np.full(x.shape, float("inf"))
second=near.copy()
for a,b in rng.uniform(0,n,(24,2)):
    dx=(xx-a+n*.5)%n-n*.5
    dy=(yy-b+n*.5)%n-n*.5
    d=dx*dx+dy*dy
    second=np.minimum(second,np.maximum(near,d))
    near=np.minimum(near,d)
edge=(np.sqrt(second)-np.sqrt(near))*.5
crack=np.exp(-edge*1.55)
wear=np.sin(x/n*2*np.pi)*np.cos(y/n*6*np.pi)
chip=rng.normal(0,1.55,(n,n))+1.9*wear
color=np.array([54,57,62])[None,None,:] + (chip-20*crack)[:,:,None]
Image.fromarray(np.uint8(np.clip(color,0,255))).save(OUT/"Highway.png",optimize=True)
height=-.095*crack+rng.normal(0,.005,(n,n))
dy=(np.roll(height,-1,0)-np.roll(height,1,0))*2.0
dx=(np.roll(height,-1,1)-np.roll(height,1,1))*2.0
normals=np.stack([-dx,dy,np.ones_like(dx)],axis=-1)
normals/=np.linalg.norm(normals,axis=-1,keepdims=True)
Image.fromarray(np.uint8(np.clip(normals*127.5+127.5,0,255))).save(OUT/"Highway_normal.png",optimize=True)

# Single feathered mask shared by flame, sparks and smoke particle renderers.
n=64
y,x=np.mgrid[:n,:n]
u,v=(x+.5-n*.5)/(n*.5),(y+.5-n*.5)/(n*.5)
radius=np.hypot(u,v)+.075*np.cos(9*u+5*v)*np.sin(7*v)
alpha=np.clip(1-radius,0,1)**1.8
particle=np.full((n,n,4),255,dtype=np.uint8)
particle[:,:,3]=np.uint8(255*alpha)
Image.fromarray(particle).save(OUT/"ParticleSoft.png",optimize=True)
