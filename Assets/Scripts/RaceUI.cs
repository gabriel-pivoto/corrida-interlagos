using UnityEngine;

namespace Corrida
{
    /// <summary>Cores e utilitários de desenho da interface (estilo transmissão de F1).</summary>
    public static class UI
    {
        public static readonly Color Bg = new Color(0.055f, 0.06f, 0.075f, 0.9f);
        public static readonly Color Bg2 = new Color(0.11f, 0.12f, 0.14f, 0.92f);
        public static readonly Color Bg3 = new Color(0.17f, 0.18f, 0.21f, 0.95f);
        public static readonly Color Red = new Color(0.88f, 0.03f, 0.0f);
        public static readonly Color Grey = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color Dim = new Color(0.4f, 0.42f, 0.46f);
        public static readonly Color Purple = new Color(0.7f, 0.35f, 1f);
        public static readonly Color Green = new Color(0.2f, 0.85f, 0.35f);
        public static readonly Color Yellow = new Color(1f, 0.82f, 0.12f);
        public static readonly Color Gold = new Color(0.95f, 0.75f, 0.2f);
        public static readonly Color Silver = new Color(0.75f, 0.77f, 0.8f);
        public static readonly Color Bronze = new Color(0.8f, 0.5f, 0.28f);

        static Font regular, bold;
        static bool boldIsReal;
        static GUIStyle style;
        static Texture2D gradV, gradH;

        static void Init()
        {
            if (style != null) return;
            var installed = new System.Collections.Generic.HashSet<string>(Font.GetOSInstalledFontNames());
            regular = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Arial" }, 32);
            boldIsReal = installed.Contains("Bahnschrift SemiBold");
            bold = boldIsReal ? Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift SemiBold", "Bahnschrift", "Arial" }, 32) : regular;
            style = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = true, clipping = TextClipping.Overflow };
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);

            gradV = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) gradV.SetPixel(0, i, new Color(1f, 1f, 1f, i / 63f));
            gradV.Apply();
            gradH = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) gradH.SetPixel(i, 0, new Color(1f, 1f, 1f, 1f - i / 63f));
            gradH.Apply();
        }

        static void Prep(float size, TextAnchor a, bool b)
        {
            Init();
            style.font = b ? bold : regular;
            style.fontStyle = b && !boldIsReal ? FontStyle.Bold : FontStyle.Normal;
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            style.alignment = a;
        }

        public static void Text(Rect r, string s, float size, TextAnchor a, Color c, bool b = true, bool shadow = false)
        {
            Prep(size, a, b);
            if (shadow)
            {
                style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * c.a);
                float o = Mathf.Max(1f, size * 0.05f);
                GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), s, style);
            }
            style.normal.textColor = c;
            GUI.Label(r, s, style);
        }

        public static float Width(string s, float size, bool b = true)
        {
            Prep(size, TextAnchor.MiddleLeft, b);
            return style.CalcSize(new GUIContent(s)).x;
        }

        public static void Box(Rect r, Color c, float radius = 0f)
        {
            if (radius <= 0f) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; return; }
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        /// <summary>Retângulo com cantos diferentes (tl, tr, br, bl).</summary>
        public static void Box(Rect r, Color c, Vector4 radii) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, radii);

        public static void Outline(Rect r, Color c, float width, float radius) =>
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);

        /// <summary>Degradê vertical: transparente em cima, cor embaixo (bottom = true) ou o contrário.</summary>
        public static void GradV(Rect r, Color c, bool bottom)
        {
            Init();
            GUI.color = c;
            if (bottom) GUI.DrawTexture(r, gradV);
            else GUI.DrawTextureWithTexCoords(r, gradV, new Rect(0f, 1f, 1f, -1f));
            GUI.color = Color.white;
        }

        /// <summary>Degradê horizontal: cor à esquerda, transparente à direita.</summary>
        public static void GradH(Rect r, Color c)
        {
            Init();
            GUI.color = c;
            GUI.DrawTexture(r, gradH);
            GUI.color = Color.white;
        }

        public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        public static string Time(float t)
        {
            if (t < 0f) return "--:--.---";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return m + ":" + s.ToString("00.000");
        }

        public static string Gap(float t)
        {
            if (t < 60f) return "+" + t.ToString("0.000");
            return "+" + Time(t);
        }
    }

    public partial class RaceManager
    {
        int menuSel = 4, pauseSel, resultSel;
        Vector2 lastMouse;
        bool mouseMoved;

        static readonly string[] MenuLabels = { "JOGADORES", "ADVERSÁRIOS", "VOLTAS", "DIFICULDADE" };

        public static string Fmt(float t) => UI.Time(t);

        string MakeTag(RaceCar rc)
        {
            if (rc.IsHuman) return humans == 1 ? "VOCÊ" : "JG" + (rc.player + 1);
            string n = rc.name;
            int dot = n.IndexOf(". ");
            string sur = dot >= 0 ? n.Substring(dot + 2) : n;
            return sur.Substring(0, Mathf.Min(3, sur.Length)).ToUpperInvariant();
        }

        // ================================================================ entrada nos menus (Update)

        void MenuInput()
        {
            var m = GameInput.MenuMove();
            if (m.y != 0) menuSel = (menuSel + m.y + 6) % 6;
            if (m.x != 0 && menuSel < 4) MenuChange(menuSel, m.x);
            if (GameInput.MenuConfirm()) MenuActivate(menuSel);
        }

        void MenuChange(int item, int d)
        {
            switch (item)
            {
                case 0: humans = Mathf.Clamp(humans + d, 1, 2); aiCount = Mathf.Min(aiCount, 8 - humans); break;
                case 1: aiCount = Mathf.Clamp(aiCount + d, 0, 8 - humans); break;
                case 2: laps = Mathf.Clamp(laps + d, 1, 71); break;
                case 3: difficulty = Mathf.Clamp(difficulty + d, 0, 2); break;
            }
        }

        void MenuActivate(int item)
        {
            if (item == 5) Quit();
            else StartRace();
        }

        void PauseInput()
        {
            var m = GameInput.MenuMove();
            if (m.y != 0) pauseSel = (pauseSel + m.y + 3) % 3;
            if (GameInput.MenuConfirm()) PauseActivate(pauseSel);
        }

        void PauseActivate(int i)
        {
            if (i == 0) SetPaused(false);
            else if (i == 1) StartRace();
            else GoMenu();
        }

        void ResultsInput()
        {
            var m = GameInput.MenuMove();
            if (m.x != 0 || m.y != 0) resultSel = 1 - resultSel;
            if (GameInput.MenuConfirm()) { if (resultSel == 0) StartRace(); else GoMenu(); }
        }

        // ================================================================ desenho

        void OnGUI()
        {
            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float W = Screen.width / s, H = 1080f;

            if (Event.current.type == EventType.Repaint)
            {
                Vector2 mp = Event.current.mousePosition;
                mouseMoved = (mp - lastMouse).sqrMagnitude > 1f;
                lastMouse = mp;
            }

            if (state == RaceState.Menu)
            {
                DrawMenu(W, H);
                return;
            }
            foreach (var rc in cars)
                if (rc.IsHuman) DrawHud(rc, ViewRect(rc.player, W, H));
            if (humans == 2) UI.Box(new Rect(0f, H * 0.5f - 2f, W, 4f), new Color(0.02f, 0.02f, 0.03f, 1f));
            if (state == RaceState.Finished) DrawResults(W, H);
            else if (paused) DrawPause(W, H);
        }

        bool Hover(Rect r) => mouseMoved && r.Contains(Event.current.mousePosition);
        static bool Click(Rect r) => GUI.Button(r, GUIContent.none, GUIStyle.none);

        Rect ViewRect(int p, float W, float H)
        {
            if (humans == 1) return new Rect(0f, 0f, W, H);
            return p == 0 ? new Rect(0f, 0f, W, H * 0.5f) : new Rect(0f, H * 0.5f, W, H * 0.5f);
        }

        // ---------------------------------------------------------------- HUD da corrida

        void DrawHud(RaceCar rc, Rect v)
        {
            float k = Mathf.Clamp(v.height / 1080f, 0.62f, 1f);
            float pad = 22f * k;

            DrawTower(rc, new Vector2(v.x + pad, v.y + pad), k, v.height - 2f * pad);
            DrawSection(rc, v, k, pad);
            DrawMap(rc, v, k, pad);
            DrawTimes(rc, v, k, pad);
            DrawDash(rc, v, k, pad);
            DrawCenter(rc, v, k);
        }

        void DrawTower(RaceCar me, Vector2 o, float k, float maxH)
        {
            float w = 310f * k, hh = 50f * k;
            float rh = Mathf.Min(36f * k, (maxH * 0.62f - hh) / Mathf.Max(order.Count, 1));
            // cabeçalho
            UI.Box(new Rect(o.x, o.y, w, hh), UI.Bg, new Vector4(8f * k, 8f * k, 0f, 0f));
            UI.Box(new Rect(o.x, o.y, 8f * k, hh), UI.Red, new Vector4(8f * k, 0f, 0f, 0f));
            string head;
            if (state == RaceState.Finished || (me.finished && state == RaceState.Racing)) head = "<b>FINAL</b>";
            else head = $"VOLTA  <b><color=#ffffff>{Mathf.Clamp(me.lap, 1, laps)}</color></b><color=#9a9ea6>/{laps}</color>";
            UI.Text(new Rect(o.x + 22f * k, o.y, w, hh), head, 26f * k, TextAnchor.MiddleLeft, UI.Grey);
            UI.Text(new Rect(o.x, o.y, w - 14f * k, hh), "INTERLAGOS", 15f * k, TextAnchor.MiddleRight, UI.Dim);

            float y = o.y + hh;
            for (int i = 0; i < order.Count; i++)
            {
                var c = order[i];
                bool last = i == order.Count - 1;
                Rect r = new Rect(o.x, y, w, rh);
                bool mine = c == me, human = c.IsHuman;
                Color bg = mine ? new Color(0.95f, 0.95f, 0.96f, 0.95f) : (i % 2 == 0 ? UI.Bg2 : UI.Bg);
                UI.Box(r, bg, last ? new Vector4(0f, 0f, 8f * k, 8f * k) : Vector4.zero);
                Color fg = mine ? new Color(0.05f, 0.05f, 0.07f) : Color.white;
                Color fg2 = mine ? new Color(0.25f, 0.25f, 0.3f) : UI.Grey;

                UI.Text(new Rect(r.x, r.y, 44f * k, rh), (i + 1).ToString(), 21f * k, TextAnchor.MiddleCenter, fg);
                UI.Box(new Rect(r.x + 46f * k, r.y + rh * 0.18f, 5f * k, rh * 0.64f), c.color, 2f * k);
                string tag = c.tag ?? "";
                UI.Text(new Rect(r.x + 60f * k, r.y, 120f * k, rh), tag, 21f * k, TextAnchor.MiddleLeft, human && !mine ? UI.Yellow : fg);

                string gap;
                Color gc = fg2;
                if (i == 0) gap = c.finished ? "VENCEDOR" : "LÍDER";
                else
                {
                    float g = GapToLeader(c, out int behind);
                    gap = behind > 0 ? $"+{behind} VOLTA{(behind > 1 ? "S" : "")}" : (state == RaceState.Countdown ? "" : UI.Gap(g));
                }
                if (c.finished && i > 0) gc = fg;
                UI.Text(new Rect(r.x, r.y, w - 12f * k, rh), gap, 18f * k, TextAnchor.MiddleRight, gc, false);

                // DRS aberto / volta mais rápida
                float tx = r.x + 60f * k + UI.Width(tag, 21f * k) + 8f * k;
                if (c.car.DrsOpen)
                {
                    Rect pr = new Rect(tx, r.y + rh * 0.22f, 38f * k, rh * 0.56f);
                    UI.Box(pr, UI.Green, 3f * k);
                    UI.Text(pr, "DRS", 13f * k, TextAnchor.MiddleCenter, Color.black);
                    tx += 44f * k;
                }
                if (sessionBestCar == c)
                {
                    Rect pr = new Rect(tx, r.y + rh * 0.3f, rh * 0.4f, rh * 0.4f);
                    UI.Box(pr, UI.Purple, rh * 0.2f);
                }
                y += rh;
            }
        }

        void DrawSection(RaceCar rc, Rect v, float k, float pad)
        {
            string s = track.Section[rc.trackIndex].ToUpperInvariant();
            float tw = UI.Width(s, 22f * k) + 44f * k;
            Rect r = new Rect(v.center.x - tw * 0.5f, v.y + pad, tw, 40f * k);
            UI.Box(r, UI.Bg, 6f * k);
            UI.Box(new Rect(r.x, r.y, 6f * k, r.height), UI.Red, new Vector4(6f * k, 0f, 0f, 6f * k));
            UI.Text(new Rect(r.x + 8f * k, r.y, r.width - 8f * k, r.height), s, 22f * k, TextAnchor.MiddleCenter, Color.white);
            // DRS disponível
            if (rc.car.drsAllowed && !rc.car.DrsOpen && state == RaceState.Racing)
            {
                string t = humans == 1 ? "DRS DISPONÍVEL  •  APERTE E" : (rc.player == 0 ? "DRS DISPONÍVEL  •  E" : "DRS DISPONÍVEL  •  0 / ;");
                float w2 = UI.Width(t, 18f * k) + 30f * k;
                Rect d = new Rect(v.center.x - w2 * 0.5f, r.yMax + 8f * k, w2, 32f * k);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
                UI.Box(d, UI.A(UI.Green, pulse), 6f * k);
                UI.Text(d, t, 18f * k, TextAnchor.MiddleCenter, Color.black);
            }
        }

        void DrawMap(RaceCar rc, Rect v, float k, float pad)
        {
            float ms = Mathf.Min(300f * k, v.height * 0.4f);
            Rect card = new Rect(v.xMax - ms - pad, v.y + pad, ms, ms);
            UI.Box(card, UI.A(UI.Bg, 0.75f), 10f * k);
            Rect mr = new Rect(card.x + 8f * k, card.y + 8f * k, ms - 16f * k, ms - 16f * k);
            GUI.DrawTexture(mr, minimap);
            for (int pass = 0; pass < 2; pass++)
                foreach (var o in cars)
                {
                    bool me = o == rc;
                    if ((pass == 1) != (me || o.IsHuman)) continue;
                    Vector2 m = MapPx(o.car.transform.position) / MapSize;
                    Vector2 c = new Vector2(mr.x + m.x * mr.width, mr.y + (1f - m.y) * mr.height);
                    float r = (me ? 9f : o.IsHuman ? 7.5f : 6f) * k;
                    UI.Box(new Rect(c.x - r - 2f, c.y - r - 2f, (r + 2f) * 2f, (r + 2f) * 2f), me ? Color.white : new Color(0f, 0f, 0f, 0.85f), r + 2f);
                    UI.Box(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), o.color, r);
                }
            UI.Text(new Rect(card.x + 12f * k, card.yMax - 30f * k, ms, 24f * k), "<color=#46dc6e>■</color> DRS", 15f * k, TextAnchor.MiddleLeft, UI.Grey, false);
        }

        void DrawTimes(RaceCar rc, Rect v, float k, float pad)
        {
            float h = 150f * k, pw = 140f * k, tw = 310f * k;
            Rect r = new Rect(v.x + pad, v.yMax - pad - h, pw + tw, h);
            // posição
            Rect pr = new Rect(r.x, r.y, pw, h);
            UI.Box(pr, UI.Red, new Vector4(10f * k, 0f, 0f, 10f * k));
            UI.Text(new Rect(pr.x, pr.y + 8f * k, pw, 26f * k), "POSIÇÃO", 16f * k, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            UI.Text(new Rect(pr.x, pr.y + 26f * k, pw, 90f * k), "P" + rc.position, 70f * k, TextAnchor.MiddleCenter, Color.white);
            UI.Text(new Rect(pr.x, pr.yMax - 36f * k, pw, 28f * k), "de " + cars.Count, 18f * k, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f), false);
            // tempos
            Rect tr = new Rect(r.x + pw, r.y, tw, h);
            UI.Box(tr, UI.Bg, new Vector4(0f, 10f * k, 10f * k, 0f));
            float cur = state == RaceState.Countdown || rc.lap < 1 ? 0f : (rc.finished ? rc.lastLap : raceTime - rc.lapStart);
            Color best = rc.bestLap > 0f && Mathf.Approximately(rc.bestLap, sessionBest) ? UI.Purple : (rc.bestLap > 0f ? UI.Green : Color.white);
            Color lastC = rc.lastLap < 0f ? Color.white : Mathf.Approximately(rc.lastLap, sessionBest) ? UI.Purple : Mathf.Approximately(rc.lastLap, rc.bestLap) ? UI.Green : UI.Yellow;
            string[] labels = { "ATUAL", "ÚLTIMA", "MELHOR" };
            string[] vals = { UI.Time(cur), UI.Time(rc.lastLap), UI.Time(rc.bestLap) };
            Color[] cols = { Color.white, lastC, best };
            float rowH = (h - 16f * k) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Rect row = new Rect(tr.x + 18f * k, tr.y + 8f * k + i * rowH, tw - 36f * k, rowH);
                UI.Text(row, labels[i], 16f * k, TextAnchor.MiddleLeft, UI.Grey);
                UI.Text(row, vals[i], (i == 0 ? 34f : 28f) * k, TextAnchor.MiddleRight, cols[i]);
            }
        }

        void DrawDash(RaceCar rc, Rect v, float k, float pad)
        {
            var car = rc.car;
            float w = 560f * k, h = 180f * k;
            Rect r = new Rect(v.xMax - pad - w, v.yMax - pad - h, w, h);
            UI.Box(r, UI.Bg, 12f * k);
            float ip = 14f * k;

            // luzes de giro (verde, vermelho, roxo) e piscando no corte
            const int leds = 15;
            float frac = Mathf.InverseLerp(F1Spec.IdleRpm + 3000f, F1Spec.ShiftRpm, car.Rpm);
            bool limiter = car.Rpm >= F1Spec.ShiftRpm * 0.995f && Mathf.Repeat(Time.unscaledTime * 10f, 1f) > 0.5f;
            float lw = (w - 2f * ip) / leds;
            for (int i = 0; i < leds; i++)
            {
                float on = (i + 0.5f) / leds;
                Color c = i < 5 ? UI.Green : i < 10 ? new Color(1f, 0.15f, 0.12f) : new Color(0.45f, 0.4f, 1f);
                bool lit = frac >= on && !limiter;
                Rect lr = new Rect(r.x + ip + i * lw + 2f * k, r.y + ip, lw - 4f * k, 12f * k);
                UI.Box(lr, lit ? c : new Color(0.2f, 0.21f, 0.24f, 1f), 4f * k);
            }

            // velocidade
            float y0 = r.y + ip + 18f * k;
            UI.Text(new Rect(r.x + ip, y0 - 6f * k, 250f * k, 100f * k), Mathf.RoundToInt(car.SpeedKmh).ToString(), 92f * k, TextAnchor.MiddleLeft, Color.white);
            UI.Text(new Rect(r.x + ip + 4f * k, y0 + 86f * k, 120f * k, 24f * k), "KM/H", 16f * k, TextAnchor.MiddleLeft, UI.Grey);

            // marcha
            Rect gr = new Rect(r.xMax - ip - 104f * k, y0, 104f * k, 112f * k);
            UI.Box(gr, UI.Bg3, 8f * k);
            UI.Text(new Rect(gr.x, gr.y + 2f * k, gr.width, 22f * k), "MARCHA", 13f * k, TextAnchor.MiddleCenter, UI.Grey);
            UI.Text(new Rect(gr.x, gr.y + 14f * k, gr.width, gr.height - 14f * k), car.ForwardSpeed < -0.5f ? "R" : car.Gear.ToString(), 70f * k, TextAnchor.MiddleCenter, Color.white);

            // coluna do meio: DRS, ERS, combustível, giro, G
            float mx = r.x + 262f * k, mw = gr.x - mx - 12f * k;
            Rect dr = new Rect(mx, y0 + 4f * k, 84f * k, 34f * k);
            bool open = car.DrsOpen, can = car.drsAllowed;
            UI.Box(dr, open ? UI.Green : can ? new Color(0.1f, 0.35f, 0.15f) : UI.Bg3, 6f * k);
            if (can && !open) UI.Outline(dr, UI.Green, 2f * k, 6f * k);
            UI.Text(dr, "DRS", 20f * k, TextAnchor.MiddleCenter, open ? Color.black : can ? Color.white : UI.Dim);

            float ers = Mathf.Clamp01(car.ersEnergy / F1Spec.ErsPerLap);
            Rect el = new Rect(dr.xMax + 12f * k, y0 + 2f * k, mw - 96f * k, 16f * k);
            UI.Text(el, car.ErsDeploying ? "<color=#ffd21f>ERS ▲</color>" : "ERS", 14f * k, TextAnchor.MiddleLeft, UI.Grey);
            Rect eb = new Rect(el.x, el.yMax + 4f * k, el.width, 12f * k);
            UI.Box(eb, UI.Bg3, 4f * k);
            if (ers > 0.01f) UI.Box(new Rect(eb.x, eb.y, eb.width * ers, eb.height), car.ErsDeploying ? UI.Yellow : new Color(0.95f, 0.6f, 0.1f), 4f * k);

            float ly = y0 + 52f * k;
            Rect s1 = new Rect(mx, ly, mw, 26f * k), s2 = new Rect(mx, ly + 28f * k, mw, 26f * k);
            UI.Text(s1, "COMB", 15f * k, TextAnchor.MiddleLeft, UI.Grey);
            UI.Text(s1, $"{car.fuel:0.0} kg", 20f * k, TextAnchor.MiddleRight, Color.white);
            UI.Text(s2, "RPM", 15f * k, TextAnchor.MiddleLeft, UI.Grey);
            UI.Text(s2, $"{car.Rpm:0}", 20f * k, TextAnchor.MiddleRight, Color.white);
            UI.Text(new Rect(r.x + ip + 70f * k, y0 + 86f * k, 120f * k, 24f * k), $"{car.GForce:0.0} G", 16f * k, TextAnchor.MiddleLeft, new Color(0.7f, 0.85f, 1f));
            if (car.OnGrass) UI.Text(new Rect(r.x, r.y - 34f * k, w, 30f * k), "FORA DA PISTA", 20f * k, TextAnchor.MiddleRight, UI.Yellow, true, true);
        }

        void Banner(Rect v, float k, float y, string title, string sub, Color accent, float alpha = 1f)
        {
            float tw = Mathf.Max(UI.Width(title, 40f * k), sub != null ? UI.Width(sub, 26f * k) : 0f) + 80f * k;
            float h = sub != null ? 100f * k : 66f * k;
            Rect r = new Rect(v.center.x - tw * 0.5f, y, tw, h);
            UI.Box(r, UI.A(UI.Bg, alpha), 10f * k);
            UI.Box(new Rect(r.x, r.y, 10f * k, h), UI.A(accent, alpha), new Vector4(10f * k, 0f, 0f, 10f * k));
            UI.Text(new Rect(r.x + 10f * k, r.y + 6f * k, tw - 10f * k, 54f * k), title, 40f * k, TextAnchor.MiddleCenter, UI.A(accent, alpha));
            if (sub != null) UI.Text(new Rect(r.x + 10f * k, r.y + 54f * k, tw - 10f * k, 38f * k), sub, 28f * k, TextAnchor.MiddleCenter, UI.A(Color.white, alpha));
        }

        void DrawCenter(RaceCar rc, Rect v, float k)
        {
            float y = v.y + v.height * 0.2f;
            // luzes de largada: 5 colunas que acendem uma por segundo e apagam juntas
            if (state == RaceState.Countdown || goTimer > 0f)
            {
                float elapsed = countdownTotal - countdown;
                int lit = state == RaceState.Countdown ? Mathf.Clamp(Mathf.FloorToInt(elapsed), 0, 5) : 0;
                float alpha = state == RaceState.Countdown ? 1f : Mathf.Clamp01(goTimer / 0.6f);
                float pw = 74f * k, ph = 170f * k, gap = 16f * k;
                float total = 5f * pw + 4f * gap;
                float x0 = v.center.x - total * 0.5f;
                UI.Box(new Rect(x0 - 20f * k, y + 30f * k, total + 40f * k, 18f * k), UI.A(new Color(0.08f, 0.08f, 0.09f), alpha), 4f * k);
                for (int i = 0; i < 5; i++)
                {
                    Rect pod = new Rect(x0 + i * (pw + gap), y, pw, ph);
                    UI.Box(pod, UI.A(new Color(0.07f, 0.07f, 0.08f, 0.96f), alpha), 12f * k);
                    for (int j = 0; j < 4; j++)
                    {
                        float d = pw * 0.62f;
                        Rect c = new Rect(pod.center.x - d * 0.5f, pod.y + 10f * k + j * (ph - 20f * k) / 4f + ((ph - 20f * k) / 4f - d) * 0.5f, d, d);
                        bool red = j >= 2 && i < lit;
                        UI.Box(c, UI.A(red ? new Color(1f, 0.08f, 0.05f) : new Color(0.17f, 0.17f, 0.19f), alpha), d * 0.5f);
                        if (red) UI.Box(new Rect(c.x + d * 0.2f, c.y + d * 0.15f, d * 0.3f, d * 0.25f), UI.A(new Color(1f, 0.6f, 0.5f, 0.6f), alpha), d * 0.15f);
                    }
                }
                if (state == RaceState.Countdown && elapsed < 1f)
                    UI.Text(new Rect(v.x, y + ph + 16f * k, v.width, 40f * k), "PREPARE-SE PARA A LARGADA", 26f * k, TextAnchor.MiddleCenter, Color.white, true, true);
                return;
            }

            if (rc.finished)
            {
                if (state != RaceState.Finished)
                    Banner(v, k, y, "BANDEIRADA", $"Você terminou em {rc.position}º  •  {UI.Time(rc.finishTime)}", UI.Yellow);
                return;
            }
            if (rc.wrongWayTimer > 1.5f) { Banner(v, k, y, "CONTRAMÃO", "Dê meia-volta", new Color(1f, 0.25f, 0.2f)); return; }
            if (rc.lapFlash > 0f && rc.lapFlashText != null)
            {
                float a = Mathf.Clamp01(rc.lapFlash / 0.5f);
                string sub = UI.Time(rc.lastLap);
                if (sessionBest > 0f && rc.lastLap > sessionBest + 0.0005f) sub += "   " + UI.Gap(rc.lastLap - sessionBest);
                Banner(v, k, y, rc.lapFlashText, sub, rc.lapFlashColor, a);
                return;
            }
            if (rc.lap == laps && rc.lap > 0 && raceTime - rc.lapStart < 3f)
                Banner(v, k, y, laps == 1 ? "VOLTA ÚNICA" : "ÚLTIMA VOLTA", null, Color.white);
        }

        // ---------------------------------------------------------------- menu principal

        void DrawMenu(float W, float H)
        {
            // fundo: escurece a esquerda (onde fica o menu) e embaixo
            UI.GradH(new Rect(0f, 0f, 1100f, H), new Color(0.02f, 0.02f, 0.03f, 0.92f));
            UI.GradV(new Rect(0f, H - 260f, W, 260f), new Color(0f, 0f, 0f, 0.6f), true);

            float x = 90f, y = 80f, w = 640f;
            UI.Box(new Rect(x - 30f, y + 6f, 10f, 150f), UI.Red, 3f);
            UI.Text(new Rect(x, y, w, 40f), "GRANDE PRÊMIO DE", 30f, TextAnchor.MiddleLeft, UI.Grey);
            UI.Text(new Rect(x - 4f, y + 30f, w + 200f, 110f), "INTERLAGOS", 108f, TextAnchor.MiddleLeft, Color.white);
            UI.Text(new Rect(x, y + 132f, w, 34f), "SÃO PAULO  •  BRASIL  •  FÓRMULA 1  •  REGULAMENTO 2025", 20f, TextAnchor.MiddleLeft, UI.Red);

            y += 210f;
            string[] values =
            {
                humans == 1 ? "1  •  sozinho" : "2  •  tela dividida",
                aiCount == 0 ? "nenhum" : aiCount.ToString(),
                laps.ToString(),
                DiffNames[difficulty]
            };
            float rowH = 66f, gapY = 10f;
            for (int i = 0; i < 4; i++)
            {
                Rect r = new Rect(x, y, w, rowH);
                bool sel = menuSel == i;
                if (Hover(r)) menuSel = i;
                UI.Box(r, sel ? UI.Bg3 : UI.A(UI.Bg, 0.85f), 8f);
                if (sel) UI.Box(new Rect(r.x, r.y, 6f, rowH), UI.Red, new Vector4(8f, 0f, 0f, 8f));
                UI.Text(new Rect(r.x + 28f, r.y, 300f, rowH), MenuLabels[i], 22f, TextAnchor.MiddleLeft, sel ? Color.white : UI.Grey);

                Rect lb = new Rect(r.xMax - 300f, r.y + 11f, 44f, 44f), rb = new Rect(r.xMax - 56f, r.y + 11f, 44f, 44f);
                bool canL = CanChange(i, -1), canR = CanChange(i, 1);
                UI.Box(lb, canL ? (sel ? UI.Red : UI.Bg3) : UI.A(UI.Bg3, 0.4f), 6f);
                UI.Box(rb, canR ? (sel ? UI.Red : UI.Bg3) : UI.A(UI.Bg3, 0.4f), 6f);
                UI.Text(lb, "‹", 34f, TextAnchor.MiddleCenter, canL ? Color.white : UI.Dim);
                UI.Text(rb, "›", 34f, TextAnchor.MiddleCenter, canR ? Color.white : UI.Dim);
                UI.Text(new Rect(lb.xMax, r.y, rb.x - lb.xMax, rowH), values[i], 26f, TextAnchor.MiddleCenter, sel ? Color.white : new Color(0.85f, 0.86f, 0.9f));
                if (Click(lb)) { menuSel = i; MenuChange(i, -1); }
                else if (Click(rb)) { menuSel = i; MenuChange(i, 1); }
                else if (Click(r)) menuSel = i;
                y += rowH + gapY;
            }

            // largada
            y += 14f;
            Rect go = new Rect(x, y, w, 86f);
            if (Hover(go)) menuSel = 4;
            bool gsel = menuSel == 4;
            UI.Box(go, gsel ? UI.Red : new Color(0.6f, 0.03f, 0.02f, 0.95f), 10f);
            if (gsel) UI.Outline(go, Color.white, 3f, 10f);
            UI.Text(go, "CORRER", 40f, TextAnchor.MiddleCenter, Color.white);
            UI.Text(new Rect(go.x, go.y, go.width - 30f, go.height), "›", 54f, TextAnchor.MiddleRight, Color.white);
            if (Click(go)) StartRace();
            y += 100f;

            Rect quit = new Rect(x, y, 200f, 52f);
            if (Hover(quit)) menuSel = 5;
            bool qsel = menuSel == 5;
            UI.Box(quit, qsel ? UI.Bg3 : UI.A(UI.Bg, 0.85f), 8f);
            if (qsel) UI.Outline(quit, Color.white, 2f, 8f);
            UI.Text(quit, "SAIR", 22f, TextAnchor.MiddleCenter, qsel ? Color.white : UI.Grey);
            if (Click(quit)) Quit();

            UI.Text(new Rect(x, H - 70f, 1000f, 30f),
                "<color=#ffffff>↑ ↓</color>  escolher     <color=#ffffff>← →</color>  alterar     <color=#ffffff>Enter</color> / <color=#ffffff>A</color>  correr     ou use o mouse",
                18f, TextAnchor.MiddleLeft, UI.Grey, false);

            DrawCircuitCard(new Rect(W - 90f - 470f, 80f, 470f, 560f));
            DrawControlsCard(new Rect(W - 90f - 470f, 660f, 470f, 330f));
        }

        bool CanChange(int item, int d)
        {
            switch (item)
            {
                case 0: return d < 0 ? humans > 1 : humans < 2;
                case 1: return d < 0 ? aiCount > 0 : aiCount < 8 - humans;
                case 2: return d < 0 ? laps > 1 : laps < 71;
                default: return d < 0 ? difficulty > 0 : difficulty < 2;
            }
        }

        void DrawCircuitCard(Rect r)
        {
            UI.Box(r, UI.A(UI.Bg, 0.88f), 12f);
            UI.Box(new Rect(r.x, r.y, r.width, 6f), UI.Red, new Vector4(12f, 12f, 0f, 0f));
            UI.Text(new Rect(r.x + 24f, r.y + 18f, r.width, 30f), "CIRCUITO", 16f, TextAnchor.MiddleLeft, UI.Red);
            UI.Text(new Rect(r.x + 24f, r.y + 44f, r.width, 36f), "Autódromo José Carlos Pace", 28f, TextAnchor.MiddleLeft, Color.white);
            float ms = 250f;
            GUI.DrawTexture(new Rect(r.center.x - ms * 0.5f, r.y + 88f, ms, ms), minimap);

            string[,] stats =
            {
                { "EXTENSÃO", "4,309 km" }, { "SENTIDO", "anti-horário" },
                { "ZONAS DE DRS", "2" }, { "DESNÍVEL", $"{track.MaxY - track.MinY:0} m" },
                { "POLE 2025", "1:09.511" }, { "SEU RECORDE", record > 0f ? UI.Time(record) : "--:--.---" },
            };
            float sy = r.y + 352f, cw = (r.width - 48f) * 0.5f;
            for (int i = 0; i < 6; i++)
            {
                float sx = r.x + 24f + (i % 2) * cw;
                float yy = sy + (i / 2) * 64f;
                UI.Box(new Rect(sx, yy, cw - 10f, 56f), UI.Bg2, 6f);
                UI.Text(new Rect(sx + 14f, yy + 4f, cw, 22f), stats[i, 0], 14f, TextAnchor.MiddleLeft, UI.Grey);
                UI.Text(new Rect(sx + 14f, yy + 24f, cw, 30f), stats[i, 1], 24f, TextAnchor.MiddleLeft, i == 5 && record > 0f ? UI.Purple : Color.white);
            }
        }

        void Key(ref float x, float y, string k)
        {
            float w = Mathf.Max(34f, UI.Width(k, 15f) + 16f);
            Rect r = new Rect(x, y, w, 30f);
            UI.Box(r, new Color(0.85f, 0.86f, 0.9f), 5f);
            UI.Box(new Rect(r.x, r.yMax - 4f, r.width, 4f), new Color(0.55f, 0.56f, 0.6f), new Vector4(0f, 0f, 5f, 5f));
            UI.Text(new Rect(r.x, r.y - 1f, r.width, r.height - 2f), k, 15f, TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.1f));
            x += w + 6f;
        }

        void ControlLine(float x, float y, string label, params string[] keys)
        {
            UI.Text(new Rect(x, y, 140f, 30f), label, 16f, TextAnchor.MiddleLeft, UI.Grey, false);
            float kx = x + 120f;
            foreach (var k in keys) Key(ref kx, y, k);
        }

        void DrawControlsCard(Rect r)
        {
            UI.Box(r, UI.A(UI.Bg, 0.88f), 12f);
            UI.Text(new Rect(r.x + 24f, r.y + 14f, r.width, 30f), "CONTROLES", 16f, TextAnchor.MiddleLeft, UI.Red);
            float c1 = r.x + 24f, y = r.y + 50f;
            UI.Text(new Rect(c1, y, 200f, 26f), "<color=#e60f0f>■</color> JOGADOR 1", 18f, TextAnchor.MiddleLeft, Color.white);
            UI.Text(new Rect(c1 + 220f, y, 200f, 26f), "<color=#1a4ae6>■</color> JOGADOR 2", 18f, TextAnchor.MiddleLeft, Color.white);
            y += 34f;
            string[] labels = { "Acelerar / frear", "Virar", "DRS", "Freio de mão", "Câmera", "Reposicionar" };
            string[][] p1 = { new[] { "W", "S" }, new[] { "A", "D" }, new[] { "E" }, new[] { "Espaço" }, new[] { "C" }, new[] { "R" } };
            string[][] p2 = { new[] { "↑", "↓" }, new[] { "←", "→" }, new[] { "0", ";" }, new[] { "Shift D" }, new[] { "Ctrl D" }, new[] { "Enter" } };
            for (int i = 0; i < labels.Length; i++)
            {
                UI.Text(new Rect(c1, y, 120f, 30f), labels[i], 14f, TextAnchor.MiddleLeft, UI.Grey, false);
                float kx = c1 + 122f;
                foreach (var k in p1[i]) Key(ref kx, y, k);
                kx = c1 + 300f;
                foreach (var k in p2[i]) Key(ref kx, y, k);
                y += 34f;
            }
            UI.Text(new Rect(c1, r.yMax - 34f, r.width - 48f, 26f), "Esc: pausa   •   Gamepad: RT/LT, X = DRS, Start = pausa", 14f, TextAnchor.MiddleLeft, UI.Dim, false);
        }

        // ---------------------------------------------------------------- pausa

        void DrawPause(float W, float H)
        {
            UI.Box(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.45f));
            float pw = 560f, ph = 400f;
            Rect p = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
            UI.Box(p, UI.Bg, 14f);
            UI.Box(new Rect(p.x, p.y, pw, 6f), UI.Red, new Vector4(14f, 14f, 0f, 0f));
            UI.Text(new Rect(p.x, p.y + 24f, pw, 60f), "PAUSA", 50f, TextAnchor.MiddleCenter, Color.white);
            string[] items = { "CONTINUAR", "REINICIAR CORRIDA", "MENU PRINCIPAL" };
            for (int i = 0; i < 3; i++)
            {
                Rect b = new Rect(p.x + 60f, p.y + 120f + i * 84f, pw - 120f, 68f);
                if (Hover(b)) pauseSel = i;
                bool sel = pauseSel == i;
                UI.Box(b, sel ? UI.Red : UI.Bg3, 8f);
                UI.Text(b, items[i], 26f, TextAnchor.MiddleCenter, Color.white);
                if (Click(b)) PauseActivate(i);
            }
        }

        // ---------------------------------------------------------------- resultado

        void DrawResults(float W, float H)
        {
            UI.Box(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.5f));
            float pw = 1180f, row = 54f;
            float ph = 250f + order.Count * row + (newRecord ? 50f : 0f);
            Rect p = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
            UI.Box(p, UI.Bg, 14f);
            UI.Box(new Rect(p.x, p.y, pw, 6f), UI.Red, new Vector4(14f, 14f, 0f, 0f));
            UI.Text(new Rect(p.x + 40f, p.y + 22f, pw, 56f), "RESULTADO FINAL", 46f, TextAnchor.MiddleLeft, Color.white);
            UI.Text(new Rect(p.x + 40f, p.y + 72f, pw, 30f), $"GRANDE PRÊMIO DE INTERLAGOS  •  {laps} VOLTA{(laps > 1 ? "S" : "")}  •  {DiffNames[difficulty].ToUpperInvariant()}", 18f, TextAnchor.MiddleLeft, UI.Grey);

            float y = p.y + 116f;
            float cPos = p.x + 40f, cName = p.x + 130f, cTime = p.x + 560f, cBest = p.x + 790f, cPts = p.x + 1020f;
            UI.Text(new Rect(cPos, y, 80f, 30f), "POS", 15f, TextAnchor.MiddleLeft, UI.Dim);
            UI.Text(new Rect(cName, y, 300f, 30f), "PILOTO", 15f, TextAnchor.MiddleLeft, UI.Dim);
            UI.Text(new Rect(cTime, y, 200f, 30f), "TEMPO / DIF.", 15f, TextAnchor.MiddleLeft, UI.Dim);
            UI.Text(new Rect(cBest, y, 200f, 30f), "MELHOR VOLTA", 15f, TextAnchor.MiddleLeft, UI.Dim);
            UI.Text(new Rect(cPts, y, 120f, 30f), "PONTOS", 15f, TextAnchor.MiddleLeft, UI.Dim);
            y += 34f;
            var winner = order.Count > 0 ? order[0] : null;
            for (int i = 0; i < order.Count; i++)
            {
                var rc = order[i];
                Rect r = new Rect(p.x + 24f, y, pw - 48f, row - 6f);
                UI.Box(r, rc.IsHuman ? new Color(1f, 1f, 1f, 0.12f) : (i % 2 == 0 ? UI.Bg2 : UI.A(UI.Bg2, 0.5f)), 6f);
                Color posC = i == 0 ? UI.Gold : i == 1 ? UI.Silver : i == 2 ? UI.Bronze : UI.Bg3;
                Rect pr = new Rect(cPos - 8f, y + 6f, 52f, row - 18f);
                UI.Box(pr, posC, 5f);
                UI.Text(pr, (i + 1).ToString(), 24f, TextAnchor.MiddleCenter, i < 3 ? Color.black : Color.white);
                UI.Box(new Rect(cName, y + 10f, 6f, row - 26f), rc.color, 2f);
                UI.Text(new Rect(cName + 18f, y, 400f, row - 6f), rc.name + (rc.IsHuman ? "  <color=#9a9ea6>•  #" + rc.number + "</color>" : "  <color=#6a6e76>#" + rc.number + "</color>"), 26f, TextAnchor.MiddleLeft, rc.IsHuman ? UI.Yellow : Color.white);

                string time;
                if (!rc.finished) time = $"volta {Mathf.Max(rc.lap, 1)}/{laps}";
                else if (rc == winner) time = UI.Time(rc.finishTime);
                else
                {
                    float g = GapToLeader(rc, out int behind);
                    time = behind > 0 ? $"+{behind} volta{(behind > 1 ? "s" : "")}" : UI.Gap(g);
                }
                UI.Text(new Rect(cTime, y, 220f, row - 6f), time, 24f, TextAnchor.MiddleLeft, rc.finished ? Color.white : UI.Grey);
                bool fastest = rc == sessionBestCar;
                UI.Text(new Rect(cBest, y, 220f, row - 6f), UI.Time(rc.bestLap), 24f, TextAnchor.MiddleLeft, fastest ? UI.Purple : Color.white);
                int pts = rc.position <= Points2025.Length ? Points2025[rc.position - 1] : 0;
                UI.Text(new Rect(cPts, y, 100f, row - 6f), pts > 0 ? "+" + pts : "0", 24f, TextAnchor.MiddleLeft, pts > 0 ? Color.white : UI.Dim);
                y += row;
            }
            if (newRecord)
            {
                UI.Text(new Rect(p.x, y + 6f, pw, 36f), "NOVO RECORDE PESSOAL EM INTERLAGOS:  " + UI.Time(record), 24f, TextAnchor.MiddleCenter, UI.Purple);
                y += 50f;
            }
            string[] items = { "CORRER DE NOVO", "MENU" };
            for (int i = 0; i < 2; i++)
            {
                Rect b = new Rect(p.center.x - 330f + i * 350f, p.yMax - 92f, 310f, 66f);
                if (Hover(b)) resultSel = i;
                bool sel = resultSel == i;
                UI.Box(b, sel ? UI.Red : UI.Bg3, 8f);
                UI.Text(b, items[i], 24f, TextAnchor.MiddleCenter, Color.white);
                if (Click(b)) { if (i == 0) StartRace(); else GoMenu(); }
            }
        }
    }
}
