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

        public static Font Font { get; private set; }
        public static Sprite RoundedSprite { get; private set; }
        public static Sprite CircleSprite { get; private set; }

        static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            Toon = LoadMaterial("NR_Toon", "NumberRoad/Toon");
            Transparent = LoadMaterial("NR_Transparent", "NumberRoad/Transparent");
            Additive = LoadMaterial("NR_Additive", "NumberRoad/Additive");
            Water = LoadMaterial("NR_Water", "NumberRoad/Water");

            Shadow = new Material(Transparent) { name = "NR_Shadow", mainTexture = CreateBlobTexture(64) };
            Shadow.SetColor("_Color", new Color(0f, 0f, 0f, 0.32f));
            Shadow.renderQueue = 2900;
            Panel = new Material(Transparent) { name = "NR_Panel", mainTexture = CreatePanelTexture(128, 64) };

            Quad = CreateQuad(false);
            GroundQuad = CreateQuad(true);
            Cube = CreateCube();
            Bolt = CreateBolt();

            Font = Resources.Load<Font>("Fonts/UIFont");
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            RoundedSprite = CreateRoundedSprite(64, 24);
            CircleSprite = CreateRoundedSprite(64, 32);
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
