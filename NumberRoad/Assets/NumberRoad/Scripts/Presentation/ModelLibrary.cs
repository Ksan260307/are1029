using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NumberRoad.Presentation
{
    /// <summary>
    /// Instantiates the Blender models from Resources/Models. If a model is
    /// missing (e.g. the FBX files were not generated yet) a coloured box
    /// stand-in is built instead, so the game always runs.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        public static GameObject Spawn(string name, Transform parent, Vector3 localPosition, float scale = 1f)
        {
            GameObject prefab = Load(name);
            GameObject go = prefab != null ? Object.Instantiate(prefab, parent, false) : CreateFallback(name, parent);
            go.name = name;
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            PrepareRenderers(go);
            return go;
        }

        public static bool Has(string name) => Load(name) != null;

        static GameObject Load(string name)
        {
            if (!Prefabs.TryGetValue(name, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>("Models/" + name);
                Prefabs[name] = prefab;
                if (prefab == null)
                    Debug.LogWarning($"[NumberRoad] Model '{name}' not found in Resources/Models - using a stand-in. Run blender/build_models.py.");
            }
            return prefab;
        }

        static void PrepareRenderers(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = GameAssets.Toon;
                r.sharedMaterials = materials;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        // --- Stand-ins ----------------------------------------------------------------

        static GameObject CreateFallback(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            switch (name)
            {
                case "gate_pillar":
                    Box(root, name, new Vector3(0, 1.35f, 0), new Vector3(0.34f, 2.7f, 0.34f), new Color(0.95f, 0.94f, 0.9f));
                    break;
                case "gate_beam":
                    Box(root, name, Vector3.zero, new Vector3(1f, 0.26f, 0.26f), new Color(0.95f, 0.94f, 0.9f));
                    break;
                case "crate":
                    Box(root, name, new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 1.1f), new Color(0.7f, 0.48f, 0.27f));
                    break;
                case "road_tile":
                    Box(root, name, new Vector3(0, -0.3f, 0), new Vector3(8f, 0.6f, 10f), new Color(0.85f, 0.81f, 0.75f));
                    break;
                case "wall":
                    Box(root, name, new Vector3(0, -0.6f, 0), new Vector3(0.5f, 2.4f, 10f), new Color(0.8f, 0.76f, 0.7f));
                    break;
                case "war_wagon":
                    Box(root, name, new Vector3(0, 1.1f, 0), new Vector3(2.1f, 2.2f, 2.7f), new Color(0.69f, 0.48f, 0.27f));
                    break;
                case "castle_gate":
                    Box(root, name, new Vector3(0, 1.4f, 0), new Vector3(10f, 6.4f, 2.5f), new Color(0.55f, 0.5f, 0.47f));
                    break;
                default:
                    Box(root, name, new Vector3(0, 0.5f, 0), Vector3.one, new Color(0.6f, 0.6f, 0.6f));
                    break;
            }
            return root;
        }

        static void Box(GameObject root, string name, Vector3 position, Vector3 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = GameAssets.Cube;
            var renderer = go.AddComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Procedural animation for the multi-part Blender characters: legs swing at
    /// the hip, the club arm swings at the shoulder, the body bobs. Also owns the
    /// per-renderer tint / hit-flash property blocks.
    /// </summary>
    public sealed class CharacterRig
    {
        public readonly Transform Root;
        readonly Transform _body;
        readonly Transform _legL;
        readonly Transform _legR;
        readonly Transform _armL;
        readonly Transform _armR;
        readonly Vector3 _bodyBase;
        readonly Quaternion _legLBase, _legRBase, _armLBase, _armRBase;
        readonly Renderer[] _renderers;
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        Color _tint = Color.white;
        float _flash;
        bool _dirty = true;

        public CharacterRig(GameObject go)
        {
            Root = go.transform;
            _body = Find("_Body");
            _legL = Find("_LegL");
            _legR = Find("_LegR");
            _armL = Find("_ArmL");
            _armR = Find("_ArmR");
            if (_body != null) _bodyBase = _body.localPosition;
            _legLBase = _legL != null ? _legL.localRotation : Quaternion.identity;
            _legRBase = _legR != null ? _legR.localRotation : Quaternion.identity;
            _armLBase = _armL != null ? _armL.localRotation : Quaternion.identity;
            _armRBase = _armR != null ? _armR.localRotation : Quaternion.identity;
            _renderers = go.GetComponentsInChildren<Renderer>(true);
            Apply();
        }

        Transform Find(string suffix)
        {
            foreach (Transform t in Root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(suffix, System.StringComparison.Ordinal)) return t;
            }
            return null;
        }

        /// <summary>Walk cycle. <paramref name="stride"/> in degrees; 0 = standing.</summary>
        public void Walk(float phase, float stride, float bob)
        {
            float s = Mathf.Sin(phase);
            if (_legL != null) _legL.localRotation = _legLBase * Quaternion.Euler(s * stride, 0f, 0f);
            if (_legR != null) _legR.localRotation = _legRBase * Quaternion.Euler(-s * stride, 0f, 0f);
            if (_armL != null) _armL.localRotation = _armLBase * Quaternion.Euler(-s * stride * 0.4f, 0f, 0f);
            if (_body != null) _body.localPosition = _bodyBase + new Vector3(0f, Mathf.Abs(Mathf.Cos(phase)) * bob, 0f);
        }

        /// <summary>Club arm angle in degrees: negative raises it overhead, positive slams down.</summary>
        public void Swing(float degrees)
        {
            if (_armR != null) _armR.localRotation = _armRBase * Quaternion.Euler(degrees, 0f, 0f);
        }

        public void SetTint(Color tint)
        {
            if (_tint == tint) return;
            _tint = tint;
            _dirty = true;
        }

        public void SetFlash(float flash)
        {
            flash = Mathf.Clamp01(flash);
            if (_flash == flash) return;
            _flash = flash;
            _dirty = true;
        }

        public void Apply()
        {
            if (!_dirty) return;
            _dirty = false;
            _block.SetColor("_Color", _tint);
            _block.SetFloat("_Flash", _flash);
            foreach (Renderer r in _renderers)
            {
                if (r != null) r.SetPropertyBlock(_block);
            }
        }
    }
}
