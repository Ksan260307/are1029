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
        // effects
        public float Burn;
        public float BurnDps;
        public float Slow;
        public float SlowFactor;
        public float Poison;
        public float PoisonDps;
        /// <summary>Backwards speed from a knockback, decaying quickly.</summary>
        public float Knock;
        /// <summary>Time since this agent was last updated (far agents are updated every few frames).</summary>
        public float Pending;
    }

    public enum HitResult
    {
        /// <summary>Nothing was hit (the agent cannot be hurt by this right now): the shot flies on.</summary>
        Missed,
        Hurt,
        /// <summary>A shield took the hit: the shot stops.</summary>
        Blocked,
        Killed,
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
        void OnExplosion(Vector3 center, float radius, bool fire);
        void OnBurningGround(float soldiersPerSecond, float dt);
        /// <summary>The agent got behind the squad and rams the war wagon (it is removed right after).</summary>
        void OnBreach(ref Agent a);
    }

    /// <summary>
    /// Every regular enemy of a level as a struct array. Behaviours come from <see cref="EnemyType.Traits"/>,
    /// neighbours are found through a uniform grid, and drawing goes through the instanced <see cref="CrowdRenderer"/>.
    /// To stay light with thousands of enemies, idle and distant ones are updated every few frames (decided by
    /// their distance to the squad, never by the frame rate), and spawns are queued so references stay valid.
    /// </summary>
    public sealed class Swarm
    {
        const int MaxLabels = 18;
        const float RenderDistance = 145f;
        const int FrontSize = 48;
        const float FrontReach = 36f;

        /// <summary>A patch of burning ground left by fire imps and meteors: hurts whatever stands in it.</summary>
        struct Patch
        {
            public Vector3 Pos;
            public float Radius;
            public float Time;
            public float Dps;
        }

        public Agent[] Agents = new Agent[1024];
        public int Count;
        public readonly SwarmGrid Grid = new SwarmGrid();

        readonly ISwarmHost _host;
        readonly GameContext _ctx;
        readonly CrowdModel[] _models = new CrowdModel[Enemies.All.Length];
        readonly List<Agent> _pending = new List<Agent>();
        readonly Stack<List<int>> _lists = new Stack<List<int>>();
        readonly Dictionary<int, LabelLayer.Label> _labels = new Dictionary<int, LabelLayer.Label>();
        readonly int[] _front = new int[FrontSize];
        readonly float[] _frontDz = new float[FrontSize];
        readonly List<Patch> _patches = new List<Patch>();
        readonly List<Vector4> _auras = new List<Vector4>();
        readonly InstancedBatch _batch = new InstancedBatch();
        int _nextUid = 1;
        int _frame;
        float _time;
        float _patchClock;

        /// <summary>Squad fire-rate factor caused by nearby ice wraiths (1 = no effect).</summary>
        public float Chill { get; private set; } = 1f;
        public int Alive { get; private set; }
        /// <summary>Alive plus queued spawns (for the <see cref="Balance.MaxAliveEnemies"/> cap).</summary>
        public int Total => Alive + _pending.Count;
        /// <summary>Speed factor from buffs (SLOW TIME, BERSERK).</summary>
        public float SpeedFactor = 1f;
        /// <summary>Front of the war wagon: an enemy that gets behind this line rams it.</summary>
        public float BreachZ = float.MinValue;

        // targets shared by every soldier this frame
        public int FrontCount { get; private set; }
        public int Toughest { get; private set; } = -1;
        public Vector3 Densest { get; private set; }
        public bool HasDensest { get; private set; }

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
                Shield = t.Has(EnemyTrait.Shield) ? hp * 2f : 0f,
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

        /// <summary>How many frames apart this agent is updated: near ones every frame, far and idle ones less often.</summary>
        static int Cadence(ref Agent a, float dz)
        {
            if (a.State == AgentState.Idle) return dz > Balance.EnemyWakeDistance + 12f ? 4 : 2;
            if (dz > Balance.FarUpdateDistance) return 2;
            return 1;
        }

        public void Tick(float dt, Squad squad, bool battle)
        {
            FlushPending();
            _time += dt;
            _frame++;
            float chill = 1f;
            int alive = 0;
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead) continue;
                alive++;
                float dz = a.Pos.z - squad.Z;
                a.Pending += dt;
                int cadence = Cadence(ref a, dz);
                if (cadence > 1 && (_frame + a.Uid) % cadence != 0) continue;
                float step = a.Pending;
                a.Pending = 0f;

                EnemyType t = Enemies.Get(a.Type);
                a.Flash = Mathf.Max(0f, a.Flash - step * 8f);
                a.Buff = Mathf.Max(0f, a.Buff - step);
                if (a.Knock > 0.01f)
                {
                    a.Pos.z += a.Knock * step;
                    a.Knock *= Mathf.Exp(-9f * step);
                }
                if (Effects(i, ref a, t, step)) continue;

                if (a.Getup > 0f)
                {
                    a.Getup -= step;
                    continue;
                }

                if (a.State == AgentState.Idle)
                {
                    a.Phase += step * 2f;
                    float wake = t.Has(EnemyTrait.Ambush) ? 6.5f : Balance.EnemyWakeDistance;
                    if (battle && dz < wake)
                    {
                        a.State = AgentState.Walk;
                        _host.OnAgentWoke(ref a);
                    }
                    continue;
                }

                if (t.Has(EnemyTrait.Regen) && a.Burn <= 0f) a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.04f * step);
                if (t.Has(EnemyTrait.Chill) && dz < 9f && dz > -2f) chill = Mathf.Min(chill, 0.7f);
                if (battle) Abilities(ref a, t, step, dz);

                if (a.State == AgentState.Walk) Move(ref a, t, step, squad, dz, battle);
                if (a.State == AgentState.Dead || !battle || squad.Count == 0) continue;
                if (a.Pos.z < BreachZ && !a.Fleeing)
                {
                    // slipped past the squad: it runs on into the war wagon
                    _host.OnBreach(ref a);
                    a.State = AgentState.Dead;
                    ReleaseLabel(a.Uid);
                    continue;
                }

                bool touching = Touching(ref a, t, squad);
                if (t.Has(EnemyTrait.Melee))
                {
                    if (MeleeTick(ref a, t, step, touching)) _host.OnMeleeStrike(ref a);
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
            TickPatches(dt, squad, battle);
        }

        /// <summary>Burning, poison and frost. Returns true when the effects killed the agent.</summary>
        bool Effects(int i, ref Agent a, EnemyType t, float step)
        {
            if (a.Slow > 0f) a.Slow -= step;
            float damage = 0f;
            if (a.Burn > 0f)
            {
                a.Burn -= step;
                damage += a.BurnDps * step;
            }
            if (a.Poison > 0f)
            {
                a.Poison -= step;
                damage += a.PoisonDps * step;
                if (a.Poison <= 0f) a.PoisonDps = 0f;
            }
            if (damage <= 0f || a.Getup > 0f) return false;
            a.Hp -= damage;
            if (a.Hp > 0f) return false;
            Kill(i, false);
            return true;
        }

        void Move(ref Agent a, EnemyType t, float dt, Squad squad, float dz, bool battle)
        {
            float speed = a.Speed * SpeedFactor;
            if (a.Buff > 0f) speed *= 1.45f;
            if (a.Slow > 0f) speed *= 1f - a.SlowFactor;
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
                // wolves and runners swing wide and come in from the flanks; the rest home straight in
                float homing = t.Has(EnemyTrait.Melee) ? 0.7f : speed > 3.5f ? 2.4f : 1.5f;
                float aim = squad.X;
                if (t.Has(EnemyTrait.Charge) && dz > 6f) aim = squad.X + (a.Seed > 50f ? 2.6f : -2.6f);
                a.Pos.x += Mathf.Clamp(aim - a.Pos.x, -1f, 1f) * homing * dt;
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
            a.Clock += dt * (a.Buff > 0f ? 1.3f : 1f) * (a.Slow > 0f ? 1f - a.SlowFactor * 0.5f : 1f);
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

        // --- Targets ------------------------------------------------------------------

        /// <summary>
        /// Picks this frame's shared targets in one pass: the nearest enemies ahead (seeking arrows, lightning,
        /// sky strikes), the toughest one in range (snipers) and the thickest crowd (area attacks).
        /// </summary>
        public void PrepareTargets(Squad squad)
        {
            FrontCount = 0;
            Toughest = -1;
            float toughest = 0f;
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead || a.Getup > 0f) continue;
                float dz = a.Pos.z - squad.Z;
                if (dz < -0.5f || dz > FrontReach) continue;
                if (a.Hp > toughest)
                {
                    toughest = a.Hp;
                    Toughest = i;
                }
                // keep the FrontSize nearest (insertion into a small sorted array)
                if (FrontCount < FrontSize || dz < _frontDz[FrontCount - 1])
                {
                    int k = FrontCount < FrontSize ? FrontCount++ : FrontSize - 1;
                    while (k > 0 && _frontDz[k - 1] > dz)
                    {
                        _front[k] = _front[k - 1];
                        _frontDz[k] = _frontDz[k - 1];
                        k--;
                    }
                    _front[k] = i;
                    _frontDz[k] = dz;
                }
            }
            HasDensest = Grid.Densest(squad.Z + 6f, squad.Z + 30f, out Vector3 point);
            Densest = point;
        }

        /// <summary>The k-th nearest enemy ahead (wraps around), or -1.</summary>
        public int Front(int k) => FrontCount == 0 ? -1 : _front[(k & int.MaxValue) % FrontCount];

        /// <summary>The front enemy closest to <paramref name="x"/> among the nearest few.</summary>
        public int FrontNear(float x, int k)
        {
            if (FrontCount == 0) return -1;
            int best = -1;
            float bestCost = float.MaxValue;
            int n = Mathf.Min(FrontCount, 12);
            for (int j = 0; j < n; j++)
            {
                int i = _front[(j + k) % FrontCount];
                float cost = Mathf.Abs(Agents[i].Pos.x - x) * 2f + _frontDz[(j + k) % FrontCount] * 0.15f;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = i;
                }
            }
            return best;
        }

        public bool IsAlive(int i) => i >= 0 && i < Count && Agents[i].State != AgentState.Dead;

        // --- Damage -------------------------------------------------------------

        /// <summary>True while the agent cannot be hit at all: dead or getting up.</summary>
        public bool Invulnerable(ref Agent a) => a.State == AgentState.Dead || a.Getup > 0f;

        /// <summary>A faded ghost: only holy light and lightning touch it.</summary>
        public bool Faded(ref Agent a) => Enemies.Get(a.Type).Has(EnemyTrait.Phase) && Phased(ref a);

        bool Phased(ref Agent a) => Mathf.Sin(_time * 1.7f + a.Seed) > 0.4f;

        public float RadiusOf(ref Agent a)
        {
            EnemyType t = Enemies.Get(a.Type);
            return t.Radius * a.Scale / t.ModelScale;
        }

        /// <summary>
        /// Applies one hit. <paramref name="spent"/> is the part of <paramref name="damage"/> it used up: all of it,
        /// or, for a kill, just what the enemy had left (a carried-over shot flies on with the rest).
        /// <paramref name="frontal"/> hits (straight shots) are stopped by shields.
        /// </summary>
        public HitResult Hit(int i, float damage, Element element, bool crit, bool frontal, float holyBonus, out float spent)
        {
            spent = 0f;
            ref Agent a = ref Agents[i];
            if (Invulnerable(ref a)) return HitResult.Missed;
            EnemyType t = Enemies.Get(a.Type);
            if (Faded(ref a) && element != Element.Holy && element != Element.Lightning && holyBonus <= 0f) return HitResult.Missed;
            float multiplier = t.Multiplier(element);
            if (holyBonus > 0f && t.Has(EnemyTrait.Undead)) multiplier *= 1f + holyBonus;

            if (a.State == AgentState.Idle) a.State = AgentState.Walk;
            if (t.Has(EnemyTrait.Runaway)) a.Fleeing = true;
            a.Flash = 1f;
            float effective = damage * multiplier;
            spent = damage;

            if (frontal && a.Shield > 0f)
            {
                // the shield takes the whole hit and the shot stops; lobs, sky strikes and sweeps go around it
                a.Shield -= effective;
                if (a.Shield <= 0f) _ctx.Fx.Burst(a.Pos + new Vector3(0f, 0.6f, -0.3f), new Color(0.6f, 0.5f, 0.35f), Budget(8), 4f, 0.13f, 0.7f);
                return HitResult.Blocked;
            }

            float before = a.Hp;
            a.Hp -= effective;
            if (crit) _host.OnCritical(ref a, effective);
            if (a.Hp > 0f) return HitResult.Hurt;
            spent = Mathf.Min(damage, before / Mathf.Max(0.01f, multiplier));
            Kill(i, false);
            return HitResult.Killed;
        }

        /// <summary>Applies one plain physical hit. Returns true when it killed the agent.</summary>
        public bool Damage(int i, float damage, bool crit) => Hit(i, damage, Element.Physical, crit, false, 0f, out _) == HitResult.Killed;

        public void Ignite(int i, float dps, float seconds)
        {
            ref Agent a = ref Agents[i];
            if (a.State == AgentState.Dead) return;
            float mult = Enemies.Get(a.Type).Multiplier(Element.Fire);
            a.BurnDps = Mathf.Max(a.BurnDps * (a.Burn > 0f ? 1f : 0f), dps * mult);
            a.Burn = Mathf.Max(a.Burn, seconds);
        }

        public void SlowDown(int i, float factor, float seconds)
        {
            ref Agent a = ref Agents[i];
            if (a.State == AgentState.Dead) return;
            float mult = Enemies.Get(a.Type).Multiplier(Element.Frost);
            if (mult < 0.5f) return;
            a.SlowFactor = Mathf.Max(a.SlowFactor * (a.Slow > 0f ? 1f : 0f), Mathf.Clamp01(factor * mult));
            a.Slow = Mathf.Max(a.Slow, seconds);
        }

        public void Envenom(int i, float dps)
        {
            ref Agent a = ref Agents[i];
            if (a.State == AgentState.Dead) return;
            // poison stacks, but never past 3 hits' worth
            a.PoisonDps = Mathf.Min(a.PoisonDps + dps, dps * 3f);
            a.Poison = 3f;
        }

        public void Push(int i, float distance)
        {
            ref Agent a = ref Agents[i];
            if (a.State == AgentState.Dead || Enemies.Get(a.Type).Has(EnemyTrait.Steady)) return;
            a.Knock = Mathf.Max(a.Knock, distance * 9f / Mathf.Max(0.6f, a.Scale));
        }

        /// <summary>Damages every agent within the radius (splash, explosions). Returns the kills.</summary>
        public int AreaDamage(Vector3 center, float radius, float damage, int excludeUid = -1, Element element = Element.Physical,
            float knockback = 0f, float burn = 0f, float frost = 0f)
        {
            List<int> near = Query(center, radius);
            int kills = 0;
            foreach (int j in near)
            {
                if (Agents[j].Uid == excludeUid) continue;
                if (Hit(j, damage, element, false, false, 0f, out _) == HitResult.Killed)
                {
                    kills++;
                    continue;
                }
                if (knockback > 0f) Push(j, knockback);
                if (burn > 0f) Ignite(j, damage * burn * 0.5f, 2f);
                if (frost > 0f) SlowDown(j, frost, 2f);
            }
            Return(near);
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
                a.Burn = a.Poison = 0f;
                _ctx.Fx.Burst(a.Pos + Vector3.up * 0.5f, t.Debris, Budget(5), 3f, 0.1f, 0.6f);
                return;
            }

            a.State = AgentState.Dead;
            ReleaseLabel(a.Uid);
            Vector3 center = a.Pos + Vector3.up * (0.5f * a.Scale);
            bool big = t.IsBig || a.Elite;
            _ctx.Fx.Burst(center, t.Debris, Budget(big ? 22 : 4), big ? 6.5f : 4.5f, big ? 0.26f : 0.13f, big ? 1.3f : 0.75f);
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
                bool fire = t.Contact >= 4;
                float radius = fire ? 2.9f : 2.2f;
                _host.OnExplosion(center, radius, fire);
                AreaDamage(a.Pos, radius, a.MaxHp * 3f + 2f, a.Uid, fire ? Element.Fire : Element.Physical);
                // fire imps leave the ground burning: a lasting hazard that costs nothing per frame but a radius check
                if (fire) AddBurningGround(a.Pos, 1.6f, 4f, a.MaxHp * 1.5f + 1f);
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
            _patches.Clear();
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
            _patches.Clear();
        }

        public List<int> Query(Vector3 center, float radius)
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

        public void Return(List<int> list) => _lists.Push(list);

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
            // indices changed: the shared targets are rebuilt after the grid
            FrontCount = 0;
            Toughest = -1;
        }

        public void BuildGrid(float zMin, float zMax) => Grid.Build(Agents, Count, zMin, zMax);

        // --- Burning ground -----------------------------------------------------------

        public void AddBurningGround(Vector3 pos, float radius, float seconds, float dps)
        {
            if (_patches.Count >= 24) _patches.RemoveAt(0);
            _patches.Add(new Patch { Pos = new Vector3(pos.x, 0f, pos.z), Radius = radius, Time = seconds, Dps = dps });
        }

        void TickPatches(float dt, Squad squad, bool battle)
        {
            if (_patches.Count == 0) return;
            _patchClock += dt;
            bool pulse = _patchClock >= 0.25f;
            if (pulse) _patchClock = 0f;
            for (int p = _patches.Count - 1; p >= 0; p--)
            {
                Patch patch = _patches[p];
                patch.Time -= dt;
                if (patch.Time <= 0f)
                {
                    _patches.RemoveAt(p);
                    continue;
                }
                _patches[p] = patch;
                if (pulse) AreaDamage(patch.Pos, patch.Radius, patch.Dps * 0.25f, -1, Element.Fire);
                if (battle && squad.Count > 0 && Mathf.Abs(patch.Pos.z - squad.Z) < patch.Radius + squad.HalfDepth * 0.5f &&
                    Mathf.Abs(patch.Pos.x - squad.X) < patch.Radius + squad.HalfWidth * 0.5f)
                {
                    _host.OnBurningGround(Mathf.Max(1f, squad.Count * 0.04f), dt);
                }
            }
        }

        /// <summary>Burning patch the auto pilot should steer around, if any is just ahead.</summary>
        public bool HazardAhead(float x, float z, out float hazardX)
        {
            hazardX = 0f;
            foreach (Patch p in _patches)
            {
                if (p.Pos.z - z < -1f || p.Pos.z - z > 8f || Mathf.Abs(p.Pos.x - x) > p.Radius + 2f) continue;
                hazardX = p.Pos.x;
                return true;
            }
            return false;
        }

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
                label.SetText(Fmt.Num(Mathf.CeilToInt(Mathf.Max(0f, a.Hp))));
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

        /// <summary>Ground ring colour for enemies that affect their neighbours (shown so the player can spot them).</summary>
        static bool AuraOf(EnemyType t, out Color color, out float radius)
        {
            if (t.Has(EnemyTrait.Healer))
            {
                color = new Color(0.3f, 1f, 0.5f, 0.45f);
                radius = 2.8f;
                return true;
            }
            if (t.Has(EnemyTrait.Drummer))
            {
                color = new Color(1f, 0.5f, 0.15f, 0.45f);
                radius = 3.4f;
                return true;
            }
            if (t.Has(EnemyTrait.Chill))
            {
                color = new Color(0.5f, 0.85f, 1f, 0.45f);
                radius = 2.4f;
                return true;
            }
            if (t.Has(EnemyTrait.Summoner))
            {
                color = new Color(0.65f, 0.3f, 1f, 0.45f);
                radius = 2f;
                return true;
            }
            color = default;
            radius = 0f;
            return false;
        }

        public void Render(CrowdRenderer renderer, float squadZ)
        {
            _auras.Clear();
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
                if (a.Burn > 0f) tint *= new Color(1.3f, 0.75f, 0.55f);
                else if (a.Slow > 0f) tint *= new Color(0.7f, 0.9f, 1.35f);
                else if (a.Poison > 0f) tint *= new Color(0.75f, 1.2f, 0.6f);
                if (t.Has(EnemyTrait.Berserk) && a.Hp < a.MaxHp * 0.5f) tint *= new Color(1.35f, 0.7f, 0.7f);
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

                if (a.State != AgentState.Idle && AuraOf(t, out Color aura, out float radius) && a.Pos.z - squadZ < 45f)
                {
                    float pulse = 0.85f + 0.15f * Mathf.Sin(_time * 4f + a.Seed);
                    _auras.Add(new Vector4(a.Pos.x, a.Pos.z, radius * 2f * pulse, (int)(aura.r * 255) << 16 | (int)(aura.g * 255) << 8 | (int)(aura.b * 255)));
                }
            }
        }

        /// <summary>Ground decals: support auras and burning patches (one instanced batch).</summary>
        public void RenderGround()
        {
            if (_auras.Count == 0 && _patches.Count == 0) return;
            _batch.Begin(GameAssets.GroundQuad, GameAssets.Shadow);
            foreach (Vector4 v in _auras)
            {
                int rgb = (int)v.w;
                var c = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 0.4f);
                _batch.Add(Matrix4x4.TRS(new Vector3(v.x, 0.025f, v.y), Quaternion.identity, new Vector3(v.z, 1f, v.z)), c);
            }
            foreach (Patch p in _patches)
            {
                float flicker = 0.85f + 0.15f * Mathf.Sin(_time * 18f + p.Pos.x * 3f);
                float fade = Mathf.Clamp01(p.Time / 0.6f);
                _batch.Add(Matrix4x4.TRS(p.Pos + Vector3.up * 0.03f, Quaternion.identity, new Vector3(p.Radius * 2.4f, 1f, p.Radius * 2.4f) * flicker),
                    new Color(1f, 0.45f, 0.08f, 0.75f * fade));
            }
            _batch.End();
            foreach (Patch p in _patches)
            {
                if (Random.value < 0.25f) _ctx.Fx.Sparks(p.Pos + new Vector3(Random.Range(-p.Radius, p.Radius), 0.1f, Random.Range(-p.Radius, p.Radius)),
                    new Color(1f, 0.55f, 0.1f), 1, 2.5f, 0.25f);
            }
        }

        public void CollectShadows(List<Vector4> into, float squadZ)
        {
            for (int i = 0; i < Count; i++)
            {
                ref Agent a = ref Agents[i];
                if (a.State == AgentState.Dead || a.Pos.z - squadZ > 55f) continue;
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

        int CountIn(int cell) => _start[cell + 1] - _start[cell];

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

        /// <summary>Centre of the 3 x 3 m block holding the most agents between two distances (area attacks aim there).</summary>
        public bool Densest(float z0, float z1, out Vector3 point)
        {
            point = Vector3.zero;
            if (_rows <= 0) return false;
            int cz0 = Mathf.Max(1, Mathf.FloorToInt((z0 - _z0) / Cell));
            int cz1 = Mathf.Min(_rows - 2, Mathf.FloorToInt((z1 - _z0) / Cell));
            int best = 0;
            for (int cz = cz0; cz <= cz1; cz += 2)
            {
                for (int cx = 1; cx < Cols - 1; cx += 2)
                {
                    int n = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int row = (cz + dz) * Cols;
                        n += CountIn(row + cx - 1) + CountIn(row + cx) + CountIn(row + cx + 1);
                    }
                    if (n <= best) continue;
                    best = n;
                    point = new Vector3(MinX + (cx + 0.5f) * Cell, 0f, _z0 + (cz + 0.5f) * Cell);
                }
            }
            return best > 0;
        }
    }
}
