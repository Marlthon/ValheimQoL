using HarmonyLib;
using System;
using TMPro;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class Patch_CraftFromContainers
    {
        private sealed class PendingSingleIngredient
        {
            internal Inventory Inventory = null!;
            internal string SharedName = string.Empty;
            internal int Amount;
            internal int Quality;
        }

        private static PendingSingleIngredient? _pendingSingleIngredient;

        private static Piece? GetMatchingSelectedPiece(
            Player player,
            Piece.Requirement[] requirements)
        {
            if (player == null || player.m_buildPieces == null)
            {
                return null;
            }

            Piece? selectedPiece = player.m_buildPieces.GetSelectedPiece();
            return selectedPiece != null &&
                   ReferenceEquals(selectedPiece.m_resources, requirements)
                ? selectedPiece
                : null;
        }

        private static bool HasCraftingRequirements(
            Player player,
            Recipe recipe,
            int qualityLevel,
            int multiplier)
        {
            if (recipe.m_requireOnlyOneIngredient)
            {
                for (int resourceIndex = 0;
                     resourceIndex < recipe.m_resources.Length;
                     resourceIndex++)
                {
                    Piece.Requirement requirement =
                        recipe.m_resources[resourceIndex];

                    if (requirement.m_resItem == null)
                    {
                        continue;
                    }

                    int required =
                        requirement.GetAmount(qualityLevel) * multiplier;

                    if (required <= 0)
                    {
                        continue;
                    }

                    string sharedName =
                        requirement.m_resItem.m_itemData.m_shared.m_name;

                    int maximumQuality =
                        requirement.m_resItem.m_itemData.m_shared.m_maxQuality;

                    for (int quality = 1;
                         quality <= maximumQuality;
                         quality++)
                    {
                        if (NearbyContainerManager.CountAvailableForCrafting(
                                player,
                                sharedName,
                                quality) >= required)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            for (int resourceIndex = 0;
                 resourceIndex < recipe.m_resources.Length;
                 resourceIndex++)
            {
                Piece.Requirement requirement =
                    recipe.m_resources[resourceIndex];

                if (requirement.m_resItem == null)
                {
                    continue;
                }

                int required =
                    requirement.GetAmount(qualityLevel) * multiplier;

                if (required <= 0)
                {
                    continue;
                }

                string sharedName =
                    requirement.m_resItem.m_itemData.m_shared.m_name;

                if (NearbyContainerManager.CountAvailableForCrafting(
                        player,
                        sharedName) < required)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasPieceRequirements(
            Player player,
            Piece piece,
            Player.RequirementMode mode)
        {
            if (piece.m_craftingStation != null)
            {
                if (mode == Player.RequirementMode.IsKnown ||
                    mode == Player.RequirementMode.CanAlmostBuild)
                {
                    if (!player.m_knownStations.ContainsKey(
                            piece.m_craftingStation.m_name))
                    {
                        return false;
                    }
                }
                else if (CraftingStation.HaveBuildStationInRange(
                             piece.m_craftingStation.m_name,
                             player.transform.position) == null &&
                         !ZoneSystem.instance.GetGlobalKey(
                             GlobalKeys.NoWorkbench))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(piece.m_dlc) &&
                !DLCMan.instance.IsDLCInstalled(piece.m_dlc))
            {
                return false;
            }

            if (mode == Player.RequirementMode.IsKnown)
            {
                return false;
            }

            if (ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
            {
                return true;
            }

            for (int index = 0;
                 index < piece.m_resources.Length;
                 index++)
            {
                Piece.Requirement requirement = piece.m_resources[index];
                if (requirement.m_resItem == null ||
                    requirement.m_amount <= 0)
                {
                    continue;
                }

                string sharedName =
                    requirement.m_resItem.m_itemData.m_shared.m_name;

                int needed = mode == Player.RequirementMode.CanAlmostBuild
                    ? 1
                    : requirement.m_amount;

                if (NearbyContainerManager.CountAvailableForPiece(
                        player,
                        piece,
                        sharedName) < needed)
                {
                    return false;
                }
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        private static void Player_HaveRequirementItems_Postfix(
            Player __instance,
            Recipe piece,
            bool discover,
            int qualityLevel,
            int amount,
            ref bool __result)
        {
            if (__result ||
                discover ||
                __instance != Player.m_localPlayer ||
                !NearbyContainerManager.IsCraftingEnabled())
            {
                return;
            }

            __result = HasCraftingRequirements(
                __instance,
                piece,
                qualityLevel,
                amount);
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(Player),
            nameof(Player.HaveRequirements),
            new Type[] { typeof(Piece), typeof(Player.RequirementMode) })]
        private static void Player_HaveRequirements_Piece_Postfix(
            Player __instance,
            Piece piece,
            Player.RequirementMode mode,
            ref bool __result)
        {
            if (__result ||
                __instance != Player.m_localPlayer ||
                !NearbyContainerManager.IsEnabledForPiece(piece))
            {
                return;
            }

            __result = HasPieceRequirements(__instance, piece, mode);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static bool Player_ConsumeResources_Prefix(
            Player __instance,
            Piece.Requirement[] requirements,
            int qualityLevel,
            int itemQuality,
            int multiplier)
        {
            if (__instance != Player.m_localPlayer)
            {
                return true;
            }

            Piece? selectedPiece =
                GetMatchingSelectedPiece(__instance, requirements);

            bool crafting = selectedPiece == null;
            bool enabled = crafting
                ? NearbyContainerManager.IsCraftingEnabled()
                : NearbyContainerManager.IsEnabledForPiece(selectedPiece);

            if (!enabled)
            {
                return true;
            }

            for (int index = 0; index < requirements.Length; index++)
            {
                Piece.Requirement requirement = requirements[index];
                if (requirement.m_resItem == null)
                {
                    continue;
                }

                int amount =
                    requirement.GetAmount(qualityLevel) * multiplier;

                if (amount <= 0)
                {
                    continue;
                }

                string sharedName =
                    requirement.m_resItem.m_itemData.m_shared.m_name;

                if (crafting)
                {
                    NearbyContainerManager.ConsumeForCrafting(
                        __instance,
                        sharedName,
                        amount,
                        itemQuality);
                }
                else
                {
                    NearbyContainerManager.ConsumeForPiece(
                        __instance,
                        selectedPiece,
                        sharedName,
                        amount,
                        itemQuality);
                }
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
        private static void Player_GetFirstRequiredItem_Postfix(
            Player __instance,
            Inventory inventory,
            Recipe recipe,
            int qualityLevel,
            ref int amount,
            ref int extraAmount,
            int craftMultiplier,
            ref ItemDrop.ItemData? __result)
        {
            if (__result != null ||
                __instance != Player.m_localPlayer ||
                !recipe.m_requireOnlyOneIngredient ||
                !NearbyContainerManager.IsCraftingEnabled())
            {
                return;
            }

            for (int resourceIndex = 0;
                 resourceIndex < recipe.m_resources.Length;
                 resourceIndex++)
            {
                Piece.Requirement requirement =
                    recipe.m_resources[resourceIndex];

                if (requirement.m_resItem == null)
                {
                    continue;
                }

                int required =
                    requirement.GetAmount(qualityLevel) * craftMultiplier;

                if (required <= 0)
                {
                    continue;
                }

                string sharedName =
                    requirement.m_resItem.m_itemData.m_shared.m_name;

                int maximumQuality =
                    requirement.m_resItem.m_itemData.m_shared.m_maxQuality;

                for (int quality = 1;
                     quality <= maximumQuality;
                     quality++)
                {
                    if (NearbyContainerManager.CountAvailableForCrafting(
                            __instance,
                            sharedName,
                            quality) < required)
                    {
                        continue;
                    }

                    ItemDrop.ItemData? item =
                        NearbyContainerManager.FindCraftingItem(
                            __instance,
                            sharedName,
                            quality);

                    if (item == null)
                    {
                        continue;
                    }

                    __result = item;
                    amount = required;
                    extraAmount =
                        requirement.m_extraAmountOnlyOneIngredient;

                    _pendingSingleIngredient =
                        new PendingSingleIngredient
                        {
                            Inventory = inventory,
                            SharedName = sharedName,
                            Amount = required,
                            Quality = quality
                        };

                    return;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(Inventory),
            nameof(Inventory.RemoveItem),
            new Type[]
            {
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(bool)
            })]
        private static bool Inventory_RemoveItem_Prefix(
            Inventory __instance,
            string name,
            int amount,
            int itemQuality)
        {
            PendingSingleIngredient? pending =
                _pendingSingleIngredient;

            if (pending == null ||
                !ReferenceEquals(__instance, pending.Inventory) ||
                pending.SharedName != name ||
                pending.Amount != amount ||
                pending.Quality != itemQuality ||
                Player.m_localPlayer == null)
            {
                return true;
            }

            _pendingSingleIngredient = null;

            NearbyContainerManager.ConsumeForCrafting(
                Player.m_localPlayer,
                name,
                amount,
                itemQuality);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void InventoryGui_DoCrafting_Prefix()
        {
            _pendingSingleIngredient = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void InventoryGui_DoCrafting_Postfix()
        {
            _pendingSingleIngredient = null;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(
            typeof(InventoryGui),
            nameof(InventoryGui.SetupRequirement))]
        private static void InventoryGui_SetupRequirement_Postfix(
            Transform elementRoot,
            Piece.Requirement req,
            Player player,
            bool craft,
            int quality,
            int craftMultiplier,
            ref bool __result)
        {
            if (!__result ||
                player == null ||
                req.m_resItem == null)
            {
                return;
            }

            Piece? selectedPiece = null;
            int resourceMultiplier = craftMultiplier;

            if (craft)
            {
                if (!NearbyContainerManager.IsCraftingEnabled())
                {
                    return;
                }
            }
            else
            {
                if (player.m_buildPieces == null)
                {
                    return;
                }

                selectedPiece = player.m_buildPieces.GetSelectedPiece();
                if (!NearbyContainerManager.IsEnabledForPiece(
                        selectedPiece))
                {
                    return;
                }

                if (selectedPiece != null &&
                    selectedPiece.GetComponent<Plant>() != null)
                {
                    resourceMultiplier =
                        Patch_PlantGrid.GetResourceMultiplier(
                            player,
                            selectedPiece);
                }
            }

            int required =
                req.GetAmount(quality) * resourceMultiplier;

            if (required <= 0)
            {
                return;
            }

            string sharedName =
                req.m_resItem.m_itemData.m_shared.m_name;

            int available = craft
                ? NearbyContainerManager.CountAvailableForCrafting(
                    player,
                    sharedName)
                : NearbyContainerManager.CountAvailableForPiece(
                    player,
                    selectedPiece,
                    sharedName);

            TMP_Text? amountText =
                elementRoot.transform.Find("res_amount")?
                    .GetComponent<TMP_Text>();

            if (amountText == null)
            {
                return;
            }

            amountText.text = available + "/" + required;

            bool freeCost =
                ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGlobalKey(
                    craft
                        ? GlobalKeys.NoCraftCost
                        : GlobalKeys.NoBuildCost);

            amountText.color = available < required && !freeCost
                ? (Mathf.Sin(Time.time * 10f) > 0f
                    ? Color.red
                    : Color.white)
                : Color.white;
        }
    }
}
