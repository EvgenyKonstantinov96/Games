using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZombieSurvivalRun;

// Minimal value types for executing the shared campaign math without booting Unity.
namespace UnityEngine
{
    public struct Color
    {
        public Color(float r, float g, float b) { }
    }

    public readonly struct Vector2
    {
        public readonly float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
    }

    public static class Mathf
    {
        public static float Abs(float x) => Math.Abs(x);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int RoundToInt(float x) => (int)MathF.Round(x);
        public static float MoveTowards(float a, float b, float d) =>
            Math.Abs(a - b) <= d ? b : a + Math.Sign(b - a) * d;
    }
}

static class CampaignProbe
{
    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    static IEnumerable<int> ExpectedStory()
    {
        for (int level = 5; level <= 15; level += 5) yield return level;
        for (int level = 25; level <= 65; level += 10) yield return level;
        for (int level = 90; level <= 1000; level += 25) yield return level;
    }

    static float SustainedDamage(WeaponType weapon, int rank)
    {
        var profile = ZSRWeaponProfile.For(weapon);
        // Use Max, the slower of the two shooters, throughout the budget.
        return profile.Damage(rank) * profile.Magazine /
            (profile.Magazine * profile.ShotInterval(0) + profile.Reload);
    }

    static void Main()
    {
        Require(ZSRLevelPlan.RestoreProgress(1, -1) == (1, 0), "New player");
        Require(ZSRLevelPlan.RestoreProgress(12, -1) == (12, 11), "Save before the story key");
        Require(ZSRLevelPlan.RestoreProgress(15, 15) == (16, 15), "Original campaign completed");
        Require(ZSRLevelPlan.RestoreProgress(623, 622) == (623, 622), "Campaign resume");
        Require(ZSRLevelPlan.RestoreProgress(1000, 1000) == (1000, 1000), "Campaign complete");
        Console.WriteLine("SAVE PASS: new, legacy, original finale, mid-campaign and completed saves.");
        var story = new List<int>();
        var titles = new HashSet<string>();
        var encounters = new HashSet<string>();
        var formations = new HashSet<ZSRFormation>();
        var sectorNames = new HashSet<string>();
        var roster = (ZombieType[])Enum.GetValues(typeof(ZombieType));
        var weapons = (WeaponType[])Enum.GetValues(typeof(WeaponType));
        float worstBossMargin = float.PositiveInfinity;
        int worstBoss = 0, bossCount = 0, unitCount = 0, hazardCount = 0;
        var samples = new Dictionary<int, float>();
        float trafficMargin = float.PositiveInfinity;
        int slowedTraffic = 0;

        for (int level = 1; level <= ZSRLevelPlan.Count; level++)
        {
            var plan = new ZSRLevelPlan(level);
            var previous = new ZSRLevelPlan(level - 1);
            int rank = ZSRLevelPlan.UpgradeCap(level);
            sectorNames.Add(plan.District);
            Require(plan.Environment >= 0 && plan.Environment <= 2, $"Environment at {level}");
            Require(plan.Duration >= 66f && plan.Duration < 112f, $"Duration at {level}");
            Require(plan.Speed >= previous.Speed && plan.Speed < 22f, $"Road speed at {level}");
            Require(plan.IncomingDamage >= previous.IncomingDamage && plan.IncomingDamage < 1.43f, $"Incoming damage at {level}");
            Require(roster.All(kind => plan.EnemyHealth(kind) >= previous.EnemyHealth(kind)), $"Health at {level}");
            Require(rank >= ZSRLevelPlan.UpgradeCap(level - 1) && rank <= 20, $"Rank at {level}");
            if (level >= 40 && rank > ZSRLevelPlan.UpgradeCap(level - 1))
                Require(ZSRLevelPlan.UnlockForRank(rank) == level, $"Unlock message at {level}");

            if (ZSRStory.TryGet(level, out var beat))
            {
                Require(titles.Add(beat.Title) && beat.Line(0) != beat.Line(1) && beat.After.Length > 20, $"Story at {level}");
                story.Add(level);
            }
            Require(plan.IsBoss == (story.Contains(level) || level == 1000), $"Boss cadence at {level}");

            float time = .8f, healthBudget = 0f;
            var signature = new List<string>();
            for (int wave = 0; time < plan.Duration - (plan.IsBoss ? 18f : 5f); wave++)
            {
                int count = plan.WaveSize(wave);
                Require(count >= 3 && count <= 6, $"Wave size at {level}/{wave}");
                formations.Add(plan.Formation(wave));
                for (int slot = 0; slot < count; slot++)
                {
                    var unit = plan.WaveUnit(wave, slot);
                    Require(unit == new ZSRLevelPlan(level).WaveUnit(wave, slot), "Deterministic retry");
                    Require(Math.Abs(unit.x) <= 3.95f && unit.z >= 31f && unit.z < 65f && unit.kind != ZombieType.Boss,
                        $"Wave geometry at {level}/{wave}/{slot}");
                    if (wave < 3) signature.Add($"{(int)unit.kind}/{unit.x:F3}/{unit.z:F3}");
                    unitCount++;
                    healthBudget += plan.EnemyHealth(unit.kind);
                }
                time += plan.WaveDelay(time);
            }
            Require(encounters.Add(string.Join(",", signature)), $"Duplicate opening at {level}");
            if (level is 16 or 65 or 250 or 500 or 750 or 999)
                samples[level] = healthBudget / time / SustainedDamage(WeaponType.Minigun, rank);

            // Simulate the live depth reservation with the fastest allowed speed.
            // Duplicates model the two obstacles of a split in a single traffic window.
            var occupied = new List<float>();
            float roadSpeed = plan.Speed * ZSRRunPace.SurgeScale;
            float at = 0f, obstacleAt = 9f, gateAt = 8.5f;
            int obstacle = 0;
            while (Math.Min(obstacleAt, gateAt) < plan.Duration - 10f)
            {
                bool gate = gateAt < obstacleAt;
                float next = Math.Min(obstacleAt, gateAt);
                float travel = (next - at) * roadSpeed;
                for (int i = 0; i < occupied.Count; i++) occupied[i] -= travel;
                occupied.RemoveAll(depth => depth <= 0f);
                at = next;
                if (!gate || at < plan.Duration - 13f)
                {
                    float desired = (gate ? 39f : 43f) * plan.SpawnDepthScale;
                    float placed = Traffic.SpaceTraffic(desired, roadSpeed * 1.30f, occupied);
                    if (placed > desired + .01f) slowedTraffic++;
                    foreach (float other in occupied)
                        trafficMargin = Math.Min(trafficMargin, Math.Abs(other - placed) / roadSpeed);
                    occupied.Add(placed);
                    if (!gate)
                    {
                        int kind = plan.ObstacleKind(obstacle);
                        Require(kind >= 0 && kind <= 3, "Obstacle style");
                        Require(plan.HazardDelay(obstacle) >= 6f, "Obstacle recovery");
                        if (plan.SplitHazard(obstacle))
                        {
                            Require(2.9f - 1.61f > 1.2f, "Central gap in split hazards");
                            occupied.Add(placed);
                        }
                        hazardCount++;
                    }
                }
                if (gate) gateAt += 16f + plan.Environment * 2f;
                else obstacleAt += plan.HazardDelay(++obstacle);
            }
            Require(plan.Payout(0, true, false, 1f) > 0 && plan.Payout(1800, true, false, 1f) < plan.Payout(1800, true, true, 1f),
                $"Replay reward at {level}");
            Require(plan.Payout(1800, false, false, .19f) == 0 && plan.Payout(1800, false, false, .3f) == 450,
                $"Defeat reward at {level}");

            if (!plan.IsBoss) continue;
            bossCount++;
            float frontLineHealth = plan.EnemyHealth(ZombieType.Boss) + (level >= 10 ? 2 * plan.EnemyHealth(ZombieType.Brute) : 0f);
            foreach (WeaponType weapon in weapons.Where(w => ZSRWeaponProfile.For(w).UnlockLevel <= level))
            {
                // Discount uptime for steering, waves still on approach, item animations and misses.
                float seconds = frontLineHealth / (SustainedDamage(weapon, rank) * .57f);
                float margin = 18f + plan.BossOvertime - seconds;
                Require(margin > 4f, $"Boss budget {level}/{weapon}: {margin:F1} s");
                int affordableRank = Math.Max(1, rank - 1);
                float modestUpgradeTime = frontLineHealth / (SustainedDamage(weapon, affordableRank) * .57f);
                Require(modestUpgradeTime < 18f + plan.BossOvertime, $"Boss with one rank missing at {level}/{weapon}");
                if (margin < worstBossMargin) { worstBossMargin = margin; worstBoss = level; }
            }
        }

        Require(ZSRStory.SpeakerAt(1f) == -1 && ZSRStory.SpeakerAt(2f) == 0 &&
            ZSRStory.SpeakerAt(12f) == 1 && ZSRStory.SpeakerAt(21f) == -1, "Radio timing and order");
        Require(story.SequenceEqual(ExpectedStory()) && story.Count == 45 && !ZSRStory.TryGet(1001, out _), "Precisely 45 story levels");
        Require(sectorNames.Count == 40 && formations.Count == 6 && bossCount == 46, "Sector and encounter coverage");
        Require(trafficMargin >= 1.299f && slowedTraffic > 1000, $"Traffic clearance {trafficMargin:F3} s");
        Require(samples[999] > samples[16] && samples[999] > 1.35f, "Final waves demand more than maximum continuous fire");
        for (int rank = 1; rank < 20; rank++)
        {
            Require(ZSRLevelPlan.UpgradeCost(750, rank) < ZSRLevelPlan.UpgradeCost(750, rank + 1), $"Upgrade pricing at rank {rank}");
            Require(weapons.All(w => ZSRWeaponProfile.For(w).Damage(rank) < ZSRWeaponProfile.For(w).Damage(rank + 1)),
                $"Damage improvement at rank {rank}");
        }

        foreach (var example in new[] { (40, 5, 1500), (280, 9, 4000), (880, 19, 16000) })
        {
            var zone = new ZSRLevelPlan(example.Item1 - 1);
            int cost = ZSRLevelPlan.UpgradeCost(750, example.Item2);
            int income = zone.Payout(example.Item1 * 4 + 2200, true, false, 1f);
            int needed = (cost - example.Item3 + income - 1) / income;
            Require(needed >= 1 && needed <= 8, $"Replay shortfall at level {example.Item1}: {needed} clears");
            Console.WriteLine($"ECONOMY lv {example.Item1}: {needed} replay(s) with sample wallet {example.Item3} and payout {income}.");
        }

        Console.WriteLine($"CAMPAIGN PASS: {encounters.Count} distinct openings, {unitCount} wave units, {sectorNames.Count} sectors and {formations.Count} formations.");
        Console.WriteLine($"STORY PASS: {story.Count} beats, first {story[0]}, final {story[story.Count - 1]}. {bossCount} boss encounters including level 1000.");
        Console.WriteLine($"BALANCE PASS: worst boss budget {worstBossMargin:F2} s at level {worstBoss}. Math model at 57% firing uptime.");
        Console.WriteLine($"PRESSURE: {string.Join(", ", samples.Select(c => $"{c.Key}={c.Value:F2}×"))} enemy HP/s vs. sustained minigun DPS.");
        Console.WriteLine($"TRAFFIC PASS: {hazardCount} obstacle windows, {slowedTraffic} moves, {trafficMargin:F3} s minimum clearance at sprint speed.");
    }
}
