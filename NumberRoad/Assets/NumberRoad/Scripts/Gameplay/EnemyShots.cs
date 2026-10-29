using NumberRoad.Presentation;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// Arrows and fireballs lobbed at the squad by archers, whelps and the dragon. Each one flies in an arc to
    /// where the squad will be and shows a red marker on the road, so moving sideways dodges it.
    /// </summary>
    public sealed class EnemyShots
    {
        struct Shot
        {
            public Vector3 From;
            public Vector3 To;
            public float Clock;
            public float Duration;
            public float Arc;
            public int Damage;
            public float Radius;
            public bool Fire;
        }

        const int Capacity = 256;
        static readonly Color ArrowColor = new Color(0.45f, 0.3f, 0.18f, 1f);
        static readonly Color FireColor = new Color(1f, 0.45f, 0.1f, 1f);

        readonly Shot[] _shots = new Shot[Capacity];
        readonly InstancedBatch _batch = new InstancedBatch();
        int _count;

        public int Count => _count;

        public void Fire(Vector3 from, Vector3 target, float duration, int damage, float radius, bool fire)
        {
            if (_count >= Capacity) return;
            float distance = Vector3.Distance(from, target);
            _shots[_count++] = new Shot
            {
                From = from,
                To = new Vector3(target.x, 0f, target.z),
                Duration = Mathf.Max(0.3f, duration),
                Arc = Mathf.Clamp(distance * 0.18f, 1.2f, 5f),
                Damage = Mathf.Max(1, damage),
                Radius = radius,
                Fire = fire,
            };
        }

        public void Clear() => _count = 0;

        /// <summary>Lands shots that reached the road. <paramref name="onHit"/> receives each one that hit the squad.</summary>
        public void Tick(float dt, Squad squad, GameContext ctx, System.Action<Vector3, int, bool> onHit)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                ref Shot s = ref _shots[i];
                s.Clock += dt;
                if (s.Clock < s.Duration) continue;

                float dx = Mathf.Abs(s.To.x - squad.X);
                float dz = Mathf.Abs(s.To.z - squad.Z);
                bool hit = squad.Count > 0 && dx < squad.HalfWidth + s.Radius && dz < squad.HalfDepth + s.Radius;
                if (hit) onHit(s.To, s.Damage, s.Fire);
                else if (s.Fire) ctx.Fx.Sparks(s.To + Vector3.up * 0.2f, FireColor, 8, 5f, 0.35f);
                else ctx.Fx.Burst(s.To + Vector3.up * 0.1f, new Color(0.7f, 0.65f, 0.55f), 3, 2f, 0.07f, 0.4f);
                _shots[i] = _shots[--_count];
            }
        }

        /// <summary>Nearest shot about to land close to <paramref name="x"/> (the auto pilot side-steps it).</summary>
        public bool Threat(float x, float z, out float landX)
        {
            landX = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                ref Shot s = ref _shots[i];
                if (Mathf.Abs(s.To.z - z) > 3f || Mathf.Abs(s.To.x - x) > s.Radius + 2.5f) continue;
                float left = s.Duration - s.Clock;
                if (left < best)
                {
                    best = left;
                    landX = s.To.x;
                }
            }
            return best < float.MaxValue;
        }

        static Vector3 PointAt(ref Shot s, float u) => Vector3.Lerp(s.From, s.To, u) + Vector3.up * (s.Arc * 4f * u * (1f - u));

        public void Render()
        {
            if (_count == 0) return;

            // landing markers grow more solid as the shot comes down
            _batch.Begin(GameAssets.GroundQuad, GameAssets.Shadow);
            for (int i = 0; i < _count; i++)
            {
                ref Shot s = ref _shots[i];
                float u = Mathf.Clamp01(s.Clock / s.Duration);
                float size = s.Radius * 2.4f * (0.6f + 0.4f * u);
                var c = new Color(1f, 0.1f, 0.05f, 0.25f + 0.45f * u);
                _batch.Add(Matrix4x4.TRS(s.To + Vector3.up * 0.03f, Quaternion.identity, new Vector3(size, 1f, size)), c);
            }
            _batch.End();

            _batch.Begin(GameAssets.Bolt, GameAssets.Toon);
            for (int i = 0; i < _count; i++)
            {
                ref Shot s = ref _shots[i];
                if (s.Fire) continue;
                float u = Mathf.Clamp01(s.Clock / s.Duration);
                Vector3 p = PointAt(ref s, u);
                Vector3 dir = PointAt(ref s, Mathf.Min(1f, u + 0.02f)) - p;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.back;
                _batch.Add(Matrix4x4.TRS(p, Quaternion.LookRotation(dir), new Vector3(0.12f, 0.12f, 0.9f)), ArrowColor);
            }
            _batch.End();

            _batch.Begin(GameAssets.Orb, GameAssets.Transparent);
            for (int i = 0; i < _count; i++)
            {
                ref Shot s = ref _shots[i];
                if (!s.Fire) continue;
                float u = Mathf.Clamp01(s.Clock / s.Duration);
                float flicker = 1f + Mathf.Sin(s.Clock * 40f + i) * 0.12f;
                _batch.Add(Matrix4x4.TRS(PointAt(ref s, u), Quaternion.identity, Vector3.one * (0.7f * flicker)), FireColor);
                _batch.Add(Matrix4x4.TRS(PointAt(ref s, Mathf.Max(0f, u - 0.04f)), Quaternion.identity, Vector3.one * 0.45f), new Color(1f, 0.8f, 0.2f, 0.7f));
            }
            _batch.End();
        }
    }
}
