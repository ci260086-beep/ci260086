using System;
using System.Collections.Generic;

namespace DeepDrill.Core
{
    public enum SimEventKind
    {
        BlockHit,
        BlockBroken,
        DepthChanged,
        SkillBought,
        Explosion,
        FrenzyStarted,
        FrenzyEnded,
        Landed,
        BossStarted,
        BossDefeated,
        BossFailed,
        BossArmorBroken,
        DrillsMergedAll,
        ChallengeFailed,
        BossMoveWarned,
        BossMoveStruck,
        BossMoveEnded,
        Rebirth,
        LayerEntered,
        DrillsChanged,
        DrillMerged,
        RareFound,
        AchievementUnlocked,
        Overheated,
        PixelsDug,
        DrillBoosted,
        MilestoneReached,
        MasteryUp,
        SurfaceEventAppeared,
        SurfaceEventClaimed,
        SchoolCleared,
        ResearchDone,
        ExpeditionReturned,
        ExplorerRescued,
        ChallengeDone,
        RunRestarted,
        Warped,
    }

    public struct SimEvent
    {
        public SimEventKind Kind;
        public int Row;
        public int Col;
        public BlockType Block;
        public int Ore;
        public double Amount;
        public bool FromClick;
        public bool Crit;
        public bool Lead;
    }

    /// <summary>One drill on the field. Up to the simulation limit each owned drill gets a body.</summary>
    public sealed class DrillUnit
    {
        public readonly DrillBody Body = new DrillBody();
        public int Code;
        public double HitClock;
        public double LastSwing = -1;
        public double LastPull = double.MinValue;
        public bool PrefersLeft;

        /// <summary>Stacked with nowhere to go: this drill hammers the one below instead.</summary>
        public bool Boosting;
        public DrillUnit Below;


        public int Family => DrillCode.Family(Code);
        public int Tier => DrillCode.Tier(Code);
    }

    /// <summary>
    /// The whole game rule set. Knows nothing about Unity: the presentation layer calls Tick and
    /// Click, then drains <see cref="Events"/> to draw what happened. Needs GameData loaded.
    /// </summary>
    /// <summary>What happened while the game was closed, for the welcome back screen.</summary>
    public struct OfflineReport
    {
        public double SecondsAway;
        public double SecondsPaid;
        public double Rate;
        public double Coins;

        public bool Capped => SecondsAway > SecondsPaid;
    }

    public sealed partial class GameSim
    {
        private const int IncomeWindowSeconds = 10;
        private const int MaxSwingsPerUnitPerTick = 8;
        private const double HousekeepingSeconds = 1;
        private const int SwarmPerDrills = 10;
        private const int AutoBuysPerTick = 5;
        private const int MaxSpawnRowsUp = 60;
        private const int SpawnDropRows = 6;
        private const int SlideSearchColumns = 8;
        private const int MaxRejoinsPerCheck = 5;
        private const int MaxBoostChain = 400;
        public const string RootSkill = "root";
        private const int SwiftFamily = 1;
        private const int HeavyFamily = 2;
        private const int BlastFamily = 3;
        private const int RareRanks = 3;
        private const int MagnetFamily = 4;

        public readonly GameState State;
        public readonly List<DrillUnit> Units = new List<DrillUnit>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        private readonly Queue<(int row, int col)> pendingBlasts = new Queue<(int, int)>();
        private readonly Dictionary<long, DrillUnit> occupied = new Dictionary<long, DrillUnit>();
        private readonly double[] incomeBuckets = new double[IncomeWindowSeconds];
        private readonly double[] effects = new double[Enum.GetValues(typeof(SkillEffect)).Length];
        private readonly double[] layerBonus;
        private readonly double[] familyDamage;
        private readonly double[] familySpeed;
        private readonly double[] familyTrait;
        private readonly int[] familyMilestones;
        private readonly int[] masteryLevels;
        private double milestoneCrit;
        private readonly Random random;
        private int incomeIndex;
        private double incomeClock;
        private int incomeFilled;
        private double physicsClock;
        private double comboClock;
        private double housekeepingClock;
        private double swarmMultiplier = 1;
        private double drillBonus = 1;
        private readonly Dictionary<string, int> achievementTally = new Dictionary<string, int>();
        private readonly double[] rareWeight = new double[RareRanks];
        private int achievementsTallied = -1;
        private int raresTallied = -1;
        private int blastsThisChain;
        private int blastBudget;
        private bool insideBlast;
        private double simTime;
        private double heatClock;
        private double overheatLeft;

        public GameSim(GameState state, int? randomSeed = null)
        {
            State = state;
            random = new Random(randomSeed ?? state.seed);
            solidAt = SolidAtTiles;
            solidForStepper = SolidForStepper;
            layerBonus = new double[GameData.Layers.Count];
            familyDamage = new double[GameData.Families.Count];
            familySpeed = new double[GameData.Families.Count];
            familyTrait = new double[GameData.Families.Count];
            familyMilestones = new int[GameData.Families.Count];
            masteryLevels = new int[GameData.Layers.Count];
            for (int i = 0; i < masteryLevels.Length; i++) masteryLevels[i] = MasteryLevelFor(i);
            RefreshEffects();
            ResetWorld();

            if (!state.drillPlaced)
            {
                state.drillPlaced = true;
                state.drillX = Balance.StartColumn;
                state.drillY = -0.5 - DrillBody.HalfHeight;
            }
            if (state.drills.Count == 0) state.drills.Add(DrillCode.Make(0, 0));
            RebuildUnits();
        }

        public DrillUnit Lead { get; private set; }
        public OfflineReport LastOffline { get; private set; }
        private double runRateClock;
        public double Now => simTime;

        public bool BossActive { get; private set; }
        public double BossHp { get; private set; }
        public double BossArmor { get; private set; }
        public double BossTimeLeft { get; private set; }

        /// <summary>The extra speed from tapping, which fades while the player's hands are still.</summary>
        public double ClickBoost { get; private set; }

        /// <summary>The layer whose guardian is next, or null once all of them are down.</summary>
        public LayerDef CurrentBoss =>
            State.bossesDefeated < GameData.Layers.Count ? GameData.Layers[State.bossesDefeated] : null;
        public double BossMaxHp => CurrentBoss == null ? 0 : Balance.BlockHp(CurrentBoss.RowEnd) * CurrentBoss.BossHpMult * Balance.BossHpScale;
        public double BossMaxArmor => CurrentBoss == null || CurrentBoss.Index == 0 ? 0 : BossMaxHp * Balance.BossArmorRatio;
        public double BossDuration => CurrentBoss?.BossSeconds ?? 0;

        public int ReachedLayer => GameData.LayerOfRow(State.maxDepthEver);

        public int ShardsOnRebirth =>
            (int)Math.Floor(Balance.ShardsForRebirth(State.maxDepth, State.totalIncome)
                * (1 + (Effect(SkillEffect.shard_pct) + 5 * AchievementCount("rebirth_")) / 100) * Planet.Shards);
        public bool CanRebirth => State.AnyBossDown && ShardsOnRebirth > 0 && State.challenge < 0;

        /// <summary>Shards this run has earned per minute so far, zero until the run has settled.</summary>
        public double ShardsPerMinute =>
            State.runSeconds < Balance.RebirthRateMinSeconds ? 0
                : Balance.ShardProgress(State.maxDepth, State.totalIncome)
                    * (1 + (Effect(SkillEffect.shard_pct) + 5 * AchievementCount("rebirth_")) / 100) * Planet.Shards
                    / (State.runSeconds / 60);

        /// <summary>True once a run of some length has slipped well below its best rate, the moment a rebirth pays most.</summary>
        public bool RebirthAdvised =>
            CanRebirth
            && State.runSeconds >= Balance.RebirthAdviceMinSeconds
            && ShardsOnRebirth >= Balance.RebirthAdviceMinShards
            && ShardsPerMinute < State.peakShardRate * Balance.RebirthAdviceRatio;

        public int Combo { get; private set; }
        public double FrenzyEnergy { get; private set; }
        public double FrenzyTimeLeft { get; private set; }
        public bool InFrenzy => FrenzyTimeLeft > 0;
        public bool FrenzyReady => !InFrenzy && FrenzyEnergy >= 1;
        public double FrenzyDuration => Balance.FrenzySeconds + Effect(SkillEffect.frenzy_time);
        public double SwarmMultiplier => swarmMultiplier;

        public double ComboMultiplier =>
            1 + Math.Min(Balance.ComboBonusMax + (Effect(SkillEffect.combo_cap) + 10 * AchievementCount("combo_")) / 100,
                Balance.ComboBonusPerStep * (Combo / Balance.ComboStep));

        public double Effect(SkillEffect effect) => effects[(int)effect];

        /// <summary>The first level of the root skill: until then drills only move when tapped.</summary>
        public bool AutoDrillUnlocked => State.LevelOf(RootSkill) >= 1;

        // These two sit under the income of every dug pixel, so the walks over the lists are cached.
        private int AchievementCount(string prefix)
        {
            if (achievementsTallied != State.achievements.Count)
            {
                achievementsTallied = State.achievements.Count;
                achievementTally.Clear();
                foreach (string id in State.achievements)
                {
                    int cut = id.IndexOf('_');
                    if (cut < 0) continue;
                    string group = id.Substring(0, cut + 1);
                    achievementTally.TryGetValue(group, out int n);
                    achievementTally[group] = n + 1;
                }
            }
            return achievementTally.TryGetValue(prefix, out int count) ? count : 0;
        }

        // Each rare's bonus grows 50% per duplicate, up to ten duplicates.
        private double RareBonus(int rank, double perRare)
        {
            if (raresTallied != State.RareVersion)
            {
                raresTallied = State.RareVersion;
                Array.Clear(rareWeight, 0, rareWeight.Length);
                foreach (var rare in GameData.Rares)
                {
                    int count = State.RareCountOf(rare.Id);
                    if (count <= 0) continue;
                    int dupes = Math.Min(count - 1, Balance.RareMaxDupes);
                    rareWeight[rare.Rank] += 1 + Balance.RareDupeBonus * dupes;
                }
            }
            return perRare * rareWeight[rank];
        }

        private double GlobalDamageMultiplier
        {
            get
            {
                int layer = GameData.LayerOfRow(State.depth);
                double frenzy = InFrenzy
                    ? Balance.FrenzyDamageMultiplier * (1 + Effect(SkillEffect.frenzy_mult) / 100)
                    : 1;
                return (1 + Effect(SkillEffect.dmg_pct) / 100)
                    * (1 + Effect(SkillEffect.swarm_pct) / 100 * (State.drills.Count / SwarmPerDrills))
                    * (1 + layerBonus[layer] / 100)
                    * (1 + MasteryDamage(layer) / 100)
                    * (1 + Balance.ShardBonus * State.shards)
                    * drillBonus
                    * ComboMultiplier
                    * DamageBuffMult
                    * (1 + metaDamage / 100)
                    * ChallengeDamage
                    * Planet.Damage
                    * frenzy;
            }
        }

        public static double KindDamage(int code)
        {
            var family = GameData.Families[DrillCode.Family(code)];
            return family.Damage * Math.Pow(GameData.TierDamageMult, DrillCode.Tier(code));
        }

        /// <summary>What one swing of this kind of drill does right now, swarm share included.</summary>
        public double DamageOf(int code) =>
            KindDamage(code) * familyDamage[DrillCode.Family(code)] * GlobalDamageMultiplier * swarmMultiplier;

        public double ClickDamage => DamageOf(Lead.Code) * (1 + Effect(SkillEffect.click_pct) / 100);

        public double SpeedMultiplier =>
            (1 + Effect(SkillEffect.speed_pct) / 100)
            * SpeedBuffMult
            * MoveSpeed
            * (1 + ClickBoost)
            * (InFrenzy ? Balance.FrenzySpeedMultiplier : (Overheating ? Balance.OverheatSpeed : 1));

        /// <summary>In the Moho layer drills overheat in bursts; frenzy cools them.</summary>
        public bool Overheating => overheatLeft > 0 && !InFrenzy;

        public double HitsPerSecond(int code) =>
            GameData.Families[DrillCode.Family(code)].HitsPerSecond * familySpeed[DrillCode.Family(code)] * SpeedMultiplier;

        /// <summary>Combined damage per second of every owned drill.</summary>
        public double TotalDps
        {
            get
            {
                double sum = 0;
                foreach (int code in State.drills) sum += KindDamage(code) * familyDamage[DrillCode.Family(code)] * HitsPerSecond(code);
                return sum * GlobalDamageMultiplier;
            }
        }

        /// <summary>True where only frenzy still does full damage.</summary>
        public bool HeatWeakens => !InFrenzy && (Planet.Heat || GameData.LayerOfRow(State.depth) == Balance.CoreLayer);

        public int BlastRadius => Balance.BaseBlastRadius + (int)Effect(SkillEffect.blast_radius);

        private double TntChance => Balance.TntChance * (1 + Effect(SkillEffect.tnt_rate) / 100);
        private double ChestChance => Balance.ChestChance * (1 + Effect(SkillEffect.chest_pct) / 100);
        private int ChainLimit => Effect(SkillEffect.chain) > 0 ? Balance.LinkedChainLimit : Balance.BaseChainLimit;
        private double CritChance => Balance.CritChance + (Effect(SkillEffect.crit_chance) + milestoneCrit) / 100;
        private double CritMultiplier => Balance.CritMultiplier + Effect(SkillEffect.crit_mult) / 100;

        private double BlastDamage(int row) => Math.Max(
            Balance.BlockHp(row) * Balance.BlastFloorTiles,
            DamageOf(Lead.Code) * Balance.BlastSwings * (1 + (Effect(SkillEffect.blast_dmg) + 10 * AchievementCount("chain_")) / 100));

        private double IncomeMultiplier =>
            (1 + Effect(SkillEffect.gold_pct) / 100)
            * (1 + (2 * AchievementCount("reach_") + RareBonus(2, 5)) / 100)
            * (1 + metaIncome / 100)
            * ChallengePay
            * Planet.Income;

        public int SimulationLimit => Balance.SimulatedDrills + (int)Effect(SkillEffect.hangar_slots);

        private bool IsBossRow(int row)
        {
            for (int i = State.bossesDefeated; i < GameData.Layers.Count; i++)
            {
                int start = GameData.Layers[i].RowEnd;
                if (row >= start && row < start + Balance.BossRows) return true;
            }
            return false;
        }

        public void Tick(double dt)
        {
            blastBudget = (int)(Balance.MaxBlastPixelsPerSecond * dt); // by time, so any step length blasts at the same rate
            RunBlasts();
            ClickBoost = Math.Max(0, ClickBoost - Balance.ClickBoostDecay * dt);
            AdvanceRunClock(dt);
            AdvanceIncomeWindow(dt);
            AdvanceCombo(dt);
            AdvanceFrenzy(dt);
            AdvancePulse(dt);
            AdvanceLoose(dt);
            AdvanceLayerMechanics(dt);
            AdvancePhysics(dt);
            AdvanceBoss(dt);
            AdvanceHits(dt);
            AdvanceHousekeeping(dt);
            AdvanceEvents(dt);
            GrowCrystals(dt);
            AdvanceLab(dt);
        }

        public bool FamilyUnlocked(int family) => ReachedLayer >= GameData.Families[family].UnlockLayer;

        private double DiscountFraction =>
            Math.Min(0.9, (Effect(SkillEffect.buy_discount) + 2 * AchievementCount("drills_")) / 100);

        public int BuyTier => (int)Effect(SkillEffect.buy_tier);

        public double DrillPrice(int family) =>
            Balance.DrillPrice(GameData.Families[family].BasePrice, State.BoughtOf(family), DiscountFraction, BuyTier);

        public bool CanBuyDrill(int family) => FamilyUnlocked(family) && State.coins >= DrillPrice(family) && !Under(ChallengeRule.no_drills) && DrillRoom > 0;

        public bool BuyDrill(int family)
        {
            if (!CanBuyDrill(family)) return false;
            State.coins -= DrillPrice(family);
            State.AddBought(family);
            State.drills.Add(DrillCode.Make(family, Math.Min(BuyTier, GameData.Tiers - 1)));
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.DrillsChanged, Amount = family });
            return true;
        }

        /// <summary>Buys up to a number of a family's drills in one go.</summary>
        public int BuyDrills(int family, int count)
        {
            int bought = 0;
            while (bought < count && CanBuyDrill(family))
            {
                State.coins -= DrillPrice(family);
                State.AddBought(family);
                State.drills.Add(DrillCode.Make(family, Math.Min(BuyTier, GameData.Tiers - 1)));
                bought++;
            }
            if (bought == 0) return 0;
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.DrillsChanged, Amount = family });
            return bought;
        }

        /// <summary>What the next few of a family's drills would cost together.</summary>
        public double DrillsCost(int family, int count)
        {
            double total = 0;
            for (int i = 0; i < count; i++)
                total += Balance.DrillPrice(GameData.Families[family].BasePrice, State.BoughtOf(family) + i, DiscountFraction, BuyTier);
            return total;
        }

        /// <summary>How many of a family's drills the coins in hand would buy, up to a limit.</summary>
        public int AffordableDrills(int family, int limit)
        {
            if (!FamilyUnlocked(family) || Under(ChallengeRule.no_drills)) return 0;
            limit = Math.Min(limit, Math.Max(0, DrillRoom));
            double coins = State.coins;
            for (int i = 0; i < limit; i++)
            {
                coins -= Balance.DrillPrice(GameData.Families[family].BasePrice, State.BoughtOf(family) + i, DiscountFraction, BuyTier);
                if (coins < 0) return i;
            }
            return limit;
        }

        public bool MergeBlocked => Under(ChallengeRule.no_merge) || HasTrait(SchoolTrait.no_merge);

        public bool CanMerge(int code)
        {
            if (MergeBlocked || DrillCode.Tier(code) + 1 >= GameData.Tiers) return false;
            int found = 0;
            foreach (int c in State.drills)
                if (c == code && ++found >= 2) return true;
            return false;
        }

        /// <summary>Two drills of one kind become one of the next material, sometimes two steps up.</summary>
        public bool Merge(int code)
        {
            if (!CanMerge(code)) return false;
            MergeOnce(code);
            RebuildUnits();
            return true;
        }

        private void MergeOnce(int code)
        {
            State.drills.Remove(code);
            State.drills.Remove(code);
            double lucky = (Effect(SkillEffect.merge_lucky) + AchievementCount("tier_")) / 100;
            int steps = random.NextDouble() < lucky ? 2 : 1;
            int tier = Math.Min(GameData.Tiers - 1, DrillCode.Tier(code) + steps);
            State.drills.Add(DrillCode.Make(DrillCode.Family(code), tier));
            State.mergesEver++;
            State.bestTier = Math.Max(State.bestTier, tier + 1);
            Events.Add(new SimEvent { Kind = SimEventKind.DrillMerged, Amount = DrillCode.Make(DrillCode.Family(code), tier) });
        }

        /// <summary>
        /// Merges everything that can be merged, tier by tier so each new drill can pair up again.
        /// Works on counts, so a huge hangar costs no more than a small one, and sends one event.
        /// </summary>
        public int MergeEverything()
        {
            if (MergeBlocked) return 0;
            var counts = new SortedDictionary<int, int>();
            foreach (int code in State.drills)
                counts[TierFirst(code)] = counts.TryGetValue(TierFirst(code), out int n) ? n + 1 : 1;

            int merges = 0, best = -1;
            double lucky = (Effect(SkillEffect.merge_lucky) + AchievementCount("tier_")) / 100;
            for (int tier = 0; tier < GameData.Tiers - 1; tier++)
            {
                for (int family = 0; family < GameData.Families.Count; family++)
                {
                    int key = tier * MergeKeyStep + family;
                    if (!counts.TryGetValue(key, out int have) || have < 2) continue;
                    int pairs = have / 2;
                    counts[key] = have - pairs * 2;
                    for (int i = 0; i < pairs; i++)
                    {
                        int up = Math.Min(GameData.Tiers - 1, tier + (random.NextDouble() < lucky ? 2 : 1));
                        int upKey = up * MergeKeyStep + family;
                        counts[upKey] = counts.TryGetValue(upKey, out int above) ? above + 1 : 1;
                        State.bestTier = Math.Max(State.bestTier, up + 1);
                        int code = DrillCode.Make(family, up);
                        if (best < 0 || GameSim.KindDamage(code) > GameSim.KindDamage(best)) best = code;
                        merges++;
                    }
                }
            }
            if (merges == 0) return 0;

            State.mergesEver += merges;
            State.drills.Clear();
            foreach (var pair in counts)
                for (int i = 0; i < pair.Value; i++)
                    State.drills.Add(DrillCode.Make(pair.Key % MergeKeyStep, pair.Key / MergeKeyStep));
            RebuildUnits();
            Events.Add(new SimEvent { Kind = SimEventKind.DrillsMergedAll, Amount = best, Row = merges });
            return merges;
        }

        private const int MergeKeyStep = 100;

        private static int TierFirst(int code) => DrillCode.Tier(code) * MergeKeyStep + DrillCode.Family(code);

        /// <summary>Merges every pair that exists right now, one step up; true if any did.</summary>
        public bool MergeAll()
        {
            bool merged = MergePairs();
            if (merged) RebuildUnits();
            return merged;
        }

        private bool MergePairs()
        {
            if (MergeBlocked) return false;
            var counts = new Dictionary<int, int>();
            foreach (int code in State.drills)
                counts[code] = counts.TryGetValue(code, out int n) ? n + 1 : 1;
            bool merged = false;
            foreach (var pair in counts)
            {
                if (DrillCode.Tier(pair.Key) + 1 >= GameData.Tiers) continue;
                for (int i = 0; i < pair.Value / 2; i++)
                {
                    MergeOnce(pair.Key);
                    merged = true;
                }
            }
            return merged;
        }

        /// <summary>Brings the bodies on the field in line with the owned list.</summary>
        public void SyncDrills() => RebuildUnits();

        /// <summary>Tops up the frenzy meter; for the demo driver.</summary>
        public void FillFrenzy() => FrenzyEnergy = 1;

        public bool StartFrenzy()
        {
            if (!FrenzyReady || Under(ChallengeRule.no_frenzy)) return false;
            FrenzyEnergy = 0;
            FrenzyTimeLeft = FrenzyDuration;
            Events.Add(new SimEvent { Kind = SimEventKind.FrenzyStarted });
            return true;
        }

        public bool LayerReached(SkillDef skill) => ReachedLayer >= skill.RequiresLayer;

        public bool IsUnlocked(SkillDef skill)
        {
            if (!LayerReached(skill)) return false;
            foreach (string id in skill.Prereq)
                if (State.LevelOf(id) <= 0) return false;
            return true;
        }

        public bool CanBuy(SkillDef skill)
        {
            int level = State.LevelOf(skill.Id);
            if (Under(ChallengeRule.no_skills) && skill.Id != RootSkill) return false;
            return IsUnlocked(skill) && level < skill.MaxLevel && State.coins >= SkillCost(skill);
        }

        public bool Buy(string skillId)
        {
            var skill = GameData.Skill(skillId);
            if (skill == null || !CanBuy(skill)) return false;
            int level = State.LevelOf(skillId);
            State.coins -= SkillCost(skill);
            State.SetLevel(skillId, level + 1);
            RefreshEffects();
            Events.Add(new SimEvent { Kind = SimEventKind.SkillBought, Amount = level + 1 });
            return true;
        }

        /// <summary>Sets a skill directly and recomputes its effect; for tests and the demo driver.</summary>
        public void GrantSkill(string skillId, int level)
        {
            State.SetLevel(skillId, level);
            RefreshEffects();
        }

        /// <summary>Credits offline income and returns how much was added.</summary>
        public double ApplyOffline(long nowUnixSeconds)
        {
            if (State.lastSaveUnixSeconds <= 0 || !AutoDrillUnlocked) return 0;
            double away = nowUnixSeconds - State.lastSaveUnixSeconds;
            if (away > 0)
            {
                GrowCrystals(away);
                AdvanceLab(away);
            }
            double extraRate = Effect(SkillEffect.offline_pct) / 100, extraHours = Effect(SkillEffect.offline_hours);
            double earned = Balance.OfflineEarnings(State.incomePerSecond, away, extraRate, extraHours);
            State.coins += earned;
            State.totalIncome += earned;
            LastOffline = new OfflineReport
            {
                SecondsAway = away,
                SecondsPaid = Balance.OfflineSecondsPaid(away, extraHours),
                Rate = Balance.OfflineRate + extraRate,
                Coins = earned,
            };
            return earned;
        }

        // The run's clock and its best shard rate, sampled once a second.
        private void AdvanceRunClock(double dt)
        {
            State.runSeconds += dt;
            runRateClock += dt;
            if (runRateClock < 1) return;
            runRateClock = 0;
            State.peakShardRate = Math.Max(State.peakShardRate, ShardsPerMinute);
        }

        /// <summary>Trades the run for star core shards. Shards and lifetime records are all that stay.</summary>
        public bool Rebirth()
        {
            if (!CanRebirth) return false;
            int gained = ShardsOnRebirth;
            State.shards += gained;
            State.rebirths++;
            ResetRun();
            Events.Add(new SimEvent { Kind = SimEventKind.Rebirth, Amount = gained });
            return true;
        }

        // Clears everything a run owns; only shards and lifetime records stay.
        private void ResetRun()
        {
            double startGold = Balance.StartGold * Math.Max(1, Effect(SkillEffect.start_gold));
            bool keepAutoDrill = Effect(SkillEffect.start_gold) > 0;

            State.coins = startGold;
            State.ores = 0;
            State.depth = 0;
            State.maxDepth = 0;
            State.totalIncome = 0;
            State.incomePerSecond = 0;
            State.runSeconds = State.peakShardRate = 0;
            State.skills.Clear();
            if (keepAutoDrill) State.SetLevel(RootSkill, 1);
            State.drills.Clear();
            State.bought.Clear();
            State.dug.Clear();
            State.bossesDefeated = 0;
            State.seed = random.Next(1, int.MaxValue);
            State.school = State.secondSchool = -1;
            State.drillX = Balance.StartColumn;
            State.drillY = -0.5 - DrillBody.HalfHeight;

            ResetWorld();
            pendingBlasts.Clear();
            Array.Clear(incomeBuckets, 0, incomeBuckets.Length);
            incomeFilled = 0;
            incomeClock = physicsClock = comboClock = 0;
            Combo = 0;
            FrenzyEnergy = FrenzyTimeLeft = 0;
            BossActive = false;
            overheatLeft = heatClock = 0;

            GrantRunStart();
            RefreshEffects();
            Units.Clear();
            Lead = null;
            State.drills.Add(DrillCode.Make(0, 0));
            GrantStartDrills();
            RebuildUnits();
        }

        private void RefreshEffects()
        {
            Array.Clear(effects, 0, effects.Length);
            Array.Clear(layerBonus, 0, layerBonus.Length);
            foreach (var level in State.skills)
            {
                var skill = GameData.Skill(level.id);
                if (skill == null) continue;
                // the root's first level buys auto drilling, not damage
                int counted = level.id == RootSkill ? Math.Max(0, level.level - 1) : level.level;
                double amount = skill.PerLevel * counted;
                if (skill.Effect == SkillEffect.layer_pct) layerBonus[skill.ForLayer] += amount;
                effects[(int)skill.Effect] += amount;
            }
            RefreshPermanent();
            RefreshBonuses();
        }

        private void RefreshBonuses()
        {
            drillBonus = (1 + (3 * AchievementCount("boss_") + 2 * AchievementCount("merge_")) / 100.0)
                * (1 + RareBonus(0, 1) / 100);

            milestoneCrit = 0;
            for (int f = 0; f < familyDamage.Length; f++)
            {
                familyDamage[f] = familySpeed[f] = familyTrait[f] = 1;
                familyMilestones[f] = MilestonesOf(f);
                for (int i = 0; i < familyMilestones[f]; i++)
                {
                    var reward = GameData.Milestones[f][i];
                    if (reward.Kind == MilestoneKind.damage) familyDamage[f] += reward.Amount / 100;
                    if (reward.Kind == MilestoneKind.speed) familySpeed[f] += reward.Amount / 100;
                    if (reward.Kind == MilestoneKind.crit) milestoneCrit += reward.Amount;
                    if (reward.Kind == MilestoneKind.trait) familyTrait[f] = reward.Amount;
                }
            }
        }

        // Keeps the bodies in step with the owned list, strongest first, without moving drills
        // that stay on the field. Owned drills beyond the limit lend their power to the others.
        private void RebuildUnits()
        {
            var sorted = new List<int>(State.drills);
            sorted.Sort((a, b) => KindDamage(b).CompareTo(KindDamage(a)));
            int limit = Math.Min(sorted.Count, SimulationLimit);

            var spare = new Dictionary<int, Stack<DrillUnit>>();
            foreach (var unit in Units)
            {
                if (!spare.TryGetValue(unit.Code, out var stack)) spare[unit.Code] = stack = new Stack<DrillUnit>();
                stack.Push(unit);
            }

            var keep = new List<DrillUnit>(limit);
            var fresh = new List<DrillUnit>();
            for (int i = 0; i < limit; i++)
            {
                if (spare.TryGetValue(sorted[i], out var stack) && stack.Count > 0)
                {
                    keep.Add(stack.Pop());
                    continue;
                }
                var unit = new DrillUnit { Code = sorted[i], PrefersLeft = random.Next(2) == 0 };
                keep.Add(unit);
                fresh.Add(unit);
            }

            var leftovers = new List<DrillUnit>();
            foreach (var stack in spare.Values) leftovers.AddRange(stack);

            if (Lead != null && !keep.Contains(Lead))
            {
                // the lead was merged away: hand its place and the camera on
                DrillBody old = Lead.Body;
                if (fresh.Count > 0) fresh[0].Body.Place(old.X, old.Y);
                Lead = fresh.Count > 0 ? fresh[0] : keep[0];
                if (fresh.Count > 0) fresh.RemoveAt(0);
            }

            Units.Clear();
            Units.AddRange(keep);
            foreach (var unit in fresh)
            {
                if (Lead == null)
                {
                    unit.Body.Place(State.drillX, State.drillY);
                    Lead = unit;
                }
                else if (leftovers.Count > 0)
                {
                    var old = leftovers[leftovers.Count - 1];
                    leftovers.RemoveAt(leftovers.Count - 1);
                    unit.Body.Place(old.Body.X, old.Body.Y); // a merged drill takes over a spent one's spot
                }
                else
                {
                    SpawnNearLead(unit);
                }
            }

            double ownedPower = 0, activePower = 0;
            foreach (int code in State.drills) ownedPower += KindDamage(code) * GameData.Families[DrillCode.Family(code)].HitsPerSecond;
            foreach (var unit in Units) activePower += KindDamage(unit.Code) * GameData.Families[unit.Family].HitsPerSecond;
            swarmMultiplier = activePower > 0 ? ownedPower / activePower : 1;
        }

        private int EmptiestColumn()
        {
            int best = Lead.Body.Column, fewest = int.MaxValue;
            int width = Balance.SpawnSpreadColumns * 2 + 1;
            int start = random.Next(width);
            for (int i = 0; i < width; i++)
            {
                int offset = (start + i) % width - Balance.SpawnSpreadColumns;
                int col = Math.Max(1, Math.Min(Balance.Columns - 2, Lead.Body.Column + offset));
                int count = 0;
                foreach (var other in Units)
                    if (other.Body.Column == col) count++;
                if (count >= fewest) continue;
                fewest = count;
                best = col;
            }
            return best;
        }

        private void AdvanceHits(double dt)
        {
            if (!AutoDrillUnlocked || DrillsBound) return;
            foreach (var unit in Units)
            {
                unit.HitClock += dt * HitsPerSecond(unit.Code);
                int swings = 0;
                while (unit.HitClock >= 1 && swings < MaxSwingsPerUnitPerTick)
                {
                    unit.HitClock -= 1;
                    swings++;
                    Swing(unit);
                }
                if (unit.HitClock > 1) unit.HitClock = 1;
            }
        }

        private bool RollCrit() => random.NextDouble() < CritChance;

        private void RollRareDrop(int row)
        {
            int layer = GameData.LayerOfRow(row);
            double boost = 1 + Effect(SkillEffect.rare_pct) / 100;
            for (int i = 0; i < GameData.Rares.Count; i++)
            {
                var rare = GameData.Rares[i];
                if (rare.Layer != layer || random.NextDouble() >= rare.DropChance * boost) continue;
                State.AddRare(rare.Id);
                RefreshBonuses();
                Events.Add(new SimEvent { Kind = SimEventKind.RareFound, Amount = i });
            }
        }

        private void UpdateDepth()
        {
            int feet = Lead.Body.FeetRow;
            if (feet <= State.depth) return;
            int top = feet; // chunks stay until the last straggler has left them, or they rebuild every row
            foreach (var unit in Units) top = Math.Min(top, unit.Body.FeetRow);
            world.DropAbove(top);
            world.PrepareBelow(feet);
            int layer = GameData.LayerOfRow(feet);
            bool entered = layer != GameData.LayerOfRow(State.depth);
            State.depth = feet;
            State.maxDepth = Math.Max(State.maxDepth, feet);
            State.maxDepthEver = Math.Max(State.maxDepthEver, feet);
            Events.Add(new SimEvent { Kind = SimEventKind.DepthChanged, Row = feet });
            if (entered) Events.Add(new SimEvent { Kind = SimEventKind.LayerEntered, Amount = layer });
        }

        private static long CellKey(int row, int col) => row * 64L + col;

        private void AdvanceHousekeeping(double dt)
        {
            housekeepingClock += dt;
            if (housekeepingClock < HousekeepingSeconds) return;
            housekeepingClock = 0;
            BringBackStragglers();
            AutoHangar();
            CheckAchievements();
            CheckMilestones();
            CheckMastery();
            CheckSchoolCleared();
            CheckChallenge();
            RunAutomation();
        }

        // Drills that fall behind or wander off screen rejoin the lead, so the view stays busy.
        private void BringBackStragglers()
        {
            occupied.Clear();
            int moved = 0;
            foreach (var unit in Units)
            {
                if (unit == Lead) continue;
                bool behind = Lead.Body.Y - unit.Body.Y > Balance.StragglerRows;
                bool away = Math.Abs(Lead.Body.X - unit.Body.X) > Balance.StragglerColumns;
                if (!behind && !away) continue;
                SpawnNearLead(unit);
                if (++moved >= MaxRejoinsPerCheck) break; // a mass rejoin in one tick would hitch
            }
        }

        private void AutoHangar()
        {
            if (!AutoDrillUnlocked) return;
            bool changed = Effect(SkillEffect.auto_merge) > 0 && MergePairs();

            if (Effect(SkillEffect.auto_buy) > 0)
            {
                for (int i = 0; i < AutoBuysPerTick; i++)
                {
                    int cheapest = -1;
                    for (int f = 0; f < GameData.Families.Count; f++)
                        if (CanBuyDrill(f) && (cheapest < 0 || DrillPrice(f) < DrillPrice(cheapest))) cheapest = f;
                    if (cheapest < 0) break;
                    State.coins -= DrillPrice(cheapest);
                    State.AddBought(cheapest);
                    State.drills.Add(DrillCode.Make(cheapest, Math.Min(BuyTier, GameData.Tiers - 1)));
                    changed = true;
                }
            }
            if (changed) RebuildUnits();
        }

        /// <summary>How many of a family's milestones have been earned; a rebirth does not take them back.</summary>
        public int MilestonesOf(int family)
        {
            int best = State.FamilyBestOf(family), reached = 0;
            while (reached < GameData.MilestoneCounts.Count && best >= GameData.MilestoneCounts[reached]) reached++;
            return reached;
        }

        /// <summary>A layer's mastery level, from every block ever dug out of it.</summary>
        public int MasteryOf(int layer) => masteryLevels[layer];

        public double MasteryBlocksDug(int layer) => State.LayerPixelsOf(layer) / PixelsPerTile;

        public double MasteryDamage(int layer) => masteryLevels[layer] * GameData.MasteryDamagePct;

        public double MasteryValue(int layer) => masteryLevels[layer] * GameData.MasteryValuePct;

        private int MasteryLevelFor(int layer)
        {
            double blocks = MasteryBlocksDug(layer);
            int level = 0;
            while (level < GameData.MasteryBlocks.Count && blocks >= GameData.MasteryBlocks[level]) level++;
            return level;
        }

        private void CheckMilestones()
        {
            for (int f = 0; f < familyMilestones.Length; f++)
            {
                int was = familyMilestones[f], now = MilestonesOf(f);
                if (now == was) continue;
                RefreshBonuses();
                for (int i = was; i < now; i++)
                    Events.Add(new SimEvent { Kind = SimEventKind.MilestoneReached, Row = i, Amount = f });
            }
        }

        private void CheckMastery()
        {
            for (int layer = 0; layer < masteryLevels.Length; layer++)
            {
                int now = MasteryLevelFor(layer);
                if (now == masteryLevels[layer]) continue;
                masteryLevels[layer] = now;
                Events.Add(new SimEvent { Kind = SimEventKind.MasteryUp, Row = now, Amount = layer });
            }
        }

        /// <summary>The condition for an achievement, decoded from its id in the design data.</summary>
        public bool AchievementMet(AchievementDef a)
        {
            string id = a.Id;
            if (id == "center") return State.maxDepthEver >= GameData.TotalRows;
            int number = int.Parse(id.Substring(id.IndexOf('_') + 1));
            if (id.StartsWith("reach_", StringComparison.Ordinal)) return ReachedLayer >= number - 1;
            if (id.StartsWith("boss_", StringComparison.Ordinal)) return State.bossesEver >= number;
            if (id.StartsWith("drills_", StringComparison.Ordinal)) return State.drills.Count >= number;
            if (id.StartsWith("tier_", StringComparison.Ordinal)) return State.bestTier >= number;
            if (id.StartsWith("merge_", StringComparison.Ordinal)) return State.mergesEver >= number;
            if (id.StartsWith("combo_", StringComparison.Ordinal)) return State.bestCombo >= number;
            if (id.StartsWith("rebirth_", StringComparison.Ordinal)) return State.rebirths >= number;
            if (id.StartsWith("chain_", StringComparison.Ordinal)) return State.bestChain >= number;
            return false;
        }

        private void CheckAchievements()
        {
            for (int i = 0; i < GameData.Achievements.Count; i++)
            {
                var a = GameData.Achievements[i];
                if (State.achievements.Contains(a.Id) || !AchievementMet(a)) continue;
                State.achievements.Add(a.Id);
                RefreshBonuses();
                Events.Add(new SimEvent { Kind = SimEventKind.AchievementUnlocked, Amount = i });
            }
        }

        private void AdvanceBoss(double dt)
        {
            var layer = CurrentBoss;
            if (layer == null) return;
            if (!BossActive)
            {
                if (Lead.Body.FeetRow < layer.RowEnd || !Lead.Body.Grounded) return;
                BossActive = true;
                ResetBoss();
                Events.Add(new SimEvent { Kind = SimEventKind.BossStarted });
                return;
            }

            AdvanceBossMove(dt);
            BossTimeLeft -= dt;
            if (BossTimeLeft > 0) return;
            // it recovers: the player gets another attempt, usually after upgrading
            ResetBoss();
            Events.Add(new SimEvent { Kind = SimEventKind.BossFailed });
        }

        private void ResetBoss()
        {
            BossHp = BossMaxHp;
            BossArmor = BossMaxArmor;
            BossTimeLeft = BossDuration;
            ResetBossMove();
        }

        // Armour soaks the full hit and lets a reduced share through until it is gone.
        private void HitBossArmor(ref double amount)
        {
            if (BossArmor <= 0) return;
            BossArmor -= amount;
            amount *= Balance.BossArmorDamageRatio;
            if (BossArmor > 0) return;
            BossArmor = 0;
            Events.Add(new SimEvent { Kind = SimEventKind.BossArmorBroken });
        }

        private void AdvanceCombo(double dt)
        {
            if (Combo == 0) return;
            comboClock -= dt;
            if (comboClock <= 0) Combo = 0;
        }

        private void AdvanceFrenzy(double dt)
        {
            if (!InFrenzy) return;
            FrenzyTimeLeft -= dt;
            if (FrenzyTimeLeft > 0) return;

            FrenzyTimeLeft = 0;
            Events.Add(new SimEvent { Kind = SimEventKind.FrenzyEnded });
            if (Effect(SkillEffect.frenzy_quake) > 0)
            {
                Explode(Lead.Body.FeetRow, Lead.Body.Column, Balance.FrenzyQuakeRadius);
                RunBlasts();
            }
        }

        private void AdvanceIncomeWindow(double dt)
        {
            incomeClock += dt;
            while (incomeClock >= 1)
            {
                incomeClock -= 1;
                incomeFilled = Math.Min(incomeFilled + 1, IncomeWindowSeconds - 1);
                incomeIndex = (incomeIndex + 1) % IncomeWindowSeconds;
                incomeBuckets[incomeIndex] = 0;

                double sum = 0;
                foreach (double b in incomeBuckets) sum += b;
                State.incomePerSecond = sum / Math.Max(1, incomeFilled);
            }
        }

        private static int RoundToInt(double v) => (int)Math.Floor(v + 0.5);
    }
}
