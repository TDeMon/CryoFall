﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier1.Farming
{
    using AtomicTorch.CBND.CoreMod.CraftRecipes;

    public class TechNodeWateringCanCopper : TechNode<TechGroupFarming>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddRecipe<RecipeWateringCanCopper>();

            config.SetRequiredNode<TechNodeWateringCanWood>();
        }
    }
}