using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    public enum AgentState : byte
    {
        Idle,
        Walk,
        Attack,
        Dead,
    }

    /// <summary>One enemy in the swarm. Plain data so thousands can be updated and drawn every frame.</summary>
    public struct Agent
    {
        public int Type;
        public int Uid;
        public Vector3 Pos;
        public float Hp;
        public float MaxHp;
        public float Shield;
        public AgentState State;
        public float Speed;
        public float Phase;
        public float Flash;
        /// <summary>Ability / ranged-shot cooldown.</summary>
        public float Timer;
        /// <summary>Melee swing clock.</summary>
        public float Clock;
        public bool Struck;
        /// <summary>Seconds left of a war drum speed boost.</summary>
        public float Buff;
        public float Seed;
        /// <summary>Seconds a skeleton needs to get back up (cannot be hit meanwhile).</summary>
        public float Getup;
        public float Scale;
        /// <summary>Soldiers lost per melee strike or ranged hit (scales with the squad the level expects).</summary>
        public int Smash;
        /// <summary>Multiplier on the type's body-slam damage (see <see cref="Balance.ContactWeight"/>).</summary>
        public int Weight;
        public bool LaneLocked;
        public bool Elite;
        public bool Revived;
        public bool Fleeing;
    }

    /// <summary>Receives what happens inside the swarm (rewards, damage to the squad, effects).</summary>
    public interface ISwarmHost
    {
        void OnAgentWoke(ref Agent a);
        void OnContact(ref Agent a);
        void OnMeleeStrike(ref Agent a);
        void OnRangedShot(ref Agent a);
        void OnCritical(ref Agent a, float damage);
        void OnAgentKilled(ref Agent a, bool byContact);
        void OnAgentEscaped(ref Agent a);
        void OnExplosion(Vector3 center, float radius);
    }

    /// <summary>
    /// Every regular enemy of a level as a struct array. Behaviours come from <see cref="EnemyType.Traits"/>,
    /// neighbours are found through a uniform grid, and drawing goes through the instanced <see cref="CrowdRenderer"/>.
    /// Spawns are queued and appended between updates so references into the array stay valid.
    /// </summary>
    public sealed class Swarm
    {
        const int MaxLabels = 18;
        const float RenderDistance = 100f;

        public Agent[] Agents = new Agent[1024];
        public int Count;
        public readonly SwarmGrid Grid = new SwarmGrid();

        readonly ISwarmHost _host;
        readonly GameContext _ctx;
        readonly CrowdModel[] _models = new CrowdModel[Enemies.All.Length];
        readonly List<Agent> _pending = new List<Agent>();
        readonly Stack<List<int>> _lists = new Stack<List<int>>();
        readonly Dictionary<int, LabelLayer.Label> _labels = new Dictionary<int, LabelLayer.Label>();
        int _nextUid = 1;
        float _time;

        /// <summary>Squad fire-rate factor caused by nearby ice wraiths (1 = no effect).</summary>
        public float Chill { get; private set; } = 1f;
        public int Alive { get; private set; }
        /// <summary>Alive plus queued spawns (for the <see cref="Balance.MaxAliveEnemies"/> cap).</summary>
        public int Total => Alive + _pending.Count;

        public Swarm(ISwarmHost host, GameContext ctx)
        {
            _host = host;
            _ctx = ctx;
        }

        public CrowdModel ModelOf(int type)
        {
            CrowdModel m = _models[type];
            if (m != null) return m;
            EnemyType t = Enemies.Get(type);
            bool ghostly = t.Has(EnemyTrait.Phase) || t.Has(EnemyTrait.Chill);
            m = CrowdLibrary.Get(t.Model, ghostly ? GameAssets.Transparent : GameAssets.Toon);
            _models[type] = m;
            return m;
        }

        /// <summary>Queues a new enemy. <paramref name="hpBase"/> is multiplied by the type's HP (and by 5 for elites).</summary>
        public void Spawn(int type, Vector3 pos, float hpBase, int smash, int weight, bool laneLocked, bool elite, bool awake = false, float getup = 0f)
        {
            EnemyType t = Enemies.Get(type);
            float hp = Mathf.Max(0.3f, hpBase * t.Hp * (elite ? Balance.EliteHp : 1f));
            _pending.Add(new Agent
            {
                Type = type,
                Uid = _nextUid++,
                Pos = pos,
                Hp = hp,
                MaxHp = hp,
                Shield = t.Has(EnemyTrait.Shield) ? hp * 1.5f : 0f,
                State = awake ? AgentState.Walk : AgentState.Idle,
                Speed = t.Speed * Random.Range(0.9f, 1.1f),
                Phase = Random.Range(0f, 10f),
                Seed = Random.Range(0f, 100f),
                Timer = Random.Range(0.3f, 1.5f),
                Getup = getup,
                Scale = t.ModelScale * (elite ? Balance.EliteScale : 1f),
                Smash = Mathf.Max(1, smash),
                Weight = Mathf.Max(1, weight),
                LaneLocked = laneLocked,
                Elite = elite,
            });
            ModelOf(type);
        }

        void FlushPending()
        {
            if (_pending.Count == 0) return;
            int needed = Count + _pending.Count;
            if (needed > Agents.Length) System.Array.Resize(ref Agents, Mathf.NextPowerOfTwo(needed));
            for (int i = 0; i < _pending.Count; i++) Agents[Count++] = _pending[i];
            Alive += _pending.Count;
            _pending.Clear();
        }

        // --- Simulation -------------------------------------------------------------

        public void Tick(float dt, Squad squad, bool battle)
        {
            FlushPending();
            _time += dt;
            float chill = 1f;
            int alive = 0;
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead) continue;
                alive++;
                EnemyType t = Enemies.Get(a.Type);
                float dz = a.Pos.z - squad.Z;
                a.Flash = Mathf.Max(0f, a.Flash - dt * 8f);
                a.Buff = Mathf.Max(0f, a.Buff - dt);

                if (a.Getup > 0f)
                {
                    a.Getup -= dt;
                    continue;
                }

                if (a.State == AgentState.Idle)
                {
                    a.Phase += dt * 2f;
                    float wake = t.Has(EnemyTrait.Ambush) ? 6.5f : Balance.EnemyWakeDistance;
                    if (battle && dz < wake)
                    {
                        a.State = AgentState.Walk;
                        _host.OnAgentWoke(ref a);
                    }
                    continue;
                }

                if (t.Has(EnemyTrait.Regen)) a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.04f * dt);
                if (t.Has(EnemyTrait.Chill) && dz < 9f && dz > -2f) chill = Mathf.Min(chill, 0.7f);
                if (battle) Abilities(ref a, t, dt, dz);

                if (a.State == AgentState.Walk) Move(ref a, t, dt, squad, dz, battle);
                if (a.State == AgentState.Dead || !battle || squad.Count == 0) continue;

                bool touching = Touching(ref a, t, squad);
                if (t.Has(EnemyTrait.Melee))
                {
                    if (MeleeTick(ref a, t, dt, touching)) _host.OnMeleeStrike(ref a);
                }
                else if (touching && !a.Fleeing)
                {
                    _host.OnContact(ref a);
                    Kill(i, true);
                }
            }
            FlushPending();
            Chill = chill;
            Alive = alive + (Count - n);
        }

        void Move(ref Agent a, EnemyType t, float dt, Squad squad, float dz, bool battle)
        {
            float speed = a.Speed;
            if (a.Buff > 0f) speed *= 1.45f;
            if (t.Has(EnemyTrait.Berserk)) speed *= 1f + 1.2f * (1f - a.Hp / a.MaxHp);
            if (t.Has(EnemyTrait.Charge) && dz < 11f && dz > 0f) speed *= 2.4f;

            if (a.Fleeing)
            {
                // the gold goblin outruns the marching squad towards the side of the road
                a.Pos.z += (speed * 1.25f + (battle ? Balance.MarchSpeed : 0f)) * dt;
                a.Pos.x = Mathf.MoveTowards(a.Pos.x, a.Seed > 50f ? 3.3f : -3.3f, dt * 1.4f);
                a.Phase += dt * speed * 5f;
                if (dz > 42f)
                {
                    a.State = AgentState.Dead;
                    ReleaseLabel(a.Uid);
                    _host.OnAgentEscaped(ref a);
                }
                return;
            }

            if (t.Has(EnemyTrait.Ranged) && battle && squad.Count > 0 && dz > 12f && dz < 21f)
            {
                // stands its ground and shoots; walking closer resumes once the squad is near
                a.Timer -= dt;
                if (a.Timer <= 0f)
                {
                    a.Timer = (t.IsBig ? 2.6f : 2f) * Random.Range(0.85f, 1.15f);
                    _host.OnRangedShot(ref a);
                }
                a.Phase += dt * 1.5f;
                return;
            }

            a.Pos.z -= speed * dt;
            if (!a.LaneLocked && battle && dz < 16f && dz > -0.5f)
            {
                float homing = t.Has(EnemyTrait.Melee) ? 0.7f : speed > 3.5f ? 2.4f : 1.5f;
                a.Pos.x += Mathf.Clamp(squad.X - a.Pos.x, -1f, 1f) * homing * dt;
            }
            if (t.Has(EnemyTrait.Zigzag)) a.Pos.x += Mathf.Sin(_time * 3.2f + a.Seed) * 2f * dt;
            a.Pos.x = Mathf.Clamp(a.Pos.x, -Balance.SoldierEdgeX, Balance.SoldierEdgeX);
            a.Phase += dt * speed * 4.2f / Mathf.Max(0.5f, a.Scale);
        }

        void Abilities(ref Agent a, EnemyType t, float dt, float dz)
        {
            const EnemyTrait active = EnemyTrait.Healer | EnemyTrait.Drummer | EnemyTrait.Summoner | EnemyTrait.Blink;
            if ((t.Traits & active) == 0 || a.State == AgentState.Idle) return;
            a.Timer -= dt;
            if (a.Timer > 0f) return;

            if (t.Has(EnemyTrait.Healer))
            {
                a.Timer = 1f;
                List<int> near = Query(a.Pos, 2.8f);
                foreach (int j in near)
                {
                    ref Agent b = ref Agents[j];
                    b.Hp = Mathf.Min(b.MaxHp, b.Hp + b.MaxHp * 0.1f);
                }
                Return(near);
                _ctx.Fx.Sparks(a.Pos + Vector3.up * 1.1f, new Color(0.4f, 1f, 0.6f), 4, 3f, 0.3f);
            }
            else if (t.Has(EnemyTrait.Drummer))
            {
                a.Timer = 0.5f;
                List<int> near = Query(a.Pos, 3.4f);
                foreach (int j in near) Agents[j].Buff = 0.8f;
                Return(near);
            }
            else if (t.Has(EnemyTrait.Summoner))
            {
                a.Timer = 3.2f;
                if (dz < 38f && Total < Balance.MaxAliveEnemies)
                {
                    float hpBase = a.MaxHp / t.Hp * 0.5f;
                    for (int s = 0; s < 2; s++)
                    {
                        Vector3 p = a.Pos + new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.8f, 0.1f));
                        Spawn(Enemies.Skeleton, p, hpBase, a.Smash, a.Weight, a.LaneLocked, false, true, 0.45f);
                    }
                    _ctx.Fx.Sparks(a.Pos + Vector3.up, new Color(0.6f, 0.3f, 1f), 8, 4f, 0.35f);
                }
            }
            else if (dz > 5f && dz < 30f && a.State == AgentState.Walk)
            {
                a.Timer = 2.6f;
                var color = new Color(0.8f, 0.4f, 1f);
                _ctx.Fx.Sparks(a.Pos + Vector3.up * 0.6f, color, 6, 3f, 0.3f);
                a.Pos.z -= Mathf.Min(4.5f, dz - 3f);
                a.Pos.x = Mathf.Clamp(a.Pos.x + Random.Range(-1.2f, 1.2f), -3.5f, 3.5f);
                _ctx.Fx.Sparks(a.Pos + Vector3.up * 0.6f, color, 6, 3f, 0.3f);
            }
        }

        static float ReachOf(ref Agent a, EnemyType t) => t.Radius * a.Scale / t.ModelScale + (t.Has(EnemyTrait.LongReach) ? 0.9f : 0f);

        static bool Touching(ref Agent a, EnemyType t, Squad squad)
        {
            float reach = ReachOf(ref a, t);
            float dz = a.Pos.z - squad.Z;
            float reachZ = squad.HalfDepth + reach;
            if (dz > reachZ || dz < -reachZ - 0.5f) return false;
            return Mathf.Abs(a.Pos.x - squad.X) < squad.HalfWidth + reach;
        }

        static bool MeleeTick(ref Agent a, EnemyType t, float dt, bool touching)
        {
            if (a.State == AgentState.Walk && touching)
            {
                a.State = AgentState.Attack;
                a.Clock = 0f;
                a.Struck = false;
            }
            if (a.State != AgentState.Attack) return false;
            a.Clock += dt * (a.Buff > 0f ? 1.3f : 1f);
            float interval = t.AttackInterval;
            bool strike = false;
            if (a.Clock >= interval * 0.55f && !a.Struck)
            {
                a.Struck = true;
                strike = touching;
            }
            if (a.Clock >= interval)
            {
                a.Clock = 0f;
                a.Struck = false;
                if (!touching) a.State = AgentState.Walk;
            }
            return strike;
        }

        /// <summary>True while a melee enemy is hammering the squad (the march stops).</summary>
        public bool AnyBlocking(Squad squad)
        {
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Attack && Touching(ref a, Enemies.Get(a.Type), squad)) return true;
            }
            return false;
        }

        // --- Damage -------------------------------------------------------------

        /// <summary>True while the agent cannot be hit: dead, getting up, or a ghost that has phased out.</summary>
        public bool Invulnerable(ref Agent a)
        {
            if (a.State == AgentState.Dead || a.Getup > 0f) return true;
            return Enemies.Get(a.Type).Has(EnemyTrait.Phase) && Phased(ref a);
        }

        bool Phased(ref Agent a) => Mathf.Sin(_time * 1.7f + a.Seed) > 0.4f;

        public float RadiusOf(ref Agent a)
        {
            EnemyType t = Enemies.Get(a.Type);
            return t.Radius * a.Scale / t.ModelScale;
        }

        /// <summary>Applies one hit. Returns true when it killed the agent.</summary>
        public bool Damage(int i, float damage, bool crit)
        {
            ref Agent a = ref Agents[i];
            if (Invulnerable(ref a)) return false;
            EnemyType t = Enemies.Get(a.Type);
            if (t.Has(EnemyTrait.Armor)) damage *= 0.6f;
            if (a.Shield > 0f)
            {
                float absorbed = Mathf.Min(a.Shield, damage * 0.75f);
                a.Shield -= absorbed;
                damage -= absorbed;
                if (a.Shield <= 0f) _ctx.Fx.Burst(a.Pos + new Vector3(0f, 0.6f, -0.3f), new Color(0.6f, 0.5f, 0.35f), Budget(6), 4f, 0.12f, 0.7f);
            }
            if (a.State == AgentState.Idle) a.State = AgentState.Walk;
            if (t.Has(EnemyTrait.Runaway)) a.Fleeing = true;
            a.Hp -= damage;
            a.Flash = 1f;
            if (crit) _host.OnCritical(ref a, damage);
            if (a.Hp > 0f) return false;
            Kill(i, false);
            return true;
        }

        /// <summary>Damages every agent within the radius (splash, explosions). Returns the kills.</summary>
        public int AreaDamage(Vector3 center, float radius, float damage, int excludeUid = -1)
        {
            List<int> near = Query(center, radius);
            int kills = 0;
            foreach (int j in near)
            {
                if (Agents[j].Uid == excludeUid) continue;
                if (Damage(j, damage, false)) kills++;
            }
            Return(near);
            return kills;
        }

        /// <summary>Damages everything between two distances along the road (VOLLEY). Returns the kills.</summary>
        public int BoxDamage(float z0, float z1, float damage)
        {
            int kills = 0;
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead || a.Pos.z < z0 || a.Pos.z > z1) continue;
                if (Damage(i, damage, false)) kills++;
            }
            return kills;
        }

        public void Kill(int i, bool byContact)
        {
            ref Agent a = ref Agents[i];
            if (a.State == AgentState.Dead) return;
            EnemyType t = Enemies.Get(a.Type);

            if (!byContact && t.Has(EnemyTrait.Revive) && !a.Revived)
            {
                // the skeleton falls apart and gets back up once
                a.Revived = true;
                a.Hp = a.MaxHp * 0.5f;
                a.Getup = 0.9f;
                _ctx.Fx.Burst(a.Pos + Vector3.up * 0.5f, t.Debris, Budget(5), 3f, 0.1f, 0.6f);
                return;
            }

            a.State = AgentState.Dead;
            ReleaseLabel(a.Uid);
            Vector3 center = a.Pos + Vector3.up * (0.5f * a.Scale);
            bool big = t.IsBig || a.Elite;
            _ctx.Fx.Burst(center, t.Debris, Budget(big ? 22 : 5), big ? 6.5f : 4.5f, big ? 0.26f : 0.13f, big ? 1.3f : 0.8f);
            _host.OnAgentKilled(ref a, byContact);

            if (t.Has(EnemyTrait.Split))
            {
                float hpBase = a.MaxHp / t.Hp;
                for (int s = -1; s <= 1; s += 2)
                {
                    Spawn(Enemies.MiniSlime, a.Pos + new Vector3(0.32f * s, 0f, 0.25f), hpBase, a.Smash, a.Weight, a.LaneLocked, false, true);
                }
            }
            if (t.Has(EnemyTrait.Explode))
            {
                float radius = t.Contact >= 4 ? 2.9f : 2.2f;
                _host.OnExplosion(center, radius);
                AreaDamage(a.Pos, radius, a.MaxHp * 3f + 2f, a.Uid);
            }
        }

        int Budget(int pieces) => _ctx.LowQuality ? Mathf.Max(1, pieces / 2) : pieces;

        /// <summary>Victory: everything left on the road falls (with rewards), then the stragglers vanish.</summary>
        public void KillAll()
        {
            FlushPending();
            for (int i = 0; i < Count; i++)
            {
                if (Agents[i].State != AgentState.Dead) Kill(i, false);
            }
            FlushPending();
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead) continue;
                a.State = AgentState.Dead;
                ReleaseLabel(a.Uid);
            }
        }

        /// <summary>Removes enemies around a point without rewards (revive shockwave).</summary>
        public void Clear(Vector3 center, float radius)
        {
            FlushPending();
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead) continue;
                if ((a.Pos - center).sqrMagnitude > radius * radius) continue;
                a.State = AgentState.Dead;
                _ctx.Fx.Burst(a.Pos + Vector3.up * 0.5f, Enemies.Get(a.Type).Debris, Budget(4), 5f, 0.12f, 0.7f);
                ReleaseLabel(a.Uid);
            }
        }

        List<int> Query(Vector3 center, float radius)
        {
            List<int> list = _lists.Count > 0 ? _lists.Pop() : new List<int>(64);
            list.Clear();
            Grid.Query(center.x - radius, center.x + radius, center.z - radius, center.z + radius, list);
            float r2 = radius * radius;
            for (int k = list.Count - 1; k >= 0; k--)
            {
                ref Agent a = ref Agents[list[k]];
                float dx = a.Pos.x - center.x, dz = a.Pos.z - center.z;
                if (a.State == AgentState.Dead || dx * dx + dz * dz > r2) list.RemoveAt(k);
            }
            return list;
        }

        void Return(List<int> list) => _lists.Push(list);

        /// <summary>Drops the dead and the ones left far behind. Call once per frame after all damage.</summary>
        public void Compact(float behindZ)
        {
            FlushPending();
            int w = 0;
            for (int r = 0; r < Count; r++)
            {
                ref Agent a = ref Agents[r];
                if (a.State != AgentState.Dead && a.Pos.z < behindZ)
                {
                    a.State = AgentState.Dead;
                    ReleaseLabel(a.Uid);
                }
                if (a.State == AgentState.Dead) continue;
                if (w != r) Agents[w] = a;
                w++;
            }
            Count = w;
            Alive = w;
        }

        public void BuildGrid(float zMin, float zMax) => Grid.Build(Agents, Count, zMin, zMax);

        // --- Labels / drawing --------------------------------------------------------------

        void ReleaseLabel(int uid)
        {
            if (!_labels.TryGetValue(uid, out LabelLayer.Label label)) return;
            _ctx.Labels.Release(label);
            _labels.Remove(uid);
        }

        public void ReleaseAllLabels()
        {
            foreach (LabelLayer.Label l in _labels.Values) _ctx.Labels.Release(l);
            _labels.Clear();
        }

        /// <summary>Melee, elite, mimic and gold enemies show their HP while close (a capped number at once).</summary>
        public void UpdateLabels(float squadZ)
        {
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead) continue;
                EnemyType t = Enemies.Get(a.Type);
                bool ambush = t.Has(EnemyTrait.Ambush);
                if (!t.Has(EnemyTrait.Melee) && !a.Elite && !t.Has(EnemyTrait.Runaway) && !ambush) continue;
                bool near = a.Pos.z - squadZ < 34f && (a.State != AgentState.Idle || ambush);
                if (!_labels.TryGetValue(a.Uid, out LabelLayer.Label label))
                {
                    if (!near || _labels.Count >= MaxLabels) continue;
                    label = _ctx.Labels.Create(LabelStyle.EnemyHp);
                    _labels[a.Uid] = label;
                }
                else if (!near)
                {
                    ReleaseLabel(a.Uid);
                    continue;
                }
                label.SetText(Fmt.Num(Mathf.CeilToInt(Mathf.Max(0f, a.Hp + a.Shield))));
                label.SetColor(a.Elite || t.Has(EnemyTrait.Runaway) ? UIFactory.Gold : Color.white);
                label.World = a.Pos + Vector3.up * (ModelOf(a.Type).Height * a.Scale + 0.35f + Hover(ref a, t));
            }
        }

        static float Hover(ref Agent a, EnemyType t)
        {
            if (!t.Has(EnemyTrait.Flying)) return 0f;
            float h;
            switch (t.Key)
            {
                case "bat": h = 1.1f; break;
                case "whelp": h = 1.3f; break;
                default: h = 0.35f; break;
            }
            return h + Mathf.Sin(a.Phase * 0.7f) * 0.12f;
        }

        static float FlapOf(EnemyType t)
        {
            switch (t.Key)
            {
                case "bat": return 55f;
                case "whelp": return 30f;
                default: return 0f;
            }
        }

        public void Render(CrowdRenderer renderer, float squadZ)
        {
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead || a.Pos.z - squadZ > RenderDistance) continue;
                EnemyType t = Enemies.Get(a.Type);
                CrowdModel model = ModelOf(a.Type);

                Vector3 scale = Vector3.one * a.Scale;
                if (t.Has(EnemyTrait.Split) || a.Type == Enemies.MiniSlime)
                {
                    float sq = Mathf.Sin(a.Phase * 1.5f) * 0.12f;
                    scale = new Vector3(a.Scale * (1f + sq), a.Scale * (1f - sq), a.Scale * (1f + sq));
                }
                float getup = a.Getup > 0f ? Mathf.Clamp01(a.Getup / 0.9f) : 0f;
                Quaternion facing = Quaternion.Euler(-getup * 80f, a.Fleeing ? 0f : 180f, Mathf.Sin(a.Phase) * 3f);
                Matrix4x4 root = Matrix4x4.TRS(a.Pos + Vector3.up * Hover(ref a, t), facing, scale);

                Color tint = t.Tint;
                if (a.Elite) tint = new Color(1.25f, 1.05f, 0.5f);
                if (a.Buff > 0f) tint *= new Color(1.15f, 0.85f, 0.85f);
                if (t.Has(EnemyTrait.Phase) || t.Has(EnemyTrait.Chill)) tint.a = t.Has(EnemyTrait.Phase) && Phased(ref a) ? 0.22f : 0.82f;

                float swing = 0f;
                if (a.State == AgentState.Attack)
                {
                    float u = a.Clock / Mathf.Max(0.1f, t.AttackInterval);
                    if (u < 0.55f)
                    {
                        float k = u / 0.55f;
                        swing = -130f * (1f - (1f - k) * (1f - k));
                    }
                    else if (u < 0.65f) swing = Mathf.Lerp(-130f, 25f, (u - 0.55f) / 0.1f);
                    else swing = Mathf.Lerp(25f, 0f, (u - 0.65f) / 0.35f);
                    // models without a weapon arm lunge with the whole body instead
                    if (!model.HasArmR) root *= Matrix4x4.Translate(new Vector3(0f, 0f, Mathf.Max(0f, swing) / 25f * 0.35f));
                }
                bool walking = a.State == AgentState.Walk && a.Getup <= 0f;
                var pose = new CrowdPose
                {
                    Phase = a.Phase,
                    Stride = walking ? 30f : 3f,
                    Bob = walking ? 0.04f * a.Scale : 0f,
                    Swing = swing,
                    Flap = FlapOf(t),
                };
                renderer.Add(model, root, tint, a.Flash * 0.85f, pose);
            }
        }

        public void CollectShadows(List<Vector4> into, float squadZ)
        {
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead || a.Pos.z - squadZ > 70f) continue;
                into.Add(new Vector4(a.Pos.x, 0.02f, a.Pos.z, RadiusOf(ref a) * 2.2f));
            }
        }

        /// <summary>The closest living agent with the trait at or beyond <paramref name="minZ"/>, or -1 (auto pilot).</summary>
        public int Find(EnemyTrait trait, float minZ)
        {
            int best = -1;
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead || a.Pos.z < minZ || !Enemies.Get(a.Type).Has(trait)) continue;
                if (best < 0 || a.Pos.z < Agents[best].Pos.z) best = i;
            }
            return best;
        }
    }

    /// <summary>Uniform 1 m grid over the road, rebuilt every frame with a counting sort (no allocations).</summary>
    public sealed class SwarmGrid
    {
        public const float Cell = 1f;
        public const int Cols = 10;
        const float MinX = -5f;

        float _z0;
        int _rows;
        int[] _start = new int[1];
        int[] _fill = new int[1];
        int[] _items = new int[16];
        int[] _cellOf = new int[16];

        public void Build(Agent[] agents, int count, float zMin, float zMax)
        {
            _z0 = zMin;
            _rows = Mathf.Clamp(Mathf.CeilToInt((zMax - zMin) / Cell), 1, 600);
            int cells = _rows * Cols;
            if (_start.Length < cells + 1)
            {
                _start = new int[cells + 1];
                _fill = new int[cells];
            }
            if (_items.Length < count)
            {
                _items = new int[Mathf.NextPowerOfTwo(count)];
                _cellOf = new int[_items.Length];
            }
            System.Array.Clear(_start, 0, cells + 1);
            System.Array.Clear(_fill, 0, cells);
            for (int i = 0; i < count; i++)
            {
                int c = agents[i].State == AgentState.Dead ? -1 : CellIndex(agents[i].Pos.x, agents[i].Pos.z);
                _cellOf[i] = c;
                if (c >= 0) _start[c + 1]++;
            }
            for (int c = 0; c < cells; c++) _start[c + 1] += _start[c];
            for (int i = 0; i < count; i++)
            {
                int c = _cellOf[i];
                if (c >= 0) _items[_start[c] + _fill[c]++] = i;
            }
        }

        int CellIndex(float x, float z)
        {
            int cz = Mathf.FloorToInt((z - _z0) / Cell);
            if (cz < 0 || cz >= _rows) return -1;
            int cx = Mathf.Clamp(Mathf.FloorToInt((x - MinX) / Cell), 0, Cols - 1);
            return cz * Cols + cx;
        }

        /// <summary>Adds the indices of agents whose cell overlaps the rectangle.</summary>
        public void Query(float x0, float x1, float z0, float z1, List<int> results)
        {
            if (_rows <= 0) return;
            int cx0 = Mathf.Clamp(Mathf.FloorToInt((x0 - MinX) / Cell), 0, Cols - 1);
            int cx1 = Mathf.Clamp(Mathf.FloorToInt((x1 - MinX) / Cell), 0, Cols - 1);
            int cz0 = Mathf.Max(0, Mathf.FloorToInt((z0 - _z0) / Cell));
            int cz1 = Mathf.Min(_rows - 1, Mathf.FloorToInt((z1 - _z0) / Cell));
            for (int cz = cz0; cz <= cz1; cz++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int c = cz * Cols + cx;
                    for (int k = _start[c]; k < _start[c + 1]; k++) results.Add(_items[k]);
                }
            }
        }
    }
}
