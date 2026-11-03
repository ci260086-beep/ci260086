using System;
using System.Collections.Generic;
using DeepDrill.Core;

namespace DeepDrill.Game
{
    /// <summary>
    /// Where scores go once there is somewhere to send them. The Steam build will implement this
    /// with Steamworks leaderboards (one board per <see cref="LeaderboardDef.Id"/>, keep-best,
    /// descending) and set <see cref="Leaderboards.Backend"/> at start-up. Until then nothing is
    /// sent and the page shows the player's own records.
    /// </summary>
    public interface ILeaderboardBackend
    {
        void Submit(string board, long score);
    }

    public sealed class LeaderboardDef
    {
        public string Id;
        public string Name;
        public string Rule;                     // one line on what counts
        public Func<GameState, long> Score;     // the number a board ranks by, higher is better
        public Func<GameState, string> Show;    // how the player's own record reads
    }

    public static class Leaderboards
    {
        public static ILeaderboardBackend Backend;

        public static readonly List<LeaderboardDef> All = new List<LeaderboardDef>
        {
            new LeaderboardDef
            {
                Id = "standard_depth", Name = "基础钻挑战", Rule = "只用标准钻的一局里挖到的最深处",
                Score = s => s.standardBestRow,
                Show = s => s.standardBestRow <= 0 ? "还没有记录"
                    : $"{GameData.Layers[GameData.LayerOfRow(s.standardBestRow)].Name} {GameData.FormatDepth(s.standardBestRow)}",
            },
            new LeaderboardDef
            {
                Id = "play_time", Name = "累计时长", Rule = "这个存档一共挖了多久",
                Score = s => (long)s.playSecondsEver, Show = s => Duration(s.playSecondsEver),
            },
            new LeaderboardDef
            {
                Id = "longest_run", Name = "最长一局", Rule = "两次地核重铸之间最久的一局",
                Score = s => (long)s.longestRunSeconds, Show = s => Duration(s.longestRunSeconds),
            },
            new LeaderboardDef
            {
                Id = "best_tier", Name = "最高钻头", Rule = "合成出的最高材质等级",
                Score = s => s.bestTier,
                Show = s => s.bestTier <= 0 ? "还没有记录" : $"{s.bestTier} 级 {GameData.Materials[Math.Min(s.bestTier, GameData.Tiers) - 1]}",
            },
            new LeaderboardDef
            {
                Id = "most_drills", Name = "钻头最多", Rule = "同时拥有的钻头数量",
                Score = s => s.mostDrills, Show = s => $"{s.mostDrills} 台",
            },
        };

        /// <summary>Sends every record to the backend, if there is one. Boards keep the best, so resending is harmless.</summary>
        public static void SubmitAll(GameState state)
        {
            if (Backend == null) return;
            foreach (var board in All) Backend.Submit(board.Id, board.Score(state));
        }

        public static string Duration(double seconds)
        {
            long minutes = (long)(seconds / 60);
            if (minutes < 60) return $"{minutes} 分钟";
            long hours = minutes / 60;
            return hours < 100 ? $"{hours} 小时 {minutes % 60} 分" : $"{hours} 小时";
        }
    }
}
