using UnityEngine;

namespace ZombieSurvivalRun
{
    internal readonly struct ZSRWeaponProfile
    {
        static readonly ZSRWeaponProfile[] Arsenal =
        {
            new("Быстрая перезарядка", 27, .080f, .33f, 12, 1.05f, 1, 0, new Color(1f, .84f, .55f), .047f),
            new("Точный огонь против длинных волн", 22, .060f, .13f, 30, 1.35f, 25, 0, new Color(1f, .74f, .27f), .060f),
            new("Групповой урон, медленная перезарядка", 55, .066f, .57f, 8, 1.65f, 150, 28000, new Color(1f, .55f, .22f), .080f),
            new("Огневая поддержка, долгая перезарядка", 13, .037f, .075f, 80, 3.1f, 500, 180000, new Color(1f, .84f, .37f), .053f)
        };

        internal readonly string Role;
        internal readonly int Magazine, UnlockLevel, Price;
        internal readonly float Interval, Reload, TracerWidth;
        internal readonly Color Tracer;
        readonly int baseDamage;
        readonly float veteranGrowth;

        ZSRWeaponProfile(string role, int baseDamage, float veteranGrowth, float interval, int magazine, float reload,
            int unlockLevel, int price, Color tracer, float tracerWidth)
        {
            Role = role;
            this.baseDamage = baseDamage;
            this.veteranGrowth = veteranGrowth;
            Interval = interval;
            Magazine = magazine;
            Reload = reload;
            UnlockLevel = unlockLevel;
            Price = price;
            Tracer = tracer;
            TracerWidth = tracerWidth;
        }

        internal static ZSRWeaponProfile For(WeaponType weapon) => Arsenal[(int)weapon];
        internal static WeaponType RestoreSelection(WeaponType saved, int unlocked, bool owned) =>
            owned && unlocked >= For(saved).UnlockLevel ? saved : WeaponType.Pistol;
        internal int Damage(int upgradeLevel) => upgradeLevel <= 5 ? baseDamage + upgradeLevel * 5 :
            Mathf.RoundToInt((baseDamage + 25) * (1f + (upgradeLevel - 5) * veteranGrowth));
        internal float ShotInterval(int selectedHero) => Interval * (selectedHero == 1 ? .90f : 1f);
    }
}
