﻿namespace AtomicTorch.CBND.CoreMod.Technologies
{
    using AtomicTorch.CBND.CoreMod.Systems.Droplists;
    using AtomicTorch.CBND.CoreMod.Systems.PvE;
    using AtomicTorch.CBND.GameApi.Scripting;

    public static class ServerTechTimeGateHelper
    {
        public static bool IsAvailableT3Basic(DropItemContext context)
        {
            return IsTimeGateFinished(TechConstants.PvpTechTimeGameTier3Basic);
        }

        public static bool IsAvailableT3Specialized(DropItemContext context)
        {
            return IsTimeGateFinished(TechConstants.PvpTechTimeGameTier3Specialized);
        }

        public static bool IsAvailableT4Basic(DropItemContext context)
        {
            return IsTimeGateFinished(TechConstants.PvpTechTimeGameTier4Basic);
        }

        public static bool IsAvailableT4Specialized(DropItemContext context)
        {
            return IsTimeGateFinished(TechConstants.PvpTechTimeGameTier4Specialized);
        }

        private static bool IsTimeGateFinished(double timeGateDuration)
        {
            if (Api.IsEditor)
            {
                return true;
            }

            if (PveSystem.ServerIsPvE)
            {
                // no time-gating in PvE
                return true;
            }

            var timeRemains = timeGateDuration - Api.Server.Game.SecondsSinceWorldCreation;
            return timeRemains <= 0;
        }
    }
}