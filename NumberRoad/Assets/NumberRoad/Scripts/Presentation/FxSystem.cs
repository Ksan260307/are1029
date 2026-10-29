using UnityEngine;
using UnityEngine.Rendering;

namespace NumberRoad.Presentation
{
    /// <summary>
    /// Collects per-instance matrices and colours and renders them with GPU
    /// instancing in chunks of 1023 (bullets, debris, sparks, blob shadows).
    /// </summary>
    public sealed class InstancedBatch
    {
        const int Max = 1023;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        readonly Matrix4x4[] _matrices = new Matrix4x4[Max];
        readonly Vector4[] _colors = new Vector4[Max];
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        Mesh _mesh;
        Material _material;
        int _count;

        public void Begin(Mesh mesh, Material material)
        {
            _mesh = mesh;
            _material = material;
            _count = 0;
        }

        public void Add(in Matrix4x4 matrix, Color color)
        {
            if (_count == Max) Flush();
            _matrices[_count] = matrix;
            _colors[_count] = QualitySettings.activeColorSpace == ColorSpace.Linear ? (Vector4)color.linear : (Vector4)color;
            _count++;
        }

        public void End() => Flush();

        void Flush()
        {
            if (_count == 0 || _mesh == null || _material == null) return;
            _block.SetVectorArray(ColorId, _colors);
            var rp = new RenderParams(_material)
            {
                matProps = _block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };
            Graphics.RenderMeshInstanced(rp, _mesh, 0, _matrices, _count);
            _count = 0;
        }
    }

    /// <summary>Cheap pooled particles: tumbling debris cubes and glowing sparks.</summary>
    public sealed class FxSystem
    {
        struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public Quaternion Rotation;
            public Vector3 SpinAxis;
            public float SpinSpeed;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Gravity;
            public Color Color;
        }

        const int Capacity = 1500;
        readonly Particle[] _debris = new Particle[Capacity];
        readonly Particle[] _sparks = new Particle[Capacity];
        readonly InstancedBatch _batch = new InstancedBatch();
        int _debrisCount;
        int _sparkCount;

        /// <summary>Chunky debris that tumbles, falls and bounces on the road.</summary>
        public void Burst(Vector3 position, Color color, int count, float speed, float size, float life = 0.9f)
        {
            for (int i = 0; i < count && _debrisCount < Capacity; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.4f + 0.3f;
                float shade = Random.Range(0.8f, 1.1f);
                _debris[_debrisCount++] = new Particle
                {
                    Position = position + Random.insideUnitSphere * size,
                    Velocity = dir.normalized * speed * Random.Range(0.5f, 1.1f),
                    Rotation = Random.rotation,
                    SpinAxis = Random.onUnitSphere,
                    SpinSpeed = Random.Range(180f, 720f),
                    Size = size * Random.Range(0.6f, 1.2f),
                    Life = life * Random.Range(0.7f, 1.1f),
                    MaxLife = life,
                    Gravity = 18f,
                    Color = new Color(color.r * shade, color.g * shade, color.b * shade, 1f),
                };
            }
        }

        /// <summary>Short additive streaks flying outwards.</summary>
        public void Sparks(Vector3 position, Color color, int count, float speed = 7f, float size = 0.35f)
        {
            for (int i = 0; i < count && _sparkCount < Capacity; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y);
                _sparks[_sparkCount++] = new Particle
                {
                    Position = position,
                    Velocity = dir * speed * Random.Range(0.4f, 1f),
                    Size = size * Random.Range(0.6f, 1.1f),
                    Life = Random.Range(0.18f, 0.35f),
                    MaxLife = 0.35f,
                    Gravity = 6f,
                    Color = color,
                };
            }
        }

        public void Clear()
        {
            _debrisCount = 0;
            _sparkCount = 0;
        }

        public void Tick(float dt)
        {
            for (int i = _debrisCount - 1; i >= 0; i--)
            {
                ref Particle p = ref _debris[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    _debris[i] = _debris[--_debrisCount];
                    continue;
                }
                p.Velocity.y -= p.Gravity * dt;
                p.Position += p.Velocity * dt;
                if (p.Position.y < p.Size * 0.5f)
                {
                    p.Position.y = p.Size * 0.5f;
                    p.Velocity.y = Mathf.Abs(p.Velocity.y) * 0.35f;
                    p.Velocity.x *= 0.7f;
                    p.Velocity.z *= 0.7f;
                    p.SpinSpeed *= 0.6f;
                }
                p.Rotation = Quaternion.AngleAxis(p.SpinSpeed * dt, p.SpinAxis) * p.Rotation;
            }

            for (int i = _sparkCount - 1; i >= 0; i--)
            {
                ref Particle p = ref _sparks[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    _sparks[i] = _sparks[--_sparkCount];
                    continue;
                }
                p.Velocity.y -= p.Gravity * dt;
                p.Position += p.Velocity * dt;
            }
        }

        public void Render()
        {
            if (_debrisCount > 0)
            {
                _batch.Begin(GameAssets.Cube, GameAssets.Toon);
                for (int i = 0; i < _debrisCount; i++)
                {
                    ref Particle p = ref _debris[i];
                    float shrink = Mathf.Clamp01(p.Life / (p.MaxLife * 0.35f));
                    _batch.Add(Matrix4x4.TRS(p.Position, p.Rotation, Vector3.one * (p.Size * shrink)), p.Color);
                }
                _batch.End();
            }

            if (_sparkCount > 0)
            {
                _batch.Begin(GameAssets.Bolt, GameAssets.Transparent);
                for (int i = 0; i < _sparkCount; i++)
                {
                    ref Particle p = ref _sparks[i];
                    float t = Mathf.Clamp01(p.Life / p.MaxLife);
                    Vector3 v = p.Velocity.sqrMagnitude > 0.001f ? p.Velocity : Vector3.up;
                    var scale = new Vector3(p.Size * 0.35f, p.Size * 0.35f, p.Size * (0.6f + v.magnitude * 0.08f));
                    Color c = p.Color;
                    c.a *= t;
                    _batch.Add(Matrix4x4.TRS(p.Position, Quaternion.LookRotation(v), scale), c);
                }
                _batch.End();
            }
        }
    }
}
