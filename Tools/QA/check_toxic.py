"""Run Toxic attacks, pause and pooling outside PlayMode with entity/renderer stand-ins."""
import argparse
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets/Scripts"


def block(source, name):
    """Extract the class member whose body follows the given signature."""
    start = source.index(name)
    opening = source.index("{", start)
    depth = 0
    for end in range(opening, len(source)):
        token = source[end]
        depth += int(token == "{") - int(token == "}")
        if depth == 0:
            return source[start:end + 1]
    raise ValueError(f"Unclosed member {name}")


def expression(source, signature):
    start = source.index(signature)
    return source[start:source.index(";", start) + 1]


def extract():
    actor = (SCRIPTS / "ZSRActors.cs").read_text(encoding="utf-8")
    zombie = actor[actor.index("public sealed class ZSRZombie"):]
    fx = (SCRIPTS / "ZSRCombatEffects.cs").read_text(encoding="utf-8")
    game = (SCRIPTS / "ZSRGame.cs").read_text(encoding="utf-8")
    late = block(game, "void LateUpdate()")
    late = "\n".join(line for line in late.splitlines()
                     if "music." not in line and "jitter" not in line)
    prefix = block(game, "void Update()").split("foreach (var chunk in road)", 1)[0] + "}"
    fx_tick = block(fx, "public void Tick(").split("for (int i = 0; i < traces.Length", 1)[0] + "}"
    return "\n".join((
        "using System.Collections.Generic; using UnityEngine; using UnityEngine.InputSystem;",
        "namespace ZombieSurvivalRun {",
        block(actor, "public enum ZombieType"),
        block(game, "public enum ZSRState"),
        block(game, "public enum ItemType"),
        "public sealed partial class ZSRZombie : MonoBehaviour {",
        zombie[zombie.index("public ZombieType Kind"):zombie.index("public void Construct")],
        *[block(zombie, member) for member in (
            "internal void Activate", "void Fall()", "void FaceTravel()", "void Update()", "void TelegraphAttack")],
        "}",
        "sealed partial class ZSRCombatEffects {",
        fx[fx.index("readonly SpitSlot[] spits"):fx.index("int nextTrace")],
        *[block(fx, member) for member in ("public void LaunchSpit(", "public void TickSpits(", "public void SetPaused(")],
        fx_tick, "}",
        "sealed partial class ZSRGame {",
        *[expression(game, member) for member in (
            "public bool IsRunning", "public int Level =>", "public float RunSpeed", "public float FrameStep",
            "public bool IsBossLevel", "public bool IsShielded", "internal void ToxicBurst(", "internal void LaunchToxicSpit(")],
        prefix, late,
        *[block(game, member) for member in ("void SpawnEnemy(", "public void RecycleZombie(", "public void TogglePause(", "public void HurtPlayer(")],
        "}",
        "}",
    ))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--unity-data", type=Path, required=True, help="Unity Editor/Data directory")
    editor = parser.parse_args().unity_data.resolve()
    runtime = editor / "NetCoreRuntime"
    shared = max((runtime / "shared/Microsoft.NETCore.App").iterdir(), key=lambda p: tuple(map(int, p.name.split("."))))
    dotnet = runtime / "dotnet"
    if not dotnet.exists():
        dotnet = runtime / "dotnet.exe"

    with tempfile.TemporaryDirectory() as folder:
        folder = Path(folder)
        exe = folder / "ToxicSpitProbe.dll"
        extracted = folder / "Extracted.cs"
        extracted.write_text(extract(), encoding="utf-8")
        engine = Path(shutil.copy(editor / "Managed/UnityEngine/UnityEngine.CoreModule.dll", folder))
        shutil.copy(editor / "Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll", folder)
        exe.with_suffix(".runtimeconfig.json").write_text(
            '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"' + shared.name + '"}}}',
            encoding="utf-8")
        subprocess.run([dotnet, editor / "DotNetSdkRoslyn/csc.dll", "-nologo", "-noconfig", "-nostdlib",
                        "-langversion:10", f"-out:{exe}", f"-r:engine={engine}",
                        *[f"-r:{dll}" for dll in sorted(shared.glob("*.dll"))],
                        *(SCRIPTS / (name + ".cs") for name in ("ZSRInfectedMotion", "ZSRRunPace", "ZSRLevelPlan", "ZSRStory")),
                        extracted, ROOT / "Tools/QA/ToxicSpitProbe.cs"], check=True)
        subprocess.run([dotnet, exe], check=True)


if __name__ == "__main__":
    main()
