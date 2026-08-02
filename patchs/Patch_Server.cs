using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_Server
    {
        private const int VanillaMaxPlayers = 10;
        private const string DynamicHarmonyId = "marlthon.ValheimQoL.ServerBackend";

        private static ConfigEntry<int> MaxPlayers = null!;
        private static Harmony? _dynamicHarmony;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            MaxPlayers = plugin.config(
                "Server",
                "MaxPlayers",
                40,
                "Sets the maximum number of connected players for Steam and PlayFab backends. Example: 40 allows up to 40 players. Vanilla: 10.");
        }

        public static void Shutdown()
        {
            Harmony? dynamicHarmony = _dynamicHarmony;
            if (dynamicHarmony == null)
            {
                return;
            }

            dynamicHarmony.UnpatchSelf();
            _dynamicHarmony = null;
        }

        private static int GetConfiguredMaxPlayers()
        {
            return Math.Max(1, MaxPlayers.Value);
        }

        private static uint GetConfiguredLobbyMaxPlayers()
        {
            return (uint)GetConfiguredMaxPlayers();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
        private static IEnumerable<CodeInstruction> ZNet_RPC_PeerInfo_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo getPlayerCount = AccessTools.Method(
                typeof(ZNet),
                nameof(ZNet.GetNrOfPlayers));

            MethodInfo getConfiguredLimit = AccessTools.Method(
                typeof(Patch_Server),
                nameof(GetConfiguredMaxPlayers));

            bool foundPlayerCountCall = false;
            bool replacedLimit = false;

            for (int index = 0; index < codes.Count; index++)
            {
                if (!foundPlayerCountCall)
                {
                    if (codes[index].Calls(getPlayerCount))
                    {
                        foundPlayerCountCall = true;
                    }

                    continue;
                }

                if (!codes[index].LoadsConstant(VanillaMaxPlayers))
                {
                    continue;
                }

                codes[index] = new CodeInstruction(
                    OpCodes.Call,
                    getConfiguredLimit).MoveLabelsFrom(codes[index]);

                replacedLimit = true;
                break;
            }

            if (!replacedLimit)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] Não foi possível localizar o limite vanilla em ZNet.RPC_PeerInfo.");
            }

            return codes;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
        private static void FejdStartup_Start_Postfix()
        {
            if (_dynamicHarmony != null)
            {
                return;
            }

            _dynamicHarmony = new Harmony(DynamicHarmonyId);
            ValheimQoLPlugin.Log.LogInfo(
                "[Server] Backend detectado: " + ZNet.m_onlineBackend);

            try
            {
                switch (ZNet.m_onlineBackend)
                {
                    case OnlineBackendType.PlayFab:
                        PatchPlayFabBackend();
                        break;

                    case OnlineBackendType.Steamworks:
                        PatchSteamBackend();
                        break;

                    default:
                        ValheimQoLPlugin.Log.LogWarning(
                            "[Server] Backend sem patch específico: " +
                            ZNet.m_onlineBackend);
                        break;
                }
            }
            catch (Exception exception)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] Falha ao aplicar o limite do backend: " +
                    exception);
            }
        }

        private static void PatchPlayFabBackend()
        {
            Harmony? dynamicHarmony = _dynamicHarmony;
            if (dynamicHarmony == null)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] Instância Harmony dinâmica não inicializada.");
                return;
            }

            MethodInfo createLobby = AccessTools.DeclaredMethod(
                typeof(ZPlayFabMatchmaking),
                nameof(ZPlayFabMatchmaking.CreateLobby));

            MethodInfo createNetwork = AccessTools.DeclaredMethod(
                typeof(ZPlayFabMatchmaking),
                nameof(ZPlayFabMatchmaking.CreateAndJoinNetwork));

            if (createLobby == null || createNetwork == null)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] Métodos do backend PlayFab não encontrados.");
                return;
            }

            dynamicHarmony.Patch(
                createLobby,
                transpiler: new HarmonyMethod(
                    typeof(Patch_Server),
                    nameof(PlayFab_CreateLobby_Transpiler)));

            dynamicHarmony.Patch(
                createNetwork,
                transpiler: new HarmonyMethod(
                    typeof(Patch_Server),
                    nameof(PlayFab_CreateNetwork_Transpiler)));
        }

        private static void PatchSteamBackend()
        {
            Harmony? dynamicHarmony = _dynamicHarmony;
            if (dynamicHarmony == null)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] Instância Harmony dinâmica não inicializada.");
                return;
            }

            MethodInfo setMaxPlayers = AccessTools.DeclaredMethod(
                typeof(SteamGameServer),
                nameof(SteamGameServer.SetMaxPlayerCount));

            if (setMaxPlayers == null)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] SteamGameServer.SetMaxPlayerCount não encontrado.");
                return;
            }

            dynamicHarmony.Patch(
                setMaxPlayers,
                prefix: new HarmonyMethod(
                    typeof(Patch_Server),
                    nameof(Steam_SetMaxPlayerCount_Prefix)));
        }

        private static void Steam_SetMaxPlayerCount_Prefix(ref int cPlayersMax)
        {
            cPlayersMax = GetConfiguredMaxPlayers();
        }

        private static IEnumerable<CodeInstruction> PlayFab_CreateLobby_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceFirstLimitConstant(
                instructions,
                AccessTools.Method(
                    typeof(Patch_Server),
                    nameof(GetConfiguredLobbyMaxPlayers)),
                "ZPlayFabMatchmaking.CreateLobby");
        }

        private static IEnumerable<CodeInstruction> PlayFab_CreateNetwork_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceFirstLimitConstant(
                instructions,
                AccessTools.Method(
                    typeof(Patch_Server),
                    nameof(GetConfiguredMaxPlayers)),
                "ZPlayFabMatchmaking.CreateAndJoinNetwork");
        }

        private static IEnumerable<CodeInstruction> ReplaceFirstLimitConstant(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo replacementMethod,
            string targetName)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            bool replaced = false;

            for (int index = 0; index < codes.Count; index++)
            {
                if (!codes[index].LoadsConstant(VanillaMaxPlayers))
                {
                    continue;
                }

                codes[index] = new CodeInstruction(
                    OpCodes.Call,
                    replacementMethod).MoveLabelsFrom(codes[index]);

                replaced = true;
                break;
            }

            if (!replaced)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[Server] Limite vanilla não encontrado em " +
                    targetName + ".");
            }

            return codes;
        }
    }
}
