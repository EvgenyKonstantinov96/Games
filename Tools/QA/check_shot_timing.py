"""Execute actual shot/LateUpdate control flow with explicit scene/graph observers.

This measures source scheduling, not native Animator, render timing or Game View.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
from check_toxic import block

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--unity-data", type=Path, required=True)
args = parser.parse_args()
editor = args.unity_data.resolve()
runtime = editor / "NetCoreRuntime"
shared = max((runtime / "shared/Microsoft.NETCore.App").iterdir(), key=lambda p: tuple(map(int, p.name.split("."))))
source = (ROOT / "Assets/Scripts/ZSRBundledCharacter.cs").read_text()
actors = (ROOT / "Assets/Scripts/ZSRActors.cs").read_text()
mount = block(source, "void AttachWeapon(")
kick = mount[mount.index("            kick = weapon switch"):mount.index("            var gun =")]
members = [block(source, signature) for signature in (
    "enum Motion", "public void Fire(", "public void ResetMotion(", "public void Die()",
    "void Change(", "void HideFlash()", "void LateUpdate()")]
members += [line.strip() for line in source.splitlines() if line.strip().startswith((
    "const float Fps", "static readonly int[]", "Motion Locomotion =>", "Motion Shooting =>",
    "static bool IsShooting(", "static bool IsGait("))]
with tempfile.TemporaryDirectory() as temp:
    folder = Path(temp)
    executable = folder / "ShotTimingProbe.dll"
    extracted = folder / "Extracted.cs"
    extracted.write_text("using UnityEngine; namespace ZombieSurvivalRun { " +
                         block(actors, "public enum WeaponType") + " partial class ShotActor { " +
                         "\n".join(members) + " void ConfigureWeapon(WeaponType weapon) { " + kick + " } } }")
    engine = Path(shutil.copy(editor / "Managed/UnityEngine/UnityEngine.CoreModule.dll", folder))
    shutil.copy(editor / "Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll", folder)
    executable.with_suffix(".runtimeconfig.json").write_text(json.dumps({
        "runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App", "version": shared.name}}}))
    subprocess.run([runtime / "dotnet", editor / "DotNetSdkRoslyn/csc.dll", "-nologo", "-noconfig",
                    "-nostdlib", "-langversion:10", f"-out:{executable}", f"-r:engine={engine}",
                    *[f"-r:{dll}" for dll in shared.glob("*.dll")], extracted,
                    ROOT / "Tools/QA/ShotTimingProbe.cs", ROOT / "Assets/Scripts/ZSRRunPace.cs",
                    ROOT / "Assets/Scripts/ZSRUpperBodyAim.cs"], check=True)
    subprocess.run([runtime / "dotnet", executable], check=True)
