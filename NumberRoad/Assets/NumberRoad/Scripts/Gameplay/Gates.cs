using System;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// One numbered gate: two pillars, a beam and a translucent panel showing the
    /// effect. Shootable gates count up by one with every bullet that hits them.
    /// </summary>
    public sealed class GateView : IBulletTarget
    {
        public static readonly Color GoodColor = new Color(0.2f, 0.55f, 1f, 0.92f);
        public static readonly Color BadColor = new Color(1f, 0.26f, 0.2f, 0.92f);
        public static readonly Color WeaponColor = new Color(1f, 0.74f, 0.16f, 0.92f);

        public GateEffect Effect;
        public readonly bool Shootable;
        public readonly int MaxValue;
        public readonly float CenterX;
        public readonly float Width;
        public readonly float Z;

        readonly GameContext _ctx;
        readonly Transform _root;
        readonly Transform _panel;
        readonly Renderer _panelRenderer;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly LabelLayer.Label _label;
        readonly Vector3 _panelScale;
        float _punch;
        float _consumeClock = -1f;
        bool _chosen;

        public bool Consumed => _consumeClock >= 0f;
        public bool Finished => _consumeClock >= 1f;

        public GateView(GameContext ctx, Transform parent, float centerX, float width, float z, GateEffect effect,
            bool shootable, int maxValue)
        {
            _ctx = ctx;
            Effect = effect;
            Shootable = shootable;
            MaxValue = maxValue;
            CenterX = centerX;
            Width = width;
            Z = z;

            _root = new GameObject(shootable ? "ShootGate" : "Gate").transform;
            _root.SetParent(parent, false);
            _root.localPosition = new Vector3(centerX, 0f, z);
            float post = width * 0.5f - 0.22f;
            ModelLibrary.Spawn("gate_pillar", _root, new Vector3(-post, 0f, 0f), 0.9f);
            ModelLibrary.Spawn("gate_pillar", _root, new Vector3(post, 0f, 0f), 0.9f);
            GameObject beam = ModelLibrary.Spawn("gate_beam", _root, new Vector3(0f, 2.28f, 0f));
            beam.transform.localScale = new Vector3(width - 0.3f, 1f, 1f);

            var panel = new GameObject("Panel");
            _panel = panel.transform;
            _panel.SetParent(_root, false);
            _panel.localPosition = new Vector3(0f, 1.18f, 0f);
            _panelScale = new Vector3(width - 0.62f, 1.95f, 1f);
            _panel.localScale = _panelScale;
            panel.AddComponent<MeshFilter>().sharedMesh = GameAssets.Quad;
            _panelRenderer = panel.AddComponent<MeshRenderer>();
            _panelRenderer.sharedMaterial = GameAssets.Panel;
            _panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _label = ctx.Labels.Create(effect.AffectsCount ? LabelStyle.Gate : LabelStyle.GateWord);
            _label.World = new Vector3(centerX, 1.25f, z);
            Refresh();
        }

        public bool Contains(float x) => Mathf.Abs(x - CenterX) <= Width * 0.5f;

        void Refresh()
        {
            Color color = !Effect.AffectsCount ? WeaponColor : Effect.IsBeneficial ? GoodColor : BadColor;
            _block.SetColor("_Color", color);
            _panelRenderer.SetPropertyBlock(_block);
            _label.SetText(Effect.Label);
        }

        // --- IBulletTarget ------------------------------------------------------------
        public bool IsTargetable => Shootable && !Consumed;
        public Vector3 TargetPosition => new Vector3(CenterX, 1f, Z);
        public float HalfWidth => Width * 0.5f - 0.3f;
        public float HalfDepth => 0.3f;

        public void OnBulletHit(float damage, Vector3 point)
        {
            if (Effect.value < MaxValue)
            {
                Effect.value++;
                Refresh();
            }
            _punch = 1f;
            _label.PunchScale(0.12f);
        }

        public void Consume(bool chosen)
        {
            if (Consumed) return;
            _consumeClock = 0f;
            _chosen = chosen;
            if (chosen) _ctx.Fx.Sparks(new Vector3(CenterX, 1.2f, Z), Effect.IsBeneficial ? new Color(0.2f, 0.6f, 1f) : new Color(1f, 0.25f, 0.2f), 14, 6f, 0.4f);
        }

        public void Tick(float dt)
        {
            _punch = Mathf.Max(0f, _punch - dt * 6f);
            float s = 1f + _punch * 0.06f;
            if (Consumed && _consumeClock < 1f)
            {
                _consumeClock = Mathf.Min(1f, _consumeClock + dt * 2.5f);
                float t = _consumeClock;
                if (_chosen) s *= 1f + t * 0.3f;
                _root.localScale = Vector3.one * Mathf.Lerp(1f, 0.0f, t * t);
                _label.Alpha = 1f - t;
            }
            _panel.localScale = Vector3.Scale(_panelScale, new Vector3(s, s, 1f));
        }

        public void Dispose()
        {
            _ctx.Labels.Release(_label);
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }

    /// <summary>Two gates across the road; the squad's centre picks exactly one.</summary>
    public sealed class GatePair
    {
        public readonly GateView Left;
        public readonly GateView Right;
        public readonly float Z;
        public bool Done;

        public GatePair(GameContext ctx, Transform parent, SpawnEvent ev, bool shootable)
        {
            Z = ev.z;
            float half = Balance.RoadHalfWidth;
            Left = new GateView(ctx, parent, -half * 0.5f, half, ev.z, ev.left, shootable, ev.leftMax);
            Right = new GateView(ctx, parent, half * 0.5f, half, ev.z, ev.right, shootable, ev.rightMax);
        }

        public GateView Pick(float squadX) => squadX < 0f ? Left : Right;

        public void Tick(float dt)
        {
            Left.Tick(dt);
            Right.Tick(dt);
        }

        public void Dispose()
        {
            Left.Dispose();
            Right.Dispose();
        }
    }

    /// <summary>A small "+n" pad. Several in a row form a strip along one lane.</summary>
    public sealed class StripTile
    {
        public readonly GateEffect Effect;
        public readonly float X;
        public readonly float Z;
        public readonly float Width;
        public bool Collected { get; private set; }

        readonly GameContext _ctx;
        readonly Transform _root;
        readonly LabelLayer.Label _label;
        float _clock = -1f;

        public StripTile(GameContext ctx, Transform parent, float x, float z, float width, GateEffect effect)
        {
            _ctx = ctx;
            Effect = effect;
            X = x;
            Z = z;
            Width = width;

            var go = new GameObject("StripTile");
            _root = go.transform;
            _root.SetParent(parent, false);
            _root.localPosition = new Vector3(x, 0.55f, z);
            _root.localScale = new Vector3(width, 0.95f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = GameAssets.Quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GameAssets.Panel;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", GateView.GoodColor);
            renderer.SetPropertyBlock(block);

            _label = ctx.Labels.Create(LabelStyle.GateSmall, effect.Label);
            _label.World = new Vector3(x, 0.6f, z);
        }

        public bool Overlaps(Squad squad) => Mathf.Abs(squad.X - X) < Width * 0.5f + squad.HalfWidth * 0.35f;

        public void Collect()
        {
            Collected = true;
            _clock = 0f;
            _ctx.Fx.Sparks(new Vector3(X, 0.8f, Z), new Color(0.2f, 0.6f, 1f), 5, 4f, 0.3f);
        }

        public void Tick(float dt)
        {
            if (_clock < 0f || _clock >= 1f) return;
            _clock = Mathf.Min(1f, _clock + dt * 4f);
            _root.localScale = new Vector3(Width, 0.95f, 1f) * (1f - _clock);
            _label.Alpha = 1f - _clock;
        }

        public void Dispose()
        {
            _ctx.Labels.Release(_label);
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }

    /// <summary>A crate with hit points; shooting it open grants its reward.</summary>
    public sealed class Crate : IBulletTarget
    {
        public readonly GateEffect Reward;
        public readonly float MaxHp;
        public readonly Vector3 Position;
        public float Hp { get; private set; }
        public bool Broken { get; private set; }
        public Action<Crate, bool> Opened;

        readonly GameContext _ctx;
        readonly GameObject _go;
        readonly CharacterRig _rig;
        readonly LabelLayer.Label _hpLabel;
        readonly LabelLayer.Label _rewardLabel;
        float _flash;
        float _punch;

        public Crate(GameContext ctx, Transform parent, Vector3 position, float hp, GateEffect reward)
        {
            _ctx = ctx;
            Position = position;
            MaxHp = Mathf.Max(1f, hp);
            Hp = MaxHp;
            Reward = reward;
            _go = ModelLibrary.Spawn("crate", parent, position, 1.1f);
            _go.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            _rig = new CharacterRig(_go);
            _hpLabel = ctx.Labels.Create(LabelStyle.Crate);
            _hpLabel.World = position + new Vector3(0f, 1.9f, 0f);
            _rewardLabel = ctx.Labels.Create(LabelStyle.Reward, reward.Label);
            _rewardLabel.World = position + new Vector3(0f, 0.65f, -0.7f);
            UpdateLabel();
        }

        void UpdateLabel() => _hpLabel.SetText(Mathf.CeilToInt(Mathf.Max(0f, Hp)).ToString());

        public bool IsTargetable => !Broken;
        public Vector3 TargetPosition => Position;
        public float HalfWidth => 0.62f;
        public float HalfDepth => 0.62f;

        public void OnBulletHit(float damage, Vector3 point)
        {
            if (Broken) return;
            Hp -= damage;
            _flash = 1f;
            _punch = 1f;
            UpdateLabel();
            _ctx.Audio.Play(Sfx.Hit, 0.25f, 0.2f, 0.08f);
            if (Hp <= 0f) Break(true);
        }

        public void Break(bool byBullets)
        {
            if (Broken) return;
            Broken = true;
            _ctx.Fx.Burst(Position + Vector3.up * 0.6f, GameContext.WoodColor, 18, 6f, 0.22f, 1.2f);
            _ctx.Audio.Play(Sfx.CrateBreak);
            Opened?.Invoke(this, byBullets);
            Dispose();
        }

        public void Tick(float dt)
        {
            if (Broken) return;
            _flash = Mathf.Max(0f, _flash - dt * 8f);
            _punch = Mathf.Max(0f, _punch - dt * 7f);
            _rig.SetFlash(_flash * 0.7f);
            _rig.Apply();
            _go.transform.localScale = Vector3.one * (1.1f * (1f + _punch * 0.06f));
        }

        public void Dispose()
        {
            _ctx.Labels.Release(_hpLabel);
            _ctx.Labels.Release(_rewardLabel);
            if (_go != null) Object.Destroy(_go);
        }
    }
}
