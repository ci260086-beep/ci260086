using System.Collections.Generic;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// 8-bit effects rendered at start-up by <see cref="SfxSynth"/>. Plays through a small pool
    /// with a rate limit, so a big swarm cannot turn into a wall of noise.
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
            int seed = 1;
            foreach (var pair in SfxSynth.Library())
            {
                var data = SfxSynth.Render(pair.Value, 0.8, seed++);
                var clip = AudioClip.Create(pair.Key, data.Length, 1, SfxSynth.SampleRate, false);
                clip.SetData(data, 0);
                clips[pair.Key] = clip;
            }
            Ui.Clicked += OnUiClicked;
        }

        private void OnUiClicked() => Play("click", 0.35f, 0, true, false);

        private void OnDestroy() => Ui.Clicked -= OnUiClicked;

        /// <summary>Plays a named effect. Pitch shifts randomly, plus any extra such as a combo.</summary>
        /// <remarks>
        /// Frequent sounds (always false) share a rate limit and get a random pitch so repeats do
        /// not drone; tuned jingles keep their pitch so they stay in key with the music.
        /// </remarks>
        public void Play(string name, float volume = 0.5f, float extraPitch = 0f, bool always = false, bool jitter = true)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            if (!always && Time.unscaledTime - lastPlay < MinGapSeconds) return;
            if (!always) lastPlay = Time.unscaledTime;
            var source = sources[next];
            next = (next + 1) % sources.Count;
            source.pitch = 1f + (jitter && !always ? Random.Range(-PitchJitter, PitchJitter) : 0f) + extraPitch;
            source.PlayOneShot(clip, volume * GameSettings.SoundScale);
        }
    }
}
