"""Exercise the actual ponytail update with Unity vector math and scene stand-ins."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile
from check_toxic import block

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument("--unity-data",type=Path,required=True)
editor=parser.parse_args().unity_data.resolve()
runtime=editor/"NetCoreRuntime"
shared=max((runtime/"shared/Microsoft.NETCore.App").iterdir())
with tempfile.TemporaryDirectory() as temp:
    folder=Path(temp)
    core=Path(shutil.copy(editor/"Managed/UnityEngine/UnityEngine.CoreModule.dll",folder))
    shutil.copy(editor/"Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll",folder)
    source=(ROOT/"Assets/Scripts/ZSRSecondaryMotion.cs").read_text()
    code=folder/"Extracted.cs"
    code.write_text("using UnityEngine; namespace ZombieSurvivalRun { partial class ZSRSecondaryMotion {"+
                    block(source,"void LateUpdate()")+"}}")
    exe=folder/"SecondaryMotionProbe.dll"
    exe.with_suffix(".runtimeconfig.json").write_text(
        '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"'+shared.name+'"}}}')
    subprocess.run([runtime/"dotnet",editor/"DotNetSdkRoslyn/csc.dll","-nologo","-noconfig","-nostdlib",
                    "-langversion:10",f"-out:{exe}",f"-r:engine={core}",
                    *[f"-r:{p}" for p in shared.glob("*.dll")],code,
                    ROOT/"Tools/QA/SecondaryMotionProbe.cs"],check=True)
    subprocess.run([runtime/"dotnet",exe],check=True)
