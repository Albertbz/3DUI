using UnityEngine;

namespace SpatialPhotos
{
    // Sound feedback for resizing the capture frame:
    // - a soft looping tone whose pitch follows the frame size and whose volume follows how fast it changes
    // - ratchet ticks every few centimeters of size change, brighter when growing and lower when shrinking
    // Clips are synthesized at startup unless custom ones are assigned.
    public class FrameResizeAudio : MonoBehaviour
    {
        [Header("Clips (synthesized when empty)")]
        [SerializeField] private AudioClip humClip;
        [SerializeField] private AudioClip tickClip;

        [Header("Size mapping")]
        [SerializeField] private float minDiagonal = 0.05f;
        [SerializeField] private float maxDiagonal = 0.9f;
        [SerializeField] private float minPitch = 0.7f;
        [SerializeField] private float maxPitch = 1.8f;

        [Header("Hum")]
        [SerializeField] private float humMaxVolume = 0.25f;
        [Tooltip("Resize speed (m/s of diagonal change) that plays the hum at full volume")]
        [SerializeField] private float fullVolumeSpeed = 0.5f;
        [Tooltip("Extra pitch bend up when growing and down when shrinking")]
        [SerializeField] private float directionBend = 0.12f;

        [Header("Ticks")]
        [SerializeField] private float tickSpacing = 0.025f; // one tick per 2.5 cm of diagonal change
        [SerializeField] private float tickVolume = 0.35f;
        [SerializeField] private float minTickInterval = 0.035f;
        [SerializeField] private float shrinkTickPitch = 0.8f;

        private AudioSource humSource;
        private AudioSource tickSource;

        private float targetHumVolume;
        private float targetHumPitch = 1f;
        private float lastTickDiagonal = -1f;
        private float lastTickTime;

        private static AudioClip synthesizedHum;
        private static AudioClip synthesizedTick;

        private void Awake()
        {
            if (humClip == null)
            {
                if (synthesizedHum == null)
                {
                    synthesizedHum = SynthesizeHum();
                }
                humClip = synthesizedHum;
            }
            if (tickClip == null)
            {
                if (synthesizedTick == null)
                {
                    synthesizedTick = SynthesizeTick();
                }
                tickClip = synthesizedTick;
            }

            humSource = CreateSource();
            humSource.clip = humClip;
            humSource.loop = true;

            tickSource = CreateSource();
        }

        private void OnEnable()
        {
            targetHumVolume = 0f;
            humSource.volume = 0f;
            lastTickDiagonal = -1f;
            humSource.Play();
        }

        private void OnDisable()
        {
            humSource.Stop();
        }

        private void Update()
        {
            // Smooth toward the targets so the tone glides instead of stepping each frame
            humSource.volume = Mathf.Lerp(humSource.volume, targetHumVolume, 1f - Mathf.Exp(-Time.deltaTime * 15f));
            humSource.pitch = Mathf.Lerp(humSource.pitch, targetHumPitch, 1f - Mathf.Exp(-Time.deltaTime * 20f));

            // Fade out when no resize updates arrive (hands held still)
            targetHumVolume = Mathf.MoveTowards(targetHumVolume, 0f, Time.deltaTime * 2f);
        }

        // diagonal: frame diagonal in meters; changeSpeed: m/s, positive while growing
        public void UpdateSize(float diagonal, float changeSpeed)
        {
            float size01 = Mathf.InverseLerp(minDiagonal, maxDiagonal, diagonal);
            float basePitch = Mathf.Lerp(minPitch, maxPitch, size01);
            float speed01 = Mathf.Clamp01(Mathf.Abs(changeSpeed) / fullVolumeSpeed);

            targetHumVolume = humMaxVolume * speed01;
            targetHumPitch = basePitch * (1f + directionBend * speed01 * Mathf.Sign(changeSpeed));

            if (lastTickDiagonal < 0f)
            {
                lastTickDiagonal = diagonal;
                return;
            }

            float travelled = diagonal - lastTickDiagonal;
            if (Mathf.Abs(travelled) >= tickSpacing && Time.time - lastTickTime >= minTickInterval)
            {
                bool growing = travelled > 0f;
                // Snap to the tick grid so fast movements don't drift the tick positions
                lastTickDiagonal += Mathf.Sign(travelled) * tickSpacing * Mathf.Floor(Mathf.Abs(travelled) / tickSpacing);
                lastTickTime = Time.time;

                tickSource.pitch = basePitch * (growing ? 1f : shrinkTickPitch);
                tickSource.PlayOneShot(tickClip, tickVolume * Mathf.Lerp(0.6f, 1f, speed01));
            }
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 0.3f;
            source.maxDistance = 5f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            return source;
        }

        private static AudioClip SynthesizeHum()
        {
            // One second at 220 Hz is a whole number of cycles, so the loop is seamless
            int sampleRate = AudioSettings.outputSampleRate;
            var samples = new float[sampleRate];
            const float frequency = 220f;
            var random = new System.Random(1);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float phase = 2f * Mathf.PI * frequency * t;
                float tone = Mathf.Sin(phase) + 0.35f * Mathf.Sin(2f * phase) + 0.12f * Mathf.Sin(3f * phase);
                // Slow shimmer: 4 Hz also divides one second evenly
                float shimmer = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                float air = ((float)random.NextDouble() * 2f - 1f) * 0.06f;
                samples[i] = (tone * shimmer * 0.45f + air) * 0.8f;
            }
            var clip = AudioClip.Create("FrameHum", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeTick()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            var samples = new float[Mathf.CeilToInt(sampleRate * 0.04f)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 140f);
                float body = Mathf.Sin(2f * Mathf.PI * 1400f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2900f * t);
                samples[i] = body * envelope * 0.6f;
            }
            var clip = AudioClip.Create("FrameTick", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
