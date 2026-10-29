using System;
using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace NumberRoad.Gameplay
{
    public enum EnemyState
    {
        Idle,
        Walking,
        Attacking,
        Dead,
    }

    /// <summary>
    /// One enemy. Foot soldiers wait until the squad comes close, then charge and
    /// trade themselves 1:1 for a soldier on contact. Brutes and the boss stop
    /// and smash the squad with their club until one side is dead.
    /// </summary>
    public sealed class Enemy : IBulletTarget
    {
        public readonly EnemyKind Kind;
        public readonly EnemyArchetype Arch;
        public readonly float MaxHp;
        public readonly bool LaneLocked;
        public readonly int Smash;
        public Vector3 Position;

        /// <summary>The boss stands still until the arena fight starts.</summary>
        public bool Held;

        public Action<Enemy> Killed;

        readonly GameContext _ctx;
        readonly GameObject _go;
        readonly CharacterRig _rig;
        readonly float _speed;
        readonly float _homing;
        readonly LabelLayer.Label _label;
        float _flash;
        float _phase;
        float _attackClock;
        bool _struck;

        public float Hp { get; private set; }
        public EnemyState State { get; private set; }
        public bool IsAlive => State != EnemyState.Dead;
        public bool IsAttacking => State == EnemyState.Attacking;

        public Enemy(GameContext ctx, Transform parent, EnemyKind kind, Vector3 position, float hp, int smash, bool laneLocked)
        {
            _ctx = ctx;
            Kind = kind;
            Arch = Balance.Archetype(kind);
            MaxHp = Mathf.Max(0.1f, hp);
            Hp = MaxHp;
            Smash = Mathf.Max(1, smash);
            LaneLocked = laneLocked;
            Position = position;
            State = EnemyState.Idle;

            _go = ModelLibrary.Spawn(Arch.Model, parent, position, Arch.Scale);
            _go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _rig = new CharacterRig(_go);
            if (kind == EnemyKind.Runner) _rig.SetTint(new Color(1f, 0.86f, 0.5f));
            _rig.Apply();
            _speed = Arch.Speed * Random.Range(0.9f, 1.1f);
            _homing = Arch.Melee ? 0.7f : kind == EnemyKind.Runner ? 2.4f : 1.5f;
            _phase = Random.Range(0f, 10f);

            if (Arch.Melee)
            {
                _label = ctx.Labels.Create(kind == EnemyKind.Boss ? LabelStyle.BossHp : LabelStyle.EnemyHp);
                UpdateLabel();
            }
        }

        // --- IBulletTarget ------------------------------------------------------------
        public bool IsTargetable => State != EnemyState.Dead;
        public Vector3 TargetPosition => Position;
        public float HalfWidth => Arch.Radius;
        public float HalfDepth => Arch.Radius;

        public void OnBulletHit(float damage, Vector3 point)
        {
            if (State == EnemyState.Dead) return;
            Hp -= damage;
            _flash = 1f;
            if (State == EnemyState.Idle && !Held) State = EnemyState.Walking;
            if (Arch.Melee)
            {
                _ctx.Fx.Sparks(new Vector3(point.x, Arch.Scale * 1.3f, point.z), new Color(1f, 0.55f, 0.1f), 1, 4f, 0.25f);
                _ctx.Audio.Play(Sfx.Hit, 0.35f, 0.15f, 0.07f);
            }
            if (Hp <= 0f) Die(false);
        }

        public void Die(bool byContact)
        {
            if (State == EnemyState.Dead) return;
            State = EnemyState.Dead;
            Vector3 center = Position + Vector3.up * (0.6f * Arch.Scale);
            Color color = Kind == EnemyKind.Runner ? new Color(1f, 0.7f, 0.3f) : GameContext.EnemyColor;
            int pieces = Arch.Melee ? 26 : 6;
            _ctx.Fx.Burst(center, color, pieces, Arch.Melee ? 7f : 4.5f, Arch.Melee ? 0.3f : 0.14f, Arch.Melee ? 1.4f : 0.8f);
            if (Arch.Melee) _ctx.Fx.Burst(center, new Color(0.9f, 0.6f, 0.45f), pieces / 2, 6f, 0.25f, 1.2f);
            _ctx.Audio.Play(Sfx.EnemyDie, Arch.Melee ? 1f : 0.45f, 0.15f, 0.04f);
            Cleanup();
            // trading yourself for a soldier on contact earns the player nothing
            if (!byContact) Killed?.Invoke(this);
            Killed = null;
        }

        /// <summary>Removes the enemy silently (walked past the squad, level teardown).</summary>
        public void Despawn()
        {
            if (State == EnemyState.Dead) return;
            State = EnemyState.Dead;
            Cleanup();
            Killed = null;
        }

        void Cleanup()
        {
            if (_label != null) _ctx.Labels.Release(_label);
            if (_go != null) UnityEngine.Object.Destroy(_go);
        }

        public void Wake()
        {
            Held = false;
            if (State == EnemyState.Idle) State = EnemyState.Walking;
        }

        public void Tick(float dt, Squad squad, bool battle)
        {
            if (State == EnemyState.Dead) return;
            float dz = Position.z - squad.Z;
            switch (State)
            {
                case EnemyState.Idle:
                    if (!Held && dz < Balance.EnemyWakeDistance) State = EnemyState.Walking;
                    _phase += dt * 2f;
                    _rig.Walk(_phase, 4f, 0.02f);
                    break;
                case EnemyState.Walking:
                    Position.z -= _speed * dt;
                    if (!LaneLocked && battle && dz < 16f && dz > -0.5f)
                    {
                        float dx = squad.X - Position.x;
                        Position.x += Mathf.Clamp(dx, -1f, 1f) * _homing * dt;
                    }
                    Position.x = Mathf.Clamp(Position.x, -Balance.SoldierEdgeX, Balance.SoldierEdgeX);
                    _phase += dt * _speed * 4.2f / Arch.Scale;
                    _rig.Walk(_phase, 32f, 0.05f * Arch.Scale);
                    break;
            }

            _flash = Mathf.Max(0f, _flash - dt * 8f);
            _rig.SetFlash(_flash * 0.8f);
            _rig.Apply();
            _go.transform.localPosition = Position;
            UpdateLabel();
        }

        /// <summary>Melee state machine. Returns true on the frame a club strike lands on the squad.</summary>
        public bool TickMelee(float dt, bool touching)
        {
            if (!Arch.Melee || State == EnemyState.Dead) return false;
            if (State == EnemyState.Walking && touching)
            {
                State = EnemyState.Attacking;
                _attackClock = 0f;
                _struck = false;
            }
            if (State != EnemyState.Attacking)
            {
                _rig.Swing(0f);
                return false;
            }

            _attackClock += dt;
            float t = _attackClock / Arch.AttackInterval;
            bool strike = false;
            if (t < 0.55f)
            {
                float u = t / 0.55f;
                _rig.Swing(-130f * (1f - (1f - u) * (1f - u)));
            }
            else if (t < 0.65f)
            {
                _rig.Swing(Mathf.Lerp(-130f, 25f, (t - 0.55f) / 0.1f));
                if (!_struck)
                {
                    _struck = true;
                    strike = touching;
                }
            }
            else
            {
                _rig.Swing(Mathf.Lerp(25f, 0f, (t - 0.65f) / 0.35f));
            }

            if (_attackClock >= Arch.AttackInterval)
            {
                _attackClock = 0f;
                _struck = false;
                if (!touching) State = EnemyState.Walking;
            }
            return strike;
        }

        void UpdateLabel()
        {
            if (_label == null) return;
            _label.SetText(Mathf.CeilToInt(Mathf.Max(0f, Hp)).ToString());
            _label.World = Position + Vector3.up * (Arch.Scale * 2.85f);
        }

        public void CollectShadow(List<Vector4> into)
        {
            if (State == EnemyState.Dead) return;
            into.Add(new Vector4(Position.x, 0.02f, Position.z, Arch.Radius * 2.2f));
        }
    }
}
