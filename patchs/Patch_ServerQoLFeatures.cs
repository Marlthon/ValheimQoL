using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimQoL
{
    internal static class Patch_ServerQoLFeatures
    {
        private const float UpdateIntervalSeconds = 1f;

        private static readonly List<Door> RegisteredDoors =
            new List<Door>();
        private static readonly Dictionary<Door, float> DoorFarSince =
            new Dictionary<Door, float>();
        private static readonly List<VehicleRemovalState> RegisteredVehicles =
            new List<VehicleRemovalState>();
        private static readonly List<TurretTargetState> RegisteredTurrets =
            new List<TurretTargetState>();
        private static readonly List<TameableFeatureState> RegisteredTames =
            new List<TameableFeatureState>();

        private static ValheimQoLPlugin? Plugin;
        private static Coroutine? UpdateCoroutine;

        private static ConfigEntry<bool> AutoCloseDoors = null!;
        private static ConfigEntry<float> AutoCloseDistance = null!;
        private static ConfigEntry<float> AutoCloseDelay = null!;

        private static ConfigEntry<bool> DeconstructCartsWithHammer = null!;
        private static ConfigEntry<bool> DeconstructShipsWithHammer = null!;

        private static ConfigEntry<bool> BallistasIgnorePlayers = null!;
        private static ConfigEntry<bool> BallistasIgnoreTamedAnimals = null!;

        private static ConfigEntry<bool> MakeAllTamesCommandable = null!;
        private static ConfigEntry<bool> TeleportFollowingTames = null!;
        private static ConfigEntry<float> TeleportFollowDistance = null!;
        private static ConfigEntry<bool> TakeFollowingTamesIntoDungeons = null!;

        private static ConfigEntry<bool> AllowBuildingInDungeons = null!;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            Plugin = plugin;

            AutoCloseDoors = plugin.config(
                "Doors",
                "AutoCloseDoors",
                true,
                "Automatically closes player-built doors after every player has moved away. Example: false leaves doors in their current state.",
                true);

            AutoCloseDistance = plugin.config(
                "Doors",
                "AutoCloseDistance",
                5f,
                new ConfigDescription(
                    "Minimum distance in meters that every player must be from an open door before its close timer starts. Example: 5 starts the timer when no player is within 5 meters.",
                    new AcceptableValueRange<float>(1f, 100f)),
                true);

            AutoCloseDelay = plugin.config(
                "Doors",
                "AutoCloseDelay",
                2f,
                new ConfigDescription(
                    "Seconds an open door waits after every player has moved away before closing. Example: 2 closes the door two seconds after the area is clear.",
                    new AcceptableValueRange<float>(0f, 60f)),
                true);

            DeconstructCartsWithHammer = plugin.config(
                "Vehicles",
                "DeconstructCartsWithHammer",
                true,
                "Allows carts to be deconstructed with the hammer. Vanilla inventory and usage safety checks still apply. Example: false restores the cart's original removal rule.",
                true);

            DeconstructShipsWithHammer = plugin.config(
                "Vehicles",
                "DeconstructShipsWithHammer",
                true,
                "Allows ships to be deconstructed with the hammer. A ship cannot be removed while a player is aboard. Example: false restores the ship's original removal rule.",
                true);

            BallistasIgnorePlayers = plugin.config(
                "Ballistas",
                "IgnorePlayers",
                true,
                "Prevents ballistas from selecting players as targets. Example: false restores the original player targeting rule.",
                true);

            BallistasIgnoreTamedAnimals = plugin.config(
                "Ballistas",
                "IgnoreTamedAnimals",
                true,
                "Prevents ballistas from selecting tamed animals as targets in both normal and configured target modes. Example: false restores the original tamed target rule.",
                true);

            MakeAllTamesCommandable = plugin.config(
                "Tames",
                "MakeAllTamesCommandable",
                true,
                "Makes every tamed creature commandable like a wolf, allowing players to toggle follow and stay with the Use key. Example: false restores each creature's original command behavior.",
                true);

            TeleportFollowingTames = plugin.config(
                "Tames",
                "TeleportFollowingTames",
                true,
                "Teleports a following tamed creature near its player when it falls too far behind in the open world. Mounted creatures are never teleported. Example: false disables only long-distance follow teleporting.",
                true);

            TeleportFollowDistance = plugin.config(
                "Tames",
                "TeleportFollowDistance",
                64f,
                new ConfigDescription(
                    "Open-world distance in meters at which a following tame is moved near its player. Example: 64 teleports the tame after it falls about one Valheim zone behind.",
                    new AcceptableValueRange<float>(10f, 500f)),
                true);

            TakeFollowingTamesIntoDungeons = plugin.config(
                "Tames",
                "TakeFollowingTamesIntoDungeons",
                true,
                "Moves tamed creatures that are actively following a player into and out of dungeons with that player. Mounted creatures are never moved. Example: false keeps following tames outside.",
                true);

            AllowBuildingInDungeons = plugin.config(
                "World",
                "AllowBuildingInDungeons",
                true,
                "Allows normal building pieces to be placed inside dungeons by applying the vanilla DungeonBuild rule. Example: false restores each piece's original dungeon restriction.",
                true);

            AutoCloseDoors.SettingChanged += OnDoorSettingChanged;
            AutoCloseDistance.SettingChanged += OnDoorSettingChanged;
            AutoCloseDelay.SettingChanged += OnDoorSettingChanged;

            DeconstructCartsWithHammer.SettingChanged +=
                OnVehicleSettingChanged;
            DeconstructShipsWithHammer.SettingChanged +=
                OnVehicleSettingChanged;

            BallistasIgnorePlayers.SettingChanged +=
                OnTurretSettingChanged;
            BallistasIgnoreTamedAnimals.SettingChanged +=
                OnTurretSettingChanged;

            MakeAllTamesCommandable.SettingChanged +=
                OnTameSettingChanged;

            UpdateCoroutine =
                plugin.StartCoroutine(UpdateRegisteredObjects());
        }

        internal static void Shutdown()
        {
            if (Plugin != null && UpdateCoroutine != null)
            {
                Plugin.StopCoroutine(UpdateCoroutine);
                UpdateCoroutine = null;
            }

            if (AutoCloseDoors != null)
            {
                AutoCloseDoors.SettingChanged -= OnDoorSettingChanged;
                AutoCloseDistance.SettingChanged -= OnDoorSettingChanged;
                AutoCloseDelay.SettingChanged -= OnDoorSettingChanged;
            }

            if (DeconstructCartsWithHammer != null)
            {
                DeconstructCartsWithHammer.SettingChanged -=
                    OnVehicleSettingChanged;
                DeconstructShipsWithHammer.SettingChanged -=
                    OnVehicleSettingChanged;
            }

            if (BallistasIgnorePlayers != null)
            {
                BallistasIgnorePlayers.SettingChanged -=
                    OnTurretSettingChanged;
                BallistasIgnoreTamedAnimals.SettingChanged -=
                    OnTurretSettingChanged;
            }

            if (MakeAllTamesCommandable != null)
            {
                MakeAllTamesCommandable.SettingChanged -=
                    OnTameSettingChanged;
            }

            RestoreVehicles();
            RestoreTurrets();
            RestoreTames();

            RegisteredDoors.Clear();
            DoorFarSince.Clear();
            Plugin = null;
        }

        private static IEnumerator UpdateRegisteredObjects()
        {
            WaitForSeconds wait =
                new WaitForSeconds(UpdateIntervalSeconds);

            while (true)
            {
                yield return wait;
                UpdateDoors();
                UpdateFollowingTames();
            }
        }

        private static void OnDoorSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            DoorFarSince.Clear();
        }

        private static void OnVehicleSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            ApplyVehicles();
        }

        private static void OnTurretSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            ApplyTurrets();
        }

        private static void OnTameSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            ApplyTames();
        }

        internal static void RegisterDoor(Door door)
        {
            if (door != null && !RegisteredDoors.Contains(door))
            {
                RegisteredDoors.Add(door);
            }
        }

        private static void UpdateDoors()
        {
            if (AutoCloseDoors == null || !AutoCloseDoors.Value)
            {
                DoorFarSince.Clear();
                RemoveDestroyedDoors();
                return;
            }

            float currentTime = Time.time;
            float closeDistance = AutoCloseDistance.Value;
            float closeDelay = AutoCloseDelay.Value;

            for (int index = RegisteredDoors.Count - 1;
                 index >= 0;
                 index--)
            {
                Door door = RegisteredDoors[index];
                if (door == null)
                {
                    RegisteredDoors.RemoveAt(index);
                    continue;
                }

                ZNetView nview =
                    door.GetComponent<ZNetView>();

                if (nview == null ||
                    !nview.IsValid() ||
                    !nview.IsOwner())
                {
                    DoorFarSince.Remove(door);
                    continue;
                }

                ZDO zdo = nview.GetZDO();
                if (zdo == null)
                {
                    DoorFarSince.Remove(door);
                    continue;
                }

                if (zdo.GetInt(ZDOVars.s_state) == 0)
                {
                    DoorFarSince.Remove(door);
                    continue;
                }

                Piece piece = door.GetComponent<Piece>();
                if (piece == null ||
                    !piece.IsPlacedByPlayer() ||
                    door.m_keyItem != null ||
                    door.m_canNotBeClosed)
                {
                    DoorFarSince.Remove(door);
                    continue;
                }

                if (Player.GetClosestPlayer(
                        door.transform.position,
                        closeDistance) != null)
                {
                    DoorFarSince.Remove(door);
                    continue;
                }

                if (!DoorFarSince.TryGetValue(
                        door,
                        out float farSince))
                {
                    DoorFarSince.Add(door, currentTime);
                    continue;
                }

                if (currentTime - farSince < closeDelay)
                {
                    continue;
                }

                zdo.Set(ZDOVars.s_state, 0, false);
                DoorFarSince.Remove(door);
            }
        }

        private static void RemoveDestroyedDoors()
        {
            for (int index = RegisteredDoors.Count - 1;
                 index >= 0;
                 index--)
            {
                if (RegisteredDoors[index] == null)
                {
                    RegisteredDoors.RemoveAt(index);
                }
            }
        }

        internal static void RegisterCart(Vagon cart)
        {
            RegisterVehicle(
                cart != null
                    ? cart.GetComponent<Piece>()
                    : null,
                false);
        }

        internal static void RegisterShip(Ship ship)
        {
            RegisterVehicle(
                ship != null
                    ? ship.GetComponent<Piece>()
                    : null,
                true);
        }

        private static void RegisterVehicle(
            Piece? piece,
            bool isShip)
        {
            if (piece == null)
            {
                return;
            }

            for (int index = 0;
                 index < RegisteredVehicles.Count;
                 index++)
            {
                if (RegisteredVehicles[index].Piece == piece)
                {
                    return;
                }
            }

            VehicleRemovalState state =
                new VehicleRemovalState(
                    piece,
                    isShip,
                    piece.m_canBeRemoved);

            RegisteredVehicles.Add(state);
            ApplyVehicle(state);
        }

        private static void ApplyVehicles()
        {
            for (int index = RegisteredVehicles.Count - 1;
                 index >= 0;
                 index--)
            {
                VehicleRemovalState state =
                    RegisteredVehicles[index];

                if (state.Piece == null)
                {
                    RegisteredVehicles.RemoveAt(index);
                    continue;
                }

                ApplyVehicle(state);
            }
        }

        private static void ApplyVehicle(
            VehicleRemovalState state)
        {
            bool enabled = state.IsShip
                ? DeconstructShipsWithHammer.Value
                : DeconstructCartsWithHammer.Value;

            state.Piece.m_canBeRemoved =
                state.OriginalCanBeRemoved ||
                enabled;
        }

        private static void RestoreVehicles()
        {
            for (int index = RegisteredVehicles.Count - 1;
                 index >= 0;
                 index--)
            {
                VehicleRemovalState state =
                    RegisteredVehicles[index];

                if (state.Piece != null)
                {
                    state.Piece.m_canBeRemoved =
                        state.OriginalCanBeRemoved;
                }
            }

            RegisteredVehicles.Clear();
        }

        internal static void RegisterTurret(Turret turret)
        {
            if (turret == null)
            {
                return;
            }

            for (int index = 0;
                 index < RegisteredTurrets.Count;
                 index++)
            {
                if (RegisteredTurrets[index].Turret == turret)
                {
                    return;
                }
            }

            TurretTargetState state =
                new TurretTargetState(
                    turret,
                    turret.m_targetPlayers,
                    turret.m_targetTamed,
                    turret.m_targetTamedConfig);

            RegisteredTurrets.Add(state);
            ApplyTurret(state);
        }

        private static void ApplyTurrets()
        {
            for (int index = RegisteredTurrets.Count - 1;
                 index >= 0;
                 index--)
            {
                TurretTargetState state =
                    RegisteredTurrets[index];

                if (state.Turret == null)
                {
                    RegisteredTurrets.RemoveAt(index);
                    continue;
                }

                ApplyTurret(state);
            }
        }

        private static void ApplyTurret(
            TurretTargetState state)
        {
            state.Turret.m_targetPlayers =
                state.OriginalTargetPlayers &&
                !BallistasIgnorePlayers.Value;

            state.Turret.m_targetTamed =
                state.OriginalTargetTamed &&
                !BallistasIgnoreTamedAnimals.Value;

            state.Turret.m_targetTamedConfig =
                state.OriginalTargetTamedConfig &&
                !BallistasIgnoreTamedAnimals.Value;
        }

        private static void RestoreTurrets()
        {
            for (int index = RegisteredTurrets.Count - 1;
                 index >= 0;
                 index--)
            {
                TurretTargetState state =
                    RegisteredTurrets[index];

                if (state.Turret == null)
                {
                    continue;
                }

                state.Turret.m_targetPlayers =
                    state.OriginalTargetPlayers;
                state.Turret.m_targetTamed =
                    state.OriginalTargetTamed;
                state.Turret.m_targetTamedConfig =
                    state.OriginalTargetTamedConfig;
            }

            RegisteredTurrets.Clear();
        }

        internal static void RegisterTame(Tameable tameable)
        {
            if (tameable == null)
            {
                return;
            }

            for (int index = 0;
                 index < RegisteredTames.Count;
                 index++)
            {
                if (RegisteredTames[index].Tameable == tameable)
                {
                    return;
                }
            }

            TameableFeatureState state =
                new TameableFeatureState(
                    tameable,
                    tameable.m_commandable);

            RegisteredTames.Add(state);
            ApplyTame(state);
        }

        private static void ApplyTames()
        {
            for (int index = RegisteredTames.Count - 1;
                 index >= 0;
                 index--)
            {
                TameableFeatureState state =
                    RegisteredTames[index];

                if (state.Tameable == null)
                {
                    RegisteredTames.RemoveAt(index);
                    continue;
                }

                ApplyTame(state);
            }
        }

        private static void ApplyTame(
            TameableFeatureState state)
        {
            state.Tameable.m_commandable =
                state.OriginalCommandable ||
                MakeAllTamesCommandable.Value;
        }

        private static void RestoreTames()
        {
            for (int index = RegisteredTames.Count - 1;
                 index >= 0;
                 index--)
            {
                TameableFeatureState state =
                    RegisteredTames[index];

                if (state.Tameable != null)
                {
                    state.Tameable.m_commandable =
                        state.OriginalCommandable;
                }
            }

            RegisteredTames.Clear();
        }

        private static void UpdateFollowingTames()
        {
            if (TeleportFollowingTames == null ||
                !TeleportFollowingTames.Value)
            {
                RemoveDestroyedTames();
                return;
            }

            float minimumDistance =
                TeleportFollowDistance.Value;
            float minimumDistanceSquared =
                minimumDistance * minimumDistance;

            for (int index = RegisteredTames.Count - 1;
                 index >= 0;
                 index--)
            {
                Tameable tameable =
                    RegisteredTames[index].Tameable;

                if (tameable == null)
                {
                    RegisteredTames.RemoveAt(index);
                    continue;
                }

                if (!TryGetFollowingPlayer(
                        tameable,
                        out Player player,
                        out ZNetView nview))
                {
                    continue;
                }

                if (tameable.HaveRider() ||
                    Character.InInterior(
                        tameable.transform.position) ||
                    Character.InInterior(
                        player.transform.position))
                {
                    continue;
                }

                Vector3 difference =
                    tameable.transform.position -
                    player.transform.position;
                difference.y = 0f;

                if (difference.sqrMagnitude <
                    minimumDistanceSquared)
                {
                    continue;
                }

                Vector3 targetPosition =
                    GetPositionNearPlayer(
                        player.transform.position,
                        player.transform.rotation,
                        false);

                TeleportTame(
                    tameable,
                    nview,
                    targetPosition,
                    player.transform.rotation);
            }
        }

        private static void RemoveDestroyedTames()
        {
            for (int index = RegisteredTames.Count - 1;
                 index >= 0;
                 index--)
            {
                if (RegisteredTames[index].Tameable == null)
                {
                    RegisteredTames.RemoveAt(index);
                }
            }
        }

        private static bool TryGetFollowingPlayer(
            Tameable tameable,
            out Player player,
            out ZNetView nview)
        {
            player = null!;
            nview = tameable.GetComponent<ZNetView>();

            if (!tameable.IsTamed() ||
                nview == null ||
                !nview.IsValid() ||
                !nview.IsOwner())
            {
                return false;
            }

            MonsterAI monsterAI =
                tameable.GetComponent<MonsterAI>();

            if (monsterAI == null)
            {
                return false;
            }

            GameObject followTarget =
                monsterAI.GetFollowTarget();
            if (followTarget == null)
            {
                return false;
            }

            player = followTarget.GetComponent<Player>();
            return player != null;
        }

        internal static void TakeFollowingTamesThroughTeleport(
            Player player,
            Vector3 destination,
            Quaternion destinationRotation)
        {
            if (TakeFollowingTamesIntoDungeons == null ||
                !TakeFollowingTamesIntoDungeons.Value ||
                player == null ||
                Character.InInterior(player.transform.position) ==
                Character.InInterior(destination))
            {
                return;
            }

            bool playerWasInside =
                Character.InInterior(player.transform.position);

            for (int index = RegisteredTames.Count - 1;
                 index >= 0;
                 index--)
            {
                Tameable tameable =
                    RegisteredTames[index].Tameable;

                if (tameable == null)
                {
                    RegisteredTames.RemoveAt(index);
                    continue;
                }

                if (!tameable.IsTamed() ||
                    tameable.HaveRider() ||
                    Character.InInterior(
                        tameable.transform.position) !=
                    playerWasInside)
                {
                    continue;
                }

                MonsterAI monsterAI =
                    tameable.GetComponent<MonsterAI>();
                ZNetView nview =
                    tameable.GetComponent<ZNetView>();

                if (monsterAI == null ||
                    nview == null ||
                    !nview.IsValid())
                {
                    continue;
                }

                GameObject followTarget =
                    monsterAI.GetFollowTarget();

                if (followTarget != player.gameObject &&
                    nview.GetZDO().GetString(
                        ZDOVars.s_follow,
                        string.Empty) !=
                    player.GetPlayerName())
                {
                    continue;
                }

                if (!nview.IsOwner())
                {
                    nview.ClaimOwnership();
                }

                Vector3 targetPosition =
                    GetPositionNearPlayer(
                        destination,
                        destinationRotation,
                        Character.InInterior(destination));

                TeleportTame(
                    tameable,
                    nview,
                    targetPosition,
                    destinationRotation);
            }
        }

        private static Vector3 GetPositionNearPlayer(
            Vector3 playerPosition,
            Quaternion playerRotation,
            bool interiorDestination)
        {
            Vector3 backward =
                playerRotation * Vector3.back;
            Quaternion spread =
                Quaternion.Euler(
                    0f,
                    UnityEngine.Random.Range(-45f, 45f),
                    0f);

            Vector3 targetPosition =
                playerPosition +
                spread * backward *
                UnityEngine.Random.Range(2f, 4f);

            if (interiorDestination)
            {
                targetPosition.y +=
                    UnityEngine.Random.Range(0.1f, 0.8f);
                return targetPosition;
            }

            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.FindFloor(
                    targetPosition,
                    out float floorHeight))
            {
                targetPosition.y = floorHeight + 0.2f;
            }
            else
            {
                targetPosition.y = playerPosition.y + 0.2f;
            }

            return targetPosition;
        }

        private static void TeleportTame(
            Tameable tameable,
            ZNetView nview,
            Vector3 targetPosition,
            Quaternion targetRotation)
        {
            if (tameable == null ||
                nview == null ||
                !nview.IsValid() ||
                !nview.IsOwner())
            {
                return;
            }

            Rigidbody body =
                tameable.GetComponent<Rigidbody>();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = targetPosition;
                body.rotation = targetRotation;
            }

            tameable.transform.SetPositionAndRotation(
                targetPosition,
                targetRotation);

            ZDO zdo = nview.GetZDO();
            zdo.SetPosition(targetPosition);
            zdo.SetRotation(targetRotation);

            ZSyncTransform syncTransform =
                tameable.GetComponent<ZSyncTransform>();
            if (syncTransform != null)
            {
                syncTransform.SyncNow();
            }

            Physics.SyncTransforms();
        }

        internal static bool ShouldAllowBuildingInDungeons()
        {
            return AllowBuildingInDungeons != null &&
                   AllowBuildingInDungeons.Value;
        }

        private sealed class VehicleRemovalState
        {
            internal VehicleRemovalState(
                Piece piece,
                bool isShip,
                bool originalCanBeRemoved)
            {
                Piece = piece;
                IsShip = isShip;
                OriginalCanBeRemoved = originalCanBeRemoved;
            }

            internal Piece Piece { get; }
            internal bool IsShip { get; }
            internal bool OriginalCanBeRemoved { get; }
        }

        private sealed class TurretTargetState
        {
            internal TurretTargetState(
                Turret turret,
                bool originalTargetPlayers,
                bool originalTargetTamed,
                bool originalTargetTamedConfig)
            {
                Turret = turret;
                OriginalTargetPlayers = originalTargetPlayers;
                OriginalTargetTamed = originalTargetTamed;
                OriginalTargetTamedConfig =
                    originalTargetTamedConfig;
            }

            internal Turret Turret { get; }
            internal bool OriginalTargetPlayers { get; }
            internal bool OriginalTargetTamed { get; }
            internal bool OriginalTargetTamedConfig { get; }
        }

        private sealed class TameableFeatureState
        {
            internal TameableFeatureState(
                Tameable tameable,
                bool originalCommandable)
            {
                Tameable = tameable;
                OriginalCommandable = originalCommandable;
            }

            internal Tameable Tameable { get; }
            internal bool OriginalCommandable { get; }
        }
    }

    [HarmonyPatch(typeof(Door), "Awake")]
    internal static class ServerQoL_Door_Awake_Patch
    {
        private static void Postfix(Door __instance)
        {
            Patch_ServerQoLFeatures.RegisterDoor(__instance);
        }
    }

    [HarmonyPatch(typeof(Vagon), "Awake")]
    internal static class ServerQoL_Vagon_Awake_Patch
    {
        private static void Postfix(Vagon __instance)
        {
            Patch_ServerQoLFeatures.RegisterCart(__instance);
        }
    }

    [HarmonyPatch(typeof(Ship), "Awake")]
    internal static class ServerQoL_Ship_Awake_Patch
    {
        private static void Postfix(Ship __instance)
        {
            Patch_ServerQoLFeatures.RegisterShip(__instance);
        }
    }

    [HarmonyPatch(typeof(Turret), "Awake")]
    internal static class ServerQoL_Turret_Awake_Patch
    {
        private static void Postfix(Turret __instance)
        {
            Patch_ServerQoLFeatures.RegisterTurret(__instance);
        }
    }

    [HarmonyPatch(typeof(Tameable), "Awake")]
    internal static class ServerQoL_Tameable_Awake_Patch
    {
        private static void Postfix(Tameable __instance)
        {
            Patch_ServerQoLFeatures.RegisterTame(__instance);
        }
    }

    [HarmonyPatch(
        typeof(Player),
        nameof(Player.TeleportTo))]
    internal static class ServerQoL_Player_TeleportTo_Patch
    {
        private static void Postfix(
            Player __instance,
            Vector3 pos,
            Quaternion rot,
            bool __result)
        {
            if (!__result)
            {
                return;
            }

            Patch_ServerQoLFeatures
                .TakeFollowingTamesThroughTeleport(
                    __instance,
                    pos,
                    rot);
        }
    }

    [HarmonyPatch(
        typeof(ZoneSystem),
        nameof(ZoneSystem.GetGlobalKey),
        new Type[] { typeof(GlobalKeys) })]
    internal static class ServerQoL_ZoneSystem_GetGlobalKey_Patch
    {
        private static bool Prefix(
            GlobalKeys key,
            ref bool __result)
        {
            if (key != GlobalKeys.DungeonBuild ||
                !Patch_ServerQoLFeatures
                    .ShouldAllowBuildingInDungeons())
            {
                return true;
            }

            __result = true;
            return false;
        }
    }
}
