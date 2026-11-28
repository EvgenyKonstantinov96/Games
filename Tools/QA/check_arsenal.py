"""Execute actual arsenal purchase/upgrade methods with an explicit in-memory PlayerPrefs."""
import argparse,json,subprocess,tempfile
from pathlib import Path
from check_toxic import block,expression

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--unity-data',required=True,type=Path)
data=parser.parse_args().unity_data
runtime=data/'NetCoreRuntime'
shared=max((runtime/'shared/Microsoft.NETCore.App').iterdir(),key=lambda p:tuple(map(int,p.name.split('.'))))
game=(ROOT/'Assets/Scripts/ZSRGame.cs').read_text()
assert game.count('ZSRWeaponProfile.RestoreSelection(Weapon, UnlockedLevel, WeaponOwned(Weapon))')==2
assert 'PlayerPrefs.GetInt("zsr40_weapon", 0)' in game
methods='\n'.join([expression(game,s) for s in (
 'public int WeaponUnlockLevel(', 'public int WeaponPrice(', 'public bool WeaponLevelUnlocked(',
 'public bool WeaponOwned(', 'internal int WeaponLevelFor(', 'internal int WeaponUpgradeCost(')])
methods+='\n'+block(game,'public void SelectWeapon(')+'\n'+block(game,'internal void UpgradeWeapon(WeaponType')
enums=(ROOT/'Assets/Scripts/ZSRActors.cs').read_text()
with tempfile.TemporaryDirectory() as folder:
 folder=Path(folder);exe=folder/'ArsenalProbe.dll';source=folder/'Extracted.cs'
 source.write_text('using UnityEngine; namespace ZombieSurvivalRun { '+
  block(enums,'public enum WeaponType')+block(enums,'public enum ZombieType')+
  'partial class ZSRGame { '+methods+' } }')
 exe.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'framework':{'name':'Microsoft.NETCore.App','version':shared.name}}}))
 subprocess.run([runtime/'dotnet',data/'DotNetSdkRoslyn/csc.dll','-nologo','-noconfig','-nostdlib','-langversion:10',f'-out:{exe}',
  *[f'-r:{p}' for p in shared.glob('*.dll')],source,ROOT/'Tools/QA/ArsenalProbe.cs',
  *[ROOT/'Assets/Scripts'/(name+'.cs') for name in ('ZSRWeaponProfile','ZSRLevelPlan','ZSRStory')]],check=True)
 subprocess.run([runtime/'dotnet',exe],check=True)
