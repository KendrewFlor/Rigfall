// The dawn reward wheel ("core sample wheel"). Clearing a night earns one spin. Segment
// sizes follow the rarity weights in rarities.json (Common 55%, Rare 28%, Epic 12%,
// Legendary 4.5%, Blowout Brilliant 0.5%); luck shifts weight toward rarer segments.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    public sealed class WheelSegment
    {
        public RarityDef rarity;
        public float probability;   // 0..1, what the spin actually uses
        public float startAngle;    // degrees, for drawing
        public float sweep;         // degrees, for drawing (tiny segments get a minimum size)
    }

    public sealed class WheelResult
    {
        public RarityDef rarity;
        public WheelRewardDef reward;
        public int segmentIndex;
        public float landAngle;
        public int amount;
    }

    public static class RewardWheel
    {
        public const float MinSweep = 7f;

        public static List<WheelSegment> Segments(GameDatabase db, float luck)
        {
            var segs = new List<WheelSegment>();
            float total = 0;
            foreach (var r in db.Rarities)
            {
                float w = r.wheelWeight * (1f + Math.Max(0f, luck) * r.order);
                segs.Add(new WheelSegment { rarity = r, probability = w });
                total += w;
            }
            if (total <= 0) return segs;
            foreach (var s in segs) s.probability /= total;

            // Drawing: proportional sweeps, but never thinner than MinSweep so Blowout Brilliant stays visible.
            float extra = 0, big = 0;
            foreach (var s in segs)
            {
                s.sweep = s.probability * 360f;
                if (s.sweep < MinSweep) { extra += MinSweep - s.sweep; s.sweep = MinSweep; }
                else big += s.sweep;
            }
            if (extra > 0 && big > 0)
                foreach (var s in segs) if (s.sweep > MinSweep) s.sweep -= extra * (s.sweep / big);
            float a = 0;
            foreach (var s in segs) { s.startAngle = a; a += s.sweep; }
            return segs;
        }

        public static WheelResult Spin(GameDatabase db, float luck, Rng rng, int night)
        {
            var segs = Segments(db, luck);
            var weights = new List<float>();
            foreach (var s in segs) weights.Add(s.probability);
            int i = rng.Weighted(weights);
            if (i < 0) i = 0;
            var seg = segs[i];

            var options = db.Wheel.FindAll(w => w.rarity == seg.rarity.id);
            var reward = options.Count > 0 ? rng.Pick(options) : null;
            return new WheelResult
            {
                rarity = seg.rarity,
                reward = reward,
                segmentIndex = i,
                landAngle = seg.startAngle + seg.sweep * rng.Range(0.2f, 0.8f),
                amount = reward == null ? 0 : (int)Math.Round(reward.amount + reward.perNight * Math.Max(0, night)),
            };
        }

        /// <summary>Text for the reward, with {n} filled in.</summary>
        public static string Describe(WheelResult r)
        {
            if (r == null || r.reward == null) return "";
            return r.reward.description.Replace("{n}", r.amount.ToString());
        }
    }
}
