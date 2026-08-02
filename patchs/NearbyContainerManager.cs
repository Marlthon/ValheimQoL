using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimQoL
{
    internal static class NearbyContainerManager
    {
        private static readonly HashSet<Container> RegisteredContainers =
            new HashSet<Container>();

        private static readonly List<Container> NearbyContainers =
            new List<Container>(64);

        private static ConfigEntry<bool> Enabled = null!;
        private static ConfigEntry<float> ContainerRange = null!;
        private static ConfigEntry<bool> CraftingEnabled = null!;
        private static ConfigEntry<bool> BuildingEnabled = null!;
        private static ConfigEntry<bool> PlantingEnabled = null!;
        private static ConfigEntry<bool> LeaveOneItem = null!;
        private static ConfigEntry<bool> RespectPrivateAreas = null!;

        private static int _cachedFrame = -1;
        private static Vector3 _cachedPosition;
        private static float _cachedRange;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            Enabled = plugin.config(
                "CraftFromContainers",
                "Enabled",
                true,
                "Enables the server-controlled use of resources stored in nearby containers. Example: true allows the features below; false restores vanilla inventory-only behavior.");

            ContainerRange = plugin.config(
                "CraftFromContainers",
                "ContainerRange",
                20f,
                new ConfigDescription(
                    "Sets the maximum distance in meters used to search for containers. Example: 20 searches player-built containers up to 20 meters away.",
                    new AcceptableValueRange<float>(1f, 100f)));

            CraftingEnabled = plugin.config(
                "CraftFromContainers",
                "CraftingEnabled",
                true,
                "Allows crafting recipes to use resources from nearby containers. Example: Wood stored in a nearby chest can be used at a workbench.");

            BuildingEnabled = plugin.config(
                "CraftFromContainers",
                "BuildingEnabled",
                true,
                "Allows normal building pieces to use resources from nearby containers. Example: a wall can use Wood from an accessible chest.");

            PlantingEnabled = plugin.config(
                "CraftFromContainers",
                "PlantingEnabled",
                true,
                "Allows cultivator planting, including complete planting grids, to use seeds from nearby containers. Example: a 2x2 carrot grid can consume four Carrot Seeds from a chest.");

            LeaveOneItem = plugin.config(
                "CraftFromContainers",
                "LeaveOneItem",
                false,
                "Leaves one matching item in each container when resources are pulled. Example: a chest containing 10 Wood provides at most 9 Wood when this is true.");

            RespectPrivateAreas = plugin.config(
                "CraftFromContainers",
                "RespectPrivateAreas",
                true,
                "Prevents pulling from private containers and containers protected against the current player. Example: resources cannot be taken through another player's ward.");
        }

        internal static bool IsCraftingEnabled()
        {
            return Enabled != null &&
                   Enabled.Value &&
                   CraftingEnabled != null &&
                   CraftingEnabled.Value;
        }

        internal static bool IsEnabledForPiece(Piece? piece)
        {
            if (Enabled == null || !Enabled.Value || piece == null)
            {
                return false;
            }

            bool isPlant = piece.GetComponent<Plant>() != null;
            return isPlant
                ? PlantingEnabled != null && PlantingEnabled.Value
                : BuildingEnabled != null && BuildingEnabled.Value;
        }

        internal static int CountAvailableForCrafting(
            Player player,
            string sharedName,
            int quality = -1)
        {
            if (player == null)
            {
                return 0;
            }

            int playerAmount =
                player.GetInventory().CountItems(sharedName, quality);

            return IsCraftingEnabled()
                ? playerAmount +
                  CountInNearbyContainers(player, sharedName, quality)
                : playerAmount;
        }

        internal static int CountAvailableForPiece(
            Player player,
            Piece? piece,
            string sharedName,
            int quality = -1)
        {
            if (player == null)
            {
                return 0;
            }

            int playerAmount =
                player.GetInventory().CountItems(sharedName, quality);

            return IsEnabledForPiece(piece)
                ? playerAmount +
                  CountInNearbyContainers(player, sharedName, quality)
                : playerAmount;
        }

        internal static int ConsumeForCrafting(
            Player player,
            string sharedName,
            int amount,
            int quality)
        {
            return Consume(
                player,
                null,
                true,
                sharedName,
                amount,
                quality);
        }

        internal static int ConsumeForPiece(
            Player player,
            Piece? piece,
            string sharedName,
            int amount,
            int quality)
        {
            return Consume(
                player,
                piece,
                false,
                sharedName,
                amount,
                quality);
        }

        internal static ItemDrop.ItemData? FindCraftingItem(
            Player player,
            string sharedName,
            int quality)
        {
            if (player == null || !IsCraftingEnabled())
            {
                return null;
            }

            ItemDrop.ItemData? playerItem =
                player.GetInventory().GetItem(sharedName, quality);

            if (playerItem != null)
            {
                return playerItem;
            }

            List<Container> containers = GetNearbyContainers(player);
            for (int index = 0; index < containers.Count; index++)
            {
                Inventory? inventory = containers[index].GetInventory();
                ItemDrop.ItemData? item =
                    inventory?.GetItem(sharedName, quality);

                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static int Consume(
            Player player,
            Piece? piece,
            bool crafting,
            string sharedName,
            int amount,
            int quality)
        {
            if (player == null || amount <= 0)
            {
                return 0;
            }

            bool canUseContainers = crafting
                ? IsCraftingEnabled()
                : IsEnabledForPiece(piece);

            Inventory playerInventory = player.GetInventory();
            int fromPlayer = Mathf.Min(
                amount,
                playerInventory.CountItems(sharedName, quality));

            if (fromPlayer > 0)
            {
                playerInventory.RemoveItem(
                    sharedName,
                    fromPlayer,
                    quality);
            }

            int consumed = fromPlayer;
            int remaining = amount - fromPlayer;

            if (!canUseContainers || remaining <= 0)
            {
                return consumed;
            }

            List<Container> containers = GetNearbyContainers(player);
            for (int index = 0;
                 index < containers.Count && remaining > 0;
                 index++)
            {
                Container container = containers[index];
                Inventory? inventory = container.GetInventory();
                if (inventory == null)
                {
                    continue;
                }

                int stored = inventory.CountItems(sharedName, quality);
                int removable = GetRemovableAmount(stored);
                if (removable <= 0)
                {
                    continue;
                }

                if (!container.m_nview.IsOwner())
                {
                    if (container.IsInUse())
                    {
                        continue;
                    }

                    container.m_nview.ClaimOwnership();
                }

                if (!container.m_nview.IsOwner())
                {
                    continue;
                }

                stored = inventory.CountItems(sharedName, quality);
                removable = GetRemovableAmount(stored);

                int take = Mathf.Min(remaining, removable);
                if (take <= 0)
                {
                    continue;
                }

                inventory.RemoveItem(sharedName, take, quality);
                consumed += take;
                remaining -= take;
            }

            return consumed;
        }

        private static int CountInNearbyContainers(
            Player player,
            string sharedName,
            int quality)
        {
            int total = 0;
            List<Container> containers = GetNearbyContainers(player);

            for (int index = 0; index < containers.Count; index++)
            {
                Inventory? inventory = containers[index].GetInventory();
                if (inventory == null)
                {
                    continue;
                }

                total += GetRemovableAmount(
                    inventory.CountItems(sharedName, quality));
            }

            return total;
        }

        private static int GetRemovableAmount(int stored)
        {
            if (stored <= 0)
            {
                return 0;
            }

            return LeaveOneItem != null && LeaveOneItem.Value
                ? Mathf.Max(0, stored - 1)
                : stored;
        }

        private static List<Container> GetNearbyContainers(Player player)
        {
            float range = ContainerRange == null
                ? 20f
                : Mathf.Clamp(ContainerRange.Value, 1f, 100f);

            Vector3 position = player.transform.position;
            if (_cachedFrame == Time.frameCount &&
                _cachedRange == range &&
                (_cachedPosition - position).sqrMagnitude < 0.01f)
            {
                return NearbyContainers;
            }

            _cachedFrame = Time.frameCount;
            _cachedPosition = position;
            _cachedRange = range;
            NearbyContainers.Clear();

            float squaredRange = range * range;
            foreach (Container container in RegisteredContainers)
            {
                if (!CanUseContainer(container, player))
                {
                    continue;
                }

                if ((container.transform.position - position).sqrMagnitude <=
                    squaredRange)
                {
                    NearbyContainers.Add(container);
                }
            }

            return NearbyContainers;
        }

        private static bool CanUseContainer(
            Container? container,
            Player player)
        {
            if (container == null ||
                container.m_nview == null ||
                !container.m_nview.IsValid() ||
                container.GetInventory() == null ||
                container.m_piece == null ||
                container.m_piece.GetCreator() == 0L ||
                container.m_wagon != null ||
                container.GetComponentInParent<Player>() != null)
            {
                return false;
            }

            if (container.IsInUse() && !container.IsOwner())
            {
                return false;
            }

            if (RespectPrivateAreas == null ||
                !RespectPrivateAreas.Value)
            {
                return true;
            }

            if (!PrivateArea.CheckAccess(
                    container.transform.position,
                    0f,
                    false))
            {
                return false;
            }

            switch (container.m_privacy)
            {
                case Container.PrivacySetting.Private:
                    return container.m_piece.GetCreator() ==
                           player.GetPlayerID();

                case Container.PrivacySetting.Group:
                    return false;

                case Container.PrivacySetting.Public:
                    return true;

                default:
                    return false;
            }
        }

        internal static void Register(Container? container)
        {
            if (container != null)
            {
                RegisteredContainers.Add(container);
                _cachedFrame = -1;
            }
        }

        internal static void Unregister(Container? container)
        {
            if (container != null)
            {
                RegisteredContainers.Remove(container);
                _cachedFrame = -1;
            }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    internal static class CraftContainers_Container_Awake_Patch
    {
        private static void Postfix(Container __instance)
        {
            NearbyContainerManager.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
    internal static class CraftContainers_Container_OnDestroyed_Patch
    {
        private static void Postfix(Container __instance)
        {
            NearbyContainerManager.Unregister(__instance);
        }
    }
}
