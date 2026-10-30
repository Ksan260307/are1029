using System;
using NumberRoad.Core;
using NumberRoad.Presentation;
using NumberRoad.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// One gate of a row: two pillars, a beam and a translucent panel showing the effect. Special gates:
    /// shootable (counts up per hit), roulette (cycles + − × ÷), mystery ("?"), jackpot (golden ×5)
    /// and locked (shoot the lock open first).
    /// </summary>
    public sealed class GateView : IBulletTarget
    {
        public static readonly Color GoodColor = new Color(0.2f, 0.55f, 1f, 0.92f);
        public static readonly Color BadColor = new Color(1f, 0.26f, 0.2f, 0.92f);
        public static readonly Color WeaponColor = new Color(1f, 0.74f, 0.16f, 0.92f);
        public static readonly Color ClassUpColor = new Color(0.85f, 0.35f, 1f, 0.92f);
        public static readonly Color MysteryColor = new Color(0.5f, 0.32f, 0.8f, 0.92f);
        public static readonly Color JackpotColor = new Color(1f, 0.8f, 0.1f, 0.95f);

        public readonly GateSpec Spec;
        public GateEffect Effect;
        public readonly float Width;
        public readonly float Z;
        public float CenterX { get; private set; }

        readonly GameContext _ctx;
        readonly Transform _root;
        readonly Transform _panel;
        readonly Renderer _panelRenderer;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly LabelLayer.Label _label;
        LabelLayer.Label _lockLabel;
        readonly Vector3 _panelScale;
        readonly float _fit;
        GameObject _lock;
        float _lockHp;
        int _rouletteIndex;
        float _rouletteClock;
        float _punch;
        float _clock;
        float _selected;
        float _consumeClock = -1f;
        bool _chosen;
        bool _revealed;

        public bool Shootable => Spec.shootable;
        public int MaxValue => Spec.maxValue;
        public bool Locked => _lockHp > 0f;
        public bool Mystery => Spec.mystery && !_revealed;
        public bool Consumed => _consumeClock >= 0f;
        public bool Finished => _consumeClock >= 1f;

        public GateView(GameContext ctx, Transform parent, GateSpec spec, float centerX, float width, float z)
        {
            _ctx = ctx;
            Spec = spec;
            Effect = spec.effect;
            CenterX = centerX;
            Width = width;
            Z = z;
            _lockHp = spec.lockHp;

            _root = new GameObject(spec.shootable ? "ShootGate" : "Gate").transform;
            _root.SetParent(parent, false);
            _root.localPosition = new Vector3(centerX, 0f, z);
            float post = width * 0.5f - 0.2f;
            ModelLibrary.Spawn("gate_pillar", _root, new Vector3(-post, 0f, 0f), 0.9f);
            ModelLibrary.Spawn("gate_pillar", _root, new Vector3(post, 0f, 0f), 0.9f);
            GameObject beam = ModelLibrary.Spawn("gate_beam", _root, new Vector3(0f, 2.28f, 0f));
            beam.transform.localScale = new Vector3(width - 0.3f, 1f, 1f);

            var panel = new GameObject("Panel");
            _panel = panel.transform;
            _panel.SetParent(_root, false);
            _panel.localPosition = new Vector3(0f, 1.18f, 0f);
            _panelScale = new Vector3(width - 0.58f, 1.95f, 1f);
            _panel.localScale = _panelScale;
            panel.AddComponent<MeshFilter>().sharedMesh = GameAssets.Quad;
            _panelRenderer = panel.AddComponent<MeshRenderer>();
            _panelRenderer.sharedMaterial = GameAssets.Panel;
            _panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // narrow gates (rows of 3-4) shrink their text so it stays inside the panel
            _fit = Mathf.Clamp(width / 3.8f, 0.5f, 1f);
            _label = ctx.Labels.Create(Effect.AffectsCount ? LabelStyle.Gate : LabelStyle.GateWord);
            _label.World = new Vector3(centerX, 1.25f, z);

            if (Locked)
            {
                _lock = new GameObject("Lock");
                _lock.transform.SetParent(_root, false);
                _lock.transform.localPosition = new Vector3(0f, 0.7f, -0.15f);
                _lock.transform.localScale = new Vector3(0.75f, 0.65f, 0.3f);
                _lock.AddComponent<MeshFilter>().sharedMesh = GameAssets.Cube;
                var r = _lock.AddComponent<MeshRenderer>();
                r.sharedMaterial = GameAssets.Toon;
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", new Color(0.35f, 0.33f, 0.38f));
                r.SetPropertyBlock(block);
                _lockLabel = ctx.Labels.Create(LabelStyle.Crate);
            }
            Refresh();
        }

        Color BaseColor
        {
            get
            {
                if (Mystery) return MysteryColor;
                if (Spec.jackpot) return JackpotColor;
                switch (Effect.kind)
                {
                    case GateKind.ClassUp: return ClassUpColor;
                    case GateKind.Recruit:
                        // the line's colour, pulled towards the "good gate" blue so it never reads as disabled
                        Color c = Color.Lerp(Units.LineColor(Effect.line), GoodColor, 0.35f);
                        c.a = 0.92f;
                        return c;
                }
                if (Effect.IsWeapon) return WeaponColor;
                return Effect.IsBeneficial ? GoodColor : BadColor;
            }
        }

        /// <summary>Bigger numbers get bigger text.</summary>
        float MagnitudeScale
        {
            get
            {
                if (Mystery || !Effect.AffectsCount) return 1f;
                float v = Mathf.Abs(Effect.value);
                if (Effect.kind == GateKind.Multiply || Effect.kind == GateKind.Divide) v *= 15f;
                return 0.9f + Mathf.Clamp01(Mathf.Log10(Mathf.Max(1f, v)) / 4f) * 0.45f;
            }
        }

        void Refresh()
        {
            _label.SetText(Mystery ? "?" : Effect.Label);
            _label.Scale = _fit * MagnitudeScale;
            if (_lockLabel != null) _lockLabel.SetText(Fmt.Num(Mathf.CeilToInt(_lockHp)));
        }

        public void SetCenter(float x)
        {
            CenterX = x;
            _root.localPosition = new Vector3(x, 0f, Z);
            _label.World = new Vector3(x, 1.25f, Z);
        }

        // --- IBulletTarget ------------------------------------------------------------
        public bool IsTargetable => !Consumed && (Shootable || Locked);
        public Vector3 TargetPosition => new Vector3(CenterX, 1f, Z);
        public float HalfWidth => Width * 0.5f - 0.3f;
        public float HalfDepth => 0.3f;

        public void OnBulletHit(float damage, Vector3 point, bool crit, Element element = Element.Physical)
        {
            _punch = 1f;
            if (Locked)
            {
                _lockHp -= damage;
                if (_lockHp <= 0f) Unlock();
                else Refresh();
                return;
            }
            if (Effect.value < MaxValue)
            {
                Effect.value = Mathf.Min(MaxValue, Effect.value + Mathf.Max(1, Spec.step));
                Refresh();
            }
            _label.PunchScale(0.12f);
        }

        void Unlock()
        {
            _lockHp = 0f;
            Vector3 p = new Vector3(CenterX, 0.9f, Z);
            _ctx.Fx.Burst(p, new Color(0.4f, 0.38f, 0.42f), 14, 6f, 0.15f, 0.9f);
            _ctx.Fx.Sparks(p, GameContext.GoldColor, 16, 7f, 0.45f);
            _ctx.Audio.Play(Sfx.Chest, 0.8f);
            _ctx.Labels.Popup("UNLOCKED!", p + Vector3.up * 1.4f, GameContext.GoldColor, 0.9f);
            if (_lock != null) Object.Destroy(_lock);
            _lock = null;
            if (_lockLabel != null) _ctx.Labels.Release(_lockLabel);
            _lockLabel = null;
            _label.PunchScale(0.4f);
            Refresh();
        }

        /// <summary>Freezes the gate (roulette stops, mystery is revealed) and starts its fade.</summary>
        public void Consume(bool chosen)
        {
            if (Consumed) return;
            _consumeClock = 0f;
            _chosen = chosen;
            if (Spec.mystery)
            {
                _revealed = true;
                Refresh();
            }
            if (chosen && !Locked) _ctx.Fx.Sparks(new Vector3(CenterX, 1.2f, Z), BaseColor, 14, 6f, 0.4f);
        }

        public void Tick(float dt, bool selected, bool near)
        {
            _clock += dt;
            _punch = Mathf.Max(0f, _punch - dt * 6f);
            _selected = Mathf.MoveTowards(_selected, selected && !Consumed ? 1f : 0f, dt * 6f);

            if (Spec.roulette != null && !Consumed)
            {
                _rouletteClock += dt;
                if (_rouletteClock >= GateSpec.RouletteStep)
                {
                    _rouletteClock -= GateSpec.RouletteStep;
                    _rouletteIndex = (_rouletteIndex + 1) % Spec.roulette.Length;
                    Effect = Spec.roulette[_rouletteIndex];
                    Refresh();
                    _label.PunchScale(0.15f);
                    if (near) _ctx.Audio.Play(Sfx.Click, 0.25f, 0.05f, 0.1f);
                }
            }

            Color color = BaseColor;
            if (!Effect.IsBeneficial && !Mystery && !Consumed)
            {
                // harmful gates throb red as a warning
                float throb = 0.5f + 0.5f * Mathf.Sin(_clock * 7f);
                color = Color.Lerp(color, new Color(1f, 0.55f, 0.45f, 1f), throb * 0.35f);
            }
            if (Spec.jackpot && !Consumed)
            {
                float shimmer = 0.5f + 0.5f * Mathf.Sin(_clock * 9f);
                color = Color.Lerp(color, new Color(1f, 1f, 0.75f, 1f), shimmer * 0.45f);
                if (near && UnityEngine.Random.value < dt * 6f)
                    _ctx.Fx.Sparks(new Vector3(CenterX + UnityEngine.Random.Range(-0.8f, 0.8f), UnityEngine.Random.Range(0.4f, 2f), Z), GameContext.GoldColor, 1, 2f, 0.3f);
            }
            if (Locked) color = Color.Lerp(color, new Color(0.45f, 0.45f, 0.5f, 0.9f), 0.45f);
            color = Color.Lerp(color, new Color(1f, 1f, 1f, 1f), _selected * (0.18f + 0.1f * Mathf.Sin(_clock * 10f)));
            _block.SetColor("_Color", color);
            _panelRenderer.SetPropertyBlock(_block);

            float s = 1f + _punch * 0.06f + _selected * 0.04f;
            if (Consumed && _consumeClock < 1f)
            {
                _consumeClock = Mathf.Min(1f, _consumeClock + dt * 2.5f);
                float t = _consumeClock;
                if (_chosen) s *= 1f + t * 0.3f;
                _root.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t * t);
                _label.Alpha = 1f - t;
            }
            _panel.localScale = Vector3.Scale(_panelScale, new Vector3(s, s, 1f));
            if (_lockLabel != null && Locked) _lockLabel.World = new Vector3(CenterX, 0.75f, Z - 0.3f);
        }

        public void Dispose()
        {
            _ctx.Labels.Release(_label);
            if (_lockLabel != null) _ctx.Labels.Release(_lockLabel);
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }

    /// <summary>A row of 2-4 gates across the road (optionally sliding sideways); the squad passes through exactly one.</summary>
    public sealed class GateRow
    {
        public readonly GateView[] Gates;
        public readonly float Z;
        public bool Done;

        readonly float _slide;
        readonly float _span;
        readonly float _speed;
        float _clock;

        public GateRow(GameContext ctx, Transform parent, SpawnEvent ev)
        {
            Z = ev.z;
            _slide = ev.slide;
            _span = Balance.RoadHalfWidth * 2f - _slide * 2f;
            _speed = 1.1f + (ev.z % 3f) * 0.15f;
            int n = ev.gates.Length;
            float width = _span / n;
            Gates = new GateView[n];
            for (int i = 0; i < n; i++) Gates[i] = new GateView(ctx, parent, ev.gates[i], CenterOf(i, 0f), width, ev.z);
        }

        float CenterOf(int i, float offset) => -_span * 0.5f + _span / Gates.Length * (i + 0.5f) + offset;

        public bool Shootable
        {
            get
            {
                foreach (GateView g in Gates)
                {
                    if (g.Shootable) return true;
                }
                return false;
            }
        }

        /// <summary>The gate whose centre is nearest to <paramref name="x"/>.</summary>
        public GateView Pick(float x)
        {
            GateView best = Gates[0];
            foreach (GateView g in Gates)
            {
                if (Mathf.Abs(g.CenterX - x) < Mathf.Abs(best.CenterX - x)) best = g;
            }
            return best;
        }

        public void Tick(float dt, float squadX, float squadZ)
        {
            _clock += dt;
            if (_slide > 0f && !Done)
            {
                float offset = Mathf.Sin(_clock * _speed) * _slide;
                for (int i = 0; i < Gates.Length; i++) Gates[i].SetCenter(CenterOf(i, offset));
            }
            float dz = Z - squadZ;
            bool near = dz < 30f && dz > -2f;
            GateView pick = near && !Done ? Pick(squadX) : null;
            foreach (GateView g in Gates) g.Tick(dt, g == pick, near);
        }

        public bool Finished
        {
            get
            {
                foreach (GateView g in Gates)
                {
                    if (!g.Finished) return false;
                }
                return true;
            }
        }

        public void Dispose()
        {
            foreach (GateView g in Gates) g.Dispose();
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

    /// <summary>A crate with hit points; shooting it open grants its reward, running into it costs soldiers.</summary>
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

        void UpdateLabel() => _hpLabel.SetText(Fmt.Num(Mathf.CeilToInt(Mathf.Max(0f, Hp))));

        public bool IsTargetable => !Broken;
        public Vector3 TargetPosition => Position;
        public float HalfWidth => 0.62f;
        public float HalfDepth => 0.62f;

        public void OnBulletHit(float damage, Vector3 point, bool crit, Element element = Element.Physical)
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
            _ctx.Audio.Play(byBullets ? Sfx.Chest : Sfx.CrateBreak);
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
