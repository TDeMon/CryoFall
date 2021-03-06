﻿namespace AtomicTorch.CBND.CoreMod.Technologies.Tier4.Construction4
{
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Barrels;

    public class TechNodeCistern : TechNode<TechGroupConstruction4>
    {
        protected override void PrepareTechNode(Config config)
        {
            config.Effects
                  .AddStructure<ObjectCistern>();

            config.SetRequiredNode<TechNodeLandClaimT4>();
        }
    }
}