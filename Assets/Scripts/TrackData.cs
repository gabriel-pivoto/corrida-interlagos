using System.Collections.Generic;
using UnityEngine;

namespace Corrida
{
    /// <summary>
    /// Traçado de Interlagos (Autódromo José Carlos Pace), sentido anti-horário, 4.309 m.
    /// Os pontos de controle reproduzem a sequência e a direção das curvas reais
    /// (S do Senna, Curva do Sol, Reta Oposta, Descida do Lago, Ferradura, Laranjinha,
    /// Pinheirinho, Bico de Pato, Mergulho, Junção, Subida dos Boxes) e o desnível do circuito.
    /// O traçado é escalado para o comprimento oficial.
    /// </summary>
    public class TrackData
    {
        public struct CP
        {
            public float x, z, h;
            public string name;
            public CP(float x, float z, float h, string name = null) { this.x = x; this.z = z; this.h = h; this.name = name; }
        }

        public static readonly CP[] Layout =
        {
            new CP(700, 600, 40, "Reta dos Boxes"),
            new CP(450, 610, 40),
            new CP(260, 600, 40),
            new CP(200, 580, 39, "S do Senna"),
            new CP(180, 540, 34),
            new CP(165, 500, 30),
            new CP(130, 460, 26),
            new CP(100, 400, 22, "Curva do Sol"),
            new CP(100, 330, 20),
            new CP(130, 270, 18),
            new CP(190, 230, 16, "Reta Oposta"),
            new CP(350, 200, 12),
            new CP(550, 170, 7),
            new CP(750, 140, 3),
            new CP(840, 130, 1, "Descida do Lago"),
            new CP(900, 160, 0),
            new CP(910, 220, 0),
            new CP(880, 260, 1),
            new CP(800, 280, 2),
            new CP(700, 285, 4, "Ferradura"),
            new CP(640, 300, 6),
            new CP(620, 340, 9),
            new CP(625, 400, 12, "Laranjinha"),
            new CP(660, 430, 14),
            new CP(720, 435, 15),
            new CP(780, 430, 15, "Pinheirinho"),
            new CP(820, 455, 15),
            new CP(830, 500, 16),
            new CP(845, 530, 17, "Bico de Pato"),
            new CP(875, 525, 17),
            new CP(885, 490, 15),
            new CP(895, 430, 10, "Mergulho"),
            new CP(920, 390, 7),
            new CP(960, 375, 6, "Junção"),
            new CP(985, 400, 7),
            new CP(990, 470, 14, "Subida dos Boxes"),
            new CP(970, 545, 24),
            new CP(920, 590, 33),
            new CP(840, 605, 38),
        };

        public const float TargetLength = 4309f;
        public const float HalfWidth = 7f;
        public const float ShoulderWidth = 9f;
        public static float WallOffset => HalfWidth + ShoulderWidth;

        public int Count;
        public float Step, Length, Scale;
        public Vector2 Offset;
        public Vector3[] P, F, R;
        public float[] Dist, Curv, SpeedTarget;
        public float[] RaceOff, RaceCurv;     // linha ideal: deslocamento lateral e curvatura
        public Vector3[] RaceP, RaceF;
        public readonly List<DrsZone> DrsZones = new List<DrsZone>();
        readonly Dictionary<int, float[]> speedCache = new Dictionary<int, float[]>();

        public struct DrsZone { public int detect, start, end; public string name; }
        public string[] Section;
        public float MinY, MaxY;
        public Vector2 MinXZ, MaxXZ;
        public readonly List<KeyValuePair<int, string>> NamedPoints = new List<KeyValuePair<int, string>>();

        float[] subX, subY, subZ;

        public Vector2 ToWorld(float x, float z) => new Vector2(x * Scale - Offset.x, z * Scale - Offset.y);

        public void Build()
        {
            int n = Layout.Length;
            var raw = new Vector3[n];
            for (int i = 0; i < n; i++) raw[i] = new Vector3(Layout[i].x, Layout[i].h, Layout[i].z);

            // 1) Catmull-Rom centrípeta fechada
            var dense = new List<Vector3>();
            const int sub = 40;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = raw[(i - 1 + n) % n], p1 = raw[i], p2 = raw[(i + 1) % n], p3 = raw[(i + 2) % n];
                for (int j = 0; j < sub; j++) dense.Add(CatmullRom(p0, p1, p2, p3, j / (float)sub));
            }

            // 2) escala horizontal para 4.309 m e centraliza
            float len2d = 0f;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector3 a = dense[i], b = dense[(i + 1) % dense.Count];
                len2d += new Vector2(b.x - a.x, b.z - a.z).magnitude;
            }
            Scale = TargetLength / len2d;
            float minx = float.MaxValue, maxx = float.MinValue, minz = float.MaxValue, maxz = float.MinValue;
            foreach (var p in dense)
            {
                minx = Mathf.Min(minx, p.x * Scale); maxx = Mathf.Max(maxx, p.x * Scale);
                minz = Mathf.Min(minz, p.z * Scale); maxz = Mathf.Max(maxz, p.z * Scale);
            }
            Offset = new Vector2((minx + maxx) * 0.5f, (minz + maxz) * 0.5f);
            for (int i = 0; i < dense.Count; i++)
            {
                var p = dense[i];
                dense[i] = new Vector3(p.x * Scale - Offset.x, p.y, p.z * Scale - Offset.y);
            }

            // 3) reamostra com espaçamento uniforme (~2 m)
            int m = dense.Count;
            var cum = new float[m + 1];
            for (int i = 0; i < m; i++) cum[i + 1] = cum[i] + Vector3.Distance(dense[i], dense[(i + 1) % m]);
            float L = cum[m];
            Count = Mathf.RoundToInt(L / 2f);
            float step = L / Count;
            P = new Vector3[Count];
            int seg = 0;
            for (int k = 0; k < Count; k++)
            {
                float target = k * step;
                while (seg < m - 1 && cum[seg + 1] < target) seg++;
                float t = Mathf.InverseLerp(cum[seg], cum[seg + 1], target);
                P[k] = Vector3.Lerp(dense[seg], dense[(seg + 1) % m], t);
            }

            // 4) suaviza (horizontal leve, altura forte)
            for (int pass = 0; pass < 6; pass++) SmoothXZ();
            for (int pass = 0; pass < 40; pass++) SmoothY();

            // 5) distâncias, frames, curvatura
            Dist = new float[Count];
            for (int i = 1; i < Count; i++) Dist[i] = Dist[i - 1] + Vector3.Distance(P[i - 1], P[i]);
            Length = Dist[Count - 1] + Vector3.Distance(P[Count - 1], P[0]);
            Step = Length / Count;

            F = new Vector3[Count];
            R = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                F[i] = (P[(i + 1) % Count] - P[(i - 1 + Count) % Count]).normalized;
                R[i] = Vector3.Cross(Vector3.up, F[i]).normalized;
            }

            Curv = new float[Count];
            const int kk = 4;
            for (int i = 0; i < Count; i++)
            {
                Vector3 a = Flat(F[(i - kk + Count) % Count]), b = Flat(F[(i + kk) % Count]);
                Curv[i] = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad / (2f * kk * Step);
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var c2 = new float[Count];
                for (int i = 0; i < Count; i++)
                    c2[i] = (Curv[(i - 1 + Count) % Count] + 2f * Curv[i] + Curv[(i + 1) % Count]) * 0.25f;
                Curv = c2;
            }

            ComputeRacingLine(2.0f);
            SpeedTarget = SpeedFor(1f);

            // limites
            MinY = float.MaxValue; MaxY = float.MinValue;
            MinXZ = new Vector2(float.MaxValue, float.MaxValue);
            MaxXZ = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in P)
            {
                MinY = Mathf.Min(MinY, p.y); MaxY = Mathf.Max(MaxY, p.y);
                MinXZ = Vector2.Min(MinXZ, new Vector2(p.x, p.z));
                MaxXZ = Vector2.Max(MaxXZ, new Vector2(p.x, p.z));
            }

            // nomes dos trechos
            NamedPoints.Clear();
            foreach (var cp in Layout)
            {
                if (cp.name == null) continue;
                Vector2 w = ToWorld(cp.x, cp.z);
                NamedPoints.Add(new KeyValuePair<int, string>(FindNearest(new Vector3(w.x, cp.h, w.y), -1), cp.name));
            }
            NamedPoints.Sort((a, b) => a.Key.CompareTo(b.Key));
            Section = new string[Count];
            for (int i = 0; i < Count; i++)
            {
                string s = NamedPoints[NamedPoints.Count - 1].Value;
                foreach (var np in NamedPoints) if (np.Key <= i) s = np.Value;
                Section[i] = s;
            }
            BuildDrsZones();
        }

        int Named(string n)
        {
            foreach (var np in NamedPoints) if (np.Value == n) return np.Key;
            return 0;
        }

        public int Wrap(int i) => ((i % Count) + Count) % Count;
        public bool InSpan(int i, int a, int b) => Wrap(i - a) <= Wrap(b - a);

        /// <summary>
        /// Zonas de DRS de Interlagos: Reta dos Boxes (detecção na Junção) e Reta Oposta (detecção no S do Senna).
        /// </summary>
        void BuildDrsZones()
        {
            DrsZones.Clear();
            int senna = Named("S do Senna"), sol = Named("Curva do Sol"), oposta = Named("Reta Oposta"),
                lago = Named("Descida do Lago"), juncao = Named("Junção"), subida = Named("Subida dos Boxes");
            DrsZones.Add(new DrsZone { name = "Reta dos Boxes", detect = juncao, start = Wrap(subida + 40), end = Wrap(senna - 15) });
            DrsZones.Add(new DrsZone { name = "Reta Oposta", detect = Wrap(senna + (Wrap(sol - senna)) * 6 / 10), start = Wrap(oposta - 20), end = Wrap(lago - 10) });
        }

        /// <summary>
        /// Linha ideal de corrida: relaxação iterativa que minimiza a curvatura (alarga as curvas usando a largura da pista),
        /// em várias escalas, limitada a |deslocamento| &lt;= HalfWidth - margem.
        /// </summary>
        void ComputeRacingLine(float margin)
        {
            int N = Count;
            float lim = HalfWidth - margin;
            var o = new float[N];
            var nx = new float[N];
            var qx = new float[N];
            var qz = new float[N];
            int[] spans = { 8, 4, 2, 1 };
            foreach (int span in spans)
                for (int it = 0; it < 350; it++)
                {
                    for (int i = 0; i < N; i++) { qx[i] = P[i].x + R[i].x * o[i]; qz[i] = P[i].z + R[i].z * o[i]; }
                    for (int i = 0; i < N; i++)
                    {
                        int a = Wrap(i - span), c = Wrap(i + span);
                        float mx = (qx[a] + qx[c]) * 0.5f - qx[i], mz = (qz[a] + qz[c]) * 0.5f - qz[i];
                        nx[i] = Mathf.Clamp(o[i] + 0.5f * (mx * R[i].x + mz * R[i].z), -lim, lim);
                    }
                    var t = o; o = nx; nx = t;
                }
            RaceOff = o;
            RaceP = new Vector3[N];
            RaceF = new Vector3[N];
            for (int i = 0; i < N; i++) RaceP[i] = P[i] + R[i] * o[i];
            for (int i = 0; i < N; i++) RaceF[i] = (RaceP[Wrap(i + 1)] - RaceP[Wrap(i - 1)]).normalized;
            RaceCurv = new float[N];
            const int kk = 4;
            for (int i = 0; i < N; i++)
            {
                Vector3 a = Flat(RaceF[Wrap(i - kk)]), b = Flat(RaceF[Wrap(i + kk)]);
                float ds = (RaceP[Wrap(i + kk)] - RaceP[Wrap(i - kk)]).magnitude;
                RaceCurv[i] = Vector3.SignedAngle(a, b, Vector3.up) * Mathf.Deg2Rad / Mathf.Max(ds, 0.1f);
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var c2 = new float[N];
                for (int i = 0; i < N; i++) c2[i] = (RaceCurv[Wrap(i - 1)] + 2f * RaceCurv[i] + RaceCurv[Wrap(i + 1)]) * 0.25f;
                RaceCurv = c2;
            }
        }

        /// <summary>
        /// Velocidade-alvo na linha ideal usando uma fração da aderência (gripFactor; 1 = limite do carro):
        /// limite de curva v²·|k| = f·Grip(v) (resolvido por bisseção) e depois a passada de frenagem para trás.
        /// </summary>
        public float[] SpeedFor(float gripFactor)
        {
            int key = Mathf.RoundToInt(gripFactor * 1000f);
            if (speedCache.TryGetValue(key, out var cached)) return cached;
            const float vmax = 100f, mass = F1Spec.Mass + 30f;
            int N = Count;
            var st = new float[N];
            for (int i = 0; i < N; i++)
            {
                float k = Mathf.Abs(RaceCurv[i]);
                float lo = 0f, hi = vmax;
                for (int it = 0; it < 30; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (mid * mid * k <= gripFactor * F1Spec.Grip(mid, false, mass)) lo = mid; else hi = mid;
                }
                st[i] = lo;
            }
            for (int pass = 0; pass < 2; pass++)
                for (int i = N - 1; i >= 0; i--)
                {
                    int j = Wrap(i + 1);
                    float next = st[j];
                    float lat = next * next * Mathf.Abs(RaceCurv[j]);
                    float g = gripFactor * F1Spec.Grip(next, false, mass);
                    float rem = Mathf.Sqrt(Mathf.Max(0f, 1f - (lat / g) * (lat / g)));
                    float decel = g * Mathf.Max(rem, 0.3f) + F1Spec.DragAccel(next, mass) + F1Spec.Rolling;
                    float ds = (RaceP[j] - RaceP[i]).magnitude;
                    float lim = Mathf.Sqrt(next * next + 2f * decel * ds);
                    if (st[i] > lim) st[i] = lim;
                }
            speedCache[key] = st;
            return st;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }

        void SmoothXZ()
        {
            var q = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                Vector3 a = P[(i - 1 + Count) % Count], b = P[i], c = P[(i + 1) % Count];
                q[i] = new Vector3((a.x + 2f * b.x + c.x) * 0.25f, b.y, (a.z + 2f * b.z + c.z) * 0.25f);
            }
            P = q;
        }

        void SmoothY()
        {
            var q = new Vector3[Count];
            for (int i = 0; i < Count; i++)
            {
                float y = (P[(i - 2 + Count) % Count].y + P[(i - 1 + Count) % Count].y + P[i].y + P[(i + 1) % Count].y + P[(i + 2) % Count].y) * 0.2f;
                q[i] = new Vector3(P[i].x, y, P[i].z);
            }
            P = q;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float t0 = 0f;
            float t1 = t0 + Mathf.Sqrt(Vector3.Distance(p0, p1));
            float t2 = t1 + Mathf.Sqrt(Vector3.Distance(p1, p2));
            float t3 = t2 + Mathf.Sqrt(Vector3.Distance(p2, p3));
            float t = Mathf.Lerp(t1, t2, u);
            Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        /// <summary>Índice da amostra mais próxima. Com hint &gt;= 0 procura só numa janela em volta.</summary>
        public int FindNearest(Vector3 pos, int hint, int window = 60)
        {
            int best = 0;
            float bd = float.MaxValue;
            if (hint < 0)
            {
                for (int i = 0; i < Count; i++)
                {
                    float d = (P[i] - pos).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                return best;
            }
            for (int o = -window; o <= window; o++)
            {
                int i = ((hint + o) % Count + Count) % Count;
                float d = (P[i] - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void EnsureSub()
        {
            if (subX != null) return;
            int n = Count / 3;
            subX = new float[n]; subY = new float[n]; subZ = new float[n];
            for (int k = 0; k < n; k++) { var p = P[k * 3]; subX[k] = p.x; subY[k] = p.y; subZ[k] = p.z; }
        }

        public float NearestDistXZ(float x, float z)
        {
            EnsureSub();
            float dn = float.MaxValue;
            for (int k = 0; k < subX.Length; k++)
            {
                float dx = x - subX[k], dz = z - subZ[k];
                float d2 = dx * dx + dz * dz;
                if (d2 < dn) dn = d2;
            }
            return Mathf.Sqrt(dn);
        }

        /// <summary>Altura do terreno: acompanha a pista (sempre um pouco abaixo dela) e suaviza longe dela.</summary>
        public float GroundHeight(float x, float z)
        {
            EnsureSub();
            float ws = 0f, hs = 0f, dn = float.MaxValue, hn = 0f;
            for (int k = 0; k < subX.Length; k++)
            {
                float dx = x - subX[k], dz = z - subZ[k];
                float d2 = dx * dx + dz * dz;
                if (d2 < dn) { dn = d2; hn = subY[k]; }
                float w = 1f / (d2 + 900f);
                w *= w;
                ws += w;
                hs += w * subY[k];
            }
            float idw = hs / ws;
            float d = Mathf.Sqrt(dn);
            float under = Mathf.Min(idw, hn - 0.45f);
            float near = WallOffset + 3f, far = WallOffset + 60f;
            if (d <= near) return under;
            float tt = Mathf.InverseLerp(near, far, d);
            tt = tt * tt * (3f - 2f * tt);
            return Mathf.Lerp(under, idw, tt);
        }
    }
}
