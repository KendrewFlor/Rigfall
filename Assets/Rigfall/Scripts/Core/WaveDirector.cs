// Builds the enemy wave for a night. Pure function of (field, night, difficulty, modifiers,
// seed), so the same night always plays out the same way and it can be unit tested.
//
// Rules from the design:
//   * The cycle never ends; each night is a little harder than the last.
//   * A new enemy type joins every 3 nights (two on night 1, then nights 4, 7, 10, ...),
//     in the field's own order: different fields, different enemies.
//   * A boss arrives every 7th night (7, 14, 21, ...; every 5th with Accelerated Schedule),
//     rotating through the bosses and getting stronger each lap.
//   * Enemies come from predefined spawn vectors; more of them open as the nights go on.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    public sealed class WaveSpawn
    {
        public string enemyId;
        public int lane;        // index into MapDef.spawns
        public float time;      // seconds after night start
        public bool elite;
        public bool boss;
    }

    public sealed class WavePlan
    {
        public int night;
        public readonly List<WaveSpawn> spawns = new List<WaveSpawn>();
        public readonly List<int> lanes = new List<int>();
        public int primaryLane;
        public string newEnemyId = "";
        public string bossId = "";
        public int bossLap;          // 0 for the first time a boss appears, 1 the second time, ...
        public float hpMult = 1, damageMult = 1, speedMult = 1, bossHpMult = 1;
        public float budget;
        public float duration;
    }

    public static class WaveDirector
    {
        public const int NightsPerNewEnemy = 3;
        public const int DefaultBossInterval = 7;

        /// <summary>How many enemy types from the field's order are active on this night.</summary>
        public static int UnlockedEnemyCount(int night, int orderLength)
        {
            int n = 2 + Math.Max(0, (night - 1) / NightsPerNewEnemy);
            return Math.Min(n, orderLength);
        }

        /// <summary>The enemy introduced on this night, or "" if none.</summary>
        public static string NewEnemyOn(MapDef map, int night)
        {
            if (night < 1 || map.enemyOrder.Length == 0) return "";
            if (night == 1) return map.enemyOrder[0];
            if ((night - 1) % NightsPerNewEnemy != 0) return "";
            int index = 1 + (night - 1) / NightsPerNewEnemy;
            return index < map.enemyOrder.Length ? map.enemyOrder[index] : "";
        }

        public static int BossInterval(ModifierSet mods)
        {
            int interval = (int)Math.Round(mods.Get(Stat.BossInterval, DefaultBossInterval));
            return Math.Max(3, interval);
        }

        public static bool IsBossNight(int night, ModifierSet mods) => night > 0 && night % BossInterval(mods) == 0;

        /// <summary>The boss for this night (rotating), and how many times the rotation has wrapped.</summary>
        public static EnemyDef BossFor(GameDatabase db, int night, ModifierSet mods, out int lap)
        {
            lap = 0;
            var bosses = db.Bosses();
            if (bosses.Count == 0 || !IsBossNight(night, mods)) return null;
            int index = night / BossInterval(mods) - 1;
            lap = index / bosses.Count;
            return bosses[index % bosses.Count];
        }

        /// <summary>Lanes open on this night: those whose fromNight has passed, plus extra lanes from mutators.</summary>
        public static List<int> ActiveLanes(MapDef map, int night, ModifierSet mods)
        {
            var open = new List<int>();
            var closed = new List<int>();
            for (int i = 0; i < map.spawns.Length; i++)
                (map.spawns[i].fromNight <= night ? open : closed).Add(i);
            closed.Sort((a, b) => map.spawns[a].fromNight.CompareTo(map.spawns[b].fromNight));
            int extra = (int)Math.Round(mods.Get(Stat.SpawnExtraLanes, 0));
            for (int i = 0; i < extra && i < closed.Count; i++) open.Add(closed[i]);
            if (open.Count == 0 && map.spawns.Length > 0) open.Add(0);
            open.Sort();
            return open;
        }

        public static float Budget(int night, DifficultyDef diff, ModifierSet mods)
        {
            float n = Math.Max(0, night - 1);
            float growth = (5.5f * n + 0.12f * n * n) * diff.scaling;
            return (8f + growth) * diff.enemyCount * mods.Mul(Stat.EnemyCount);
        }

        public static float HpMultiplier(int night, DifficultyDef diff, ModifierSet mods)
        {
            float n = Math.Max(0, night - 1);
            float week = (float)Math.Floor(n / 7f);
            return diff.enemyHp * mods.Mul(Stat.EnemyHp) * (1f + 0.06f * n * diff.scaling) * (1f + 0.1f * week);
        }

        public static float DamageMultiplier(int night, DifficultyDef diff, ModifierSet mods)
        {
            float n = Math.Max(0, night - 1);
            return diff.enemyDamage * mods.Mul(Stat.EnemyDamage) * (1f + 0.03f * n * diff.scaling);
        }

        public static float EliteChance(int night, DifficultyDef diff, ModifierSet mods)
        {
            float c = diff.eliteChance + mods.Get(Stat.EnemyEliteChance, 0) + 0.004f * Math.Max(0, night - 1);
            return Math.Min(0.6f, Math.Max(0f, c));
        }

        public static WavePlan Plan(GameDatabase db, MapDef map, int night, DifficultyDef diff, ModifierSet mods, ulong runSeed)
        {
            var plan = new WavePlan { night = night };
            var rng = new Rng(runSeed ^ Rng.Hash(map.id + ":night:" + night));

            plan.lanes.AddRange(ActiveLanes(map, night, mods));
            if (plan.lanes.Count == 0 || night < 1) return plan; // a field with no spawn points has no waves
            plan.primaryLane = plan.lanes[(night - 1) % plan.lanes.Count];
            plan.newEnemyId = NewEnemyOn(map, night);
            plan.hpMult = HpMultiplier(night, diff, mods);
            plan.damageMult = DamageMultiplier(night, diff, mods);
            plan.speedMult = Math.Min(1.8f, diff.enemySpeed * mods.Mul(Stat.EnemySpeed));

            // Which enemy types are available tonight.
            var pool = new List<EnemyDef>();
            int unlocked = UnlockedEnemyCount(night, map.enemyOrder.Length);
            for (int i = 0; i < unlocked; i++)
            {
                var e = db.Enemy(map.enemyOrder[i]);
                if (e != null && !e.boss && e.threat > 0) pool.Add(e);
            }
            if (mods.HasSpecial("earlyFlyers") && night >= 2 && !pool.Exists(e => e.flying))
            {
                foreach (var id in map.enemyOrder)
                {
                    var e = db.Enemy(id);
                    if (e != null && e.flying && e.threat > 0) { pool.Add(e); break; }
                }
            }
            if (pool.Count == 0) return plan;

            int lap;
            var boss = BossFor(db, night, mods, out lap);
            float budget = Budget(night, diff, mods);
            if (boss != null) budget *= 0.6f;
            plan.budget = budget;

            float flyingBias = 1f + 5f * mods.Get(Stat.EnemyFlyingShare, 0);
            string newest = pool[pool.Count - 1].id;
            var weights = new List<float>();
            foreach (var e in pool)
            {
                float w = 1f / (float)Math.Sqrt(Math.Max(1, e.threat));
                if (e.id == newest && night > 1) w *= 1.8f;
                if (e.flying) w *= flyingBias;
                weights.Add(w);
            }

            float eliteChance = EliteChance(night, diff, mods);
            float duration = Math.Min(60f, 18f + 1.4f * night);
            plan.duration = duration;

            // Spend the budget. Each pick goes into a small group on one lane.
            float spent = 0;
            int guard = 0;
            var picks = new List<WaveSpawn>();
            while (spent < budget && guard++ < 2000)
            {
                var e = pool[rng.Weighted(weights)];
                bool elite = night >= 3 && rng.Chance(eliteChance);
                float cost = e.threat * (elite ? 2.5f : 1f);
                if (spent + cost > budget * 1.1f && picks.Count > 0)
                {
                    // Too expensive for what's left: try the cheapest type instead, or stop.
                    var cheapest = pool[0];
                    foreach (var p in pool) if (p.threat < cheapest.threat) cheapest = p;
                    if (spent + cheapest.threat > budget * 1.1f) break;
                    e = cheapest; elite = false; cost = e.threat;
                }
                picks.Add(new WaveSpawn { enemyId = e.id, elite = elite });
                spent += cost;
            }

            // Lane assignment: the primary lane gets about half of the wave.
            foreach (var s in picks)
                s.lane = (plan.lanes.Count == 1 || rng.Chance(0.5f)) ? plan.primaryLane : rng.Pick(plan.lanes);

            // Timing: groups of 3-6 arrive together; groups are spread across the night.
            rng.Shuffle(picks);
            float t = 2f;
            int i0 = 0;
            while (i0 < picks.Count)
            {
                int size = Math.Min(picks.Count - i0, rng.Range(3, 7));
                for (int k = 0; k < size; k++) picks[i0 + k].time = t + k * 0.35f;
                i0 += size;
                t += Math.Max(1.2f, duration / Math.Max(1f, picks.Count / 4.5f)) * rng.Range(0.7f, 1.3f);
            }
            plan.spawns.AddRange(picks);

            if (boss != null)
            {
                plan.bossId = boss.id;
                plan.bossLap = lap;
                plan.bossHpMult = 1f + 0.6f * lap;
                plan.spawns.Add(new WaveSpawn { enemyId = boss.id, lane = plan.primaryLane, time = 4f, boss = true });
            }
            plan.spawns.Sort((a, b) => a.time.CompareTo(b.time));
            return plan;
        }
    }
}
