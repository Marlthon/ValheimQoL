using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_Container
    {
        // === Controle principal ===
        private static ConfigEntry<bool> ChestControl = null!;

        // === Baús vanilla ===
        private static ConfigEntry<int> PrivateRows = null!;
        private static ConfigEntry<int> PrivateCols = null!;

        private static ConfigEntry<int> WoodRows = null!;
        private static ConfigEntry<int> WoodCols = null!;

        private static ConfigEntry<int> IronRows = null!;
        private static ConfigEntry<int> IronCols = null!;

        private static ConfigEntry<int> BlackmetalRows = null!;
        private static ConfigEntry<int> BlackmetalCols = null!;

        private static ConfigEntry<int> BarrelRows = null!;
        private static ConfigEntry<int> BarrelCols = null!;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            ChestControl = plugin.config("ContainerControl", "EnableChestControl", true,
                "Enables custom sizes for supported vanilla containers. Example: false keeps every chest at its vanilla size.");

            PrivateRows = plugin.config("Chests", "PrivateChestRows", 2,
                new ConfigDescription("Sets the row count of the personal chest. Example: 4 rows with PrivateChestCols=3 provides 12 slots.", new AcceptableValueRange<int>(2, 20)));
            PrivateCols = plugin.config("Chests", "PrivateChestCols", 3,
                new ConfigDescription("Sets the column count of the personal chest. Example: 6 columns with PrivateChestRows=2 provides 12 slots.", new AcceptableValueRange<int>(3, 8)));

            WoodRows = plugin.config("Chests", "WoodChestRows", 2,
                new ConfigDescription("Sets the row count of the wooden chest. Example: 4 rows with WoodChestCols=5 provides 20 slots.", new AcceptableValueRange<int>(2, 10)));
            WoodCols = plugin.config("Chests", "WoodChestCols", 5,
                new ConfigDescription("Sets the column count of the wooden chest. Example: 8 columns with WoodChestRows=2 provides 16 slots.", new AcceptableValueRange<int>(5, 8)));

            IronRows = plugin.config("Chests", "IronChestRows", 4,
                new ConfigDescription("Sets the row count of the reinforced chest. Example: 6 rows with IronChestCols=6 provides 36 slots.", new AcceptableValueRange<int>(2, 20)));
            IronCols = plugin.config("Chests", "IronChestCols", 6,
                new ConfigDescription("Sets the column count of the reinforced chest. Example: 8 columns with IronChestRows=4 provides 32 slots.", new AcceptableValueRange<int>(3, 8)));

            BlackmetalRows = plugin.config("Chests", "BlackmetalChestRows", 4,
                new ConfigDescription("Sets the row count of the black metal chest. Example: 6 rows with BlackmetalChestCols=8 provides 48 slots.", new AcceptableValueRange<int>(3, 20)));
            BlackmetalCols = plugin.config("Chests", "BlackmetalChestCols", 8,
                new ConfigDescription("Sets the column count of the black metal chest. Example: 8 columns with BlackmetalChestRows=4 provides 32 slots.", new AcceptableValueRange<int>(6, 8)));

            BarrelRows = plugin.config("Chests", "BarrelChestRows", 3,
                new ConfigDescription("Sets the row count of the barrel container. Example: 4 rows with BarrelChestCols=3 provides 12 slots.", new AcceptableValueRange<int>(2, 10)));
            BarrelCols = plugin.config("Chests", "BarrelChestCols", 3,
                new ConfigDescription("Sets the column count of the barrel container. Example: 6 columns with BarrelChestRows=3 provides 18 slots.", new AcceptableValueRange<int>(3, 8)));

            SubscribeChanges();
        }

        private static void SubscribeChanges()
        {
            void Watch<T>(ConfigEntry<T> entry)
            {
                if (entry != null)
                    entry.SettingChanged += (_, _) => UpdateAllChests();
            }

            Watch(ChestControl);
            Watch(PrivateRows); Watch(PrivateCols);
            Watch(WoodRows); Watch(WoodCols);
            Watch(IronRows); Watch(IronCols);
            Watch(BlackmetalRows); Watch(BlackmetalCols);
            Watch(BarrelRows); Watch(BarrelCols);
        }

        // === PATCHES ===

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
        private static void Container_Awake_Postfix(Container __instance)
        {
            TryResize(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
        private static void Container_Interact_Prefix(Container __instance, Humanoid character, bool hold, bool alt)
        {
            TryResize(__instance);
        }

        // === Lógica principal ===
        private static void TryResize(Container container)
        {
            if (!ChestControl.Value) return;
            if (container == null || container.name.StartsWith("Treasure")) return;

            var nview = container.m_nview;
            if (nview == null || !nview.IsValid()) return;
            if (nview.GetZDO().GetLong(ZDOVars.s_creator) == 0L) return;

            var inv = container.GetInventory();
            if (inv == null) return;

            string name = container.transform.root.name.Trim().Replace("(Clone)", "");

            switch (name)
            {
                case "piece_chest_private":
                    inv.m_height = PrivateRows.Value;
                    inv.m_width = PrivateCols.Value;
                    break;

                case "piece_chest_wood":
                    inv.m_height = WoodRows.Value;
                    inv.m_width = WoodCols.Value;
                    break;

                case "piece_chest":
                    inv.m_height = IronRows.Value;
                    inv.m_width = IronCols.Value;
                    break;

                case "piece_chest_blackmetal":
                    inv.m_height = BlackmetalRows.Value;
                    inv.m_width = BlackmetalCols.Value;
                    break;

                case "piece_chest_barrel":
                    inv.m_height = BarrelRows.Value;
                    inv.m_width = BarrelCols.Value;
                    break;
            }
        }

        // === Atualiza todos os containers existentes (quando muda o cfg) ===
        private static void UpdateAllChests()
        {
            if (!ChestControl.Value) return;

            foreach (var container in Resources.FindObjectsOfTypeAll<Container>())
            {
                if (container == null || container.name.StartsWith("Treasure")) continue;
                var nview = container.m_nview;
                if (nview == null || !nview.IsValid()) continue;
                if (nview.GetZDO().GetLong(ZDOVars.s_creator) == 0L) continue;

                TryResize(container);
            }

            ValheimQoLPlugin.Log.LogInfo("[ValheimQoL/Container] Tamanhos dos baús atualizados.");
        }
    }
}
