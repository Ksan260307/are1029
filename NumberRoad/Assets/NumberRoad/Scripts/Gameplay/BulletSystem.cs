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
        /// <summary>A hit. <paramref name="element"/> lets a boss take more from what it is weak to.</summary>
        void OnBulletHit(float damage, Vector3 point, bool crit, Element element = Element.Physical);
    }

    /// <summary>
    /// Every ally attack. Projectiles (straight, seeking, spray, lobbed) live in a struct array and are swept
    /// against the swarm grid; instant attacks (piercing line, chain lightning, sky strike, melee sweep) land
    /// immediately and leave a short flash. A kill hands what is left of a shot to the next enemy (carry-over),
    /// and the buffs picked between waves add burning, frost, poison, blasts, chains, shards and knockback.
    /// </summary>
    public sealed class BulletSystem
    {
        struct Bullet
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Speed;
            public float Damage;
            public float BaseDamage;
            public float Travel;
            public float Range;
            public float Splash;
            public int Pierce;
            public int LastUid;
            public int Seed;
            public bool Crit;
            public bool Bounced;
            public AttackStyle Style;
            public Element Element;
            public ProjectileKind Kind;
            public float Knockback;
            public Color Color;
            // lobbed shots
            public Vector3 Start;
            public Vector3 Target;
            public float Clock;
            public float Duration;
        }

        enum FlashKind : byte
        {
            Line,
            Pillar,
            Ring,
        }

        struct Flash
        {
            public Vector3 A;
            public Vector3 B;
            public float Life;
            public float MaxLife;
            public float Width;
            public Color Color;
            public FlashKind Kind;
        }

        public const int Capacity = 900;
        const int FlashCapacity = 160;
        const float Radius = 0.12f;
        const float MaxAgentRadius = 1.3f;

        readonly Bullet[] _bullets = new Bullet[Capacity];
        readonly Flash[] _flashes = new Flash[FlashCapacity];
        readonly InstancedBatch _batch = new InstancedBatch();
        readonly List<int> _candidates = new List<int>(64);
        readonly List<int> _chainHit = new List<int>(8);
        readonly GameContext _ctx;
        Swarm _swarm;
        RunMods _mods = new RunMods();
        int _count;
        int _flashCount;
        int _hits;
        int _seed;

        public int Count => _count;

        public BulletSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Bind(Swarm swarm, RunMods mods)
        {
            _swarm = swarm;
            _mods = mods;
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

        public void Clear()
        {
            _count = 0;
            _flashCount = 0;
        }

        // --- Projectiles -------------------------------------------------------------

        /// <summary>A straight, seeking or spray shot.</summary>
        public void Shoot(UnitClass unit, Vector3 position, Vector3 direction, float damage, bool crit)
        {
            if (_count >= Capacity) return;
            AttackStyle style = unit.Style;
            if (_mods.Seeking && style == AttackStyle.Straight) style = AttackStyle.Seek;
            _bullets[_count++] = new Bullet
            {
                Position = position,
                Direction = direction,
                Speed = SpeedOf(unit.Projectile) * _mods.ShotSpeed,
                Damage = damage,
                BaseDamage = damage,
                Range = unit.Range * _mods.Range,
                Splash = unit.Splash > 0f ? unit.Splash + _mods.ExtraSplash : 0f,
                Pierce = unit.Pierce + _mods.ExtraPierce,
                LastUid = -1,
                Seed = ++_seed,
                Crit = crit,
                Style = style,
                Element = unit.Element,
                Kind = unit.Projectile,
                Knockback = unit.Knockback + _mods.Knockback,
                Color = unit.Color,
            };
        }

        /// <summary>An arcing shot that bursts where it lands (goes over shields and flyers alike).</summary>
        public void Lob(UnitClass unit, Vector3 position, Vector3 target, float damage, bool crit)
        {
            if (_count >= Capacity) return;
            float distance = Vector3.Distance(position, target);
            _bullets[_count++] = new Bullet
            {
                Position = position,
                Start = position,
                Target = new Vector3(target.x, 0.2f, target.z),
                Duration = Mathf.Clamp(distance / (SpeedOf(unit.Projectile) * 0.8f * _mods.ShotSpeed), 0.35f, 1.4f),
                Damage = damage,
                BaseDamage = damage,
                Splash = unit.Splash + _mods.ExtraSplash,
                LastUid = -1,
                Seed = ++_seed,
                Crit = crit,
                Style = AttackStyle.Lob,
                Element = unit.Element,
                Kind = unit.Projectile,
                Knockback = unit.Knockback + _mods.Knockback,
                Color = unit.Color,
            };
        }

        public void Tick(float dt, List<IBulletTarget> targets)
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                ref Bullet b = ref _bullets[i];
                bool alive = b.Style == AttackStyle.Lob ? TickLob(ref b, dt, targets) : TickFlying(ref b, dt, targets);
                if (!alive) _bullets[i] = _bullets[--_count];
            }
            TickFlashes(dt);
        }

        bool TickLob(ref Bullet b, float dt, List<IBulletTarget> targets)
        {
            b.Clock += dt;
            float u = Mathf.Clamp01(b.Clock / b.Duration);
            float arc = Mathf.Clamp(Vector3.Distance(b.Start, b.Target) * 0.3f, 1.5f, 6f);
            b.Position = Vector3.Lerp(b.Start, b.Target, u) + Vector3.up * (arc * 4f * u * (1f - u));
            if (u < 1f) return true;

            Burst(b.Target, b.Splash, b.Damage, b.Element, b.Knockback, -1);
            foreach (IBulletTarget t in targets)
            {
                if (!t.IsTargetable) continue;
                Vector3 c = t.TargetPosition;
                if (Mathf.Abs(c.x - b.Target.x) < t.HalfWidth + b.Splash && Mathf.Abs(c.z - b.Target.z) < t.HalfDepth + b.Splash)
                {
                    t.OnBulletHit(b.Damage, b.Target, b.Crit, b.Element);
                }
            }
            return false;
        }

        bool TickFlying(ref Bullet b, float dt, List<IBulletTarget> targets)
        {
            if (b.Style == AttackStyle.Seek) Steer(ref b, dt);
            float step = b.Speed * dt;
            Vector3 from = b.Position;
            Vector3 to = from + b.Direction * step;

            // nearest swarm agent along the swept segment
            int hitAgent = -1;
            float hitZ = float.MaxValue;
            _candidates.Clear();
            float x0 = Mathf.Min(from.x, to.x) - MaxAgentRadius, x1 = Mathf.Max(from.x, to.x) + MaxAgentRadius;
            _swarm.Grid.Query(x0, x1, Mathf.Min(from.z, to.z) - MaxAgentRadius, Mathf.Max(from.z, to.z) + MaxAgentRadius, _candidates);
            for (int k = 0; k < _candidates.Count; k++)
            {
                int index = _candidates[k];
                ref Agent a = ref _swarm.Agents[index];
                if (a.Uid == b.LastUid || _swarm.Invulnerable(ref a)) continue;
                float r = _swarm.RadiusOf(ref a);
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
                hitTarget.OnBulletHit(b.Damage, point, b.Crit, b.Element);
                if (b.Splash > 0f) Burst(point, b.Splash, b.Damage * 0.6f, b.Element, 0f, -1);
                return false;
            }

            if (hitAgent >= 0)
            {
                ref Agent a = ref _swarm.Agents[hitAgent];
                int uid = a.Uid;
                bool flying = Enemies.Get(a.Type).Has(EnemyTrait.Flying);
                // straight shots often pass under flyers; seeking arrows never do
                if (flying && b.Style != AttackStyle.Seek && Hash(b.Seed, uid) < Combat.FlyerMiss)
                {
                    b.LastUid = uid;
                    b.Position = to;
                    b.Travel += step;
                    return b.Travel <= b.Range;
                }
                var point = new Vector3(a.Pos.x, b.Position.y, hitZ);
                HitResult result = _swarm.Hit(hitAgent, b.Damage, b.Element, b.Crit, true, _mods.HolyBonus, out float spent);
                if (result == HitResult.Missed)
                {
                    b.LastUid = uid;
                    b.Position = to;
                    b.Travel += step;
                    return b.Travel <= b.Range;
                }
                OnHit(hitAgent, point, uid, b.Damage, b.Element, b.Knockback, result == HitResult.Killed);
                if (b.Splash > 0f) Burst(point, b.Splash, b.Damage * 0.6f, b.Element, 0f, uid);
                if (result == HitResult.Blocked) return false;

                if (result == HitResult.Killed)
                {
                    // carry-over: the shot keeps flying with what the kill did not use up
                    float rest = b.Damage - spent;
                    if (rest < b.BaseDamage * Combat.CarryOverMinimum) return Bounce(ref b, point, uid);
                    b.Damage = rest;
                }
                else if (b.Pierce > 0)
                {
                    b.Pierce--;
                }
                else
                {
                    return Bounce(ref b, point, uid);
                }
                b.LastUid = uid;
                b.Position = new Vector3(to.x, b.Position.y, hitZ + 0.05f);
                b.Travel += step;
                return b.Travel <= b.Range;
            }

            b.Position = to;
            b.Travel += step;
            return b.Travel <= b.Range;
        }

        /// <summary>BOUNCING ROUNDS: a spent shot jumps once to a nearby enemy.</summary>
        bool Bounce(ref Bullet b, Vector3 point, int uid)
        {
            if (!_mods.Ricochet || b.Bounced) return false;
            List<int> near = _swarm.Query(point, 3.5f);
            int best = -1;
            foreach (int j in near)
            {
                if (_swarm.Agents[j].Uid != uid) best = j;
                if (best >= 0) break;
            }
            _swarm.Return(near);
            if (best < 0) return false;
            Vector3 dir = _swarm.Agents[best].Pos + Vector3.up * 0.5f - point;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return false;
            b.Bounced = true;
            b.Direction = dir.normalized;
            b.Damage = b.BaseDamage * 0.7f;
            b.LastUid = uid;
            b.Position = point;
            return true;
        }

        void Steer(ref Bullet b, float dt)
        {
            int target = _swarm.FrontNear(b.Position.x, b.Seed);
            if (target < 0) return;
            Vector3 to = _swarm.Agents[target].Pos + Vector3.up * 0.5f - b.Position;
            if (to.z < 0.5f) return;
            to.y = 0f;
            Vector3 want = to.normalized;
            b.Direction = Vector3.RotateTowards(b.Direction, want, 7f * dt, 0f).normalized;
        }

        static float Hash(int a, int b)
        {
            uint x = unchecked((uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u);
            x ^= x >> 15;
            x *= 0x2C1B3C6Du;
            x ^= x >> 12;
            return (x & 0xFFFFFF) / 16777216f;
        }

        // --- What every hit does ----------------------------------------------------------

        /// <summary>Effects of the class and of the buffs: burning, frost, poison, knockback, blasts, chains, shards.</summary>
        void OnHit(int index, Vector3 point, int uid, float damage, Element element, float knockback, bool killed)
        {
            RunMods m = _mods;
            if (!killed)
            {
                float burn = m.Burn + (element == Element.Fire ? 0.3f : 0f);
                if (burn > 0f) _swarm.Ignite(index, damage * burn * 0.5f, 2f);
                float frost = Mathf.Max(m.Frost, element == Element.Frost ? 0.35f : 0f);
                if (frost > 0f) _swarm.SlowDown(index, frost, 2f);
                if (m.Poison > 0f) _swarm.Envenom(index, damage * m.Poison);
                if (knockback > 0f) _swarm.Push(index, knockback);
            }
            if (m.Blast > 0f) Burst(point, 1f + m.ExtraSplash, damage * m.Blast, element, 0f, uid, small: true);
            if (m.ChainEvery > 0 && ++_hits % m.ChainEvery == 0) ChainFrom(point, uid, damage * 0.5f, m.ChainJumps, Element.Lightning);
            if (killed && m.Shards > 0) Burst(point, 1.2f, damage * 0.3f * m.Shards, element, 0f, uid, small: true);
        }

        /// <summary>Area damage with its effects and a flash.</summary>
        void Burst(Vector3 center, float radius, float damage, Element element, float knockback, int excludeUid, bool small = false)
        {
            if (radius <= 0f) return;
            float burn = element == Element.Fire ? 0.3f + _mods.Burn : _mods.Burn;
            float frost = element == Element.Frost ? Mathf.Max(0.35f, _mods.Frost) : _mods.Frost;
            _swarm.AreaDamage(new Vector3(center.x, 0f, center.z), radius, damage, excludeUid, element, knockback, burn, frost);
            if (small && Random.value > 0.35f) return;
            AddFlash(new Vector3(center.x, 0.05f, center.z), Vector3.zero, Units.ElementColor(element), radius * 2.2f, 0.3f, FlashKind.Ring);
            if (!_ctx.LowQuality || Random.value < 0.5f)
            {
                _ctx.Fx.Sparks(center + Vector3.up * 0.3f, Units.ElementColor(element), small ? 3 : 8, 3f + radius * 2f, 0.3f + radius * 0.1f);
            }
            // meteors leave the ground burning
            if (element == Element.Fire && radius >= 2.5f) _swarm.AddBurningGround(center, radius * 0.6f, 3f, damage * 0.25f);
        }

        // --- Instant attacks ----------------------------------------------------------------

        /// <summary>A piercing line from <paramref name="from"/> towards <paramref name="to"/> (snipers, riflemen).</summary>
        public void Beam(UnitClass unit, Vector3 from, Vector3 to, float damage, bool crit, IBulletTarget fallback)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            float length = Mathf.Min(dir.magnitude, unit.Range * _mods.Range);
            if (length < 0.5f) return;
            dir /= dir.magnitude;
            Vector3 end = from + dir * length;

            _candidates.Clear();
            _swarm.Grid.Query(Mathf.Min(from.x, end.x) - 1f, Mathf.Max(from.x, end.x) + 1f, Mathf.Min(from.z, end.z) - 1f, Mathf.Max(from.z, end.z) + 1f, _candidates);
            // nearest first: insertion sort by distance along the line (the lists are short)
            for (int k = 1; k < _candidates.Count; k++)
            {
                int item = _candidates[k];
                float d = Vector3.Dot(_swarm.Agents[item].Pos - from, dir);
                int j = k - 1;
                while (j >= 0 && Vector3.Dot(_swarm.Agents[_candidates[j]].Pos - from, dir) > d)
                {
                    _candidates[j + 1] = _candidates[j];
                    j--;
                }
                _candidates[j + 1] = item;
            }

            float left = damage;
            int pierce = unit.Pierce + _mods.ExtraPierce;
            Vector3 stop = end;
            foreach (int index in _candidates)
            {
                ref Agent a = ref _swarm.Agents[index];
                if (_swarm.Invulnerable(ref a)) continue;
                Vector3 rel = a.Pos - from;
                float along = Vector3.Dot(rel, dir);
                if (along < 0f || along > length) continue;
                float off = Vector3.Cross(dir, rel).magnitude;
                if (off > _swarm.RadiusOf(ref a) + 0.3f) continue;
                int uid = a.Uid;
                Vector3 point = a.Pos + Vector3.up * 0.6f;
                HitResult r = _swarm.Hit(index, left, unit.Element, crit, true, _mods.HolyBonus, out float spent);
                if (r == HitResult.Missed) continue;
                OnHit(index, point, uid, left, unit.Element, unit.Knockback + _mods.Knockback, r == HitResult.Killed);
                if (r == HitResult.Blocked)
                {
                    stop = point;
                    left = 0f;
                    break;
                }
                if (r == HitResult.Killed)
                {
                    left -= spent;
                    if (left < damage * Combat.CarryOverMinimum)
                    {
                        stop = point;
                        left = 0f;
                        break;
                    }
                }
                else if (pierce-- <= 0)
                {
                    stop = point;
                    left = 0f;
                    break;
                }
            }
            if (left > 0f && fallback != null && fallback.IsTargetable && Vector3.Distance(fallback.TargetPosition, from) <= length + fallback.HalfDepth)
            {
                fallback.OnBulletHit(left, fallback.TargetPosition, crit, unit.Element);
                stop = fallback.TargetPosition;
            }
            AddFlash(from, new Vector3(stop.x, from.y, stop.z), unit.Color, crit ? 0.22f : 0.14f, 0.12f, FlashKind.Line);
        }

        /// <summary>Lightning that leaps from enemy to enemy (wizards, THUNDER ROUNDS).</summary>
        public void Chain(UnitClass unit, Vector3 from, int first, float damage, bool crit)
        {
            if (!_swarm.IsAlive(first)) return;
            Vector3 p = _swarm.Agents[first].Pos + Vector3.up * 0.6f;
            AddFlash(from, p, Units.ElementColor(Element.Lightning), 0.12f, 0.15f, FlashKind.Line);
            int uid = _swarm.Agents[first].Uid;
            HitResult r = _swarm.Hit(first, damage, unit.Element, crit, false, _mods.HolyBonus, out _);
            if (r != HitResult.Missed) OnHit(first, p, uid, damage, unit.Element, 0f, r == HitResult.Killed);
            ChainFrom(p, uid, damage * 0.85f, unit.Chains + _mods.ChainJumps, unit.Element);
        }

        void ChainFrom(Vector3 point, int fromUid, float damage, int jumps, Element element)
        {
            _chainHit.Clear();
            _chainHit.Add(fromUid);
            Vector3 at = point;
            for (int j = 0; j < jumps; j++)
            {
                List<int> near = _swarm.Query(at, 3f);
                int best = -1;
                float bestD = float.MaxValue;
                foreach (int k in near)
                {
                    ref Agent a = ref _swarm.Agents[k];
                    if (_chainHit.Contains(a.Uid)) continue;
                    float d = (a.Pos - at).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = k;
                    }
                }
                _swarm.Return(near);
                if (best < 0) return;
                Vector3 next = _swarm.Agents[best].Pos + Vector3.up * 0.6f;
                AddFlash(at, next, Units.ElementColor(Element.Lightning), 0.1f, 0.15f, FlashKind.Line);
                _chainHit.Add(_swarm.Agents[best].Uid);
                _swarm.Hit(best, damage, element, false, false, _mods.HolyBonus, out _);
                at = next;
                damage *= 0.85f;
            }
        }

        /// <summary>Light called down on an enemy (clerics): ignores shields, bursts on groups.</summary>
        public void Smite(UnitClass unit, int target, float damage, bool crit)
        {
            if (!_swarm.IsAlive(target)) return;
            Vector3 p = _swarm.Agents[target].Pos;
            int uid = _swarm.Agents[target].Uid;
            HitResult r = _swarm.Hit(target, damage, unit.Element, crit, false, _mods.HolyBonus, out _);
            if (r != HitResult.Missed) OnHit(target, p + Vector3.up * 0.6f, uid, damage, unit.Element, unit.Knockback, r == HitResult.Killed);
            float splash = unit.Splash + _mods.ExtraSplash;
            if (splash > 0f) _swarm.AreaDamage(p, splash, damage * 0.6f, uid, unit.Element);
            AddFlash(p + Vector3.up * 9f, p, unit.Color, crit ? 0.6f : 0.42f, 0.22f, FlashKind.Pillar);
            if (splash > 0.5f) AddFlash(new Vector3(p.x, 0.05f, p.z), Vector3.zero, unit.Color, splash * 2.2f, 0.25f, FlashKind.Ring);
        }

        /// <summary>Melee sweeps in front of the squad (knights): every enemy in reach takes the hit.</summary>
        public void Sweep(UnitClass unit, Vector3 center, float damage, bool flash)
        {
            float radius = unit.Range * Mathf.Sqrt(_mods.Range);
            _swarm.AreaDamage(center, radius, damage, -1, unit.Element, unit.Knockback + _mods.Knockback, _mods.Burn, _mods.Frost);
            if (flash) AddFlash(new Vector3(center.x, 0.06f, center.z), Vector3.zero, unit.Color, radius * 2f, 0.18f, FlashKind.Ring);
        }

        /// <summary>A hit on the boss (or a crate / gate) from an instant attack when no enemy is in reach.</summary>
        public void Strike(IBulletTarget target, Vector3 from, float damage, bool crit, Color color, bool fromSky, Element element = Element.Physical)
        {
            if (target == null || !target.IsTargetable) return;
            Vector3 p = target.TargetPosition;
            target.OnBulletHit(damage, p, crit, element);
            if (fromSky) AddFlash(p + Vector3.up * 9f, p, color, 0.5f, 0.2f, FlashKind.Pillar);
            else AddFlash(from, p, color, 0.14f, 0.12f, FlashKind.Line);
        }

        // --- Flashes and drawing ----------------------------------------------------------------

        void AddFlash(Vector3 a, Vector3 b, Color color, float width, float life, FlashKind kind)
        {
            if (_flashCount >= FlashCapacity) return;
            _flashes[_flashCount++] = new Flash { A = a, B = b, Color = color, Width = width, Life = life, MaxLife = life, Kind = kind };
        }

        void TickFlashes(float dt)
        {
            for (int i = _flashCount - 1; i >= 0; i--)
            {
                _flashes[i].Life -= dt;
                if (_flashes[i].Life <= 0f) _flashes[i] = _flashes[--_flashCount];
            }
        }

        public void Render()
        {
            if (_count > 0)
            {
                // alpha-blended rather than additive: additive glow vanishes against the light road
                _batch.Begin(GameAssets.Bolt, GameAssets.Transparent);
                for (int i = 0; i < _count; i++)
                {
                    ref Bullet b = ref _bullets[i];
                    if (b.Style == AttackStyle.Lob || b.Kind == ProjectileKind.Orb || b.Kind == ProjectileKind.Cannon || b.Kind == ProjectileKind.Holy) continue;
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
                    if (b.Style == AttackStyle.Lob) size = b.Kind == ProjectileKind.Cannon ? 0.5f : 0.38f + b.Splash * 0.08f;
                    else if (b.Kind == ProjectileKind.Orb) size = 0.32f + b.Splash * 0.08f;
                    else if (b.Kind == ProjectileKind.Cannon) size = 0.45f;
                    else if (b.Kind == ProjectileKind.Holy) size = 0.3f + b.Splash * 0.1f;
                    else continue;
                    float wobble = 1f + Mathf.Sin((b.Travel + b.Clock * 10f) * 3f + i) * 0.1f;
                    if (b.Crit) size *= 1.4f;
                    _batch.Add(Matrix4x4.TRS(b.Position, Quaternion.identity, Vector3.one * (size * wobble)), Faded(ref b));
                }
                _batch.End();
            }

            if (_flashCount == 0) return;
            _batch.Begin(GameAssets.Bolt, GameAssets.Transparent);
            for (int i = 0; i < _flashCount; i++)
            {
                ref Flash f = ref _flashes[i];
                if (f.Kind == FlashKind.Ring) continue;
                Vector3 d = f.B - f.A;
                float length = d.magnitude;
                if (length < 0.01f) continue;
                float t = f.Life / f.MaxLife;
                Color c = f.Color;
                c.a = t;
                var scale = new Vector3(f.Width * (0.6f + 0.4f * t), f.Width * (0.6f + 0.4f * t), length);
                _batch.Add(Matrix4x4.TRS((f.A + f.B) * 0.5f, Quaternion.LookRotation(d), scale), c);
            }
            _batch.End();

            _batch.Begin(GameAssets.GroundQuad, GameAssets.Shadow);
            for (int i = 0; i < _flashCount; i++)
            {
                ref Flash f = ref _flashes[i];
                if (f.Kind != FlashKind.Ring) continue;
                float t = f.Life / f.MaxLife;
                Color c = f.Color;
                c.a = 0.75f * t;
                float size = f.Width * (1.15f - 0.25f * t);
                _batch.Add(Matrix4x4.TRS(f.A, Quaternion.identity, new Vector3(size, 1f, size)), c);
            }
            _batch.End();
        }

        static Color Faded(ref Bullet b)
        {
            float fade = b.Style == AttackStyle.Lob ? 1f : 1f - Mathf.Clamp01((b.Travel - b.Range * 0.75f) / (b.Range * 0.25f));
            Color c = b.Crit ? Color.Lerp(b.Color, new Color(1f, 0.95f, 0.3f), 0.6f) : b.Color;
            c.a *= fade;
            return c;
        }
    }
}
