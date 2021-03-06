﻿namespace AtomicTorch.CBND.CoreMod.CraftRecipes
{
    using System;
    using AtomicTorch.CBND.CoreMod.Items.Food;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Manufacturers;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.Crafting;

    public class RecipeRiceCooked : Recipe.RecipeForManufacturing
    {
        protected override void SetupRecipe(
            StationsList stations,
            out TimeSpan duration,
            InputItems inputItems,
            OutputItems outputItems)
        {
            stations.Add<ObjectStove>();
            stations.Add<ObjectStoveElectric>();

            duration = CraftingDuration.Medium;

            inputItems.Add<ItemRice>(count: 5);
            inputItems.Add<ItemBottleWater>(count: 1);

            outputItems.Add<ItemRiceCooked>(count: 1);
            outputItems.Add<ItemBottleEmpty>(count: 1);
        }
    }
}