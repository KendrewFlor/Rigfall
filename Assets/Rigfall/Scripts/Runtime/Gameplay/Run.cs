// One run on one field: the world (field, rig, slots, buildings, units, enemies, hero),
// the economy (barrels), modifiers from perks/mutators/blessings, and per-run stats.
// The Game's state machine drives it: BeginDay -> BeginNight -> EndNight (dawn) -> PrepareNextDay.
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Run : MonoBehaviour
    {
        public static Run Current { get; private set; }

        // ---- loadout ----------------------------------------------------------------------
        public MapDef Map { get; private set; }
        public DifficultyDef Difficulty { get; private set; }
        public ClassDef Class { get; private set; }
        public WeaponDef Weapon { get; private set; }
        public readonly List<PerkDef> Perks = new List<PerkDef>();
        public readonly List<MutatorDef> Mutators = new List<MutatorDef>();
        public readonly List<string> Blessings = new List<string>();
        public readonly ModifierSet Mods = new ModifierSet();
        public float ScoreMultiplier { get; private set; } = 1f;

        // ---- state ------------------------------------------------------------------------
        public int Night { get; private set; } = 1;        // the night this day leads into / the current night
        public int Barrels { get; private set; }
        public int Score { get; private set; }
        public int FreeUpgrades { get; set; }
        public bool FirstPurchaseToday { get; set; } = true;
        public bool ReliefUsed { get; set; }
        public bool BopUsedTonight { get; set; }
        public string Seed { get; private set; } = "";
        public ulong SeedHash { get; private set; }
        public RunStats Stats { get; private set; } = new RunStats();
        public QuestState Quest { get; private set; } = new QuestState();
        public WavePlan Plan { get; private set; }
        public string ChaosMutatorTonight { get; private set; } = "";
        public bool NightLostBuilding { get; private set; }
        public bool IsNight { get; private set; }
        public bool IsTutorial { get; private set; }

        // ---- world ------------------------------------------------------------------------
        public Field Field { get; private set; }
        public MainRig Rig { get; private set; }
        public Hero Hero { get; private set; }
        public WaveSpawner Spawner { get; private set; }
        public readonly List<BuildingBase> Buildings = new List<BuildingBase>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<FriendlyUnit> Units = new List<FriendlyUnit>();

        public int RigTier => Rig != null ? Rig.Tier : 0;
        public int UnitsAlive { get { int n = 0; foreach (var u in Units) if (u != null && u.Alive) n++; return n; } }
        public int EnemiesAlive { get { int n = 0; foreach (var e in Enemies) if (e != null && e.Alive) n++; return n; } }

        static GameDatabase Db => Game.Db;

        // ---- creation ---------------------------------------------------------------------

        public static Run Create(RunSnapshot snap, bool tutorial)
        {
            if (Current != null) Current.Dispose();
            var go = new GameObject("Run");
            var run = go.AddComponent<Run>();
            Current = run;
            run.IsTutorial = tutorial;
            run.Setup(snap);
            return run;
        }

        /// <summary>A fresh snapshot for night 1 from a loadout.</summary>
        public static RunSnapshot NewSnapshot(Loadout l)
        {
            var map = Db.Map(l.mapId);
            var diff = Db.Difficulty(l.difficultyId);
            var s = new RunSnapshot
            {
                valid = true,
                mapId = l.mapId,
                difficultyId = l.difficultyId,
                classId = l.classId,
                weaponId = l.weaponId,
                night = 1,
                barrels = Economy.StartingBarrels(map, diff),
                seed = System.DateTime.UtcNow.Ticks.ToString("x") + "-" + Random.Range(0, int.MaxValue).ToString("x"),
            };
            s.perks.AddRange(l.perks);
            s.mutators.AddRange(l.mutators);
            return s;
        }

        void Setup(RunSnapshot s)
        {
            Map = Db.Map(s.mapId);
            Difficulty = Db.Difficulty(s.difficultyId);
            Class = Db.Class(s.classId);
            Weapon = Db.Weapon(s.weaponId);
            Night = Mathf.Max(1, s.night);
            Barrels = s.barrels;
            Score = s.score;
            FreeUpgrades = s.freeUpgrades;
            ReliefUsed = s.reliefUsed;
            Seed = string.IsNullOrEmpty(s.seed) ? "rigfall" : s.seed;
            SeedHash = Rng.Hash(Seed);
            Stats = s.stats ?? new RunStats();
            Quest = s.quest ?? new QuestState();

            Mods.Clear();
            foreach (var id in s.perks)
            {
                var p = Db.Perk(id);
                if (p == null) continue;
                Perks.Add(p);
                Mods.Add(p.mods);
                Mods.AddSpecial(p.special);
            }
            Mutators.AddRange(Progress.ActiveMutators(Db, Difficulty, s.mutators));
            foreach (var m in Mutators)
            {
                if (m.special == "chaos") { Mods.AddSpecial("chaos"); continue; }
                Mods.Add(m.mods);
                Mods.AddSpecial(m.special);
            }
            foreach (var b in s.blessings)
            {
                var w = Db.Wheel.Find(x => x.id == b);
                if (w == null) continue;
                Blessings.Add(b);
                Mods.Add(w.mods);
            }
            ScoreMultiplier = Scoring.Multiplier(Difficulty, Mutators);

            Field = MapBuilder.Build(Map, transform, false);
            BuildRig(s);
            foreach (var slot in Field.Slots)
            {
                var saved = s.buildings.Find(b => b.slotId == slot.Def.id);
                if (saved != null && saved.buildingId == slot.Def.building) slot.Restore(saved.path);
            }
            var heroGo = new GameObject("Hero");
            heroGo.transform.SetParent(transform, false);
            Hero = heroGo.AddComponent<Hero>();
            Hero.Setup(Class, Weapon, Progress.WeaponLevelOf(Game.Profile, Weapon.id), Field.HeroSpawn);
            var spawnerGo = new GameObject("WaveSpawner");
            spawnerGo.transform.SetParent(transform, false);
            Spawner = spawnerGo.AddComponent<WaveSpawner>();
            CameraRig.Instance.Follow(Hero.transform, true);
            Game.UI.Hud.Bind(this);
        }

        void BuildRig(RunSnapshot s)
        {
            var def = Db.Building("main-rig");
            var saved = s.buildings.Find(b => b.slotId == "rig");
            var path = saved != null && BuildingRules.IsValidPath(def, saved.path) ? saved.path : new List<string> { def.tiers[0].options[0].id };
            var go = new GameObject("Main Rig");
            go.transform.SetParent(Field.BuildingRoot, false);
            go.transform.position = Vector3.zero;
            Rig = go.AddComponent<MainRig>();
            Rig.Setup(def, null, path);
            Rig.Interaction = Field.RigSlot;
            if (Field.RigSlot != null) Field.RigSlot.AttachExisting(Rig);
        }

        public void Dispose()
        {
            Projectile.ClearAll();
            if (Current == this) Current = null;
            DestroyImmediate(gameObject); // immediately, so the next field never overlaps this one's NavMesh
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---- economy ----------------------------------------------------------------------

        public bool CanAfford(int price) => price <= 0 || FreeUpgrades > 0 || Barrels >= price;

        /// <summary>Pay for a build or upgrade. Free work orders from the wheel are used first.</summary>
        public bool Spend(int price)
        {
            if (price <= 0) { FirstPurchaseToday = false; return true; }
            if (FreeUpgrades > 0) { FreeUpgrades--; FirstPurchaseToday = false; return true; }
            if (Barrels < price) return false;
            Barrels -= price;
            FirstPurchaseToday = false;
            return true;
        }

        public void AddBarrels(int amount)
        {
            if (amount <= 0) return;
            Barrels += amount;
            Stats.barrelsEarned += amount;
            if (Barrels > Stats.maxBarrels) Stats.maxBarrels = Barrels;
            Game.Profile.stats.barrelsEarned += amount;
            Game.UI.Hud.PunchBarrels();
            Game.CheckAchievements();
        }

        public void AddScore(int points)
        {
            if (points <= 0) return;
            Score += Mathf.RoundToInt(points * ScoreMultiplier);
        }

        // ---- day / night ------------------------------------------------------------------

        public void BeginDay()
        {
            IsNight = false;
            FirstPurchaseToday = true;
            Plan = WaveDirector.Plan(Db, Map, Night, Difficulty, Mods, SeedHash);
            if (string.IsNullOrEmpty(Quest.questId))
            {
                var killable = new List<string>();
                foreach (var sp in Plan.spawns) if (!sp.boss && !killable.Contains(sp.enemyId)) killable.Add(sp.enemyId);
                Quest = Quests.Pick(Db, Night, !string.IsNullOrEmpty(Plan.bossId), killable, new Rng(SeedHash ^ (ulong)(Night * 7919)));
            }
            foreach (var slot in Field.Slots) slot.SetInteractive(true);
            if (Field.RigSlot != null) Field.RigSlot.SetInteractive(true);
            Field.ShowLanePreview(Plan, Mods.HasSpecial("kickDetection"));
            foreach (var b in Buildings) if (b != null) b.OnDayStart();
            foreach (var u in Units) if (u != null) u.OnDayStart();
            Hero.OnDayStart();
        }

        public void BeginNight()
        {
            IsNight = true;
            NightLostBuilding = false;
            BopUsedTonight = false;
            Stats.maxUnitsAlive = Mathf.Max(Stats.maxUnitsAlive, UnitsAlive);
            foreach (var slot in Field.Slots) slot.SetInteractive(false);
            if (Field.RigSlot != null) Field.RigSlot.SetInteractive(false);
            Field.HideLanePreview();

            ChaosMutatorTonight = "";
            if (Mods.HasSpecial("chaos"))
            {
                var pool = Db.Mutators.FindAll(m => m.special != "chaos" && !Mutators.Contains(m));
                if (pool.Count > 0)
                {
                    var pick = new Rng(SeedHash ^ (ulong)(Night * 104729)).Pick(pool);
                    ChaosMutatorTonight = pick.id;
                    Mods.SetNightLayer(pick.mods, pick.special);
                    Game.UI.Toast("Uncontrolled Flow: " + pick.name + " tonight");
                }
            }
            // Plan again in case the night layer changed it (Uncontrolled Flow).
            Plan = WaveDirector.Plan(Db, Map, Night, Difficulty, Mods, SeedHash);
            Quests.OnNightStart(Quest, Db, Barrels);
            foreach (var b in Buildings) if (b != null) b.OnNightStart();
            Spawner.Begin(Plan, Field);
        }

        public sealed class DawnReport
        {
            public IncomeBreakdown income;
            public bool questDone;
            public string questName = "";
            public int questBarrels, questCore;
            public List<string> newWeapons = new List<string>();
            public bool stageCleared;
            public int stage;
            public int corePerNight;
        }

        /// <summary>The night was cleared: pay out, repair, record progress.</summary>
        public DawnReport EndNight()
        {
            IsNight = false;
            var report = new DawnReport();
            Mods.ClearNightLayer();
            ChaosMutatorTonight = "";
            Stats.nightsSurvived = Night;
            if (!NightLostBuilding && Night > Stats.bestCleanNight) Stats.bestCleanNight = Night;
            AddScore(Scoring.ForNight(Night));

            // Quest payout.
            var qdef = Db.Quest(Quest.questId);
            if (qdef != null)
            {
                report.questName = qdef.name;
                report.questDone = Quests.IsComplete(Quest, Db);
                if (report.questDone)
                {
                    report.questBarrels = qdef.rewardBarrels;
                    report.questCore = qdef.rewardCore;
                }
            }

            // Income.
            var producers = new List<Producer>();
            foreach (var b in Buildings)
            {
                var h = b as House;
                if (h != null) producers.Add(new Producer(b.Def.kind, h.Income, b.Destroyed));
            }
            report.income = Economy.Dawn(RigTier, Rig.Option.income, producers, Barrels, Difficulty, Mods);

            // Permanent progress.
            var profile = Game.Profile;
            int before = Progress.Record(profile, Map.id).stage;
            report.newWeapons = Progress.RecordNight(profile, Db, Map.id, Difficulty.id, Night, Score);
            report.stage = Progress.Record(profile, Map.id).stage;
            report.stageCleared = report.stage > before;
            if (Night >= 3 && !profile.classesNight3.Contains(Class.id)) profile.classesNight3.Add(Class.id);
            report.corePerNight = 1 + Mathf.RoundToInt(Mods.Get(Stat.CorePerNight, 0)) + Night / 7;
            profile.coreSamples += report.corePerNight + report.questCore;

            // Pay out now (the dawn screen animates the numbers).
            AddBarrels(report.income.total + report.questBarrels);

            // Repair and rebuild everything, heal units.
            foreach (var b in Buildings) if (b != null) b.OnDawn();
            bool healUnits = !Mods.Flag(Stat.UnitNoDawnHeal);
            foreach (var u in Units) if (u != null) u.OnDawn(healUnits);
            Hero.OnDawn();
            Game.CheckAchievements();
            Game.SaveProfile();
            return report;
        }

        /// <summary>After the wheel: advance to the next day and checkpoint.</summary>
        public void PrepareNextDay()
        {
            Night++;
            Quest = new QuestState();
            Plan = null;
            SaveCheckpoint(false);
        }

        public void SaveCheckpoint(bool isRunStart)
        {
            if (IsTutorial) return;
            SaveService.SaveRun(Snapshot(), isRunStart);
        }

        public RunSnapshot Snapshot()
        {
            var s = new RunSnapshot
            {
                valid = true,
                mapId = Map.id,
                difficultyId = Difficulty.id,
                classId = Class.id,
                weaponId = Weapon.id,
                night = Night,
                barrels = Barrels,
                score = Score,
                freeUpgrades = FreeUpgrades,
                reliefUsed = ReliefUsed,
                seed = Seed,
                quest = Quest,
                stats = Stats,
            };
            foreach (var p in Perks) s.perks.Add(p.id);
            foreach (var m in Mutators) s.mutators.Add(m.id);
            s.blessings.AddRange(Blessings);
            if (Rig != null) s.buildings.Add(new BuildingState { slotId = "rig", buildingId = Rig.Def.id, path = new List<string>(Rig.Path) });
            foreach (var slot in Field.Slots)
                if (slot.Building != null) s.buildings.Add(new BuildingState { slotId = slot.Def.id, buildingId = slot.Def.building, path = new List<string>(slot.Building.Path) });
            return s;
        }

        // ---- wheel ------------------------------------------------------------------------

        /// <summary>Apply a wheel reward. Returns extra text (e.g. what was unlocked).</summary>
        public string ApplyWheel(WheelResult r)
        {
            if (r == null || r.reward == null) return "";
            Stats.wheelRarities.Add(r.rarity.id);
            Game.Profile.stats.wheelSpins++;
            var w = r.reward;
            var rng = new Rng(SeedHash ^ (ulong)(Night * 31337));
            string extra = "";
            switch (w.kind)
            {
                case "barrels": AddBarrels(r.amount); break;
                case "core": Game.Profile.coreSamples += r.amount; break;
                case "rigHeal":
                    Blessings.Add(w.id); Mods.Add(w.mods);
                    Rig.RefreshStats();
                    Rig.Health.Heal(Rig.Health.max * w.amount);
                    break;
                case "blessing":
                    Blessings.Add(w.id); Mods.Add(w.mods);
                    if (w.target == "gusher" && !Game.Profile.perks.Contains("gusher")) { Game.Profile.perks.Add("gusher"); extra = "Perk unlocked: Gusher"; }
                    foreach (var b in Buildings) if (b != null) b.RefreshStats();
                    foreach (var u in Units) if (u != null) u.RefreshStats();
                    Hero.RefreshStats();
                    break;
                case "freeUpgrade": FreeUpgrades++; break;
                case "unlockPerk":
                case "unlockMutator":
                case "unlockWeapon":
                {
                    string kind = w.kind == "unlockPerk" ? "perk" : w.kind == "unlockMutator" ? "mutator" : "weapon";
                    string id = Progress.UnlockRandom(Game.Profile, Db, kind, w.target, rng);
                    if (string.IsNullOrEmpty(id))
                    {
                        int core = 20 + 10 * Db.RarityOrder(w.target);
                        Game.Profile.coreSamples += core;
                        extra = "Everything unlocked already: +" + core + " core samples instead";
                    }
                    else
                    {
                        string name = kind == "perk" ? Db.Perk(id).name : kind == "mutator" ? Db.Mutator(id).name : Db.Weapon(id).name;
                        extra = (kind == "perk" ? "Perk" : kind == "mutator" ? "Mutator" : "Weapon") + " unlocked: " + name;
                    }
                    if (w.kind == "unlockWeapon" && r.amount > 0) AddBarrels(r.amount);
                    break;
                }
            }
            Game.CheckAchievements();
            Game.SaveProfile();
            return extra;
        }

        // ---- events from the world --------------------------------------------------------

        public void OnEnemyKilled(Enemy e, KillSource source)
        {
            var def = e.Def;
            Stats.kills++;
            var ps = Game.Profile.stats;
            ps.kills++;
            switch (source)
            {
                case KillSource.Hero: Stats.heroKills++; ps.heroKills++; break;
                case KillSource.Tower: Stats.towerKills++; ps.towerKills++; break;
                case KillSource.RangedUnit: Stats.unitKills++; Stats.rangedUnitKills++; ps.unitKills++; ps.rangedUnitKills++; break;
                case KillSource.MeleeUnit: Stats.unitKills++; ps.unitKills++; break;
            }
            if (def.flying) { Stats.flyingKills++; ps.flyingKills++; }
            if (def.boss)
            {
                Stats.bossKills++; ps.bossKills++;
                if (!Stats.bossesKilled.Contains(def.id)) Stats.bossesKilled.Add(def.id);
                Fx.Shake(1f);
                Game.UI.Banner(def.name + " defeated!", Palette.Crit);
                AudioService.Ui("sfx_victory_stage");
            }
            Quests.OnKill(Quest, Db, def, source);
            AddScore(def.score * (e.Elite ? 2 : 1));
            float bounty = Mods.Get(Stat.BountyChance, 0);
            if (bounty > 0 && Random.value < bounty)
            {
                Barrels++;
                Fx.Emit(FxKind.Coin, e.transform.position + Vector3.up, 8);
                Game.UI.Hud.PunchBarrels();
            }
            if (def.boss || Stats.kills % 10 == 0) Game.CheckAchievements();
        }

        public void OnBuildingDestroyed(BuildingBase b)
        {
            NightLostBuilding = true;
            Quests.OnBuildingLost(Quest, Db, b.Def.kind);
        }

        public void OnRigDamaged() => Quests.OnRigDamaged(Quest, Db);

        public void OnUnitDied(FriendlyUnit u) => Quests.OnUnitLost(Quest, Db);

        public void OnBuilt(BuildingBase b, bool upgrade)
        {
            Stats.buildingsBuilt++;
            Game.Profile.stats.buildingsBuilt++;
            Quests.OnBuildAction(Quest, Db);
            if (b.Def.kind == "barracks" && !Stats.unitBuildingKinds.Contains(b.Def.id)) Stats.unitBuildingKinds.Add(b.Def.id);
            Tutorial.Notify("build:" + b.Def.id);
            Game.CheckAchievements();
        }

        public Dictionary<string, int> BuildingCounts()
        {
            var d = new Dictionary<string, int>();
            foreach (var b in Buildings)
            {
                if (b == null || b.Destroyed) continue;
                int n;
                d.TryGetValue(b.Def.id, out n);
                d[b.Def.id] = n + 1;
            }
            return d;
        }

        public void Register(BuildingBase b) { if (!Buildings.Contains(b)) Buildings.Add(b); }
        public void Register(FriendlyUnit u) { if (!Units.Contains(u)) Units.Add(u); Stats.maxUnitsAlive = Mathf.Max(Stats.maxUnitsAlive, UnitsAlive); }
        public void Register(Enemy e) { if (!Enemies.Contains(e)) Enemies.Add(e); }
        public void Unregister(FriendlyUnit u) => Units.Remove(u);
        public void Unregister(Enemy e) => Enemies.Remove(e);
    }
}
