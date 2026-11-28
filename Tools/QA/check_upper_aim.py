"""Execute the production shoulder aiming controller with Unity managed vector math."""
import argparse,json,subprocess,tempfile,shutil
from pathlib import Path
from check_toxic import block
ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser();parser.add_argument('--unity-data',type=Path,required=True)
data=parser.parse_args().unity_data
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
    start=source.index('            if (spine != null && motion != Motion.Death &&')
    end=source.index('            ApplyUpperBodyAim(dt);',start)
    code.write_text('using UnityEngine; namespace ZombieSurvivalRun { partial class AimRig {'+
        block(source,'enum Motion')+block(source,'void RestoreSampledPose()')+block(source,'void ApplyUpperBodyAim(float dt)')+block(source,'void ApplyAim()')+
        'void ApplySteering(){'+source[start:end]+'}}}')
    rig=folder/'UpperRigProbe.dll';shutil.copy(exe.with_suffix('.runtimeconfig.json'),rig.with_suffix('.runtimeconfig.json'))
    subprocess.run([runtime/'dotnet',data/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-langversion:10',
        f'-out:{rig}',f'-r:engine={core}',*[f'-r:{p}' for p in shared.glob('*.dll')],code,
        ROOT/'Assets/Scripts/ZSRUpperBodyAim.cs',ROOT/'Tools/QA/UpperRigProbe.cs'],check=True)
    subprocess.run([runtime/'dotnet',rig],check=True)
