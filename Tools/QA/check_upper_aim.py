"""Execute the production shoulder aiming controller with Unity managed vector math."""
import argparse,hashlib,json,subprocess,tempfile,shutil
from pathlib import Path
from check_toxic import block
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--unity-data',type=Path,required=True)
parser.add_argument('--fbx-samples',type=Path,help='Optional export_grip_samples.py output, from shipped Max/Eva FBXs')
args=parser.parse_args();data=args.unity_data
if args.fbx_samples:
    dataset=json.loads(args.fbx_samples.read_text())
    assert {model['name'] for model in dataset['models']}=={'Max','Eva'},'Expected both shipped survivor rigs'
    for model in dataset['models']:
        fbx=ROOT/'Assets/Resources/ProductionArt'/f"{model['name']}.fbx"
        assert model['sha256']==hashlib.sha256(fbx.read_bytes()).hexdigest(),f'{fbx.name} changed; rerun export_grip_samples.py'
runtime=data/'NetCoreRuntime'
shared=max((runtime/'shared/Microsoft.NETCore.App').iterdir(),key=lambda p:tuple(map(int,p.name.split('.'))))
with tempfile.TemporaryDirectory() as folder:
    folder=Path(folder);exe=folder/'UpperAimProbe.dll'
    core=Path(shutil.copy(data/'Managed/UnityEngine/UnityEngine.CoreModule.dll',folder))
    shutil.copy(data/'Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll',folder)
    exe.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'framework':{'name':'Microsoft.NETCore.App','version':shared.name}}}))
    subprocess.run([runtime/'dotnet',data/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-langversion:10',
        f'-out:{exe}',f'-r:{core}',*[f'-r:{p}' for p in shared.glob('*.dll')],
        ROOT/'Assets/Scripts/ZSRUpperBodyAim.cs',ROOT/'Tools/QA/UpperAimProbe.cs'],check=True)
    subprocess.run([runtime/'dotnet',exe],check=True)
    code=folder/'Extracted.cs'
    source=(ROOT/'Assets/Scripts/ZSRBundledCharacter.cs').read_text()
    # Execute the actual calibration prefix without constructing renderer meshes.
    mount=block(source,'void AttachWeapon(')
    weapon_kick=mount[mount.index('            kick = weapon switch'):mount.index('            var gun =')]
    mount=mount[:mount.index('            kick = weapon switch')]+'}'
    start=source.index('            if (spine != null && motion != Motion.Death &&')
    end=source.index('            ApplyUpperBodyAim(dt);',start)
    code.write_text('using UnityEngine; namespace ZombieSurvivalRun { partial class AimRig {'+
        block(source,'enum Motion')+block(source,'void RestoreSampledPose()')+block(source,'void ApplyUpperBodyAim(float dt)')+block(source,'void ApplyAim()')+
        block(source,'void RestoreSupportPose()')+block(source,'void ApplySupportGrip(float dt)')+
        ''.join(block(source,signature) for signature in ('public void Fire(', 'public void Reload(',
            'public void UseItem(', 'public void Die()', 'void Change(', 'void HideFlash()'))+
        'void DecayRecoil(float dt) {'+next(line.strip() for line in source.splitlines()
            if line.strip().startswith('recoil = Mathf.MoveTowards(recoil'))+'}'+
        'void ConfigureWeapon(WeaponType weapon) {'+weapon_kick+'}'+
        mount+block(source,'static Transform FindBone(')+
        next(line.strip() for line in source.splitlines() if line.strip().startswith('Motion Shooting =>'))+
        next(line.strip() for line in source.splitlines() if line.strip().startswith('static bool IsGait(Motion value)'))+
        'void ApplySteering(){'+source[start:end]+'}}}')
    rig=folder/'UpperRigProbe.dll';shutil.copy(exe.with_suffix('.runtimeconfig.json'),rig.with_suffix('.runtimeconfig.json'))
    subprocess.run([runtime/'dotnet',data/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-langversion:10',
        f'-out:{rig}',f'-r:engine={core}',*[f'-r:{p}' for p in shared.glob('*.dll')],code,
        ROOT/'Assets/Scripts/ZSRUpperBodyAim.cs',ROOT/'Tools/QA/UpperRigProbe.cs'],check=True)
    subprocess.run([runtime/'dotnet',rig,*([args.fbx_samples.resolve()] if args.fbx_samples else [])],check=True)
