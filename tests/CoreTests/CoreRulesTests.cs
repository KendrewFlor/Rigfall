using System;
using System.Collections.Generic;
using System.Linq;
using Rigfall.Core;

namespace Rigfall.CoreTests
{
    public static class DataTests
    {
        [Test]
        public static void DatabaseLoadsTheDesignedContent()
        {
            var db = Data.Db;
            Assert.Equal(5, db.Classes.Count, "classes");
            Assert.Equal(40, db.Perks.Count, "perks");
            Assert.Equal(12, db.Perks.Count(p => p.source == "starter"), "starter perks");
            Assert.Equal(15, db.Perks.Count(p => p.source == "achievement"), "achievement perks");
            Assert.Equal(19, db.Mutators.Count, "mutators");
            Assert.Equal(5, db.Rarities.Count, "rarities");
            Assert.Equal("brilliant", db.Rarities[4].id, "Blowout Brilliant is the top rarity");
            Assert.Equal(12, db.Units.Count, "units");
            Assert.True(db.Maps.Count >= 5, "five fields");
            Assert.True(db.Bosses().Count >= 3, "bosses");
            foreach (var c in db.Classes) Assert.Equal(5, db.WeaponsFor(c.id).Count, "weapons for " + c.id);
        }
    }

    public static class StateMachineTests
    {
        [Test]
        public static void DayNightLoopFollowsTheAllowedTransitions()
        {
            var log = new List<string>();
            var fsm = NightRules.CreateMachine();
            foreach (GamePhase p in Enum.GetValues(typeof(GamePhase)))
            {
                var phase = p;
                fsm.Add(phase, new LambdaState(() => log.Add("enter " + phase), null, () => log.Add("exit " + phase)));
            }
            fsm.Start(GamePhase.Boot);
            Assert.True(fsm.TransitionTo(GamePhase.MainMenu), "boot -> menu");
            Assert.True(fsm.TransitionTo(GamePhase.Day), "menu -> day");
            Assert.True(!fsm.TransitionTo(GamePhase.GameOver), "day -> game over is not allowed");
            Assert.True(!fsm.TransitionTo(GamePhase.Dawn), "day -> dawn is not allowed");
            Assert.True(fsm.TransitionTo(GamePhase.Night), "day -> night");
            Assert.True(fsm.TransitionTo(GamePhase.Dawn), "night -> dawn");
            Assert.True(fsm.TransitionTo(GamePhase.Day), "dawn -> day");
            Assert.True(fsm.TransitionTo(GamePhase.Night), "day -> night again");
            Assert.True(fsm.TransitionTo(GamePhase.GameOver), "night -> game over");
            Assert.True(fsm.TransitionTo(GamePhase.Day), "retry from last dawn");
            Assert.True(fsm.TransitionTo(GamePhase.MainMenu), "menu from anywhere");
            Assert.Equal("exit Night", log[log.IndexOf("enter GameOver") - 1], "exit runs before enter");
        }

        [Test]
        public static void NightOutcomeMatchesTheDesign()
        {
            Assert.Equal(NightOutcome.Lost, NightRules.Check(0, 5, 3), "rig destroyed");
            Assert.Equal(NightOutcome.Lost, NightRules.Check(-10, 0, 0), "rig destroyed even if the wave is gone");
            Assert.Equal(NightOutcome.Continue, NightRules.Check(100, 1, 0), "enemies left");
            Assert.Equal(NightOutcome.Continue, NightRules.Check(100, 0, 2), "spawns still pending");
            Assert.Equal(NightOutcome.Cleared, NightRules.Check(1, 0, 0), "all cleared");
        }
    }

    public static class WaveTests
    {
        static ModifierSet NoMods() => new ModifierSet();

        [Test]
        public static void NewEnemyEveryThreeNights()
        {
            var map = Data.Db.Map("permian-pad");
            Assert.Equal(map.enemyOrder[0], WaveDirector.NewEnemyOn(map, 1), "night 1");
            Assert.Equal("", WaveDirector.NewEnemyOn(map, 2), "night 2");
            Assert.Equal("", WaveDirector.NewEnemyOn(map, 3), "night 3");
            Assert.Equal(map.enemyOrder[2], WaveDirector.NewEnemyOn(map, 4), "night 4");
            Assert.Equal(map.enemyOrder[3], WaveDirector.NewEnemyOn(map, 7), "night 7");
            Assert.Equal(map.enemyOrder[4], WaveDirector.NewEnemyOn(map, 10), "night 10");
            Assert.Equal(2, WaveDirector.UnlockedEnemyCount(1, 12), "two types on night 1");
            Assert.Equal(3, WaveDirector.UnlockedEnemyCount(4, 12), "three on night 4");
            Assert.Equal(12, WaveDirector.UnlockedEnemyCount(500, 12), "capped at the full list");
        }

        [Test]
        public static void BossesEverySeventhNightAndRotate()
        {
            var db = Data.Db;
            var mods = NoMods();
            int lap;
            Assert.True(WaveDirector.BossFor(db, 6, mods, out lap) == null, "no boss on night 6");
            var b7 = WaveDirector.BossFor(db, 7, mods, out lap);
            var b14 = WaveDirector.BossFor(db, 14, mods, out lap);
            var b21 = WaveDirector.BossFor(db, 21, mods, out lap);
            Assert.True(b7 != null && b14 != null && b21 != null, "bosses on 7, 14, 21");
            Assert.True(b7.id != b14.id && b14.id != b21.id, "different bosses");
            int count = db.Bosses().Count;
            var again = WaveDirector.BossFor(db, 7 * (count + 1), mods, out lap);
            Assert.Equal(b7.id, again.id, "rotation wraps");
            Assert.Equal(1, lap, "second lap");

            var fast = new ModifierSet();
            fast.Add(new StatMod(Stat.BossInterval, "set", 5));
            Assert.True(WaveDirector.IsBossNight(5, fast) && !WaveDirector.IsBossNight(7, fast), "accelerated schedule");
        }

        [Test]
        public static void WavesGrowSlowlyAndNeverEnd()
        {
            var db = Data.Db;
            var map = db.Map("permian-pad");
            var normal = db.Difficulty("normal");
            float lastBudget = 0, lastHp = 0;
            for (int n = 1; n <= 60; n++)
            {
                float b = WaveDirector.Budget(n, normal, NoMods());
                float hp = WaveDirector.HpMultiplier(n, normal, NoMods());
                Assert.True(b > lastBudget && hp >= lastHp, "night " + n + " is harder than the one before");
                if (n > 1) Assert.True(b / lastBudget < 1.8f, "growth is gradual on night " + n);
                lastBudget = b; lastHp = hp;
            }
            var late = WaveDirector.Plan(db, map, 120, normal, NoMods(), 42);
            Assert.True(late.spawns.Count > 0, "night 120 still has a wave");
        }

        [Test]
        public static void PlansAreDeterministicAndUseOpenLanes()
        {
            var db = Data.Db;
            var map = db.Map("delta-swamp");
            var diff = db.Difficulty("hard");
            var a = WaveDirector.Plan(db, map, 9, diff, NoMods(), 7);
            var b = WaveDirector.Plan(db, map, 9, diff, NoMods(), 7);
            Assert.Equal(a.spawns.Count, b.spawns.Count, "same seed, same wave");
            for (int i = 0; i < a.spawns.Count; i++) Assert.Equal(a.spawns[i].enemyId, b.spawns[i].enemyId, "same order");
            var open = WaveDirector.ActiveLanes(map, 1, NoMods());
            Assert.Equal(1, open.Count, "one lane on night 1");
            var plan1 = WaveDirector.Plan(db, map, 1, diff, NoMods(), 7);
            Assert.True(plan1.spawns.All(s => s.lane == open[0]), "night 1 only uses the open lane");
            var extra = new ModifierSet();
            extra.Add(new StatMod(Stat.SpawnExtraLanes, "add", 1));
            Assert.Equal(2, WaveDirector.ActiveLanes(map, 1, extra).Count, "extra lane mutator");
            for (int i = 1; i < a.spawns.Count; i++) Assert.True(a.spawns[i].time >= a.spawns[i - 1].time, "sorted by time");
        }

        [Test]
        public static void HarderDifficultiesMakeBiggerWaves()
        {
            var db = Data.Db;
            float prev = 0;
            foreach (var d in db.Difficulties)
            {
                float b = WaveDirector.Budget(10, d, NoMods()) * WaveDirector.HpMultiplier(10, d, NoMods());
                Assert.True(b >= prev, d.id + " is at least as hard as the one before");
                prev = b;
            }
        }
    }

    public static class EconomyTests
    {
        [Test]
        public static void DawnIncomeCountsPumpjacksAndPerks()
        {
            var db = Data.Db;
            var normal = db.Difficulty("normal");
            var producers = new List<Producer> { new Producer("house", 1, false), new Producer("house", 2, false), new Producer("house", 1, true), new Producer("mill", 2, false) };
            var plain = Economy.Dawn(0, 3, producers, 0, normal, new ModifierSet());
            Assert.Equal(3, plain.rig, "rig income");
            Assert.Equal(3, plain.houses, "knocked-out pumpjack pays nothing");
            Assert.Equal(2, plain.mills, "tank battery");
            Assert.Equal(8, plain.total, "total");

            var mods = new ModifierSet();
            mods.Add(db.Perk("resilient-pumpjacks").mods);
            mods.Add(db.Perk("enhanced-oil-recovery").mods);
            mods.Add(db.Perk("royalty-interest").mods);
            var perked = Economy.Dawn(0, 3, producers, 100, normal, mods);
            Assert.Equal(1 + 2 + 1 + 3, perked.houses, "resilient pumpjacks keep paying, EOR adds 1 each");
            Assert.Equal(5, perked.interest, "interest is capped at 5");
        }

        [Test]
        public static void CostsRespondToPerksAndMutators()
        {
            var db = Data.Db;
            var tower = db.Building("derrick-tower");
            var t0 = tower.tiers[0].options[0];
            var none = new ModifierSet();
            Assert.Equal(t0.cost, Economy.Cost(tower, 0, t0, none, false), "base price");
            var scaffold = new ModifierSet();
            scaffold.Add(db.Perk("modular-rig-up").mods);
            Assert.Equal(t0.cost - 1, Economy.Cost(tower, 0, t0, scaffold, false), "Modular Rig-Up: towers 1 cheaper");
            var lost = new ModifierSet();
            lost.Add(db.Mutator("lost-circulation").mods);
            Assert.True(Economy.Cost(tower, 0, t0, lost, false) > t0.cost, "Lost Circulation raises prices");
            var spot = new ModifierSet();
            spot.Add(db.Perk("spot-price-spike").mods);
            Assert.Equal(t0.cost - 2, Economy.Cost(tower, 0, t0, spot, true), "first purchase of the day");
            Assert.Equal(t0.cost, Economy.Cost(tower, 0, t0, spot, false), "later purchases");
        }

        [Test]
        public static void WeaponUpgradesCostMoreForRarerWeapons()
        {
            Assert.True(WeaponUpgrades.CostToUpgrade(0, 4) > WeaponUpgrades.CostToUpgrade(0, 0), "rarity factor");
            Assert.Equal(-1, WeaponUpgrades.CostToUpgrade(WeaponUpgrades.MaxLevel, 0), "max level");
            Assert.Near(1.4f, WeaponUpgrades.DamageMult(5), 0.001f, "damage at level 5");
        }
    }

    public static class WheelTests
    {
        [Test]
        public static void SegmentsCoverTheWheelAndFollowTheOdds()
        {
            var db = Data.Db;
            var segs = RewardWheel.Segments(db, 0);
            Assert.Near(1f, segs.Sum(s => s.probability), 0.0001f, "probabilities sum to 1");
            Assert.Near(360f, segs.Sum(s => s.sweep), 0.01f, "sweeps fill the circle");
            Assert.True(segs.All(s => s.sweep >= RewardWheel.MinSweep - 0.01f), "every segment visible");
            Assert.Near(0.005f, segs[4].probability, 0.0001f, "Blowout Brilliant is 0.5%");
            var lucky = RewardWheel.Segments(db, 0.5f);
            Assert.True(lucky[4].probability > segs[4].probability * 2, "luck favours rare segments");
        }

        [Test]
        public static void SpinsLandOnTheirSegment()
        {
            var db = Data.Db;
            var rng = new Rng(99);
            var counts = new int[5];
            for (int i = 0; i < 20000; i++)
            {
                var r = RewardWheel.Spin(db, 0, rng, 5);
                counts[r.rarity.order]++;
                Assert.True(r.reward != null && r.reward.rarity == r.rarity.id, "reward matches rarity");
            }
            Assert.Near(0.55f, counts[0] / 20000f, 0.02f, "common share");
            Assert.Near(0.28f, counts[1] / 20000f, 0.02f, "rare share");
            Assert.True(counts[4] > 30 && counts[4] < 200, "brilliant is rare but happens");
        }
    }

    public static class ProgressionTests
    {
        [Test]
        public static void NewPlayerHasTwelvePerkChoices()
        {
            var db = Data.Db;
            var p = Progress.NewProfile(db);
            Assert.Equal(12, Progress.UnlockedPerkCount(p, db), "12 perks at first");
            Assert.True(Progress.IsMapUnlocked(p, db.Map("permian-pad")), "first field open");
            Assert.True(!Progress.IsMapUnlocked(p, db.Map("delta-swamp")), "second field locked");
            Assert.Equal(8, db.Mutators.Count(m => Progress.IsMutatorUnlocked(p, m)), "8 starter mutators");
        }

        [Test]
        public static void ClearingStagesUnlocksFieldsAndWeapons()
        {
            var db = Data.Db;
            var p = Progress.NewProfile(db);
            for (int n = 1; n <= 6; n++) Progress.RecordNight(p, db, "permian-pad", "normal", n, n * 100);
            Assert.True(!Progress.IsMapUnlocked(p, db.Map("delta-swamp")), "still locked before night 7");
            var unlocked = Progress.RecordNight(p, db, "permian-pad", "normal", 7, 700);
            Assert.True(unlocked.Contains("pdc-bit"), "stage I reward weapon");
            Assert.True(Progress.IsMapUnlocked(p, db.Map("delta-swamp")), "next field opens");
            Assert.Equal(1, Progress.Record(p, "permian-pad").stage, "stage recorded");
            Assert.Equal(7, Progress.Record(p, "permian-pad").bestNight, "best night");
        }

        [Test]
        public static void SanitizeDropsTamperedData()
        {
            var db = Data.Db;
            var p = new Profile();
            p.perks = new List<string> { "hardbanding", "not-a-perk", "hardbanding", null };
            p.coreSamples = -50;
            p.weaponLevels = new List<WeaponLevel> { new WeaponLevel { id = "tricone-bit", level = 99 }, new WeaponLevel { id = "fake", level = 1 } };
            p.settings = new GameSettings { masterVolume = 7f, graphicsQuality = 9, fpsCap = 13, musicTrack = "nope" };
            p.loadout = new Loadout { classId = "driller", weaponId = "black-diamond-core-bit", perks = new List<string> { "top-drive", "mud-motor", "hazard-pay", "hardbanding", "crew-change", "gusher" } };
            p.stats = null;
            Progress.Sanitize(p, db);
            Assert.Equal(1, p.perks.Count, "unknown and duplicate perks removed");
            Assert.Equal(0, p.coreSamples, "negative currency clamped");
            Assert.Equal(1, p.weaponLevels.Count, "unknown weapon removed");
            Assert.Equal(WeaponUpgrades.MaxLevel, p.weaponLevels[0].level, "level clamped");
            Assert.Equal(1f, p.settings.masterVolume, "volume clamped");
            Assert.Equal(2, p.settings.graphicsQuality, "quality clamped");
            Assert.Equal(60, p.settings.fpsCap, "fps cap reset");
            Assert.Equal("", p.settings.musicTrack, "unknown track reset");
            Assert.Equal("tricone-bit", p.loadout.weaponId, "locked weapon replaced by the starter");
            Assert.True(p.loadout.perks.Count <= Progress.MaxPerks && !p.loadout.perks.Contains("gusher"), "only 4 owned perks");
            Assert.True(p.stats != null, "stats recreated");
        }

        [Test]
        public static void BlowoutDifficultyTurnsOnAllMutators()
        {
            var db = Data.Db;
            Assert.Equal(19, Progress.ActiveMutators(db, db.Difficulty("blowout"), new List<string>()).Count, "all 19");
            Assert.Equal(1, Progress.ActiveMutators(db, db.Difficulty("hard"), new List<string> { "price-crash", "price-crash" }).Count, "chosen only, no duplicates");
        }

        [Test]
        public static void UnlockRandomPrefersTheRequestedRarity()
        {
            var db = Data.Db;
            var p = Progress.NewProfile(db);
            var rng = new Rng(3);
            string id = Progress.UnlockRandom(p, db, "perk", "legendary", rng);
            Assert.Equal("legendary", db.Perk(id).rarity, "legendary first");
            for (int i = 0; i < 60; i++) Progress.UnlockRandom(p, db, "perk", "legendary", rng);
            Assert.Equal("", Progress.UnlockRandom(p, db, "perk", "legendary", rng), "nothing left to unlock");
        }
    }

    public static class AchievementTests
    {
        [Test]
        public static void SurvivingThreeNightsUnlocksFishingJob()
        {
            var db = Data.Db;
            var p = Progress.NewProfile(db);
            var ctx = new AchievementContext { db = db, profile = p, run = new RunStats { nightsSurvived = 3 }, classId = "driller", difficultyOrder = 1 };
            var got = Achievements.Check(ctx).Select(a => a.id).ToList();
            Assert.True(got.Contains("first-light") && got.Contains("field-hand"), "night achievements");
            Assert.True(Progress.IsPerkUnlocked(p, db.Perk("fishing-job")), "perk granted");
            Assert.Equal(0, Achievements.Check(ctx).Count, "no double unlocks");
        }

        [Test]
        public static void FiltersApply()
        {
            var db = Data.Db;
            var p = Progress.NewProfile(db);
            var ctx = new AchievementContext { db = db, profile = p, run = new RunStats { nightsSurvived = 7 }, classId = "driller", difficultyOrder = 1 };
            var got = Achievements.Check(ctx).Select(a => a.id).ToList();
            Assert.True(!got.Contains("kick-drill"), "kick drill needs Hard");
            ctx.difficultyOrder = 2;
            got = Achievements.Check(ctx).Select(a => a.id).ToList();
            Assert.True(got.Contains("kick-drill"), "now on Hard");
        }
    }

    public static class QuestTests
    {
        [Test]
        public static void ContractsTrackProgressAndFailures()
        {
            var db = Data.Db;
            var q = new QuestState { questId = "q-hero-kills", target = 3 };
            var gasKick = db.Enemy("gas-kick");
            Quests.OnKill(q, db, gasKick, KillSource.Tower);
            Assert.Equal(0, q.progress, "tower kills don't count");
            for (int i = 0; i < 3; i++) Quests.OnKill(q, db, gasKick, KillSource.Hero);
            Assert.True(Quests.IsComplete(q, db), "complete");

            var wall = new QuestState { questId = "q-no-wall", target = 1 };
            Assert.True(Quests.IsComplete(wall, db), "holding");
            Quests.OnBuildingLost(wall, db, "wall");
            Assert.True(!Quests.IsComplete(wall, db), "failed after a wall falls");

            var picked = Quests.Pick(db, 7, true, new List<string> { "gas-kick" }, new Rng(1));
            Assert.True(!string.IsNullOrEmpty(picked.questId) && picked.target >= 1, "a contract is offered");
        }
    }

    public static class BuildingTests
    {
        [Test]
        public static void UpgradePathsRespectRequirementsAndRigTier()
        {
            var db = Data.Db;
            var tower = db.Building("derrick-tower");
            var mods = new ModifierSet();
            var first = BuildingRules.NextChoices(tower, new List<string>(), 0, mods, false);
            Assert.Equal(1, first.Count, "one way to build a tower");
            var second = BuildingRules.NextChoices(tower, new List<string> { "tower-t0" }, 0, mods, false);
            Assert.Equal(2, second.Count, "two branches");
            Assert.True(second.All(c => c.locked), "locked below rig tier 1");
            mods.Add(db.Perk("modular-rig-up").mods);
            Assert.True(BuildingRules.NextChoices(tower, new List<string> { "tower-t0" }, 0, mods, false).All(c => !c.locked), "Modular Rig-Up lifts the limit");
            var third = BuildingRules.NextChoices(tower, new List<string> { "tower-t0", "flare-stack" }, 2, mods, false);
            Assert.Equal(1, third.Count, "only the matching branch");
            Assert.Equal("thermal-oxidizer", third[0].option.id, "flare stack leads to the oxidizer");
            Assert.True(BuildingRules.IsValidPath(tower, new List<string> { "tower-t0", "flare-stack", "thermal-oxidizer" }), "valid path");
            Assert.True(!BuildingRules.IsValidPath(tower, new List<string> { "tower-t0", "flare-stack", "monkeyboard-overwatch" }), "invalid path");
        }

        [Test]
        public static void BarracksFieldUnitsPlusDoubleCrew()
        {
            var db = Data.Db;
            var camp = db.Building("crew-camp");
            var path = new List<string> { "camp-tong-hand", "camp-second-shift" };
            Assert.Equal("tong-hand", BuildingRules.UnitId(camp, path), "unit chosen at tier 0");
            var mods = new ModifierSet();
            Assert.Equal(3, BuildingRules.UnitCount(camp, path, mods), "three after Second Shift");
            mods.Add(db.Perk("double-crew").mods);
            Assert.Equal(4, BuildingRules.UnitCount(camp, path, mods), "Double Crew adds one");
        }
    }

    public static class ModifierTests
    {
        [Test]
        public static void ModifiersStackAndNightLayerResets()
        {
            var m = new ModifierSet();
            m.Add(new StatMod("hero.damage", "mul", 1.2f));
            m.Add(new StatMod("hero.damage", "mul", 1.1f));
            m.Add(new StatMod("income.house", "add", 1));
            Assert.Near(1.32f, m.Mul("hero.damage"), 0.0001f, "multiplicative stacking");
            Assert.Near(3f, m.Get("income.house", 2), 0.0001f, "additive");
            m.SetNightLayer(new[] { new StatMod("enemy.speed", "mul", 1.3f) }, "fog");
            Assert.Near(1.3f, m.Mul("enemy.speed"), 0.0001f, "night layer applies");
            Assert.True(m.HasSpecial("fog"), "night special");
            m.ClearNightLayer();
            Assert.Near(1f, m.Mul("enemy.speed"), 0.0001f, "and resets at dawn");
            m.Add(new StatMod("tower.ignoreRigTier", "set", 1));
            Assert.True(m.Flag("tower.ignoreRigTier"), "flags");
        }
    }
}
