using UnityEngine;

namespace Corrida
{
    /// <summary>Câmera de perseguição com 3 modos: perto, longe e capô.</summary>
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        public CarController target;
        public int mode;
        public Camera Cam { get; private set; }

        Vector3 dir;
        bool init;

        void Awake() { Cam = GetComponent<Camera>(); }

        void LateUpdate()
        {
            if (target == null) return;
            Transform t = target.transform;
            float dt = Time.deltaTime;
            float s = Mathf.Clamp01(Mathf.Abs(target.ForwardSpeed) / target.maxSpeed);

            if (mode == 2)
            {
                // T-cam: em cima da entrada de ar, como nas transmissões de F1
                transform.position = t.TransformPoint(new Vector3(0f, 1.25f, -0.45f));
                transform.rotation = Quaternion.LookRotation(t.forward, t.up) * Quaternion.Euler(4f, 0f, 0f);
                Cam.fieldOfView = Mathf.Lerp(64f, 78f, s);
                return;
            }

            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 vflat = Vector3.ProjectOnPlane(target.Body.GetVel(), Vector3.up);
            Vector3 want = fwd;
            if (vflat.magnitude > 4f && Vector3.Dot(vflat, fwd) > 0f) want = Vector3.Slerp(fwd, vflat.normalized, 0.35f);
            if (!init) { dir = want; init = true; }
            dir = Vector3.Slerp(dir, want, 1f - Mathf.Exp(-6f * dt)).normalized;

            float dist = mode == 0 ? 7.2f : 12f;
            float h = mode == 0 ? 2.0f : 3.6f;
            Vector3 focus = t.position + Vector3.up * 1.1f;
            Vector3 desired = focus - dir * dist + Vector3.up * (h - 1.1f);
            if (Physics.Linecast(focus, desired, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                desired = hit.point + hit.normal * 0.3f;
            transform.position = desired;
            transform.rotation = Quaternion.LookRotation(focus + dir * 4f - desired, Vector3.up);
            Cam.fieldOfView = Mathf.Lerp(58f, 76f, s);
        }
    }
}
