// Finite State Machine for the day/night loop.
//
//   MainMenu ──start──▶ Day ──night horn──▶ Night ──all enemies cleared──▶ Dawn ──wheel done──▶ Day ...
//                        ▲                    │
//                        │                    └──Main Rig integrity <= 0──▶ GameOver ──retry──┘
//
// The machine only allows the transitions registered with Allow(); anything else is
// refused, so a bug can't (for example) jump from Day straight to GameOver.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    public enum GamePhase { Boot, MainMenu, Day, Night, Dawn, GameOver }

    public interface IGameState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    /// <summary>A state built from delegates, handy for small states and tests.</summary>
    public sealed class LambdaState : IGameState
    {
        readonly Action enter, exit;
        readonly Action<float> tick;
        public LambdaState(Action enter = null, Action<float> tick = null, Action exit = null)
        {
            this.enter = enter; this.tick = tick; this.exit = exit;
        }
        public void Enter() => enter?.Invoke();
        public void Tick(float dt) => tick?.Invoke(dt);
        public void Exit() => exit?.Invoke();
    }

    public sealed class StateMachine<TKey> where TKey : struct
    {
        readonly Dictionary<TKey, IGameState> states = new Dictionary<TKey, IGameState>();
        readonly HashSet<long> allowed = new HashSet<long>();
        readonly HashSet<TKey> fromAnywhere = new HashSet<TKey>();
        bool started, changing;

        public TKey Current { get; private set; }
        public TKey Previous { get; private set; }
        public float TimeInState { get; private set; }

        /// <summary>Raised after a transition: (from, to).</summary>
        public event Action<TKey, TKey> Changed;

        public StateMachine<TKey> Add(TKey key, IGameState state)
        {
            states[key] = state;
            return this;
        }

        public StateMachine<TKey> Allow(TKey from, TKey to)
        {
            allowed.Add(Pair(from, to));
            return this;
        }

        /// <summary>Allow a transition into this state from any state (e.g. MainMenu).</summary>
        public StateMachine<TKey> AllowFromAnywhere(TKey to)
        {
            fromAnywhere.Add(to);
            return this;
        }

        static long Pair(TKey a, TKey b) => ((long)Convert.ToInt32(a) << 32) | (uint)Convert.ToInt32(b);

        public bool CanTransition(TKey to) =>
            started && states.ContainsKey(to) && (fromAnywhere.Contains(to) || allowed.Contains(Pair(Current, to)));

        public void Start(TKey initial)
        {
            if (!states.ContainsKey(initial)) throw new ArgumentException("Unknown state " + initial);
            started = true;
            Current = initial;
            Previous = initial;
            TimeInState = 0;
            states[initial].Enter();
        }

        /// <summary>Change state. Returns false (and does nothing) for a transition that isn't allowed.</summary>
        public bool TransitionTo(TKey to)
        {
            if (changing || !CanTransition(to)) return false;
            changing = true;
            try
            {
                var from = Current;
                states[from].Exit();
                Previous = from;
                Current = to;
                TimeInState = 0;
                states[to].Enter();
                Changed?.Invoke(from, to);
            }
            finally { changing = false; }
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!started) return;
            TimeInState += deltaTime;
            states[Current].Tick(deltaTime);
        }

        public bool Is(TKey key) => started && EqualityComparer<TKey>.Default.Equals(Current, key);
    }

    /// <summary>Night win/loss rule from the design: rig at 0 = GameOver, nothing left = Dawn.</summary>
    public enum NightOutcome { Continue, Cleared, Lost }

    public static class NightRules
    {
        public static NightOutcome Check(float rigHealth, int enemiesAlive, int spawnsPending)
        {
            if (rigHealth <= 0f) return NightOutcome.Lost;
            if (enemiesAlive <= 0 && spawnsPending <= 0) return NightOutcome.Cleared;
            return NightOutcome.Continue;
        }

        /// <summary>Builds the standard Rigfall game-phase machine with every allowed transition.</summary>
        public static StateMachine<GamePhase> CreateMachine()
        {
            var fsm = new StateMachine<GamePhase>();
            fsm.Allow(GamePhase.Boot, GamePhase.MainMenu)
               .Allow(GamePhase.MainMenu, GamePhase.Day)
               .Allow(GamePhase.Day, GamePhase.Night)
               .Allow(GamePhase.Night, GamePhase.Dawn)
               .Allow(GamePhase.Night, GamePhase.GameOver)
               .Allow(GamePhase.Dawn, GamePhase.Day)
               .Allow(GamePhase.GameOver, GamePhase.Day)
               .Allow(GamePhase.Day, GamePhase.Day)     // retry the day from its dawn snapshot (pause menu)
               .Allow(GamePhase.Night, GamePhase.Day)   // retry from the pause menu mid-night
               .AllowFromAnywhere(GamePhase.MainMenu);
            return fsm;
        }
    }
}
