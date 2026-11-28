using System;
using System.Collections.Generic;
using ZombieSurvivalRun;
using UnityEngine;

namespace UnityEngine
{
    public readonly struct Color { public Color(float r,float g,float b){} }
    public static class Mathf
    {
        public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);
        public static int RoundToInt(float x)=>(int)MathF.Round(x);
    }
    // Explicit persistence stand-in. No native PlayerPrefs or Unity scene is used.
    public static class PlayerPrefs
    {
        public static readonly Dictionary<string,int> Values=new();
        public static int Saves;
        public static int GetInt(string key,int initial)=>Values.GetValueOrDefault(key,initial);
        public static void SetInt(string key,int value)=>Values[key]=value;
        public static void Save()=>Saves++;
        public static void Reset(){Values.Clear();Saves=0;}
    }
}
namespace ZombieSurvivalRun
{
    partial class ZSRGame
    {
        public const int MaxUpgradeLevel=20;
        public int UnlockedLevel=1,Coins=999999;
        public WeaponType Weapon=WeaponType.Pistol;
        internal int CampaignUpgradeCap=>ZSRLevelPlan.UpgradeCap(UnlockedLevel);
        public string Notice;
        void Notify(string message,float seconds)=>Notice=message;
    }
}
static class ArsenalProbe
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Main()
    {
        int checks=0;
        foreach(WeaponType gun in new[]{WeaponType.Rifle,WeaponType.Shotgun,WeaponType.Minigun})
        foreach(bool purchased in new[]{false,true})
        {
            PlayerPrefs.Reset();
            int threshold=ZSRWeaponProfile.For(gun).UnlockLevel;
            var game=new ZSRGame{UnlockedLevel=threshold-1};
            if(purchased)PlayerPrefs.SetInt("zsr40_weapon_owned_"+(int)gun,1);
            PlayerPrefs.SetInt("zsr40_weapon_level_"+(int)gun,4);
            int before=game.Coins;
            game.SelectWeapon(gun);game.UpgradeWeapon(gun);
            Require(game.Weapon==WeaponType.Pistol && game.Coins==before && PlayerPrefs.Saves==0,
                "Owned flag must not bypass campaign gate");
            Require(game.WeaponLevelFor(gun)==4,"Saved ranks retained while locked");
            game.UnlockedLevel=threshold;
            game.SelectWeapon(gun);
            int price=purchased?0:game.WeaponPrice(gun);
            Require(game.Weapon==gun && game.Coins==before-price && game.WeaponOwned(gun),"Purchase at milestone");
            game.SelectWeapon(gun);
            Require(game.Coins==before-price,"Never charge again for existing purchase");
            int rank=game.WeaponLevelFor(gun),cost=game.WeaponUpgradeCost(gun),wallet=game.Coins;
            game.UpgradeWeapon(gun);
            Require(game.WeaponLevelFor(gun)==rank+1 && game.Coins==wallet-cost,"Upgrade after unlock");
            Require(PlayerPrefs.GetInt("zsr40_coins",-1)==game.Coins &&
                PlayerPrefs.GetInt("zsr40_weapon",-1)==(int)gun,"Stored coins and selection");
            checks++;
        }
        foreach(WeaponType gun in new[]{WeaponType.Shotgun,WeaponType.Minigun})
        {
            PlayerPrefs.Reset();
            var game=new ZSRGame{UnlockedLevel=1000,Coins=ZSRWeaponProfile.For(gun).Price-1};
            int before=game.Coins;
            game.SelectWeapon(gun);
            Require(!game.WeaponOwned(gun) && game.Coins==before && PlayerPrefs.Saves==0,"Insufficient funds");
        }
        PlayerPrefs.Reset();
        var cap=new ZSRGame{UnlockedLevel=25};
        PlayerPrefs.SetInt("zsr40_weapon_level_1",cap.CampaignUpgradeCap);
        int coins=cap.Coins;
        cap.UpgradeWeapon(WeaponType.Rifle);
        Require(cap.Coins==coins && PlayerPrefs.Saves==0,"Campaign rank cap");
        Console.WriteLine($"ARSENAL TRANSACTIONS PASS: {checks} milestone/legacy-purchase scenarios, no locked equip/upgrade, retained ranks, no double charge, insufficient funds, rank cap, persistence writes. In-memory PlayerPrefs only.");
    }
}
