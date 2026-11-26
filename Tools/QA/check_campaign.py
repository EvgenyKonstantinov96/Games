"""Run the Unity-independent campaign checks with the editor's bundled Roslyn."""
import argparse
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets/Scripts"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity-data", type=Path, required=True, help="Unity Editor/Data directory")
    args = parser.parse_args()
    editor = args.unity_data.resolve()
    runtime = editor / "NetCoreRuntime"
    shared = max((runtime / "shared/Microsoft.NETCore.App").iterdir(), key=lambda p: tuple(map(int, p.name.split("."))))
    dotnet = runtime / "dotnet"
    if not dotnet.exists():
        dotnet = runtime / "dotnet.exe"
    sources = [SCRIPTS / (name + ".cs") for name in ("ZSRLevelPlan", "ZSRStory", "ZSRWeaponProfile", "ZSRRunPace")]
    enums = []
    for symbol in ("ZombieType", "WeaponType"):
        for path in SCRIPTS.glob("*.cs"):
            match = re.search(r"public enum " + symbol + r"\s*\{[^}]+\}", path.read_text(encoding="utf-8"))
            if match:
                enums.append(match[0])
                break
        else:
            raise ValueError(f"Enum {symbol} is missing")

    game = (SCRIPTS / "ZSRGame.cs").read_text(encoding="utf-8")
    start = game.index("        internal static float SpaceTraffic(")
    end = game.index("        public void ApplyBonus(", start)
    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        exe = folder / "CampaignProbe.dll"
        extracted = folder / "Extracted.cs"
        extracted.write_text("using System.Collections.Generic; using UnityEngine; namespace ZombieSurvivalRun { " +
                             " ".join(enums) + " static class Traffic { " + game[start:end] + "} }",
                             encoding="utf-8")
        exe.with_suffix(".runtimeconfig.json").write_text(
            '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"' + shared.name + '"}}}',
            encoding="utf-8")
        subprocess.run([dotnet, editor / "DotNetSdkRoslyn/csc.dll", "-nologo", "-noconfig", "-nostdlib",
                        "-langversion:9.0", f"-out:{exe}", *[f"-r:{dll}" for dll in sorted(shared.glob("*.dll"))],
                        *sources, extracted, ROOT / "Tools/QA/CampaignProbe.cs"], check=True)
        subprocess.run([dotnet, exe], check=True)


if __name__ == "__main__":
    main()
