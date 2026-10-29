using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// The player's army. <see cref="Count"/> is the real number of soldiers; up to
    /// <see cref="Balance.MaxVisibleSoldiers"/> of them are drawn, and the bullet
    /// damage is scaled so the squad always deals exactly <see cref="Balance.SquadDps"/>.
    /// </summary>
    public sealed class Squad
    {
        sealed class Soldier
        {
            public GameObject Go;
            public CharacterRig Rig;
            public Vector3 Local;
            public float FireTimer;
            public float Phase;
            public float Pop;
        }

        readonly GameContext _ctx;
        readonly Transform _root;
        readonly List<Soldier> _soldiers = new List<Soldier>();
        readonly Stack<Soldier> _pool = new Stack<Soldier>();
        readonly LabelLayer.Label _badge;
        float _walkClock;
        float _celebrate;

        public int Count { get; private set; }
        public WeaponStats Weapon { get; set; }
        public float X { get; private set; }
        public float Z { get; private set; }
        public float PrevZ { get; private set; }
        public float TargetX { get; set; }
        public float HalfWidth { get; private set; }
        public float HalfDepth { get; private set; }
        public int VisibleCount => _soldiers.Count;
        public Vector3 Position => new Vector3(X, 0f, Z);
        public float Dps => Balance.SquadDps(Count, Weapon);

        public Squad(GameContext ctx, Transform parent, int count, WeaponStats weapon)
        {
            _ctx = ctx;
            _root = new GameObject("Squad").transform;
            _root.SetParent(parent, false);
            Weapon = weapon;
            Count = Mathf.Clamp(count, 1, Balance.MaxSquad);
            int visible = Mathf.Min(Count, Balance.MaxVisibleSoldiers);
            for (int i = 0; i < visible; i++)
            {
                Soldier s = Rent();
                s.Local = Slot(i);
                s.Pop = 1f;
                _soldiers.Add(s);
            }
            _badge = ctx.Labels.Create(LabelStyle.SquadBadge);
            UpdateExtents();
            UpdateVisuals(0f, false);
        }

        static Vector3 Slot(int index)
        {
            Vector2 o = Formation.Offset(index, Balance.FormationSpacing);
            return new Vector3(o.x, 0f, o.y);
        }

        void UpdateExtents()
        {
            HalfWidth = Formation.Radius(_soldiers.Count, Balance.FormationSpacing);
            HalfDepth = HalfWidth * 0.85f;
        }

        public void Steer(float deltaX)
        {
            TargetX = Mathf.Clamp(TargetX + deltaX, -Balance.SquadMaxX, Balance.SquadMaxX);
        }

        public void Move(float dt, float advance)
        {
            PrevZ = Z;
            Z += advance;
            X = Mathf.Lerp(X, TargetX, 1f - Mathf.Exp(-16f * dt));
        }

        public void Celebrate() => _celebrate = 4f;

        public void UpdateVisuals(float dt, bool walking)
        {
            _walkClock += dt * (walking ? 11f : 2f);
            _celebrate = Mathf.Max(0f, _celebrate - dt);
            float follow = 1f - Mathf.Exp(-8f * dt);
            for (int i = 0; i < _soldiers.Count; i++)
            {
                Soldier s = _soldiers[i];
                s.Local = Vector3.Lerp(s.Local, Slot(i), follow);
                s.Pop = Mathf.MoveTowards(s.Pop, 1f, dt * 4f);
                var position = new Vector3(
                    Mathf.Clamp(X + s.Local.x, -Balance.SoldierEdgeX, Balance.SoldierEdgeX),
                    0f,
                    Z + s.Local.z);
                if (_celebrate > 0f) position.y = Mathf.Abs(Mathf.Sin(Time.time * 9f + s.Phase)) * 0.45f;
                Transform t = s.Go.transform;
                t.localPosition = position;
                t.localScale = Vector3.one * (Balance.SoldierScale * EaseOutBack(s.Pop));
                float phase = _walkClock + s.Phase;
                s.Rig.Walk(phase, walking ? 34f : 3f, walking ? 0.045f : 0.01f);
            }

            _badge.SetText(Count.ToString());
            _badge.World = new Vector3(X, 1.55f, Z + 0.2f);
        }

        /// <summary>Emits this frame's bullets. Damage per bullet keeps total DPS equal to <see cref="Dps"/>.</summary>
        public void Fire(float dt, BulletSystem bullets)
        {
            int visible = _soldiers.Count;
            if (visible == 0 || Count == 0) return;
            FirePlan plan = Balance.PlanFire(Count, visible, Weapon);
            float interval = plan.Interval;
            float damage = plan.Damage;
            int shots = plan.Shots;
            Color color = BulletSystem.ColorForTier(Weapon.Tier);
            bool fired = false;

            for (int i = 0; i < visible; i++)
            {
                Soldier s = _soldiers[i];
                s.FireTimer -= dt;
                if (s.FireTimer > 0f) continue;
                s.FireTimer += interval * Random.Range(0.85f, 1.15f);
                if (s.FireTimer < 0f) s.FireTimer = interval * Random.value;
                if (s.Pop < 0.6f) continue;

                Vector3 muzzle = s.Go.transform.localPosition + new Vector3(0f, 0.52f, 0.4f);
                for (int b = 0; b < shots; b++)
                {
                    float angle = (b - (shots - 1) * 0.5f) * Balance.MultiShotSpreadDegrees;
                    bullets.Spawn(muzzle, Quaternion.Euler(0f, angle, 0f) * Vector3.forward, damage, color);
                }
                fired = true;
            }

            if (fired) _ctx.Audio.Play(Sfx.Shoot, 0.28f, 0.12f, 0.06f);
        }

        /// <summary>Adds soldiers that run in from <paramref name="origin"/> (e.g. the gate they came through).</summary>
        public void Add(int amount, Vector3 origin)
        {
            if (amount <= 0) return;
            Count = Mathf.Min(Balance.MaxSquad, Count + amount);
            int target = Mathf.Min(Count, Balance.MaxVisibleSoldiers);
            while (_soldiers.Count < target)
            {
                Soldier s = Rent();
                Vector2 jitter = Random.insideUnitCircle * 0.6f;
                s.Local = new Vector3(origin.x - X + jitter.x, 0f, origin.z - Z + jitter.y);
                s.Pop = 0f;
                _soldiers.Add(s);
            }
            UpdateExtents();
            _badge.PunchScale(0.45f);
        }

        /// <summary>Removes soldiers; the visible ones closest to <paramref name="hitPoint"/> fall first.</summary>
        public void Remove(int amount, Vector3 hitPoint)
        {
            if (amount <= 0 || Count == 0) return;
            Count = Mathf.Max(0, Count - amount);
            int target = Mathf.Min(Count, Balance.MaxVisibleSoldiers);
            int removed = 0;
            while (_soldiers.Count > target)
            {
                int best = 0;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < _soldiers.Count; i++)
                {
                    float d = (_soldiers[i].Go.transform.localPosition - hitPoint).sqrMagnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }
                Soldier s = _soldiers[best];
                if (removed++ < 12) _ctx.Fx.Burst(s.Go.transform.localPosition + Vector3.up * 0.4f, GameContext.AllyColor, 5, 4f, 0.13f, 0.7f);
                _soldiers.RemoveAt(best);
                Return(s);
            }
            UpdateExtents();
            _badge.PunchScale(0.3f);
            _ctx.Audio.Play(Sfx.SoldierDie, 0.6f, 0.1f, 0.05f);
        }

        /// <summary>Applies a count-changing gate. Returns the signed change.</summary>
        public int Apply(GateEffect effect, Vector3 origin)
        {
            if (!effect.AffectsCount)
            {
                Weapon = effect.ApplyToWeapon(Weapon);
                return 0;
            }
            int before = Count;
            int after = effect.ApplyToCount(before);
            if (after > before) Add(after - before, origin);
            else if (after < before) Remove(before - after, origin);
            return after - before;
        }

        public void Dispose()
        {
            _ctx.Labels.Release(_badge);
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        Soldier Rent()
        {
            Soldier s;
            if (_pool.Count > 0)
            {
                s = _pool.Pop();
                s.Go.SetActive(true);
            }
            else
            {
                GameObject go = ModelLibrary.Spawn("soldier", _root, Vector3.zero, Balance.SoldierScale);
                s = new Soldier { Go = go, Rig = new CharacterRig(go) };
            }
            s.Phase = Random.Range(0f, Mathf.PI * 2f);
            s.FireTimer = Random.Range(0f, 0.5f);
            s.Go.transform.localRotation = Quaternion.Euler(0f, Random.Range(-6f, 6f), 0f);
            return s;
        }

        void Return(Soldier s)
        {
            s.Go.SetActive(false);
            _pool.Push(s);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>World positions of the visible soldiers (for blob shadows).</summary>
        public void CollectShadows(List<Vector4> into)
        {
            foreach (Soldier s in _soldiers)
            {
                Vector3 p = s.Go.transform.localPosition;
                into.Add(new Vector4(p.x, 0.02f, p.z, 0.55f));
            }
        }
    }
}
