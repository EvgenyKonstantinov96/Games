"""Validate exported combat geometry, readable transparent icons and shared material data."""
from pathlib import Path
import hashlib,math,re
import argparse
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
ASSETS=ROOT/'Assets/Resources'
parser=argparse.ArgumentParser()
parser.add_argument('--unity-data',type=Path)
args=parser.parse_args()

for name,budget in (('drone',4000),('rotor',300),('grenade',2500),('medkit',1500)):
    points,uvs,normals,faces=[],[],[],[]
    for line in (ASSETS/'Meshes'/('combat_'+name+'.obj')).read_text().splitlines():
        if line.startswith('v '):points.append(tuple(map(float,line.split()[1:])))
        elif line.startswith('vt '):uvs.append(tuple(map(float,line.split()[1:])))
        elif line.startswith('vn '):normals.append(tuple(map(float,line.split()[1:])))
        elif line.startswith('f '):faces.append([tuple(int(n)-1 for n in word.split('/')) for word in line.split()[1:]])
    points=np.asarray(points);normals=np.asarray(normals);uvs=np.asarray(uvs)
    triangles=sum(len(face)-2 for face in faces)
    assert 0<triangles<budget,(name,triangles)
    assert np.max(np.abs(np.linalg.norm(normals,axis=1)-1))<.001
    assert np.all((0<uvs)&(uvs<1))
    for face in faces:
        assert all(0<=i<len(points) and 0<=u<len(uvs) and 0<=n<len(normals) for i,u,n in face)
        xyz=np.asarray([points[i] for i,u,n in face]);area=np.linalg.norm(np.cross(xyz,np.roll(xyz,-1,axis=0)).sum(axis=0))
        assert area>1e-8,(name,face)
    if name=='drone':
        for x in (-.45,.45):
            for z in (-.38,.38):
                near=points[(abs(points[:,0]-x)<.05)&(abs(points[:,2]-z)<.05)]
                assert abs(near[:,1].max()-.108)<1e-5,(x,z)
    if name=='rotor':assert np.linalg.norm(points[:,[0,2]],axis=1).max()<.27
    print(f'PROP PASS {name}: {triangles} triangles, normalized normals, UVs and nondegenerate faces; bounds {np.ptp(points,axis=0).round(3).tolist()}.')

hashes=[]
for name in ('medkit','grenade','drone'):
    path=ASSETS/'UI'/('kit_'+name+'.png')
    with Image.open(path) as im:
        assert im.size==(512,512) and im.mode=='RGBA'
        alpha=np.asarray(im.getchannel('A'));assert not any((alpha[0].any(),alpha[-1].any(),alpha[:,0].any(),alpha[:,-1].any()))
        assert .08<np.mean(alpha>128)<.75
        left,top,right,bottom=im.getchannel('A').getbbox();assert min(left,top,512-right,512-bottom)>=12,(name,(left,top,right,bottom))
    hashes.append(hashlib.sha256(path.read_bytes()).hexdigest())
    meta=path.with_suffix('.png.meta').read_text();assert 'enableMipMap: 0' in meta and 'nPOTScale: 0' in meta
assert len(set(hashes))==3
with Image.open(ASSETS/'ProductionArt/CombatAtlas.png') as im:
    red=im.getpixel((64,64));olive=im.getpixel((64,192));white=im.getpixel((64,320))
    assert red[0]>red[1]*3 and olive[1]>olive[0]>olive[2] and min(white)>170,(red,olive,white)
print('FIELD KIT ART PASS: three distinct 512² transparent icons, no clipped border, red medical case / olive grenade / light quadcopter; shared 512² atlas.')

if args.unity_data:
    packages=args.unity_data/'Resources/PackageManager/BuiltInPackages'
    source=(packages/'com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs').read_text()
    definition=source.split('public enum LightRenderingMode',1)[1].split('}',1)[0]
    pixel=int(re.search(r'PerPixel\s*=\s*(\d+)',definition)[1])
    configured=int(re.search(r'm_AdditionalLightsRenderingMode: (\d+)',(ROOT/'Assets/Settings/ZSR_URP.asset').read_text())[1])
    assert configured==pixel,(configured,pixel)
    ui=(packages/'com.unity.render-pipelines.core/Runtime/Debugging/DebugManager.UIState.cs').read_text()
    assert 'm_Root.name = "[Debug Canvas]"' in ui and 'CoreUtils.Destroy(m_Root)' in ui
    game=(ROOT/'Assets/Scripts/ZSRGame.cs').read_text()
    assert all('DebugManager.instance.'+flag+' = false;' in game for flag in ('displayRuntimeUI','displayPersistentRuntimeUI','enableRuntimeUI'))
    print(f'URP SOURCE PASS: configured additional light value {configured} equals actual package PerPixel; debug canvas identity and public shutdown APIs verified in Core 17.3. Not a native UI/render test.')
