﻿namespace AtomicTorch.CBND.CoreMod.Items.Ammo
{
    using AtomicTorch.CBND.CoreMod.Items.Weapons;
    using AtomicTorch.CBND.GameApi.Data.Weapons;

    public class ItemAmmo10mmHollowPoint : ProtoItemAmmo, IAmmoCaliber10mm
    {
        public override string Description =>
            "Modification of the 10mm round specifically designed for biological targets. The projectile expands upon impact, providing much higher stopping power and damage. Completely ineffective against armored targets.";

        public override string Name => "10mm hollow-point ammo";

        protected override void PrepareDamageDescription(
            out double damageValue,
            out double armorPiercingCoef,
            out double finalDamageMultiplier,
            out double rangeMax,
            DamageDistribution damageDistribution)
        {
            damageValue = 8;
            armorPiercingCoef = 0;
            finalDamageMultiplier = 3.5;
            rangeMax = 10;
            damageDistribution.Set(DamageType.Kinetic, 0.9)
                              .Set(DamageType.Impact, 0.1);
        }

        protected override WeaponFireTracePreset PrepareFireTracePreset()
        {
            return WeaponFireTracePresets.Firearm;
        }
    }
}