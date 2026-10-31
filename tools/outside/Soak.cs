using System;
using System.Diagnostics;
using System.IO;
using DeepDrill.Core;

// Usage: Soak <data folder> [simulated hours] [seed]
// Leaves the rules running for hours with a simple player and prints what is held in memory.
public static class Soak
{
    private const double Step = 0.1;
    private const double DecideSeconds = 1;
    private const double MemoryGrowthLimit = 1.5;
    private const int SettleHours = 2;
    private const double OneMegabyte = 1024 * 1024;

    public static int Main(string[] args)
    {
        string data = args[0];
        double hours = args.Length > 1 ? double.Parse(args[1]) : 8;
        int seed = args.Length > 2 ? int.Parse(args[2]) : 7;
        GameData.Load(name => File.ReadAllText(Path.Combine(data, name + ".json")));

        var sim = new GameSim(new GameState { seed = seed });
        sim.GrantSkill(GameSim.RootSkill, 1);
        var clock = Stopwatch.StartNew();
        double t = 0, decide = 0, worstTick = 0, settled = 0, peak = 0;
        int rebirths = 0, nextHour = 1;

        while (t < hours * 3600)
        {
            double before = clock.Elapsed.TotalMilliseconds;
            sim.Tick(Step);
            worstTick = Math.Max(worstTick, clock.Elapsed.TotalMilliseconds - before);
            t += Step;
            sim.Events.Clear();

            if ((decide += Step) >= DecideSeconds)
            {
                decide = 0;
                Spend(sim);
                sim.StartFrenzy();
                sim.ClaimEvent();
                if (sim.RebirthAdvised && sim.Rebirth()) rebirths++;
            }

            if (t < nextHour * 3600) continue;
            sim.PackHoles();
            int holes = 0;
            foreach (var chunk in sim.State.dug) holes += chunk.holes.Length;
            double megabytes = GC.GetTotalMemory(true) / OneMegabyte;
            if (nextHour == SettleHours) settled = megabytes;
            peak = Math.Max(peak, megabytes);
            var w = sim.World;
            Console.WriteLine($"hour {nextHour}: managed {megabytes:0.0}MB, chunks {w.LoadedChunks}, archived {w.ArchivedChunks}, tiles {w.KnownTiles}, " +
                $"charges {w.SpentCharges}, holes {holes / 1024}KB, drills {sim.State.drills.Count}, rebirths {rebirths}, depth {sim.State.maxDepthEver}, " +
                $"worst tick {worstTick:0.0}ms, real {clock.Elapsed.TotalSeconds:0}s");
            worstTick = 0;
            nextHour++;
        }

        bool steady = settled <= 0 || peak <= settled * MemoryGrowthLimit;
        Console.WriteLine(steady ? $"ok: memory held at or below {peak:0.0}MB" : $"GROWING: {settled:0.0}MB at hour {SettleHours}, {peak:0.0}MB at peak");
        return steady ? 0 : 1;
    }

    private static void Spend(GameSim sim)
    {
        for (bool bought = true; bought;)
        {
            bought = false;
            SkillDef best = null;
            foreach (var skill in GameData.Skills)
                if (sim.CanBuy(skill) && (best == null || sim.SkillCost(skill) < sim.SkillCost(best)))
                    best = skill;
            if (best != null) bought = sim.Buy(best.Id);
        }

        for (int f = 0; f < GameData.Families.Count; f++)
            while (sim.CanBuyDrill(f)) sim.BuyDrill(f);
        while (sim.MergeAll()) { }

        for (int i = 0; i < GameData.Factory.Count; i++)
            while (sim.UpgradeFactory(i)) { }
        for (int i = 0; i < GameData.Research.Count; i++)
            if (sim.StartResearch(i)) break;
        while (sim.SendExpedition(1)) { }
    }
}
