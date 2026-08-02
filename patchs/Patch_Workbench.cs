using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using System.Linq;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_Workbench
    {
        private static ConfigEntry<float> WorkBenchRange = null!;
        private static ConfigEntry<float> WorkBenchPlayerBase = null!;
        private static ConfigEntry<bool> NoRoofRequirement = null!;
        private static ConfigEntry<float> WorkBenchExtensionRange = null!;
        private static ConfigEntry<bool> AutoRepairWorkbench = null!;
        private static ConfigEntry<bool> EnableAreaRepair = null!;
        private static ConfigEntry<float> AreaRepairRadius = null!;

        private static int _repairCount;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            WorkBenchRange = plugin.config("Workbench", "WorkBenchRange", 20f, "Sets the workbench building radius in meters. Example: 20 allows building within 20 meters of the station.");
            WorkBenchPlayerBase = plugin.config("Workbench", "WorkBenchPlayerBase", 20f, "Sets the workbench player-base radius used by systems such as enemy spawn suppression. Example: 30 marks a 30-meter base area.");
            NoRoofRequirement = plugin.config("Workbench", "NoRoofRequirement", false, "Removes the roof requirement from crafting stations. Example: true allows using an exposed outdoor workbench.");
            WorkBenchExtensionRange = plugin.config("Workbench", "WorkBenchExtensionRange", 5f, "Sets the extra build radius added per workbench level. Example: 5 adds 5 meters for each valid station upgrade level.");
            AutoRepairWorkbench = plugin.config("Workbench", "AutoRepairWorkbench", true, "Repairs every repairable equipped or inventory item when interacting with a valid crafting station. Example: false restores one-item-at-a-time vanilla repair.");
            EnableAreaRepair = plugin.config("Workbench", "EnableAreaRepair", true, "Repairs all damaged structures inside AreaRepairRadius when using the hammer repair action. Example: false repairs only the selected piece.");
            AreaRepairRadius = plugin.config("Workbench", "AreaRepairRadius", 10f, "Sets the area-repair radius in meters. Example: 10 repairs eligible structures within 10 meters of the targeted piece.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingStation), "Start")]
        private static void CraftingStation_Start_Postfix(CraftingStation __instance)
        {
            if (WorkBenchRange.Value > 0f)
                __instance.m_rangeBuild = WorkBenchRange.Value;

            if (WorkBenchPlayerBase.Value > 0f && __instance.m_areaMarkerCircle != null)
                __instance.m_areaMarkerCircle.m_radius = WorkBenchPlayerBase.Value;

            if (WorkBenchExtensionRange.Value > 0f && WorkBenchRange.Value >= 5f)
                __instance.m_extraRangePerLevel = WorkBenchExtensionRange.Value;

            if (NoRoofRequirement.Value)
                __instance.m_craftRequireRoof = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.CheckUsable))]
        private static bool CraftingStation_CheckUsable_Prefix(ref bool __result)
        {
            if (!NoRoofRequirement.Value) return true;
            __result = true;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRepair))]
        private static void InventoryGui_UpdateRepair_Prefix(InventoryGui __instance)
        {
            if (AutoRepairWorkbench == null || !AutoRepairWorkbench.Value) return;

            var station = Player.m_localPlayer?.GetCurrentCraftingStation();
            if (station == null) return;

            int repaired = 0;
            while (__instance.HaveRepairableItems())
            {
                __instance.RepairOneItem();
                repaired++;
            }

            if (repaired > 0)
                station.m_repairItemDoneEffects.Create(station.transform.position, Quaternion.identity, null, 1f, -1);
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.RepairOneItem))]
        private static IEnumerable<CodeInstruction> InventoryGui_RepairOneItem_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var effectMethod = AccessTools.Method(typeof(EffectList), nameof(EffectList.Create));
            var noopMethod = AccessTools.Method(typeof(Patch_Workbench), nameof(NoopEffect));

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Calls(effectMethod))
                {
                    list[i].opcode = OpCodes.Call;
                    list[i].operand = noopMethod;
                }
            }
            return list;
        }

        private static GameObject[] NoopEffect(Vector3 pos, Quaternion rot, Transform parent, float scale, int variant)
            => System.Array.Empty<GameObject>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
        private static bool Player_Repair_Prefix(Player __instance, ItemDrop.ItemData toolItem)
        {
            if (!__instance.InRepairMode()) return true;
            if (!EnableAreaRepair.Value) return true;

            Piece hoveringPiece = __instance.GetHoveringPiece();
            if (hoveringPiece == null)
                return true;

            if (!__instance.CheckCanRemovePiece(hoveringPiece) ||
                !PrivateArea.CheckAccess(
                    hoveringPiece.transform.position,
                    0f,
                    false))
            {
                return true;
            }

            int repaired = RepairArea(__instance, hoveringPiece);
            if (repaired <= 0)
                return true;

            __instance.FaceLookDirection();
            __instance.m_zanim.SetTrigger(
                toolItem.m_shared.m_attack.m_attackAnimation);

            __instance.Message(
                MessageHud.MessageType.TopLeft,
                repaired == 1
                    ? Localization.instance.Localize(
                        "$msg_repaired",
                        hoveringPiece.m_name)
                    : "Area repair: " + repaired + " pieces repaired");

            __instance.UseStamina(__instance.GetBuildStamina());
            __instance.UseEitr(
                toolItem.m_shared.m_attack.m_attackEitr);

            if (toolItem.m_shared.m_useDurability)
            {
                toolItem.m_durability -=
                    toolItem.m_shared.m_useDurabilityDrain;
            }

            return false;
        }

        private static int RepairArea(Player player, Piece hoveringPiece)
        {
            _repairCount = 0;
            Vector3 origin = hoveringPiece.transform.position;

            List<WearNTear> pieces = GetAllPiecesInRadius(origin, AreaRepairRadius.Value);

            foreach (WearNTear wnt in pieces)
                RepairEach(player, wnt);

            return _repairCount;
        }

        private static void RepairEach(Player player, WearNTear wnt)
        {
            if (wnt == null) return;

            Piece piece = wnt.GetComponent<Piece>();
            if (piece == null) return;
            if (!player.CheckCanRemovePiece(piece)) return;
            if (!PrivateArea.CheckAccess(
                    piece.transform.position,
                    0f,
                    false))
                return;

            if (wnt.GetHealthPercentage() >= 1f) return;

            if (!wnt.Repair()) return;

            piece.m_placeEffect.Create(
                piece.transform.position,
                piece.transform.rotation);

            _repairCount++;
        }

        private static List<WearNTear> GetAllPiecesInRadius(Vector3 pos, float radius)
        {
            var result = new List<WearNTear>();
            foreach (WearNTear wnt in WearNTear.s_allInstances)
            {
                if (wnt == null) continue;
                if (Vector3.Distance(pos, wnt.transform.position) <= radius)
                    result.Add(wnt);
            }
            return result;
        }

    }
}
