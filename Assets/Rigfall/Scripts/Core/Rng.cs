// Small deterministic random number generator (xorshift128+ seeded with splitmix64).
// Runs are reproducible from a seed, so "retry from last dawn" replays the same wave.
using System;
using System.Collections.Generic;

namespace Rigfall.Core
{
    public sealed class Rng
    {
        ulong s0, s1;

        public Rng(ulong seed) { Reseed(seed); }
        public Rng(string seed) : this(Hash(seed)) { }

        public void Reseed(ulong seed)
        {
            s0 = SplitMix(ref seed);
            s1 = SplitMix(ref seed);
            if (s0 == 0 && s1 == 0) s1 = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>FNV-1a 64-bit hash, used to turn text seeds ("permian-pad:7") into numbers.</summary>
        public static ulong Hash(string text)
        {
            ulong h = 1469598103934665603UL;
            if (text != null)
                foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
            return h;
        }

        public ulong NextULong()
        {
            ulong x = s0, y = s1;
            s0 = y;
            x ^= x << 23;
            s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
            return s1 + y;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value() => (NextULong() >> 40) / (float)(1UL << 24);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }

        public bool Chance(float p) => Value() < p;

        public T Pick<T>(IList<T> items) => items[Range(0, items.Count)];

        /// <summary>Index chosen with probability proportional to its weight. Returns -1 if all weights are zero.</summary>
        public int Weighted(IList<float> weights)
        {
            float total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0) return -1;
            float roll = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= Math.Max(0f, weights[i]);
                if (roll < 0) return i;
            }
            return weights.Count - 1;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
