using System.Collections.Generic;
using UnityEngine;

namespace Corrida
{
    /// <summary>
    /// Monta um F1 no formato do regulamento 2022-2025 ("efeito solo"): comprimento ~5,6 m, entre-eixos 3,6 m,
    /// largura 2,0 m, altura ~0,95 m, rodas de 18" com calotas e pneus 305/720 e 405/720, defletores sobre as rodas
    /// dianteiras, bico longo apoiado na asa dianteira de 4 elementos que sobe em direção às placas laterais,
    /// sidepods em rampa ("downwash") com entrada alta, assoalho largo com difusor grande, asa traseira com
    /// placas arredondadas, viga inferior (beam wing) e flap do DRS móvel, escapamento central, halo e T-cam.
    /// A carroceria é uma malha lisa gerada por seções transversais (superelipses). Pintura genérica.
    /// </summary>
    public static class CarFactory
    {
        public const int CarLayer = 2; // "Ignore Raycast": a suspensão não detecta outros carros

        static int L;

        struct St
        {
            public float z, xc, hw, y0, y1, n;
            public St(float z, float hw, float y0, float y1, float n = 2.6f, float xc = 0f) { this.z = z; this.hw = hw; this.y0 = y0; this.y1 = y1; this.n = n; this.xc = xc; }
        }

        static GameObject P(Transform t, Vector3 pos, Vector3 size, Material m, Vector3? euler = null, PrimitiveType type = PrimitiveType.Cube)
            => Mats.Part(t, type, pos, size, m, euler.HasValue ? Quaternion.Euler(euler.Value) : (Quaternion?)null, L);

        static void Mirror(Transform t, float s, Vector3 pos, Vector3 size, Material m, Vector3? euler = null)
        {
            Vector3 e = euler ?? Vector3.zero;
            P(t, new Vector3(pos.x * s, pos.y, pos.z), size, m, new Vector3(e.x, e.y * s, e.z * s));
        }

        /// <summary>Barra fina ligando dois pontos (braços de suspensão).</summary>
        static void Arm(Transform t, Vector3 a, Vector3 b, float thick, Material m)
        {
            var g = Mats.Part(t, PrimitiveType.Cube, (a + b) * 0.5f, new Vector3(thick, thick * 0.45f, (b - a).magnitude), m, Quaternion.LookRotation(b - a), L);
            g.name = "Braco";
        }

        /// <summary>Tubo cilíndrico entre dois pontos (halo).</summary>
        static void Tube(Transform t, Vector3 a, Vector3 b, float d, Material m)
        {
            Vector3 dir = b - a;
            Mats.Part(t, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(d, dir.magnitude * 0.5f + d * 0.3f, d), m, Quaternion.FromToRotation(Vector3.up, dir), L);
        }

        /// <summary>Malha lisa ligando seções transversais em superelipse (da frente para trás), com tampas.</summary>
        static GameObject Loft(Transform parent, string name, Material m, St[] st, int ring = 22, float mirrorX = 1f)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int S = st.Length;
            for (int s = 0; s < S; s++)
            {
                var q = st[s];
                float yc = (q.y0 + q.y1) * 0.5f, hh = (q.y1 - q.y0) * 0.5f, ex = 2f / q.n;
                for (int j = 0; j < ring; j++)
                {
                    float th = j * Mathf.PI * 2f / ring;
                    float c = Mathf.Cos(th), si = Mathf.Sin(th);
                    float x = q.xc + q.hw * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), ex);
                    float y = yc + hh * Mathf.Sign(si) * Mathf.Pow(Mathf.Abs(si), ex);
                    verts.Add(new Vector3(x * mirrorX, y, q.z));
                    uvs.Add(new Vector2(j / (float)ring * 2f, q.z));
                }
            }
            for (int s = 0; s < S - 1; s++)
                for (int j = 0; j < ring; j++)
                {
                    int a = s * ring + j, b = s * ring + (j + 1) % ring, c = (s + 1) * ring + j, d = (s + 1) * ring + (j + 1) % ring;
                    if (mirrorX > 0f) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                }
            // tampas (vértices próprios para normal reta)
            for (int cap = 0; cap < 2; cap++)
            {
                int s = cap == 0 ? 0 : S - 1;
                var q = st[s];
                int center = verts.Count;
                verts.Add(new Vector3(q.xc * mirrorX, (q.y0 + q.y1) * 0.5f, q.z));
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int j = 0; j < ring; j++) { verts.Add(verts[s * ring + j]); uvs.Add(uvs[s * ring + j]); }
                bool front = cap == 0;
                if (mirrorX < 0f) front = !front;
                for (int j = 0; j < ring; j++)
                {
                    int a = center + 1 + j, b = center + 1 + (j + 1) % ring;
                    if (front) { tris.Add(center); tris.Add(a); tris.Add(b); }
                    else { tris.Add(center); tris.Add(b); tris.Add(a); }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var g = new GameObject(name);
            g.layer = L;
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        public static CarController Create(string name, Color color, Color accent, int number, Vector3 pos, Quaternion rot)
        {
            L = CarLayer;
            var carbon = Mats.Lib("Carbono", new Color(0.08f, 0.08f, 0.09f));
            var rubber = Mats.Lib("Borracha", new Color(0.06f, 0.06f, 0.06f));
            var tailOff = Mats.Lib("LanternaOff", new Color(0.35f, 0.02f, 0.02f));
            var tailOn = Mats.Lib("LanternaOn", new Color(1f, 0.1f, 0.08f));
            var paint = Mats.Tinted("PinturaCarro", color);
            var accentM = Mats.Tinted("PinturaCarro", accent);
            var black = Mats.Tinted("Tinta", new Color(0.03f, 0.03f, 0.035f));
            var rimM = Mats.Tinted("Cromado", new Color(0.16f, 0.16f, 0.18f));
            var sidewall = Mats.Tinted("Tinta", new Color(1f, 0.82f, 0.05f)); // faixa amarela = composto médio
            var helmet = Mats.Tinted("PinturaCarro", accent);
            var visor = Mats.Lib("Vidro", new Color(0.05f, 0.05f, 0.08f));
            bool bright = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f > 0.55f;
            Color numColor = bright ? Color.black : Color.white;

            var root = new GameObject("F1_" + name);
            root.layer = CarLayer;
            root.transform.SetPositionAndRotation(pos, rot);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = F1Spec.Mass;
            rb.SetDamping(0.0f, 0.6f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, -0.05f);
            col.size = new Vector3(2.0f, 0.6f, 5.6f);
            PhysMat.Slippery(col);
            rb.centerOfMass = new Vector3(0f, 0.28f, -0.15f);

            var t = root.transform;

            // ---- monocoque, bico e tampa do motor (uma peça lisa)
            Loft(t, "Chassi", paint, new[]
            {
                new St(2.86f, 0.05f, 0.15f, 0.23f, 2.2f),
                new St(2.62f, 0.09f, 0.13f, 0.29f),
                new St(2.25f, 0.13f, 0.14f, 0.36f),
                new St(1.80f, 0.17f, 0.16f, 0.45f),
                new St(1.30f, 0.23f, 0.17f, 0.55f),
                new St(0.85f, 0.31f, 0.13f, 0.63f, 3f),
                new St(0.45f, 0.38f, 0.09f, 0.67f, 3f),
                new St(0.00f, 0.42f, 0.09f, 0.69f, 3f),
                new St(-0.35f, 0.42f, 0.09f, 0.80f, 3f),
                new St(-0.75f, 0.38f, 0.09f, 0.86f, 3f),
                new St(-1.25f, 0.30f, 0.11f, 0.78f, 2.8f),
                new St(-1.75f, 0.20f, 0.13f, 0.62f),
                new St(-2.15f, 0.12f, 0.17f, 0.48f),
                new St(-2.42f, 0.07f, 0.22f, 0.38f, 2.2f),
            });
            // faixa da pintura no bico e na tampa do motor
            Loft(t, "FaixaBico", accentM, new[]
            {
                new St(2.70f, 0.02f, 0.27f, 0.295f, 2f),
                new St(2.25f, 0.045f, 0.345f, 0.37f, 2f),
                new St(1.80f, 0.06f, 0.435f, 0.457f, 2f),
                new St(1.30f, 0.07f, 0.54f, 0.557f, 2f),
                new St(0.95f, 0.07f, 0.605f, 0.62f, 2f),
            });
            // cockpit, encosto e piloto
            P(t, new Vector3(0f, 0.69f, 0.2f), new Vector3(0.5f, 0.02f, 0.72f), black);
            P(t, new Vector3(0f, 0.75f, -0.24f), new Vector3(0.6f, 0.1f, 0.2f), accentM, new Vector3(-8f, 0f, 0f));
            P(t, new Vector3(0f, 0.79f, -0.06f), new Vector3(0.27f, 0.27f, 0.3f), helmet, null, PrimitiveType.Sphere);
            P(t, new Vector3(0f, 0.81f, 0.07f), new Vector3(0.2f, 0.07f, 0.08f), visor);
            P(t, new Vector3(0f, 0.91f, -0.08f), new Vector3(0.05f, 0.03f, 0.28f), paint);

            // entrada de ar sobre o piloto (airbox), T-cam e barbatana
            Loft(t, "Airbox", paint, new[]
            {
                new St(-0.17f, 0.12f, 0.70f, 1.02f, 2.4f),
                new St(-0.35f, 0.15f, 0.69f, 1.06f, 2.6f),
                new St(-0.70f, 0.13f, 0.70f, 0.99f, 2.6f),
                new St(-1.10f, 0.08f, 0.72f, 0.89f),
                new St(-1.50f, 0.03f, 0.74f, 0.80f),
            });
            P(t, new Vector3(0f, 0.87f, -0.16f), new Vector3(0.17f, 0.17f, 0.02f), black);
            P(t, new Vector3(0f, 1.085f, -0.4f), new Vector3(0.13f, 0.035f, 0.06f), accentM); // T-cam
            P(t, new Vector3(0f, 0.86f, -1.75f), new Vector3(0.015f, 0.2f, 1.1f), paint, new Vector3(-6f, 0f, 0f));

            // ---- halo (tubo de titânio)
            Vector3[] halo =
            {
                new Vector3(0.31f, 0.70f, -0.33f), new Vector3(0.31f, 0.84f, -0.25f), new Vector3(0.30f, 0.90f, -0.05f),
                new Vector3(0.26f, 0.92f, 0.18f), new Vector3(0.15f, 0.93f, 0.36f), new Vector3(0f, 0.93f, 0.43f)
            };
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < halo.Length - 1; i++)
                {
                    Vector3 a = halo[i], b = halo[i + 1];
                    a.x *= s; b.x *= s;
                    Tube(t, a, b, 0.05f, black);
                }
            Tube(t, new Vector3(0f, 0.66f, 0.66f), new Vector3(0f, 0.93f, 0.43f), 0.05f, black);

            // ---- sidepods em rampa (downwash) com entrada alta e recorte por baixo
            for (int s = -1; s <= 1; s += 2)
            {
                var pods = new[]
                {
                    new St(0.78f, 0.15f, 0.33f, 0.57f, 3.2f, 0.63f),
                    new St(0.55f, 0.22f, 0.22f, 0.59f, 3.2f, 0.62f),
                    new St(0.10f, 0.25f, 0.13f, 0.56f, 3.2f, 0.62f),
                    new St(-0.40f, 0.24f, 0.11f, 0.47f, 3.2f, 0.58f),
                    new St(-0.90f, 0.20f, 0.11f, 0.37f, 3f, 0.51f),
                    new St(-1.40f, 0.14f, 0.11f, 0.27f, 3f, 0.43f),
                    new St(-1.90f, 0.07f, 0.11f, 0.18f, 2.6f, 0.33f),
                };
                Loft(t, "Sidepod", paint, pods, 22, s);
                Loft(t, "FaixaSidepod", accentM, new[]
                {
                    new St(0.55f, 0.07f, 0.575f, 0.597f, 2f, 0.62f),
                    new St(0.10f, 0.08f, 0.545f, 0.567f, 2f, 0.62f),
                    new St(-0.40f, 0.075f, 0.457f, 0.477f, 2f, 0.58f),
                    new St(-0.90f, 0.06f, 0.358f, 0.377f, 2f, 0.51f),
                }, 12, s);
                Mirror(t, s, new Vector3(0.64f, 0.46f, 0.785f), new Vector3(0.26f, 0.2f, 0.02f), black); // boca do sidepod
                // retrovisores (no ombro do sidepod)
                Mirror(t, s, new Vector3(0.5f, 0.66f, 0.5f), new Vector3(0.02f, 0.1f, 0.03f), black);
                Mirror(t, s, new Vector3(0.53f, 0.72f, 0.5f), new Vector3(0.15f, 0.06f, 0.05f), paint);
            }

            // ---- assoalho com borda e difusor grande
            Loft(t, "Assoalho", carbon, new[]
            {
                new St(1.15f, 0.30f, 0.05f, 0.08f, 6f),
                new St(0.80f, 0.72f, 0.05f, 0.08f, 6f),
                new St(-1.50f, 0.86f, 0.05f, 0.08f, 6f),
                new St(-1.90f, 0.72f, 0.05f, 0.08f, 6f),
            }, 16);
            for (int s = -1; s <= 1; s += 2)
            {
                Mirror(t, s, new Vector3(0.86f, 0.12f, -0.35f), new Vector3(0.015f, 0.09f, 2.2f), carbon); // borda do assoalho
                Mirror(t, s, new Vector3(0.82f, 0.165f, -0.35f), new Vector3(0.1f, 0.012f, 2.0f), accentM, new Vector3(0f, 0f, 12f));
                Mirror(t, s, new Vector3(0.5f, 0.24f, -2.25f), new Vector3(0.015f, 0.32f, 0.72f), carbon, new Vector3(0f, 0f, 0f)); // parede do difusor
                Mirror(t, s, new Vector3(0.22f, 0.2f, -2.25f), new Vector3(0.012f, 0.22f, 0.66f), carbon);
            }
            P(t, new Vector3(0f, 0.2f, -2.25f), new Vector3(1.0f, 0.02f, 0.78f), carbon, new Vector3(20f, 0f, 0f)); // rampa do difusor

            // ---- asa dianteira: 4 elementos subindo para fora, placas laterais arredondadas
            Material[] fwMats = { carbon, carbon, paint, accentM };
            for (int s = -1; s <= 1; s += 2)
            {
                for (int e = 0; e < 4; e++)
                {
                    float y = 0.075f + e * 0.035f, z = 2.86f - e * 0.085f, chord = 0.15f - e * 0.02f, pitch = 5f + e * 11f;
                    // parte interna baixa e parte externa que sobe em direção à placa lateral
                    Mirror(t, s, new Vector3(0.37f, y, z), new Vector3(0.5f, 0.018f, chord), fwMats[e], new Vector3(pitch, 0f, 0f));
                    Mirror(t, s, new Vector3(0.78f, y + 0.045f + e * 0.012f, z - 0.03f), new Vector3(0.36f, 0.018f, chord), fwMats[e], new Vector3(pitch, 0f, 13f));
                }
                Mirror(t, s, new Vector3(0.98f, 0.17f, 2.72f), new Vector3(0.02f, 0.2f, 0.42f), paint, new Vector3(0f, -4f, 0f));
                Mirror(t, s, new Vector3(0.965f, 0.29f, 2.62f), new Vector3(0.02f, 0.08f, 0.24f), paint, new Vector3(-25f, -4f, 0f));
            }
            P(t, new Vector3(0f, 0.09f, 2.84f), new Vector3(0.36f, 0.02f, 0.2f), carbon); // centro da asa (abaixo do bico)

            // ---- asa traseira: placas arredondadas, plano principal em "colher", flap do DRS, viga inferior
            P(t, new Vector3(0f, 0.9f, -2.53f), new Vector3(0.5f, 0.035f, 0.32f), paint, new Vector3(8f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                Mirror(t, s, new Vector3(0.36f, 0.915f, -2.53f), new Vector3(0.24f, 0.035f, 0.32f), paint, new Vector3(8f, 0f, 6f));
                Mirror(t, s, new Vector3(0.5f, 0.72f, -2.55f), new Vector3(0.022f, 0.42f, 0.5f), paint);            // placa lateral
                Mirror(t, s, new Vector3(0.465f, 0.99f, -2.53f), new Vector3(0.022f, 0.16f, 0.44f), paint, new Vector3(0f, 0f, 38f)); // curva superior
                Mirror(t, s, new Vector3(0.512f, 0.62f, -2.55f), new Vector3(0.005f, 0.16f, 0.46f), accentM);
                Mirror(t, s, new Vector3(0.12f, 0.6f, -2.42f), new Vector3(0.03f, 0.55f, 0.08f), carbon, new Vector3(12f, 0f, 0f)); // suporte "pescoço de cisne"
            }
            var flapPivot = new GameObject("DRS").transform;
            flapPivot.gameObject.layer = L;
            flapPivot.SetParent(t, false);
            flapPivot.localPosition = new Vector3(0f, 0.985f, -2.4f);
            flapPivot.localRotation = Quaternion.Euler(32f, 0f, 0f);
            P(flapPivot, new Vector3(0f, 0f, -0.12f), new Vector3(0.96f, 0.025f, 0.24f), accentM);
            P(t, new Vector3(0f, 0.42f, -2.5f), new Vector3(0.9f, 0.025f, 0.16f), carbon, new Vector3(10f, 0f, 0f)); // beam wing
            P(t, new Vector3(0f, 0.5f, -2.42f), new Vector3(0.9f, 0.022f, 0.12f), carbon, new Vector3(24f, 0f, 0f));
            // escapamento central e luz traseira
            Mats.Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.36f, -2.42f), new Vector3(0.11f, 0.12f, 0.11f), rimM, Quaternion.Euler(90f, 0f, 0f), L);
            var rain = P(t, new Vector3(0f, 0.27f, -2.5f), new Vector3(0.14f, 0.06f, 0.03f), tailOff);

            // ---- números (bico e laterais da tampa do motor)
            string num = number.ToString();
            Mats.Text3D(t, num, new Vector3(0f, 0.465f, 1.72f), Quaternion.LookRotation(Vector3.down, Vector3.forward), 0.03f, numColor, true, L);
            Mats.Text3D(t, num, new Vector3(-0.355f, 0.6f, -0.85f), Quaternion.LookRotation(Vector3.right), 0.034f, numColor, true, L);
            Mats.Text3D(t, num, new Vector3(0.355f, 0.6f, -0.85f), Quaternion.LookRotation(Vector3.left), 0.034f, numColor, true, L);

            var car = root.AddComponent<CarController>();
            car.brakeLights = new[] { rain.GetComponent<Renderer>() };
            car.brakeOn = tailOn;
            car.brakeOff = tailOff;
            car.drsFlap = flapPivot;
            car.SetFuel(0f);

            // ---- rodas 18": pneus 305/720 (frente) e 405/720 (trás), calotas, dutos de freio, defletores
            float mountY = car.wheelRadius + car.suspRest;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float side = (i % 2 == 0) ? -1f : 1f;
                float z = front ? 1.8f : -1.8f;
                float width = front ? 0.305f : 0.405f;
                float x = 1.0f - width * 0.5f - 0.005f;
                car.wheelMounts[i] = new Vector3(x * side, mountY, z);
                car.wheelVisualBase[i] = car.wheelMounts[i];

                var pivot = new GameObject("Roda" + i).transform;
                pivot.gameObject.layer = L;
                pivot.SetParent(t, false);
                pivot.localPosition = car.wheelVisualBase[i] + Vector3.down * car.suspRest;
                var spinner = new GameObject("Giro").transform;
                spinner.gameObject.layer = L;
                spinner.SetParent(pivot, false);
                Quaternion axisX = Quaternion.Euler(0f, 0f, 90f);
                float d = car.wheelRadius * 2f;
                Mats.Part(spinner, PrimitiveType.Cylinder, Vector3.zero, new Vector3(d, width * 0.5f, d), rubber, axisX, L);
                Mats.Part(spinner, PrimitiveType.Cylinder, Vector3.zero, new Vector3(d * 0.86f, width * 0.5f + 0.003f, d * 0.86f), sidewall, axisX, L);
                Mats.Part(spinner, PrimitiveType.Cylinder, Vector3.zero, new Vector3(d * 0.8f, width * 0.5f + 0.006f, d * 0.8f), rubber, axisX, L);
                Mats.Part(spinner, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.457f, width * 0.5f + 0.009f, 0.457f), rimM, axisX, L);
                // calota (obrigatória desde 2022) com marca da equipe para ver o giro
                Mats.Part(spinner, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.43f, width * 0.5f + 0.012f, 0.43f), carbon, axisX, L);
                Mats.Part(spinner, PrimitiveType.Cube, new Vector3(0f, 0f, 0.11f), new Vector3(width + 0.03f, 0.05f, 0.09f), accentM, null, L); // marca na calota
                // duto de freio e defletor sobre a roda dianteira (giram com o esterço, não com a roda)
                Mats.Part(pivot, PrimitiveType.Cylinder, new Vector3(-side * (width * 0.5f + 0.03f), 0f, 0f), new Vector3(0.36f, 0.03f, 0.36f), carbon, axisX, L);
                if (front)
                    Mats.Part(pivot, PrimitiveType.Cube, new Vector3(0f, d * 0.5f + 0.04f, 0.02f), new Vector3(width * 0.9f, 0.015f, 0.26f), carbon, Quaternion.Euler(-12f, 0f, 0f), L);
                car.wheelPivots[i] = pivot;
                car.wheelSpinners[i] = spinner;

                // suspensão: braços duplos + pushrod (frente) / pullrod (trás)
                float cx = front ? 0.2f : 0.18f;
                float wy = car.wheelRadius;
                Vector3 hub = new Vector3((x - width * 0.5f) * side, wy, z);
                Arm(t, new Vector3(cx * side, front ? 0.5f : 0.45f, z + 0.2f), hub + new Vector3(0f, 0.1f, 0f), 0.04f, black);
                Arm(t, new Vector3(cx * side, front ? 0.5f : 0.45f, z - 0.25f), hub + new Vector3(0f, 0.1f, 0f), 0.04f, black);
                Arm(t, new Vector3(cx * side, 0.2f, z + 0.2f), hub - new Vector3(0f, 0.1f, 0f), 0.04f, black);
                Arm(t, new Vector3(cx * side, 0.2f, z - 0.25f), hub - new Vector3(0f, 0.1f, 0f), 0.04f, black);
                if (front) Arm(t, new Vector3(cx * side, 0.55f, z - 0.05f), hub - new Vector3(0.05f * side, 0.08f, 0f), 0.03f, black);
                else Arm(t, new Vector3(cx * side, 0.2f, z + 0.05f), hub + new Vector3(-0.05f * side, 0.08f, 0f), 0.03f, black);
            }
            return car;
        }
    }
}
