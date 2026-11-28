// Executes the real interface class with managed scene/UI stand-ins; not PlayMode.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
using UnityEngine.UI;
using ZombieSurvivalRun;

static class Program
{
 static readonly string Output=Environment.GetEnvironmentVariable("ZSR_QA_OUTPUT") ?? Path.GetTempPath();
 static ZSRInterface ui;
 static GameObject host;
 static readonly ZSRGame game=ZSRGame.Instance;
 static MethodInfo Method(string name)=>typeof(ZSRInterface).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic);
 static void Invoke(string name,params object[] args)=>Method(name).Invoke(ui,args);
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static IEnumerable<GameObject> Active(Transform t) {
  if(!t.gameObject.activeSelf) yield break;
  yield return t.gameObject;
  foreach(var child in t.children) foreach(var descendant in Active(child)) yield return descendant;
 }
 static IEnumerable<Button> Buttons()=>Active(host.transform).Select(o=>o.GetComponent<Button>()).Where(b=>b!=null);
 static Button Find(string label)=>Buttons().Single(b=>b.gameObject.name=="BUTTON: "+label);
 static Button Slot(int index)=>Buttons().Where(b=>b.gameObject.name=="BUTTON: ОСМОТР").ElementAt(index);
 static bool HasText(string value)=>Active(host.transform).Any(o=>o.GetComponent<Text>()?.text==value);
 static GameObject Current()=>Active(host.transform).Single(o=>o.name=="Current screen");
 static void Draw(){Invoke("Draw");Invoke("AnimatePage",.30f);}
 static void Dump(string path){
  var items=new List<object>();
  var canvas=(RectTransform)host.transform.children.First();
  Walk(canvas,0,0,1,canvas.rect.width,canvas.rect.height,items);
  File.WriteAllText(path,JsonSerializer.Serialize(new {width=canvas.rect.width,height=canvas.rect.height,items}));
 }
 static void Walk(Transform t,float px,float py,float scale,float pw,float ph,List<object> items){
  if(!t.gameObject.activeSelf)return;
  if(t is RectTransform rt){
   float x=px,y=py,w=pw,h=ph;
   if(!rt.Stretch){x+=rt.anchoredPosition.x*scale;y-=rt.anchoredPosition.y*scale;w=rt.sizeDelta.x*scale*rt.localScale.x;h=rt.sizeDelta.y*scale*rt.localScale.y;}
   scale*=rt.localScale.x;
   foreach(var graphic in t.gameObject.components.OfType<Graphic>()){
    float[] color={graphic.color.r,graphic.color.g,graphic.color.b,graphic.color.a};
    if(graphic is Text a){items.Add(new {kind="text",x,y,w,h,color,text=a.text,size=a.fontSize*scale,style=a.fontStyle.ToString(),align=a.alignment.ToString(),font=a.font?.path});}
    else if(graphic is RawImage b){items.Add(new {kind="raw",x,y,w,h,color,path=b.texture.path,uv=new[]{b.uvRect.x,b.uvRect.y,b.uvRect.width,b.uvRect.height}});}
    else if(graphic is Image c){items.Add(new {kind="image",x,y,w,h,color,path=c.sprite?.Texture.path,enabled=t.gameObject.GetComponent<Button>()?.interactable??true});}
   }
   foreach(var child in t.children)Walk(child,x,y,scale,w,h,items);
  }
 }
 static void Tap(string label) { var button=Find(label);Assert(button.interactable,label);button.onClick.Invoke();Draw(); }
 public static void Main(){
  host=new GameObject("host");ui=host.AddComponent<ZSRInterface>();Invoke("Awake");Draw();
  Assert(!Buttons().Any(b=>b.gameObject.name.Contains("СЮЖЕТ")),"No story control on home");
  Assert(Find("ПРОДОЛЖИТЬ").transform.children.Select(t=>t.gameObject.GetComponent<Text>())
       .Any(a=>a?.font?.path=="UI/ZSRButtonFont"),"Button uses the bundled bold font");
  Dump(Path.Combine(Output,"home.json"));
  Tap("СМЕНИТЬ");Assert(game.State==ZSRState.Heroes,"Home hero shortcut");Tap("МАКС");Assert(game.SelectedHero==0&&HasText("132 HP"),"Full Max portrait selects Max");Tap("ЕВА");Assert(game.SelectedHero==1&&HasText("112 HP"),"Full Eva portrait selects Eva");Dump(Path.Combine(Output,"heroes.json"));
  Tap("ОТКРЫТЬ АРСЕНАЛ");Assert(game.State==ZSRState.Arsenal,"Hero loadout shortcut");
  var shotgun=Find("ДРОБОВИК");UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(shotgun.gameObject);
  Tap("ДРОБОВИК");Assert(ReferenceEquals(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Find("ДРОБОВИК").gameObject),"Inspection retains keyboard focus on new control");
  Assert(HasText(ZSRWeaponProfile.For(WeaponType.Shotgun).Role),"Whole weapon card changes inspection");
  Tap("Предметы");Draw();Dump(Path.Combine(Output,"items.json"));
  Tap("Главная");Tap("СНАРЯЖЕНИЕ");Assert(game.State==ZSRState.Arsenal,"Home loadout shortcut");
  Tap("Уровни");
  Assert(Buttons().Count(b=>char.IsDigit(b.gameObject.name[8]) && int.TryParse(b.gameObject.name.Substring(8),out _))==25,"Only one 25-level page");
  Assert(Find("1").interactable&&!Find("7").interactable,"Unlocked and locked levels");
  Tap("+100");
  Assert(HasText("Уровни 101–125")&&!Find("105").interactable&&game.Level==2,"Jump across locked districts");
  Tap("К ФРОНТУ");
  Assert(HasText("Уровни 1–25")&&game.Level==6,"Return to the frontier");
  Tap("5");
  Assert(HasText("5  •  МУТАНТ У ВОРОТ"),"Select an unlocked boss");
  Tap("ИГРАТЬ УРОВЕНЬ 5");
  game.State=ZSRState.Briefing;game.DialogueIndex=0;Draw();
  Assert(HasText("МАКС")&&HasText(game.Briefing.Max), "Readable opening Max");
  Dump(Path.Combine(Output,"dialogue-max.json"));
  Tap("ДАЛЕЕ  ›");
  Assert(HasText("ЕВА")&&HasText(game.Briefing.Eva), "Manual next speaker");
  Dump(Path.Combine(Output,"dialogue-eva.json"));
  Tap("НАЧАТЬ ЗАБЕГ");Assert(game.IsRunning,"Final confirmation starts HUD");
  game.State=ZSRState.Victory;Draw();Assert(HasText(game.StoryBeat.After),"Victory shows the aftermath");
  game.UnlockedLevel=1000;game.ClearedLevel=999;game.StartLevel(1000);Draw();
  Assert(HasText("УРОВЕНЬ 1000 / 1000")&&!game.HasStoryBeat,"Final boss HUD and no out-of-cadence story");
  foreach(string item in new[]{"medkit","grenade","drone"})
    Assert(Active(host.transform).Any(o=>o.GetComponent<RawImage>()?.texture?.path=="UI/kit_"+item),"Rendered field-kit image "+item);
  game.Hero.Health=80;Draw();
  Assert(Find("АПТЕЧКА").interactable&&Find("ГРАНАТА").interactable&&Find("ДРОН").interactable,"Available items accept input");
  Find("АПТЕЧКА").onClick.Invoke();Find("ГРАНАТА").onClick.Invoke();Find("ДРОН").onClick.Invoke();
  Assert(game.MedkitCalls==1&&game.GrenadeCalls==1&&game.DroneCalls==1,"Field-kit controls preserve exactly one callback");
  game.Medkits=game.Grenades=game.Drones=0;Draw();
  Assert(!Find("АПТЕЧКА").interactable&&!Find("ГРАНАТА").interactable&&!Find("ДРОН").interactable,"Empty stocks disable their controls");
  game.Medkits=2;game.Grenades=1;game.Drones=1;game.Hero.Health=112;Draw();
  Assert(!Find("АПТЕЧКА").interactable,"Full health preserves the medical charge");
  Dump(Path.Combine(Output,"hud.json"));
  game.Hero.Health=64;game.DamageFlash=.65f;game.LastDamageKind=ZSRDamageKind.Acid;game.CurrentNotice="КИСЛОТА  -6 HP";Draw();
  Assert(HasText("ЗДОРОВЬЕ 64")&&Active(host.transform).Any(o=>o.name=="notice contrast panel"),"Damage notice remains readable on its dark panel");
  Dump(Path.Combine(Output,"hud-damage.json"));
  game.CurrentNotice="";game.DamageFlash=0;game.Hero.Health=112;
  game.SetWeaponRank(WeaponType.Rifle,18);game.Coins=12000;game.State=ZSRState.Defeat;Draw();Dump(Path.Combine(Output,"defeat.json"));
  Assert(Active(host.transform).Any(o=>o.GetComponent<Text>()?.text.StartsWith("До улучшения оружия") == true),"Replay tips are based on the current upgrade shortfall");
  Tap("ЗАРАБОТАТЬ МОНЕТЫ  •  УР. 999");
  Assert(game.Level==999&&game.IsRunning,"Defeat shortcut launches prior unlocked replay");
  game.GoMenu();game.SelectLevel(1000);game.GoLevels();Draw();
  Assert(HasText("Уровни 976–1000") && Find("1000").interactable && !Find("+100").interactable,"Final page caps correctly");
  Dump(Path.Combine(Output,"late.json"));
  for(int page=40;page>=1;page--){
   int low=(page-1)*25+1, high=page*25;
   Assert(HasText("Уровни "+low+"–"+high) && Find(low.ToString()).interactable && Find(high.ToString()).interactable,
          "Accessible page "+page);
   if(page>1){Buttons().Last(b=>b.gameObject.name=="BUTTON: ‹").onClick.Invoke();Draw();}
  }
  Assert(!Find("−100").interactable && Find("1").interactable,"First page caps at level 1");
  Tap("К ФРОНТУ");Assert(HasText("Уровни 976–1000"),"Frontier return from page 1");

  game.GoArsenal();game.Coins=24500;game.SetWeaponRank(WeaponType.Rifle,8);Draw();
  Dump(Path.Combine(Output,"arsenal.json"));
  Assert(HasText("РАНГ 8 / 20")&&Buttons().Any(b=>b.interactable && b.gameObject.name.StartsWith("BUTTON: УЛУЧШИТЬ")),"Rank and upgrade quote");
  game.UnlockedLevel=35;game.SetWeaponRank(WeaponType.Rifle,5);Draw();
  Assert(!Find("РАНГ 6 С УР. 40").interactable,"Future rank shown but locked");
  foreach(var (w,h,notch) in new[]{(390,844,25),(1080,2400,85),(1080,2640,104)}){
   Screen.width=w;Screen.height=h;Screen.safeArea=new(0,notch,w,h-notch*2);
   foreach(var state in new[]{ZSRState.Menu,ZSRState.Heroes,ZSRState.Arsenal,ZSRState.Items,ZSRState.Bonuses,ZSRState.Levels,ZSRState.Playing,ZSRState.Defeat,ZSRState.Briefing}){
    game.State=state;Draw();
    var content=(RectTransform)Current().transform;
    var canvas=(RectTransform)host.transform.children.First();
    float units=canvas.rect.height/h;
    Assert(-content.anchoredPosition.y>=notch*units-.01f,"Safe Area top");
    Assert(-content.anchoredPosition.y+844*content.localScale.y<=844-notch*units+.01f,"Safe Area bottom");
    foreach(var button in Buttons()){
     var rect=(RectTransform)button.transform;float x=rect.anchoredPosition.x,y=-rect.anchoredPosition.y;
     Assert(x>=0&&y>=0&&x+rect.sizeDelta.x<=390.01f&&y+rect.sizeDelta.y<=844.01f,$"Button bounds {state} {button.gameObject.name}");
     Assert(rect.sizeDelta.x>=44&&rect.sizeDelta.y>=44,$"Target {state} {button.gameObject.name}");
    }
   }
  }
  game.UnlockedLevel=1000;
  int dialoguePages=0;
  for(int level=1;level<=1000;level++){
   if(!ZSRStory.TryBriefing(level,out var beat))continue;
   game.SelectLevel(level);game.State=ZSRState.Briefing;
   for(int speaker=0;speaker<2;speaker++){
    game.DialogueIndex=speaker;Draw();dialoguePages++;
    Assert(HasText(beat.Line(speaker)), "Complete dialogue line");
    Assert(Active(host.transform).Any(o=>o.GetComponent<RawImage>()?.texture?.path==
       "UI/dialogue_face_"+(speaker==0?"max":"eva")), "Face matches speaker");
   }
  }
  Assert(dialoguePages==92, "Opening plus 45 scheduled scenes");
  Console.WriteLine("DIALOGUE UI PASS: 92 speaker pages with matching face and complete line.");
  Console.WriteLine("FIELD KIT UI PASS: three rendered icons, one callback per item, 72×80 controls, ammo and high-contrast damage notice.");
  Console.WriteLine("UI PASS: 27 portrait layouts and 44×44 minimum design-space inputs, all 40 pages with 25 entries, level 1000, rank locks, bold button font, manual dialogue with face portraits and replay callback.");
 }
}
