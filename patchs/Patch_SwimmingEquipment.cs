using BepInEx.Configuration;
using HarmonyLib;
using System;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class Patch_SwimmingEquipment
    {
        private static ConfigEntry<bool> KeepEquipmentWhileSwimming = null!;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            KeepEquipmentWhileSwimming = plugin.config(
                "Player",
                "KeepEquipmentWhileSwimming",
                true,
                "Prevents weapons, shields, tools and torches from being automatically unequipped when a player starts swimming. Example: false restores Valheim's normal behavior of putting hand items away in deep water.");
        }

        private static bool IsEnabled()
        {
            return KeepEquipmentWhileSwimming != null &&
                   KeepEquipmentWhileSwimming.Value;
        }

        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(Humanoid),
            nameof(Humanoid.HideHandItems),
            new Type[]
            {
                typeof(bool),
                typeof(bool)
            })]
        private static bool Humanoid_HideHandItems_Prefix(
            Humanoid __instance,
            ref bool __result)
        {
            Player? player = __instance as Player;

            if (!IsEnabled() ||
                player == null ||
                player.IsDead() ||
                !player.IsSwimming() ||
                player.IsOnGround())
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
