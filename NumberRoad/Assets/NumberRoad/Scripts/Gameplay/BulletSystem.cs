using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>Anything bullets can hit besides the swarm. Shapes are axis-aligned boxes on the road plane (x/z).</summary>
    public interface IBulletTarget
    {
        bool IsTargetable { get; }
        Vector3 TargetPosition { get; }
        float HalfWidth { get; }
        float HalfDepth { get; }
        void OnBulletHit(float damage, Vector3 point, bool crit);
    }

    /// <summary>
    /// Struct-array projectiles of every ally class: arrows, spears, magic orbs, bullets, cannon balls and holy light.
    /// Each is swept along its direction per frame and tested against the swarm grid and the other targets.
    /// Splash damages everything around the impact, pierce passes through several enemies, crits deal double.
    /// </summary>
    public sealed class BulletSystem
    {
        struct Bullet
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Speed;
            public float Damage;
            public float Travel;
            public float Range;
            public float Splash;
            public int Pierce;
            public int LastUid;
            public bool Crit;
            public ProjectileKind Kind;
            public Color Color;
        }

        public const int Capacity = 900;
        const float Radius = 0.12f;
        const float MaxAgentRadius = 1.3f;

        readonly Bullet[] _bullets = new Bullet[Capacity];
        readonly InstancedBatch _batch = new InstancedBatch();
        readonly List<int> _candidates = new List<int>(64);
        readonly GameContext _ctx;
        int _count;

        public int Count => _count;

        public BulletSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public static float SpeedOf(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Spear: return 24f;
                case ProjectileKind.Orb: return 20f;
                case ProjectileKind.Bullet: return 34f;
                case ProjectileKind.Cannon: return 18f;
                case ProjectileKind.Holy: return 24f;
                default: return 30f;
            }
        }

        public void Spawn(UnitClass unit, Vector3 position, Vector3 direction, float damage, bool crit)
        {
            if (_count >= Capacity) return;
            _bullets[_count++] = new Bullet
            {
                Position = position,
                Direction = direction,
                Speed = SpeedOf(unit.Projectile),
                Damage = damage * (crit ? Balance.CritMultiplier : 1f),
                Range = unit.Range,
                Splash = unit.Splash,
                Pierce = unit.Pierce,
                LastUid = -1,
                Crit = crit,
                Kind = unit.Projectile,
                Color = unit.Color,
            };
        }

        public void Clear() => _count = 0;

        public void Tick(float dt, Swarm swarm, List<IBulletTarget> targets)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                ref Bullet b = ref _bullets[i];
                float step = b.Speed * dt;
                Vector3 from = b.Position;
                Vector3 to = from + b.Direction * step;

                // nearest swarm agent along the swept segment
                int hitAgent = -1;
                float hitZ = float.MaxValue;
                _candidates.Clear();
                float x0 = Mathf.Min(from.x, to.x) - MaxAgentRadius, x1 = Mathf.Max(from.x, to.x) + MaxAgentRadius;
                swarm.Grid.Query(x0, x1, from.z - MaxAgentRadius, to.z + MaxAgentRadius, _candidates);
                for (int k = 0; k < _candidates.Count; k++)
                {
                    int index = _candidates[k];
                    ref Agent a = ref swarm.Agents[index];
                    if (a.Uid == b.LastUid || swarm.Invulnerable(ref a)) continue;
                    float r = swarm.RadiusOf(ref a);
                    float hd = r + Radius;
                    if (to.z < a.Pos.z - hd || from.z > a.Pos.z + hd) continue;
                    if (Mathf.Abs(to.x - a.Pos.x) > r + Radius) continue;
                    float entry = Mathf.Max(from.z, a.Pos.z - hd);
                    if (entry < hitZ)
                    {
                        hitZ = entry;
                        hitAgent = index;
                    }
                }

                IBulletTarget hitTarget = null;
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
                        hitTarget = target;
                        hitAgent = -1;
                    }
                }

                if (hitTarget != null)
                {
                    var point = new Vector3(to.x, b.Position.y, hitZ);
                    hitTarget.OnBulletHit(b.Damage, point, b.Crit);
                    if (b.Splash > 0f) Splash(swarm, point, b.Splash, b.Damage, -1, b.Kind);
                    _bullets[i] = _bullets[--_count];
                    continue;
                }

                if (hitAgent >= 0)
                {
                    ref Agent a = ref swarm.Agents[hitAgent];
                    int uid = a.Uid;
                    var point = new Vector3(a.Pos.x, b.Position.y, hitZ);
                    swarm.Damage(hitAgent, b.Damage, b.Crit);
                    if (b.Splash > 0f) Splash(swarm, point, b.Splash, b.Damage, uid, b.Kind);
                    if (b.Pierce > 0)
                    {
                        b.Pierce--;
                        b.LastUid = uid;
                        b.Position = new Vector3(to.x, b.Position.y, hitZ + 0.05f);
                        b.Travel += step;
                        continue;
                    }
                    _bullets[i] = _bullets[--_count];
                    continue;
                }

                b.Position = to;
                b.Travel += step;
                if (b.Travel > b.Range) _bullets[i] = _bullets[--_count];
            }
        }

        void Splash(Swarm swarm, Vector3 point, float radius, float damage, int excludeUid, ProjectileKind kind)
        {
            swarm.AreaDamage(point, radius, damage * 0.6f, excludeUid);
            if (_ctx.LowQuality && Random.value < 0.5f) return;
            Color c = kind == ProjectileKind.Cannon ? new Color(1f, 0.55f, 0.15f) : kind == ProjectileKind.Holy ? new Color(1f, 0.95f, 0.6f) : new Color(0.75f, 0.5f, 1f);
            _ctx.Fx.Sparks(point, c, kind == ProjectileKind.Cannon ? 10 : 5, 3f + radius * 2f, 0.3f + radius * 0.1f);
        }

        public void Render()
        {
            if (_count == 0) return;
            // alpha-blended rather than additive: additive glow vanishes against the light road
            _batch.Begin(GameAssets.Bolt, GameAssets.Transparent);
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _bullets[i];
                if (b.Kind == ProjectileKind.Orb || b.Kind == ProjectileKind.Cannon || b.Kind == ProjectileKind.Holy) continue;
                Vector3 scale;
                switch (b.Kind)
                {
                    case ProjectileKind.Spear: scale = new Vector3(0.16f, 0.16f, 1.5f); break;
                    case ProjectileKind.Bullet: scale = new Vector3(0.15f, 0.15f, 0.8f); break;
                    default: scale = new Vector3(0.18f, 0.18f, 1.2f); break;
                }
                if (b.Crit) scale *= 1.5f;
                _batch.Add(Matrix4x4.TRS(b.Position, Quaternion.LookRotation(b.Direction), scale), Faded(ref b));
            }
            _batch.End();

            _batch.Begin(GameAssets.Orb, GameAssets.Transparent);
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _bullets[i];
                float size;
                switch (b.Kind)
                {
                    case ProjectileKind.Orb: size = 0.32f + b.Splash * 0.08f; break;
                    case ProjectileKind.Cannon: size = 0.45f; break;
                    case ProjectileKind.Holy: size = 0.3f + b.Splash * 0.1f; break;
                    default: continue;
                }
                float wobble = 1f + Mathf.Sin(b.Travel * 3f + i) * 0.1f;
                if (b.Crit) size *= 1.4f;
                _batch.Add(Matrix4x4.TRS(b.Position, Quaternion.identity, Vector3.one * (size * wobble)), Faded(ref b));
            }
            _batch.End();
        }

        static Color Faded(ref Bullet b)
        {
            float fade = 1f - Mathf.Clamp01((b.Travel - b.Range * 0.75f) / (b.Range * 0.25f));
            Color c = b.Crit ? Color.Lerp(b.Color, new Color(1f, 0.95f, 0.3f), 0.6f) : b.Color;
            c.a *= fade;
            return c;
        }
    }
}
