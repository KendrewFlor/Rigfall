// Extra behaviour for bosses (every 7th night). Each boss has one signature move, with a
// visible wind-up so the player can react:
//   summon    - calls adds (The Blowout: Flash Fires, Riser Kraken: Riser Eels)
//   shockwave - slams the ground: damage and a stun around it (Salt Dome Colossus)
//   breath    - a long freezing cone that slows (Hydrate Leviathan)
using System.Collections;
using System.Collections.Generic;
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public sealed class BossBrain : MonoBehaviour
    {
        Enemy enemy;
        float next;
        static readonly List<Health> scratch = new List<Health>();

        public void Setup(Enemy e)
        {
            enemy = e;
            next = Time.time + 6f;
            Ui.ShowBossBar(e);
            Ui.Banner(e.Def.name + " approaches!", Palette.Danger);
            AudioService.Ui("sfx_boss_roar");
            Fx.Shake(0.8f);
        }

        void Update()
        {
            if (enemy == null || !enemy.Alive || Time.time < next) return;
            next = Time.time + Interval();
            StartCoroutine(Signature());
        }

        float Interval()
        {
            switch (enemy.Def.special)
            {
                case "shockwave": return Mathf.Max(3.5f, enemy.Def.specialValue);
                case "breath": return 7f;
                default: return 9f;
            }
        }

        IEnumerator Signature()
        {
            var def = enemy.Def;
            // Wind-up: a pulse and a warning ring.
            Tweener.Punch(enemy.transform, 0.25f, 0.6f);
            Fx.Emit(FxKind.Smoke, enemy.transform.position + Vector3.up * 2f, 16);
            AudioService.Sfx("sfx_boss_roar", enemy.transform.position, 0.8f, 0.6f);
            yield return new WaitForSeconds(0.7f);
            if (enemy == null || !enemy.Alive) yield break;

            Vector3 pos = enemy.transform.position;
            switch (def.special)
            {
                case "summon":
                {
                    var add = Game.Db.Enemy(def.spawns);
                    var run = Run.Current;
                    int n = Mathf.Max(1, (int)def.specialValue);
                    if (add != null && run != null)
                        for (int i = 0; i < n; i++)
                        {
                            Vector3 p = pos + Quaternion.Euler(0f, i * 360f / n, 0f) * Vector3.forward * 2.5f;
                            Enemy.Spawn(add, p, false, run.Plan);
                        }
                    Fx.Emit(def.id == "the-blowout" ? FxKind.Fire : FxKind.Mud, pos + Vector3.up, 30);
                    break;
                }
                case "shockwave":
                {
                    var d = DamageInfo.Simple(enemy.Damage * 0.6f, KillSource.Other, enemy.gameObject);
                    d.stunTime = 1.2f;
                    d.knockback = 5f;
                    Combat.Splash(pos, 7f, Team.Player, false, d, FxKind.Shockwave);
                    Fx.Emit(FxKind.Dust, pos, 40);
                    Fx.Shake(0.9f);
                    break;
                }
                case "breath":
                {
                    Vector3 fwd = enemy.transform.forward;
                    Health.InRadius(pos, def.specialValue, Team.Player, true, true, scratch);
                    var victims = scratch.ToArray();
                    foreach (var h in victims)
                    {
                        Vector3 to = MapMath.Flat(h.transform.position - pos);
                        if (Vector3.Angle(fwd, to) > 32f) continue;
                        var d = DamageInfo.Simple(enemy.Damage * 0.5f, KillSource.Other, enemy.gameObject);
                        d.slow = 0.5f; d.slowTime = 3f;
                        Combat.Hit(h, d, FxKind.Ice, "");
                    }
                    for (float r = 1f; r < def.specialValue; r += 1.2f)
                        Fx.Emit(FxKind.Ice, pos + fwd * r + Vector3.up * 1.5f, 6);
                    AudioService.Sfx("sfx_spray", pos, 0.6f);
                    break;
                }
            }
        }

        void OnDestroy() { Ui.HideBossBar(enemy); }
    }
}
