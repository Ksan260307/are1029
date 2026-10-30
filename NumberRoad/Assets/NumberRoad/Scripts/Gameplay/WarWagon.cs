using NumberRoad.Presentation;
using UnityEngine;

namespace NumberRoad.Gameplay
{
    /// <summary>
    /// The war wagon rolling behind the squad: the base enemies try to reach. It wears its damage - smoke first,
    /// then the banner is shot away and fire breaks out, then the roof is blown off and it lists - and bursts
    /// apart when it is destroyed.
    /// </summary>
    public sealed class WarWagon
    {
        /// <summary>Space between the back of the squad and the front of the wagon.</summary>
        public const float Gap = 1.6f;
        /// <summary>Half the wagon's length along the road.</summary>
        public const float HalfDepth = 1.25f;
        const float Scale = 0.9f;
        const float WheelRadius = 0.55f * Scale;

        readonly GameContext _ctx;
        readonly Transform _root;
        readonly Transform _body;
        readonly GameObject _roof;
        readonly GameObject _flag;
        readonly Transform[] _wheels = new Transform[4];
        readonly Quaternion[] _wheelRest = new Quaternion[4];
        readonly Renderer[] _renderers;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        float _health = 1f;
        float _flash;
        float _shake;
        float _smokeClock;
        float _roll;
        bool _destroyed;

        public Vector3 Position => _root.position;

        public WarWagon(GameContext ctx, Transform parent, Vector3 position)
        {
            _ctx = ctx;
            GameObject go = ModelLibrary.Spawn("war_wagon", parent, position, Scale);
            _root = go.transform;
            _body = Find("war_wagon_Body");
            _roof = Find("war_wagon_Roof")?.gameObject;
            _flag = Find("war_wagon_Flag")?.gameObject;
            string[] wheels = { "war_wagon_WheelFL", "war_wagon_WheelFR", "war_wagon_WheelBL", "war_wagon_WheelBR" };
            for (int i = 0; i < wheels.Length; i++)
            {
                _wheels[i] = Find(wheels[i]);
                if (_wheels[i] != null) _wheelRest[i] = _wheels[i].localRotation;
            }
            _renderers = go.GetComponentsInChildren<Renderer>(true);
            Paint();
        }

        Transform Find(string name)
        {
            foreach (Transform t in _root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        /// <summary>Rolls along behind the squad (x follows it loosely, the wheels turn with the distance covered).</summary>
        public void Follow(Squad squad, float dt)
        {
            Vector3 p = _root.position;
            float z = squad.Z - squad.HalfDepth - Gap - HalfDepth;
            float moved = z - p.z;
            p.z = z;
            p.x = Mathf.Lerp(p.x, squad.X * 0.6f, 1f - Mathf.Exp(-3f * dt));
            _root.position = p;
            if (_destroyed) return;

            _roll += moved / WheelRadius * Mathf.Rad2Deg;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null) _wheels[i].localRotation = Quaternion.Euler(_roll, 0f, 0f) * _wheelRest[i];
            }

            _shake = Mathf.Max(0f, _shake - dt * 4f);
            if (_body != null)
            {
                float lean = _health < 0.25f ? 6f : 0f;
                Vector3 jitter = _shake > 0f ? new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (_shake * 0.12f) : Vector3.zero;
                _body.localPosition = jitter;
                _body.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt * 5f);
                Paint();
            }
            Smoke(dt);
        }

        void Smoke(float dt)
        {
            if (_health >= 0.75f) return;
            _smokeClock -= dt;
            if (_smokeClock > 0f) return;
            _smokeClock = Mathf.Lerp(0.12f, 0.45f, _health / 0.75f);
            Vector3 top = _root.position + new Vector3(Random.Range(-0.5f, 0.5f), 2.2f, Random.Range(-0.4f, 0.6f));
            _ctx.Fx.Burst(top, new Color(0.3f, 0.3f, 0.32f), 2, 1.6f, 0.22f, 1.1f);
            if (_health < 0.5f) _ctx.Fx.Sparks(top + Vector3.down * 0.5f, new Color(1f, 0.5f, 0.12f), _health < 0.25f ? 5 : 2, 3f, 0.3f);
        }

        /// <summary>Shows the new health (0..1): banner gone below a half, roof gone below a quarter.</summary>
        public void SetHealth(float health)
        {
            _health = Mathf.Clamp01(health);
            if (_destroyed) return;
            if (_flag != null && _flag.activeSelf != _health > 0.5f)
            {
                if (_flag.activeSelf) Debris(_flag.transform.position + Vector3.up, new Color(0.18f, 0.43f, 0.9f));
                _flag.SetActive(_health > 0.5f);
            }
            if (_roof != null && _roof.activeSelf != _health > 0.25f)
            {
                if (_roof.activeSelf) Debris(_roof.transform.position + Vector3.up * 0.4f, new Color(0.18f, 0.43f, 0.88f));
                _roof.SetActive(_health > 0.25f);
            }
            Paint();
        }

        /// <summary>An enemy got through and rammed the wagon.</summary>
        public void Hit(Vector3 from)
        {
            if (_destroyed) return;
            _flash = 1f;
            _shake = 1f;
            Vector3 p = new Vector3(Mathf.Clamp(from.x, _root.position.x - 1f, _root.position.x + 1f), 1f, _root.position.z + HalfDepth);
            _ctx.Fx.Burst(p, new Color(0.62f, 0.45f, 0.28f), 4, 4f, 0.14f, 0.6f);
            Paint();
        }

        public void Destroy()
        {
            if (_destroyed) return;
            _destroyed = true;
            Vector3 c = _root.position + Vector3.up * 1.2f;
            _ctx.Fx.Burst(c, new Color(0.45f, 0.3f, 0.18f), 40, 9f, 0.3f, 1.4f);
            _ctx.Fx.Burst(c, new Color(0.25f, 0.25f, 0.28f), 24, 5f, 0.4f, 1.8f);
            _ctx.Fx.Sparks(c, new Color(1f, 0.55f, 0.15f), 40, 10f, 0.6f);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Brings a destroyed or battered wagon back (revive).</summary>
        public void Restore(float health)
        {
            _destroyed = false;
            _root.gameObject.SetActive(true);
            if (_flag != null) _flag.SetActive(true);
            if (_roof != null) _roof.SetActive(true);
            SetHealth(health);
            _ctx.Fx.Sparks(_root.position + Vector3.up * 1.5f, new Color(1f, 0.95f, 0.6f), 30, 6f, 0.5f);
        }

        void Debris(Vector3 at, Color color)
        {
            _ctx.Fx.Burst(at, color, 14, 6f, 0.2f, 1.1f);
            _ctx.Fx.Sparks(at, new Color(1f, 0.6f, 0.2f), 12, 6f, 0.4f);
        }

        void Paint()
        {
            // scorched as it loses health, red for a moment when hit
            Color tint = Color.Lerp(Color.white, new Color(0.45f, 0.38f, 0.34f), (1f - _health) * 0.85f);
            tint = Color.Lerp(tint, new Color(1f, 0.35f, 0.3f), _flash * 0.6f);
            _block.SetColor("_Color", tint);
            foreach (Renderer r in _renderers) r.SetPropertyBlock(_block);
        }
    }
}
