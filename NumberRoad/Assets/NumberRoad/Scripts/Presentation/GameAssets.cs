using System.Collections.Generic;
using UnityEngine;

namespace NumberRoad.Presentation
{
    /// <summary>
    /// Shared runtime assets. Materials come from Resources (so their shaders and
    /// instancing variants are guaranteed to be in every build); meshes, textures
    /// and UI sprites are generated here so the project needs no hand-made art
    /// besides the Blender models.
    /// </summary>
    public static class GameAssets
    {
        public static Material Toon { get; private set; }
        public static Material Transparent { get; private set; }
        public static Material Additive { get; private set; }
        public static Material Water { get; private set; }
        public static Material Shadow { get; private set; }
        public static Material Panel { get; private set; }

        public static Mesh Quad { get; private set; }
        public static Mesh GroundQuad { get; private set; }
        public static Mesh Cube { get; private set; }
        public static Mesh Bolt { get; private set; }
        public static Mesh Orb { get; private set; }

        public static Font Font { get; private set; }
        /// <summary>True when the bundled font (Resources/Fonts/UIFont, already bold) is used.</summary>
        public static bool FontIsBold { get; private set; }
        public static Sprite RoundedSprite { get; private set; }
        public static Sprite CircleSprite { get; private set; }
        public static Sprite StarSprite { get; private set; }
        public static Sprite GemSprite { get; private set; }

        static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            Toon = LoadMaterial("NR_Toon", "NumberRoad/Toon");
            Transparent = LoadMaterial("NR_Transparent", "NumberRoad/Transparent");
            Additive = LoadMaterial("NR_Additive", "NumberRoad/Additive");
            // a runtime copy: regions recolour the water, and in the editor that would be saved into the asset
            Water = new Material(LoadMaterial("NR_Water", "NumberRoad/Water")) { name = "NR_Water (runtime)" };

            Shadow = new Material(Transparent) { name = "NR_Shadow", mainTexture = CreateBlobTexture(64) };
            Shadow.SetColor("_Color", new Color(0f, 0f, 0f, 0.32f));
            Shadow.renderQueue = 2900;
            Panel = new Material(Transparent) { name = "NR_Panel", mainTexture = CreatePanelTexture(128, 64) };

            Quad = CreateQuad(false);
            GroundQuad = CreateQuad(true);
            Cube = CreateCube();
            Bolt = CreateBolt();
            Orb = CreateOrb();

            Font = Resources.Load<Font>("Fonts/UIFont");
            FontIsBold = Font != null;
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RoundedSprite = CreateRoundedSprite(64, 24);
            CircleSprite = CreateRoundedSprite(64, 32);
            StarSprite = CreatePolygonSprite("NR_Star", 96, 5, 0.42f);
            GemSprite = CreatePolygonSprite("NR_Gem", 64, 4, 1f);
        }

        static Material LoadMaterial(string resource, string shaderName)
        {
            var material = Resources.Load<Material>("Materials/" + resource);
            if (material != null) return material;

            Debug.LogWarning($"[NumberRoad] Material '{resource}' missing - run 'Number Road > Setup Project'. Using a runtime copy.");
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            return new Material(shader) { name = resource, enableInstancing = true };
        }

        // --- Meshes ---------------------------------------------------------------

        static Mesh CreateQuad(bool ground)
        {
            var mesh = new Mesh { name = ground ? "NR_GroundQuad" : "NR_Quad" };
            if (ground)
            {
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
                };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            }
            else
            {
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
                };
                mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            }
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.colors32 = new[] { White, White, White, White };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static readonly Color32 White = new Color32(255, 255, 255, 255);

        static Mesh CreateCube()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var colors = new Color32[24];
            var triangles = new int[36];
            Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faceNormals[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                for (int c = 0; c < 4; c++)
                {
                    float su = (c & 1) == 0 ? -0.5f : 0.5f;
                    float sv = (c & 2) == 0 ? -0.5f : 0.5f;
                    int index = f * 4 + c;
                    vertices[index] = n * 0.5f + u * su + v * sv;
                    normals[index] = n;
                    uvs[index] = new Vector2(su + 0.5f, sv + 0.5f);
                    colors[index] = White;
                }
                int t = f * 6, b = f * 4;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b + 2; triangles[t + 4] = b + 1; triangles[t + 5] = b + 3;
            }
            var mesh = new Mesh
            {
                name = "NR_Cube",
                vertices = vertices,
                normals = normals,
                uv = uvs,
                colors32 = colors,
                triangles = triangles,
            };
            FixWinding(mesh);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flips any triangle whose winding disagrees with its normal.</summary>
        static void FixWinding(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            Vector3[] n = mesh.normals;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]]) < 0f)
                {
                    (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                }
            }
            mesh.triangles = t;
        }

        /// <summary>Elongated octahedron along +Z, length 1: reads as a glowing arrow / streak.</summary>
        static Mesh CreateBolt()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.15f), new Vector3(0f, 0.5f, 0.15f),
                new Vector3(-0.5f, 0f, 0.15f), new Vector3(0f, -0.5f, 0.15f),
            };
            var triangles = new[]
            {
                0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2,
                1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5,
            };
            var uvs = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(0.5f, 0.5f);
                colors[i] = White;
            }
            var mesh = new Mesh { name = "NR_Bolt", vertices = vertices, triangles = triangles, uv = uvs, colors32 = colors };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Low-poly ball (octahedron subdivided once) for orbs, cannon balls and fireballs.</summary>
        static Mesh CreateOrb()
        {
            var verts = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            var tris = new List<int> { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var mids = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mids.TryGetValue(key, out int i)) return i;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
                mids[key] = verts.Count - 1;
                return verts.Count - 1;
            }
            var next = new List<int>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var colors = new Color32[verts.Count];
            var uvs = new Vector2[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                verts[i] *= 0.5f;
                colors[i] = White;
                uvs[i] = new Vector2(0.5f, 0.5f);
            }
            var mesh = new Mesh { name = "NR_Orb" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(next, 0);
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // --- Textures / sprites ------------------------------------------------------

        static Texture2D CreateBlobTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "NR_Blob", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>Translucent fill with an opaque rounded border, tinted per gate.</summary>
        static Texture2D CreatePanelTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "NR_Panel", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            const float radius = 12f;
            const float border = 5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, width, height, radius);
                    float outside = Mathf.Clamp01(d + 0.5f);
                    float inBorder = Mathf.Clamp01(d + border + 0.5f);
                    float alpha = Mathf.Lerp(0.58f, 1f, inBorder) * (1f - outside);
                    byte shade = (byte)Mathf.Lerp(235f, 255f, inBorder);
                    pixels[y * width + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        static Sprite CreateRoundedSprite(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "NR_Rounded", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float b = Mathf.Min(radius + 2f, size / 2f - 1f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        /// <summary>
        /// A star (inner radius &lt; 1) or a regular polygon (inner = 1, e.g. the 4-sided gem), point up,
        /// anti-aliased by 4x4 supersampling.
        /// </summary>
        static Sprite CreatePolygonSprite(string name, int size, int points, float inner)
        {
            int corners = inner < 1f ? points * 2 : points;
            var poly = new Vector2[corners];
            for (int i = 0; i < corners; i++)
            {
                float r = inner < 1f && i % 2 == 1 ? inner : 1f;
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / corners;
                poly[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * 0.48f) + new Vector2(0.5f, 0.5f);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            var p = new Vector2((x + (sx + 0.5f) / 4f) / size, (y + (sy + 0.5f) / 4f) / size);
                            if (Inside(poly, p)) inside++;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 16));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Signed distance (pixels) to a rounded rectangle filling the texture; negative inside.</summary>
        static float RoundedRectDistance(float x, float y, float width, float height, float radius)
        {
            float hx = width * 0.5f, hy = height * 0.5f;
            float qx = Mathf.Abs(x - hx) - (hx - radius);
            float qy = Mathf.Abs(y - hy) - (hy - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
