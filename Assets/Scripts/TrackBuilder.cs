using UnityEngine;
using UnityEngine.Rendering;

namespace Corrida
{
    /// <summary>Gera a malha 3D do autódromo: asfalto, faixas, zebras, grama, muros, terreno e cenário.</summary>
    public static class TrackBuilder
    {
        public static Vector2 LakeCenter;
        public static float LakeRadius;

        public static void Build(TrackData t, Transform root)
        {
            var asphalt = Mats.Lib("Asfalto", new Color(0.25f, 0.25f, 0.27f));
            var line = Mats.Lib("Linha", Color.white);
            var grassM = Mats.Lib("Grama", new Color(0.25f, 0.5f, 0.18f));
            var curbM = Mats.Lib("Zebra", new Color(0.8f, 0.1f, 0.1f));
            var concrete = Mats.Lib("Concreto", new Color(0.7f, 0.7f, 0.7f));
            var panels = Mats.Lib("Paineis", new Color(0.1f, 0.3f, 0.7f));
            var tires = Mats.Lib("Pneus", new Color(0.1f, 0.1f, 0.1f));
            var checkerM = Mats.Lib("Quadriculado", Color.white);

            var road = new GameObject("Pista").transform;
            road.SetParent(root, false);

            int N = t.Count;
            float hw = TrackData.HalfWidth, wo = TrackData.WallOffset, sw = TrackData.ShoulderWidth;
            const float wallH = 1.1f, wallT = 0.5f, band = 0.3f;
            Vector3 up = Vector3.up;

            for (int s0 = 0; s0 < N; s0 += 80)
            {
                int s1 = Mathf.Min(s0 + 80, N);
                // asfalto (UV em metros)
                Strip(road, "Asfalto", asphalt, t, s0, s1, i => t.P[i] - t.R[i] * hw, i => t.P[i] + t.R[i] * hw, 0f, 2f * hw, 1f, true, false);
                // faixas brancas das bordas
                Strip(road, "Faixa", line, t, s0, s1, i => t.P[i] - t.R[i] * (hw - 0.2f) + up * 0.008f, i => t.P[i] - t.R[i] * (hw - 0.5f) + up * 0.008f, 0f, 1f, 1f, false, false);
                Strip(road, "Faixa", line, t, s0, s1, i => t.P[i] + t.R[i] * (hw - 0.5f) + up * 0.008f, i => t.P[i] + t.R[i] * (hw - 0.2f) + up * 0.008f, 0f, 1f, 1f, false, false);
                // grama (áreas de escape)
                Strip(road, "Grama", grassM, t, s0, s1, i => t.P[i] - t.R[i] * wo - up * 0.12f, i => t.P[i] - t.R[i] * hw - up * 0.02f, 0f, sw, 1f, true, true);
                Strip(road, "Grama", grassM, t, s0, s1, i => t.P[i] + t.R[i] * hw - up * 0.02f, i => t.P[i] + t.R[i] * wo - up * 0.12f, 0f, sw, 1f, true, true);
                // muro esquerdo: face interna, faixa vermelha e branca no topo, topo e face externa
                Strip(road, "Muro", concrete, t, s0, s1, i => t.P[i] - t.R[i] * wo + up * wallH, i => t.P[i] - t.R[i] * wo - up * 0.6f, 0f, wallH + 0.6f, 1f, true, false);
                Strip(road, "MuroFaixa", curbM, t, s0, s1, i => t.P[i] - t.R[i] * (wo - 0.02f) + up * wallH, i => t.P[i] - t.R[i] * (wo - 0.02f) + up * (wallH - band), 0f, 1f, 1f, false, false);
                Strip(road, "MuroTopo", concrete, t, s0, s1, i => t.P[i] - t.R[i] * (wo + wallT) + up * wallH, i => t.P[i] - t.R[i] * wo + up * wallH, 0f, wallT, 1f, false, false);
                Strip(road, "MuroFora", concrete, t, s0, s1, i => t.P[i] - t.R[i] * (wo + wallT) - up * 5f, i => t.P[i] - t.R[i] * (wo + wallT) + up * wallH, 0f, wallH + 5f, 1f, false, false);
                // muro direito
                Strip(road, "Muro", concrete, t, s0, s1, i => t.P[i] + t.R[i] * wo - up * 0.6f, i => t.P[i] + t.R[i] * wo + up * wallH, wallH + 0.6f, 0f, 1f, true, false);
                Strip(road, "MuroFaixa", curbM, t, s0, s1, i => t.P[i] + t.R[i] * (wo - 0.02f) + up * (wallH - band), i => t.P[i] + t.R[i] * (wo - 0.02f) + up * wallH, 1f, 0f, 1f, false, false);
                Strip(road, "MuroTopo", concrete, t, s0, s1, i => t.P[i] + t.R[i] * wo + up * wallH, i => t.P[i] + t.R[i] * (wo + wallT) + up * wallH, 0f, wallT, 1f, false, false);
                Strip(road, "MuroFora", concrete, t, s0, s1, i => t.P[i] + t.R[i] * (wo + wallT) + up * wallH, i => t.P[i] + t.R[i] * (wo + wallT) - up * 5f, 0f, wallH + 5f, 1f, false, false);
            }

            // zebras nas curvas
            var curbMask = new bool[N];
            for (int i = 0; i < N; i++)
                if (Mathf.Abs(t.Curv[i]) > 1f / 170f)
                    for (int k = -6; k <= 6; k++) curbMask[(i + k + N) % N] = true;
            Runs(curbMask, true, 80, (a, b) =>
            {
                Strip(road, "Zebra", curbM, t, a, b, i => t.P[i] - t.R[i] * (hw + 0.7f) + up * 0.02f, i => t.P[i] - t.R[i] * (hw - 0.9f) + up * 0.03f, 0f, 1f, 1f, false, false);
                Strip(road, "Zebra", curbM, t, a, b, i => t.P[i] + t.R[i] * (hw - 0.9f) + up * 0.03f, i => t.P[i] + t.R[i] * (hw + 0.7f) + up * 0.02f, 0f, 1f, 1f, false, false);
            });

            // muros: barreira de pneus nas curvas fortes, painéis de publicidade no resto
            var tireMask = new bool[N];
            for (int i = 0; i < N; i++)
                if (Mathf.Abs(t.Curv[i]) > 1f / 110f)
                    for (int k = -14; k <= 14; k++) tireMask[(i + k + N) % N] = true;
            float pTop = wallH - band - 0.02f, pBot = 0.05f;
            System.Action<int, int, Material> deco = (a, b, m) =>
            {
                Strip(road, "Deco", m, t, a, b, i => t.P[i] - t.R[i] * (wo - 0.03f) + up * pTop, i => t.P[i] - t.R[i] * (wo - 0.03f) + up * pBot, 0f, 1f, 1f, false, false);
                Strip(road, "Deco", m, t, a, b, i => t.P[i] + t.R[i] * (wo - 0.03f) + up * pBot, i => t.P[i] + t.R[i] * (wo - 0.03f) + up * pTop, 1f, 0f, 1f, false, false);
            };
            Runs(tireMask, true, 80, (a, b) => deco(a, b, tires));
            Runs(tireMask, false, 80, (a, b) => deco(a, b, panels));

            // linha de largada/chegada quadriculada
            Strip(road, "Largada", checkerM, t, 0, 1, i => t.P[i] - t.R[i] * hw + up * 0.025f, i => t.P[i] + t.R[i] * hw + up * 0.025f, 0f, 7f, 0.5f, false, false);
            // marcas do grid
            for (int k = 0; k < 8; k++)
            {
                int idx = GridIndex(t, k, out float lat);
                Mats.Box(road, t.P[idx] + t.R[idx] * lat + t.F[idx] * 2.6f + up * 0.012f, new Vector3(2.4f, 0.01f, 0.25f), Quaternion.LookRotation(t.F[idx], up), line);
            }

            BuildGround(t, root);
            BuildScenery(t, root);
        }

        /// <summary>Chama f(a, b) para cada trecho contínuo onde mask == value (em pedaços de até maxLen amostras).</summary>
        static void Runs(bool[] mask, bool value, int maxLen, System.Action<int, int> f)
        {
            int N = mask.Length;
            int i = 0;
            while (i < N)
            {
                if (mask[i] != value) { i++; continue; }
                int j = i;
                while (j + 1 < N && mask[j + 1] == value) j++;
                for (int a = i; a < j; a += maxLen) f(a, Mathf.Min(a + maxLen, j));
                i = j + 1;
            }
            if (mask[N - 1] == value && mask[0] == value) f(N - 1, N);
        }

        /// <summary>Posição no grid de largada (k = 0 é a pole). Retorna o índice da amostra e o deslocamento lateral.</summary>
        public static int GridIndex(TrackData t, int k, out float lateral)
        {
            float back = 9f + (k / 2) * 14f + (k % 2) * 7f; // grid de F1: fileiras alternadas
            lateral = (k % 2 == 0) ? -3.2f : 3.2f;
            int n = Mathf.RoundToInt(back / t.Step);
            return ((-n) % t.Count + t.Count) % t.Count;
        }

        static GameObject Strip(Transform parent, string name, Material m, TrackData t, int s0, int s1,
            System.Func<int, Vector3> A, System.Func<int, Vector3> B, float uA, float uB, float vScale, bool collider, bool grass)
        {
            int N = t.Count;
            int rings = s1 - s0 + 1;
            var verts = new Vector3[rings * 2];
            var uvs = new Vector2[rings * 2];
            var tris = new int[(rings - 1) * 6];
            for (int r = 0; r < rings; r++)
            {
                int raw = s0 + r;
                int i = raw % N;
                float d = raw >= N ? t.Length : t.Dist[i];
                verts[2 * r] = A(i);
                verts[2 * r + 1] = B(i);
                uvs[2 * r] = new Vector2(uA, d * vScale);
                uvs[2 * r + 1] = new Vector2(uB, d * vScale);
            }
            for (int r = 0; r < rings - 1; r++)
            {
                int a = 2 * r, b = a + 1, a2 = a + 2, b2 = a + 3, o = r * 6;
                tris[o] = a; tris[o + 1] = a2; tris[o + 2] = b;
                tris[o + 3] = b; tris[o + 4] = a2; tris[o + 5] = b2;
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                PhysMat.Slippery(mc);
                if (grass) TrackSurfaces.RegisterGrass(mc);
            }
            return go;
        }

        static void BuildGround(TrackData t, Transform root)
        {
            const float pad = 450f, res = 12f;
            float x0 = t.MinXZ.x - pad, z0 = t.MinXZ.y - pad, x1 = t.MaxXZ.x + pad, z1 = t.MaxXZ.y + pad;
            int nx = Mathf.CeilToInt((x1 - x0) / res) + 1, nz = Mathf.CeilToInt((z1 - z0) / res) + 1;
            var verts = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = x0 + i * res, z = z0 + j * res;
                    verts[j * nx + i] = new Vector3(x, t.GroundHeight(x, z), z);
                    uv[j * nx + i] = new Vector2(x, z);
                }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int o = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris[o++] = a; tris[o++] = c; tris[o++] = b;
                    tris[o++] = b; tris[o++] = c; tris[o++] = d;
                }
            var mesh = new Mesh { name = "Terreno", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject("Terreno");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lib("Terreno", new Color(0.3f, 0.5f, 0.2f));
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            TrackSurfaces.RegisterGrass(mc);
        }

        static void BuildScenery(TrackData t, Transform root)
        {
            var scen = new GameObject("Cenario").transform;
            scen.SetParent(root, false);
            int N = t.Count;
            float hw = TrackData.HalfWidth, wo = TrackData.WallOffset;
            Vector3 up = Vector3.up;

            var concrete = Mats.Lib("Concreto", new Color(0.65f, 0.65f, 0.66f));
            var metal = Mats.Lib("Metal", new Color(0.8f, 0.8f, 0.82f));
            var crowd = Mats.Lib("Torcida", new Color(0.6f, 0.5f, 0.4f));
            var garages = Mats.Lib("Garagens", new Color(0.5f, 0.5f, 0.5f));
            var glass = Mats.Lib("Vidro", new Color(0.1f, 0.12f, 0.15f));
            var water = Mats.Lib("Agua", new Color(0.12f, 0.32f, 0.5f));
            var bark = Mats.Lib("Casca", new Color(0.35f, 0.24f, 0.15f));
            var green = Mats.Tinted("Tinta", new Color(0.0f, 0.5f, 0.22f));
            var yellow = Mats.Tinted("Tinta", new Color(1f, 0.8f, 0.05f));
            var blue = Mats.Tinted("Tinta", new Color(0.06f, 0.2f, 0.6f));
            Material[] leaves =
            {
                Mats.Tinted("Folhas", new Color(0.85f, 0.95f, 0.8f)),
                Mats.Tinted("Folhas", new Color(1f, 1f, 0.75f)),
                Mats.Tinted("Folhas", new Color(0.7f, 0.85f, 0.75f)),
            };
            Material[] buildings =
            {
                Mats.Tinted("Predio", new Color(0.95f, 0.93f, 0.88f)),
                Mats.Tinted("Predio", new Color(0.85f, 0.75f, 0.65f)),
                Mats.Tinted("Predio", new Color(0.7f, 0.75f, 0.8f)),
                Mats.Tinted("Predio", new Color(0.95f, 0.85f, 0.7f)),
                Mats.Tinted("Predio", new Color(0.75f, 0.72f, 0.7f)),
            };

            // pórtico da largada (verde e amarelo)
            {
                Vector3 p = t.P[0];
                Quaternion rot = Quaternion.LookRotation(t.F[0], up);
                float span = hw + 1.6f;
                Mats.Box(scen, p - t.R[0] * span + up * 3.5f, new Vector3(0.7f, 7f, 0.7f), rot, concrete);
                Mats.Box(scen, p + t.R[0] * span + up * 3.5f, new Vector3(0.7f, 7f, 0.7f), rot, concrete);
                Mats.Box(scen, p + up * 7.4f, new Vector3(span * 2f + 0.7f, 1.6f, 0.8f), rot, green);
                Mats.Box(scen, p + up * 6.55f, new Vector3(span * 2f + 0.72f, 0.12f, 0.82f), rot, yellow);
                Mats.Box(scen, p + up * 8.25f, new Vector3(span * 2f + 0.72f, 0.12f, 0.82f), rot, yellow);
                Mats.Text3D(scen, "INTERLAGOS", p + up * 7.4f - t.F[0] * 0.45f, rot, 0.16f, new Color(1f, 0.85f, 0.1f));
                Mats.Text3D(scen, "INTERLAGOS", p + up * 7.4f + t.F[0] * 0.45f, Quaternion.LookRotation(-t.F[0], up), 0.16f, new Color(1f, 0.85f, 0.1f));
            }

            // arquibancadas (lado de fora da reta)
            for (int k = -4; k <= 6; k++)
            {
                int i = ((k * 22) % N + N) % N;
                Vector3 p = t.P[i];
                Quaternion rot = Quaternion.LookRotation(t.F[i], up);
                float baseY = p.y - 4f;
                for (int tier = 0; tier < 7; tier++)
                {
                    float top = p.y + 1.5f + tier * 0.9f;
                    float h = top - baseY;
                    Vector3 c = p + t.R[i] * (wo + 3f + tier * 1.5f + 0.75f);
                    c.y = baseY + h * 0.5f;
                    Mats.Box(scen, c, new Vector3(1.5f, h, 43f), rot, tier == 0 ? concrete : crowd);
                }
                Vector3 roof = p + t.R[i] * (wo + 3f + 5.25f);
                roof.y = p.y + 1.5f + 6f * 0.9f + 4f;
                Mats.Box(scen, roof, new Vector3(11.5f, 0.35f, 43f), rot, k % 2 == 0 ? blue : metal);
                Vector3 post = p + t.R[i] * (wo + 3f + 10.4f);
                post.y = (baseY + roof.y) * 0.5f;
                Mats.Box(scen, post, new Vector3(0.5f, roof.y - baseY, 0.5f), rot, concrete);
            }

            // prédio dos boxes (lado de dentro)
            {
                int i = Mathf.RoundToInt(40f / t.Step);
                Vector3 p = t.P[i];
                Quaternion rot = Quaternion.LookRotation(t.F[i], up);
                Vector3 c = p - t.R[i] * (wo + 9f);
                c.y = p.y + 2.5f;
                Mats.Box(scen, c, new Vector3(13f, 7f, 210f), rot, concrete);
                Vector3 gate = p - t.R[i] * (wo + 2.45f);
                gate.y = p.y + 1.5f;
                Mats.Box(scen, gate, new Vector3(0.1f, 5f, 206f), Quaternion.LookRotation(t.F[i], up), garages);
                Vector3 win = c; win.y = p.y + 4.9f;
                Mats.Box(scen, win, new Vector3(13.1f, 1.2f, 206f), rot, glass);
                Vector3 r = c; r.y = p.y + 6.2f;
                Mats.Box(scen, r, new Vector3(14f, 0.4f, 212f), rot, metal);
                Vector3 stripe = c; stripe.y = p.y + 5.75f;
                Mats.Box(scen, stripe, new Vector3(13.2f, 0.25f, 210.5f), rot, green);
            }

            // placas com o nome de cada trecho
            foreach (var np in t.NamedPoints)
            {
                if (np.Value == "Reta dos Boxes") continue;
                int i = ((np.Key - 35) % N + N) % N;
                float side = t.Curv[np.Key] > 0f ? -1f : 1f; // lado de fora da curva
                Vector3 p = t.P[i] + t.R[i] * side * (wo + 3.5f);
                Quaternion rot = Quaternion.LookRotation(t.F[i], up);
                Mats.Box(scen, p + up * 1.5f, new Vector3(0.3f, 7f, 0.3f), rot, concrete);
                Mats.Box(scen, p + up * 4.6f, new Vector3(13f, 2f, 0.2f), rot, blue);
                Mats.Box(scen, p + up * 3.55f, new Vector3(13f, 0.12f, 0.22f), rot, yellow);
                Mats.Text3D(scen, np.Value.ToUpper(), p + up * 4.6f - t.F[i] * 0.15f, rot, 0.12f, Color.white);
            }

            // lago (perto da Descida do Lago)
            LakeCenter = t.ToWorld(805f, 205f);
            LakeRadius = 55f;
            if (t.NearestDistXZ(LakeCenter.x, LakeCenter.y) > LakeRadius + wo)
            {
                float lh = t.GroundHeight(LakeCenter.x, LakeCenter.y);
                Mats.Part(scen, PrimitiveType.Cylinder, new Vector3(LakeCenter.x, lh + 0.05f, LakeCenter.y), new Vector3(LakeRadius * 2f, 0.25f, LakeRadius * 1.4f), water);
            }
            else LakeRadius = 0f;

            var rnd = new System.Random(4309);

            // árvores (copas com 2 ou 3 esferas)
            var trees = new GameObject("Arvores").transform;
            trees.SetParent(scen, false);
            int placed = 0;
            for (int k = 0; k < 2500 && placed < 520; k++)
            {
                float x = Mathf.Lerp(t.MinXZ.x - 380f, t.MaxXZ.x + 380f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(t.MinXZ.y - 380f, t.MaxXZ.y + 380f, (float)rnd.NextDouble());
                float dn = t.NearestDistXZ(x, z);
                if (dn < wo + 12f) continue;
                if (LakeRadius > 0f && (new Vector2(x, z) - LakeCenter).magnitude < LakeRadius + 8f) continue;
                if ((new Vector2(x, z) - new Vector2(t.P[0].x, t.P[0].z)).magnitude < 280f && dn < 60f) continue;
                float gy = t.GroundHeight(x, z);
                float s = 0.8f + (float)rnd.NextDouble() * 0.9f;
                var tr = new GameObject("Arvore").transform;
                tr.SetParent(trees, false);
                tr.position = new Vector3(x, gy, z);
                tr.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                Mats.Part(tr, PrimitiveType.Cylinder, new Vector3(0f, 1.8f * s, 0f), new Vector3(0.55f * s, 1.8f * s, 0.55f * s), bark);
                var leaf = leaves[rnd.Next(leaves.Length)];
                Mats.Part(tr, PrimitiveType.Sphere, new Vector3(0f, 4.6f * s, 0f), new Vector3(4.6f * s, 3.8f * s, 4.6f * s), leaf);
                Mats.Part(tr, PrimitiveType.Sphere, new Vector3(1.3f * s, 4.0f * s, 0.6f * s), new Vector3(3.2f * s, 2.8f * s, 3.2f * s), leaf);
                if (rnd.NextDouble() > 0.4) Mats.Part(tr, PrimitiveType.Sphere, new Vector3(-1.1f * s, 5.4f * s, -0.5f * s), new Vector3(3f * s, 2.6f * s, 3f * s), leaf);
                placed++;
            }

            // prédios de São Paulo ao fundo
            var city = new GameObject("Cidade").transform;
            city.SetParent(scen, false);
            int nb = 0;
            for (int k = 0; k < 3000 && nb < 90; k++)
            {
                float x = Mathf.Lerp(t.MinXZ.x - 440f, t.MaxXZ.x + 440f, (float)rnd.NextDouble());
                float z = Mathf.Lerp(t.MinXZ.y - 440f, t.MaxXZ.y + 440f, (float)rnd.NextDouble());
                if (t.NearestDistXZ(x, z) < 260f) continue;
                float gy = t.GroundHeight(x, z);
                float w = 16f + (float)rnd.NextDouble() * 20f, d = 16f + (float)rnd.NextDouble() * 20f;
                float h = 15f + Mathf.Pow((float)rnd.NextDouble(), 2f) * 75f;
                h = Mathf.Round(h / 3f) * 3f;
                var rot = Quaternion.Euler(0f, rnd.Next(4) * 90f + (float)rnd.NextDouble() * 20f, 0f);
                Mats.Box(city, new Vector3(x, gy - 2f + h * 0.5f, z), new Vector3(w, h + 4f, d), rot, buildings[rnd.Next(buildings.Length)]);
                Mats.Box(city, new Vector3(x, gy + h + 2.2f, z), new Vector3(w * 0.4f, 1.5f, d * 0.4f), rot, metal);
                nb++;
            }

            StaticBatchingUtility.Combine(scen.gameObject);
        }
    }
}
