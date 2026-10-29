using System.Collections.Generic;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>What a boss does to the level; implemented by <see cref="LevelSession"/>.</summary>
    public interface IBossHost
    {
        void OnBossHit(BossUnit boss, float damage, Vector3 point, bool crit);
        void OnBossStrike(BossUnit boss);
        void OnBossSkill(BossUnit boss);
        void OnBossRage(BossUnit boss);
        void OnBossKilled(BossUnit boss);
    }

    /// <summary>
    /// The level's boss. Walks at the squad and smashes it, plus one signature skill per
    /// <see cref="BossSkill"/>; at 30% HP it rages (red, faster, attacks more often).
    /// </summary>
    public sealed class BossUnit : IBulletTarget
    {
        enum State
        {
            Walk,
            Attack,
            Windup,
            Dead,
        }

        const float RageShare = 0.3f;
        const float WindupTime = 0.85f;

        public readonly BossType Type;
        public readonly float MaxHp;
        public readonly int Smash;
        public Vector3 Position;
        /// <summary>Stands still (but can be shot) until the arena fight starts.</summary>
        public bool Held = true;

        public float Hp { get; private set; }
        public bool Raging { get; private set; }
        public bool IsAlive => _state != State.Dead;
        public bool IsAttacking => _state == State.Attack;
        /// <summary>0..1 while a telegraphed skill is charging (shockwave ring, fire breath).</summary>
        public float Windup => _state == State.Windup ? Mathf.Clamp01(_clock / WindupTime) : 0f;
        public float Radius => Type.Radius;

        readonly GameContext _ctx;
        readonly IBossHost _host;
        readonly CrowdModel _model;
        readonly LabelLayer.Label _label;
        State _state = State.Walk;
        float _phase;
        float _flash;
        float _clock;
        float _skillTimer;
        float _dash;
        bool _struck;

        public BossUnit(GameContext ctx, IBossHost host, BossType type, Vector3 position, float hp, int smash)
        {
            _ctx = ctx;
            _host = host;
            Type = type;
            Position = position;
            MaxHp = Mathf.Max(1f, hp);
            Hp = MaxHp;
            Smash = Mathf.Max(1, smash);
            _model = CrowdLibrary.Get(type.Model);
            _label = ctx.Labels.Create(LabelStyle.BossHp);
            _skillTimer = SkillCooldown * 0.6f;
            UpdateLabel();
        }

        float Speed => Type.Speed * (Raging ? 1.5f : 1f);
        float AttackInterval => Type.AttackInterval * (Raging ? 0.7f : 1f);

        float SkillCooldown
        {
            get
            {
                float s;
                switch (Type.Skill)
                {
                    case BossSkill.Summon: s = 6f; break;
                    case BossSkill.Shockwave: s = 7f; break;
                    case BossSkill.FireBreath: s = Type.AttackInterval; break;
                    case BossSkill.Blink: s = 5f; break;
                    case BossSkill.Dash: s = 4.5f; break;
                    default: s = 0f; break;
                }
                return s * (Raging ? 0.65f : 1f);
            }
        }

        // --- IBulletTarget -------------------------------------------------------------
        public bool IsTargetable => IsAlive;
        public Vector3 TargetPosition => Position;
        public float HalfWidth => Type.Radius;
        public float HalfDepth => Type.Radius;

        public void OnBulletHit(float damage, Vector3 point, bool crit)
        {
            if (!IsAlive) return;
            damage *= Type.Armor;
            Hp -= damage;
            _flash = 1f;
            _host.OnBossHit(this, damage, point, crit);
            if (!Raging && Hp > 0f && Hp <= MaxHp * RageShare)
            {
                Raging = true;
                _host.OnBossRage(this);
            }
            if (Hp <= 0f) Die();
            else UpdateLabel();
        }

        void Die()
        {
            if (!IsAlive) return;
            _state = State.Dead;
            Hp = 0f;
            _ctx.Labels.Release(_label);
            Vector3 center = Position + Vector3.up * (_model.Height * Type.Scale * 0.5f);
            _ctx.Fx.Burst(center, new Color(0.9f, 0.6f, 0.45f), 40, 9f, 0.35f, 1.6f);
            _ctx.Fx.Sparks(center, GameContext.GoldColor, 40, 10f, 0.6f);
            _host.OnBossKilled(this);
        }

        public void Despawn()
        {
            if (!IsAlive) return;
            _state = State.Dead;
            _ctx.Labels.Release(_label);
        }

        public bool Touching(Squad squad)
        {
            float dz = Position.z - squad.Z;
            float reachZ = squad.HalfDepth + Type.Radius;
            if (dz > reachZ || dz < -reachZ - 0.5f) return false;
            return Mathf.Abs(Position.x - squad.X) < squad.HalfWidth + Type.Radius;
        }

        public void Tick(float dt, Squad squad, bool fighting)
        {
            if (!IsAlive) return;
            _flash = Mathf.Max(0f, _flash - dt * 8f);
            if (Held || !fighting || squad.Count == 0)
            {
                _phase += dt * 1.5f;
                UpdateLabel();
                return;
            }

            float dz = Position.z - squad.Z;
            bool touching = Touching(squad);
            _skillTimer -= dt;

            switch (_state)
            {
                case State.Walk:
                    if (touching)
                    {
                        _state = State.Attack;
                        _clock = 0f;
                        _struck = false;
                        break;
                    }
                    if (Type.Skill != BossSkill.Smash && _skillTimer <= 0f && StartSkill(squad, dz)) break;
                    bool keepsDistance = Type.Skill == BossSkill.FireBreath && dz < 9.5f;
                    if (!keepsDistance)
                    {
                        float speed = Speed * (_dash > 0f ? 3.2f : 1f);
                        Position.z -= speed * dt;
                        Position.x = Mathf.MoveTowards(Position.x, Mathf.Clamp(squad.X, -2.6f, 2.6f), dt * (Raging ? 1.4f : 0.8f));
                        _phase += dt * speed * 3.2f;
                    }
                    else
                    {
                        _phase += dt * 1.5f;
                    }
                    _dash = Mathf.Max(0f, _dash - dt);
                    break;

                case State.Attack:
                    _clock += dt;
                    if (_clock >= AttackInterval * 0.55f && !_struck)
                    {
                        _struck = true;
                        if (touching) _host.OnBossStrike(this);
                    }
                    if (_clock >= AttackInterval)
                    {
                        _clock = 0f;
                        _struck = false;
                        if (!touching) _state = State.Walk;
                        else if (Type.Skill != BossSkill.Smash && _skillTimer <= 0f) StartSkill(squad, dz);
                    }
                    break;

                case State.Windup:
                    _clock += dt;
                    if (_clock >= WindupTime)
                    {
                        _host.OnBossSkill(this);
                        _state = State.Walk;
                        _skillTimer = SkillCooldown;
                    }
                    break;
            }
            UpdateLabel();
        }

        /// <summary>Starts the signature skill. Instant ones (blink, dash) fire right away.</summary>
        bool StartSkill(Squad squad, float dz)
        {
            switch (Type.Skill)
            {
                case BossSkill.Blink:
                    if (dz < 6f) return false;
                    _ctx.Fx.Sparks(Position + Vector3.up * 1.5f, new Color(0.8f, 0.3f, 1f), 24, 6f, 0.5f);
                    Position = new Vector3(Mathf.Clamp(squad.X + Random.Range(-1f, 1f), -2.6f, 2.6f), 0f, squad.Z + squad.HalfDepth + Type.Radius + 1.2f);
                    _ctx.Fx.Sparks(Position + Vector3.up * 1.5f, new Color(0.8f, 0.3f, 1f), 24, 6f, 0.5f);
                    _ctx.Audio.Play(Sfx.Skill, 0.6f);
                    _skillTimer = SkillCooldown;
                    return true;
                case BossSkill.Dash:
                    if (dz > 14f || dz < 3f) return false;
                    _dash = 0.7f;
                    _skillTimer = SkillCooldown;
                    _ctx.Audio.Play(Sfx.Skill, 0.5f);
                    return false;
                case BossSkill.FireBreath:
                    if (dz > 24f) return false;
                    break;
                case BossSkill.Shockwave:
                    if (dz > 9f) return false;
                    break;
            }
            _state = State.Windup;
            _clock = 0f;
            return true;
        }

        void UpdateLabel()
        {
            if (!IsAlive) return;
            _label.SetText(Fmt.Num(Mathf.CeilToInt(Mathf.Max(0f, Hp))));
            _label.World = Position + Vector3.up * (_model.Height * Type.Scale + 0.5f + Hover);
        }

        float Hover => Type.Skill == BossSkill.FireBreath ? 1.2f + Mathf.Sin(_phase * 0.6f) * 0.2f : 0f;

        public void Render(CrowdRenderer renderer)
        {
            if (!IsAlive) return;
            float swing = 0f;
            if (_state == State.Attack)
            {
                float u = _clock / AttackInterval;
                if (u < 0.55f)
                {
                    float k = u / 0.55f;
                    swing = -130f * (1f - (1f - k) * (1f - k));
                }
                else if (u < 0.65f) swing = Mathf.Lerp(-130f, 25f, (u - 0.55f) / 0.1f);
                else swing = Mathf.Lerp(25f, 0f, (u - 0.65f) / 0.35f);
            }
            else if (_state == State.Windup)
            {
                swing = -150f * Windup;
            }

            float pulse = Raging ? 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) : 0f;
            var tint = new Color(1f, 1f - 0.45f * pulse, 1f - 0.5f * pulse);
            float scale = Type.Scale * (1f + (_state == State.Windup ? 0.06f * Windup : 0f));
            Matrix4x4 root = Matrix4x4.TRS(Position + Vector3.up * Hover, Quaternion.Euler(0f, 180f, 0f), Vector3.one * scale);
            if (!_model.HasArmR && swing != 0f) root *= Matrix4x4.Translate(new Vector3(0f, 0f, Mathf.Max(0f, swing) / 25f * 0.3f));
            bool walking = _state == State.Walk && !Held;
            var pose = new CrowdPose
            {
                Phase = _phase,
                Stride = walking ? 26f : 3f,
                Bob = walking ? 0.06f * Type.Scale : 0f,
                Swing = swing,
                Flap = Type.Skill == BossSkill.FireBreath ? 26f : 0f,
            };
            renderer.Add(_model, root, tint, Mathf.Max(_flash * 0.75f, pulse * 0.15f), pose);
        }

        public void CollectShadow(List<Vector4> into)
        {
            if (IsAlive) into.Add(new Vector4(Position.x, 0.02f, Position.z, Type.Radius * 2.4f));
        }
    }
}
