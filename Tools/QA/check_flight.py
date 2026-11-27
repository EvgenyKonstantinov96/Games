"""Compile the game's grenade flight and run deterministic throw checks outside PlayMode."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity-data", required=True, type=Path, help="Unity Editor/Data directory")
    editor = parser.parse_args().unity_data.resolve()
    runtime = editor / "NetCoreRuntime"
    shared = max((runtime / "shared/Microsoft.NETCore.App").iterdir(), key=lambda p: tuple(map(int, p.name.split("."))))
    dotnet = runtime / "dotnet"
    if not dotnet.exists():
        dotnet = runtime / "dotnet.exe"

    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        exe = folder / "FlightProbe.dll"
        engine = Path(shutil.copy(editor / "Managed/UnityEngine/UnityEngine.CoreModule.dll", folder))
        exe.with_suffix(".runtimeconfig.json").write_text(
            '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"' + shared.name + '"}}}',
            encoding="utf-8")
        subprocess.run([dotnet, editor / "DotNetSdkRoslyn/csc.dll", "-nologo", "-noconfig", "-nostdlib",
                        "-langversion:10", f"-out:{exe}", f"-r:engine={engine}",
                        *[f"-r:{dll}" for dll in sorted(shared.glob("*.dll"))],
                        ROOT / "Assets/Scripts/ZSRGrenadeFlight.cs", ROOT / "Tools/QA/FlightProbe.cs"], check=True)
        subprocess.run([dotnet, exe], check=True)


if __name__ == "__main__":
    main()
