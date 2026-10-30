using UnityEngine;

namespace DeepDrill.Game
{
    /// <summary>Player preferences that live outside the save file.</summary>
    public static class GameSettings
    {
        private const string ShakeKey = "shake";
        private const string ParticleKey = "particles";

        public static readonly string[] ParticleNames = { "低", "中", "高" };
        public static readonly string[] ShakeNames = { "关", "弱", "中", "强" };
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

        public static float ShakeScale => ShakeScales[ShakeLevel];
        public static float ParticleScale => ParticleScales[ParticleLevel];
    }
}
