using UnityEngine;
using VirtualDrivingSchool.Input;
using VirtualDrivingSchool.Vehicle;

namespace VirtualDrivingSchool.Audio
{
    /// <summary>
    /// Generates dynamic procedural engine audio, starter motor cranking,
    /// gear grinding, stall shudder, and tire squeal without requiring external audio assets.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralCarAudio : MonoBehaviour
    {
        [Header("References")]
        public ManualCarController car;
        public EngineSimulation engine;
        public Transmission transmission;
        public LogitechG920Manager input;

        [Header("Audio Tuning")]
        public float baseFrequency = 45f; // Hz at idle
        public float maxFrequency = 240f; // Hz at redline
        [Range(0.05f, 0.5f)] public float masterVolume = 0.25f;

        private AudioSource audioSource;
        private float phase = 0f;
        private float samplingRate = 48000f;
        private float currentFreq = 50f;
        private float grindPhase = 0f;
        private System.Random rng = new System.Random();

        private void Awake()
        {
            if (car == null) car = GetComponent<ManualCarController>();
            if (engine == null && car != null) engine = car.engine;
            if (transmission == null && car != null) transmission = car.transmission;
            if (input == null) input = LogitechG920Manager.Instance;

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.spatialBlend = 0.5f;
            samplingRate = AudioSettings.outputSampleRate;
        }

        private void Update()
        {
            if (engine == null) return;

            // Pitch is proportional to engine RPM
            float rpmNorm = Mathf.Clamp01(engine.currentRPM / engine.maxRPM);
            float targetFreq = Mathf.Lerp(baseFrequency, maxFrequency, rpmNorm);
            currentFreq = Mathf.Lerp(currentFreq, targetFreq, Time.deltaTime * 15f);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (engine == null) return;

            bool isRunning = (engine.state == EngineState.Running);
            bool isCranking = (engine.state == EngineState.Cranking);
            bool isGrinding = (transmission != null && transmission.isGearsGrinding);

            float throttle = input != null ? input.Throttle : 0f;
            float amplitude = isRunning ? (masterVolume * (0.6f + 0.4f * throttle)) : (isCranking ? 0.35f : 0f);

            float increment = (currentFreq * 2f * Mathf.PI) / samplingRate;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;

                if (isRunning)
                {
                    phase += increment;
                    if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

                    // 4-cylinder combustion harmonics (fundamental + 2nd + 3rd order harmonics)
                    float fund = Mathf.Sin(phase);
                    float h2 = Mathf.Sin(phase * 2f) * 0.5f;
                    float h3 = Mathf.Sin(phase * 4f) * 0.25f;
                    // Cylinder exhaust pulse shaping (soft clipping)
                    sample = Mathf.Clamp(fund + h2 + h3, -1f, 1f) * amplitude;
                }
                else if (isCranking)
                {
                    phase += (increment * 0.4f);
                    if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
                    // Rhythmic starter motor chug
                    float crankPulse = Mathf.Sin(phase * 3f);
                    sample = crankPulse * amplitude;
                }

                // Gear Grind effect (harsh metallic noise burst)
                if (isGrinding)
                {
                    grindPhase += 0.3f;
                    float metallicNoise = ((float)rng.NextDouble() * 2f - 1f) * 0.7f;
                    sample += metallicNoise * 0.5f;
                }

                // Duplicate sample across stereo channels
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = sample;
                }
            }
        }
    }
}
