using System.Collections.Generic;
using NumberRoad.Core;
using UnityEngine;

namespace NumberRoad.Presentation
{
    /// <summary>
    /// The scenery: an endless stone walkway with parapets over animated water,
    /// towers, banners and rocks, plus the enemy castle that closes each level.
    /// Road segments are recycled as the squad advances.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public static readonly Color SkyColor = new Color(0.56f, 0.82f, 1f);
        const float SegmentLength = 10f;
        const int SegmentCount = 12;
        const float BehindDistance = 25f;
        const float WaterLevel = -1.6f;
        const float WaterCell = 2f;

        sealed class Segment
        {
            public GameObject Root;
            public Renderer[] Stone;
            public GameObject TowerBlue;
            public GameObject TowerRed;
            public GameObject BannerLeft;
            public GameObject BannerRight;
            public Transform[] Rocks;
            public int Index = int.MinValue;
        }

        readonly List<Segment> _segments = new List<Segment>();
        // created in Init: Unity does not allow property blocks in MonoBehaviour field initializers
        MaterialPropertyBlock _roadBlock;
        MaterialPropertyBlock _wallBlock;
        Transform _water;
        GameObject _castle;
        float _levelLength = 200f;

        public Biome Biome { get; private set; } = Biomes.All[0];
        public Color Sky => Biome.Sky;

        public void Init()
        {
            _roadBlock = new MaterialPropertyBlock();
            _wallBlock = new MaterialPropertyBlock();
            for (int i = 0; i < SegmentCount; i++) _segments.Add(CreateSegment(i));
            _water = CreateWater().transform;
            _castle = ModelLibrary.Spawn("castle_gate", transform, Vector3.zero);
            _castle.name = "EnemyCastle";
            ApplyBiome(Biomes.All[0]);
        }

        /// <summary>Region palette: light, ambient, fog/sky, water and stone tints (shaders read the globals).</summary>
        public void ApplyBiome(Biome biome)
        {
            Biome = biome;
            Shader.SetGlobalVector("_NR_LightDir", new Vector3(-0.35f, 0.85f, -0.4f).normalized);
            Shader.SetGlobalColor("_NR_LightColor", biome.Light);
            Shader.SetGlobalColor("_NR_SkyAmbient", biome.SkyAmbient);
            Shader.SetGlobalColor("_NR_GroundAmbient", biome.GroundAmbient);
            Shader.SetGlobalColor("_NR_FogColor", biome.Sky);
            const float fogStart = 40f, fogEnd = 100f;
            Shader.SetGlobalVector("_NR_FogParams", new Vector4(fogStart, 1f / (fogEnd - fogStart), 0f, 0f));
            Material water = GameAssets.Water;
            water.SetColor("_Deep", biome.WaterDeep);
            water.SetColor("_Shallow", biome.WaterShallow);
            water.SetColor("_Highlight", biome.WaterHighlight);
            _roadBlock.SetColor("_Color", biome.Road);
            _wallBlock.SetColor("_Color", biome.Wall);
            foreach (Segment s in _segments)
            {
                for (int i = 0; i < s.Stone.Length; i++) s.Stone[i].SetPropertyBlock(i == 0 ? _roadBlock : _wallBlock);
            }
        }

        /// <summary>Prepares the scenery for a new level (castle position, segment reset).</summary>
        public void Build(LevelDefinition def)
        {
            ApplyBiome(Biomes.All[def.biome % Biomes.All.Length]);
            _levelLength = def.length;
            _castle.transform.localPosition = new Vector3(0f, 0f, def.length + Balance.BossArenaDistance + 9f);
            foreach (Segment s in _segments) s.Index = int.MinValue;
            Tick(0f);
        }

        public void Tick(float focusZ)
        {
            int first = Mathf.FloorToInt((focusZ - BehindDistance) / SegmentLength);
            int last = first + SegmentCount - 1;
            int castleIndex = Mathf.FloorToInt((_levelLength + Balance.BossArenaDistance + 12f) / SegmentLength);

            // keep segments already in range, recycle the others
            var wanted = new HashSet<int>();
            for (int i = first; i <= last; i++) wanted.Add(i);
            var free = new List<Segment>();
            foreach (Segment s in _segments)
            {
                if (wanted.Contains(s.Index)) wanted.Remove(s.Index);
                else free.Add(s);
            }
            int f = 0;
            foreach (int index in wanted)
            {
                if (f >= free.Count) break;
                Place(free[f++], index, castleIndex);
            }

            float waterZ = Mathf.Round(focusZ / WaterCell) * WaterCell;
            _water.localPosition = new Vector3(0f, WaterLevel, waterZ + 40f);
        }

        Segment CreateSegment(int i)
        {
            var root = new GameObject("Segment");
            root.transform.SetParent(transform, false);
            var s = new Segment { Root = root };
            GameObject road = ModelLibrary.Spawn("road_tile", root.transform, Vector3.zero);
            GameObject wallL = ModelLibrary.Spawn("wall", root.transform, new Vector3(-4.25f, 0f, 0f));
            GameObject wallR = ModelLibrary.Spawn("wall", root.transform, new Vector3(4.25f, 0f, 0f));
            s.Stone = new[]
            {
                road.GetComponentInChildren<Renderer>(),
                wallL.GetComponentInChildren<Renderer>(),
                wallR.GetComponentInChildren<Renderer>(),
            };
            s.TowerBlue = ModelLibrary.Spawn("tower_blue", root.transform, Vector3.zero);
            s.TowerRed = ModelLibrary.Spawn("tower_red", root.transform, Vector3.zero);
            s.BannerLeft = ModelLibrary.Spawn("banner", root.transform, new Vector3(-4.25f, 0.67f, -2.5f), 0.9f);
            s.BannerRight = ModelLibrary.Spawn("banner", root.transform, new Vector3(4.25f, 0.67f, 2.5f), 0.9f);
            s.Rocks = new Transform[2];
            for (int r = 0; r < s.Rocks.Length; r++)
            {
                s.Rocks[r] = ModelLibrary.Spawn("rock", root.transform, Vector3.zero).transform;
            }
            return s;
        }

        void Place(Segment s, int index, int castleIndex)
        {
            s.Index = index;
            s.Root.transform.localPosition = new Vector3(0f, 0f, index * SegmentLength + SegmentLength * 0.5f);
            bool beyondCastle = index > castleIndex;
            s.Root.SetActive(!beyondCastle);
            if (beyondCastle) return;

            uint h = Hash(index);
            bool tower = index % 4 == 2;
            float side = (h & 1u) == 0 ? -1f : 1f;
            bool enemySide = index * SegmentLength > _levelLength * 0.55f;
            s.TowerBlue.SetActive(tower && !enemySide);
            s.TowerRed.SetActive(tower && enemySide);
            GameObject towerGo = enemySide ? s.TowerRed : s.TowerBlue;
            towerGo.transform.localPosition = new Vector3(side * 7.4f, 0f, ((h >> 3) % 5) - 2f);
            towerGo.transform.localRotation = Quaternion.Euler(0f, (h >> 7) % 360, 0f);

            bool banners = index % 2 == 0 && !enemySide;
            s.BannerLeft.SetActive(banners);
            s.BannerRight.SetActive(banners);

            for (int r = 0; r < s.Rocks.Length; r++)
            {
                uint rh = Hash(index * 31 + r * 7 + 3);
                bool show = rh % 3 != 0;
                s.Rocks[r].gameObject.SetActive(show);
                if (!show) continue;
                float rs = (rh & 2u) == 0 ? -1f : 1f;
                float x = rs * (6.2f + (rh >> 4) % 9);
                float z = ((rh >> 9) % 10) - 5f;
                float scale = 0.5f + ((rh >> 13) % 10) * 0.09f;
                s.Rocks[r].localPosition = new Vector3(x, WaterLevel + 0.1f, z);
                s.Rocks[r].localRotation = Quaternion.Euler(0f, (rh >> 17) % 360, 0f);
                s.Rocks[r].localScale = Vector3.one * scale;
            }
        }

        static uint Hash(int value)
        {
            uint x = unchecked((uint)value * 0x9E3779B1u + 0x7F4A7C15u);
            x ^= x >> 15;
            x *= 0x2C1B3C6Du;
            x ^= x >> 12;
            x *= 0x297A2D39u;
            x ^= x >> 15;
            return x;
        }

        GameObject CreateWater()
        {
            const int cellsX = 36, cellsZ = 70;
            float width = cellsX * WaterCell, length = cellsZ * WaterCell;
            var vertices = new Vector3[(cellsX + 1) * (cellsZ + 1)];
            for (int z = 0; z <= cellsZ; z++)
            {
                for (int x = 0; x <= cellsX; x++)
                {
                    vertices[z * (cellsX + 1) + x] = new Vector3(x * WaterCell - width * 0.5f, 0f, z * WaterCell - length * 0.5f);
                }
            }
            var triangles = new int[cellsX * cellsZ * 6];
            int t = 0;
            for (int z = 0; z < cellsZ; z++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    int a = z * (cellsX + 1) + x;
                    int b = a + cellsX + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
                }
            }
            var mesh = new Mesh { name = "NR_Water" };
            mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(width, 2f, length));

            var go = new GameObject("Water");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GameAssets.Water;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
