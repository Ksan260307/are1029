using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>Anything bullets can hit. Shapes are axis-aligned boxes on the road plane (x/z).</summary>
    public interface IBulletTarget
    {
        bool IsTargetable { get; }
        Vector3 TargetPosition { get; }
        float HalfWidth { get; }
        float HalfDepth { get; }
        void OnBulletHit(float damage, Vector3 point);
    }

    /// <summary>
    /// Struct-array bullets: swept along +z each frame and tested against all
    /// targets, rendered as one instanced batch. No physics engine involved.
    /// </summary>
    public sealed class BulletSystem
    {
        struct Bullet
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Damage;
            public float Travel;
            public Color Color;
        }

        public const int Capacity = 700;
        const float Radius = 0.12f;
        static readonly Vector3 Scale = new Vector3(0.2f, 0.2f, 1.2f);

        readonly Bullet[] _bullets = new Bullet[Capacity];
        readonly InstancedBatch _batch = new InstancedBatch();
        int _count;

        public int Count => _count;

        public void Spawn(Vector3 position, Vector3 direction, float damage, Color color)
        {
            if (_count >= Capacity) return;
            _bullets[_count++] = new Bullet { Position = position, Direction = direction, Damage = damage, Color = color };
        }

        public void Clear() => _count = 0;

        public void Tick(float dt, List<IBulletTarget> targets)
        {
            float step = Balance.BulletSpeed * dt;
            for (int i = _count - 1; i >= 0; i--)
            {
                ref Bullet b = ref _bullets[i];
                Vector3 from = b.Position;
                Vector3 to = from + b.Direction * step;

                IBulletTarget hit = null;
                float hitZ = float.MaxValue;
                for (int t = 0; t < targets.Count; t++)
                {
                    IBulletTarget target = targets[t];
                    if (!target.IsTargetable) continue;
                    Vector3 c = target.TargetPosition;
                    float hd = target.HalfDepth + Radius;
                    if (to.z < c.z - hd || from.z > c.z + hd) continue;
                    if (Mathf.Abs(to.x - c.x) > target.HalfWidth + Radius) continue;
                    float entry = Mathf.Max(from.z, c.z - hd);
                    if (entry < hitZ)
                    {
                        hitZ = entry;
                        hit = target;
                    }
                }

                if (hit != null)
                {
                    hit.OnBulletHit(b.Damage, new Vector3(to.x, b.Position.y, hitZ));
                    _bullets[i] = _bullets[--_count];
                    continue;
                }

                b.Position = to;
                b.Travel += step;
                if (b.Travel > Balance.BulletRange) _bullets[i] = _bullets[--_count];
            }
        }

        public void Render()
        {
            if (_count == 0) return;
            // alpha-blended rather than additive: additive glow vanishes against the light road
            _batch.Begin(GameAssets.Bolt, GameAssets.Transparent);
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _bullets[i];
                float fade = 1f - Mathf.Clamp01((b.Travel - Balance.BulletRange * 0.75f) / (Balance.BulletRange * 0.25f));
                Color c = b.Color;
                c.a *= fade;
                _batch.Add(Matrix4x4.TRS(b.Position, Quaternion.LookRotation(b.Direction), Scale), c);
            }
            _batch.End();
        }

        public static Color ColorForTier(int tier)
        {
            switch (tier)
            {
                case 1: return new Color(0.1f, 0.8f, 0.35f, 1f);
                case 2: return new Color(1f, 0.55f, 0.05f, 1f);
                case 3: return new Color(0.9f, 0.15f, 0.85f, 1f);
                default: return new Color(0.1f, 0.5f, 1f, 1f);
            }
        }
    }
}
