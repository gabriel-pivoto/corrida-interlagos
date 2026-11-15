using System.Collections.Generic;
using UnityEngine;

namespace Corrida
{
    /// <summary>Compatibilidade entre Unity 2022 e Unity 6 (nomes de API do Rigidbody mudaram).</summary>
    public static class RbCompat
    {
#if UNITY_6000_0_OR_NEWER
        public static Vector3 GetVel(this Rigidbody r) => r.linearVelocity;
        public static void SetVel(this Rigidbody r, Vector3 v) => r.linearVelocity = v;
        public static void SetDamping(this Rigidbody r, float lin, float ang) { r.linearDamping = lin; r.angularDamping = ang; }
#else
        public static Vector3 GetVel(this Rigidbody r) => r.velocity;
        public static void SetVel(this Rigidbody r, Vector3 v) => r.velocity = v;
        public static void SetDamping(this Rigidbody r, float lin, float ang) { r.drag = lin; r.angularDrag = ang; }
#endif
    }

    public static class PhysMat
    {
#if UNITY_6000_0_OR_NEWER
        static PhysicsMaterial slip;
        public static void Slippery(Collider c)
        {
            if (slip == null)
            {
                slip = new PhysicsMaterial("Liso")
                {
                    dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.05f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }
            c.sharedMaterial = slip;
        }
#else
        static PhysicMaterial slip;
        public static void Slippery(Collider c)
        {
            if (slip == null)
            {
                slip = new PhysicMaterial("Liso")
                {
                    dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0.05f,
                    frictionCombine = PhysicMaterialCombine.Minimum,
                    bounceCombine = PhysicMaterialCombine.Minimum
                };
            }
            c.sharedMaterial = slip;
        }
#endif
    }

    /// <summary>Marca quais colliders são grama (carro perde velocidade e aderência).</summary>
    public static class TrackSurfaces
    {
        static readonly HashSet<Collider> grass = new HashSet<Collider>();
        public static void RegisterGrass(Collider c) { if (c != null) grass.Add(c); }
        public static bool IsGrass(Collider c) => c != null && grass.Contains(c);
    }

    public static class Mats
    {
        static Shader baseShader;
        static Font font;
        static readonly Dictionary<string, Material> lib = new Dictionary<string, Material>();

        /// <summary>Shader do material padrão de um primitivo: funciona no editor e em build.</summary>
        public static Shader BaseShader
        {
            get
            {
                if (baseShader == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseShader = go.GetComponent<Renderer>().sharedMaterial.shader;
                    Object.DestroyImmediate(go);
                }
                return baseShader;
            }
        }

        public static Material Make(Color c, Texture tex = null, float smooth = 0.15f, Vector2? tiling = null)
        {
            var m = new Material(BaseShader);
            m.color = c;
            if (tex != null) m.mainTexture = tex;
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Material da biblioteca (Assets/Resources/Materiais), criado pelo editor com texturas e normal maps.</summary>
        public static Material Lib(string name, Color fallback)
        {
            if (!lib.TryGetValue(name, out var m) || m == null)
            {
                m = Resources.Load<Material>("Materiais/" + name);
                if (m == null) m = Make(fallback);
                lib[name] = m;
            }
            return m;
        }

        /// <summary>Cópia de um material da biblioteca com outra cor.</summary>
        public static Material Tinted(string name, Color c)
        {
            var m = new Material(Lib(name, c));
            m.color = c;
            return m;
        }

        public static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material m, Quaternion? localRot = null, int layer = 0)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.layer = layer;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot ?? Quaternion.identity;
            g.transform.localScale = localScale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        /// <summary>Caixa com UV em metros em todas as faces (textura não estica).</summary>
        public static GameObject Box(Transform parent, Vector3 center, Vector3 size, Quaternion rot, Material m, string name = "Caixa")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static readonly Dictionary<Vector3, Mesh> boxCache = new Dictionary<Vector3, Mesh>();

        static Mesh BoxMesh(Vector3 s)
        {
            if (boxCache.TryGetValue(s, out var cached)) return cached;
            Vector3 h = s * 0.5f;
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 normal, Vector3 U, Vector3 V, float hn, float hu, float hv)
            {
                Vector3 c = normal * hn;
                int b = v.Count;
                v.Add(c - U * hu - V * hv); v.Add(c + U * hu - V * hv); v.Add(c - U * hu + V * hv); v.Add(c + U * hu + V * hv);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(2f * hu, 0f)); uv.Add(new Vector2(0f, 2f * hv)); uv.Add(new Vector2(2f * hu, 2f * hv));
                for (int i = 0; i < 4; i++) nn.Add(normal);
                if (Vector3.Dot(Vector3.Cross(V, U), normal) > 0f) tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                else tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            Face(Vector3.right, Vector3.forward, Vector3.up, h.x, h.z, h.y);
            Face(Vector3.left, Vector3.back, Vector3.up, h.x, h.z, h.y);
            Face(Vector3.forward, Vector3.left, Vector3.up, h.z, h.x, h.y);
            Face(Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z);
            Face(Vector3.down, Vector3.right, Vector3.back, h.y, h.x, h.z);
            var mesh = new Mesh { name = "Caixa" };
            mesh.SetVertices(v);
            mesh.SetNormals(nn);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            boxCache[s] = mesh;
            return mesh;
        }

        public static Font GetFont()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }

        /// <summary>Texto 3D. Fica legível para quem olha na direção do "forward" da rotação.</summary>
        public static GameObject Text3D(Transform parent, string text, Vector3 pos, Quaternion rot, float size, Color c, bool local = false, int layer = 0)
        {
            var go = new GameObject("Texto_" + text);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            if (local) { go.transform.localPosition = pos; go.transform.localRotation = rot; }
            else go.transform.SetPositionAndRotation(pos, rot);
            var tm = go.AddComponent<TextMesh>();
            tm.font = GetFont();
            tm.text = text;
            tm.fontSize = 80;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = c;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial;
            return go;
        }

        static Material textMat;

        /// <summary>Material de texto com teste de profundidade (shader Corrida/Texto3D).</summary>
        static Material TextMaterial
        {
            get
            {
                if (textMat == null)
                {
                    var f = GetFont();
                    var lib = Resources.Load<Material>("Materiais/Texto");
                    if (lib == null) return f.material;
                    textMat = new Material(lib);
                    textMat.mainTexture = f.material.mainTexture;
                    Font.textureRebuilt += rebuilt =>
                    {
                        if (rebuilt == font && textMat != null) textMat.mainTexture = font.material.mainTexture;
                    };
                }
                return textMat;
            }
        }
    }

    /// <summary>Texturas geradas por código (usadas pelo editor para criar os PNGs e como reserva em tempo de execução).</summary>
    public static class Tex
    {
        static Texture2D New(int w, int h, bool point = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = point ? FilterMode.Point : FilterMode.Trilinear;
            t.anisoLevel = 8;
            return t;
        }

        static Texture2D Finish(Texture2D t, Color[] px)
        {
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static float Hash(int x, int y, int s = 0)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        public static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        /// <summary>Zebra: 2 blocos (vermelho/branco) ao longo de V, com sujeira e borda.</summary>
        public static Texture2D Zebra()
        {
            const int w = 64, h = 256;
            var t = New(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool red = y < h / 2;
                    Color c = red ? new Color(0.82f, 0.07f, 0.06f) : new Color(0.93f, 0.93f, 0.9f);
                    float n = Mathf.PerlinNoise(x * 0.15f, y * 0.06f);
                    c *= 0.85f + 0.15f * n;
                    float edge = Mathf.Min(x, w - 1 - x) / (float)w;
                    if (edge < 0.04f) c *= 0.75f;
                    if ((y % (h / 2)) < 2) c *= 0.7f;
                    // marcas de pneu
                    float tire = Mathf.PerlinNoise(x * 0.05f + 10f, y * 0.01f);
                    if (tire > 0.62f) c = Color.Lerp(c, new Color(0.1f, 0.1f, 0.1f), (tire - 0.62f) * 2f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        public static Texture2D Checker()
        {
            var t = New(16, 16, true);
            var px = new Color[16 * 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    px[y * 16 + x] = ((x / 8 + y / 8) % 2 == 0) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.06f, 0.06f, 0.06f);
            return Finish(t, px);
        }

        static readonly Color[] Brand =
        {
            new Color(0.0f, 0.55f, 0.25f), new Color(1f, 0.8f, 0.0f), new Color(0.05f, 0.2f, 0.65f), new Color(0.85f, 0.1f, 0.1f),
            new Color(0.95f, 0.45f, 0.05f), new Color(0.1f, 0.1f, 0.12f), new Color(0.55f, 0.1f, 0.6f), new Color(0.0f, 0.65f, 0.75f)
        };

        /// <summary>Painéis de publicidade (sem marcas): U = altura (0 topo .. 1 base, 0,8 m), V = comprimento (48 m, 8 painéis de 6 m).</summary>
        public static Texture2D Paineis()
        {
            const int w = 64, h = 1024;
            var t = New(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int panel = y / 128;
                    float py = (y % 128) / 128f;          // 0..1 ao longo do painel (6 m)
                    float um = x / (float)w * 0.8f;       // metros na altura
                    float vm = py * 6f;                   // metros no comprimento
                    Color bg = Brand[panel % Brand.Length];
                    Color fg = panel % 2 == 0 ? Color.white : Brand[(panel + 3) % Brand.Length];
                    Color c = bg;
                    switch (panel % 4)
                    {
                        case 0: if (um > 0.3f && um < 0.5f && vm > 0.6f && vm < 5.4f) c = fg; break;
                        case 1: if ((new Vector2(um - 0.4f, vm - 3f)).magnitude < 0.28f) c = fg; if (vm > 3.6f && vm < 5.5f && um > 0.3f && um < 0.5f) c = fg; break;
                        case 2: if (Mathf.Repeat(vm + um * 1.5f, 1.2f) < 0.35f) c = fg; break;
                        case 3: if (vm < 2.2f) c = fg; if (vm > 2.6f && vm < 5.5f && Mathf.Abs(um - 0.4f) < 0.06f) c = Color.white; break;
                    }
                    if (py < 0.01f || py > 0.99f || x < 2 || x > w - 3) c = new Color(0.15f, 0.15f, 0.15f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Barreira de pneus com cinta colorida: U = altura (0,8 m), V = 4 m.</summary>
        public static Texture2D Pneus()
        {
            const int w = 128, h = 512;
            var t = New(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float um = x / (float)w * 0.8f, vm = y / (float)h * 4f;
                    float cu = um < 0.4f ? 0.2f : 0.6f;
                    int col = Mathf.FloorToInt(vm / 0.5f);
                    float cv = col * 0.5f + 0.25f;
                    float r = new Vector2(um - cu, vm - cv).magnitude;
                    Color c = new Color(0.12f, 0.12f, 0.12f);
                    if (r < 0.21f)
                    {
                        float g = 0.07f + 0.05f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                        c = new Color(g, g, g);
                        if (r > 0.19f) c *= 0.6f;
                        if (r < 0.1f) c = new Color(0.03f, 0.03f, 0.03f);
                    }
                    if (um > 0.35f && um < 0.45f) c = (col % 2 == 0) ? new Color(0.85f, 0.1f, 0.08f) : new Color(0.92f, 0.92f, 0.92f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Torcida: U = 8 m ao longo, V = 1 m de altura (uma fileira).</summary>
        public static Texture2D Torcida()
        {
            const int w = 256, h = 32;
            var t = New(w, h);
            var px = new Color[w * h];
            Color[] shirts = { new Color(1f, 0.85f, 0.05f), new Color(0.05f, 0.55f, 0.2f), new Color(0.1f, 0.25f, 0.75f), Color.white, new Color(0.85f, 0.1f, 0.1f), new Color(0.2f, 0.2f, 0.2f), new Color(1f, 0.5f, 0.1f) };
            Color[] skin = { new Color(0.95f, 0.78f, 0.62f), new Color(0.78f, 0.56f, 0.4f), new Color(0.55f, 0.36f, 0.24f), new Color(0.36f, 0.24f, 0.16f) };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int person = x / 8;
                    float lx = (x % 8) / 8f, ly = y / (float)h;
                    bool present = Hash(person, 7) > 0.12f;
                    Color c = new Color(0.32f, 0.33f, 0.36f); // assento
                    if (ly < 0.12f) c = new Color(0.55f, 0.55f, 0.57f); // degrau
                    if (present)
                    {
                        Color shirt = shirts[(int)(Hash(person, 1) * shirts.Length) % shirts.Length];
                        Color sk = skin[(int)(Hash(person, 2) * skin.Length) % skin.Length];
                        if (ly > 0.15f && ly < 0.62f && lx > 0.1f && lx < 0.9f) c = shirt;
                        if ((new Vector2(lx - 0.5f, (ly - 0.75f) * 1.6f)).magnitude < 0.28f) c = sk;
                        if (ly > 0.84f && ly < 0.93f && lx > 0.25f && lx < 0.75f && Hash(person, 3) > 0.5f) c = new Color(0.12f, 0.08f, 0.05f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Portões dos boxes: U = 32 m (4 garagens), V = 5 m.</summary>
        public static Texture2D Garagens()
        {
            const int w = 512, h = 160;
            var t = New(w, h);
            var px = new Color[w * h];
            Color[] team = { new Color(0.85f, 0.1f, 0.1f), new Color(0.1f, 0.3f, 0.85f), new Color(1f, 0.8f, 0.05f), new Color(0.05f, 0.55f, 0.25f) };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float um = x / (float)w * 32f, vm = y / (float)h * 5f;
                    int door = Mathf.FloorToInt(um / 8f);
                    float lu = um - door * 8f;
                    Color c = new Color(0.86f, 0.86f, 0.84f); // pilares
                    if (lu > 0.5f && lu < 7.5f && vm < 3.9f)
                    {
                        float g = 0.42f + 0.06f * Mathf.Sin(vm * Mathf.PI * 2f / 0.15f);
                        c = new Color(g, g, g * 1.05f);
                    }
                    if (vm > 4.15f && vm < 4.75f && lu > 0.5f && lu < 7.5f) c = team[door % team.Length];
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        /// <summary>Fachada de prédio: uma janela por bloco de 4 m x 3 m.</summary>
        public static Texture2D Predio()
        {
            const int w = 64, h = 48;
            var t = New(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float n = 0.9f + 0.1f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    Color c = new Color(0.9f, 0.9f, 0.88f) * n;
                    if (u > 0.12f && u < 0.88f && v > 0.25f && v < 0.85f)
                    {
                        float g = Mathf.Lerp(0.12f, 0.3f, v);
                        c = new Color(g * 0.8f, g, g * 1.35f);
                        if (u > 0.48f && u < 0.52f) c = new Color(0.5f, 0.5f, 0.5f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            return Finish(t, px);
        }

        public static Texture2D Carbono()
        {
            const int s = 64;
            var t = New(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool a = ((x / 4 + y / 4) % 2) == 0;
                    float g = a ? 0.07f + 0.04f * ((x % 4) / 4f) : 0.12f + 0.04f * ((y % 4) / 4f);
                    px[y * s + x] = new Color(g, g, g * 1.1f, 1f);
                }
            return Finish(t, px);
        }

        static float[] LeafHeight(int s)
        {
            var hgt = new float[s * s];
            var rnd = new System.Random(77);
            for (int k = 0; k < 900; k++)
            {
                float cx = (float)rnd.NextDouble() * s, cy = (float)rnd.NextDouble() * s;
                float ang = (float)rnd.NextDouble() * Mathf.PI;
                float len = 5f + (float)rnd.NextDouble() * 6f, wid = len * 0.45f;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                int r = Mathf.CeilToInt(len);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        float lx = dx * ca + dy * sa, ly = -dx * sa + dy * ca;
                        float e = (lx * lx) / (len * len) + (ly * ly) / (wid * wid);
                        if (e > 1f) continue;
                        int x = ((int)cx + dx + s) % s, y = ((int)cy + dy + s) % s;
                        float hv = (1f - e) * (0.6f + 0.4f * (k % 7) / 7f) + k / 900f * 0.3f;
                        if (hv > hgt[y * s + x]) hgt[y * s + x] = hv;
                    }
            }
            return hgt;
        }

        public static Texture2D Folhas()
        {
            const int s = 256;
            var hgt = LeafHeight(s);
            var t = New(s, s);
            var px = new Color[s * s];
            for (int i = 0; i < px.Length; i++)
            {
                float hv = hgt[i];
                float var = Hash(i % s, i / s, 5) * 0.08f;
                px[i] = hv <= 0f ? new Color(0.05f, 0.12f, 0.04f) : new Color(0.16f + hv * 0.22f + var, 0.36f + hv * 0.3f + var, 0.1f + hv * 0.1f, 1f);
            }
            return Finish(t, px);
        }

        public static Texture2D FolhasNormal() => NormalFromHeight(LeafHeight(256), 256, 6f);

        public static Texture2D AguaNormal()
        {
            const int s = 256;
            var hgt = new float[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)s * Mathf.PI * 2f, v = y / (float)s * Mathf.PI * 2f;
                    hgt[y * s + x] = Mathf.Sin(u * 3f + v * 2f) * 0.3f + Mathf.Sin(u * 7f - v * 5f) * 0.15f + Mathf.Sin(v * 11f + u) * 0.08f;
                }
            return NormalFromHeight(hgt, s, 2f);
        }

        static Texture2D NormalFromHeight(float[] hgt, int s, float strength)
        {
            var t = New(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = hgt[y * s + (x + 1) % s] - hgt[y * s + (x - 1 + s) % s];
                    float dy = hgt[((y + 1) % s) * s + x] - hgt[((y - 1 + s) % s) * s + x];
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    px[y * s + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return Finish(t, px);
        }
    }
}
