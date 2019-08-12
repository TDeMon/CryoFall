﻿namespace AtomicTorch.CBND.CoreMod.CraftRecipes
{
    using AtomicTorch.CBND.CoreMod.Items.Food;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.CraftingStations;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.Crafting;
    using System;

    public class RecipeDrinkSoft : Recipe.RecipeForStationCrafting
    {
        protected override void SetupRecipe(
            StationsList stations,
            out TimeSpan duration,
            InputItems inputItems,
            OutputItems outputItems)
        {
            stations.Add<ObjectCookingTable>();

            duration = CraftingDuration.Short;

            inputItems.Add<ItemCanEmpty>(count: 1);
            inputItems.Add<ItemPreservative>(count: 1);
            inputItems.Add<ItemBottleWater>(count: 1);
            inputItems.Add<ItemSugar>(count: 1);

            outputItems.Add<ItemDrinkSoft>();
            outputItems.Add<ItemBottleEmpty>();
        }
    }
}