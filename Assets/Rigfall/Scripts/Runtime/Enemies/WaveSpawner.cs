// Plays a WavePlan during the night: spawns each enemy at its lane's spawn vector at its
// scheduled time. The night state asks Pending + Run.EnemiesAlive to decide when it's over.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class WaveSpawner : MonoBehaviour
    {
        WavePlan plan;
        Field field;
        int next;
        float clock;
        bool active;

        public int Pending => active && plan != null ? plan.spawns.Count - next : 0;
        public int Total => plan != null ? plan.spawns.Count : 0;
        public float Progress => plan == null || plan.spawns.Count == 0 ? 1f : (float)next / plan.spawns.Count;

        public void Begin(WavePlan p, Field f)
        {
            plan = p;
            field = f;
            next = 0;
            clock = 0f;
            active = p != null && p.spawns.Count > 0;
            KickDetection.NightStartTime = Time.time;
        }

        public void Stop() { active = false; }

        void Update()
        {
            if (!active || Run.Current == null) return;
            clock += Time.deltaTime;
            while (next < plan.spawns.Count && plan.spawns[next].time <= clock)
            {
                SpawnOne(plan.spawns[next]);
                next++;
            }
            if (next >= plan.spawns.Count) active = false;
        }

        void SpawnOne(WaveSpawn s)
        {
            var def = Game.Db.Enemy(s.enemyId);
            if (def == null) return;
            Vector3 lane = field.SpawnPoint(s.lane);
            Vector3 jitter = new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-2.2f, 2.2f));
            var e = Enemy.Spawn(def, lane + jitter, s.elite, plan);
            if (s.boss) Fx.Emit(FxKind.Smoke, e.transform.position + Vector3.up, 40);
            else if (Random.value < 0.3f) AudioService.Sfx("sfx_enemy_spawn", e.transform.position, Random.Range(0.9f, 1.1f), 0.5f);
        }
    }
}
