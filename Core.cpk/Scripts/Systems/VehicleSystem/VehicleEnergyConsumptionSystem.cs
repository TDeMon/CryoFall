﻿namespace AtomicTorch.CBND.CoreMod.Systems.VehicleSystem
{
    using System;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Triggers;
    using AtomicTorch.CBND.CoreMod.Vehicles;
    using AtomicTorch.CBND.GameApi;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class VehicleEnergyConsumptionSystem : ProtoSystem<VehicleEnergyConsumptionSystem>
    {
        public const string Notification_EnergyDepleted_Message = "Energy depleted.";

        public static readonly SoundResource SoundResourceNoEnergy
            = new SoundResource("Objects/Vehicles/NoEnergy");

        [NotLocalizable]
        public override string Name => "Vehicle energy consumption system";

        public static void ClientShowNotificationNotEnoughEnergy(IProtoVehicle protoVehicle)
        {
            Client.Audio.PlayOneShot(SoundResourceNoEnergy, volume: 0.4f);

            NotificationSystem.ClientShowNotification(
                string.Format(VehicleSystem.Notification_CannotUseVehicle_TitleFormat, protoVehicle.Name),
                Notification_EnergyDepleted_Message,
                color: NotificationColor.Bad,
                icon: protoVehicle.Icon,
                playSound: false);
        }

        public static void ServerNotifyClientNotEnoughEnergy(ICharacter character, IProtoVehicle protoVehicle)
        {
            Instance.CallClient(character,
                                _ => _.ClientRemote_OnNotEnoughEnergy(protoVehicle));
        }

        protected override void PrepareSystem()
        {
            if (IsClient)
            {
                // only server will consume vehicle energy
                return;
            }

            TriggerEveryFrame.ServerRegister(
                callback: ServerUpdate,
                name: "System." + this.ShortId);
        }

        private static void ServerUpdate()
        {
            // If update rate is 1, updating will happen for each character once a second.
            // We can set it to 2 to have updates every half second.
            // If we set it to 1/2 it will update every 2 seconds and so on.
            const double updateRate = 1 / 10.0;

            foreach (var character in Server.Characters.EnumerateAllPlayerCharactersWithSpread(updateRate,
                                                                                               onlyOnline: true,
                                                                                               exceptSpectators: false))
            {
                var vehicle = character.SharedGetCurrentVehicle();
                if (vehicle is null)
                {
                    return;
                }

                var protoVehicle = (IProtoVehicle)vehicle.ProtoGameObject;
                var isMoving = vehicle.PhysicsBody.Velocity != Vector2D.Zero;
                var consumption = isMoving
                                      ? protoVehicle.EnergyUsePerSecondMoving
                                      : protoVehicle.EnergyUsePerSecondIdle;

                consumption = (ushort)Math.Floor(consumption / updateRate);

                if (VehicleEnergySystem.ServerDeductEnergyCharge(vehicle, consumption))
                {
                    // consumed energy
                    continue;
                }

                // not enough power charge - remove vehicle's pilot
                if (vehicle.GetPublicState<VehiclePublicState>().IsDismountRequested)
                {
                    // quit requested/vehicle cannot be used and awaiting pilot's quit
                    continue;
                }

                VehicleSystem.ServerCharacterExitCurrentVehicle(character, force: false);
                Instance.CallClient(character,
                                    _ => _.ClientRemote_OnNotEnoughEnergy(protoVehicle));
            }
        }

        private void ClientRemote_OnNotEnoughEnergy(IProtoVehicle protoVehicle)
        {
            ClientShowNotificationNotEnoughEnergy(protoVehicle);
        }
    }
}