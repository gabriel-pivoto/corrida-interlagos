using System.Collections.Generic;
using UnityEngine;

namespace Corrida
{
    /// <summary>
    /// Piloto automático: segue a linha ideal de corrida (curvatura de referência + correção de posição e rumo),
    /// freia no ponto calculado pela aderência do carro, abre o DRS quando pode e ultrapassa pelo lado com espaço.
    /// skill = fração da aderência que o piloto usa (1 = no limite).
    /// </summary>
    public class AIDriver
    {
        public RaceCar rc;
        public TrackData track;
        public float skill = 0.95f;
        public List<RaceCar> others;

        float laneOffset, stuckTimer, reverseTimer;
        float[] speeds;

        public DriverInput Compute(float dt)
        {
            var car = rc.car;
            int N = track.Count;
            int idx = rc.trackIndex;
            float v = car.ForwardSpeed;
            float absV = Mathf.Abs(v);
            var d = new DriverInput();
            if (speeds == null) speeds = track.SpeedFor(skill);

            Vector3 pos = car.transform.position;
            Vector3 fwd = track.F[idx], right = track.R[idx];
            float myLat = Vector3.Dot(pos - track.P[idx], right);
            float raceLat = track.RaceOff[idx];

            // ultrapassagem: só sai da linha se estiver mais rápido; senão segue atrás
            float edgeLim = TrackData.HalfWidth - 1.3f;
            float laneTarget = 0f;
            float followSpeed = float.MaxValue;
            float nearest = float.MaxValue;
            float lo = -edgeLim, hi = edgeLim;   // espaço lateral livre (carros lado a lado)
            if (others != null)
            {
                foreach (var o in others)
                {
                    if (o == rc || o.car == null) continue;
                    Vector3 delta = o.car.transform.position - pos;
                    float ahead = Vector3.Dot(delta, fwd);
                    float lat = Vector3.Dot(delta, right);
                    // lado a lado: não fecha a porta, deixa ~2,9 m entre os centros
                    if (Mathf.Abs(ahead) < 5.8f && Mathf.Abs(lat) < 4.5f && Mathf.Abs(lat) > 0.8f)
                    {
                        float oLat2 = myLat + lat;
                        if (lat > 0f) hi = Mathf.Min(hi, oLat2 - 2.9f);
                        else lo = Mathf.Max(lo, oLat2 + 2.9f);
                    }
                    if (ahead < -4f || ahead > 26f || Mathf.Abs(lat) > 2.6f || ahead >= nearest) continue;
                    nearest = ahead;
                    if (ahead > 0f && o.car.ForwardSpeed > v - 1.5f)
                    {
                        if (ahead < 14f) followSpeed = o.car.ForwardSpeed - 0.5f;
                        continue;
                    }
                    float oLat = myLat + lat;
                    float left = oLat - 3.0f, rightSide = oLat + 3.0f;
                    bool canLeft = left >= -edgeLim, canRight = rightSide <= edgeLim;
                    if (canLeft || canRight)
                    {
                        bool goLeft = canLeft && (!canRight || Mathf.Abs(left - myLat) < Mathf.Abs(rightSide - myLat));
                        laneTarget = (goLeft ? left : rightSide) - raceLat;
                    }
                    else
                    {
                        laneTarget = myLat - raceLat;
                        if (ahead > 0f) followSpeed = o.car.ForwardSpeed - 1f;
                    }
                }
            }
            laneOffset = Mathf.MoveTowards(laneOffset, laneTarget, 3f * dt);

            // linha a seguir = linha ideal + desvio de ultrapassagem (dentro da pista)
            float pathLat = Mathf.Clamp(raceLat + laneOffset, -edgeLim, edgeLim);
            if (lo <= hi) pathLat = Mathf.Clamp(pathLat, lo, hi);
            else pathLat = (lo + hi) * 0.5f;
            float e = myLat - pathLat;                                       // >0: carro à direita da linha
            Vector3 lineDir = Vector3.ProjectOnPlane(track.RaceF[idx], Vector3.up);
            Vector3 carDir = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up);
            float psi = Vector3.SignedAngle(lineDir, carDir, Vector3.up) * Mathf.Deg2Rad; // >0: apontando à direita
            float ld = 8f + absV * 0.3f;
            int pv = track.Wrap(idx + Mathf.RoundToInt(absV * 0.09f / track.Step)); // antecipa a resposta do carro
            float kappa = track.RaceCurv[pv] - 2f * e / (ld * ld) - 2f * psi / ld;
            d.steer = Mathf.Clamp(kappa / car.KMaxAt(absV), -1f, 1f);
            if (Mathf.Abs(psi) > 1.6f) d.steer = -Mathf.Sign(psi);

            // velocidade-alvo: olha à frente o tempo de reação (~0,15 s)
            float vT = float.MaxValue;
            int look = 2 + Mathf.CeilToInt(absV * 0.15f / track.Step);
            for (int k = 0; k <= look; k++) vT = Mathf.Min(vT, speeds[track.Wrap(idx + k)]);
            float edge = Mathf.Abs(myLat) - (TrackData.HalfWidth - 0.9f);
            if (edge > 0f) vT *= 0.93f;
            vT = Mathf.Min(vT, followSpeed);
            if (v < vT - 0.5f) d.throttle = 1f;
            else if (v < vT) d.throttle = 0.5f;
            if (v > vT + 0.3f) d.brake = Mathf.Clamp01((v - vT) / 1.5f);

            // DRS: abre sempre que puder com o pé embaixo
            d.drs = car.drsAllowed && d.throttle >= 1f && d.brake <= 0f;

            // destravar quando bate e fica parado
            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                d.throttle = 0f;
                d.brake = 1f;
                d.drs = false;
                d.steer = -d.steer;
                return d;
            }
            if (!car.controlsLocked && Mathf.Abs(v) < 1.5f) stuckTimer += dt;
            else stuckTimer = 0f;
            if (stuckTimer > 2f)
            {
                stuckTimer = 0f;
                reverseTimer = 1.3f;
                rc.stuckCount++;
            }
            return d;
        }
    }
}
