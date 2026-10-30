using System;
using System.Diagnostics;
using System.IO;
using DeepDrill.Core;

// Usage: Pace <data folder> [simulated hours] [real seconds allowed]
public static class Pace
{
    private const double Step = 0.05;
    private const double DecideSeconds = 1;
    private const int TapsPerSecond = 4;
    private const double DrillBudget = 0.5;

    public static int Main(string[] args)
    {
        string data = args[0];
        double hours = args.Length > 1 ? double.Parse(args[1]) : 6;
        double budget = args.Length > 2 ? double.Parse(args[2]) : 240;
        GameData.Load(name => File.ReadAllText(Path.Combine(data, name + ".json")));

        var sim = new GameSim(new GameState { seed = 7 });
        var clock = Stopwatch.StartNew();
        double t = 0, decide = 0, tap = 0, runStart = 0, lastDepthAt = 0;
        int layerSeen = 0, bossSeen = 0, depthSeen = 0, rebirths = 0;
        bool autoSeen = false, canSeen = false;

        while (t < hours * 3600 && clock.Elapsed.TotalSeconds < budget)
        {
            sim.Tick(Step);
            t += Step;
            sim.Events.Clear();

            if (!sim.AutoDrillUnlocked && (tap += Step) >= 1.0 / TapsPerSecond)
            {
                tap = 0;
                var body = sim.Lead.Body;
                // aim at the rock just under the drill, the way the tests tap it
                sim.ClickPixel((int)Math.Floor((body.X + 0.5) * 16), (int)Math.Floor((body.Y + 0.5) * 16) + 14);
            }

            if ((decide += Step) >= DecideSeconds)
            {
                decide = 0;
                Spend(sim);
                sim.StartFrenzy();
            }

            if (sim.State.maxDepth > depthSeen)
            {
                depthSeen = sim.State.maxDepth;
                lastDepthAt = t;
            }
            if (!autoSeen && sim.AutoDrillUnlocked)
            {
                autoSeen = true;
                Report(t, "auto drill unlocked");
            }
            int layer = GameData.LayerOfRow(sim.State.maxDepth);
            if (layer > layerSeen)
            {
                layerSeen = layer;
                Report(t, $"run {rebirths} layer {layer + 1} ({GameData.Layers[layer].Name}), run time {Minutes(t - runStart)}");
            }
            if (sim.State.bossesDefeated > bossSeen)
            {
                bossSeen = sim.State.bossesDefeated;
                Report(t, $"run {rebirths} boss {bossSeen} down, drills {sim.State.drills.Count}");
            }
            if (!canSeen && sim.CanRebirth)
            {
                canSeen = true;
                Report(t, $"run {rebirths} can rebirth for {sim.ShardsOnRebirth}");
            }
            if (sim.RebirthAdvised)
            {
                Report(t, $"run {rebirths} rebirth +{sim.ShardsOnRebirth} depth {sim.State.maxDepth} drills {sim.State.drills.Count} run time {Minutes(t - runStart)}");
                sim.Rebirth();
                rebirths++;
                runStart = lastDepthAt = t;
                layerSeen = bossSeen = depthSeen = 0;
                canSeen = false;
            }
        }

        Report(t, $"end: rebirths {rebirths} shards {sim.State.shards} depth {sim.State.maxDepth} best {sim.State.maxDepthEver} drills {sim.State.drills.Count} coins {sim.State.coins:0.##e0} stalled {Minutes(t - lastDepthAt)}, real {clock.Elapsed.TotalSeconds:0}s");
        return 0;
    }

    // A plain player: cheapest skill first, then drills with half of what is left, then merges.
    private static void Spend(GameSim sim)
    {
        for (bool bought = true; bought;)
        {
            bought = false;
            SkillDef best = null;
            foreach (var skill in GameData.Skills)
                if (sim.CanBuy(skill) && (best == null || skill.CostAt(sim.State.LevelOf(skill.Id)) < best.CostAt(sim.State.LevelOf(best.Id))))
                    best = skill;
            if (best != null) bought = sim.Buy(best.Id);
        }

        for (bool bought = true; bought;)
        {
            bought = false;
            int cheapest = -1;
            for (int f = 0; f < GameData.Families.Count; f++)
                if (sim.CanBuyDrill(f) && (cheapest < 0 || sim.DrillPrice(f) < sim.DrillPrice(cheapest))) cheapest = f;
            if (cheapest >= 0 && sim.DrillPrice(cheapest) <= sim.State.coins * DrillBudget) bought = sim.BuyDrill(cheapest);
        }

        for (int i = 0; i < sim.State.drills.Count; i++)
            if (sim.CanMerge(sim.State.drills[i]) && sim.Merge(sim.State.drills[i])) i = -1;
    }

    private static string Minutes(double seconds) => $"{seconds / 60:0.0}m";

    private static void Report(double t, string text) => Console.WriteLine($"{Minutes(t),8}  {text}");
}
