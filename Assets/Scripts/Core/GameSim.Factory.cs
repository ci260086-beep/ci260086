using System;

namespace DeepDrill.Core
{
    /// <summary>The surface factory, paid in ore, and core crystals, which only time can grow.</summary>
    public sealed partial class GameSim
    {
        public int Crystals => (int)State.crystals;

        /// <summary>How far the next crystal has grown, from 0 to 1.</summary>
        public double CrystalProgress => State.crystals - Math.Floor(State.crystals);

        public bool CrystalsFull => State.crystals >= GameData.CrystalCap;

        private void GrowCrystals(double seconds) =>
            State.crystals = Math.Min(GameData.CrystalCap, State.crystals + seconds / GameData.CrystalSeconds);

        public int FactoryLevel(int building) => State.LevelOf(FactoryKey(building));

        private static string FactoryKey(int building) => "factory_" + GameData.Factory[building].Id;

        public double FactoryOreCost(int building)
        {
            var def = GameData.Factory[building];
            return Math.Ceiling(def.OreCost * Math.Pow(def.OreGrowth, FactoryLevel(building)));
        }

        /// <summary>Every few levels a building also takes a crystal.</summary>
        public int FactoryCrystalCost(int building) =>
            (FactoryLevel(building) + 1) % GameData.FactoryCrystalEvery == 0 ? 1 : 0;

        public bool CanUpgradeFactory(int building) =>
            FactoryLevel(building) < GameData.Factory[building].MaxLevel
            && State.ores >= FactoryOreCost(building)
            && Crystals >= FactoryCrystalCost(building);

        public bool UpgradeFactory(int building)
        {
            if (!CanUpgradeFactory(building)) return false;
            State.ores -= FactoryOreCost(building);
            State.crystals -= FactoryCrystalCost(building);
            State.factory.Add(building);
            RefreshEffects();
            return true;
        }

        private void ApplyFactory()
        {
            for (int i = 0; i < GameData.Factory.Count; i++)
                AddPermanent(GameData.Factory[i].Effect, GameData.Factory[i].PerLevel * FactoryLevel(i));
        }
    }
}
