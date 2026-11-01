using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>Player preferences that live outside the save file.</summary>
    public static class GameSettings
    {
        private const string ShakeKey = "shake";
        private const string ParticleKey = "particles";
        private const string SoundKey = "sound";
        private const string MusicKey = "music";

        public static readonly string[] ParticleNames = { "低", "中", "高" };
        public static readonly string[] ShakeNames = { "关", "弱", "中", "强" };
        public static readonly string[] SoundNames = { "关", "25%", "50%", "75%", "100%" };
        private static readonly float[] SoundScales = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        private static readonly float[] ShakeScales = { 0f, 0.5f, 1f, 1.5f };
        private static readonly float[] ParticleScales = { 0.25f, 0.6f, 1f };

        public static int ShakeLevel
        {
            get => PlayerPrefs.GetInt(ShakeKey, 2);
            set => PlayerPrefs.SetInt(ShakeKey, value);
        }

        public static int ParticleLevel
        {
            get => PlayerPrefs.GetInt(ParticleKey, 2);
            set => PlayerPrefs.SetInt(ParticleKey, value);
        }

        public static int SoundLevel
        {
            get => PlayerPrefs.GetInt(SoundKey, 4);
            set => PlayerPrefs.SetInt(SoundKey, value);
        }

        public static int MusicLevel
        {
            get => PlayerPrefs.GetInt(MusicKey, 3);
            set => PlayerPrefs.SetInt(MusicKey, value);
        }

        public static float SoundScale => SoundScales[SoundLevel];
        public static float MusicScale => SoundScales[MusicLevel];
        public static float ShakeScale => ShakeScales[ShakeLevel];
        public static float ParticleScale => ParticleScales[ParticleLevel];
    }
}
