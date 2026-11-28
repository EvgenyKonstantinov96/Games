"""Execute actual interface layout and callbacks with explicit managed UI stand-ins."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--unity-data',type=Path,required=True)
parser.add_argument('--output',type=Path,help='Optional output directory for layout JSON previews')
args=parser.parse_args()
data=args.unity_data.resolve()
runtime=data/'NetCoreRuntime'
shared=max((runtime/'shared/Microsoft.NETCore.App').iterdir(),key=lambda p:tuple(map(int,p.name.split('.'))))
with tempfile.TemporaryDirectory() as folder:
    folder=Path(folder)
    output=args.output.resolve() if args.output else folder
    output.mkdir(parents=True,exist_ok=True)
    assembly=folder/'InterfaceProbe.dll'
    assembly.with_suffix('.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions':{'framework':{'name':'Microsoft.NETCore.App','version':shared.name}}}))
    sources=['ZSRInterface','ZSRCampaignStyle','ZSRStory','ZSRLevelPlan','ZSRRunPace','ZSRWeaponProfile']
    subprocess.run([runtime/'dotnet',data/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib',
        '-define:UNITY_EDITOR','-langversion:10.0',f'-out:{assembly}',
        *[f'-r:{p}' for p in shared.glob('*.dll')],
        ROOT/'Tools/QA/InterfaceProbeShims.cs',ROOT/'Tools/QA/InterfaceProbe.cs',
        *[ROOT/'Assets/Scripts'/(name+'.cs') for name in sources]],check=True)
    env=dict(os.environ,ZSR_QA_OUTPUT=str(output),ZSR_QA_RESOURCES=str(ROOT/'Assets/Resources'))
    subprocess.run([runtime/'dotnet',assembly],env=env,check=True)
