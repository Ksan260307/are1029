using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// The player's army. <see cref="Army"/> holds the real soldier count per class; up to
    /// <see cref="Balance.MaxVisibleSoldiers"/> are drawn (instanced), every present class gets at least one,
    /// and <see cref="FireModel"/> scales the drawn bullets so the squad deals exactly <see cref="Core.Army.Dps"/>.
    /// </summary>
    public sealed class Squad
    {
        struct Soldier
        {
            public int ClassId;
            public Vector3 Local;
            public float FireTimer;
            public float Phase;
            public float Pop;
            public float Glow;
        }

        static readonly CrowdModel[] Models = new CrowdModel[Units.Count];

        public readonly Army Army;
        public readonly UnitLine Fallback;

        readonly GameContext _ctx;
        readonly Soldier[] _soldiers = new Soldier[Balance.MaxVisibleSoldiers];
        readonly int[] _have = new int[Units.Count];
        readonly int[] _want = new int[Units.Count];
        readonly ClassFire[] _plans = new ClassFire[Units.Count];
        readonly float[] _sweep = new float[Units.Count];
        readonly LabelLayer.Label _badge;
        readonly float _guardBonus;
        readonly float _recruitBonus;
        int _visible;
        float _walkClock;
        float _celebrate;
        float _healAccum;
        float _healPopupClock;
        int _healPopup;
        float _spacing;
        bool _walking;
        bool _syncPending;
        Vector3 _syncPoint;
        float _sweepFlash;
        float _regenAccum;
        float _wardClock;

        /// <summary>Buffs picked between waves (heal and regeneration read it here).</summary>
        public RunMods Mods = new RunMods();
        /// <summary>March speed factor (FAST MARCH): the legs move faster too.</summary>
        public float Pace { get; set; } = 1f;

        public int Count => Army.Total;
        public int Peak => Army.Peak;
        /// <summary>Soldiers lost in combat this run; holy units bring them back over time.</summary>
        public int Fallen { get; private set; }
        public int Healed { get; private set; }
        public WeaponStats Weapon { get; set; }
        public float X { get; private set; }
        public float Z { get; private set; }
        public float PrevZ { get; private set; }
        public float TargetX { get; set; }
        public float HalfWidth { get; private set; }
        public float HalfDepth { get; private set; }
        public int VisibleCount => _visible;
        public Vector3 Position => new Vector3(X, 0f, Z);
        public float Dps => Army.Dps(Effective(1f));
        public float GuardChance => Mathf.Clamp(Army.GuardChance(_guardBonus) + Mods.Guard, 0f, 0.9f);

        public Squad(GameContext ctx, Army army, WeaponStats weapon, float guardBonus, float recruitBonus, UnitLine fallback)
        {
            _ctx = ctx;
            Army = army;
            Weapon = weapon;
            Fallback = fallback;
            _guardBonus = guardBonus;
            _recruitBonus = recruitBonus;
            if (Army.Total == 0) Army.Add(Units.First(fallback).Id, 1);
            _badge = ctx.Labels.Create(LabelStyle.SquadBadge);
            Sync(Vector3.zero, false);
            for (int i = 0; i < _visible; i++)
            {
                _soldiers[i].Local = Slot(i);
                _soldiers[i].Pop = 1f;
            }
            Tick(0f, false);
        }

        static CrowdModel ModelOf(int classId)
        {
            CrowdModel m = Models[classId];
            if (m == null)
            {
                m = CrowdLibrary.Get(Units.Get(classId).Model);
                Models[classId] = m;
            }
            return m;
        }

        Vector3 Slot(int index)
        {
            Vector2 o = Formation.Offset(index, _spacing);
            return new Vector3(o.x, 0f, o.y);
        }

        void UpdateExtents()
        {
            _spacing = Balance.FormationSpacing(_visible);
            HalfWidth = Formation.Radius(_visible, _spacing);
            HalfDepth = HalfWidth * 0.85f;
        }

        public void Steer(float deltaX) => TargetX = Mathf.Clamp(TargetX + deltaX, -Balance.SquadMaxX, Balance.SquadMaxX);

        public void Move(float dt, float advance)
        {
            PrevZ = Z;
            Z += advance;
            X = Mathf.Lerp(X, TargetX, 1f - Mathf.Exp(-16f * dt));
        }

        public void Celebrate() => _celebrate = 4f;

        Vector3 WorldOf(ref Soldier s) => new Vector3(Mathf.Clamp(X + s.Local.x, -Balance.SoldierEdgeX, Balance.SoldierEdgeX), 0f, Z + s.Local.z);

        // --- Composition ----------------------------------------------------------------

        /// <summary>
        /// Makes the drawn soldiers match the army's class mix. Losses take the drawn soldiers nearest to
        /// <paramref name="point"/>; newcomers run in from it (e.g. the gate they came through).
        /// </summary>
        void Sync(Vector3 point, bool effects)
        {
            _syncPending = false;
            int[] want = Army.VisiblePerClass(Mathf.Min(Count, Balance.MaxVisibleSoldiers), _want);
            System.Array.Clear(_have, 0, _have.Length);
            for (int i = 0; i < _visible; i++) _have[_soldiers[i].ClassId]++;

            int fx = 0;
            for (int c = 0; c < Units.Count; c++)
            {
                while (_have[c] > want[c])
                {
                    int best = -1;
                    float bestDistance = float.MaxValue;
                    for (int i = 0; i < _visible; i++)
                    {
                        if (_soldiers[i].ClassId != c) continue;
                        float d = (WorldOf(ref _soldiers[i]) - point).sqrMagnitude;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = i;
                        }
                    }
                    if (best < 0) break;
                    if (effects && fx++ < 12)
                    {
                        Color color = Units.LineColor(Units.Get(c).Line);
                        _ctx.Fx.Burst(WorldOf(ref _soldiers[best]) + Vector3.up * 0.4f, Color.Lerp(color, GameContext.AllyColor, 0.5f), 5, 4f, 0.13f, 0.7f);
                    }
                    System.Array.Copy(_soldiers, best + 1, _soldiers, best, _visible - best - 1);
                    _visible--;
                    _have[c]--;
                }
            }

            for (int c = 0; c < Units.Count; c++)
            {
                while (_have[c] < want[c] && _visible < _soldiers.Length)
                {
                    Vector2 jitter = Random.insideUnitCircle * 0.6f;
                    _soldiers[_visible++] = new Soldier
                    {
                        ClassId = c,
                        Local = new Vector3(point.x - X + jitter.x, 0f, point.z - Z + jitter.y),
                        FireTimer = Random.Range(0f, 0.5f),
                        Phase = Random.Range(0f, Mathf.PI * 2f),
                        Pop = 0f,
                    };
                    _have[c]++;
                    if (effects) _ctx.Save?.SeeUnit(c);
                }
            }
            UpdateExtents();
        }

        /// <summary>
        /// Many hits can land in one frame during a big fight; the drawn soldiers catch up once per frame
        /// (in <see cref="Tick"/>) instead of after every single hit.
        /// </summary>
        void SyncLater(Vector3 point)
        {
            _syncPending = true;
            _syncPoint = point;
        }

        /// <summary>Applies a gate, strip tile or crate reward. Returns the change in soldiers.</summary>
        public int Apply(GateEffect effect, Vector3 origin)
        {
            if (effect.IsWeapon)
            {
                Weapon = effect.ApplyToWeapon(Weapon);
                return 0;
            }
            if (effect.kind == GateKind.ClassUp)
            {
                ClassUp();
                return 0;
            }
            int change = Army.Apply(effect, Fallback, _recruitBonus);
            Sync(origin, true);
            _badge.PunchScale(change > 0 ? 0.45f : 0.3f);
            return change;
        }

        public void Gain(int amount, Vector3 origin)
        {
            if (amount <= 0) return;
            Army.AddMixed(amount, Fallback);
            SyncLater(origin);
            _badge.PunchScale(0.35f);
        }

        /// <summary>
        /// Soldiers fall to an enemy. Combat losses can be healed back by the holy line, are soaked up by a
        /// HOLY WARD first, and a SECOND WIND brings half of the fallen back once the army gets small.
        /// </summary>
        public int Lose(int amount, Vector3 hitPoint, bool combat = true)
        {
            if (combat && Mods.Ward > 0 && amount > 0)
            {
                int soaked = Mathf.Min(Mods.Ward, amount);
                Mods.Ward -= soaked;
                amount -= soaked;
                if (_wardClock <= 0f)
                {
                    _wardClock = 0.4f;
                    _ctx.Labels.Popup(Mods.Ward > 0 ? "WARD" : "WARD BROKEN", hitPoint + Vector3.up * 1.5f, new Color(0.7f, 0.9f, 1f), 0.7f);
                    _ctx.Fx.Sparks(hitPoint + Vector3.up * 0.6f, new Color(0.75f, 0.9f, 1f), 6, 4f, 0.25f);
                }
                if (amount <= 0) return 0;
            }
            int removed = Army.Remove(amount);
            if (removed <= 0) return 0;
            if (combat) Fallen += removed;
            SyncLater(hitPoint);
            _badge.PunchScale(0.3f);
            if (Mods.SecondWind && combat && Count < Peak * 0.25f && Fallen > 0)
            {
                Mods.SecondWind = false;
                int back = Recover(Mathf.Max(1, Fallen / 2));
                _ctx.Labels.Popup("SECOND WIND +" + Fmt.Num(back), Position + new Vector3(0f, 2.8f, 0.5f), new Color(1f, 0.95f, 0.55f), 1.3f, 1.2f);
                _ctx.Audio.Play(Sfx.Revive);
            }
            return removed;
        }

        /// <summary>Soldiers of one line join at its first class (buff cards).</summary>
        public int Recruit(UnitLine line, int amount, Vector3 origin)
        {
            int added = Army.Add(Units.First(line).Id, amount);
            Sync(origin, true);
            _badge.PunchScale(0.45f);
            return added;
        }

        /// <summary>Fallen soldiers get back up (FIELD MEDIC, SECOND WIND). Returns how many came back.</summary>
        public int Recover(int amount)
        {
            amount = Mathf.Min(amount, Fallen);
            if (amount <= 0) return 0;
            Fallen -= amount;
            Healed += amount;
            Army.AddMixed(amount, Fallback);
            Sync(Position, true);
            for (int i = 0; i < _visible; i++) _soldiers[i].Glow = 1f;
            return amount;
        }

        /// <summary>Every soldier below the top tier moves one tier up its line. Returns false when all are maxed.</summary>
        public bool ClassUp()
        {
            if (!Army.ClassUp()) return false;
            for (int i = 0; i < _visible; i++)
            {
                UnitClass next = Units.Get(_soldiers[i].ClassId).Next;
                if (next != null) _soldiers[i].ClassId = next.Id;
                _soldiers[i].Glow = 1f;
                _soldiers[i].Pop = 0.55f;
            }
            Sync(Position, true);
            _badge.PunchScale(0.5f);
            return true;
        }

        /// <summary>Gem revive: brings soldiers back on the spot.</summary>
        public void Revive(int amount)
        {
            Army.AddMixed(Mathf.Max(1, amount), Fallback);
            Sync(Position, true);
            for (int i = 0; i < _visible; i++) _soldiers[i].Glow = 1f;
        }

        // --- Per frame ---------------------------------------------------------------------

        public void Tick(float dt, bool walking)
        {
            if (_syncPending) Sync(_syncPoint, true);
            _walking = walking;
            _walkClock += dt * (walking ? 11f * Pace : 2f);
            _celebrate = Mathf.Max(0f, _celebrate - dt);
            _wardClock -= dt;
            float follow = 1f - Mathf.Exp(-8f * dt);
            for (int i = 0; i < _visible; i++)
            {
                ref Soldier s = ref _soldiers[i];
                s.Local = Vector3.Lerp(s.Local, Slot(i), follow);
                s.Pop = Mathf.MoveTowards(s.Pop, 1f, dt * 4f);
                s.Glow = Mathf.Max(0f, s.Glow - dt * 1.5f);
            }
            Heal(dt);

            _badge.SetText(Fmt.Num(Count));
            _badge.World = new Vector3(X, 1.55f + HalfWidth * 0.25f, Z + 0.2f);
        }

        void Heal(float dt)
        {
            _healPopupClock -= dt;
            if (Fallen <= 0 || Count == 0)
            {
                _healAccum = 0f;
                return;
            }
            _healAccum += Army.HealPerSecond * Mods.HealRate * dt;
            // REGENERATION: a share of the army returns every second while soldiers are down
            _regenAccum += Mods.Regen * Mathf.Max(Count, 10) * dt;
            if (_regenAccum >= 1f)
            {
                int r = Mathf.FloorToInt(_regenAccum);
                _regenAccum -= r;
                _healAccum += r;
            }
            if (_healAccum >= 1f)
            {
                int n = Mathf.Min(Fallen, Mathf.FloorToInt(_healAccum));
                _healAccum -= n;
                Fallen -= n;
                Healed += n;
                _healPopup += n;
                Army.AddMixed(n, Fallback);
                SyncLater(Position + new Vector3(0f, 0f, -HalfDepth));
            }
            if (_healPopup > 0 && _healPopupClock <= 0f)
            {
                _healPopupClock = 1.2f;
                _ctx.Labels.Popup("+" + Fmt.Num(_healPopup), Position + new Vector3(HalfWidth * 0.6f, 1.6f, 0f), new Color(1f, 0.95f, 0.55f), 0.7f);
                _ctx.Fx.Sparks(Position + Vector3.up * 0.8f, new Color(1f, 0.95f, 0.6f), 6, 3f, 0.3f);
                _healPopup = 0;
            }
        }

        /// <summary>The weapon after this run's buffs (and the BLOODLUST combo bonus).</summary>
        public WeaponStats Effective(float damageFactor)
        {
            WeaponStats w = Weapon;
            w.fireRateMul *= Mods.Rate;
            w.damageMul *= Mods.Damage * damageFactor;
            w.multiShot = Mathf.Clamp(w.multiShot + Mods.ExtraShots, 1, Balance.MaxMultiShot + 3);
            w.critChance = Mathf.Clamp01(w.critChance + Mods.CritChance);
            return w;
        }

        /// <summary>
        /// Every drawn soldier attacks in its class's own way: seeking arrows, straight bolts, spray, piercing
        /// shots at the toughest enemy, lobbed bursts at the thickest crowd, chain lightning, light from the sky
        /// or sword sweeps in front of the squad. <paramref name="rateScale"/> is below 1 while an ice wraith is near.
        /// </summary>
        public void Fire(float dt, BulletSystem bullets, Swarm swarm, IBulletTarget boss, float rateScale, float damageFactor)
        {
            if (_visible == 0 || Count == 0) return;
            WeaponStats w = Effective(damageFactor);
            float critMultiplier = Balance.CritMultiplier + Mods.CritDamage;
            float throttle = FireModel.Throttle(_have, w, rateScale);
            for (int c = 0; c < Units.Count; c++)
            {
                if (_have[c] > 0) _plans[c] = FireModel.Plan(Units.All[c], Army[c], _have[c], w, throttle, rateScale);
            }
            System.Array.Clear(_sweep, 0, _sweep.Length);
            bool bossInReach = boss != null && boss.IsTargetable;

            float scale = Balance.SoldierScale(_visible);
            bool fired = false;
            for (int i = 0; i < _visible; i++)
            {
                ref Soldier s = ref _soldiers[i];
                ClassFire plan = _plans[s.ClassId];
                s.FireTimer -= dt;
                if (s.FireTimer > 0f) continue;
                s.FireTimer += plan.Interval * Random.Range(0.85f, 1.15f);
                if (s.FireTimer < 0f) s.FireTimer = plan.Interval * Random.value;
                if (s.Pop < 0.6f) continue;

                UnitClass unit = Units.All[s.ClassId];
                float range = unit.Range * Mods.Range;
                Vector3 muzzle = WorldOf(ref s) + new Vector3(0f, 0.55f * scale, 0.35f);
                for (int b = 0; b < plan.Shots; b++)
                {
                    bool crit = Random.value < w.critChance;
                    float damage = plan.Damage * (crit ? critMultiplier : 1f);
                    float angle = (b - (plan.Shots - 1) * 0.5f) * Balance.MultiShotSpreadDegrees;
                    switch (unit.Style)
                    {
                        case AttackStyle.Sweep:
                            _sweep[s.ClassId] += damage;
                            break;
                        case AttackStyle.Spray:
                            bullets.Shoot(unit, muzzle, Quaternion.Euler(0f, angle + Random.Range(-14f, 14f), 0f) * Vector3.forward, damage, crit);
                            break;
                        case AttackStyle.Lob:
                            bullets.Lob(unit, muzzle, LobTarget(swarm, boss, muzzle, range, i + b), damage, crit);
                            break;
                        case AttackStyle.Beam:
                        {
                            Vector3 to;
                            if (unit.Pierce >= 99 && swarm.Toughest >= 0) to = swarm.Agents[swarm.Toughest].Pos + Vector3.up * 0.6f;
                            else if (unit.Pierce >= 99 && bossInReach) to = boss.TargetPosition;
                            else to = muzzle + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * range;
                            bullets.Beam(unit, muzzle, to, damage, crit, boss);
                            break;
                        }
                        case AttackStyle.Chain:
                        {
                            int first = swarm.FrontNear(muzzle.x, i + b);
                            if (first >= 0) bullets.Chain(unit, muzzle, first, damage, crit);
                            else if (bossInReach && boss.TargetPosition.z - Z < range) bullets.Strike(boss, muzzle, damage, crit, unit.Color, false, unit.Element);
                            break;
                        }
                        case AttackStyle.Smite:
                        {
                            int target = swarm.Front(Random.Range(0, 1 << 20));
                            if (target >= 0) bullets.Smite(unit, target, damage, crit);
                            else if (bossInReach && boss.TargetPosition.z - Z < range) bullets.Strike(boss, muzzle, damage, crit, unit.Color, true, unit.Element);
                            break;
                        }
                        default:
                            bullets.Shoot(unit, muzzle, Quaternion.Euler(0f, angle, 0f) * Vector3.forward, damage, crit);
                            break;
                    }
                }
                fired = true;
            }

            // knights: each class swings once per frame with everything its soldiers put into the swing
            _sweepFlash -= dt;
            for (int c = 0; c < Units.Count; c++)
            {
                if (_sweep[c] <= 0f) continue;
                UnitClass unit = Units.All[c];
                var center = new Vector3(X, 0f, Z + HalfDepth + unit.Range * 0.4f);
                bullets.Sweep(unit, center, _sweep[c], _sweepFlash <= 0f);
                if (bossInReach && boss.TargetPosition.z - center.z < unit.Range + boss.HalfDepth)
                {
                    bullets.Strike(boss, center, _sweep[c], false, unit.Color, false, unit.Element);
                }
            }
            if (_sweepFlash <= 0f) _sweepFlash = 0.22f;
            if (fired) _ctx.Audio.Play(Sfx.Shoot, 0.26f, 0.12f, 0.06f);
        }

        /// <summary>Where a lobbed shot lands: the thickest crowd ahead, else the nearest enemy, else the boss.</summary>
        Vector3 LobTarget(Swarm swarm, IBulletTarget boss, Vector3 muzzle, float range, int salt)
        {
            Vector3 target;
            if (swarm.HasDensest) target = swarm.Densest;
            else
            {
                int f = swarm.FrontNear(muzzle.x, salt);
                if (f >= 0) target = swarm.Agents[f].Pos;
                else if (boss != null && boss.IsTargetable) target = boss.TargetPosition;
                else target = muzzle + Vector3.forward * (range * 0.7f);
            }
            target += new Vector3(Random.Range(-1.1f, 1.1f), 0f, Random.Range(-1.1f, 1.1f));
            target.z = Mathf.Min(target.z, Z + range);
            target.x = Mathf.Clamp(target.x, -Balance.RoadHalfWidth, Balance.RoadHalfWidth);
            return target;
        }

        public void Render(CrowdRenderer renderer)
        {
            float baseScale = Balance.SoldierScale(_visible);
            bool walking = _walking;
            for (int i = 0; i < _visible; i++)
            {
                ref Soldier s = ref _soldiers[i];
                Vector3 p = WorldOf(ref s);
                if (_celebrate > 0f) p.y = Mathf.Abs(Mathf.Sin(Time.time * 9f + s.Phase)) * 0.45f;
                float scale = baseScale * EaseOutBack(s.Pop);
                Matrix4x4 root = Matrix4x4.TRS(p, Quaternion.Euler(0f, Mathf.Sin(s.Phase * 3f) * 5f, 0f), Vector3.one * scale);
                float phase = _walkClock + s.Phase;
                var pose = new CrowdPose { Phase = phase, Stride = walking ? 34f : 3f, Bob = walking ? 0.045f : 0.01f };
                renderer.Add(ModelOf(s.ClassId), root, Color.white, s.Glow * 0.8f, pose);
            }
        }

        /// <summary>World positions of the drawn soldiers (blob shadows).</summary>
        public void CollectShadows(List<Vector4> into)
        {
            for (int i = 0; i < _visible; i++)
            {
                Vector3 p = WorldOf(ref _soldiers[i]);
                into.Add(new Vector4(p.x, 0.02f, p.z, 0.55f));
            }
        }

        /// <summary>Per-line share of the army (HUD composition bar).</summary>
        public float LineShare(UnitLine line) => Army.LineShare(line);

        public void Dispose() => _ctx.Labels.Release(_badge);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
