using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace ValheimQoL
{
    internal static class Patch_Smelter
    {
        private const string BulkAddOreRpc =
            "ValheimQoL_BulkAddOre";
        private const string BulkAddFuelRpc =
            "ValheimQoL_BulkAddFuel";

        private static ConfigEntry<bool> BulkFeedEnabled = null!;
        private static ConfigEntry<bool> WindmillIgnoreWind = null!;

        [ThreadStatic]
        private static int WindmillProductionQueryDepth;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            BulkFeedEnabled = plugin.config(
                "Smelters",
                "BulkFeedEnabled",
                true,
                "Allows Shift + Use to fill a Smelter-based machine with as many matching items as possible in one action. This includes furnaces, blast furnaces, charcoal kilns, windmills, spinning wheels and Eitr refineries. The machine capacity and the player's available items are always respected. Example: false restores normal one-item feeding.");

            WindmillIgnoreWind = plugin.config(
                "Smelters",
                "WindmillIgnoreWind",
                true,
                "Makes Windmill production run at full power without depending on wind strength or wind cover. Blade movement and sound still follow the real vanilla wind power. Example: false restores wind-dependent production.");
        }

        internal static bool IsBulkFeedRequested()
        {
            return BulkFeedEnabled != null &&
                   BulkFeedEnabled.Value &&
                   (ZInput.GetKey(KeyCode.LeftShift) ||
                    ZInput.GetKey(KeyCode.RightShift));
        }

        internal static bool IsBulkFeedEnabled()
        {
            return BulkFeedEnabled != null &&
                   BulkFeedEnabled.Value;
        }

        internal static bool ShouldWindmillIgnoreWind()
        {
            return WindmillIgnoreWind != null &&
                   WindmillIgnoreWind.Value;
        }

        internal static bool BeginWindmillProductionQuery(
            Smelter smelter)
        {
            if (!ShouldWindmillIgnoreWind() ||
                smelter == null ||
                smelter.m_windmill == null)
            {
                return false;
            }

            WindmillProductionQueryDepth++;
            return true;
        }

        internal static void EndWindmillProductionQuery(
            bool queryStarted)
        {
            if (!queryStarted)
            {
                return;
            }

            WindmillProductionQueryDepth = Math.Max(
                0,
                WindmillProductionQueryDepth - 1);
        }

        internal static bool IsWindmillProductionQuery()
        {
            return WindmillProductionQueryDepth > 0;
        }

        internal static void Register(Smelter smelter)
        {
            ZNetView nview = GetNView(smelter);
            if (nview == null || !nview.IsValid())
            {
                return;
            }

            nview.Register<string, int>(
                BulkAddOreRpc,
                delegate(long sender, string itemName, int amount)
                {
                    ReceiveBulkOre(
                        smelter,
                        itemName,
                        amount);
                });

            nview.Register<int>(
                BulkAddFuelRpc,
                delegate(long sender, int amount)
                {
                    ReceiveBulkFuel(
                        smelter,
                        amount);
                });
        }

        internal static bool TryBulkAddOre(
            Smelter smelter,
            Humanoid user,
            ItemDrop.ItemData? requestedItem)
        {
            if (user == null)
            {
                return false;
            }

            ZNetView nview = GetNView(smelter);
            Inventory inventory = user.GetInventory();

            if (nview == null ||
                !nview.IsValid() ||
                inventory == null)
            {
                return false;
            }

            if (!TryFindOre(
                    smelter,
                    inventory,
                    requestedItem,
                    out ItemDrop.ItemData selectedItem,
                    out string prefabName))
            {
                user.Message(
                    MessageHud.MessageType.Center,
                    requestedItem == null
                        ? "$msg_noprocessableitems"
                        : "$msg_wontwork");
                return false;
            }

            ZDO zdo = nview.GetZDO();
            int queueSize =
                zdo.GetInt(ZDOVars.s_queued);
            int freeSlots =
                smelter.m_maxOre - queueSize;

            if (freeSlots <= 0)
            {
                user.Message(
                    MessageHud.MessageType.Center,
                    "$msg_itsfull");
                return false;
            }

            int availableItems =
                inventory.CountItems(
                    selectedItem.m_shared.m_name);
            int amount =
                Math.Min(freeSlots, availableItems);

            if (amount <= 0)
            {
                user.Message(
                    MessageHud.MessageType.Center,
                    "$msg_noprocessableitems");
                return false;
            }

            inventory.RemoveItem(
                selectedItem.m_shared.m_name,
                amount);

            user.Message(
                MessageHud.MessageType.Center,
                "$msg_added " +
                selectedItem.m_shared.m_name +
                " x" +
                amount);

            nview.InvokeRPC(
                BulkAddOreRpc,
                prefabName,
                amount);

            return true;
        }

        internal static bool TryBulkAddFuel(
            Smelter smelter,
            Humanoid user,
            ItemDrop.ItemData? requestedItem)
        {
            if (user == null)
            {
                return false;
            }

            ZNetView nview = GetNView(smelter);
            Inventory inventory = user.GetInventory();

            if (nview == null ||
                !nview.IsValid() ||
                inventory == null ||
                smelter.m_fuelItem == null)
            {
                return false;
            }

            string fuelName =
                smelter.m_fuelItem
                    .m_itemData
                    .m_shared
                    .m_name;

            if (requestedItem != null &&
                requestedItem.m_shared.m_name != fuelName)
            {
                user.Message(
                    MessageHud.MessageType.Center,
                    "$msg_wrongitem");
                return false;
            }

            float currentFuel =
                nview.GetZDO()
                    .GetFloat(ZDOVars.s_fuel);
            int freeSlots =
                Mathf.FloorToInt(
                    smelter.m_maxFuel -
                    currentFuel);

            if (freeSlots <= 0)
            {
                user.Message(
                    MessageHud.MessageType.Center,
                    "$msg_itsfull");
                return false;
            }

            int availableItems =
                inventory.CountItems(fuelName);
            int amount =
                Math.Min(freeSlots, availableItems);

            if (amount <= 0)
            {
                user.Message(
                    MessageHud.MessageType.Center,
                    "$msg_donthaveany " +
                    fuelName);
                return false;
            }

            inventory.RemoveItem(
                fuelName,
                amount);

            user.Message(
                MessageHud.MessageType.Center,
                "$msg_added " +
                fuelName +
                " x" +
                amount);

            nview.InvokeRPC(
                BulkAddFuelRpc,
                amount);

            return true;
        }

        private static bool TryFindOre(
            Smelter smelter,
            Inventory inventory,
            ItemDrop.ItemData? requestedItem,
            out ItemDrop.ItemData selectedItem,
            out string prefabName)
        {
            selectedItem = null!;
            prefabName = string.Empty;

            for (int index = 0;
                 index < smelter.m_conversion.Count;
                 index++)
            {
                Smelter.ItemConversion conversion =
                    smelter.m_conversion[index];

                if (conversion == null ||
                    conversion.m_from == null)
                {
                    continue;
                }

                if (requestedItem != null)
                {
                    if (requestedItem.m_dropPrefab == null ||
                        requestedItem.m_dropPrefab.name !=
                        conversion.m_from.gameObject.name)
                    {
                        continue;
                    }

                    selectedItem = requestedItem;
                    prefabName =
                        conversion.m_from.gameObject.name;
                    return true;
                }

                ItemDrop.ItemData inventoryItem =
                    inventory.GetItem(
                        conversion.m_from
                            .m_itemData
                            .m_shared
                            .m_name);

                if (inventoryItem == null)
                {
                    continue;
                }

                selectedItem = inventoryItem;
                prefabName =
                    conversion.m_from.gameObject.name;
                return true;
            }

            return false;
        }

        private static void ReceiveBulkOre(
            Smelter smelter,
            string itemName,
            int requestedAmount)
        {
            ZNetView nview = GetNView(smelter);
            if (nview == null ||
                !nview.IsValid() ||
                !nview.IsOwner() ||
                requestedAmount <= 0 ||
                !IsAllowedOre(smelter, itemName))
            {
                return;
            }

            ZDO zdo = nview.GetZDO();
            int queueSize =
                zdo.GetInt(ZDOVars.s_queued);
            int amount =
                Math.Min(
                    requestedAmount,
                    smelter.m_maxOre - queueSize);

            if (amount <= 0)
            {
                return;
            }

            for (int index = 0;
                 index < amount;
                 index++)
            {
                zdo.Set(
                    "item" + (queueSize + index),
                    itemName);
            }

            zdo.Set(
                ZDOVars.s_queued,
                queueSize + amount,
                false);

            smelter.m_oreAddedEffects.Create(
                smelter.transform.position,
                smelter.transform.rotation);
        }

        private static void ReceiveBulkFuel(
            Smelter smelter,
            int requestedAmount)
        {
            ZNetView nview = GetNView(smelter);
            if (nview == null ||
                !nview.IsValid() ||
                !nview.IsOwner() ||
                requestedAmount <= 0 ||
                smelter.m_fuelItem == null)
            {
                return;
            }

            ZDO zdo = nview.GetZDO();
            float currentFuel =
                zdo.GetFloat(ZDOVars.s_fuel);
            int amount =
                Math.Min(
                    requestedAmount,
                    Mathf.FloorToInt(
                        smelter.m_maxFuel -
                        currentFuel));

            if (amount <= 0)
            {
                return;
            }

            zdo.Set(
                ZDOVars.s_fuel,
                currentFuel + amount);

            smelter.m_fuelAddedEffects.Create(
                smelter.transform.position,
                smelter.transform.rotation,
                smelter.transform);
        }

        private static bool IsAllowedOre(
            Smelter smelter,
            string itemName)
        {
            for (int index = 0;
                 index < smelter.m_conversion.Count;
                 index++)
            {
                Smelter.ItemConversion conversion =
                    smelter.m_conversion[index];

                if (conversion != null &&
                    conversion.m_from != null &&
                    conversion.m_from.gameObject.name ==
                    itemName)
                {
                    return true;
                }
            }

            return false;
        }

        private static ZNetView GetNView(
            Smelter smelter)
        {
            if (smelter == null)
            {
                return null!;
            }

            ZNetView nview =
                smelter.GetComponent<ZNetView>();

            return nview != null
                ? nview
                : smelter.GetComponentInParent<ZNetView>();
        }

        internal static string AddBulkFeedHint(
            string hoverText)
        {
            if (!IsBulkFeedEnabled())
            {
                return hoverText;
            }

            return hoverText +
                   Localization.instance.Localize(
                       "\n[<color=yellow><b>Left Shift + $KEY_Use</b></color>] Fill");
        }
    }

    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class Smelter_Awake_Patch
    {
        private static void Postfix(Smelter __instance)
        {
            switch (__instance.m_name)
            {
                case "$piece_smelter":
                case "$piece_blastfurnace":
                case "$piece_eitrrefinery":
                    __instance.m_maxOre = 100;
                    __instance.m_maxFuel = 200;
                    break;

                case "$piece_charcoalkiln":
                    __instance.m_maxOre = 200;
                    break;

                case "$piece_windmill":
                case "$piece_spinningwheel":
                    __instance.m_maxOre = 100;
                    break;

                case "$piece_bathtub":
                    __instance.m_maxFuel = 50;
                    break;
            }

            Patch_Smelter.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class Smelter_OnAddOre_Patch
    {
        private static bool Prefix(
            Smelter __instance,
            Humanoid user,
            ItemDrop.ItemData item,
            ref bool __result)
        {
            if (!Patch_Smelter.IsBulkFeedRequested())
            {
                return true;
            }

            __result = Patch_Smelter.TryBulkAddOre(
                __instance,
                user,
                item);
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class Smelter_OnAddFuel_Patch
    {
        private static bool Prefix(
            Smelter __instance,
            Humanoid user,
            ItemDrop.ItemData item,
            ref bool __result)
        {
            if (!Patch_Smelter.IsBulkFeedRequested())
            {
                return true;
            }

            __result = Patch_Smelter.TryBulkAddFuel(
                __instance,
                user,
                item);
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
    internal static class Smelter_OnHoverAddOre_Patch
    {
        private static void Postfix(ref string __result)
        {
            __result =
                Patch_Smelter.AddBulkFeedHint(__result);
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
    internal static class Smelter_OnHoverAddFuel_Patch
    {
        private static void Postfix(ref string __result)
        {
            __result =
                Patch_Smelter.AddBulkFeedHint(__result);
        }
    }

    [HarmonyPatch(typeof(Windmill), nameof(Windmill.GetPowerOutput))]
    internal static class Windmill_GetPowerOutput_Patch
    {
        private static bool Prefix(ref float __result)
        {
            if (!Patch_Smelter.IsWindmillProductionQuery())
            {
                return true;
            }

            __result = 1f;
            return false;
        }
    }

    [HarmonyPatch(typeof(Smelter), "UpdateSmelter")]
    internal static class Windmill_Smelter_UpdateSmelter_Patch
    {
        private static void Prefix(
            Smelter __instance,
            out bool __state)
        {
            __state = Patch_Smelter
                .BeginWindmillProductionQuery(__instance);
        }

        private static void Finalizer(bool __state)
        {
            Patch_Smelter
                .EndWindmillProductionQuery(__state);
        }
    }
}
