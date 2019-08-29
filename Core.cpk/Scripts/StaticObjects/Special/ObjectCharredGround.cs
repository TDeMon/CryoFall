﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Special
{
    using AtomicTorch.CBND.CoreMod.ClientComponents.Rendering;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Explosives;
    using AtomicTorch.CBND.CoreMod.Systems.ServerTimers;
    using AtomicTorch.CBND.CoreMod.Systems.Weapons;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ObjectCharredGround
        : ProtoStaticWorldObject
            <EmptyPrivateState, ObjectCharredGround.PublicState, StaticObjectClientState>
    {
        // 5 minutes postpone if object is observed by any player
        public const double ObjectDespawnDurationPostponeIfObservedSeconds = 5 * 60;

        // despawn after 32 hours
        public const double ObjectDespawnDurationSeconds = 32 * 60 * 60;

        public const float Scale = 1f;

        public override double ClientUpdateIntervalSeconds => double.MaxValue;

        public override StaticObjectKind Kind => StaticObjectKind.FloorDecal;

        public override string Name => "Charred ground";

        public override ObjectSoundMaterial ObjectSoundMaterial => ObjectSoundMaterial.Stone;

        public override double ObstacleBlockDamageCoef => 0;

        public sealed override double ServerUpdateIntervalSeconds => double.MaxValue;

        public override float StructurePointsMax => 9001;

        public static void ServerSetWorldOffset(IStaticWorldObject worldObject, Vector2F worldOffset)
        {
            var publicState = GetPublicState(worldObject);
            publicState.WorldOffset = worldOffset;
        }

        public override bool SharedOnDamage(
            WeaponFinalCache weaponCache,
            IStaticWorldObject targetObject,
            double damagePreMultiplier,
            out double obstacleBlockDamageCoef,
            out double damageApplied)
        {
            obstacleBlockDamageCoef = 0;
            damageApplied = 0; // no damage
            return false; // no hit
        }

        protected override void ClientInitialize(ClientInitializeData data)
        {
            base.ClientInitialize(data);

            var tilePosition = data.GameObject.TilePosition;
            var renderer = data.ClientState.Renderer;
            if (ClientGroundExplosionAnimationHelper.IsGroundSpriteFlipped(tilePosition))
            {
                renderer.DrawMode = DrawMode.FlipHorizontally;
            }

            if (ClientGroundExplosionAnimationHelper.HasActiveExplosion(tilePosition))
            {
                // this is a fresh charred ground, animate the ground sprite
                var animationDuration = ClientGroundExplosionAnimationHelper.ExplosionGroundDuration;
                var framesTextureResources = ClientComponentSpriteSheetAnimator.CreateAnimationFrames(
                    ClientGroundExplosionAnimationHelper.ExplosionGroundTextureAtlas);
                var componentAnimator = renderer.SceneObject.AddComponent<ClientComponentSpriteSheetAnimator>();
                componentAnimator.Setup(renderer,
                                        framesTextureResources,
                                        frameDurationSeconds: animationDuration / framesTextureResources.Length);

                componentAnimator.IsLooped = false;
                componentAnimator.Destroy(1.5 * animationDuration);
            }
        }

        protected override void ClientSetupRenderer(IComponentSpriteRenderer renderer)
        {
            base.ClientSetupRenderer(renderer);

            var worldOffset = this.Layout.Center;
            if (renderer.SceneObject.AttachedWorldObject != null)
            {
                var publicState = GetPublicState((IStaticWorldObject)renderer.SceneObject.AttachedWorldObject);
                worldOffset = publicState.WorldOffset.ToVector2D();
            }

            renderer.PositionOffset = worldOffset;
            renderer.SpritePivotPoint = (0.5, 0.5);
            renderer.DrawOrder = DrawOrder.FloorCharredGround;
            renderer.Scale = Scale;
        }

        protected override void CreateLayout(StaticObjectLayout layout)
        {
            layout.Setup("#");
        }

        protected override void ServerInitialize(ServerInitializeData data)
        {
            base.ServerInitialize(data);

            // schedule destruction by timer
            var worldObject = data.GameObject;
            ServerTimersSystem.AddAction(
                delaySeconds: ObjectDespawnDurationSeconds,
                () => ServerDespawnTimerCallback(worldObject));
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            // no physics
        }

        private static void ServerDespawnTimerCallback(IStaticWorldObject worldObject)
        {
            if (!Server.World.IsObservedByAnyPlayer(worldObject))
            {
                // can destroy now
                Server.World.DestroyObject(worldObject);
                return;
            }

            // postpone destruction
            ServerTimersSystem.AddAction(
                delaySeconds: ObjectDespawnDurationPostponeIfObservedSeconds,
                () => ServerDespawnTimerCallback(worldObject));
        }

        public class PublicState : StaticObjectPublicState
        {
            [SyncToClient]
            public Vector2F WorldOffset { get; set; }
        }
    }
}