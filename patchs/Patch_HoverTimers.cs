using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class Patch_HoverTimers
    {
        private const string RemainingLabel = "Remaining";
        private const string PausedLabel = "Paused";

        private static ConfigEntry<bool> ShowPlantTimer = null!;
        private static ConfigEntry<bool> ShowFermenterTimer = null!;
        private static ConfigEntry<bool> ShowCookingTimer = null!;
        private static ConfigEntry<bool> ShowBeehiveTimer = null!;
        private static ConfigEntry<bool> ShowPickableTimer = null!;
        private static ConfigEntry<bool> ShowTamingTimer = null!;
        private static ConfigEntry<bool> ShowGrowthTimer = null!;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            ShowPlantTimer = plugin.config(
                "HoverTimers",
                "ShowPlantTimer",
                true,
                "Shows the remaining growth time when looking at a healthy plant. Example: false hides only the plant timer.",
                false);

            ShowFermenterTimer = plugin.config(
                "HoverTimers",
                "ShowFermenterTimer",
                true,
                "Shows the remaining processing time when looking at an active fermenter. Example: false hides only the fermenter timer.",
                false);

            ShowCookingTimer = plugin.config(
                "HoverTimers",
                "ShowCookingTimer",
                true,
                "Shows the remaining cooking time for every uncooked food slot on Cooking Stations and Stone Ovens. Example: false hides only cooking timers.",
                false);

            ShowBeehiveTimer = plugin.config(
                "HoverTimers",
                "ShowBeehiveTimer",
                true,
                "Shows the remaining production time for the next honey when looking at a beehive. Example: false hides only the beehive timer.",
                false);

            ShowPickableTimer = plugin.config(
                "HoverTimers",
                "ShowPickableTimer",
                true,
                "Shows the remaining respawn time when looking at a harvested pickable resource. Example: false hides only the pickable timer.",
                false);

            ShowTamingTimer = plugin.config(
                "HoverTimers",
                "ShowTamingTimer",
                true,
                "Shows the remaining taming time when looking at an untamed creature. Example: false hides only the taming timer.",
                false);

            ShowGrowthTimer = plugin.config(
                "HoverTimers",
                "ShowGrowthTimer",
                true,
                "Shows the remaining growth time when looking at offspring or a growing egg. Example: false hides only offspring and egg growth timers.",
                false);
        }

        private static bool TryGetZDO(
            Component component,
            out ZDO zdo)
        {
            zdo = null!;

            ZNetView nview = component.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
            {
                return false;
            }

            zdo = nview.GetZDO();
            return zdo != null;
        }

        private static bool TryGetElapsedSeconds(
            long startTicks,
            out double elapsedSeconds)
        {
            elapsedSeconds = 0d;

            if (startTicks <= 0L || ZNet.instance == null)
            {
                return false;
            }

            try
            {
                DateTime startTime = new DateTime(startTicks);
                elapsedSeconds =
                    (ZNet.instance.GetTime() - startTime).TotalSeconds;

                if (elapsedSeconds < 0d)
                {
                    elapsedSeconds = 0d;
                }

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static string FormatDuration(double seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(
                Math.Ceiling(Math.Max(0d, seconds)));

            if (duration.TotalDays >= 1d)
            {
                return
                    ((int)duration.TotalDays) +
                    "d " +
                    duration.Hours +
                    "h";
            }

            if (duration.TotalHours >= 1d)
            {
                return
                    ((int)duration.TotalHours) +
                    "h " +
                    duration.Minutes +
                    "m";
            }

            if (duration.TotalMinutes >= 1d)
            {
                return
                    ((int)duration.TotalMinutes) +
                    "m " +
                    duration.Seconds +
                    "s";
            }

            return duration.Seconds + "s";
        }

        private static void AppendRemaining(
            ref string hoverText,
            double remainingSeconds)
        {
            if (remainingSeconds <= 0d)
            {
                return;
            }

            string timer =
                RemainingLabel +
                ": " +
                FormatDuration(remainingSeconds);

            hoverText = string.IsNullOrEmpty(hoverText)
                ? timer
                : hoverText + "\n" + timer;
        }

        private static void AppendPaused(ref string hoverText)
        {
            hoverText = string.IsNullOrEmpty(hoverText)
                ? PausedLabel
                : hoverText + "\n" + PausedLabel;
        }

        private static bool ContainsRemainingTimer(
            string hoverText)
        {
            return !string.IsNullOrEmpty(hoverText) &&
                   hoverText.IndexOf(
                       RemainingLabel + ":",
                       StringComparison.Ordinal) >= 0;
        }

        private static bool TryGetGrowthRemaining(
            Component component,
            out double remainingSeconds)
        {
            remainingSeconds = 0d;

            if (ShowGrowthTimer == null ||
                !ShowGrowthTimer.Value)
            {
                return false;
            }

            Growup growup = component.GetComponent<Growup>();
            BaseAI baseAI = component.GetComponent<BaseAI>();

            if (growup == null || baseAI == null)
            {
                return false;
            }

            remainingSeconds =
                growup.m_growTime -
                baseAI.GetTimeSinceSpawned().TotalSeconds;

            return remainingSeconds > 0d;
        }

        private static void AppendCookingTimers(
            CookingStation cookingStation,
            ref string hoverText)
        {
            if (ShowCookingTimer == null ||
                !ShowCookingTimer.Value ||
                cookingStation == null ||
                cookingStation.m_slots == null ||
                cookingStation.m_conversion == null ||
                !TryGetZDO(cookingStation, out ZDO zdo))
            {
                return;
            }

            string timers = "";

            for (int slot = 0;
                 slot < cookingStation.m_slots.Length;
                 slot++)
            {
                string slotKey = "slot" + slot;
                string itemName = zdo.GetString(slotKey, "");

                if (string.IsNullOrEmpty(itemName) ||
                    zdo.GetInt("slotstatus" + slot, 0) != 0)
                {
                    continue;
                }

                CookingStation.ItemConversion? conversion = null;

                for (int conversionIndex = 0;
                     conversionIndex < cookingStation.m_conversion.Count;
                     conversionIndex++)
                {
                    CookingStation.ItemConversion candidate =
                        cookingStation.m_conversion[conversionIndex];

                    if (candidate != null &&
                        candidate.m_from != null &&
                        candidate.m_from.gameObject.name == itemName)
                    {
                        conversion = candidate;
                        break;
                    }
                }

                if (conversion == null)
                {
                    continue;
                }

                double remainingSeconds =
                    conversion.m_cookTime -
                    zdo.GetFloat(slotKey, 0f);

                if (remainingSeconds <= 0d)
                {
                    continue;
                }

                if (timers.Length > 0)
                {
                    timers += " / ";
                }

                timers += FormatDuration(remainingSeconds);
            }

            if (timers.Length == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(hoverText))
            {
                hoverText = Localization.instance.Localize(
                    cookingStation.m_name);
            }

            hoverText += "\n" + RemainingLabel + ": " + timers;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
        private static void Plant_GetHoverText_Postfix(
            Plant __instance,
            ref string __result)
        {
            if (ShowPlantTimer == null ||
                !ShowPlantTimer.Value ||
                __instance.GetStatus() != Plant.Status.Healthy ||
                !TryGetZDO(__instance, out ZDO zdo))
            {
                return;
            }

            long plantedTicks =
                zdo.GetLong(ZDOVars.s_plantTime, 0L);

            if (!TryGetElapsedSeconds(
                    plantedTicks,
                    out double elapsedSeconds))
            {
                return;
            }

            int seed = zdo.GetInt(ZDOVars.s_seed, 0);
            UnityEngine.Random.State previousRandomState =
                UnityEngine.Random.state;

            float randomValue;

            try
            {
                UnityEngine.Random.InitState(seed);
                randomValue = UnityEngine.Random.value;
            }
            finally
            {
                UnityEngine.Random.state = previousRandomState;
            }

            double totalGrowthSeconds = Mathf.Lerp(
                __instance.m_growTime,
                __instance.m_growTimeMax,
                randomValue);

            AppendRemaining(
                ref __result,
                totalGrowthSeconds - elapsedSeconds);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
        private static void Fermenter_GetHoverText_Postfix(
            Fermenter __instance,
            ref string __result)
        {
            if (ShowFermenterTimer == null ||
                !ShowFermenterTimer.Value ||
                !TryGetZDO(__instance, out ZDO zdo) ||
                string.IsNullOrEmpty(
                    zdo.GetString(ZDOVars.s_content, "")))
            {
                return;
            }

            long startTicks =
                zdo.GetLong(ZDOVars.s_startTime, 0L);

            if (!TryGetElapsedSeconds(
                    startTicks,
                    out double elapsedSeconds))
            {
                return;
            }

            AppendRemaining(
                ref __result,
                __instance.m_fermentationDuration -
                elapsedSeconds);
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(CookingStation),
            nameof(CookingStation.GetHoverText))]
        private static void CookingStation_GetHoverText_Postfix(
            CookingStation __instance,
            ref string __result)
        {
            AppendCookingTimers(__instance, ref __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText))]
        private static void CookingStationSwitch_GetHoverText_Postfix(
            Switch __instance,
            ref string __result)
        {
            CookingStation cookingStation =
                __instance.GetComponentInParent<CookingStation>();

            if (cookingStation == null ||
                cookingStation.m_addFoodSwitch != __instance)
            {
                return;
            }

            AppendCookingTimers(cookingStation, ref __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
        private static void Beehive_GetHoverText_Postfix(
            Beehive __instance,
            ref string __result)
        {
            if (ShowBeehiveTimer == null ||
                !ShowBeehiveTimer.Value ||
                !TryGetZDO(__instance, out ZDO zdo))
            {
                return;
            }

            int honeyLevel =
                zdo.GetInt(ZDOVars.s_level, 0);

            if (honeyLevel >= __instance.m_maxHoney)
            {
                return;
            }

            bool canProduce =
                __instance.CheckBiome() &&
                __instance.HaveFreeSpace();

            if (!canProduce)
            {
                AppendPaused(ref __result);
                return;
            }

            long lastUpdateTicks =
                zdo.GetLong(ZDOVars.s_lastTime, 0L);

            if (!TryGetElapsedSeconds(
                    lastUpdateTicks,
                    out double elapsedSeconds))
            {
                return;
            }

            double accumulatedSeconds =
                zdo.GetFloat(ZDOVars.s_product, 0f) +
                elapsedSeconds;

            AppendRemaining(
                ref __result,
                __instance.m_secPerUnit -
                accumulatedSeconds);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        private static void Pickable_GetHoverText_Postfix(
            Pickable __instance,
            ref string __result)
        {
            if (ShowPickableTimer == null ||
                !ShowPickableTimer.Value ||
                __instance.m_respawnTimeMinutes <= 0f ||
                __instance.GetEnabled == 0 ||
                !TryGetZDO(__instance, out ZDO zdo) ||
                !zdo.GetBool(
                    ZDOVars.s_picked,
                    __instance.m_defaultPicked))
            {
                return;
            }

            long pickedTicks =
                zdo.GetLong(ZDOVars.s_pickedTime, 0L);

            if (!TryGetElapsedSeconds(
                    pickedTicks,
                    out double elapsedSeconds))
            {
                return;
            }

            double remainingSeconds =
                __instance.m_respawnTimeMinutes *
                60d -
                elapsedSeconds;

            if (remainingSeconds <= 0d)
            {
                return;
            }

            if (string.IsNullOrEmpty(__result))
            {
                __result = Localization.instance.Localize(
                    __instance.GetHoverName());
            }

            AppendRemaining(
                ref __result,
                remainingSeconds);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        private static void Tameable_GetHoverText_Postfix(
            Tameable __instance,
            ref string __result)
        {
            if (ShowTamingTimer != null &&
                ShowTamingTimer.Value &&
                !__instance.IsTamed() &&
                TryGetZDO(__instance, out ZDO zdo))
            {
                float remainingSeconds =
                    zdo.GetFloat(
                        ZDOVars.s_tameTimeLeft,
                        __instance.m_tamingTime);

                AppendRemaining(
                    ref __result,
                    remainingSeconds);
            }

            if (TryGetGrowthRemaining(
                    __instance,
                    out double growthRemaining))
            {
                AppendRemaining(
                    ref __result,
                    growthRemaining);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
        private static void Character_GetHoverText_Postfix(
            Character __instance,
            ref string __result)
        {
            if (ContainsRemainingTimer(__result) ||
                !TryGetGrowthRemaining(
                    __instance,
                    out double growthRemaining))
            {
                return;
            }

            if (string.IsNullOrEmpty(__result))
            {
                __result = Localization.instance.Localize(
                    __instance.m_name);
            }

            AppendRemaining(
                ref __result,
                growthRemaining);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EggGrow), nameof(EggGrow.GetHoverText))]
        private static void EggGrow_GetHoverText_Postfix(
            EggGrow __instance,
            ref string __result)
        {
            if (ShowGrowthTimer == null ||
                !ShowGrowthTimer.Value ||
                !TryGetZDO(__instance, out ZDO zdo) ||
                ZNet.instance == null)
            {
                return;
            }

            float growthStart =
                zdo.GetFloat(ZDOVars.s_growStart, 0f);

            if (growthStart <= 0f)
            {
                AppendPaused(ref __result);
                return;
            }

            double elapsedSeconds =
                ZNet.instance.GetTimeSeconds() -
                growthStart;

            AppendRemaining(
                ref __result,
                __instance.m_growTime -
                elapsedSeconds);
        }
    }
}
