﻿namespace AtomicTorch.CBND.CoreMod.Systems.Weapons
{
    using System;
    using System.Collections.Generic;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Helpers.Physics;
    using AtomicTorch.CBND.CoreMod.Items.Ammo;
    using AtomicTorch.CBND.CoreMod.Items.Weapons;
    using AtomicTorch.CBND.CoreMod.Skills;
    using AtomicTorch.CBND.CoreMod.Systems.CharacterUnstuck;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.Weapons;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.DataStructures;
    using AtomicTorch.GameEngine.Common.Extensions;
    using AtomicTorch.GameEngine.Common.Helpers;
    using AtomicTorch.GameEngine.Common.Primitives;

    /// <summary>
    /// System to process weapon states (firing, cooldown, etc).
    /// </summary>
    public class WeaponSystem : ProtoSystem<WeaponSystem>
    {
        private const string RemoteCallSequenceGroupCharacterFiring = "CharacterWeaponFiringSequence";

        public override string Name => "Weapon system logic.";

        public static void ClientChangeWeaponFiringMode(bool isFiring)
        {
            var character = Client.Characters.CurrentPlayerCharacter;
            var state = PlayerCharacter.GetPrivateState(character).WeaponState;

            if (state.SharedGetInputIsFiring()
                == isFiring)
            {
                // no need to change the firing input mode
                return;
            }

            state.SharedSetInputIsFiring(isFiring);

            uint shotsDone = 0;
            if (!isFiring)
            {
                // stopping firing
                shotsDone = state.ShotsDone;
                if (SharedShouldFireMore(state))
                {
                    // assume we will attempt fire a shot more
                    var ammoConsumptionPerShot = state.ProtoWeapon.AmmoConsumptionPerShot;
                    if (ammoConsumptionPerShot > 0)
                    {
                        var ammoCountAvailable = state.ItemWeapon?
                                                      .GetPrivateState<WeaponPrivateState>()
                                                      .AmmoCount;

                        if (ammoCountAvailable.HasValue
                            && ammoCountAvailable.Value < ammoConsumptionPerShot)
                        {
                            // cannot shot more ammo than have loaded
                            ammoConsumptionPerShot = ammoCountAvailable.Value;
                        }
                    }

                    if (ammoConsumptionPerShot < 1)
                    {
                        ammoConsumptionPerShot = 1;
                    }

                    shotsDone += ammoConsumptionPerShot;
                }
            }

            // set firing mode on server
            Instance.CallServer(_ => _.ServerRemote_SetWeaponFiringMode(isFiring, shotsDone));
            //Logger.Dev(isFiring
            //               ? "SetWeaponFiringMode: firing!"
            //               : $"SetWeaponFiringMode: stop firing! Shots done: {shotsDone}");
        }

        public static void RebuildWeaponCache(
            ICharacter character,
            WeaponState weaponState)
        {
            DamageDescription damageDescription = null;
            var item = weaponState.ItemWeapon;
            var protoItem = weaponState.ProtoWeapon;
            if (protoItem == null)
            {
                return;
            }

            IProtoItemAmmo protoAmmo = null;
            if (item != null)
            {
                var weaponPrivateState = item.GetPrivateState<WeaponPrivateState>();
                protoAmmo = weaponPrivateState.CurrentProtoItemAmmo;
            }

            if (protoItem.OverrideDamageDescription != null)
            {
                damageDescription = protoItem.OverrideDamageDescription;
            }
            else if (protoAmmo != null)
            {
                damageDescription = protoAmmo.DamageDescription;
            }

            weaponState.WeaponCache = new WeaponFinalCache(
                character,
                character.SharedGetFinalStatsCache(),
                item,
                weaponState.ProtoWeapon,
                protoAmmo,
                damageDescription);
        }

        public static Vector2D SharedOffsetHitWorldPositionCloserToObjectCenter(
            IWorldObject worldObject,
            IProtoWorldObject protoWorldObject,
            Vector2D hitPoint,
            bool isRangedWeapon)
        {
            var objectCenterPosition = protoWorldObject.SharedGetObjectCenterWorldOffset(worldObject);
            var coef = isRangedWeapon ? 0.2 : 0.5;
            var offset = coef * (objectCenterPosition - hitPoint);
            // don't offset more than 0.5 tiles
            offset = offset.ClampMagnitude(0.5);
            hitPoint += offset;
            return hitPoint;
        }

        public static void SharedUpdateCurrentWeapon(
            ICharacter character,
            WeaponState state,
            double deltaTime)
        {
            var protoWeapon = state.ProtoWeapon;
            if (protoWeapon == null)
            {
                return;
            }

            if (state.CooldownSecondsRemains > 0)
            {
                state.CooldownSecondsRemains -= deltaTime;
            }

            if (state.ReadySecondsRemains > 0)
            {
                state.ReadySecondsRemains -= deltaTime;
            }

            if (state.FirePatternCooldownSecondsRemains > 0)
            {
                state.FirePatternCooldownSecondsRemains -= deltaTime;

                if (state.FirePatternCooldownSecondsRemains <= 0)
                {
                    state.FirePatternCurrentShotNumber = 0;
                }
            }

            // TODO: restore this condition when we redo UI countdown animation for ViewModelHotbarItemWeaponOverlayControl.ReloadDurationSeconds
            //if (state.CooldownSecondsRemains <= 0)
            //{
            WeaponAmmoSystem.SharedUpdateReloading(state, character, deltaTime);
            //}

            if (Api.IsServer
                && !character.ServerIsOnline
                && state.SharedGetInputIsFiring())
            {
                state.SharedSetInputIsFiring(false);
            }

            // check ammo (if applicable to this weapon prototype)
            var canFire = state.WeaponReloadingState == null
                          && protoWeapon.SharedCanFire(character, state);
            if (state.CooldownSecondsRemains > 0)
            {
                // firing cooldown is not completed
                if (!state.SharedGetInputIsFiring()
                    && state.IsEventWeaponStartSent)
                {
                    // not firing anymore
                    SharedCallOnWeaponInputStop(state, character);
                }

                return;
            }

            var wasFiring = state.IsFiring;
            if (!state.IsFiring)
            {
                state.IsFiring = state.SharedGetInputIsFiring();
            }
            else // if IsFiring
            {
                if (!SharedShouldFireMore(state))
                {
                    state.IsFiring = state.SharedGetInputIsFiring();
                }
            }

            if (!canFire)
            {
                // cannot fire (no ammo, etc)
                state.IsFiring = false;
            }

            if (!state.IsFiring)
            {
                if (wasFiring)
                {
                    // just stopped firing
                    SharedCallOnWeaponFinished(state, character);
                }

                // the character is not firing
                // reset delay for the next shot (it will be set when firing starts next time)
                state.DamageApplyDelaySecondsRemains = 0;
                return;
            }

            // let's process what happens when we're in the firing mode
            if (!state.IsEventWeaponStartSent)
            {
                // started firing
                SharedCallOnWeaponStart(state, character);
            }

            if (state.DamageApplyDelaySecondsRemains <= 0)
            {
                // initialize delay to next shot
                state.DamageApplyDelaySecondsRemains = protoWeapon.DamageApplyDelay;
                if (state.WeaponCache is null)
                {
                    RebuildWeaponCache(character, state);
                }

                SharedCallOnWeaponShot(character, protoWeapon, state.WeaponCache);
            }

            // decrease the remaining time to the damage application
            state.DamageApplyDelaySecondsRemains -= deltaTime;

            if (state.DamageApplyDelaySecondsRemains > 0)
            {
                // firing delay not completed
                return;
            }

            // firing delay completed
            state.ShotsDone++;
            //Logger.Dev("Weapon fired, shots done: " + state.ShotsDone);
            SharedFireWeapon(character, state.ItemWeapon, protoWeapon, state);
            state.CooldownSecondsRemains += protoWeapon.FireInterval - protoWeapon.DamageApplyDelay;

            if (!protoWeapon.IsLoopedAttackAnimation)
            {
                // we don't want to stuck this animation in the last frame
                // that's fix for the issue:
                // "Fix extended animation "stuck" issue for mobs (like limbs stuck in the end position and movement animation appears broken)"
                state.IsEventWeaponStartSent = false;
            }
        }

        private static Vector2D GetShotEndPosition(
            bool damageRayStoppedOnLastHit,
            List<WeaponHitData> hitObjects,
            Vector2D toPosition,
            bool isRangedWeapon)
        {
            if (!damageRayStoppedOnLastHit)
            {
                return toPosition;
            }

            var hitData = hitObjects[hitObjects.Count - 1];
            var hitPoint = hitData.HitPoint.ToVector2D();
            var worldObject = hitData.WorldObject;
            hitPoint = SharedOffsetHitWorldPositionCloserToObjectCenter(
                worldObject,
                worldObject.ProtoWorldObject,
                hitPoint,
                isRangedWeapon);

            var hitWorldPosition = worldObject switch
            {
                IDynamicWorldObject dynamicWorldObject => dynamicWorldObject.Position,
                _                                      => worldObject.TilePosition.ToVector2D(),
            };
            return hitWorldPosition + hitPoint;
        }

        private static void ServerCheckFiredShotsMismatch(WeaponState state, ICharacter character)
        {
            var ammoConsumptionPerShot = state.ProtoWeapon.AmmoConsumptionPerShot;
            if (ammoConsumptionPerShot == 0)
            {
                // weapon doesn't use any ammo - no problem with possible desync
                return;
            }

            var requestedShotsCount = state.ServerLastClientReportedShotsDoneCount;
            if (!requestedShotsCount.HasValue)
            {
                return;
            }

            var extraShotsDone = (int)(state.ShotsDone - (long)requestedShotsCount.Value);
            state.ServerLastClientReportedShotsDoneCount = null;

            if (extraShotsDone == 0)
            {
                return;
            }

            if (extraShotsDone < 0)
            {
                // should never happen as server should fire as much as client requested, always
                return;
            }

            var itemWeapon = state.ItemWeapon;
            if (itemWeapon == null)
            {
                return;
            }

            //Logger.Dev($"Shots count mismatch: requested={requestedShotsCount} actualShotsDone={state.ShotsDone}");

            Instance.CallClient(character,
                                _ => _.ClientRemote_FixAmmoCount(itemWeapon, extraShotsDone));
        }

        private static void SharedCallOnWeaponFinished(WeaponState state, ICharacter character)
        {
            if (IsServer)
            {
                ServerCheckFiredShotsMismatch(state, character);
            }

            state.IsEventWeaponStartSent = false;

            if (IsClient)
            {
                // finished firing weapon on Client-side
                WeaponSystemClientDisplay.OnWeaponFinished(character);
            }
            else // if this is Server
            {
                // notify other clients about finished firing weapon
                using var scopedBy = Api.Shared.GetTempList<ICharacter>();
                Server.World.GetScopedByPlayers(character, scopedBy);
                Instance.CallClient(scopedBy,
                                    _ => _.ClientRemote_OnWeaponFinished(character));
            }
        }

        private static void SharedCallOnWeaponHitOrTrace(
            ICharacter firingCharacter,
            IProtoItemWeapon protoWeapon,
            IProtoItemAmmo protoAmmo,
            Vector2D endPosition,
            List<WeaponHitData> hitObjects,
            bool endsWithHit)
        {
            if (IsClient)
            {
                // display weapon shot on Client-side
                WeaponSystemClientDisplay.OnWeaponHitOrTrace(firingCharacter,
                                                             protoWeapon,
                                                             protoAmmo,
                                                             firingCharacter.ProtoCharacter,
                                                             firingCharacter.Position.ToVector2Ushort(),
                                                             hitObjects,
                                                             endPosition,
                                                             endsWithHit);
            }
            else // if server
            {
                // display damages on clients in scope of every damaged object
                var observers = new HashSet<ICharacter>();
                using var tempList = Api.Shared.GetTempList<ICharacter>();
                Server.World.GetScopedByPlayers(firingCharacter, tempList);
                observers.AddRange(tempList);

                foreach (var hitObject in hitObjects)
                {
                    if (hitObject.WorldObject.IsDestroyed)
                    {
                        continue;
                    }

                    if (hitObject.WorldObject is ICharacter damagedCharacter)
                    {
                        // notify the damaged character
                        observers.Add(damagedCharacter);
                    }

                    Server.World.GetScopedByPlayers(hitObject.WorldObject, tempList);
                    tempList.Clear();
                    observers.AddRange(tempList);
                }

                // add all observers within the sound radius (so they can not only hear but also see the traces)
                var eventNetworkRadius = (byte)Math.Max(
                    15,
                    Math.Ceiling(protoWeapon.SoundPresetWeaponDistance.max));

                tempList.Clear();
                Server.World.GetCharactersInRadius(firingCharacter.TilePosition,
                                                   tempList,
                                                   radius: eventNetworkRadius,
                                                   onlyPlayers: true);
                observers.AddRange(tempList);

                // don't notify the attacking character
                observers.Remove(firingCharacter);

                if (observers.Count > 0)
                {
                    Instance.CallClient(observers,
                                        _ => _.ClientRemote_OnWeaponHitOrTrace(firingCharacter,
                                                                               protoWeapon,
                                                                               protoAmmo,
                                                                               firingCharacter.ProtoCharacter,
                                                                               firingCharacter.Position.ToVector2Ushort(),
                                                                               hitObjects.ToArray(),
                                                                               endPosition,
                                                                               endsWithHit));
                }
            }
        }

        private static void SharedCallOnWeaponInputStop(WeaponState state, ICharacter character)
        {
            Api.Assert(state.IsEventWeaponStartSent, "Firing event must be set");
            state.IsEventWeaponStartSent = false;

            if (IsClient)
            {
                // finished firing weapon on Client-side
                WeaponSystemClientDisplay.OnWeaponInputStop(character);
            }
            else // if this is Server
            {
                // notify other clients about finished firing weapon
                using var scopedBy = Api.Shared.GetTempList<ICharacter>();
                Server.World.GetScopedByPlayers(character, scopedBy);
                Instance.CallClient(
                    scopedBy,
                    _ => _.ClientRemote_OnWeaponInputStop(character));
            }
        }

        private static void SharedCallOnWeaponShot(
            ICharacter character,
            IProtoItemWeapon protoWeapon,
            WeaponFinalCache weaponCache)
        {
            if (IsClient)
            {
                // start firing weapon on Client-side
                WeaponSystemClientDisplay.OnWeaponShot(character,
                                                       protoWeapon: protoWeapon,
                                                       protoCharacter: character.ProtoCharacter,
                                                       fallbackPosition: character.Position.ToVector2Ushort());
            }
            else // if IsServer
            {
                using var observers = Api.Shared.GetTempList<ICharacter>();
                var eventNetworkRadius = (byte)Math.Max(
                    15,
                    Math.Ceiling(protoWeapon.SoundPresetWeaponDistance.max));

                Server.World.GetCharactersInRadius(character.TilePosition,
                                                   observers,
                                                   radius: eventNetworkRadius,
                                                   onlyPlayers: true);
                observers.Remove(character);

                Instance.CallClient(observers,
                                    _ => _.ClientRemote_OnWeaponShot(character,
                                                                     protoWeapon,
                                                                     character.ProtoCharacter,
                                                                     character.Position.ToVector2Ushort()));
            }
        }

        private static void SharedCallOnWeaponStart(WeaponState state, ICharacter character)
        {
            Api.Assert(!state.IsEventWeaponStartSent, "Firing event must be not set");
            state.IsEventWeaponStartSent = true;

            if (IsClient)
            {
                // start firing weapon on Client-side
                WeaponSystemClientDisplay.OnWeaponStart(character);
            }
            else // if IsServer
            {
                using var scopedBy = Api.Shared.GetTempList<ICharacter>();
                Server.World.GetScopedByPlayers(character, scopedBy);
                Instance.CallClient(scopedBy,
                                    _ => _.ClientRemote_OnWeaponStart(character));
            }
        }

        private static void SharedFireWeapon(
            ICharacter character,
            IItem weaponItem,
            IProtoItemWeapon protoWeapon,
            WeaponState weaponState)
        {
            protoWeapon.SharedOnFire(character, weaponState);

            var playerCharacterSkills = character.SharedGetSkills();
            var protoWeaponSkill = playerCharacterSkills != null
                                       ? protoWeapon.WeaponSkillProto
                                       : null;
            if (IsServer)
            {
                protoWeaponSkill?.ServerOnShot(playerCharacterSkills); // give experience for shot
                CharacterUnstuckSystem.ServerTryCancelUnstuckRequest(character);
            }

            var weaponCache = weaponState.WeaponCache;
            if (weaponCache == null)
            {
                RebuildWeaponCache(character, weaponState);
                weaponCache = weaponState.WeaponCache;
            }

            var characterCurrentVehicle = character.IsNpc
                                              ? null
                                              : character.SharedGetCurrentVehicle();

            var isMeleeWeapon = protoWeapon is IProtoItemWeaponMelee;
            var characterProtoCharacter = (IProtoCharacterCore)character.ProtoCharacter;
            var fromPosition = characterProtoCharacter.SharedGetWeaponFireWorldPosition(character, isMeleeWeapon);
            var fireSpreadAngleOffsetDeg = SharedUpdateAndGetFirePatternCurrentSpreadAngleDeg(weaponState, protoWeapon);

            var collisionGroup = isMeleeWeapon
                                     ? CollisionGroups.HitboxMelee
                                     : CollisionGroups.HitboxRanged;

            using var allHitObjects = Api.Shared.GetTempList<IWorldObject>();
            var shotsPerFire = weaponCache.FireScatterPreset.ProjectileAngleOffets;
            foreach (var angleOffsetDeg in shotsPerFire)
            {
                SharedShotWeaponHitscan(character,
                                        protoWeapon,
                                        fromPosition,
                                        weaponCache,
                                        characterProtoCharacter,
                                        fireSpreadAngleOffsetDeg + angleOffsetDeg,
                                        collisionGroup,
                                        isMeleeWeapon,
                                        characterCurrentVehicle,
                                        protoWeaponSkill,
                                        playerCharacterSkills,
                                        allHitObjects);
            }

            if (IsServer)
            {
                protoWeapon.ServerOnShot(character, weaponItem, protoWeapon, allHitObjects);
            }
        }

        private static bool SharedHasTileObstacle(
            ICharacter character,
            byte characterTileHeight,
            ICharacter damagedCharacter)
        {
            const double maxFirindgDistanceThroughPits = 2;

            var characterPosition = character.Position;
            var anyCliffIsAnObstacle = characterTileHeight != damagedCharacter.Tile.Height;

            using var testResults = character.PhysicsBody.PhysicsSpace.TestLine(
                characterPosition,
                damagedCharacter.Position,
                collisionGroup: CollisionGroups.Default);

            foreach (var testResult in testResults)
            {
                var testResultPhysicsBody = testResult.PhysicsBody;
                var protoTile = testResultPhysicsBody.AssociatedProtoTile;

                if (ReferenceEquals(protoTile, null)
                    || protoTile.Kind != TileKind.Solid)
                {
                    continue;
                }

                var testResultPosition = testResultPhysicsBody.Position;
                var attackedTile = IsServer
                                       ? Server.World.GetTile((Vector2Ushort)testResultPosition)
                                       : Client.World.GetTile((Vector2Ushort)testResultPosition);
                if (attackedTile.IsSlope)
                {
                    // slope is not an obstacle
                    continue;
                }

                // found collision with a cliff
                if (anyCliffIsAnObstacle)
                {
                    return true;
                }

                if (attackedTile.Height >= characterTileHeight)
                {
                    return true; // cliff to higher tile is always an obstacle
                }

                // TODO: we can remove this check when we will have proper AI system with pathfinding
                if (characterPosition.DistanceSquaredTo(testResultPosition + (0.5, 0.5))
                    > maxFirindgDistanceThroughPits * maxFirindgDistanceThroughPits)
                {
                    // cliff to a lower height is an obstacle as it's too far from the firing position
                    return true;
                }
            }

            return false; // no obstacles
        }

        private static void SharedShotWeaponHitscan(
            ICharacter character,
            IProtoItemWeapon protoWeapon,
            Vector2D fromPosition,
            WeaponFinalCache weaponCache,
            IProtoCharacterCore characterProtoCharacter,
            double fireSpreadAngleOffsetDeg,
            CollisionGroup collisionGroup,
            bool isMeleeWeapon,
            IDynamicWorldObject characterCurrentVehicle,
            ProtoSkillWeapons protoWeaponSkill,
            PlayerCharacterSkills playerCharacterSkills,
            ITempList<IWorldObject> allHitObjects)
        {
            var toPosition = fromPosition
                             + new Vector2D(weaponCache.RangeMax, 0)
                                 .RotateRad(characterProtoCharacter.SharedGetRotationAngleRad(character)
                                            + fireSpreadAngleOffsetDeg * Math.PI / 180.0);
            using var lineTestResults = character.PhysicsBody.PhysicsSpace.TestLine(
                fromPosition: fromPosition,
                toPosition: toPosition,
                collisionGroup: collisionGroup);
            var damageMultiplier = 1d;
            var hitObjects = new List<WeaponHitData>(isMeleeWeapon ? 1 : lineTestResults.Count);
            var characterTileHeight = character.Tile.Height;

            if (IsClient
                || Api.IsEditor)
            {
                SharedEditorPhysicsDebugger.SharedVisualizeTestResults(lineTestResults, collisionGroup);
            }

            var isDamageRayStopped = false;
            foreach (var testResult in lineTestResults)
            {
                var testResultPhysicsBody = testResult.PhysicsBody;
                var attackedProtoTile = testResultPhysicsBody.AssociatedProtoTile;
                if (attackedProtoTile != null)
                {
                    if (attackedProtoTile.Kind != TileKind.Solid)
                    {
                        // non-solid obstacle - skip
                        continue;
                    }

                    var attackedTile = IsServer
                                           ? Server.World.GetTile((Vector2Ushort)testResultPhysicsBody.Position)
                                           : Client.World.GetTile((Vector2Ushort)testResultPhysicsBody.Position);

                    if (attackedTile.Height < characterTileHeight)
                    {
                        // attacked tile is below - ignore it
                        continue;
                    }

                    // tile on the way - blocking damage ray
                    break;
                }

                var damagedObject = testResultPhysicsBody.AssociatedWorldObject;
                if (damagedObject == character
                    || damagedObject == characterCurrentVehicle)
                {
                    // ignore collision with self
                    continue;
                }

                if (!(damagedObject.ProtoGameObject is IDamageableProtoWorldObject damageableProto))
                {
                    // shoot through this object
                    continue;
                }

                var damagedCharacter = damagedObject as ICharacter;
                if (!ReferenceEquals(damagedCharacter, null))
                {
                    // don't allow damage is there is no direct line of sight on physical colliders layer between the two objects
                    if (SharedHasTileObstacle(character, characterTileHeight, damagedCharacter))
                    {
                        continue;
                    }
                }
                else if (damagedObject is IStaticWorldObject staticWorldObject
                         && characterTileHeight != staticWorldObject.OccupiedTile.Height)
                {
                    // don't allow damage to static objects on a different height level
                    continue;
                }

                using (CharacterDamageContext.Create(attackerCharacter: character,
                                                     damagedCharacter,
                                                     protoWeaponSkill))
                {
                    if (!damageableProto.SharedOnDamage(
                            weaponCache,
                            damagedObject,
                            damageMultiplier,
                            out var obstacleBlockDamageCoef,
                            out var damageApplied))
                    {
                        // not hit
                        continue;
                    }

                    if (IsServer)
                    {
                        weaponCache.ProtoWeapon
                                   .ServerOnDamageApplied(weaponCache, damagedObject, damageApplied);

                        if (damageApplied > 0
                            && !ReferenceEquals(damagedCharacter, null))
                        {
                            CharacterUnstuckSystem.ServerTryCancelUnstuckRequest(damagedCharacter);
                        }

                        if (damageApplied > 0)
                        {
                            // give experience for damage
                            protoWeaponSkill?.ServerOnDamageApplied(playerCharacterSkills,
                                                                    damagedObject,
                                                                    damageApplied);
                        }
                    }

                    if (obstacleBlockDamageCoef < 0
                        || obstacleBlockDamageCoef > 1)
                    {
                        Logger.Error(
                            "Obstacle block damage coefficient should be >= 0 and <= 1 - wrong calculation by "
                            + damageableProto);
                        break;
                    }

                    //var hitPosition = testResultPhysicsBody.Position + testResult.Penetration;
                    hitObjects.Add(new WeaponHitData(damagedObject,
                                                     testResult.Penetration.ToVector2F())); //, hitPosition));

                    if (isMeleeWeapon)
                    {
                        // currently melee weapon could attack only one object on the ray
                        break;
                    }

                    damageMultiplier = damageMultiplier * (1.0 - obstacleBlockDamageCoef);
                    if (damageMultiplier <= 0)
                    {
                        // target blocked the damage ray
                        isDamageRayStopped = true;
                        break;
                    }
                }
            }

            var shotEndPosition = GetShotEndPosition(isDamageRayStopped,
                                                     hitObjects,
                                                     toPosition,
                                                     isRangedWeapon: !isMeleeWeapon);
            SharedCallOnWeaponHitOrTrace(character,
                                         protoWeapon,
                                         weaponCache.ProtoAmmo,
                                         shotEndPosition,
                                         hitObjects,
                                         endsWithHit: isDamageRayStopped);

            foreach (var entry in hitObjects)
            {
                if (!allHitObjects.Contains(entry.WorldObject))
                {
                    allHitObjects.Add(entry.WorldObject);
                }
            }
        }

        private static bool SharedShouldFireMore(WeaponState state)
        {
            if (!state.IsFiring)
            {
                return false;
            }

            // is firing delay completed?
            var canStopFiring = state.DamageApplyDelaySecondsRemains <= 0;

            if (canStopFiring
                && IsServer
                && state.ServerLastClientReportedShotsDoneCount.HasValue)
            {
                // cannot stop firing if not all the ammo are fired yet
                if (state.ShotsDone < state.ServerLastClientReportedShotsDoneCount)
                {
                    // let's spend all the remaining ammo before stopping firing
                    canStopFiring = false;
                    //Logger.Dev("Not all shots done yet, delay stopping firing: shotsDone="
                    //           + state.ShotsDone
                    //           + " requiresShotsDone="
                    //           + state.ServerLastClientReportedShotsDoneCount);
                }
            }

            return !canStopFiring;
        }

        private static double SharedUpdateAndGetFirePatternCurrentSpreadAngleDeg(
            WeaponState state,
            IProtoItemWeapon protoWeapon)
        {
            var pattern = protoWeapon.FirePatternPreset;
            if (!pattern.IsEnabled)
            {
                // no weapon fire spread
                return 0;
            }

            state.FirePatternCooldownSecondsRemains = protoWeapon.FirePatternCooldownDuration;

            var shotNumber = state.FirePatternCurrentShotNumber;
            state.FirePatternCurrentShotNumber = (ushort)(shotNumber + 1);

            var initialSequenceLength = pattern.InitialSequence.Length;
            if (shotNumber < initialSequenceLength)
            {
                // initial fire sequence
                return pattern.InitialSequence[shotNumber];
            }

            // cycled fire sequence
            var sequenceNumber = (shotNumber - initialSequenceLength) % pattern.CycledSequence.Length;
            return pattern.CycledSequence[sequenceNumber];
        }

        // in case server fired more ammo than the client we can fix this here
        [RemoteCallSettings(DeliveryMode.ReliableOrdered)]
        private void ClientRemote_FixAmmoCount(IItem itemWeapon, int extraShotsDone)
        {
            var ammoConsumptionPerShot = ((IProtoItemWeapon)itemWeapon.ProtoItem).AmmoConsumptionPerShot;
            var deltaAmmo = -extraShotsDone * ammoConsumptionPerShot;

            var weaponPrivateState = itemWeapon.GetPrivateState<WeaponPrivateState>();
            //Logger.Dev("Client correcting ammo count for weapon by server request: "
            //           + itemWeapon
            //           + ". Current ammo count: "
            //           + weaponPrivateState.AmmoCount
            //           + ". Delta ammo (correction): "
            //           + deltaAmmo);

            weaponPrivateState.AmmoCount = (ushort)MathHelper.Clamp(
                weaponPrivateState.AmmoCount + deltaAmmo,
                0,
                ushort.MaxValue);

            var state = ClientCurrentCharacterHelper.PrivateState.WeaponState;
            state.ShotsDone = (uint)MathHelper.Clamp(
                state.ShotsDone - extraShotsDone,
                0,
                ushort.MaxValue);
        }

        [RemoteCallSettings(
            DeliveryMode.ReliableSequenced,
            maxCallsPerSecond: 60,
            keyArgIndex: 0,
            groupName: RemoteCallSequenceGroupCharacterFiring)]
        private void ClientRemote_OnWeaponFinished(ICharacter whoFires)
        {
            if (whoFires == null
                || !whoFires.IsInitialized)
            {
                return;
            }

            WeaponSystemClientDisplay.OnWeaponFinished(whoFires);
        }

        [RemoteCallSettings(DeliveryMode.Unreliable, maxCallsPerSecond: 120)]
        private void ClientRemote_OnWeaponHitOrTrace(
            ICharacter firingCharacter,
            IProtoItemWeapon protoWeapon,
            IProtoItemAmmo protoAmmo,
            IProtoCharacter protoCharacter,
            Vector2Ushort fallbackCharacterPosition,
            WeaponHitData[] hitObjects,
            Vector2D endPosition,
            bool endsWithHit)
        {
            WeaponSystemClientDisplay.OnWeaponHitOrTrace(firingCharacter,
                                                         protoWeapon,
                                                         protoAmmo,
                                                         protoCharacter,
                                                         fallbackCharacterPosition,
                                                         hitObjects,
                                                         endPosition,
                                                         endsWithHit);
        }

        [RemoteCallSettings(
            DeliveryMode.ReliableSequenced,
            maxCallsPerSecond: 60,
            keyArgIndex: 0,
            groupName: RemoteCallSequenceGroupCharacterFiring)]
        private void ClientRemote_OnWeaponInputStop(ICharacter whoFires)
        {
            if (whoFires == null
                || !whoFires.IsInitialized)
            {
                return;
            }

            WeaponSystemClientDisplay.OnWeaponInputStop(whoFires);
        }

        [RemoteCallSettings(DeliveryMode.Unreliable, maxCallsPerSecond: 60)]
        private void ClientRemote_OnWeaponShot(
            ICharacter whoFires,
            IProtoItemWeapon protoWeapon,
            IProtoCharacter fallbackProtoCharacter,
            Vector2Ushort fallbackPosition)
        {
            if (whoFires != null
                && !whoFires.IsInitialized)
            {
                whoFires = null;
            }

            WeaponSystemClientDisplay.OnWeaponShot(whoFires,
                                                   protoWeapon,
                                                   fallbackProtoCharacter,
                                                   fallbackPosition);
        }

        [RemoteCallSettings(
            DeliveryMode.ReliableSequenced,
            maxCallsPerSecond: 60,
            keyArgIndex: 0,
            groupName: RemoteCallSequenceGroupCharacterFiring)]
        private void ClientRemote_OnWeaponStart(ICharacter whoFires)
        {
            if (whoFires == null
                || !whoFires.IsInitialized)
            {
                return;
            }

            WeaponSystemClientDisplay.OnWeaponStart(whoFires);
        }

        [RemoteCallSettings(DeliveryMode.ReliableSequenced, maxCallsPerSecond: 120)]
        private void ServerRemote_SetWeaponFiringMode(
            bool isFiring,
            uint clientShotsDone)
        {
            var character = ServerRemoteContext.Character;
            var weaponState = PlayerCharacter.GetPrivateState(character).WeaponState;

            //Logger.Dev(isFiring
            //               ? "SetWeaponFiringMode: firing!"
            //               : $"SetWeaponFiringMode: stop firing! Shots done: {clientShotsDone}");

            weaponState.SharedSetInputIsFiring(isFiring,
                                               clientShotsDone);
        }
    }
}