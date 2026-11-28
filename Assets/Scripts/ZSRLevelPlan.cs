using System;

namespace ZombieSurvivalRun
{
    internal enum ZSRFormation { Crossing, Wedge, Rush, Screen, Flank, Stagger }

    /// <summary>Deterministic encounter and economy parameters for a single campaign level.</summary>
    internal readonly struct ZSRLevelPlan
    {
        internal const int Count = 1000;
        internal const int SectorSize = 25;
        internal const int SectorCount = Count / SectorSize;
        internal const int MaximumRank = 20;
        internal const int MedicalRank = 8;
        const int OriginalLevels = 15;
        const float OriginalTopSpeed = 17.08f;

        static readonly string[] Districts =
        {
            "Пригородный рубеж", "Кольцевая развязка", "Линия затопления", "Грузовые ворота", "Служебный узел",
            "Верхние ярусы", "Выгоревший рынок", "Ледяные доки", "Транзитная линия", "Тихий перрон",
            "Квартал антенн", "Сломанная дамба", "Красный мост", "Мёртвая станция", "Левый берег",
            "След теплотрассы", "Карантинный склон", "Зольный обход", "Водосброс", "Терминал связи",
            "Северный проспект", "Внешние причалы", "Эстакада сирен", "Брожение канала", "Ржавая миля",
            "Второй периметр", "Чёрные склады", "Насосный фронт", "Каменная гряда", "Каскад шлюзов",
            "Противоток", "Мост с вырезом", "Отблеск на воде", "Ночной перегон", "Обратная волна",
            "Верхняя дамба", "Полоса катеров", "Край паводка", "Огни убежища", "Выход к морю"
        };

        internal readonly int Level;
        readonly float escalation;

        internal ZSRLevelPlan(int level)
        {
            Level = Math.Clamp(level, 1, Count);
            escalation = MathF.Pow(Math.Max(0, Level - OriginalLevels) / (float)(Count - OriginalLevels), .79f);
        }

        internal int Sector => (Level - 1) / SectorSize;
        internal int Environment => Level <= OriginalLevels ? (Level - 1) / 5 : ((Level - 16) / 8 + 1) % 3;
        internal int Position => (Level - 1) % 5;
        internal bool IsBoss => ZSRStory.IndexForLevel(Level) >= 0 || Level == Count;
        internal float Speed => Level <= OriginalLevels
            ? 11.7f + (Level - 1) * .22f + Math.Max(0, Level - 5) * .23f
            : OriginalTopSpeed + 4.6f * escalation;
        internal float SpawnDepthScale => Level <= OriginalLevels
            ? Speed / (11.7f + (Level - 1) * .22f)
            : 1.154f + .17f * escalation;
        internal float Duration => Level <= OriginalLevels
            ? (IsBoss ? 84f + Environment * 4f : 66f + Position * 4f + Environment * 2f)
            : (IsBoss ? 90f : 73f) + Position * 2f + escalation * 9f;
        internal float BossOvertime => 24f + escalation * 5f;
        internal float IncomingDamage => 1f + escalation * .42f;
        internal float EnemySpeedScale => 1f + escalation * .20f;
        internal float BossAttackDelay => Level <= OriginalLevels ? 3.4f - Level * .03f : 2.95f - escalation * .46f;
        internal float HazardDelay(int index) => Level <= OriginalLevels ? 9f + Roll(4, index) * 5f :
            12.8f - escalation * 5.5f + (Roll(4, index) - .5f) * 2.4f;
        internal bool SplitHazard(int index) => Level >= 76 && (index + Sector + Position) % (escalation > .53f ? 4 : 6) == 0;
        internal int ObstacleKind(int index) => Math.Min(3, (int)(Roll(5, index) * 4));
        internal int PickupValue => Level <= OriginalLevels ? 25 + Environment * 10 : 20 + Sector / 2;

        internal string District => Districts[Sector];
        internal static string SectorName(int index) => Districts[index];

        internal float WaveDelay(float elapsed)
        {
            if (Level <= OriginalLevels) return Math.Max(1f, 2.8f - Level * .06f - elapsed * .0065f);
            float pistolAllowance = Level < ZSRWeaponProfile.For(WeaponType.Rifle).UnlockLevel ? .30f : 0f;
            return pistolAllowance + Math.Max(1.08f + (1f - escalation) * .23f, 2.10f - escalation * .35f - elapsed * .006f);
        }

        internal ZSRFormation Formation(int wave)
        {
            if (Level <= OriginalLevels) return ZSRFormation.Crossing;
            // Districts favor a formation, with a different deterministic change-up every third wave.
            return (ZSRFormation)((wave % 3 == 1 ? Sector + Position : Sector * 2 + wave + Level) % 6);
        }

        internal int WaveSize(int wave)
        {
            if (Level <= OriginalLevels)
            {
                int size = Math.Clamp(3 + (Level - 1) / 4 + (Position >= 3 ? 1 : 0), 3, 6);
                return IsBoss ? Math.Max(3, size - 1) : size;
            }
            if (Level < ZSRWeaponProfile.For(WeaponType.Rifle).UnlockLevel) return 3;
            ZSRFormation formation = Formation(wave);
            return formation == ZSRFormation.Rush || (escalation > .55f && wave % 3 == 1) ? 6 : 5;
        }

        internal (ZombieType kind, float x, float z) WaveUnit(int wave, int slot)
        {
            var formation = Formation(wave);
            float roll = Roll(0, wave * 6 + slot);
            ZombieType kind = BasicEnemy(roll);
            float side = Roll(1, wave) < .5f ? -1f : 1f;
            int row = slot / 3, column = slot % 3 - 1;
            float x = column * 2.72f;
            float z = 36f + row * 4.4f;

            switch (formation)
            {
                case ZSRFormation.Wedge:
                    z += Math.Abs(column) * 3.6f;
                    if (slot == 1) kind = ZombieType.Armored;
                    break;
                case ZSRFormation.Rush:
                    z += column * side * 1.7f;
                    if (row == 0) kind = ZombieType.Runner;
                    break;
                case ZSRFormation.Screen:
                    if (slot == 1) kind = ZombieType.Brute;
                    if (row == 1) kind = column == 0 ? ZombieType.Toxic : ZombieType.Walker;
                    z += row * 2.5f;
                    break;
                case ZSRFormation.Flank:
                    x = Math.Clamp(x + side * 1.0f, -3.72f, 3.72f);
                    z += side * column * 2.8f;
                    if (slot == 2) kind = ZombieType.Crawler;
                    break;
                case ZSRFormation.Stagger:
                    z = 33.5f + slot * 2.3f;
                    x *= -side;
                    if (slot == 3) kind = ZombieType.Toxic;
                    break;
            }
            if (Level > OriginalLevels && kind == ZombieType.Brute && column != 0)
                kind = ZombieType.Armored;
            x += (Roll(2, wave) - .5f) * 1.10f;
            z += (Roll(3, wave * 6 + slot) - .5f) * 1.80f;
            return (kind, Math.Clamp(x, -3.95f, 3.95f), z * SpawnDepthScale);
        }

        ZombieType BasicEnemy(float roll)
        {
            if (roll < .27f) return ZombieType.Runner;
            if (Level >= 2 && roll < .44f) return ZombieType.Armored;
            if (Level >= 3 && roll < .60f) return ZombieType.Toxic;
            if (Level >= 5 && roll < .73f) return ZombieType.Crawler;
            return Level >= 6 && roll < .86f ? ZombieType.Brute : ZombieType.Walker;
        }

        internal float EnemyHealth(ZombieType type)
        {
            int level = Math.Min(Level, OriginalLevels);
            if (type == ZombieType.Boss)
                return 880f + Math.Max(0, level - 5) * 48f + 1500f * escalation;
            float earlyHealth = type switch
            {
                ZombieType.Brute => 170f + level * 7f,
                ZombieType.Armored => 98f + level * 3f,
                ZombieType.Runner => 34f + level * 1.3f,
                ZombieType.Toxic => 62f + level * 2.2f,
                ZombieType.Crawler => 41f + level * 1.6f,
                _ => 54f + level * 1.8f
            };
            return earlyHealth * (1f + escalation * 1.28f);
        }

        internal int VictoryGrant(bool firstClear)
        {
            if (!firstClear) return 260 + Level * 3 + (IsBoss ? 120 : 0);
            return Level <= OriginalLevels ? 320 + Level * 95 + (IsBoss ? 450 : 0) :
                1850 + (Level - 16) * 5 + (IsBoss ? 800 : 0);
        }

        internal int Payout(int earnings, bool victory, bool firstClear, float progress)
        {
            if (!victory) return progress >= .20f ? earnings / 4 : 0;
            return VictoryGrant(firstClear) + (firstClear ? earnings : earnings * 65 / 100);
        }

        internal static (int unlocked, int cleared) RestoreProgress(int savedUnlocked, int savedCleared)
        {
            int unlocked = Math.Clamp(savedUnlocked, 1, Count);
            int cleared = savedCleared < 0 ? unlocked - 1 : Math.Clamp(savedCleared, 0, unlocked);
            return (Math.Max(unlocked, Math.Min(Count, cleared + 1)), cleared);
        }

        internal static int UpgradeCap(int unlocked) => Math.Clamp(5 + (unlocked + 20) / 60, 5, MaximumRank);
        internal static int UnlockForRank(int rank) => rank <= 5 ? 1 : (rank - 5) * 60 - 20;
        internal static int UpgradeCost(int baseCost, int rank)
        {
            if (rank < 5) return baseCost * rank;
            return baseCost * (rank + (rank - 4) * 3);
        }

        // Fixed hash and indexed channels keep retries reproducible even if combat consumes random numbers.
        internal float Roll(int channel, int index)
        {
            unchecked
            {
                uint bits = (uint)Level * 0x9e3779b9u ^ (uint)channel * 0x85ebca6bu ^ (uint)(index + 1) * 0xc2b2ae35u;
                bits ^= bits >> 16;
                bits *= 0x7feb352du;
                bits ^= bits >> 15;
                bits *= 0x846ca68bu;
                bits ^= bits >> 16;
                return (bits >> 8) * (1f / 16777216f);
            }
        }
    }
}
