using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_ItemBalance
    {
        private static ConfigEntry<float> GlobalWeightMultiplier = null!;
        private static ConfigEntry<float> GlobalStackMultiplier = null!;
        private static ConfigEntry<float> CoinWeight = null!;
        private static ConfigEntry<int> CoinStackMax = null!;

        private static ConfigEntry<bool> EnableFloatingItems = null!;
        private static ConfigEntry<string> NoFloatItems = null!;
        private static readonly HashSet<ItemDrop.ItemData.SharedData> _patchedShared = new();
        private static readonly HashSet<string> NoFloatSet = new();

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            GlobalWeightMultiplier = plugin.config("ItemBalance", "GlobalWeightMultiplier", 1f, "Multiplies the weight of non-coin items. Example: 0.5 halves item weight; 2 doubles it; 1 keeps vanilla values.");
            GlobalStackMultiplier = plugin.config("ItemBalance", "GlobalStackMultiplier", 1f, "Multiplies the maximum stack size of stackable non-coin items. Example: 2 changes a stack of 50 into 100; 1 keeps vanilla values.");
            CoinWeight = plugin.config("ItemBalance", "CoinWeight", 0.01f, "Sets the weight of one coin. Example: 0 makes no practical difference because the mod enforces a minimum of 0.0001; 0.01 makes 100 coins weigh 1.");
            CoinStackMax = plugin.config("ItemBalance", "CoinStackMax", 9999, "Sets the maximum number of coins per stack. Example: 9999 allows up to 9,999 coins in one inventory slot.");
            EnableFloatingItems = plugin.config("FloatingItems", "EnableFloatingItems", true, "Makes eligible dropped items float on water. Example: false leaves all item buoyancy at its vanilla behavior.");
            NoFloatItems = plugin.config("FloatingItems", "NoFloatItems", "Obsidian,BlackMetalScrap,IronScrap,CopperOre,TinOre,SilverOre,FlametalOre,BlackMetal,Iron,Copper,Tin,Silver,Flametal,Crystal,Chitin,SurtlingCore,Eitr,Softtissue,Tar,StoneBlock,SharpeningStone,DragonEgg", "Comma-separated prefab names excluded from floating. Example: IronScrap,CopperOre keeps those two items from floating. Use exact prefab names.");

            RebuildNoFloatList();
        }

        private static void RebuildNoFloatList()
        {
            NoFloatSet.Clear();
            foreach (string s in NoFloatItems.Value.Split(','))
                if (!string.IsNullOrWhiteSpace(s))
                    NoFloatSet.Add(s.Trim().ToLowerInvariant());
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Awake))]
        private static void ItemDrop_Awake_Postfix(ItemDrop __instance)
        {
            if (GlobalWeightMultiplier == null) return;
            if (__instance == null) return;

            var shared = __instance.m_itemData?.m_shared;
            if (shared == null) return;

            if (_patchedShared.Contains(shared)) return;
            _patchedShared.Add(shared);

            string prefabName = __instance.gameObject.name
                .Replace("(Clone)", "")
                .ToLowerInvariant();
            string displayName = (shared.m_name ?? "").ToLowerInvariant();

            if (prefabName == "coins" || displayName.Contains("coin"))
            {
                shared.m_weight = Mathf.Max(0.0001f, CoinWeight.Value);
                shared.m_maxStackSize = Mathf.Clamp(CoinStackMax.Value, 1, 999999);
                return;
            }

            if (!Mathf.Approximately(GlobalWeightMultiplier.Value, 1f))
                shared.m_weight = Mathf.Max(0.0001f, shared.m_weight * GlobalWeightMultiplier.Value);

            if (!Mathf.Approximately(GlobalStackMultiplier.Value, 1f) && shared.m_maxStackSize > 1)
                shared.m_maxStackSize = Mathf.Clamp(
                    (int)(shared.m_maxStackSize * GlobalStackMultiplier.Value),
                    1, 99999);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Start))]
        private static void ItemDrop_Start_Postfix(ItemDrop __instance)
        {
            if (!EnableFloatingItems.Value || __instance == null)
                return;

            if (!__instance.m_nview || !__instance.m_nview.IsValid())
                return;

            string prefabName = __instance.gameObject.name
                .Replace("(Clone)", "")
                .ToLowerInvariant();

            if (NoFloatSet.Contains(prefabName))
                return;

            if (__instance.GetComponent<Floating>())
                return;

            Floating floating = __instance.gameObject.AddComponent<Floating>();
            floating.m_waterLevelOffset = 0.5f;
            floating.m_forceDistance = 1f;
            floating.m_force = 0.5f;
            floating.m_balanceForceFraction = 0.02f;
            floating.m_damping = 0.05f;

            Rigidbody rb = __instance.GetComponent<Rigidbody>();
            if (!rb)
            {
                rb = __instance.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.linearDamping = 0.1f;
                rb.angularDamping = 0.05f;
            }
        }
    }
}
