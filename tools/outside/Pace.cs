using System;
using System.Diagnostics;
using System.IO;
using DeepDrill.Core;

// Usage: Pace <data folder> [simulated hours] [real seconds allowed] [seed]
public static class Pace
{
    private const double Step = 0.1;
    private const double DecideSeconds = 1;
    private const int TapsPerSecond = 4;
    private const double DrillBudget = 0.5;

    public static int Main(string[] args)
    {
        string data = args[0];
        double hours = args.Length > 1 ? double.Parse(args[1]) : 12;
        double budget = args.Length > 2 ? double.Parse(args[2]) : 240;
        int seed = args.Length > 3 ? int.Parse(args[3]) : 7;
        GameData.Load(name => File.ReadAllText(Path.Combine(data, name + ".json")));

        var sim = new GameSim(new GameState { seed = seed });
        var clock = Stopwatch.StartNew();
        double t = 0, decide = 0, tap = 0, runStart = 0, layerEnteredAt = 0;
        int runLayer = 0, frontier = 0, rebirths = 0;

        while (t < hours * 3600 && clock.Elapsed.TotalSeconds < budget && sim.State.maxDepthEver < GameData.TotalRows)
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

            int layer = GameData.LayerOfRow(sim.State.maxDepth);
            if (layer > runLayer)
            {
                // only a layer nobody has seen yet counts towards the time per layer
                if (layer > frontier)
                {
                    frontier = layer;
                    Report(t, $"layer {layer + 1} first reached in run {rebirths + 1}, after {Minutes(t - layerEnteredAt)} in layer {runLayer + 1}, run time {Minutes(t - runStart)}, dps {sim.TotalDps:0.##e0}");
                }
                runLayer = layer;
                layerEnteredAt = t;
            }

            if (!sim.RebirthAdvised) continue;
            int gained = sim.ShardsOnRebirth;
            sim.Rebirth();
            rebirths++;
            Report(t, $"run {rebirths} ends after {Minutes(t - runStart)}: layer {runLayer + 1}, +{gained} shards, {sim.State.shards} in all");
            runStart = layerEnteredAt = t;
            runLayer = 0;
        }

        string centre = sim.State.maxDepthEver >= GameData.TotalRows ? "centre reached" : "centre not reached";
        Report(t, $"{centre} in run {rebirths + 1}, best layer {frontier + 1}, depth {sim.State.maxDepthEver}, shards {sim.State.shards}, real {clock.Elapsed.TotalSeconds:0}s");
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

        while (sim.MergeAll()) { }
    }

    private static string Minutes(double seconds) => $"{seconds / 60:0.0}m";

    private static void Report(double t, string text) => Console.WriteLine($"{Minutes(t),8}  {text}");
}
