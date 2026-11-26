using UnityEngine;

namespace ZombieSurvivalRun
{
    internal readonly struct ZSRWeaponProfile
    {
        static readonly ZSRWeaponProfile[] Arsenal =
        {
            new("Быстрая перезарядка", 27, .33f, 12, 1.05f, 1, 0, new Color(1f, .84f, .55f), .047f),
            new("Универсальный выбор для длинных волн", 22, .13f, 30, 1.35f, 1, 0, new Color(1f, .74f, .27f), .060f),
            new("Задевает врагов рядом с целью", 55, .57f, 8, 1.65f, 4, 2200, new Color(1f, .55f, .22f), .080f),
            new("Плотный огонь и большой магазин", 13, .065f, 70, 2.6f, 9, 5400, new Color(1f, .84f, .37f), .053f)
        };

        internal readonly string Role;
        internal readonly int Magazine, UnlockLevel, Price;
        internal readonly float Interval, Reload, TracerWidth;
        internal readonly Color Tracer;
        readonly int baseDamage;

        ZSRWeaponProfile(string role, int baseDamage, float interval, int magazine, float reload,
            int unlockLevel, int price, Color tracer, float tracerWidth)
        {
            Role = role;
            this.baseDamage = baseDamage;
            Interval = interval;
            Magazine = magazine;
            Reload = reload;
            UnlockLevel = unlockLevel;
            Price = price;
            Tracer = tracer;
            TracerWidth = tracerWidth;
        }

        internal static ZSRWeaponProfile For(WeaponType weapon) => Arsenal[(int)weapon];
        internal int Damage(int upgradeLevel) => baseDamage + upgradeLevel * 5;
        internal float ShotInterval(int selectedHero) => Interval * (selectedHero == 1 ? .90f : 1f);
    }
}
