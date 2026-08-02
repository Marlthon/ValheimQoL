using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace ValheimQoL
{
    #region CONFORTO

    [HarmonyPatch(typeof(SE_Rested), "GetNearbyComfortPieces")]
    public static class SE_Rested_GetNearbyComfortPieces_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == 10f)
                {
                    instruction.operand = 20f; // Raio do conforto
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(EffectArea), "Awake")]
    public static class EffectArea_Awake_Patch
    {
        private static void Postfix(EffectArea __instance)
        {
            if ((__instance.m_type & EffectArea.Type.Heat) != 0)
            {
                UpdateFireEffectRadius(__instance, 20f);
            }
        }

        private static void UpdateFireEffectRadius(EffectArea area, float newRadius)
        {
            SphereCollider collider = area.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.radius = newRadius;
            }
        }
    }

    #endregion
}
