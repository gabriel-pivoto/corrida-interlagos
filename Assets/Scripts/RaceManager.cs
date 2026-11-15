using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Corrida
{
    public enum RaceState { Menu, Countdown, Racing, Finished }

    public class RaceCar
    {
        public string name;
        public Color color, accent;
        public int player = -1;          // -1 = IA
        public CarController car;
        public AIDriver ai;
        public ChaseCamera cam;
        public int trackIndex;
        public int lap;                   // 0 = antes de cruzar a largada
        public bool passedHalf = true;
        public bool invalidLap;
        public bool finished;
        public float finishTime, lapStart;
        public float bestLap = -1f, lastLap = -1f;
        public float wrongWayTimer, flipTimer;
        public int position, stuckCount;
        public float grassTime;
        public bool wasOnGrass;
        public float topSpeed;
        public int drsUses;
        public readonly float[] detectTime = { -99f, -99f, -99f, -99f };
        public readonly bool[] drsEligible = new bool[4];
        public int number;
        public string tag;                // sigla de 3 letras na torre de tempos
        public float lapFlash;            // tempo restante do aviso de volta completada
        public Color lapFlashColor;
        public string lapFlashText;
        public bool IsHuman => player >= 0;
    }

    public partial class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        TrackData track;
        readonly List<RaceCar> cars = new List<RaceCar>();
        readonly List<RaceCar> order = new List<RaceCar>();
        RaceState state = RaceState.Menu;
        bool paused;

        int humans = 1, aiCount = 5, laps = 3, difficulty = 1;
        float countdown, countdownTotal, raceTime, goTimer, finishDelay;

        // cronometragem: momento em que o líder passou por cada ponto da pista (para as diferenças na torre)
        float[] leadTime;
        int[] leadLap;
        float sessionBest = -1f;
        RaceCar sessionBestCar;
        float record = -1f;
        bool newRecord;
        const string RecordKey = "RecordeInterlagos";

        Camera menuCam;
        Transform listener;
        float menuDist;

        Texture2D minimap;
        Vector2 mapCenter;
        float mapScale;
        const int MapSize = 384;

        static readonly Color SkyColor = new Color(0.62f, 0.76f, 0.92f);

        static readonly string[] DiffNames = { "Fácil", "Médio", "Difícil" };
        static readonly float[] DiffSkill = { 0.8f, 0.9f, 0.975f };  // fração da aderência que a IA usa
        static readonly int[] Points2025 = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 }; // em 2025 não há ponto extra pela volta mais rápida
        const int DrsFromLap = 3;           // DRS liberado dois giros após a largada
        static readonly Color[] HumanColors = { new Color(0.85f, 0.06f, 0.06f), new Color(0.08f, 0.28f, 0.9f) };
        static readonly Color[] HumanAccents = { new Color(0.95f, 0.95f, 0.95f), new Color(1f, 0.82f, 0.05f) };
        static readonly Color[] AIColors =
        {
            new Color(1f, 0.82f, 0.05f), new Color(0.03f, 0.4f, 0.2f), new Color(0.92f, 0.92f, 0.92f), new Color(0.45f, 0.15f, 0.7f),
            new Color(0.1f, 0.1f, 0.12f), new Color(0.1f, 0.72f, 0.85f), new Color(0.95f, 0.4f, 0.65f), new Color(0.62f, 0.64f, 0.68f)
        };
        static readonly Color[] AIAccents =
        {
            new Color(0.0f, 0.55f, 0.25f), new Color(0.75f, 1f, 0.2f), new Color(0.85f, 0.1f, 0.1f), new Color(0.75f, 0.75f, 0.78f),
            new Color(0.85f, 0.65f, 0.2f), new Color(1f, 1f, 1f), new Color(0.1f, 0.1f, 0.12f), new Color(1f, 0.45f, 0.05f)
        };
        static readonly int[] CarNumbers = { 7, 12, 27, 44, 5, 88, 19, 33 };
        static readonly string[] AINames = { "R. Pires", "L. Andrade", "T. Moraes", "B. Rocha", "C. Fontes", "P. Siqueira", "H. Dias", "M. Albuquerque" };

        void Awake()
        {
            Instance = this;
            Time.fixedDeltaTime = 0.01f;
            SetupEnvironment();
            record = PlayerPrefs.GetFloat(RecordKey, -1f);
            track = new TrackData();
            track.Build();
            TrackBuilder.Build(track, transform);
            BuildMinimap();
            menuCam = CreateCamera("CameraMenu");
            listener = new GameObject("Ouvinte").AddComponent<AudioListener>().transform;
            Debug.Log($"[Corrida] Pista gerada: {track.Length:F0} m, {track.Count} amostras, desnível {track.MaxY - track.MinY:F1} m");

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-capturas") StartCoroutine(AutoTest(args[i + 1]));
        }

        // ---------------------------------------------------------------- teste automático (-capturas <pasta>)

        bool autoPilot;

        System.Collections.IEnumerator AutoTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            autoPilot = true;
            yield return new WaitForSeconds(3f);
            Shot(dir, "01_menu");
            yield return null;
            humans = 1; aiCount = 5; laps = 3;
            StartRace();
            yield return new WaitForSeconds(1.5f);
            var show = CreateCamera("CameraVitrine");
            show.depth = 10f;
            foreach (var rc in cars)
                if (rc.player == 0)
                {
                    var ct = rc.car.transform;
                    show.transform.position = ct.position + ct.right * 3.2f + ct.forward * 3.4f + Vector3.up * 1.3f;
                    show.transform.LookAt(ct.position + Vector3.up * 0.45f);
                    show.fieldOfView = 50f;
                }
            yield return null;
            Shot(dir, "00_carro");
            yield return null;
            var ct2 = show.transform;
            foreach (var rc in cars)
                if (rc.player == 0)
                {
                    var ct = rc.car.transform;
                    ct2.position = ct.position - ct.right * 2.6f - ct.forward * 3.6f + Vector3.up * 1.6f;
                    ct2.LookAt(ct.position + Vector3.up * 0.4f);
                }
            yield return null;
            Shot(dir, "00_carro_tras");
            yield return null;
            Destroy(show.gameObject);
            yield return new WaitForSeconds(1f);
            Shot(dir, "02_grid");
            yield return new WaitForSeconds(14f);
            Shot(dir, "03_corrida");
            yield return null;
            foreach (var rc in cars) if (rc.cam != null) rc.cam.mode = 2;
            yield return new WaitForSeconds(12f);
            Shot(dir, "04_capo");
            yield return null;
            foreach (var rc in cars) if (rc.cam != null) rc.cam.mode = 1;
            yield return new WaitForSeconds(80f);
            Shot(dir, "05_longe");
            yield return null;
            foreach (var rc in cars) if (rc.cam != null) rc.cam.mode = 0;
            yield return new WaitForSeconds(135f);
            Shot(dir, "05b_volta3");
            yield return null;
            yield return null;
            laps = 2;
            humans = 2; aiCount = 4;
            StartRace();
            yield return new WaitForSeconds(20f);
            Shot(dir, "06_dois_jogadores");
            yield return new WaitForSeconds(40f);
            Shot(dir, "07_dois_jogadores_depois");
            yield return null;
            SetPaused(true);
            yield return null;
            yield return null;
            Shot(dir, "08_pausa");
            yield return null;
            Application.Quit();
        }

        void Shot(string dir, string name)
        {
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
            var sb = new System.Text.StringBuilder($"[Corrida] {name} t={raceTime:F1}s real={Time.realtimeSinceStartup:F0}s fps={1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):F0}");
            foreach (var rc in order)
                sb.Append($" | {rc.position}o {rc.name} volta {rc.lap} {track.Section[rc.trackIndex]} {rc.car.SpeedKmh:F0}km/h max={rc.topSpeed:F0} drs={rc.drsUses} comb={rc.car.fuel:F1}kg grama={rc.grassTime:F1}s melhor={Fmt(rc.bestLap)}");
            Debug.Log(sb.ToString());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // ---------------------------------------------------------------- ambiente

        void SetupEnvironment()
        {
            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.22f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 400f;
            RenderSettings.fogEndDistance = 2000f;

            RenderSettings.skybox = Resources.Load<Material>("Materiais/Ceu");
            if (RenderSettings.skybox == null)
            {
                var sky = Shader.Find("Skybox/Procedural");
                if (sky != null) RenderSettings.skybox = new Material(sky);
            }
            QualitySettings.shadowDistance = 220f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }

        Camera CreateCamera(string name)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = SkyColor;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = 60f;
            return cam;
        }

        // ---------------------------------------------------------------- corrida

        void ClearCars()
        {
            foreach (var rc in cars)
            {
                if (rc.cam != null) Destroy(rc.cam.gameObject);
                if (rc.car != null) Destroy(rc.car.gameObject);
            }
            cars.Clear();
            order.Clear();
        }

        void StartRace()
        {
            ClearCars();
            SetPaused(false);
            float skill = DiffSkill[difficulty];
            int total = humans + aiCount;

            // IA larga na frente, jogadores atrás
            for (int k = 0; k < total; k++)
            {
                bool isHuman = k >= aiCount;
                var rc = new RaceCar();
                if (isHuman)
                {
                    rc.player = k - aiCount;
                    rc.name = "Jogador " + (rc.player + 1);
                    rc.color = HumanColors[rc.player];
                    rc.accent = HumanAccents[rc.player];
                }
                else
                {
                    rc.name = AINames[k % AINames.Length];
                    rc.color = AIColors[k % AIColors.Length];
                    rc.accent = AIAccents[k % AIAccents.Length];
                }
                int idx = TrackBuilder.GridIndex(track, k, out float lat);
                Vector3 pos = track.P[idx] + track.R[idx] * lat + Vector3.up * 0.6f;
                int number = isHuman ? (rc.player == 0 ? 1 : 2) : CarNumbers[k % CarNumbers.Length];
                rc.car = CarFactory.Create(rc.name, rc.color, rc.accent, number, pos, Quaternion.LookRotation(track.F[idx], Vector3.up));
                rc.number = number;
                rc.tag = MakeTag(rc);
                rc.car.controlsLocked = true;
                rc.trackIndex = idx;
                float sk = isHuman ? 0.9f : Mathf.Min(1f, skill * Random.Range(0.985f, 1.005f));
                rc.ai = new AIDriver { rc = rc, track = track, others = cars, skill = Mathf.Round(sk * 200f) / 200f };
                // combustível para a corrida (~1,6 kg por volta em Interlagos), limite de 110 kg
                rc.car.SetFuel(Mathf.Min(F1Spec.FuelMax, laps * 1.6f + 1.5f));
                var audio = rc.car.gameObject.AddComponent<EngineAudio>();
                audio.car = rc.car;
                audio.is2D = isHuman;
                cars.Add(rc);
            }

            foreach (var rc in cars)
            {
                if (!rc.IsHuman) continue;
                var cam = CreateCamera("Camera_J" + (rc.player + 1));
                cam.rect = humans == 1 ? new Rect(0f, 0f, 1f, 1f)
                    : rc.player == 0 ? new Rect(0f, 0.5f, 1f, 0.5f) : new Rect(0f, 0f, 1f, 0.5f);
                rc.cam = cam.gameObject.AddComponent<ChaseCamera>();
                rc.cam.target = rc.car;
            }

            menuCam.enabled = false;
            order.AddRange(cars);
            SortPositions();
            state = RaceState.Countdown;
            countdown = 6f + Random.Range(0.4f, 2.2f); // 5 luzes acendem (1 por segundo) e apagam após um tempo aleatório
            countdownTotal = countdown;
            leadTime = new float[track.Count];
            leadLap = new int[track.Count];
            for (int i = 0; i < leadLap.Length; i++) leadLap[i] = int.MinValue;
            sessionBest = -1f;
            sessionBestCar = null;
            newRecord = false;
            pauseSel = 0;
            resultSel = 0;
            raceTime = 0f;
            goTimer = 0f;
            finishDelay = -1f;
        }

        void GoMenu()
        {
            ClearCars();
            SetPaused(false);
            state = RaceState.Menu;
            menuCam.enabled = true;
        }

        void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
        }

        void Update()
        {
            if (state == RaceState.Menu)
            {
                UpdateMenuCamera();
                MenuInput();
                return;
            }

            if (GameInput.PausePressed())
            {
                if (state == RaceState.Finished) GoMenu();
                else { SetPaused(!paused); pauseSel = 0; }
                return;
            }
            if (paused) { PauseInput(); return; }
            if (state == RaceState.Finished) ResultsInput();

            float dt = Time.deltaTime;
            if (state == RaceState.Countdown)
            {
                countdown -= dt;
                if (countdown <= 0f)
                {
                    state = RaceState.Racing;
                    raceTime = 0f;
                    goTimer = 1.5f;
                    foreach (var rc in cars) { rc.car.controlsLocked = false; rc.lapStart = 0f; }
                }
            }
            else raceTime += dt;
            if (goTimer > 0f) goTimer -= dt;

            foreach (var rc in cars)
            {
                if (state != RaceState.Countdown) UpdateProgress(rc);
                if (rc.lapFlash > 0f) rc.lapFlash -= dt;
                if (state == RaceState.Racing && rc.car.OnGrass)
                {
                    if (autoPilot && !rc.wasOnGrass)
                    {
                        float latOff = Vector3.Dot(rc.car.transform.position - track.P[rc.trackIndex], track.R[rc.trackIndex]);
                        Debug.Log($"[Grama] {rc.name} {track.Section[rc.trackIndex]} idx={rc.trackIndex} v={rc.car.SpeedKmh:F0} alvo={track.SpeedTarget[rc.trackIndex] * 3.6f:F0} lat={latOff:F1} curv={track.Curv[rc.trackIndex]:F4}");
                    }
                    rc.grassTime += dt;
                }
                rc.wasOnGrass = rc.car.OnGrass;
                if (state == RaceState.Racing) rc.topSpeed = Mathf.Max(rc.topSpeed, rc.car.SpeedKmh);

                if (rc.IsHuman && !rc.finished && !autoPilot)
                {
                    rc.car.input = GameInput.ReadPlayer(rc.player);
                    if (GameInput.ResetPressed(rc.player)) ResetCar(rc);
                }
                else rc.car.input = rc.ai.Compute(dt);

                if (rc.IsHuman && GameInput.CameraPressed(rc.player)) rc.cam.mode = (rc.cam.mode + 1) % 3;

                // capotou, caiu ou ficou preso
                var tr = rc.car.transform;
                if (Vector3.Dot(tr.up, Vector3.up) < 0.3f) rc.flipTimer += dt; else rc.flipTimer = 0f;
                if (rc.flipTimer > 2f || tr.position.y < track.MinY - 20f || rc.stuckCount >= 3)
                {
                    rc.flipTimer = 0f;
                    rc.stuckCount = 0;
                    ResetCar(rc);
                }

                // contramão
                if (rc.IsHuman && state == RaceState.Racing && Vector3.Dot(rc.car.Body.GetVel(), track.F[rc.trackIndex]) < -4f)
                    rc.wrongWayTimer += dt;
                else rc.wrongWayTimer = 0f;
            }

            SortPositions();

            if (state == RaceState.Racing)
            {
                bool allHumansDone = true;
                foreach (var rc in cars) if (rc.IsHuman && !rc.finished) allHumansDone = false;
                if (allHumansDone)
                {
                    if (finishDelay < 0f) finishDelay = 3f;
                    finishDelay -= dt;
                    if (finishDelay <= 0f) state = RaceState.Finished;
                }
            }
        }

        void LateUpdate()
        {
            Transform src = menuCam.transform;
            if (state != RaceState.Menu)
                foreach (var rc in cars)
                    if (rc.player == 0 && rc.cam != null) src = rc.cam.transform;
            listener.SetPositionAndRotation(src.position, src.rotation);
        }

        void UpdateProgress(RaceCar rc)
        {
            int N = track.Count;
            int ni = track.FindNearest(rc.car.transform.position, rc.trackIndex);
            int delta = ni - rc.trackIndex;
            if (!rc.finished)
            {
                if (delta < -N / 2)
                {
                    // cruzou a linha para frente
                    if (rc.passedHalf)
                    {
                        if (rc.lap >= 1 && !rc.invalidLap)
                        {
                            float lt = raceTime - rc.lapStart;
                            rc.lastLap = lt;
                            if (autoPilot) Debug.Log($"[Volta] {rc.name} volta {rc.lap} {Fmt(lt)} max={rc.topSpeed:F0}km/h drs={rc.drsUses} comb={rc.car.fuel:F1}kg ers={rc.car.ersEnergy / 1e6f:F2}MJ");
                            bool personal = rc.bestLap < 0f || lt < rc.bestLap;
                            bool overall = sessionBest < 0f || lt < sessionBest;
                            if (personal) rc.bestLap = lt;
                            if (overall) { sessionBest = lt; sessionBestCar = rc; }
                            rc.lapFlash = 4f;
                            rc.lapFlashColor = overall ? UI.Purple : personal ? UI.Green : UI.Yellow;
                            rc.lapFlashText = overall ? "VOLTA MAIS RÁPIDA" : personal ? "MELHOR VOLTA PESSOAL" : "VOLTA " + rc.lap;
                            if (rc.IsHuman && !autoPilot && (record < 0f || lt < record))
                            {
                                record = lt;
                                newRecord = true;
                                PlayerPrefs.SetFloat(RecordKey, record);
                                PlayerPrefs.Save();
                            }
                        }
                        rc.invalidLap = false;
                        rc.car.ersEnergy = F1Spec.ErsPerLap;
                        if (rc.lap >= laps)
                        {
                            rc.finished = true;
                            rc.finishTime = raceTime;
                        }
                        else rc.lap++;
                        rc.lapStart = raceTime;
                        rc.passedHalf = false;
                    }
                }
                else if (delta > N / 2)
                {
                    // cruzou a linha de ré
                    rc.lap--;
                    rc.passedHalf = true;
                    rc.invalidLap = true;
                }
                if (ni > N * 0.35f && ni < N * 0.65f) rc.passedHalf = true;
            }
            RecordLeadTimes(rc, rc.trackIndex, ni);
            UpdateDrs(rc, rc.trackIndex, ni);
            rc.trackIndex = ni;
        }

        /// <summary>
        /// DRS (regra de 2025): no ponto de detecção, se o carro estiver a menos de 1 s do carro da frente, a asa pode
        /// ser aberta na zona seguinte. Liberado a partir da 3ª volta. Sozinho na pista (treino) é livre.
        /// </summary>
        void UpdateDrs(RaceCar rc, int from, int to)
        {
            var zones = track.DrsZones;
            bool practice = cars.Count == 1;
            int fwdSteps = track.Wrap(to - from);
            for (int z = 0; z < zones.Count; z++)
            {
                var zone = zones[z];
                if (fwdSteps > 0 && fwdSteps < 40 && track.InSpan(zone.detect, from + 1, to))
                {
                    rc.detectTime[z] = raceTime;
                    float myProg = Progress(rc);
                    RaceCar ahead = null;
                    float best = float.MaxValue;
                    foreach (var o in cars)
                    {
                        if (o == rc || o.finished) continue;
                        float gap = Progress(o) - myProg;
                        if (gap > 0f && gap < best) { best = gap; ahead = o; }
                    }
                    bool ok = ahead != null && raceTime - ahead.detectTime[z] <= 1.0f;
                    rc.drsEligible[z] = ok && state == RaceState.Racing && rc.lap >= DrsFromLap;
                }
            }
            bool allowed = false;
            if (state == RaceState.Racing && !rc.finished)
                for (int z = 0; z < zones.Count; z++)
                    if ((rc.drsEligible[z] || practice) && track.InSpan(to, zones[z].start, zones[z].end)) allowed = true;
            if (allowed && !rc.car.drsAllowed) rc.drsUses++;
            rc.car.drsAllowed = allowed;
        }

        /// <summary>Guarda quando o primeiro carro passou por cada ponto em cada volta (referência para as diferenças).</summary>
        void RecordLeadTimes(RaceCar rc, int from, int to)
        {
            if (leadTime == null || rc.finished) return;
            int steps = track.Wrap(to - from);
            if (steps <= 0 || steps > 60) return;
            for (int k = 1; k <= steps; k++)
            {
                int i = track.Wrap(from + k);
                int lapAt = (i < from && to < from) ? rc.lap : (to < from ? rc.lap - 1 : rc.lap);
                if (leadLap[i] < lapAt) { leadLap[i] = lapAt; leadTime[i] = raceTime; }
            }
        }

        /// <summary>Diferença para o líder em segundos; voltas atrás em lapsBehind (&gt; 0 = retardatário).</summary>
        float GapToLeader(RaceCar rc, out int lapsBehind)
        {
            lapsBehind = 0;
            var leader = order.Count > 0 ? order[0] : null;
            if (leader == null || leader == rc) return 0f;
            if (rc.finished && leader.finished) return rc.finishTime - leader.finishTime;
            if (leadTime == null) return 0f;
            int i = rc.trackIndex;
            if (leadLap[i] > rc.lap) { lapsBehind = leadLap[i] - rc.lap; return 0f; }
            if (leadLap[i] == rc.lap) return Mathf.Max(0f, raceTime - leadTime[i]);
            return 0f;
        }

        float Progress(RaceCar rc) => (rc.lap - 1) * track.Length + track.Dist[rc.trackIndex];

        void SortPositions()
        {
            order.Sort((a, b) =>
            {
                if (a.finished && b.finished) return a.finishTime.CompareTo(b.finishTime);
                if (a.finished) return -1;
                if (b.finished) return 1;
                return Progress(b).CompareTo(Progress(a));
            });
            for (int i = 0; i < order.Count; i++) order[i].position = i + 1;
        }

        void ResetCar(RaceCar rc)
        {
            int i = rc.trackIndex;
            Vector3 basePos = track.P[i] + Vector3.up * 0.7f;
            float[] lanes = { 0f, -3.8f, 3.8f };
            Vector3 pos = basePos;
            foreach (float l in lanes)
            {
                Vector3 p = basePos + track.R[i] * l;
                bool free = true;
                foreach (var o in cars)
                    if (o != rc && (o.car.transform.position - p).sqrMagnitude < 16f) free = false;
                if (free) { pos = p; break; }
            }
            rc.car.ResetTo(pos, Quaternion.LookRotation(track.F[i], Vector3.up));
        }

        void UpdateMenuCamera()
        {
            float dt = Time.unscaledDeltaTime;
            menuDist += dt * 22f;
            int N = track.Count;
            int i = Mathf.FloorToInt(menuDist / track.Step) % N;
            int j = (i + 30) % N;
            Vector3 pos = track.P[i] + track.R[i] * 24f + Vector3.up * 16f;
            var tr = menuCam.transform;
            if ((tr.position - pos).sqrMagnitude > 10000f) tr.position = pos;
            tr.position = Vector3.Lerp(tr.position, pos, 1f - Mathf.Exp(-1.5f * dt));
            Quaternion look = Quaternion.LookRotation(track.P[j] - tr.position, Vector3.up);
            tr.rotation = Quaternion.Slerp(tr.rotation, look, 1f - Mathf.Exp(-1.5f * dt));
        }

        // ---------------------------------------------------------------- minimapa

        void BuildMinimap()
        {
            minimap = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false);
            var px = new Color32[MapSize * MapSize];
            Vector2 size = track.MaxXZ - track.MinXZ;
            mapScale = (MapSize - 24) / Mathf.Max(size.x, size.y);
            mapCenter = (track.MinXZ + track.MaxXZ) * 0.5f;
            Color32 outline = new Color32(10, 10, 14, 230), road = new Color32(235, 235, 235, 255), start = new Color32(230, 30, 30, 255);
            Color32 drs = new Color32(70, 220, 110, 255);
            for (int i = 0; i < track.Count; i++) Disc(px, MapPx(track.P[i]), 6.5f, outline);
            for (int i = 0; i < track.Count; i++)
            {
                bool inDrs = false;
                foreach (var z in track.DrsZones) if (track.InSpan(i, z.start, z.end)) inDrs = true;
                Disc(px, MapPx(track.P[i]), 3.6f, inDrs ? drs : road);
            }
            for (int k = -2; k <= 2; k++) Disc(px, MapPx(track.P[0] + track.R[0] * k * 3f), 2.6f, start);
            minimap.SetPixels32(px);
            minimap.Apply();
        }

        Vector2 MapPx(Vector3 w) =>
            new Vector2(MapSize * 0.5f + (w.x - mapCenter.x) * mapScale, MapSize * 0.5f + (w.z - mapCenter.y) * mapScale);

        static void Disc(Color32[] px, Vector2 c, float r, Color32 col)
        {
            int x0 = Mathf.FloorToInt(c.x - r), x1 = Mathf.CeilToInt(c.x + r);
            int y0 = Mathf.FloorToInt(c.y - r), y1 = Mathf.CeilToInt(c.y + r);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || y < 0 || x >= MapSize || y >= MapSize) continue;
                    if ((new Vector2(x, y) - c).sqrMagnitude <= r * r) px[y * MapSize + x] = col;
                }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (RaceManager.Instance != null) return;
            foreach (var cam in Camera.allCameras) Object.Destroy(cam.gameObject);
            new GameObject("Corrida").AddComponent<RaceManager>();
        }
    }
}
