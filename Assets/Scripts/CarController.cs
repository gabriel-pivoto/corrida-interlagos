using UnityEngine;

namespace Corrida
{
    /// <summary>
    /// Números do regulamento técnico de 2025 da F1 (era "efeito solo", 2022-2025):
    /// peso mínimo 800 kg com piloto (sem combustível), até 110 kg de combustível com fluxo máximo de 100 kg/h,
    /// V6 1.6 turbo (~840 cv) + MGU-K de 120 kW (~160 cv) limitado a 4 MJ por volta, 8 marchas, 15.000 rpm,
    /// pneus Pirelli 18" (305/720 na frente, 405/720 atrás), entre-eixos máx. 3,6 m, largura máx. 2,0 m.
    /// Calibrado para ~1:10 em Interlagos na volta ideal (pole 2025: 1:09.511; volta mais rápida em corrida: 1:12.400)
    /// e ~325 km/h no fim da reta (+~12 km/h com DRS).
    /// </summary>
    public static class F1Spec
    {
        public const float Mass = 800f;            // kg, mínimo com piloto
        public const float FuelMax = 110f;         // kg
        public const float FuelFlow = 100f / 3600f; // kg/s com o pé no fundo
        public const float IcePower = 626000f;     // W
        public const float ErsPower = 120000f;     // W (MGU-K)
        public const float ErsPerLap = 4000000f;   // J que a bateria pode entregar por volta
        public const float DragK = 0.96f;          // N/(m/s)²  -> F = DragK·v²
        public const float DownK = 3.52f;          // N/(m/s)²  -> ~1.700 kgf a 250 km/h
        public const float DrsDrag = 0.88f;        // asa aberta: -12% de arrasto
        public const float DrsDown = 0.9f;         // e um pouco menos de downforce
        public const float Mu = 1.8f;              // atrito do slick com carga baixa
        public const float LoadExp = 0.75f;        // sensibilidade à carga (pneu perde eficiência com muito downforce)
        public const float Rolling = 0.25f;        // m/s² de resistência ao rolamento
        public const float RevLimit = 15000f, ShiftRpm = 11800f, IdleRpm = 4000f;
        public static readonly float[] GearTop = { 28f, 37.5f, 46f, 54f, 62.5f, 71f, 80.5f, 93f }; // m/s de cada marcha no giro de troca
        public const float G = 9.81f;

        /// <summary>Aceleração máxima que os pneus suportam (curva ou freio) nessa velocidade e massa.</summary>
        public static float Grip(float v, bool grass, float mass = Mass + 30f, float downFactor = 1f)
        {
            float load = G + (grass ? 0.3f : 1f) * downFactor * DownK * v * v / mass;
            return (grass ? 0.5f : 1f) * Mu * G * Mathf.Pow(load / G, LoadExp);
        }

        public static float DragAccel(float v, float mass = Mass + 30f, bool drs = false) => (drs ? DrsDrag : 1f) * DragK * v * v / mass;
    }

    /// <summary>
    /// Física: suspensão por raycast nas 4 rodas, V6 + ERS limitados por potência e tração, arrasto e downforce
    /// em função de v², DRS, consumo de combustível (massa variável), freio e curva limitados pela aderência,
    /// e esterço controlando a taxa de guinada.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public DriverInput input;
        public bool controlsLocked = true;

        public float maxSpeed = 93f;        // referência (m/s) para a câmera
        public float reverseMax = 8f;
        public float maxCurvature = 0.11f;  // raio mínimo ~9 m (F1 vira pouco em baixa)

        public float suspRest = 0.12f, wheelRadius = 0.36f, springK = 110000f, damperC = 7000f;
        public Vector3[] wheelMounts = new Vector3[4];
        public Vector3[] wheelVisualBase = new Vector3[4];
        public Transform[] wheelPivots = new Transform[4];
        public Transform[] wheelSpinners = new Transform[4];
        public Renderer[] brakeLights;
        public Material brakeOn, brakeOff;
        public Transform drsFlap;

        // regulamento
        public float fuel;                     // kg a bordo
        public float ersEnergy = F1Spec.ErsPerLap;
        public bool drsAllowed;                // RaceManager: dentro da zona e habilitado (até 1 s do carro da frente)
        public bool DrsOpen { get; private set; }

        public Rigidbody Body { get; private set; }
        public bool Grounded { get; private set; }
        public bool OnGrass { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float ThrottleAmount { get; private set; }
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public float GForce { get; private set; }
        public int Gear { get; private set; } = 1;
        public float Rpm { get; private set; } = F1Spec.IdleRpm;
        public bool ErsDeploying { get; private set; }
        public float TotalMass => F1Spec.Mass + fuel;

        readonly float[] springLen = new float[4];
        float steerSmooth, spinAngle, drsAngle;
        bool braking, lastBraking;
        Vector3 lastVel;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            for (int i = 0; i < 4; i++) springLen[i] = suspRest;
        }

        /// <summary>Curvatura máxima que o carro consegue fazer nessa velocidade (esterço = 1).</summary>
        public float KMaxAt(float absV) => Mathf.Min(maxCurvature, F1Spec.Grip(absV, false, TotalMass) / Mathf.Max(absV * absV, 0.01f));

        public void SetFuel(float kg)
        {
            fuel = Mathf.Clamp(kg, 0f, F1Spec.FuelMax);
            if (Body != null) Body.mass = TotalMass;
        }

        public void ResetTo(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            Body.position = pos;
            Body.rotation = rot;
            Body.SetVel(Vector3.zero);
            Body.angularVelocity = Vector3.zero;
            steerSmooth = 0f;
            lastVel = Vector3.zero;
            DrsOpen = false;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            int grounded = 0, grass = 0;
            Vector3 nSum = Vector3.zero;
            float maxLen = suspRest + wheelRadius;
            float mass = TotalMass;
            Body.mass = mass;
            float massK = mass / F1Spec.Mass;

            for (int i = 0; i < 4; i++)
            {
                Vector3 o = transform.TransformPoint(wheelMounts[i]);
                if (Physics.Raycast(o, -up, out RaycastHit hit, maxLen, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float len = Mathf.Max(hit.distance - wheelRadius, 0f);
                    float comp = suspRest - len;
                    float vUp = Vector3.Dot(Body.GetPointVelocity(o), up);
                    float f = (comp * springK - vUp * damperC) * massK;
                    if (f > 0f) Body.AddForceAtPosition(up * f, o);
                    springLen[i] = len;
                    grounded++;
                    nSum += hit.normal;
                    if (TrackSurfaces.IsGrass(hit.collider)) grass++;
                }
                else springLen[i] = suspRest;
            }

            Grounded = grounded > 0;
            OnGrass = grass >= 2;

            Vector3 vel = Body.GetVel();
            Vector3 fwd = transform.forward, right = transform.right;
            float v = Vector3.Dot(vel, fwd);
            float absV = Mathf.Abs(v);
            ForwardSpeed = v;
            GForce = Mathf.Lerp(GForce, ((vel - lastVel) / dt).magnitude / F1Spec.G, 0.1f);
            lastVel = vel;

            float thr = 0f, brk = 0f, steer = 0f;
            bool hb = false, drsReq = false;
            if (!controlsLocked)
            {
                thr = Mathf.Clamp01(input.throttle);
                brk = Mathf.Clamp01(input.brake);
                steer = Mathf.Clamp(input.steer, -1f, 1f);
                hb = input.handbrake;
                drsReq = input.drs;
            }
            ThrottleAmount = thr;
            steerSmooth = Mathf.MoveTowards(steerSmooth, steer, dt * 7f);
            braking = controlsLocked || (brk > 0.1f && v > 1f) || hb;

            // DRS: abre quando permitido e pedido; fecha ao frear ou ao sair da zona
            if (!drsAllowed || brk > 0.05f || hb || controlsLocked) DrsOpen = false;
            else if (drsReq) DrsOpen = true;

            UpdateGear(absV);

            Vector3 n = Grounded ? nSum.normalized : Vector3.up;
            ErsDeploying = false;
            if (Grounded)
            {
                Vector3 driveDir = Vector3.ProjectOnPlane(fwd, n).normalized;
                float downF = DrsOpen ? F1Spec.DrsDown : 1f;
                float grip = F1Spec.Grip(absV, OnGrass, mass, downF);
                float a = 0f;
                if (controlsLocked)
                {
                    a = -Mathf.Sign(v) * Mathf.Min(absV / dt, 15f);
                }
                else
                {
                    if (thr > 0.01f)
                    {
                        if (v < -0.5f) a += grip * thr;
                        else
                        {
                            float power = F1Spec.IcePower;
                            if (ersEnergy > 0f && v > 8f)
                            {
                                power += F1Spec.ErsPower;
                                ersEnergy -= F1Spec.ErsPower * thr * dt;
                                ErsDeploying = true;
                            }
                            float traction = 0.6f * grip; // tração traseira limitada pela aderência
                            a += thr * Mathf.Min(traction, power / mass / Mathf.Max(v, 1f));
                            fuel = Mathf.Max(0f, fuel - F1Spec.FuelFlow * thr * dt);
                        }
                    }
                    if (brk > 0.01f)
                    {
                        if (v > 0.5f) a -= brk * grip;
                        else if (thr <= 0.01f && v > -reverseMax) a -= 5f * brk;
                    }
                    if (absV > 0.05f)
                    {
                        // arrasto aerodinâmico + rolamento + freio-motor ao tirar o pé
                        float resist = F1Spec.DragAccel(v, mass, DrsOpen) + F1Spec.Rolling + (thr <= 0.01f ? 1.0f : 0f);
                        if (OnGrass) resist += 3f;
                        if (hb) resist += 6f;
                        a -= Mathf.Sign(v) * Mathf.Min(resist, absV / dt);
                    }
                }
                Body.AddForce(driveDir * a, ForceMode.Acceleration);

                // guinada: curvatura máxima limitada pela aderência (que cresce com o downforce)
                float lat = grip * (hb ? 1.15f : 1f);
                float kMax = Mathf.Min(maxCurvature, lat / Mathf.Max(absV * absV, 0.01f));
                float yawTarget = steerSmooth * kMax * v;
                Vector3 lav = transform.InverseTransformDirection(Body.angularVelocity);
                float resp = hb ? 5f : 16f;
                lav.y = Mathf.Lerp(lav.y, yawTarget, 1f - Mathf.Exp(-resp * dt));
                Body.angularVelocity = transform.TransformDirection(lav);

                // aderência lateral
                float latV = Vector3.Dot(vel, right);
                float gripRate = hb ? 1.5f : (OnGrass ? 3f : 16f);
                Body.AddForce(-right * latV * Mathf.Clamp01(gripRate * dt), ForceMode.VelocityChange);

                // downforce empurrando o carro contra o chão (parte dele; a suspensão é dura)
                Body.AddForce(-n * 0.4f * downF * F1Spec.DownK * v * v / mass, ForceMode.Acceleration);
            }

            // mantém o carro de pé
            Vector3 axis = Vector3.Cross(up, n);
            Vector3 av = Body.angularVelocity;
            Vector3 avTilt = av - up * Vector3.Dot(av, up);
            Body.AddTorque(axis * (Grounded ? 6f : 10f) - avTilt * (Grounded ? 0.5f : 1.5f), ForceMode.Acceleration);
        }

        /// <summary>Câmbio sequencial de 8 marchas (para o som e o painel).</summary>
        void UpdateGear(float absV)
        {
            var top = F1Spec.GearTop;
            int g = Mathf.Clamp(Gear, 1, top.Length);
            if (g < top.Length && absV > top[g - 1]) g++;
            else if (g > 1 && absV < top[g - 2] * 0.78f) g--;
            Gear = g;
            Rpm = Mathf.Clamp(F1Spec.ShiftRpm * absV / top[g - 1], F1Spec.IdleRpm, F1Spec.RevLimit);
        }

        void LateUpdate()
        {
            float steerDeg = steerSmooth * 22f;
            spinAngle = Mathf.Repeat(spinAngle + ForwardSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime, 360f);
            for (int i = 0; i < 4; i++)
            {
                if (wheelPivots[i] == null) continue;
                wheelPivots[i].localPosition = wheelVisualBase[i] + Vector3.down * springLen[i];
                wheelPivots[i].localRotation = Quaternion.Euler(0f, i < 2 ? steerDeg : 0f, 0f);
                wheelSpinners[i].localRotation = Quaternion.Euler(spinAngle, 0f, 0f);
            }
            if (drsFlap != null)
            {
                drsAngle = Mathf.MoveTowards(drsAngle, DrsOpen ? 1f : 0f, Time.deltaTime * 8f);
                drsFlap.localRotation = Quaternion.Euler(Mathf.Lerp(32f, 4f, drsAngle), 0f, 0f);
            }
            if (braking != lastBraking && brakeLights != null)
            {
                foreach (var r in brakeLights) r.sharedMaterial = braking ? brakeOn : brakeOff;
                lastBraking = braking;
            }
        }
    }
}
