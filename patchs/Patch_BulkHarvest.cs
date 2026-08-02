using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class Patch_BulkHarvest
    {
        private const KeyCode HarvestModifierKey = KeyCode.LeftShift;
        private const int ColliderBufferSize = 256;

        private static readonly Collider[] HarvestColliders =
            new Collider[ColliderBufferSize];

        private static readonly HashSet<int> ProcessedObjects =
            new HashSet<int>();

        private static ConfigEntry<bool> Enabled = null!;
        private static ConfigEntry<float> HarvestRadius = null!;
        private static ConfigEntry<int> MaxTargets = null!;
        private static ConfigEntry<bool> HarvestSameTypeOnly = null!;
        private static ConfigEntry<bool> IncludeBeehives = null!;
        private static ConfigEntry<bool> ShowHoverHint = null!;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            Enabled = plugin.config(
                "BulkHarvest",
                "Enabled",
                true,
                "Enables server-controlled area harvesting with LeftShift + Use. Example: false restores normal single-target harvesting.");

            HarvestRadius = plugin.config(
                "BulkHarvest",
                "HarvestRadius",
                3f,
                new ConfigDescription(
                    "Sets the search radius for bulk harvesting in meters. Example: 3 collects eligible plants and beehives within three meters.",
                    new AcceptableValueRange<float>(1f, 20f)));

            MaxTargets = plugin.config(
                "BulkHarvest",
                "MaxTargets",
                50,
                new ConfigDescription(
                    "Limits how many objects one bulk-harvest action can interact with, including the original target. Example: 50 prevents very large farms from being processed in one frame.",
                    new AcceptableValueRange<int>(1, 200)));

            HarvestSameTypeOnly = plugin.config(
                "BulkHarvest",
                "HarvestSameTypeOnly",
                true,
                "Controls which nearby objects are harvested. Example: true harvests only the same plant type, while false harvests every eligible plant and beehive in range.");

            IncludeBeehives = plugin.config(
                "BulkHarvest",
                "IncludeBeehives",
                true,
                "Includes beehives with available honey in bulk harvesting. Example: LeftShift + Use on one beehive collects honey from nearby ready beehives.");

            ShowHoverHint = plugin.config(
                "BulkHarvest",
                "ShowHoverHint",
                true,
                "Adds a LeftShift + Use area-harvest hint to eligible plants and beehives. Example: false hides the hint without disabling bulk harvesting.");
        }

        private static bool IsEnabled()
        {
            return Enabled != null && Enabled.Value;
        }

        private static bool IsHarvestModifierHeld()
        {
            return ZInput.GetKey(HarvestModifierKey, false);
        }

        private static string GetPrefabName(GameObject gameObject)
        {
            return gameObject == null
                ? string.Empty
                : Utils.GetPrefabName(gameObject);
        }

        private static bool IsEligiblePickable(Pickable? pickable)
        {
            return pickable != null &&
                   !pickable.GetPicked() &&
                   pickable.GetEnabled != 0;
        }

        private static bool IsEligibleBeehive(Beehive? beehive)
        {
            return IncludeBeehives != null &&
                   IncludeBeehives.Value &&
                   beehive != null &&
                   beehive.GetHoneyLevel() > 0 &&
                   PrivateArea.CheckAccess(
                       beehive.transform.position,
                       0f,
                       false);
        }

        private static Interactable? GetInteractable(GameObject gameObject)
        {
            return gameObject == null
                ? null
                : gameObject.GetComponentInParent<Interactable>();
        }

        private static bool MatchesRootType(
            Pickable? candidatePickable,
            Beehive? candidateBeehive,
            Pickable? rootPickable,
            Beehive? rootBeehive,
            string rootPrefabName)
        {
            if (HarvestSameTypeOnly == null ||
                !HarvestSameTypeOnly.Value)
            {
                return true;
            }

            if (rootPickable != null)
            {
                return candidatePickable != null &&
                       GetPrefabName(candidatePickable.gameObject) ==
                       rootPrefabName;
            }

            return rootBeehive != null && candidateBeehive != null;
        }

        private static string BuildHoverHint()
        {
            return Localization.instance.Localize(
                "\n[<color=yellow><b>LeftShift + $KEY_Use</b></color>] Harvest nearby");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "Interact")]
        private static void Player_Interact_Prefix(
            Player __instance,
            GameObject go,
            bool hold,
            bool alt)
        {
            if (!IsEnabled() ||
                __instance == null ||
                __instance != Player.m_localPlayer ||
                go == null ||
                hold ||
                __instance.InAttack() ||
                __instance.InDodge() ||
                !IsHarvestModifierHeld())
            {
                return;
            }

            Interactable? rootInteractable = GetInteractable(go);
            Pickable? rootPickable = rootInteractable as Pickable;
            Beehive? rootBeehive = rootInteractable as Beehive;

            if (!IsEligiblePickable(rootPickable) &&
                !IsEligibleBeehive(rootBeehive))
            {
                return;
            }

            MonoBehaviour? rootBehaviour = rootInteractable as MonoBehaviour;
            if (rootBehaviour == null)
            {
                return;
            }

            string rootPrefabName = rootPickable == null
                ? string.Empty
                : GetPrefabName(rootPickable.gameObject);

            float radius = HarvestRadius == null
                ? 3f
                : Mathf.Clamp(HarvestRadius.Value, 1f, 20f);

            int targetLimit = MaxTargets == null
                ? 50
                : Mathf.Clamp(MaxTargets.Value, 1, 200);

            int rootInstanceId = rootBehaviour.gameObject.GetInstanceID();
            ProcessedObjects.Clear();
            ProcessedObjects.Add(rootInstanceId);

            int colliderCount = Physics.OverlapSphereNonAlloc(
                rootBehaviour.transform.position,
                radius,
                HarvestColliders,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            int processedCount = 1;

            for (int index = 0;
                 index < colliderCount && processedCount < targetLimit;
                 index++)
            {
                Collider? collider = HarvestColliders[index];
                HarvestColliders[index] = null!;

                if (collider == null)
                {
                    continue;
                }

                Pickable? candidatePickable =
                    collider.GetComponentInParent<Pickable>();

                Beehive? candidateBeehive = candidatePickable == null
                    ? collider.GetComponentInParent<Beehive>()
                    : null;

                MonoBehaviour? candidateBehaviour =
                    candidatePickable != null
                        ? candidatePickable
                        : candidateBeehive;

                if (candidateBehaviour == null)
                {
                    continue;
                }

                int candidateInstanceId =
                    candidateBehaviour.gameObject.GetInstanceID();

                if (!ProcessedObjects.Add(candidateInstanceId) ||
                    !MatchesRootType(
                        candidatePickable,
                        candidateBeehive,
                        rootPickable,
                        rootBeehive,
                        rootPrefabName))
                {
                    continue;
                }

                Interactable? candidateInteractable;

                if (IsEligiblePickable(candidatePickable))
                {
                    candidateInteractable = candidatePickable;
                }
                else if (IsEligibleBeehive(candidateBeehive))
                {
                    candidateInteractable = candidateBeehive;
                }
                else
                {
                    continue;
                }

                if (candidateInteractable == null)
                {
                    continue;
                }

                candidateInteractable.Interact(
                    __instance,
                    false,
                    alt);

                processedCount++;
            }

            for (int index = 0; index < colliderCount; index++)
            {
                HarvestColliders[index] = null!;
            }

            ProcessedObjects.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        private static void Pickable_GetHoverText_Postfix(
            Pickable __instance,
            ref string __result)
        {
            if (!IsEnabled() ||
                ShowHoverHint == null ||
                !ShowHoverHint.Value ||
                string.IsNullOrEmpty(__result) ||
                !IsEligiblePickable(__instance))
            {
                return;
            }

            __result += BuildHoverHint();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
        private static void Beehive_GetHoverText_Postfix(
            Beehive __instance,
            ref string __result)
        {
            if (!IsEnabled() ||
                ShowHoverHint == null ||
                !ShowHoverHint.Value ||
                string.IsNullOrEmpty(__result) ||
                !IsEligibleBeehive(__instance))
            {
                return;
            }

            __result += BuildHoverHint();
        }
    }
}
