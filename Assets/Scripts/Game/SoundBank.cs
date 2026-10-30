using System.Collections.Generic;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// 8-bit effects synthesised at start-up in the spirit of sfxr: a square or noise voice with a
    /// pitch slide and a falling envelope. Plays through a small pool with a rate limit, so a big
    /// swarm cannot turn into a wall of noise.
    /// </summary>
    public sealed class SoundBank : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const int Voices = 10;
        private const float PitchJitter = 0.1f;
        private const float MinGapSeconds = 0.04f;

        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private int next;
        private float lastPlay;

        public void Init()
        {
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sources.Add(source);
            }
            clips["break"] = Synth("break", 0.09f, 220, 90, 0.6f, 0.5f);
            clips["hard"] = Synth("hard", 0.12f, 700, 240, 0.25f, 0.5f);
            clips["coin"] = Synth("coin", 0.1f, 990, 1320, 0f, 0.4f);
            clips["blast"] = Synth("blast", 0.45f, 160, 40, 1f, 0.9f);
            clips["merge"] = Synth("merge", 0.28f, 440, 1200, 0f, 0.5f);
            clips["upgrade"] = Synth("upgrade", 0.2f, 660, 1320, 0f, 0.5f);
            clips["boss"] = Synth("boss", 0.8f, 110, 55, 0.5f, 0.9f);
        }

        /// <summary>Plays a named effect. Pitch shifts randomly, plus any extra such as a combo.</summary>
        public void Play(string name, float volume = 0.5f, float extraPitch = 0f, bool always = false)
        {
            if (!always && Time.unscaledTime - lastPlay < MinGapSeconds) return;
            lastPlay = Time.unscaledTime;
            var source = sources[next];
            next = (next + 1) % sources.Count;
            source.pitch = 1f + Random.Range(-PitchJitter, PitchJitter) + extraPitch;
            source.PlayOneShot(clips[name], volume);
        }

        // noise 0 is a pure square wave, 1 pure noise
        private static AudioClip Synth(string name, float seconds, float startHz, float endHz, float noise, float gain)
        {
            int count = (int)(seconds * SampleRate);
            var data = new float[count];
            float phase = 0, held = 0;
            var random = new System.Random(name.GetHashCode());
            int hold = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float hz = Mathf.Lerp(startHz, endHz, t);
                phase += hz / SampleRate;
                float square = phase % 1f < 0.5f ? 1f : -1f;
                if (hold-- <= 0)
                {
                    held = (float)random.NextDouble() * 2 - 1;
                    hold = (int)(SampleRate / Mathf.Max(hz * 4, 1f));
                }
                float envelope = (1 - t) * (1 - t);
                data[i] = Mathf.Lerp(square, held, noise) * envelope * gain;
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
