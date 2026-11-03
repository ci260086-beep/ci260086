using System;
using System.IO;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    public sealed class SaveService
    {
        private readonly string path;

        /// <summary>
        /// Copies a save from the game's old folder when the new one has none yet. The old file is
        /// left where it was, as a backup.
        /// </summary>
        public static void AdoptOldSave(string path, string oldPath)
        {
            try
            {
                if (File.Exists(path) || string.IsNullOrEmpty(oldPath) || !File.Exists(oldPath)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.Copy(oldPath, path);
                UnityEngine.Debug.Log($"[save] adopted the save from {oldPath}");
            }
            catch (IOException e)
            {
                UnityEngine.Debug.LogWarning($"[save] could not copy the old save: {e.Message}");
            }
        }

        public SaveService(string path)
        {
            this.path = path;
        }

        public GameState Load()
        {
            if (!File.Exists(path)) return NewGame();
            try
            {
                var state = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                if (state == null) return NewGame();
                if (state.version > GameState.CurrentVersion)
                    Debug.LogWarning($"Save version {state.version} is newer than this build.");
                if (state.version != GameState.CurrentVersion) KeepCopy(state.version);
                state.Upgrade();
                return state;
            }
            catch (Exception e)
            {
                Debug.LogError($"Save unreadable, keeping a copy and starting over: {e.Message}");
                File.Copy(path, path + ".bad", true);
                return NewGame();
            }
        }

        // The first load after an update keeps the save as it was, in case the upgrade goes wrong.
        private void KeepCopy(int version)
        {
            string copy = $"{path}.v{version}";
            if (!File.Exists(copy)) File.Copy(path, copy);
        }

        /// <summary>Moves the save aside, so the next load starts a new game and the old one can be put back.</summary>
        public void Archive()
        {
            if (File.Exists(path)) File.Move(path, path + ".old-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        public void Save(GameState state)
        {
            state.lastSaveUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // write then swap, so a crash mid-write cannot destroy the save
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(state, true));
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }
            // a virus scanner can hold the old file for a moment right after it was written;
            // try again briefly rather than lose this save
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.Replace(tmp, path, null);
                    return;
                }
                catch (IOException) when (attempt < ReplaceAttempts)
                {
                    System.Threading.Thread.Sleep(ReplaceRetryMs);
                }
            }
        }

        private const int ReplaceAttempts = 5;
        private const int ReplaceRetryMs = 40;

        private static GameState NewGame() =>
            new GameState { seed = UnityEngine.Random.Range(1, int.MaxValue) };
    }
}
