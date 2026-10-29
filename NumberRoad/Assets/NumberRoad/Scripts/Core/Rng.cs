using System.Collections.Generic;

namespace NumberRoad.Core
{
    /// <summary>
    /// Tiny deterministic RNG (xorshift32). A level number always produces the
    /// same layout on every platform, which System.Random does not guarantee.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(int seed)
        {
            _state = unchecked((uint)seed * 2654435761u) ^ 0x9E3779B9u;
            if (_state == 0) _state = 0x6D2B79F5u;
            for (int i = 0; i < 4; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability) => Value < probability;

        public int Sign() => (NextUInt() & 1u) == 0 ? -1 : 1;

        public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];
    }
}
