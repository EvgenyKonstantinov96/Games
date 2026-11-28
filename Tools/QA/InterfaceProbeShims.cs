// Minimal layout and callback simulation. Unity rendering and native input are not represented.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
 public class Object { protected static void Destroy(Object obj) {} protected static T FindFirstObjectByType<T>() where T:class => null; }
 public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>(); }
 public class MonoBehaviour:Component {}
 public class GameObject:Object {
   public string name; public Transform transform; public bool activeSelf=true; public readonly List<Component> components=new();
   public GameObject(string name,params Type[] types) {this.name=name;transform=types.Contains(typeof(RectTransform))?new RectTransform():new Transform(); transform.gameObject=this;components.Add(transform); foreach(var type in types.Where(t=>t!=typeof(RectTransform))) Add(type);}
   Component Add(Type t) {var c=(Component)Activator.CreateInstance(t);c.gameObject=this;components.Add(c);return c;}
   public T AddComponent<T>() where T:Component,new()=>(T)Add(typeof(T)); public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault(); public void SetActive(bool value)=>activeSelf=value;
 }
 public class Transform:Component { public Transform parent; public List<Transform> children=new(); public Vector3 localScale=Vector3.one; public void SetParent(Transform p,bool stays) {parent?.children.Remove(this);parent=p;parent.children.Add(this);} }
 public class RectTransform:Transform {
  public Vector2 anchorMin,anchorMax,offsetMin,offsetMax,pivot,anchoredPosition,sizeDelta;
  public bool Stretch=>anchorMin.x==0 && anchorMin.y==0 && anchorMax.x==1 && anchorMax.y==1;
  public Rect rect=>Stretch?(parent is RectTransform rt?rt.rect:new Rect(0,0,390f*Screen.width/Screen.height*844f/390f,844f)):new Rect(0,0,sizeDelta.x,sizeDelta.y);
 }
 public struct Vector2 {public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>new(0,0);public static Vector2 one=>new(1,1);public static Vector2 down=>new(0,-1);public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b);public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);}
 public struct Vector3 {public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public static Vector3 one=>new(1,1,1);public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);}
 public struct Vector4 {public Vector4(float a,float b,float c,float d) {}}
 public record struct Rect(float x,float y,float width,float height) { public float xMin=>x; public float yMax=>y+height; }
 public struct Color {public float r,g,b,a; public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;} public static Color clear=>new(0,0,0,0); public static Color white=>new(1,1,1); public static Color Lerp(Color a,Color b,float t)=>new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
 public static class Mathf {public static float Clamp01(float a)=>Math.Clamp(a,0,1); public static float MoveTowards(float a,float b,float d)=>Math.Abs(a-b)<=d?b:a+Math.Sign(b-a)*d; public static int Min(int a,int b)=>Math.Min(a,b); public static float Abs(float v)=>Math.Abs(v); public static int Clamp(int value,int a,int b)=>Math.Clamp(value,a,b); public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b); public static float Min(float a,float b)=>Math.Min(a,b);public static int RoundToInt(float a)=>(int)Math.Round(a);public static int CeilToInt(float a)=>(int)Math.Ceiling(a);}
 public static class Screen {public static int width=390,height=844;public static Rect safeArea=new(0,0,390,844);}
 public static class Time {public static float unscaledDeltaTime=1f/60;}
 public class Texture2D:Object {public string path;public int width,height;}
 public class Font:Object {public string path; public static Font CreateDynamicFontFromOSFont(string[] names,int size)=>new();}
 public enum FontStyle {Normal,Bold,BoldAndItalic} public enum TextAnchor{MiddleCenter,MiddleLeft,MiddleRight,UpperLeft,UpperCenter,UpperRight} public enum HorizontalWrapMode{Wrap} public enum VerticalWrapMode{Truncate}
 public enum SpriteMeshType{FullRect} public enum RenderMode{ScreenSpaceOverlay}
 public class Sprite:Object {public Texture2D Texture;public static Sprite Create(Texture2D t,Rect r, Vector2 pivot,float ppu,uint ext,SpriteMeshType mesh,Vector4 border)=>new(){Texture=t};}
 public class Canvas:Component {public RenderMode renderMode;public int sortingOrder;}
 public class CanvasGroup:Component {public float alpha=1;public bool blocksRaycasts=true,interactable=true;}
 public static class Resources {
   public static T Load<T>(string p) where T:Object,new() {
     if(typeof(T)==typeof(Font))return new Font{path=p} as T;
     int w=256,h=96; if(p.Contains("campaign_cover_")){w=720;h=738;} else if(p.Contains("Horizon")){w=1024;h=512;} else if(p.Contains("wallpaper")){w=780;h=1688;} else if(p.EndsWith("eva")){w=444;h=668;} else if(p.EndsWith("max")){w=432;h=645;}
     string file=System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("ZSR_QA_RESOURCES"),p+".png");
     if(System.IO.File.Exists(file)){
       byte[] bytes=System.IO.File.ReadAllBytes(file);
       w=(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19];
       h=(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23];
     }
     return new Texture2D{path=p,width=w,height=h} as T;
   } public static T GetBuiltinResource<T>(string name) where T:Object,new()=>new();
 }
}
namespace UnityEngine.UI {
 using UnityEngine;
 public class Graphic:Component{public bool raycastTarget=true;public Color color;public RectTransform rectTransform=>(RectTransform)transform;}
 public class Image:Graphic{public enum Type{Sliced} public Sprite sprite; public Type type;}
 public class RawImage:Graphic{public Texture2D texture;public Rect uvRect=new(0,0,1,1);}
 public class Text:Graphic{public Font font;public string text;public int fontSize;public FontStyle fontStyle;public TextAnchor alignment;public HorizontalWrapMode horizontalOverflow;public VerticalWrapMode verticalOverflow;public bool resizeTextForBestFit;}
 public struct ColorBlock{public Color normalColor,highlightedColor,pressedColor,selectedColor,disabledColor;public float fadeDuration,colorMultiplier;}
 public class Button:Component{public Graphic targetGraphic;public bool interactable=true;public ColorBlock colors;public readonly Listener onClick=new(); public class Listener {Action callback;public void AddListener(Action action)=>callback+=action;public void Invoke()=>callback?.Invoke();}}
 public class GraphicRaycaster:Component{}
 public class CanvasScaler:Component{public enum ScaleMode{ScaleWithScreenSize} public enum ScreenMatchMode{MatchWidthOrHeight}; public ScaleMode uiScaleMode;public Vector2 referenceResolution;public ScreenMatchMode screenMatchMode;public float matchWidthOrHeight;}
}
namespace UnityEngine.EventSystems { using UnityEngine; public class EventSystem:Component{public static EventSystem current=new();public GameObject currentSelectedGameObject;public void SetSelectedGameObject(GameObject go){currentSelectedGameObject=go;} } public class StandaloneInputModule:Component{public bool enabled;} }
namespace UnityEngine.InputSystem { public class Keyboard{public static Keyboard current;public Key escapeKey=new();} public class Key{public bool wasPressedThisFrame;} }
namespace UnityEngine.InputSystem.UI {public class InputSystemUIInputModule:UnityEngine.Component{} }
namespace ZombieSurvivalRun {
 using UnityEngine;
 public enum ZSRState{Menu,Heroes,Arsenal,Bonuses,Items,Levels,Playing,Paused,Victory,Defeat,Briefing}
 public enum ZSRDamageKind{Bite,Claw,Acid,Collision,Slam}
 public enum ZombieType{Walker,Runner,Armored,Brute,Toxic,Crawler,Boss}
 public enum WeaponType{Pistol,Rifle,Shotgun,Minigun} public enum ItemType{Medkit,Grenade,Drone,Armor} public enum BonusKind{DoubleDamage,RapidFire,Shield,Heal,Magnet,Turret}
 public class HeroModel{public int Health=112,MaxHealth=112;public float PositionX=0;}
 public class ZSRGame {
   public static ZSRGame Instance=new(); public const int MaxLevels=1000; public const int GateHealAmount=45; public const float RapidFireInterval=.62f,MagnetRadius=3.6f; public const int MaxUpgradeLevel=20;
   public int ClearedLevel=1,Payout=421; public bool Replaying=>Level<=ClearedLevel;
   internal ZSRLevelPlan Plan=>new(Level); public bool IsBossLevel=>Plan.IsBoss;
   internal bool HasStoryBeat=>ZSRStory.TryGet(Level,out _);
   internal ZSRStory.Beat StoryBeat { get { ZSRStory.TryGet(Level,out var beat); return beat; } }
   public int DialogueIndex; public float DamageFlash,DamageSourceX;public ZSRDamageKind LastDamageKind;
   internal ZSRStory.Beat Briefing { get {ZSRStory.TryBriefing(Level,out var b);return b;} }
   public void AdvanceDialogue(){if(DialogueIndex++==1)State=ZSRState.Playing;}
   public int ReplayLevel { get {int candidate=Math.Min(ClearedLevel, Level-1);return candidate>1&&new ZSRLevelPlan(candidate).IsBoss?candidate-1:candidate;} }
   public int CampaignUpgradeCap=>ZSRLevelPlan.UpgradeCap(UnlockedLevel);
   public static int MaximumItemRank(ItemType item)=>item==ItemType.Medkit?8:20;
   public int ItemUpgradeCap(ItemType item)=>Math.Min(CampaignUpgradeCap,MaximumItemRank(item));
   public (int speaker,string line) Radio=(0,null); internal ZSRPace Pace; public float PaceRecharge;
   readonly Dictionary<WeaponType,int> weaponLevels=new();
   readonly HashSet<WeaponType> ownedWeapons=new(){WeaponType.Pistol,WeaponType.Rifle};
   readonly Dictionary<ItemType,int> items=new();
   public static int ItemStartCount(ItemType i,int l)=>i==ItemType.Drone?(l>=4?2:1):1+l/2;
   public static int MedkitHealth(int l)=>40+l*18; public static int GrenadeDamage(int l)=>105+l*38;
   public static float GrenadeRadius(int l)=>4.6f+.42f*l; public static float DroneDuration(int l)=>10f+2*l;
   public static int DroneDamage(int l)=>22+l*12; public static float ShieldSeconds(int l)=>8f+(l-1)*1.5f;
   public static float ArmorReduction(int l)=>(l-1)*.06f;
   public int WeaponLevelFor(WeaponType w)=>weaponLevels.GetValueOrDefault(w,1); public int WeaponUpgradeCost(WeaponType w)=>ZSRLevelPlan.UpgradeCost(750,WeaponLevelFor(w));
   public void SetWeaponRank(WeaponType w,int rank)=>weaponLevels[w]=rank;
   public void UpgradeWeapon(WeaponType w){int cost=WeaponUpgradeCost(w);if(WeaponOwned(w)&&Coins>=cost){weaponLevels[w]=WeaponLevelFor(w)+1; Coins-=cost;}}
   public ZSRState State=ZSRState.Menu;public readonly HeroModel Hero=new(); public int Coins=2719, Level=2, UnlockedLevel=6,SelectedHero=1,Earned=245,Eliminated=18,Ammo=4,Magazine=30,Medkits=2,Grenades=1,Drones=1;public WeaponType Weapon=WeaponType.Rifle; public int WeaponLevel=>WeaponLevelFor(Weapon);public int UpgradeCost=>WeaponUpgradeCost(Weapon);
   public bool IsReloading=false,BossAlive=false;public bool IsRunning=>State==ZSRState.Playing; public float BossHealthFraction=.62f,DroneSeconds=0,Progress=.57f;
   public string MusicLabel="МУЗЫКА ВКЛ",CurrentNotice="";public int Chapter=>Plan.Environment;public string LevelTitle=>Level>15?Plan.District.ToUpperInvariant():Level switch {5=>"МУТАНТ У ВОРОТ",7=>"ПРОРЫВ К КВАРТАЛАМ",12=>"ВОЕННЫЙ КОРИДОР",_=>"ТЁМНЫЙ ПРОСПЕКТ"};
   public float BonusDuration(BonusKind k)=>k switch{BonusKind.DoubleDamage=>10,BonusKind.RapidFire=>11,BonusKind.Shield=>8,BonusKind.Magnet=>12,BonusKind.Turret=>12,_=>0};
   public float BonusRemaining(BonusKind k)=>State==ZSRState.Playing&&k==BonusKind.Shield?7:0;
   public void SelectHero(int value)=>SelectedHero=value;public void SelectWeapon(WeaponType w){if(!WeaponLevelUnlocked(w))return; if(!WeaponOwned(w)){if(Coins<WeaponPrice(w))return;Coins-=WeaponPrice(w);ownedWeapons.Add(w);} Weapon=w;}public void SelectLevel(int l)=>Level=Math.Clamp(l,1,UnlockedLevel);public void StartLevel(int l){if(l>UnlockedLevel)return;SelectLevel(l);State=ZSRState.Playing;}
   public void GoMenu()=>State=ZSRState.Menu;public void GoHeroes()=>State=ZSRState.Heroes;public void GoArsenal()=>State=ZSRState.Arsenal;public void GoBonuses()=>State=ZSRState.Bonuses;public void GoItems()=>State=ZSRState.Items;public void GoLevels()=>State=ZSRState.Levels;
   public void TogglePause()=>State=State==ZSRState.Playing?ZSRState.Paused:ZSRState.Playing;public void Retry()=>StartLevel(Level);public void NextLevel()=>StartLevel(Level+1);public void UnlockForTesting()=>UnlockedLevel=1000;
   public int ItemLevel(ItemType i)=>items.GetValueOrDefault(i,1);public int ItemUpgradeCost(ItemType i)=>(i switch {ItemType.Medkit=>950,ItemType.Grenade=>1100,ItemType.Drone=>1350,_=>1500})*ItemLevel(i);public string ItemDescription(ItemType i)=>"Уровень модуля";public void UpgradeItem(ItemType i){Coins-=ItemUpgradeCost(i);items[i]=ItemLevel(i)+1;} public void UpgradeWeapon(){} public bool WeaponLevelUnlocked(WeaponType w)=>UnlockedLevel>=WeaponUnlockLevel(w); public bool WeaponOwned(WeaponType w)=>ownedWeapons.Contains(w); public int WeaponUnlockLevel(WeaponType w)=>ZSRWeaponProfile.For(w).UnlockLevel; public int WeaponPrice(WeaponType w)=>ZSRWeaponProfile.For(w).Price;
   public void CycleMusic()=>MusicLabel="МУЗЫКА ВЫКЛ";public void UseMedkit(){}public void ThrowGrenade(){}public void UseDrone(){}
 }
 public static class ZSRVeilGateView {
  public static (int slot,string label,string texture,Color tint,string title) Appearance(BonusKind kind)=> kind switch {
  BonusKind.DoubleDamage=>(0,"×2","damage",new Color(1f,.54f,.24f),"Двойной урон"),BonusKind.RapidFire=>(1,"ТЕМП","rapid",new Color(.34f,.83f,1f),"Ускорение стрельбы"),BonusKind.Shield=>(2,"ЩИТ","shield",new Color(.47f,.64f,1f),"Энергощит"),BonusKind.Heal=>(3,"HP","heal",new Color(.37f,.94f,.59f),"Восстановление здоровья"),BonusKind.Magnet=>(4,"МАГНИТ","magnet",new Color(1f,.79f,.29f),"Магнит монет"),_=>(5,"ДРОН","drone",new Color(.78f,.58f,1f),"Воздушная поддержка")};
 }
}
