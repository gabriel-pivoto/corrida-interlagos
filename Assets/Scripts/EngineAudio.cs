using UnityEngine;

namespace Corrida
{
    /// <summary>Ronco do V6 turbo sintetizado (sem arquivos de áudio), seguindo o giro do câmbio de 8 marchas.</summary>
    public class EngineAudio : MonoBehaviour
    {
        public CarController car;
        public bool is2D;

        AudioSource src;
        float baseVol;
        static AudioClip clip;

        void Start()
        {
            if (clip == null) clip = MakeClip();
            src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = is2D ? 0f : 1f;
            baseVol = is2D ? 0.18f : 0.45f;
            src.volume = baseVol;
            src.minDistance = 6f;
            src.maxDistance = 120f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            src.Play();
        }

        void Update()
        {
            if (car == null || src == null) return;
            // V6: 3 explosões por volta do virabrequim -> frequência = rpm/60*3. O clipe base tem 300 Hz (6.000 rpm).
            float target = car.Rpm / 6000f;
            src.pitch = Mathf.Lerp(src.pitch, target, Time.deltaTime * 14f);
            src.volume = baseVol * (0.55f + 0.45f * car.ThrottleAmount);
        }

        static AudioClip MakeClip()
        {
            const int rate = 44100, period = 147; // 300 Hz (V6 a 6.000 rpm)
            const int len = period * 2 * 75;
            var d = new float[len];
            for (int i = 0; i < len; i++)
            {
                float ph = (i % period) / (float)period;
                float sub = (i % (period * 2)) / (float)(period * 2);
                float saw = ph * 2f - 1f;
                float s1 = Mathf.Sin(ph * Mathf.PI * 2f);
                float s2 = Mathf.Sin(ph * Mathf.PI * 4f);
                float am = 0.8f + 0.2f * Mathf.Sin(sub * Mathf.PI * 2f);
                d[i] = (saw * 0.4f + s1 * 0.3f + s2 * 0.25f + Mathf.Sin(ph * Mathf.PI * 6f) * 0.12f) * am * 0.55f;
            }
            var c = AudioClip.Create("motor", len, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
