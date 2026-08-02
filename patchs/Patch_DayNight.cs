using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_DayNight
    {
        private const float DefaultDayDurationMinutes = 21f;
        private const float DefaultNightDurationMinutes = 9f;
        private const long FallbackVanillaCycleLengthSeconds = 1800L;
        private const double TimeSkipDurationSeconds = 12.0;
        private const double RuntimeVerificationDurationSeconds = 10.0;

        private static ConfigEntry<bool> Enabled = null!;
        private static ConfigEntry<float> DayDurationMinutes = null!;
        private static ConfigEntry<float> NightDurationMinutes = null!;

        private static long _vanillaCycleLengthSeconds =
            FallbackVanillaCycleLengthSeconds;
        private static bool? _lastLoggedDayState;
        private static bool _loggedFirstRescaleExecution;
        private static double _phaseStartedAtNetTime;
        private static bool _phaseStartedAtBoundary;
        private static bool _phaseMeasurementInvalidated;
        private static bool? _verificationPhase;
        private static double _verificationStartedAtNetTime;
        private static float _verificationStartedAtMappedFraction;
        private static bool _runtimeVerificationCompleted;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            RemoveLegacySettings(plugin);

            Enabled = plugin.config(
                "DayNight",
                "Enabled",
                false,
                "Enables custom real-time day and night durations. Example: false restores Valheim's original cycle duration and time mapping.");

            DayDurationMinutes = plugin.config(
                "DayNight",
                "DayDurationMinutes",
                DefaultDayDurationMinutes,
                new ConfigDescription(
                    "Sets how many real minutes daytime lasts. Valheim's vanilla daytime duration is 21 real minutes.",
                    new AcceptableValueRange<float>(0.5f, 120f)));

            NightDurationMinutes = plugin.config(
                "DayNight",
                "NightDurationMinutes",
                DefaultNightDurationMinutes,
                new ConfigDescription(
                    "Sets how many real minutes nighttime lasts. Valheim's vanilla nighttime duration is 9 real minutes.",
                    new AcceptableValueRange<float>(0.5f, 120f)));

            Enabled.SettingChanged += OnConfigChanged;
            DayDurationMinutes.SettingChanged += OnConfigChanged;
            NightDurationMinutes.SettingChanged += OnConfigChanged;
        }

        internal static void LogHarmonyPatchStatus()
        {
            System.Reflection.MethodInfo targetMethod =
                AccessTools.Method(
                    typeof(EnvMan),
                    "RescaleDayFraction");
            Patches? patchInfo =
                Harmony.GetPatchInfo(targetMethod);
            bool prefixRegistered = false;

            if (patchInfo != null)
            {
                for (int index = 0;
                     index < patchInfo.Prefixes.Count;
                     index++)
                {
                    if (patchInfo.Prefixes[index].owner ==
                        ValheimQoLPlugin.ModGUID)
                    {
                        prefixRegistered = true;
                        break;
                    }
                }
            }

            ValheimQoLPlugin.Log.LogInfo(
                "[DayNight] Harmony status: EnvMan.RescaleDayFraction prefix registered=" +
                prefixRegistered +
                ".");
        }

        private static void RemoveLegacySettings(
            ValheimQoLPlugin plugin)
        {
            string configPath = plugin.Config.ConfigFilePath;
            if (!File.Exists(configPath))
            {
                return;
            }

            bool hasLegacyCycle = false;
            bool hasLegacyFraction = false;

            try
            {
                string[] lines = File.ReadAllLines(configPath);
                bool insideDayNightSection = false;

                for (int index = 0;
                     index < lines.Length;
                     index++)
                {
                    string line = lines[index].Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    {
                        insideDayNightSection =
                            line.Equals(
                                "[DayNight]",
                                StringComparison.Ordinal);
                        continue;
                    }

                    if (!insideDayNightSection ||
                        line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string settingName =
                        line.Substring(0, separatorIndex).Trim();
                    hasLegacyCycle |=
                        settingName.Equals(
                            "CycleLengthSec",
                            StringComparison.Ordinal);
                    hasLegacyFraction |=
                        settingName.Equals(
                            "DayFractionOfCycle",
                            StringComparison.Ordinal);
                }
            }
            catch (Exception exception)
            {
                ValheimQoLPlugin.Log.LogWarning(
                    "[DayNight] Could not inspect legacy configuration entries: " +
                    exception.Message);
                return;
            }

            if (!hasLegacyCycle && !hasLegacyFraction)
            {
                return;
            }

            if (hasLegacyCycle)
            {
                ConfigDefinition definition =
                    new ConfigDefinition(
                        "DayNight",
                        "CycleLengthSec");
                plugin.Config.Bind(
                    definition,
                    1800,
                    new ConfigDescription(
                        "Obsolete DayNight setting."));
                plugin.Config.Remove(definition);
            }

            if (hasLegacyFraction)
            {
                ConfigDefinition definition =
                    new ConfigDefinition(
                        "DayNight",
                        "DayFractionOfCycle");
                plugin.Config.Bind(
                    definition,
                    0.5f,
                    new ConfigDescription(
                        "Obsolete DayNight setting."));
                plugin.Config.Remove(definition);
            }

            plugin.Config.Save();
            ValheimQoLPlugin.Log.LogInfo(
                "[DayNight] Removed obsolete CycleLengthSec and DayFractionOfCycle settings. The new settings use explicit real-time minutes.");
        }

        private static void OnConfigChanged(
            object sender,
            EventArgs eventArgs)
        {
            ApplySettings(EnvMan.instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.Awake))]
        private static void EnvMan_Awake_Postfix(
            EnvMan __instance)
        {
            _vanillaCycleLengthSeconds =
                Math.Max(
                    60L,
                    __instance.m_dayLengthSec);
            ApplySettings(__instance);
        }

        private static void ApplySettings(
            EnvMan? envMan)
        {
            if (envMan == null)
            {
                return;
            }

            _lastLoggedDayState = null;
            _loggedFirstRescaleExecution = false;
            _phaseStartedAtNetTime = 0.0;
            _phaseStartedAtBoundary = false;
            _phaseMeasurementInvalidated = false;
            _verificationPhase = null;
            _verificationStartedAtNetTime = 0.0;
            _verificationStartedAtMappedFraction = 0f;
            _runtimeVerificationCompleted = false;

            if (!Enabled.Value)
            {
                envMan.m_dayLengthSec =
                    _vanillaCycleLengthSeconds;
                ValheimQoLPlugin.Log.LogInfo(
                    "[DayNight] Disabled. Restored Valheim cycle length to " +
                    _vanillaCycleLengthSeconds +
                    " real seconds.");
                return;
            }

            double daySeconds =
                GetConfiguredDaySeconds();
            double nightSeconds =
                GetConfiguredNightSeconds();
            long cycleSeconds =
                Math.Max(
                    60L,
                    (long)Math.Round(
                        daySeconds + nightSeconds,
                        MidpointRounding.AwayFromZero));

            envMan.m_dayLengthSec = cycleSeconds;

            ValheimQoLPlugin.Log.LogInfo(
                "[DayNight] Applied: day=" +
                FormatMinutes(daySeconds / 60.0) +
                " real minute(s), night=" +
                FormatMinutes(nightSeconds / 60.0) +
                " real minute(s), cycle=" +
                FormatMinutes(cycleSeconds / 60.0) +
                " real minute(s), day fraction=" +
                FormatNumber(GetConfiguredDayFraction()) +
                ".");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnvMan), "RescaleDayFraction")]
        private static bool EnvMan_RescaleDayFraction_Prefix(
            float fraction,
            ref float __result)
        {
            if (!Enabled.Value)
            {
                return true;
            }

            float dayFraction =
                GetConfiguredDayFraction();
            float halfNightFraction =
                (1f - dayFraction) * 0.5f;
            float eveningStart =
                halfNightFraction + dayFraction;
            float normalized =
                Mathf.Repeat(fraction, 1f);

            if (normalized < halfNightFraction)
            {
                __result = Mathf.Lerp(
                    0f,
                    0.25f,
                    normalized / halfNightFraction);
            }
            else if (normalized < eveningStart)
            {
                __result = Mathf.Lerp(
                    0.25f,
                    0.75f,
                    (normalized - halfNightFraction) /
                    dayFraction);
            }
            else
            {
                __result = Mathf.Lerp(
                    0.75f,
                    1f,
                    (normalized - eveningStart) /
                    halfNightFraction);
            }

            LogRuntimeState(
                normalized,
                __result,
                halfNightFraction,
                eveningStart);
            return false;
        }

        private static void LogRuntimeState(
            float rawFraction,
            float mappedFraction,
            float morningStart,
            float eveningStart)
        {
            bool isDay =
                rawFraction >= morningStart &&
                rawFraction < eveningStart;

            UpdateRuntimeVerification(
                isDay,
                mappedFraction);

            if (!_loggedFirstRescaleExecution)
            {
                _loggedFirstRescaleExecution = true;
                ValheimQoLPlugin.Log.LogInfo(
                    "[DayNight] Rescale prefix is executing. Raw fraction=" +
                    FormatNumber(rawFraction) +
                    ", mapped clock fraction=" +
                    FormatNumber(mappedFraction) +
                    ".");
            }

            if (_lastLoggedDayState.HasValue &&
                _lastLoggedDayState.Value == isDay)
            {
                return;
            }

            bool hadPreviousPhase =
                _lastLoggedDayState.HasValue;
            double currentNetTime =
                ZNet.instance != null
                    ? ZNet.instance.GetTimeSeconds()
                    : 0.0;

            if (hadPreviousPhase &&
                _phaseStartedAtBoundary &&
                !_phaseMeasurementInvalidated &&
                currentNetTime >= _phaseStartedAtNetTime)
            {
                bool completedDay =
                    _lastLoggedDayState!.Value;
                double actualDurationSeconds =
                    currentNetTime -
                    _phaseStartedAtNetTime;
                double expectedDurationSeconds =
                    completedDay
                        ? GetConfiguredDaySeconds()
                        : GetConfiguredNightSeconds();

                ValheimQoLPlugin.Log.LogInfo(
                    "[DayNight] Completed " +
                    (completedDay ? "DAY" : "NIGHT") +
                    ": measured duration=" +
                    FormatMinutes(
                        actualDurationSeconds / 60.0) +
                    " real minute(s), configured duration=" +
                    FormatMinutes(
                        expectedDurationSeconds / 60.0) +
                    " real minute(s).");
            }

            _lastLoggedDayState = isDay;
            _phaseStartedAtNetTime = currentNetTime;
            _phaseStartedAtBoundary = hadPreviousPhase;
            _phaseMeasurementInvalidated = false;
            ValheimQoLPlugin.Log.LogInfo(
                "[DayNight] Phase=" +
                (isDay ? "DAY" : "NIGHT") +
                ", expected duration=" +
                FormatMinutes(
                    (isDay
                        ? GetConfiguredDaySeconds()
                        : GetConfiguredNightSeconds()) /
                    60.0) +
                " real minute(s), clock=" +
                FormatClock(mappedFraction) +
                ".");
        }

        private static void UpdateRuntimeVerification(
            bool isDay,
            float mappedFraction)
        {
            if (_runtimeVerificationCompleted ||
                ZNet.instance == null)
            {
                return;
            }

            double currentNetTime =
                ZNet.instance.GetTimeSeconds();

            if (_phaseMeasurementInvalidated ||
                !_verificationPhase.HasValue ||
                _verificationPhase.Value != isDay)
            {
                _verificationPhase = isDay;
                _verificationStartedAtNetTime =
                    currentNetTime;
                _verificationStartedAtMappedFraction =
                    mappedFraction;
                return;
            }

            double elapsedSeconds =
                currentNetTime -
                _verificationStartedAtNetTime;
            if (elapsedSeconds <
                RuntimeVerificationDurationSeconds)
            {
                return;
            }

            double configuredPhaseSeconds =
                isDay
                    ? GetConfiguredDaySeconds()
                    : GetConfiguredNightSeconds();
            double expectedClockMinutes =
                elapsedSeconds *
                (12.0 * 60.0) /
                configuredPhaseSeconds;
            double measuredClockMinutes =
                Mathf.Repeat(
                    mappedFraction -
                    _verificationStartedAtMappedFraction,
                    1f) *
                24.0 *
                60.0;

            _runtimeVerificationCompleted = true;
            ValheimQoLPlugin.Log.LogInfo(
                "[DayNight] 10-second runtime verification: phase=" +
                (isDay ? "DAY" : "NIGHT") +
                ", elapsed=" +
                FormatNumber(elapsedSeconds) +
                " real second(s), clock advanced=" +
                FormatNumber(measuredClockMinutes) +
                " minute(s), expected=" +
                FormatNumber(expectedClockMinutes) +
                " minute(s).");
        }

        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(EnvMan),
            nameof(EnvMan.GetMorningStartSec))]
        private static bool EnvMan_GetMorningStartSec_Prefix(
            EnvMan __instance,
            int day,
            ref double __result)
        {
            if (!Enabled.Value)
            {
                return true;
            }

            double morningFraction =
                GetMorningFraction();
            __result =
                day * (double)__instance.m_dayLengthSec +
                __instance.m_dayLengthSec * morningFraction;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(EnvMan),
            nameof(EnvMan.SkipToMorning))]
        private static bool EnvMan_SkipToMorning_Prefix(
            EnvMan __instance)
        {
            if (!Enabled.Value ||
                ZNet.instance == null)
            {
                return true;
            }

            double currentTime =
                ZNet.instance.GetTimeSeconds();
            double morningFraction =
                GetMorningFraction();
            double dayLength =
                __instance.m_dayLengthSec;
            int currentMorningDay =
                Mathf.FloorToInt(
                    (float)(
                        (currentTime -
                         dayLength * morningFraction) /
                        dayLength));

            double nextMorning =
                (currentMorningDay + 1) * dayLength +
                dayLength * morningFraction;

            __instance.m_skipTime = true;
            __instance.m_skipToTime = nextMorning;
            __instance.m_timeSkipSpeed =
                (nextMorning - currentTime) /
                TimeSkipDurationSeconds;
            _phaseMeasurementInvalidated = true;

            return false;
        }

        private static double GetConfiguredDaySeconds()
        {
            float minutes =
                DayDurationMinutes == null
                    ? DefaultDayDurationMinutes
                    : DayDurationMinutes.Value;
            return Math.Max(30.0, minutes * 60.0);
        }

        private static double GetConfiguredNightSeconds()
        {
            float minutes =
                NightDurationMinutes == null
                    ? DefaultNightDurationMinutes
                    : NightDurationMinutes.Value;
            return Math.Max(30.0, minutes * 60.0);
        }

        private static float GetConfiguredDayFraction()
        {
            double daySeconds =
                GetConfiguredDaySeconds();
            double cycleSeconds =
                daySeconds +
                GetConfiguredNightSeconds();
            return (float)(daySeconds / cycleSeconds);
        }

        private static double GetMorningFraction()
        {
            return
                (1.0 - GetConfiguredDayFraction()) *
                0.5;
        }

        private static string FormatClock(
            float fraction)
        {
            float totalMinutes =
                Mathf.Repeat(fraction, 1f) *
                24f *
                60f;
            int hours =
                Mathf.FloorToInt(totalMinutes / 60f);
            int minutes =
                Mathf.FloorToInt(totalMinutes) % 60;
            return hours.ToString("00", CultureInfo.InvariantCulture) +
                   ":" +
                   minutes.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FormatMinutes(
            double value)
        {
            return value.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
        }

        private static string FormatNumber(
            double value)
        {
            return value.ToString(
                "0.0000",
                CultureInfo.InvariantCulture);
        }
    }
}
