// The game's root object and its finite state machine.
//
//   MainMenu -> Day -> Night -> Dawn -> Day -> Night -> ...      (endless)
//                        \-> GameOver -> Day (retry from last dawn / restart)
//
// DAY:    no enemies, free movement, slots are interactive, barrels were paid at dawn.
// NIGHT:  prompts hidden, the wave spawns from the field's spawn vectors for this night,
//         enemy and unit AI fight; Main Rig at 0 -> GameOver, everything cleared -> Dawn.
// DAWN:   income, contract payout, repairs, then the reward wheel; Continue -> Day.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public static GameDatabase Db { get; private set; }
        public static Profile Profile { get; private set; }
        public static UIManager UI { get; private set; }
        static readonly GameSettings fallbackSettings = new GameSettings();
        public static GameSettings Settings => Profile != null ? Profile.settings : fallbackSettings;

        /// <summary>Time is stopped (pause menu, game over).</summary>
        public static bool Paused { get; private set; }
        /// <summary>The hero shouldn't react to input (paused, or a menu/dawn screen is open).</summary>
        public static bool InputBlocked => Paused || (UI != null && UI.BlocksInput);

        public StateMachine<GamePhase> Fsm { get; private set; }
        public Run.DawnReport LastDawn { get; private set; }
        Field menuField;
        float nightHold;
        const float NightHoldTime = 1.1f;

        public static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("Rigfall");
            DontDestroyOnLoad(go);
            go.AddComponent<Game>();
        }

        void Awake()
        {
            I = this;
            Application.runInBackground = false;
            Db = DataLoader.LoadAll();
            Profile = SaveService.LoadProfile(Db);
            CameraRig.Create(transform);
            Tweener.Init(transform);
            Fx.Init(transform);
            AudioService.Init(transform);
            GraphicsApplier.Apply(Settings);
            UI = UIManager.Create(transform);

            Fsm = NightRules.CreateMachine();
            Fsm.Add(GamePhase.Boot, new LambdaState());
            Fsm.Add(GamePhase.MainMenu, new LambdaState(EnterMenu, null, ExitMenu));
            Fsm.Add(GamePhase.Day, new LambdaState(EnterDay, TickDay, ExitDay));
            Fsm.Add(GamePhase.Night, new LambdaState(EnterNight, TickNight, null));
            Fsm.Add(GamePhase.Dawn, new LambdaState(EnterDawn, null, null));
            Fsm.Add(GamePhase.GameOver, new LambdaState(EnterGameOver, null, ExitGameOver));
            Fsm.Changed += (from, to) => Debug.Log("Rigfall: " + from + " -> " + to);
            Fsm.Start(GamePhase.Boot);
            Fsm.TransitionTo(GamePhase.MainMenu);
        }

        void Update()
        {
            InputReader.Tick();
            if (InputReader.PausePressed)
            {
                if (UI.HasScreen) UI.Back();
                else if (Fsm.Is(GamePhase.Day) || Fsm.Is(GamePhase.Night)) Pause();
            }
            Fsm.Tick(Time.deltaTime);
        }

        // ---- states -----------------------------------------------------------------------

        void EnterMenu()
        {
            SetPaused(false);
            Tutorial.Stop();
            if (Run.Current != null) Run.Current.Dispose();
            UI.Hud.Hide();
            UI.CloseAll();
            var map = Db.Map(Profile.loadout.mapId) ?? Db.Maps[0];
            menuField = MapBuilder.Build(map, transform, true);
            var rig = Models.Spawn(Db.Building("main-rig").model, menuField.BuildingRoot);
            rig.transform.position = Vector3.zero;
            CameraRig.Instance.MenuOrbit(Vector3.zero);
            AudioService.Music("bgm_menu");
            AudioService.Ambience(AmbienceFor(map));
            UI.Open(new MainMenuScreen());
        }

        void ExitMenu()
        {
            UI.CloseAll();
            if (menuField != null) Destroy(menuField.gameObject);
            menuField = null;
        }

        void EnterDay()
        {
            var run = Run.Current;
            SetPaused(false);
            UI.CloseAll();
            run.Field.SetDay(true, Fsm.Previous == GamePhase.MainMenu || Fsm.Previous == GamePhase.GameOver || Fsm.Previous == GamePhase.Day);
            run.BeginDay();
            UI.Hud.Bind(run);
            AudioService.Music(run.Map.bgmDay);
            AudioService.Ambience(AmbienceFor(run.Map));
            AudioService.SetIntensity(0f);
            nightHold = 0f;
            if (Fsm.Previous != GamePhase.Dawn) UI.Banner("DAY " + run.Night, Palette.Hazard);
            Ui.Toast(run.Plan != null && !string.IsNullOrEmpty(run.Plan.newEnemyId)
                ? "New enemy tonight: " + Db.Enemy(run.Plan.newEnemyId).name
                : "Build up the field, then hold N to start the night");
        }

        void TickDay(float dt)
        {
            var run = Run.Current;
            if (run == null) return;
            bool canStart = !InputBlocked && run.Hero.Alive && (!Tutorial.Active || TutorialAllowsNight());
            if (InputReader.StartNightHeld && canStart)
            {
                nightHold += dt;
                if (nightHold >= NightHoldTime)
                {
                    nightHold = 0f;
                    UI.Hud.SetNightHold(0f);
                    Fsm.TransitionTo(GamePhase.Night);
                    return;
                }
            }
            else nightHold = Mathf.MoveTowards(nightHold, 0f, dt * 2f);
            UI.Hud.SetNightHold(nightHold / NightHoldTime);
        }

        static bool TutorialAllowsNight()
        {
            var step = Tutorial.Current;
            return step == null || step.advanceOn == "night" || step.advanceOn == "command";
        }

        void ExitDay() { UI.Hud.SetNightHold(0f); }

        void EnterNight()
        {
            var run = Run.Current;
            run.BeginNight();
            run.Field.SetDay(false, false);
            bool boss = run.Plan != null && !string.IsNullOrEmpty(run.Plan.bossId);
            AudioService.Music(boss ? "bgm_boss" : run.Map.bgmNight);
            AudioService.Ui("sfx_horn_night");
            UI.Banner("NIGHT " + run.Night, Palette.Hurt);
            Fx.Shake(0.3f);
            Tutorial.Notify("night");
        }

        void TickNight(float dt)
        {
            var run = Run.Current;
            if (run == null) return;
            int alive = run.EnemiesAlive;
            AudioService.SetIntensity(Mathf.Clamp01(alive / 25f));
            run.Stats.maxUnitsAlive = Mathf.Max(run.Stats.maxUnitsAlive, run.UnitsAlive);
            switch (NightRules.Check(run.Rig.Health.current, alive, run.Spawner.Pending))
            {
                case NightOutcome.Lost: Fsm.TransitionTo(GamePhase.GameOver); break;
                case NightOutcome.Cleared: Fsm.TransitionTo(GamePhase.Dawn); break;
            }
        }

        void EnterDawn()
        {
            var run = Run.Current;
            LastDawn = run.EndNight();
            run.Field.SetDay(true, false);
            AudioService.Music("bgm_dawn");
            AudioService.Ui("sfx_dawn");
            AudioService.SetIntensity(0f);
            Tutorial.Notify("dawn");
            UI.Open(new DawnScreen(run, LastDawn));
        }

        /// <summary>Called by the Dawn screen after the wheel.</summary>
        public void ContinueToDay()
        {
            var run = Run.Current;
            if (run == null || !Fsm.Is(GamePhase.Dawn)) return;
            run.PrepareNextDay();
            Tutorial.Notify("wheelDone");
            Fsm.TransitionTo(GamePhase.Day);
        }

        void EnterGameOver()
        {
            var run = Run.Current;
            run.Spawner.Stop();
            Profile.stats.defeats++;
            if (!run.IsTutorial)
                Progress.RecordScore(Profile, new ScoreEntry { mapId = run.Map.id, difficultyId = run.Difficulty.id, classId = run.Class.id, night = run.Night, score = run.Score });
            SaveProfile();
            AudioService.Music("bgm_defeat");
            AudioService.Ui("sfx_defeat");
            Fx.Shake(1f);
            UI.Hud.Hide();
            UI.Open(new GameOverScreen(run));
            Time.timeScale = 0.25f;
        }

        void ExitGameOver() { Time.timeScale = 1f; }

        // ---- run control ------------------------------------------------------------------

        public void StartRun(Loadout loadout, bool tutorial)
        {
            if (!tutorial)
            {
                Profile.loadout = loadout;
                Profile.stats.runs++;
            }
            var snap = Run.NewSnapshot(loadout);
            var run = CreateRun(snap, tutorial);
            run.SaveCheckpoint(true);
            var ctx = Context();
            ctx.perkCount = loadout.perks.Count;
            Announce(Rigfall.Core.Achievements.Check(ctx));
            SaveProfile();
            Fsm.TransitionTo(GamePhase.Day);
            if (tutorial) Tutorial.Begin();
        }

        public void StartTutorial()
        {
            var l = new Loadout { mapId = Db.Maps[0].id, difficultyId = "easy", classId = "driller", weaponId = Db.Class("driller").startingWeapon };
            StartRun(l, true);
        }

        public bool ContinueSavedRun()
        {
            var snap = SaveService.LoadRun(Db);
            if (snap == null) { Ui.Toast("No saved run to continue"); return false; }
            CreateRun(snap, false);
            Fsm.TransitionTo(GamePhase.Day);
            return true;
        }

        /// <summary>Retry the night from the last dawn checkpoint. Buildings and barrels are as they were that morning.</summary>
        public void RetryFromDawn()
        {
            var snap = SaveService.LoadRun(Db);
            if (snap == null) { RestartRun(); return; }
            SetPaused(false);
            CreateRun(snap, false);
            Fsm.TransitionTo(GamePhase.Day);
        }

        /// <summary>Start the same run (field, loadout) again from night 1.</summary>
        public void RestartRun()
        {
            SetPaused(false);
            var snap = SaveService.LoadRunStart(Db);
            if (snap == null) { StartRun(Profile.loadout, false); return; }
            snap.seed = System.DateTime.UtcNow.Ticks.ToString("x");
            var run = CreateRun(snap, false);
            run.SaveCheckpoint(false);
            Fsm.TransitionTo(GamePhase.Day);
        }

        /// <summary>
        /// Tear down whatever field is standing (the menu backdrop or the previous run) right
        /// now, before the new field is built and baked in the same spot: two overlapping
        /// NavMeshes would let agents snap onto the one that is about to disappear.
        /// </summary>
        Run CreateRun(RunSnapshot snap, bool tutorial)
        {
            if (menuField != null) { DestroyImmediate(menuField.gameObject); menuField = null; }
            return Run.Create(snap, tutorial);
        }

        public void QuitToMenu()
        {
            SetPaused(false);
            Fsm.TransitionTo(GamePhase.MainMenu);
        }

        public void Pause()
        {
            if (Paused) return;
            SetPaused(true);
            UI.Open(new PauseScreen());
        }

        public void Resume()
        {
            SetPaused(false);
        }

        static void SetPaused(bool on)
        {
            Paused = on;
            Time.timeScale = on ? 0f : 1f;
        }

        // ---- profile & achievements --------------------------------------------------------

        public static void SaveProfile() { if (Profile != null) SaveService.SaveProfile(Profile); }

        /// <summary>Settings > Data > Reset all progress.</summary>
        public void ResetProfile()
        {
            Profile = Progress.NewProfile(Db);
            SaveProfile();
            GraphicsApplier.Apply(Settings);
            QuitToMenu();
        }

        static AchievementContext Context()
        {
            var run = Run.Current;
            var ctx = new AchievementContext { db = Db, profile = Profile };
            if (run != null && !run.IsTutorial)
            {
                ctx.run = run.Stats;
                ctx.classId = run.Class.id;
                ctx.setting = run.Map.setting;
                ctx.difficultyOrder = run.Difficulty.order;
                ctx.mutatorCount = run.Mutators.Count;
                ctx.perkCount = run.Perks.Count;
                ctx.barrels = run.Barrels;
                ctx.unitsAlive = run.UnitsAlive;
                ctx.buildingCounts = run.BuildingCounts();
            }
            return ctx;
        }

        public static void CheckAchievements()
        {
            if (Db == null || Profile == null) return;
            Announce(Rigfall.Core.Achievements.Check(Context()));
        }

        static void Announce(System.Collections.Generic.List<AchievementDef> unlocked)
        {
            if (unlocked == null || unlocked.Count == 0) return;
            foreach (var a in unlocked) Ui.Achievement(a);
            SaveProfile();
        }

        public static string AmbienceFor(MapDef map)
        {
            switch (map.biome)
            {
                case "swamp": return "amb_swamp";
                case "arctic": return "amb_arctic";
                case "sea":
                case "deepsea": return "amb_sea";
                default: return "amb_desert";
            }
        }

        void OnApplicationQuit() { SaveProfile(); }
        void OnApplicationPause(bool paused) { if (paused) SaveProfile(); }
    }
}
