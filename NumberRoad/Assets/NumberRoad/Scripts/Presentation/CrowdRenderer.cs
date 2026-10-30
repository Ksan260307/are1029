using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NumberRoad.Presentation
{
    public enum PartRole
    {
        Body,
        LegL,
        LegR,
        ArmL,
        ArmR,
        Other,
    }

    public struct CrowdPart
    {
        public Mesh Mesh;
        public Matrix4x4 Rest;
        public PartRole Role;
    }

    /// <summary>A Blender model split into its animated parts, ready for instanced drawing.</summary>
    public sealed class CrowdModel
    {
        public readonly string Name;
        public readonly CrowdPart[] Parts;
        public readonly float Height;
        public readonly Material Material;
        public readonly bool HasArmR;
        internal readonly int Id;

        internal CrowdModel(int id, string name, CrowdPart[] parts, float height, Material material)
        {
            Id = id;
            Name = name;
            Parts = parts;
            Height = height;
            Material = material;
            foreach (CrowdPart p in parts) HasArmR |= p.Role == PartRole.ArmR;
        }
    }

    /// <summary>Loads models from Resources/Models once and caches their meshes and part transforms.</summary>
    public static class CrowdLibrary
    {
        static readonly Dictionary<(string, Material), CrowdModel> Cache = new Dictionary<(string, Material), CrowdModel>();
        static int _nextId;

        public static CrowdModel Get(string name, Material material = null)
        {
            material = material != null ? material : GameAssets.Toon;
            (string, Material) key = (name, material);
            if (Cache.TryGetValue(key, out CrowdModel model)) return model;

            var parts = new List<CrowdPart>();
            float height = 1f;
            var prefab = Resources.Load<GameObject>("Models/" + name);
            if (prefab != null)
            {
                Transform root = prefab.transform;
                Matrix4x4 toRoot = root.worldToLocalMatrix;
                float top = 0f;
                foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Matrix4x4 rest = toRoot * mf.transform.localToWorldMatrix;
                    parts.Add(new CrowdPart { Mesh = mf.sharedMesh, Rest = rest, Role = RoleOf(mf.name) });
                    Bounds b = mf.sharedMesh.bounds;
                    top = Mathf.Max(top, rest.MultiplyPoint3x4(b.center + Vector3.up * b.extents.y).y);
                }
                height = Mathf.Max(0.3f, top);
            }
            if (parts.Count == 0)
            {
                Debug.LogWarning($"[NumberRoad] Crowd model '{name}' missing - using a box.");
                parts.Add(new CrowdPart { Mesh = GameAssets.Cube, Rest = Matrix4x4.TRS(new Vector3(0, 0.5f, 0), Quaternion.identity, new Vector3(0.45f, 1f, 0.35f)), Role = PartRole.Body });
            }

            model = new CrowdModel(_nextId++, name, parts.ToArray(), height, material);
            Cache[key] = model;
            return model;
        }

        static PartRole RoleOf(string objectName)
        {
            if (objectName.EndsWith("_LegL")) return PartRole.LegL;
            if (objectName.EndsWith("_LegR")) return PartRole.LegR;
            if (objectName.EndsWith("_ArmL")) return PartRole.ArmL;
            if (objectName.EndsWith("_ArmR")) return PartRole.ArmR;
            if (objectName.EndsWith("_Body") || objectName.EndsWith("_Main")) return PartRole.Body;
            return PartRole.Other;
        }
    }

    /// <summary>Animation state of one drawn character.</summary>
    public struct CrowdPose
    {
        /// <summary>Walk cycle in radians.</summary>
        public float Phase;
        /// <summary>Leg swing in degrees (0 = standing).</summary>
        public float Stride;
        public float Bob;
        /// <summary>Weapon arm angle in degrees (negative raises it overhead).</summary>
        public float Swing;
        /// <summary>Wing flap amplitude in degrees (bats, dragons). Zero for walkers.</summary>
        public float Flap;

        public static CrowdPose Walk(float phase, float stride, float bob) => new CrowdPose { Phase = phase, Stride = stride, Bob = bob };
    }

    /// <summary>
    /// Draws thousands of animated characters per frame with GPU instancing. Every (model, part) pair is a
    /// bucket; instances are appended into pooled chunks of 1023 that each own their arrays and property
    /// block, so nothing is overwritten before the GPU sees it.
    /// </summary>
    public sealed class CrowdRenderer
    {
        const int Max = 1023;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        sealed class Chunk
        {
            public readonly Matrix4x4[] Matrices = new Matrix4x4[Max];
            public readonly Vector4[] Colors = new Vector4[Max];
            public readonly float[] Flash = new float[Max];
            public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
            public int Count;
        }

        sealed class Bucket
        {
            public Mesh Mesh;
            public Material Material;
            public Chunk Current;
            public bool Active;
        }

        readonly List<Chunk> _pool = new List<Chunk>();
        readonly Dictionary<int, Bucket[]> _buckets = new Dictionary<int, Bucket[]>();
        readonly List<Bucket> _active = new List<Bucket>();
        int _poolUsed;
        bool _linear;

        /// <summary>Slow device: characters lose their legs closer to the camera.</summary>
        public bool LowQuality;
        public Vector3 Eye;

        /// <summary>
        /// Beyond this distance from the camera characters are drawn without legs - too small to notice there.
        /// Only the picture changes with the device; the game itself never does.
        /// </summary>
        public float LegDistance => LowQuality ? 30f : 58f;
        public int InstancesThisFrame { get; private set; }

        public void Begin()
        {
            _poolUsed = 0;
            InstancesThisFrame = 0;
            _linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            foreach (Bucket b in _active)
            {
                b.Current = null;
                b.Active = false;
            }
            _active.Clear();
        }

        public void Add(CrowdModel model, in Matrix4x4 root, Color tint, float flash, in CrowdPose pose)
        {
            if (!_buckets.TryGetValue(model.Id, out Bucket[] buckets))
            {
                buckets = new Bucket[model.Parts.Length];
                for (int i = 0; i < buckets.Length; i++) buckets[i] = new Bucket { Mesh = model.Parts[i].Mesh, Material = model.Material };
                _buckets[model.Id] = buckets;
            }

            float legDistance = LegDistance;
            bool far = (root.GetColumn(3) - (Vector4)Eye).sqrMagnitude > legDistance * legDistance;
            Vector4 color = _linear ? (Vector4)tint.linear : (Vector4)tint;
            float s = Mathf.Sin(pose.Phase);

            for (int i = 0; i < model.Parts.Length; i++)
            {
                ref CrowdPart part = ref model.Parts[i];
                Matrix4x4 local;
                switch (part.Role)
                {
                    case PartRole.Body:
                        if (pose.Bob != 0f)
                        {
                            local = Matrix4x4.Translate(new Vector3(0f, Mathf.Abs(Mathf.Cos(pose.Phase)) * pose.Bob, 0f));
                            Append(buckets[i], root * local * part.Rest, color, flash);
                        }
                        else
                        {
                            Append(buckets[i], root * part.Rest, color, flash);
                        }
                        continue;
                    case PartRole.LegL:
                    case PartRole.LegR:
                        if (far) continue;
                        float leg = (part.Role == PartRole.LegL ? s : -s) * pose.Stride;
                        local = leg == 0f ? Matrix4x4.identity : Matrix4x4.Rotate(Quaternion.Euler(leg, 0f, 0f));
                        break;
                    case PartRole.ArmL:
                        if (pose.Flap != 0f) local = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, Mathf.Sin(pose.Phase * 2.2f) * pose.Flap));
                        else local = Matrix4x4.Rotate(Quaternion.Euler(-s * pose.Stride * 0.4f, 0f, 0f));
                        break;
                    case PartRole.ArmR:
                        if (pose.Flap != 0f) local = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -Mathf.Sin(pose.Phase * 2.2f) * pose.Flap));
                        else local = Matrix4x4.Rotate(Quaternion.Euler(pose.Swing != 0f ? pose.Swing : s * pose.Stride * 0.4f, 0f, 0f));
                        break;
                    default:
                        local = Matrix4x4.identity;
                        break;
                }
                Append(buckets[i], root * part.Rest * local, color, flash);
            }
        }

        void Append(Bucket bucket, in Matrix4x4 matrix, Vector4 color, float flash)
        {
            if (!bucket.Active)
            {
                bucket.Active = true;
                _active.Add(bucket);
            }
            Chunk chunk = bucket.Current;
            if (chunk == null || chunk.Count == Max)
            {
                if (chunk != null) Flush(bucket, chunk);
                chunk = Rent();
                bucket.Current = chunk;
            }
            int n = chunk.Count++;
            chunk.Matrices[n] = matrix;
            chunk.Colors[n] = color;
            chunk.Flash[n] = flash;
            InstancesThisFrame++;
        }

        Chunk Rent()
        {
            if (_poolUsed == _pool.Count) _pool.Add(new Chunk());
            Chunk c = _pool[_poolUsed++];
            c.Count = 0;
            return c;
        }

        void Flush(Bucket bucket, Chunk chunk)
        {
            if (chunk.Count == 0) return;
            chunk.Block.SetVectorArray(ColorId, chunk.Colors);
            chunk.Block.SetFloatArray(FlashId, chunk.Flash);
            var rp = new RenderParams(bucket.Material)
            {
                matProps = chunk.Block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };
            Graphics.RenderMeshInstanced(rp, bucket.Mesh, 0, chunk.Matrices, chunk.Count);
        }

        public void End()
        {
            foreach (Bucket b in _active)
            {
                if (b.Current != null) Flush(b, b.Current);
            }
        }
    }
}
