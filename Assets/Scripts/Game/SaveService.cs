using System;
using System.IO;
using DeepDrill.Core;
using UnityEngine;

namespace DeepDrill.Game
{
    public sealed class SaveService
    {
        private readonly string path;

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
                state.version = GameState.CurrentVersion;
                return state;
            }
            catch (Exception e)
            {
                Debug.LogError($"Save unreadable, keeping a copy and starting over: {e.Message}");
                File.Copy(path, path + ".bad", true);
                return NewGame();
            }
        }

        public void Save(GameState state)
        {
            state.lastSaveUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // write then swap, so a crash mid-write cannot destroy the save
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(state, true));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        private static GameState NewGame() =>
            new GameState { seed = UnityEngine.Random.Range(1, int.MaxValue) };
    }
}
