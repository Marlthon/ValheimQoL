using BepInEx.Configuration;
using HarmonyLib;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_MapShare
    {
        private static ConfigEntry<bool> PreventPublicToggle = null!;
        private static ConfigEntry<bool> AdminExempt = null!;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            PreventPublicToggle = plugin.config(
                "MapShare",
                "PreventPublicToggle",
                true,
                "Forces players to keep public map position sharing enabled. Example: true makes every non-exempt player remain visible to others on the map.");

            AdminExempt = plugin.config(
                "MapShare",
                "AdminExempt",
                true,
                "Allows server administrators to disable their own public map position while PreventPublicToggle is enabled. Example: false applies the same forced sharing rule to administrators.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static void Player_OnSpawned_Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer ||
                !PreventPublicToggle.Value ||
                ZNet.instance == null)
            {
                return;
            }

            ZNet.instance.SetPublicReferencePosition(true);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.SetPublicReferencePosition))]
        private static bool ZNet_SetPublicReferencePosition_Prefix(bool pub)
        {
            if (!PreventPublicToggle.Value || pub)
            {
                return true;
            }

            return AdminExempt.Value && ValheimQoLPlugin.IsLocalPlayerAdmin();
        }
    }
}
