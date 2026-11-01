using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>
    /// Background music: one loop per layer and a faster one for guardian fights, synthesised by
    /// <see cref="MusicSynth"/> off the main thread the first time each is needed, then
    /// cross-faded in. Volume follows the music setting.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const float FadeSeconds = 1.5f;
        private const float BaseVolume = 0.55f;

        private readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<int, Task<float[]>> rendering = new Dictionary<int, Task<float[]>>();
        private AudioSource current, previous;
        private int wanted = -1, playing = -1;
        private float fade = 1;

        public void Init()
        {
            current = MakeSource();
            previous = MakeSource();
        }

        private AudioSource MakeSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0;
            return source;
        }

        /// <summary>Asks for the tune of this layer, or its guardian. Cheap to call every frame.</summary>
        public void SetMood(int layer, bool boss) => wanted = boss ? 100 + layer : layer;

        private void Update()
        {
            if (wanted >= 0 && wanted != playing && TryGetClip(wanted, out var clip))
            {
                (previous, current) = (current, previous);
                current.clip = clip;
                current.Play();
                playing = wanted;
                fade = 0;
            }

            fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / FadeSeconds);
            float volume = BaseVolume * GameSettings.MusicScale;
            current.volume = volume * fade;
            previous.volume = volume * (1 - fade);
            if (fade >= 1 && previous.isPlaying) previous.Stop();
        }

        private bool TryGetClip(int key, out AudioClip clip)
        {
            if (clips.TryGetValue(key, out clip)) return true;
            if (!rendering.TryGetValue(key, out var task))
            {
                var tune = MusicSynth.ForLayer(key % 100, key >= 100);
                rendering[key] = Task.Run(() => MusicSynth.Render(tune));
                return false;
            }
            if (!task.IsCompleted) return false;
            rendering.Remove(key);
            if (task.IsFaulted)
            {
                Debug.LogException(task.Exception);
                clips[key] = null;
                return false;
            }
            float[] data = task.Result;
            clip = AudioClip.Create("music_" + key, data.Length, 1, MusicSynth.SampleRate, false);
            clip.SetData(data, 0);
            clips[key] = clip;
            return true;
        }
    }
}
