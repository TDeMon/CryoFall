﻿namespace AtomicTorch.CBND.CoreMod.Quests.Tutorial
{
    using System.Linq;
    using AtomicTorch.CBND.CoreMod.Items.Equipment;
    using AtomicTorch.CBND.CoreMod.PlayerTasks;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.CraftingStations;
    using AtomicTorch.CBND.GameApi.Scripting;

    public class QuestCraftAndEquipBetterArmor : ProtoQuest
    {
        public const string EquipAnyChestplate = "Equip better chestplate";

        public const string EquipAnyHelmet = "Equip better helmet";

        public const string EquipAnyLegsProtection = "Equip better leg armor";

        public override string Description =>
            "It is time to get some better protection.";

        public override string Hints =>
            @"[*] You can unlock all relevant technologies in the ""Defense"" technology group.
              [*] Wooden armor may not be the best option out there, but it's still cheap to craft and certainly much better than cloth armor.";

        public override string Name => "Craft and equip better armor";

        public override ushort RewardLearningPoints => QuestConstants.TutorialRewardStage2;

        protected override void PrepareQuest(QuestsList prerequisites, TasksList tasks)
        {
            var headEquipmentExceptCloth = Api.FindProtoEntities<IProtoItemEquipmentHead>()
                                              .Where(i => !(i is ItemClothHat))
                                              .ToList();

            var chestEquipmentExceptCloth = Api.FindProtoEntities<IProtoItemEquipmentChest>()
                                               .Where(i => !(i is ItemClothShirt))
                                               .ToList();

            var legsEquipmentExceptCloth = Api.FindProtoEntities<IProtoItemEquipmentLegs>()
                                              .Where(i => !(i is ItemClothPants))
                                              .ToList();
            tasks
                .Add(TaskBuildStructure.Require<ObjectArmorerWorkbench>())
                // suggest wood helmet but require any head item except the cloth one
                .Add(TaskHaveItemEquipped.Require(
                         headEquipmentExceptCloth,
                         EquipAnyHelmet))
                // suggest wood chestplate but require any chest item except the cloth one
                .Add(TaskHaveItemEquipped.Require(
                         chestEquipmentExceptCloth,
                         EquipAnyChestplate))
                // suggest wood pants but require any legs item except the cloth one
                .Add(TaskHaveItemEquipped.Require(
                         legsEquipmentExceptCloth,
                         EquipAnyLegsProtection));

            prerequisites
                .Add<QuestExploreBiomes1>();
        }
    }
}