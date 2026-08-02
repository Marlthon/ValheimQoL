using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimQoL
{
    internal static class Patch_InfiniteFuel
    {
        private const string BathtubToggleRpc =
            "ValheimQoL_ToggleBathtub";

        private static readonly List<FireplaceFuelState> RegisteredFireplaces =
            new List<FireplaceFuelState>();
        private static readonly List<SmokeSpawnerState> RegisteredSmokeSpawners =
            new List<SmokeSpawnerState>();
        private static readonly List<SmelterFuelState> RegisteredSmelters =
            new List<SmelterFuelState>();
        private static readonly List<CookingStationFuelState> RegisteredCookingStations =
            new List<CookingStationFuelState>();

        private static readonly int AutoExtinguishedByRainZdoKey =
            "ValheimQoL_AutoExtinguishedByRain".GetStableHashCode();
        private static readonly int BathtubEnabledZdoKey =
            "ValheimQoL_BathtubEnabled".GetStableHashCode();

        private static ConfigEntry<bool> NoFuelRequired = null!;
        private static ConfigEntry<bool> OvenNoFuelRequired = null!;
        private static ConfigEntry<bool> BathtubNoFuelRequired = null!;
        private static ConfigEntry<bool> MakeToggleable = null!;
        private static ConfigEntry<bool> DisableSmoke = null!;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            NoFuelRequired = plugin.config(
                "FireSources",
                "NoFuelRequired",
                true,
                "Makes every Fireplace-based fire source work without consuming fuel. This includes campfires, hearths, torches, sconces and braziers. Example: false restores normal Wood, Resin and Coal consumption.");

            OvenNoFuelRequired = plugin.config(
                "FireSources",
                "OvenNoFuelRequired",
                true,
                "Makes the Stone Oven work without consuming Wood. Food is still required and cooking time is unchanged. Example: false restores the vanilla Wood requirement for the Stone Oven.");

            BathtubNoFuelRequired = plugin.config(
                "FireSources",
                "BathtubNoFuelRequired",
                true,
                "Keeps the Hot Tub heated without consuming Wood and changes its Wood input into an On/Off switch. The selected state is saved in the world and synchronized in multiplayer. Example: false restores the vanilla Wood requirement and Wood input interaction for the Hot Tub.");

            MakeToggleable = plugin.config(
                "FireSources",
                "MakeToggleable",
                true,
                "Allows every Fireplace-based fire source to be turned on and off with the Use key. Example: true lets players turn off campfires, torches, sconces and braziers.");

            DisableSmoke = plugin.config(
                "FireSources",
                "DisableSmoke",
                false,
                "Stops Fireplace-based fire sources from creating smoke and prevents smoke blockage from extinguishing them. Example: true removes chimney smoke while keeping the fire and its heat active.");

            NoFuelRequired.SettingChanged += OnSettingChanged;
            OvenNoFuelRequired.SettingChanged += OnSettingChanged;
            BathtubNoFuelRequired.SettingChanged += OnSettingChanged;
            MakeToggleable.SettingChanged += OnSettingChanged;
            DisableSmoke.SettingChanged += OnSettingChanged;
        }

        internal static void Shutdown()
        {
            if (NoFuelRequired != null)
            {
                NoFuelRequired.SettingChanged -= OnSettingChanged;
            }

            if (OvenNoFuelRequired != null)
            {
                OvenNoFuelRequired.SettingChanged -= OnSettingChanged;
            }

            if (BathtubNoFuelRequired != null)
            {
                BathtubNoFuelRequired.SettingChanged -= OnSettingChanged;
            }

            if (MakeToggleable != null)
            {
                MakeToggleable.SettingChanged -= OnSettingChanged;
            }

            if (DisableSmoke != null)
            {
                DisableSmoke.SettingChanged -= OnSettingChanged;
            }

            for (int index = RegisteredFireplaces.Count - 1;
                 index >= 0;
                 index--)
            {
                FireplaceFuelState state = RegisteredFireplaces[index];
                if (state != null && state.Fireplace != null)
                {
                    Restore(state);
                }
            }

            RegisteredFireplaces.Clear();
            RestoreSmokeSpawners();
            RestoreSmelters();
            RestoreCookingStations();
        }

        private static bool IsInfiniteFuelEnabled()
        {
            return NoFuelRequired != null &&
                   NoFuelRequired.Value;
        }

        private static bool IsToggleEnabled()
        {
            return MakeToggleable != null &&
                   MakeToggleable.Value;
        }

        private static bool IsOvenInfiniteFuelEnabled()
        {
            return OvenNoFuelRequired != null &&
                   OvenNoFuelRequired.Value;
        }

        private static bool IsBathtubInfiniteFuelEnabled()
        {
            return BathtubNoFuelRequired != null &&
                   BathtubNoFuelRequired.Value;
        }

        private static bool IsSmokeDisabled()
        {
            return DisableSmoke != null &&
                   DisableSmoke.Value;
        }

        private static void OnSettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            ApplyToRegisteredFireplaces();
            ApplyToRegisteredSmokeSpawners();
            ApplyToRegisteredSmelters();
            ApplyToRegisteredCookingStations();

            if (IsSmokeDisabled())
            {
                FadeExistingSmoke();
            }
        }

        private static void ApplyToRegisteredFireplaces()
        {
            for (int index = RegisteredFireplaces.Count - 1;
                 index >= 0;
                 index--)
            {
                FireplaceFuelState state = RegisteredFireplaces[index];
                if (state == null || state.Fireplace == null)
                {
                    RegisteredFireplaces.RemoveAt(index);
                    continue;
                }

                Apply(state);
            }
        }

        private static void Apply(FireplaceFuelState state)
        {
            state.Fireplace.m_infiniteFuel =
                state.OriginalInfiniteFuel ||
                IsInfiniteFuelEnabled();

            state.Fireplace.m_canTurnOff =
                state.OriginalCanTurnOff ||
                IsToggleEnabled();

            if (IsSmokeDisabled())
            {
                state.Fireplace.m_smokeSpawner = null;
            }
            else
            {
                state.Fireplace.m_smokeSpawner =
                    state.OriginalSmokeSpawner;
            }
        }

        private static void Restore(FireplaceFuelState state)
        {
            state.Fireplace.m_infiniteFuel =
                state.OriginalInfiniteFuel;
            state.Fireplace.m_canTurnOff =
                state.OriginalCanTurnOff;
            state.Fireplace.m_smokeSpawner =
                state.OriginalSmokeSpawner;
        }

        private static void FadeExistingSmoke()
        {
            for (int index = Smoke.Instances.Count - 1;
                 index >= 0;
                 index--)
            {
                Smoke? smoke = Smoke.Instances[index] as Smoke;
                if (smoke != null)
                {
                    smoke.StartFadeOut();
                }
            }
        }

        internal static void Register(Fireplace fireplace)
        {
            if (fireplace == null)
            {
                return;
            }

            FireplaceFuelState state =
                fireplace.GetComponent<FireplaceFuelState>();

            if (state == null)
            {
                state = fireplace.gameObject
                    .AddComponent<FireplaceFuelState>();

                state.Fireplace = fireplace;
                state.OriginalInfiniteFuel =
                    fireplace.m_infiniteFuel;
                state.OriginalCanTurnOff =
                    fireplace.m_canTurnOff;
                state.OriginalSmokeSpawner =
                    fireplace.m_smokeSpawner;

                RegisteredFireplaces.Add(state);
            }
            else if (!RegisteredFireplaces.Contains(state))
            {
                RegisteredFireplaces.Add(state);
            }

            SmokeSpawner[] childSmokeSpawners =
                fireplace.GetComponentsInChildren<SmokeSpawner>(true);

            for (int index = 0;
                 index < childSmokeSpawners.Length;
                 index++)
            {
                RegisterSmokeSpawner(childSmokeSpawners[index]);
            }

            RegisterSmokeSpawner(state.OriginalSmokeSpawner);
            Apply(state);
        }

        internal static void RegisterSmokeSpawner(
            SmokeSpawner? smokeSpawner)
        {
            if (smokeSpawner == null)
            {
                return;
            }

            if (smokeSpawner.GetComponentInParent<Smelter>() != null)
            {
                return;
            }

            for (int index = 0;
                 index < RegisteredSmokeSpawners.Count;
                 index++)
            {
                if (RegisteredSmokeSpawners[index].SmokeSpawner ==
                    smokeSpawner)
                {
                    ApplySmokeSpawner(
                        RegisteredSmokeSpawners[index]);
                    return;
                }
            }

            SmokeSpawnerState state =
                new SmokeSpawnerState(
                    smokeSpawner,
                    smokeSpawner.enabled);

            RegisteredSmokeSpawners.Add(state);
            ApplySmokeSpawner(state);
        }

        private static void ApplyToRegisteredSmokeSpawners()
        {
            for (int index = RegisteredSmokeSpawners.Count - 1;
                 index >= 0;
                 index--)
            {
                SmokeSpawnerState state =
                    RegisteredSmokeSpawners[index];

                if (state.SmokeSpawner == null)
                {
                    RegisteredSmokeSpawners.RemoveAt(index);
                    continue;
                }

                ApplySmokeSpawner(state);
            }
        }

        private static void ApplySmokeSpawner(
            SmokeSpawnerState state)
        {
            state.SmokeSpawner.enabled =
                state.OriginalEnabled &&
                !IsSmokeDisabled();
        }

        private static void RestoreSmokeSpawners()
        {
            for (int index = RegisteredSmokeSpawners.Count - 1;
                 index >= 0;
                 index--)
            {
                SmokeSpawnerState state =
                    RegisteredSmokeSpawners[index];

                if (state.SmokeSpawner != null)
                {
                    state.SmokeSpawner.enabled =
                        state.OriginalEnabled;
                }
            }

            RegisteredSmokeSpawners.Clear();
        }

        internal static void RegisterSmelter(Smelter smelter)
        {
            if (smelter == null || !IsSupportedFuelFreeSmelter(smelter))
            {
                return;
            }

            SmelterFuelState state =
                smelter.GetComponent<SmelterFuelState>();

            if (state == null)
            {
                state = smelter.gameObject
                    .AddComponent<SmelterFuelState>();

                state.Smelter = smelter;
                state.OriginalMaxFuel = smelter.m_maxFuel;
                state.FuelSwitch = smelter.m_addWoodSwitch;
                state.OriginalFuelSwitchActive =
                    state.FuelSwitch != null &&
                    state.FuelSwitch.gameObject.activeSelf;

                if (state.FuelSwitch != null)
                {
                    state.OriginalFuelOnUse =
                        state.FuelSwitch.m_onUse;
                    state.OriginalFuelOnHover =
                        state.FuelSwitch.m_onHover;
                }

                RegisteredSmelters.Add(state);
            }
            else if (!RegisteredSmelters.Contains(state))
            {
                RegisteredSmelters.Add(state);
            }

            RegisterBathtubRpc(state);

            Apply(state);
        }

        private static void RegisterBathtubRpc(
            SmelterFuelState state)
        {
            if (state.RpcRegistered)
            {
                return;
            }

            ZNetView nview = GetSmelterNView(state.Smelter);
            if (nview == null || !nview.IsValid())
            {
                return;
            }

            state.NView = nview;
            nview.Register(BathtubToggleRpc, state.ReceiveToggleRpc);
            state.RpcRegistered = true;
        }

        private static bool IsSupportedFuelFreeSmelter(
            Smelter smelter)
        {
            return smelter.m_name == "$piece_bathtub";
        }

        private static bool ShouldRemoveSmelterFuel(
            Smelter smelter)
        {
            if (smelter.m_name == "$piece_bathtub")
            {
                return IsBathtubInfiniteFuelEnabled();
            }

            return false;
        }

        internal static bool ShouldHandleBathtubToggle(
            Smelter smelter)
        {
            return smelter != null &&
                   IsSupportedFuelFreeSmelter(smelter) &&
                   IsBathtubInfiniteFuelEnabled();
        }

        internal static bool RequestBathtubToggle(
            Smelter smelter)
        {
            if (!ShouldHandleBathtubToggle(smelter))
            {
                return false;
            }

            ZNetView nview = GetSmelterNView(smelter);
            if (nview == null || !nview.IsValid())
            {
                return false;
            }

            if (!nview.HasOwner())
            {
                nview.ClaimOwnership();
            }

            nview.InvokeRPC(BathtubToggleRpc);
            return true;
        }

        internal static void ReceiveBathtubToggle(
            Smelter smelter,
            ZNetView nview)
        {
            if (!ShouldHandleBathtubToggle(smelter) ||
                nview == null ||
                !nview.IsValid() ||
                !nview.IsOwner())
            {
                return;
            }

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
            {
                return;
            }

            zdo.Set(
                BathtubEnabledZdoKey,
                !zdo.GetBool(BathtubEnabledZdoKey, true));

            ApplyBathtubActiveState(smelter);
        }

        internal static bool IsBathtubTurnedOn(
            Smelter smelter)
        {
            ZNetView nview = GetSmelterNView(smelter);
            if (nview == null || !nview.IsValid())
            {
                return true;
            }

            ZDO zdo = nview.GetZDO();
            return zdo == null ||
                   zdo.GetBool(BathtubEnabledZdoKey, true);
        }

        internal static void ApplyBathtubActiveState(
            Smelter smelter)
        {
            if (!ShouldHandleBathtubToggle(smelter) ||
                smelter.m_enabledObject == null)
            {
                return;
            }

            smelter.m_enabledObject.SetActive(
                IsBathtubTurnedOn(smelter));
        }

        internal static string GetBathtubToggleHoverText(
            Smelter smelter)
        {
            bool enabled = IsBathtubTurnedOn(smelter);

            return Localization.instance.Localize(
                smelter.m_name +
                " (" +
                (enabled ? "On" : "Off") +
                ")\n[<color=yellow><b>$KEY_Use</b></color>] " +
                (enabled ? "Turn off" : "Turn on"));
        }

        private static ZNetView GetSmelterNView(
            Smelter smelter)
        {
            if (smelter == null)
            {
                return null!;
            }

            ZNetView nview = smelter.GetComponent<ZNetView>();
            return nview != null
                ? nview
                : smelter.GetComponentInParent<ZNetView>();
        }

        private static void ApplyToRegisteredSmelters()
        {
            for (int index = RegisteredSmelters.Count - 1;
                 index >= 0;
                 index--)
            {
                SmelterFuelState state = RegisteredSmelters[index];

                if (state == null || state.Smelter == null)
                {
                    RegisteredSmelters.RemoveAt(index);
                    continue;
                }

                Apply(state);
            }
        }

        private static void Apply(SmelterFuelState state)
        {
            bool removeFuel = ShouldRemoveSmelterFuel(state.Smelter);

            state.Smelter.m_maxFuel =
                removeFuel
                    ? 0
                    : state.OriginalMaxFuel;

            if (state.FuelSwitch != null)
            {
                state.FuelSwitch.gameObject.SetActive(
                    state.OriginalFuelSwitchActive);

                state.FuelSwitch.m_onUse = removeFuel
                    ? state.ToggleBathtub
                    : state.OriginalFuelOnUse;

                state.FuelSwitch.m_onHover = removeFuel
                    ? state.GetBathtubToggleHoverText
                    : state.OriginalFuelOnHover;
            }
        }

        private static void RestoreSmelters()
        {
            for (int index = RegisteredSmelters.Count - 1;
                 index >= 0;
                 index--)
            {
                SmelterFuelState state = RegisteredSmelters[index];

                if (state == null || state.Smelter == null)
                {
                    continue;
                }

                state.Smelter.m_maxFuel = state.OriginalMaxFuel;

                if (state.FuelSwitch != null)
                {
                    state.FuelSwitch.gameObject.SetActive(
                        state.OriginalFuelSwitchActive);
                    state.FuelSwitch.m_onUse =
                        state.OriginalFuelOnUse;
                    state.FuelSwitch.m_onHover =
                        state.OriginalFuelOnHover;
                }
            }

            RegisteredSmelters.Clear();
        }

        internal static void RegisterCookingStation(
            CookingStation cookingStation)
        {
            if (cookingStation == null || !IsStoneOven(cookingStation))
            {
                return;
            }

            CookingStationFuelState state =
                cookingStation.GetComponent<CookingStationFuelState>();

            if (state == null)
            {
                state = cookingStation.gameObject
                    .AddComponent<CookingStationFuelState>();

                state.CookingStation = cookingStation;
                state.FuelSwitch = cookingStation.m_addFuelSwitch;
                state.OriginalFuelSwitchActive =
                    state.FuelSwitch != null &&
                    state.FuelSwitch.gameObject.activeSelf;

                RegisteredCookingStations.Add(state);
            }
            else if (!RegisteredCookingStations.Contains(state))
            {
                RegisteredCookingStations.Add(state);
            }

            Apply(state);
        }

        private static bool IsStoneOven(
            CookingStation cookingStation)
        {
            return cookingStation.m_name == "$piece_oven" ||
                   cookingStation.gameObject.name == "piece_oven" ||
                   cookingStation.gameObject.name == "piece_oven(Clone)";
        }

        internal static bool ShouldUseInfiniteOvenFuel(
            CookingStation cookingStation)
        {
            if (cookingStation == null ||
                !IsStoneOven(cookingStation) ||
                !IsOvenInfiniteFuelEnabled())
            {
                return false;
            }

            CookingStationFuelState state =
                cookingStation.GetComponent<CookingStationFuelState>();

            return state == null || !state.SuppressInfiniteFuel;
        }

        private static void ApplyToRegisteredCookingStations()
        {
            for (int index = RegisteredCookingStations.Count - 1;
                 index >= 0;
                 index--)
            {
                CookingStationFuelState state =
                    RegisteredCookingStations[index];

                if (state == null || state.CookingStation == null)
                {
                    RegisteredCookingStations.RemoveAt(index);
                    continue;
                }

                Apply(state);
            }
        }

        private static void Apply(CookingStationFuelState state)
        {
            if (state.FuelSwitch == null)
            {
                return;
            }

            state.FuelSwitch.gameObject.SetActive(
                state.OriginalFuelSwitchActive &&
                !IsOvenInfiniteFuelEnabled());
        }

        private static void RestoreCookingStations()
        {
            for (int index = RegisteredCookingStations.Count - 1;
                 index >= 0;
                 index--)
            {
                CookingStationFuelState state =
                    RegisteredCookingStations[index];

                if (state == null ||
                    state.CookingStation == null ||
                    state.FuelSwitch == null)
                {
                    continue;
                }

                state.SuppressInfiniteFuel = false;
                state.FuelSwitch.gameObject.SetActive(
                    state.OriginalFuelSwitchActive);
            }

            RegisteredCookingStations.Clear();
        }

        internal static void SetCookingStationFuelSuppressed(
            CookingStation cookingStation,
            bool suppressed)
        {
            if (cookingStation == null)
            {
                return;
            }

            CookingStationFuelState state =
                cookingStation.GetComponent<CookingStationFuelState>();

            if (state != null)
            {
                state.SuppressInfiniteFuel = suppressed;
            }
        }

        internal static void Unregister(SmelterFuelState state)
        {
            if (state != null)
            {
                RegisteredSmelters.Remove(state);
            }
        }

        internal static void Unregister(CookingStationFuelState state)
        {
            if (state != null)
            {
                RegisteredCookingStations.Remove(state);
            }
        }

        internal static void UpdateRainAutoRelight(
            Fireplace fireplace,
            bool isWet,
            ZNetView nview)
        {
            if (fireplace == null ||
                nview == null ||
                !nview.IsValid() ||
                !nview.IsOwner())
            {
                return;
            }

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
            {
                return;
            }

            bool handlesRainRelight =
                IsInfiniteFuelEnabled() &&
                IsToggleEnabled() &&
                fireplace.m_infiniteFuel &&
                fireplace.m_canTurnOff;

            if (!handlesRainRelight)
            {
                if (zdo.GetBool(AutoExtinguishedByRainZdoKey))
                {
                    zdo.Set(AutoExtinguishedByRainZdoKey, false);
                }

                return;
            }

            int currentState =
                zdo.GetInt(ZDOVars.s_state, 1);

            if (isWet)
            {
                if (currentState == 1)
                {
                    zdo.Set(AutoExtinguishedByRainZdoKey, true);
                }

                return;
            }

            if (!zdo.GetBool(AutoExtinguishedByRainZdoKey))
            {
                return;
            }

            if (currentState != 1)
            {
                zdo.Set(ZDOVars.s_state, 1, false);
            }

            zdo.Set(AutoExtinguishedByRainZdoKey, false);
        }

        internal static void Unregister(FireplaceFuelState state)
        {
            if (state != null)
            {
                RegisteredFireplaces.Remove(state);
            }
        }

        internal static bool ShouldHandleInfiniteFuelToggle(
            Fireplace fireplace)
        {
            return fireplace != null &&
                   fireplace.m_canTurnOff &&
                   fireplace.m_infiniteFuel;
        }
    }

    internal sealed class FireplaceFuelState : MonoBehaviour
    {
        internal Fireplace Fireplace = null!;
        internal bool OriginalInfiniteFuel;
        internal bool OriginalCanTurnOff;
        internal SmokeSpawner? OriginalSmokeSpawner;

        private void OnDestroy()
        {
            Patch_InfiniteFuel.Unregister(this);
        }
    }

    internal sealed class SmokeSpawnerState
    {
        internal SmokeSpawnerState(
            SmokeSpawner smokeSpawner,
            bool originalEnabled)
        {
            SmokeSpawner = smokeSpawner;
            OriginalEnabled = originalEnabled;
        }

        internal SmokeSpawner SmokeSpawner { get; }
        internal bool OriginalEnabled { get; }
    }

    internal sealed class SmelterFuelState : MonoBehaviour
    {
        internal Smelter Smelter = null!;
        internal ZNetView? NView;
        internal Switch? FuelSwitch;
        internal Switch.Callback? OriginalFuelOnUse;
        internal Switch.TooltipCallback? OriginalFuelOnHover;
        internal int OriginalMaxFuel;
        internal bool OriginalFuelSwitchActive;
        internal bool RpcRegistered;

        internal bool ToggleBathtub(
            Switch caller,
            Humanoid user,
            ItemDrop.ItemData item)
        {
            return Patch_InfiniteFuel.RequestBathtubToggle(Smelter);
        }

        internal string GetBathtubToggleHoverText()
        {
            return Patch_InfiniteFuel
                .GetBathtubToggleHoverText(Smelter);
        }

        internal void ReceiveToggleRpc(long sender)
        {
            Patch_InfiniteFuel.ReceiveBathtubToggle(
                Smelter,
                NView!);
        }

        private void OnDestroy()
        {
            Patch_InfiniteFuel.Unregister(this);
        }
    }

    internal sealed class CookingStationFuelState : MonoBehaviour
    {
        internal CookingStation CookingStation = null!;
        internal Switch? FuelSwitch;
        internal bool OriginalFuelSwitchActive;
        internal bool SuppressInfiniteFuel;

        private void OnDestroy()
        {
            Patch_InfiniteFuel.Unregister(this);
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Awake))]
    internal static class InfiniteFuel_Fireplace_Awake_Patch
    {
        private static void Postfix(Fireplace __instance)
        {
            Patch_InfiniteFuel.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Fireplace), "UpdateState")]
    internal static class FireSources_Fireplace_UpdateState_Patch
    {
        private static void Prefix(
            Fireplace __instance,
            bool ___m_wet,
            ZNetView ___m_nview)
        {
            Patch_InfiniteFuel.UpdateRainAutoRelight(
                __instance,
                ___m_wet,
                ___m_nview);
        }
    }

    [HarmonyPatch(typeof(Smelter), "Awake")]
    [HarmonyPriority(Priority.Last)]
    internal static class InfiniteFuel_Smelter_Awake_Patch
    {
        private static void Postfix(Smelter __instance)
        {
            Patch_InfiniteFuel.RegisterSmelter(__instance);
        }
    }

    [HarmonyPatch(typeof(Smelter), nameof(Smelter.IsActive))]
    internal static class InfiniteFuel_Smelter_IsActive_Patch
    {
        private static void Postfix(
            Smelter __instance,
            ref bool __result)
        {
            if (Patch_InfiniteFuel
                    .ShouldHandleBathtubToggle(__instance))
            {
                __result = Patch_InfiniteFuel
                    .IsBathtubTurnedOn(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "UpdateState")]
    internal static class InfiniteFuel_Smelter_UpdateState_Patch
    {
        private static void Postfix(Smelter __instance)
        {
            Patch_InfiniteFuel
                .ApplyBathtubActiveState(__instance);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "Awake")]
    [HarmonyPriority(Priority.Last)]
    internal static class InfiniteFuel_CookingStation_Awake_Patch
    {
        private static void Postfix(CookingStation __instance)
        {
            Patch_InfiniteFuel.RegisterCookingStation(__instance);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "GetFuel")]
    internal static class InfiniteFuel_CookingStation_GetFuel_Patch
    {
        private static bool Prefix(
            CookingStation __instance,
            ref float __result)
        {
            if (!Patch_InfiniteFuel
                    .ShouldUseInfiniteOvenFuel(__instance))
            {
                return true;
            }

            __result = Mathf.Max(1f, __instance.m_maxFuel);
            return false;
        }
    }

    [HarmonyPatch(typeof(CookingStation), "UpdateFuel")]
    internal static class InfiniteFuel_CookingStation_UpdateFuel_Patch
    {
        private static bool Prefix(CookingStation __instance)
        {
            return !Patch_InfiniteFuel
                .ShouldUseInfiniteOvenFuel(__instance);
        }
    }

    [HarmonyPatch(typeof(CookingStation), "DropAllItems")]
    internal static class InfiniteFuel_CookingStation_DropAllItems_Patch
    {
        private static void Prefix(CookingStation __instance)
        {
            Patch_InfiniteFuel.SetCookingStationFuelSuppressed(
                __instance,
                true);
        }

        private static void Postfix(CookingStation __instance)
        {
            Patch_InfiniteFuel.SetCookingStationFuelSuppressed(
                __instance,
                false);
        }
    }

    [HarmonyPatch(typeof(SmokeSpawner), "Awake")]
    internal static class FireSources_SmokeSpawner_Awake_Patch
    {
        private static void Postfix(SmokeSpawner __instance)
        {
            Patch_InfiniteFuel.RegisterSmokeSpawner(__instance);
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    internal static class FireSources_Fireplace_Interact_Patch
    {
        private static bool Prefix(
            Fireplace __instance,
            bool hold,
            bool alt,
            ref bool __result)
        {
            if (hold ||
                alt ||
                !Patch_InfiniteFuel
                    .ShouldHandleInfiniteFuelToggle(__instance))
            {
                return true;
            }

            ZNetView nview =
                __instance.GetComponent<ZNetView>();

            if (nview == null || !nview.IsValid())
            {
                return true;
            }

            if (!nview.HasOwner())
            {
                nview.ClaimOwnership();
            }

            nview.InvokeRPC("RPC_ToggleOn");
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    internal static class FireSources_Fireplace_GetHoverText_Patch
    {
        private static void Postfix(
            Fireplace __instance,
            ref string __result)
        {
            if (!Patch_InfiniteFuel
                    .ShouldHandleInfiniteFuelToggle(__instance))
            {
                return;
            }

            ZNetView nview =
                __instance.GetComponent<ZNetView>();

            if (nview == null || !nview.IsValid())
            {
                return;
            }

            __result = Localization.instance.Localize(
                __instance.m_name +
                "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }
    }
}
