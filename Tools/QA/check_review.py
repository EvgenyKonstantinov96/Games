"""Execute the briefing flow and sparse animation pose regression outside Unity."""
import argparse
from pathlib import Path
import subprocess
import tempfile
from check_toxic import block

ROOT = Path(__file__).resolve().parents[2]


def extract():
    source = (ROOT / "Assets/Scripts/ZSRBundledCharacter.cs").read_text()
    start = source.index("            if (spine != null && motion != Motion.Death &&")
    end = source.index("            ApplyUpperBodyAim(dt);", start)
    pose = [
        block(source, "enum Motion"),
        *[line for line in source.splitlines() if "static readonly int[]" in line],
        block(source, "void RestoreSampledPose()"),
        block(source, "void RestoreSupportPose()"),
        block(source, "void Sample()"),
        "void Steer() { " + source[start:end] + " }",
    ]
    game = (ROOT / "Assets/Scripts/ZSRGame.cs").read_text()
    run_guard = block(game, "void Update()")
    assert run_guard.index("if (State != ZSRState.Playing) return;") < run_guard.index("Elapsed += dt;")
    return ("using UnityEngine; namespace PoseCheck { partial class Actor { " + "\n".join(pose) +
            "} } namespace ZombieSurvivalRun { " +
            block(game, "public enum ZSRState") + " partial class ZSRGame { " +
            "\n".join(block(game, member) for member in
                      ("public void StartLevel(", "public void AdvanceDialogue()", "public void GoMenu()")) + "} }")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity-data", type=Path, required=True)
    editor = parser.parse_args().unity_data.resolve()
    runtime = editor / "NetCoreRuntime"
    shared = max((runtime / "shared/Microsoft.NETCore.App").iterdir())
    with tempfile.TemporaryDirectory() as temp:
        folder = Path(temp)
        exe = folder / "ReviewProbe.dll"
        code = folder / "Extracted.cs"
        code.write_text(extract())
        exe.with_suffix(".runtimeconfig.json").write_text(
            '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"' + shared.name + '"}}}')
        subprocess.run([runtime / "dotnet", editor / "DotNetSdkRoslyn/csc.dll",
                        "-nologo", "-noconfig", "-nostdlib", "-langversion:10", f"-out:{exe}",
                        *[f"-r:{dll}" for dll in sorted(shared.glob("*.dll"))], code,
                        ROOT / "Tools/QA/ReviewProbe.cs", ROOT / "Assets/Scripts/ZSRStory.cs"], check=True)
        subprocess.run([runtime / "dotnet", exe], check=True)


if __name__ == "__main__":
    main()
