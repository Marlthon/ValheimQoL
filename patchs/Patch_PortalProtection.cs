using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class Patch_PortalProtection
    {
        private const string ZdoOwnerKey = "TargetPortal PortalOwnerId";
        private const string ZdoModeKey = "TargetPortal PortalMode";
        private const string RpcChangeMode = "TargetPortals ChangePortalMode";

        private static readonly Collider[] NearbyColliders = new Collider[64];

        private static ConfigEntry<bool> Enabled = null!;
        private static ConfigEntry<float> TerrainProtectionRadius = null!;
        private static ConfigEntry<float> PortalSearchRadius = null!;

        private static bool IsEnabled()
        {
            return Enabled != null && Enabled.Value;
        }

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            Enabled = plugin.config(
                "PortalProtection",
                "Enabled",
                true,
                "Protects TargetPortals portals from unauthorized mode changes, damage, removal, and nearby terrain edits. Example: false disables all protection from this patch.");

            TerrainProtectionRadius = plugin.config(
                "PortalProtection",
                "TerrainProtectionRadius",
                4f,
                "Sets the protected terrain radius around a TargetPortals portal, in meters. Example: 4 blocks unauthorized terrain edits within 4 meters.");

            PortalSearchRadius = plugin.config(
                "PortalProtection",
                "PortalSearchRadius",
                30f,
                "Sets the initial portal search radius, in meters. Example: 30 searches for nearby portals within 30 meters before applying TerrainProtectionRadius. Keep this value equal to or greater than TerrainProtectionRadius.");
        }

        private static string NormalizeUserId(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("Steam_", string.Empty);
        }

        private static bool IsOwnerOrAdmin(ZNetView networkView)
        {
            if (networkView == null || !networkView.IsValid())
            {
                return false;
            }

            if (ValheimQoLPlugin.IsLocalPlayerAdmin())
            {
                return true;
            }

            ZDO zdo = networkView.GetZDO();
            if (zdo == null)
            {
                return false;
            }

            string ownerId = NormalizeUserId(zdo.GetString(ZdoOwnerKey));
            if (!string.IsNullOrEmpty(ownerId))
            {
                try
                {
                    string localId = NormalizeUserId(
                        UserInfo.GetLocalUser().UserId.ToString());

                    if (!string.IsNullOrEmpty(localId) && ownerId == localId)
                    {
                        return true;
                    }
                }
                catch
                {
                    // A validação pelo criador do ZDO abaixo continua disponível.
                }
            }

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }

            long creator = zdo.GetLong(ZDOVars.s_creator);
            return creator != 0L && creator == player.GetPlayerID();
        }

        private static bool IsTargetPortal(ZNetView networkView)
        {
            if (networkView == null || !networkView.IsValid())
            {
                return false;
            }

            ZDO zdo = networkView.GetZDO();
            if (zdo == null)
            {
                return false;
            }

            return zdo.GetInt(ZdoModeKey, -1) != -1 ||
                   !string.IsNullOrEmpty(zdo.GetString(ZdoOwnerKey));
        }

        private static ZNetView? FindNetworkView(object firstParameter)
        {
            if (!(firstParameter is ZDOID zdoId) ||
                ZDOMan.instance == null ||
                ZDOMan.instance.m_objectsByID == null ||
                !ZDOMan.instance.m_objectsByID.TryGetValue(zdoId, out ZDO zdo) ||
                ZNetScene.instance == null)
            {
                return null;
            }

            return ZNetScene.instance.FindInstance(zdo);
        }

        private static bool IsNearProtectedTargetPortal(Vector3 point)
        {
            if (!IsEnabled())
            {
                return false;
            }

            float searchRadius = Mathf.Max(
                TerrainProtectionRadius.Value,
                PortalSearchRadius.Value);

            int count = Physics.OverlapSphereNonAlloc(
                point,
                searchRadius,
                NearbyColliders);

            float protectedRadius = Mathf.Max(0f, TerrainProtectionRadius.Value);

            for (int index = 0; index < count; index++)
            {
                Collider collider = NearbyColliders[index];
                System.Array.Clear(NearbyColliders, index, 1);

                if (collider == null)
                {
                    continue;
                }

                TeleportWorld portal = collider.GetComponentInParent<TeleportWorld>();
                if (portal == null ||
                    portal.m_nview == null ||
                    !portal.m_nview.IsValid() ||
                    !IsTargetPortal(portal.m_nview))
                {
                    continue;
                }

                if (Vector3.Distance(point, portal.transform.position) <=
                    protectedRadius &&
                    !IsOwnerOrAdmin(portal.m_nview))
                {
                    return true;
                }
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(ZRoutedRpc),
            nameof(ZRoutedRpc.InvokeRoutedRPC),
            typeof(long),
            typeof(string),
            typeof(object[]))]
        private static bool ZRoutedRpc_InvokeRoutedRPC_Prefix(
            string methodName,
            object[] parameters)
        {
            if (!IsEnabled() ||
                methodName != RpcChangeMode ||
                parameters == null ||
                parameters.Length == 0)
            {
                return true;
            }

            ZNetView? networkView = FindNetworkView(parameters[0]);
            if (networkView == null ||
                !IsTargetPortal(networkView) ||
                IsOwnerOrAdmin(networkView))
            {
                return true;
            }

            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                "Este portal está protegido.");

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Damage))]
        private static bool WearNTear_Damage_Prefix(WearNTear __instance)
        {
            if (!IsEnabled())
            {
                return true;
            }

            TeleportWorld portal = __instance.GetComponent<TeleportWorld>();
            return portal == null ||
                   portal.m_nview == null ||
                   !IsTargetPortal(portal.m_nview);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Remove))]
        private static bool WearNTear_Remove_Prefix(WearNTear __instance)
        {
            if (!IsEnabled())
            {
                return true;
            }

            TeleportWorld portal = __instance.GetComponent<TeleportWorld>();
            if (portal == null ||
                portal.m_nview == null ||
                !portal.m_nview.IsValid() ||
                !IsTargetPortal(portal.m_nview))
            {
                return true;
            }

            return IsOwnerOrAdmin(portal.m_nview);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.CheckAccess))]
        private static void PrivateArea_CheckAccess_Postfix(
            Vector3 point,
            bool flash,
            ref bool __result)
        {
            if (!__result || !IsNearProtectedTargetPortal(point))
            {
                return;
            }

            if (flash && Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(
                    MessageHud.MessageType.Center,
                    "O terreno está protegido.");
            }

            __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Location), nameof(Location.IsInsideNoBuildLocation))]
        private static void Location_IsInsideNoBuildLocation_Postfix(
            Vector3 point,
            ref bool __result)
        {
            if (!__result && IsNearProtectedTargetPortal(point))
            {
                __result = true;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainComp), nameof(TerrainComp.ApplyOperation))]
        private static bool TerrainComp_ApplyOperation_Prefix(TerrainOp modifier)
        {
            if (modifier == null ||
                !IsNearProtectedTargetPortal(modifier.transform.position))
            {
                return true;
            }

            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                "O terreno está protegido.");

            return false;
        }
    }
}
