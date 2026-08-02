using BepInEx.Configuration;
using HarmonyLib;
using System;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_WearNTear
    {
        // ── Clima / água ──────────────────────────────────────────────────────
        private static ConfigEntry<bool> NoWeatherDamage = null!;
        private static ConfigEntry<bool> NoWaterDamage = null!;

        // ── Integridade estrutural ─────────────────────────────────────────────
        private static ConfigEntry<bool> StructuralIntegrityControl = null!;
        private static ConfigEntry<float> WoodIntegrity = null!;
        private static ConfigEntry<float> StoneIntegrity = null!;
        private static ConfigEntry<float> IronIntegrity = null!;
        private static ConfigEntry<float> HardwoodIntegrity = null!;
        private static ConfigEntry<float> MarbleIntegrity = null!;
        private static ConfigEntry<float> AshstoneIntegrity = null!;
        private static ConfigEntry<float> AncientIntegrity = null!;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            NoWeatherDamage = plugin.config("WearNTear", "NoWeatherDamage", true, "Prevents normal rain weathering damage to building pieces. Example: true keeps uncovered wooden structures from losing health due to rain. This setting does not disable Ashlands ash or lava damage.");
            NoWaterDamage = plugin.config("WearNTear", "NoWaterDamage", true, "Prevents underwater weathering damage to building pieces. Example: true stops submerged structures from being treated as wet by water.");

            StructuralIntegrityControl = plugin.config("WearNTear", "StructuralIntegrityControl", true, "Enables the per-material structural integrity adjustments below. Example: false ignores all WoodIntegrity, StoneIntegrity, and other material values.");
            WoodIntegrity = plugin.config("WearNTear", "WoodIntegrity", 0f, "Reduces structural support loss over distance for wood, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported wood pieces from collapsing.");
            StoneIntegrity = plugin.config("WearNTear", "StoneIntegrity", 0f, "Reduces structural support loss over distance for stone, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported stone pieces from collapsing.");
            IronIntegrity = plugin.config("WearNTear", "IronIntegrity", 0f, "Reduces structural support loss over distance for iron, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported iron pieces from collapsing.");
            HardwoodIntegrity = plugin.config("WearNTear", "HardwoodIntegrity", 0f, "Reduces structural support loss over distance for core wood, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported core wood pieces from collapsing.");
            MarbleIntegrity = plugin.config("WearNTear", "MarbleIntegrity", 0f, "Reduces structural support loss over distance for black marble, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported marble pieces from collapsing.");
            AshstoneIntegrity = plugin.config("WearNTear", "AshstoneIntegrity", 0f, "Reduces structural support loss over distance for grausten, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported grausten pieces from collapsing.");
            AncientIntegrity = plugin.config("WearNTear", "AncientIntegrity", 0f, "Reduces structural support loss over distance for ancient material pieces, from 0 to 100 percent. Example: 50 halves support loss; 100 removes distance loss and prevents unsupported ancient pieces from collapsing.");
        }

        // ── Clima: sempre considera que tem telhado ───────────────────────────
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.HaveRoof))]
        private static void HaveRoof_Postfix(ref bool __result)
        {
            if (NoWeatherDamage.Value) __result = true;
        }

        // ── Água: nunca considera submerso ────────────────────────────────────
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.IsUnderWater))]
        private static void IsUnderWater_Postfix(ref bool __result)
        {
            if (NoWaterDamage.Value) __result = false;
        }

        // ── Integridade estrutural ─────────────────────────────────────────────
        // Postfix em GetMaterialProperties: reduz horizontalLoss e verticalLoss,
        // fazendo m_support decair mais devagar em UpdateSupport (mais peças empilhadas).
        // Postfix em HaveSupport: ajusta o limiar mínimo proporcionalmente,
        // garantindo consistência entre o cálculo e a verificação de sobrevivência.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.GetMaterialProperties))]
        private static void GetMaterialProperties_Postfix(
            WearNTear __instance,
            ref float maxSupport,
            ref float minSupport,
            ref float horizontalLoss,
            ref float verticalLoss)
        {
            if (!StructuralIntegrityControl.Value) return;

            float pct = GetMaterialIntegrityConfig(__instance.m_materialType);
            if (pct <= 0f) return;

            float factor = 1f - Math.Min(Math.Max(pct, 0f), 100f) / 100f;
            horizontalLoss *= factor;
            verticalLoss *= factor;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WearNTear), "HaveSupport")]
        private static bool HaveSupport_Prefix(WearNTear __instance, ref bool __result)
        {
            if (!StructuralIntegrityControl.Value) return true;

            float pct = GetMaterialIntegrityConfig(__instance.m_materialType);
            if (pct <= 0f) return true;

            if (pct >= 100f)
            {
                __result = true;
                return false;
            }

            __instance.GetMaterialProperties(
                out float _,
                out float minSupport,
                out float _,
                out float _);

            // minSupport já vem reduzido pelo postfix acima
            __result = __instance.m_support >= minSupport;
            return false;
        }

        private static float GetMaterialIntegrityConfig(WearNTear.MaterialType mat)
        {
            return mat switch
            {
                WearNTear.MaterialType.Wood => WoodIntegrity.Value,
                WearNTear.MaterialType.Stone => StoneIntegrity.Value,
                WearNTear.MaterialType.Iron => IronIntegrity.Value,
                WearNTear.MaterialType.HardWood => HardwoodIntegrity.Value,
                WearNTear.MaterialType.Marble => MarbleIntegrity.Value,
                WearNTear.MaterialType.Ashstone => AshstoneIntegrity.Value,
                WearNTear.MaterialType.Ancient => AncientIntegrity.Value,
                _ => 0f,
            };
        }
    }
}
