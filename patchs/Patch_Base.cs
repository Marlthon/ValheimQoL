using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_Base
    {

        private static ConfigEntry<float> CarryWeightBase = null!;
        private static ConfigEntry<float> MegingjordBonus = null!;
        private static ConfigEntry<float> BuildDistance = null!;
        private static ConfigEntry<float> AutoPickupRange = null!;
        private static ConfigEntry<float> InteractDistance = null!;

        private static bool _appliedRuntimeTuningOnce;

        private const string MegingjordPrefab = "BeltStrength";
        private const string MegingjordToken = "$item_beltstrength";
        private const float VanillaCarryBase = 300f;
        private const float VanillaMegiBonus = 150f;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            CarryWeightBase = plugin.config("BaseTweaks", "CarryWeightBase", 300f, "Sets the player's base carry capacity. Example: 450 allows carrying 450 weight before applying the Megingjord bonus.");
            MegingjordBonus = plugin.config("BaseTweaks", "MegingjordBonus", 150f, "Sets the carry capacity added by Megingjord. Example: 300 combined with CarryWeightBase=300 provides 600 total capacity.");
            BuildDistance = plugin.config("BaseTweaks", "BuildDistance", 5f, "Sets the maximum building placement distance in meters. Example: 10 allows placing pieces up to 10 meters away.");
            AutoPickupRange = plugin.config("BaseTweaks", "AutoPickupRange", 2f, "Sets the automatic item pickup radius in meters. Example: 4 collects nearby drops within 4 meters.");
            InteractDistance = plugin.config("BaseTweaks", "InteractDistance", 5f, "Sets the maximum interaction distance in meters. Example: 8 allows opening containers and using objects from up to 8 meters away. Vanilla: 5.");

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.GetMaxCarryWeight))]
        private static void Player_GetMaxCarryWeight_Postfix(Player __instance, ref float __result)
        {
            float baseDelta = CarryWeightBase.Value - VanillaCarryBase;
            __result += baseDelta;

            if (HasMegingjordEquipped(__instance))
            {
                float megiDelta = MegingjordBonus.Value - VanillaMegiBonus;
                __result += megiDelta;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "Awake")]
        private static void Player_Awake_Postfix(Player __instance)
        {
            __instance.m_maxPlaceDistance = BuildDistance.Value;
            __instance.m_autoPickupRange = AutoPickupRange.Value;
            __instance.m_maxInteractDistance = InteractDistance.Value;

            if (!_appliedRuntimeTuningOnce && Player.m_localPlayer == __instance)
            {
                _appliedRuntimeTuningOnce = true;

                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    $"CarryWeight base: {CarryWeightBase.Value:0.#}  |  Megingjord: {MegingjordBonus.Value:0.#}");

                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    $"Build distance: {BuildDistance.Value:0.#}  |  Auto-pickup: {AutoPickupRange.Value:0.#}  |  Interact: {InteractDistance.Value:0.#}");
            }
        }

        private static bool HasMegingjordEquipped(Player player)
        {
            if (player == null) return false;
            var inv = player.GetInventory();
            if (inv == null) return false;

            List<ItemDrop.ItemData> items = inv.GetAllItems();
            foreach (var it in items)
            {
                if (it == null || !it.m_equipped) continue;

                if (it.m_dropPrefab != null && it.m_dropPrefab.name == MegingjordPrefab)
                    return true;

                string? sharedName = it.m_shared?.m_name;
                if (!string.IsNullOrEmpty(sharedName) &&
                    string.Equals(sharedName, MegingjordToken, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (sharedName != null &&
                    sharedName.IndexOf("belt", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

    }
}
