"""Export actual campaign speed/cadence using Unity managed math, without a scene."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
from check_toxic import block

ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--unity-data', type=Path, required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args()
data = args.unity_data.resolve()
runtime = data / 'NetCoreRuntime'
shared = max((runtime / 'shared/Microsoft.NETCore.App').iterdir(),
             key=lambda p: tuple(map(int, p.name.split('.'))))
with tempfile.TemporaryDirectory() as temp:
    folder = Path(temp)
    exe = folder / 'Cadence.dll'
    source = folder / 'Cadence.cs'
    core = Path(shutil.copy(data / 'Managed/UnityEngine/UnityEngine.CoreModule.dll', folder))
    shutil.copy(data / 'Managed/UnityEngine/UnityEngine.SharedInternalsModule.dll', folder)
    actors = (ROOT / 'Assets/Scripts/ZSRActors.cs').read_text()
    actor_enums = block(actors, 'public enum WeaponType') + block(actors, 'public enum ZombieType')
    source.write_text('''using System; using System.Globalization; using ZombieSurvivalRun;
namespace ZombieSurvivalRun { ''' + actor_enums + ''' }
class CadenceProbe {
    static void Main() {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        foreach (int level in new[]{1,15,1000}) {
            float road = new ZSRLevelPlan(level).Speed;
            foreach (int hero in new[]{0,1}) foreach (var mode in new[]{ZSRPace.Run,ZSRPace.Surge,ZSRPace.Walk}) {
                float scale = mode==ZSRPace.Surge ? ZSRRunPace.SurgeScale : mode==ZSRPace.Walk ? ZSRRunPace.WalkScale : 1;
                float cadence = ZSRHeroGait.Cadence(hero,road,scale,0,mode==ZSRPace.Walk);
                Console.WriteLine($"{level},{(hero==0?"Max":"Eva")},{mode},{road*scale:R},{cadence:R}");
            }
        }
    }
}''')
    exe.with_suffix('.runtimeconfig.json').write_text(json.dumps({
        'runtimeOptions': {'framework': {'name': 'Microsoft.NETCore.App', 'version': shared.name}}}))
    scripts = ROOT / 'Assets/Scripts'
    subprocess.run([runtime / 'dotnet', data / 'DotNetSdkRoslyn/csc.dll', '-nologo', '-noconfig',
                    '-nostdlib', '-langversion:10', f'-out:{exe}', f'-r:{core}',
                    *[f'-r:{p}' for p in shared.glob('*.dll')], source,
                    *[scripts / name for name in ('ZSRRunPace.cs', 'ZSRLevelPlan.cs',
                                                  'ZSRStory.cs', 'ZSRWeaponProfile.cs')]], check=True)
    rows = subprocess.check_output([runtime / 'dotnet', exe], text=True).splitlines()
    results = []
    for row in rows:
        level, hero, mode, road, cadence = row.split(',')
        results.append(dict(level=int(level), model=hero, mode=mode,
                            road_speed=float(road), runtime_cadence=float(cadence)))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(results, indent=2) + '\n')
    print(f'EXPORTED {len(results)} actual campaign/cadence rows; steady modes, zero lateral speed, no native scene.')
