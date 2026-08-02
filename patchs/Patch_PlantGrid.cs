using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class Patch_PlantGrid
    {
        private const string CultivatorSharedName = "$item_cultivator";
        private const int MaximumSupportedGridSize = 25;

        private static readonly List<GameObject> PreviewGhosts = new List<GameObject>();
        private static readonly Collider[] SpaceColliders = new Collider[64];

        private static readonly MethodInfo GetPlaceDurabilityMethod =
            AccessTools.DeclaredMethod(typeof(Player), "GetPlaceDurability");

        private static ConfigEntry<bool> Enabled = null!;
        private static ConfigEntry<int> Rows = null!;
        private static ConfigEntry<int> Columns = null!;
        private static ConfigEntry<int> MaxPlantsPerAction = null!;
        private static ConfigEntry<float> MinimumSpacing = null!;
        private static ConfigEntry<bool> ShowGridPreview = null!;
        private static ConfigEntry<bool> ShowTotalCost = null!;
        private static ConfigEntry<bool> ConsumeStamina = null!;
        private static ConfigEntry<float> ExtraPlantStaminaCost = null!;

        private static int _previewRootInstanceId;
        private static int _previewCount;
        private static bool _placingExtraPlants;
        private static int _spaceMask;
        private static int _roofMask;
        private static Vector3 _gridCenterPosition;
        private static Quaternion _gridCenterRotation;
        private static bool _hasGridCenter;
        private static bool _rootGridPositionValid;

        internal static void InitConfig(ValheimQoLPlugin plugin)
        {
            Enabled = plugin.config(
                "PlantGrid",
                "Enabled",
                true,
                "Enables server-controlled grid planting while using the cultivator. Example: false restores normal one-at-a-time planting.");

            Rows = plugin.config(
                "PlantGrid",
                "Rows",
                2,
                new ConfigDescription(
                    "Sets the number of rows in the planting grid. Example: 2 rows with 3 columns attempts to plant 6 plants.",
                    new AcceptableValueRange<int>(1, 10)));

            Columns = plugin.config(
                "PlantGrid",
                "Columns",
                2,
                new ConfigDescription(
                    "Sets the number of columns in the planting grid. Example: 3 columns with 2 rows attempts to plant 6 plants.",
                    new AcceptableValueRange<int>(1, 10)));

            MaxPlantsPerAction = plugin.config(
                "PlantGrid",
                "MaxPlantsPerAction",
                25,
                new ConfigDescription(
                    "Limits the total number of plants created by one action. Example: a 10x10 grid with a limit of 25 plants only the first 25 positions.",
                    new AcceptableValueRange<int>(1, MaximumSupportedGridSize)));

            MinimumSpacing = plugin.config(
                "PlantGrid",
                "MinimumSpacing",
                1f,
                new ConfigDescription(
                    "Sets the minimum spacing between grid positions in meters. The mod automatically uses a larger distance when a plant needs more grow space. Example: 1.0 keeps normal crops at least one meter apart.",
                    new AcceptableValueRange<float>(0.5f, 10f)));

            ShowGridPreview = plugin.config(
                "PlantGrid",
                "ShowGridPreview",
                true,
                "Shows a placement ghost for every position in the server-defined grid. Example: false displays only Valheim's original placement ghost.");

            ShowTotalCost = plugin.config(
                "PlantGrid",
                "ShowTotalCost",
                true,
                "Shows the total resource cost of the complete grid in the build HUD. Example: a 2x2 carrot grid displays a cost of 4 seeds.");

            ConsumeStamina = plugin.config(
                "PlantGrid",
                "ConsumeStamina",
                true,
                "Controls whether extra plants consume stamina. The first plant always uses Valheim's normal stamina cost. Example: false makes only the extra plants consume no stamina.");

            ExtraPlantStaminaCost = plugin.config(
                "PlantGrid",
                "ExtraPlantStaminaCost",
                2f,
                new ConfigDescription(
                    "Sets the stamina cost for each extra plant in the grid. The first plant always uses Valheim's normal stamina cost. Example: 2 charges 2 stamina for every extra plant.",
                    new AcceptableValueRange<float>(0f, 100f)));
        }

        private static bool IsEnabled()
        {
            return Enabled != null && Enabled.Value;
        }

        private static bool IsLocalGridPlant(Player player, Piece? piece)
        {
            if (!IsEnabled() ||
                player == null ||
                player != Player.m_localPlayer ||
                piece == null ||
                !IsHoldingCultivator(player))
            {
                return false;
            }

            Plant plant = piece.GetComponent<Plant>();
            return plant != null && plant.m_attachDistance <= 0f;
        }

        private static bool IsHoldingCultivator(Player player)
        {
            ItemDrop.ItemData rightItem = player.GetRightItem();
            return rightItem != null &&
                   rightItem.m_shared != null &&
                   rightItem.m_shared.m_name == CultivatorSharedName;
        }

        private static int GetGridCount()
        {
            if (Rows == null || Columns == null || MaxPlantsPerAction == null)
            {
                return 1;
            }

            int rows = Mathf.Clamp(Rows.Value, 1, 10);
            int columns = Mathf.Clamp(Columns.Value, 1, 10);
            int configuredMaximum = Mathf.Clamp(
                MaxPlantsPerAction.Value,
                1,
                MaximumSupportedGridSize);

            return Mathf.Min(rows * columns, configuredMaximum);
        }

        internal static int GetResourceMultiplier(
            Player player,
            Piece? piece)
        {
            return IsLocalGridPlant(player, piece)
                ? GetResourceLimitedGridCount(
                    player,
                    piece!,
                    GetGridCount())
                : 1;
        }

        private static int GetResourceLimitedGridCount(
            Player player,
            Piece piece,
            int desiredCount)
        {
            int count = Mathf.Max(0, desiredCount);
            if (count == 0 ||
                ZoneSystem.instance == null ||
                ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
            {
                return count;
            }

            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement.m_resItem == null)
                {
                    continue;
                }

                int amountPerPlant = requirement.GetAmount(0);
                if (amountPerPlant <= 0)
                {
                    continue;
                }

                string sharedName =
                    requirement.m_resItem.m_itemData.m_shared.m_name;

                int available =
                    NearbyContainerManager.CountAvailableForPiece(
                        player,
                        piece,
                        sharedName);

                count = Mathf.Min(
                    count,
                    available / amountPerPlant);
            }

            return count;
        }

        private static bool ConsumeOnePlant(
            Player player,
            Piece piece)
        {
            for (int index = 0;
                 index < piece.m_resources.Length;
                 index++)
            {
                Piece.Requirement requirement =
                    piece.m_resources[index];

                if (requirement.m_resItem == null)
                {
                    continue;
                }

                int required = requirement.GetAmount(0);
                if (required <= 0)
                {
                    continue;
                }

                string sharedName =
                    requirement.m_resItem.m_itemData.m_shared.m_name;

                int consumed =
                    NearbyContainerManager.ConsumeForPiece(
                        player,
                        piece,
                        sharedName,
                        required,
                        -1);

                if (consumed != required)
                {
                    ValheimQoLPlugin.Log.LogWarning(
                        "[PlantGrid] Planting stopped because only " +
                        consumed +
                        " of " +
                        required +
                        " required resource(s) could be consumed for " +
                        sharedName +
                        ".");

                    return false;
                }
            }

            return true;
        }

        private static float GetGridSpacing(Plant plant)
        {
            float minimum = MinimumSpacing == null
                ? 1f
                : Mathf.Clamp(MinimumSpacing.Value, 0.5f, 10f);

            return Mathf.Max(minimum, plant.m_growRadius * 2.1f);
        }

        private static Vector3 GetGridPosition(
            Vector3 rootPosition,
            Quaternion rootRotation,
            float spacing,
            int index)
        {
            int rows = Rows == null
                ? 1
                : Mathf.Clamp(Rows.Value, 1, 10);

            int columns = Columns == null
                ? 1
                : Mathf.Clamp(Columns.Value, 1, 10);

            int row = index / columns;
            int column = index % columns;
            float centerRow = (rows - 1) * 0.5f;
            float centerColumn = (columns - 1) * 0.5f;

            Vector3 offset =
                rootRotation * Vector3.right *
                ((column - centerColumn) * spacing) +
                rootRotation * Vector3.forward *
                ((row - centerRow) * spacing);

            Vector3 position = rootPosition + offset;

            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGroundHeight(rootPosition, out float rootGround) &&
                ZoneSystem.instance.GetGroundHeight(position, out float targetGround))
            {
                position.y = targetGround + (rootPosition.y - rootGround);
            }

            return position;
        }

        private static Vector3 GetGridPositionFromFirstPlant(
            Vector3 firstPlantPosition,
            Quaternion gridRotation,
            float spacing,
            int index)
        {
            int columns = Columns == null
                ? 1
                : Mathf.Clamp(Columns.Value, 1, 10);

            int row = index / columns;
            int column = index % columns;

            Vector3 offset =
                gridRotation * Vector3.right *
                (column * spacing) +
                gridRotation * Vector3.forward *
                (row * spacing);

            Vector3 position = firstPlantPosition + offset;

            if (ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGroundHeight(
                    firstPlantPosition,
                    out float firstPlantGround) &&
                ZoneSystem.instance.GetGroundHeight(
                    position,
                    out float targetGround))
            {
                position.y =
                    targetGround +
                    (firstPlantPosition.y - firstPlantGround);
            }

            return position;
        }

        private static bool CreateExtraPlant(
            Player player,
            Piece piecePrefab,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject? plantedObject = null;

            TerrainModifier.SetTriggerOnPlaced(true);

            try
            {
                plantedObject = UnityEngine.Object.Instantiate(
                    piecePrefab.gameObject,
                    position,
                    rotation);
            }
            catch (Exception exception)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[PlantGrid] Failed to create an extra plant at " +
                    position +
                    ": " +
                    exception);
            }
            finally
            {
                TerrainModifier.SetTriggerOnPlaced(false);
            }

            if (plantedObject == null)
            {
                return false;
            }

            Piece plantedPiece = plantedObject.GetComponent<Piece>();
            ZNetView plantedNetView = plantedObject.GetComponent<ZNetView>();

            if (plantedPiece == null ||
                plantedNetView == null ||
                !plantedNetView.IsValid())
            {
                ValheimQoLPlugin.Log.LogError(
                    "[PlantGrid] The extra plant was not networked correctly " +
                    "and has been removed. Prefab=" +
                    piecePrefab.gameObject.name +
                    ", Position=" +
                    position +
                    ".");

                if (plantedNetView != null &&
                    plantedNetView.IsValid() &&
                    ZNetScene.instance != null)
                {
                    plantedNetView.Destroy();
                }
                else
                {
                    UnityEngine.Object.Destroy(plantedObject);
                }

                return false;
            }

            plantedPiece.SetCreator(player.GetPlayerID());
            return true;
        }

        private static bool ValidatePlantPosition(
            Piece piece,
            Plant plant,
            Vector3 position)
        {
            if (ZoneSystem.instance == null ||
                Location.IsInsideNoBuildLocation(position) ||
                !PrivateArea.CheckAccess(position, 0f, false))
            {
                return false;
            }

            Heightmap heightmap = Heightmap.FindHeightmap(position);
            if (heightmap == null)
            {
                return false;
            }

            Heightmap.Biome biome = heightmap.GetBiome(position);
            if (plant.m_biome != Heightmap.Biome.None &&
                (biome & plant.m_biome) == Heightmap.Biome.None)
            {
                return false;
            }

            if ((piece.m_cultivatedGroundOnly || plant.m_needCultivatedGround) &&
                !heightmap.IsCultivated(position))
            {
                return false;
            }

            if (!plant.m_tolerateHeat &&
                biome == Heightmap.Biome.AshLands &&
                !ShieldGenerator.IsInsideShield(position))
            {
                return false;
            }

            if (!plant.m_tolerateCold &&
                (biome == Heightmap.Biome.DeepNorth ||
                 biome == Heightmap.Biome.Mountain) &&
                !ShieldGenerator.IsInsideShield(position))
            {
                return false;
            }

            if (_roofMask == 0)
            {
                _roofMask = LayerMask.GetMask("Default", "static_solid", "piece");
            }

            if (Physics.Raycast(position, Vector3.up, 100f, _roofMask))
            {
                return false;
            }

            if (_spaceMask == 0)
            {
                _spaceMask = LayerMask.GetMask(
                    "Default",
                    "static_solid",
                    "Default_small",
                    "piece",
                    "piece_nonsolid");
            }

            int colliderCount = Physics.OverlapSphereNonAlloc(
                position,
                plant.m_growRadius,
                SpaceColliders,
                _spaceMask);

            bool blocked = false;

            for (int index = 0; index < colliderCount; index++)
            {
                Collider? collider = SpaceColliders[index];
                SpaceColliders[index] = null!;

                if (collider == null ||
                    collider.gameObject.layer == LayerMask.NameToLayer("ghost"))
                {
                    continue;
                }

                Plant otherPlant = collider.GetComponentInParent<Plant>();
                if (otherPlant == null || otherPlant.gameObject != plant.gameObject)
                {
                    blocked = true;
                }
            }

            return !blocked;
        }

        private static float GetPlaceDurability(
            Player player,
            ItemDrop.ItemData rightItem)
        {
            try
            {
                if (GetPlaceDurabilityMethod != null)
                {
                    object value = GetPlaceDurabilityMethod.Invoke(
                        player,
                        new object[] { rightItem });

                    if (value is float durability)
                    {
                        return Mathf.Max(0f, durability);
                    }
                }
            }
            catch (Exception exception)
            {
                ValheimQoLPlugin.Log.LogError(
                    "[PlantGrid] Failed to read the vanilla durability cost: " +
                    exception.Message);
            }

            return Mathf.Max(0f, rightItem.m_shared.m_useDurabilityDrain);
        }

        private static void DestroyPreviewGhosts()
        {
            for (int index = 0; index < PreviewGhosts.Count; index++)
            {
                GameObject preview = PreviewGhosts[index];
                if (preview != null)
                {
                    UnityEngine.Object.Destroy(preview);
                }
            }

            PreviewGhosts.Clear();
            _previewRootInstanceId = 0;
            _previewCount = 0;
        }

        private static void CopyVisualNode(
            Transform source,
            Transform target,
            int layer)
        {
            target.gameObject.name = source.gameObject.name;
            target.gameObject.layer = layer;
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;

            MeshFilter sourceMeshFilter =
                source.GetComponent<MeshFilter>();

            MeshRenderer sourceMeshRenderer =
                source.GetComponent<MeshRenderer>();

            if (sourceMeshFilter != null &&
                sourceMeshRenderer != null)
            {
                MeshFilter targetMeshFilter =
                    target.gameObject.AddComponent<MeshFilter>();

                targetMeshFilter.sharedMesh =
                    sourceMeshFilter.sharedMesh;

                MeshRenderer targetMeshRenderer =
                    target.gameObject.AddComponent<MeshRenderer>();

                targetMeshRenderer.sharedMaterials =
                    sourceMeshRenderer.sharedMaterials;

                targetMeshRenderer.enabled =
                    sourceMeshRenderer.enabled;
            }

            SkinnedMeshRenderer sourceSkinnedRenderer =
                source.GetComponent<SkinnedMeshRenderer>();

            if (sourceSkinnedRenderer != null)
            {
                SkinnedMeshRenderer targetSkinnedRenderer =
                    target.gameObject.AddComponent<SkinnedMeshRenderer>();

                targetSkinnedRenderer.sharedMesh =
                    sourceSkinnedRenderer.sharedMesh;

                targetSkinnedRenderer.sharedMaterials =
                    sourceSkinnedRenderer.sharedMaterials;

                targetSkinnedRenderer.localBounds =
                    sourceSkinnedRenderer.localBounds;

                targetSkinnedRenderer.enabled =
                    sourceSkinnedRenderer.enabled;
            }

            for (int index = 0; index < source.childCount; index++)
            {
                Transform sourceChild = source.GetChild(index);
                GameObject targetChild = new GameObject(
                    sourceChild.gameObject.name);

                targetChild.transform.SetParent(target, false);

                CopyVisualNode(
                    sourceChild,
                    targetChild.transform,
                    layer);

                targetChild.SetActive(
                    sourceChild.gameObject.activeSelf);
            }
        }

        private static GameObject CreateVisualPreview(
            GameObject rootGhost,
            int index)
        {
            int ghostLayer = LayerMask.NameToLayer("ghost");
            GameObject preview = new GameObject(
                "ValheimQoL_PlantGridGhost_" + index);

            preview.transform.SetParent(
                rootGhost.transform.parent,
                false);

            CopyVisualNode(
                rootGhost.transform,
                preview.transform,
                ghostLayer >= 0
                    ? ghostLayer
                    : rootGhost.layer);

            preview.SetActive(false);
            return preview;
        }

        private static void SetPreviewInvalid(
            GameObject preview,
            bool invalid)
        {
            if (MaterialMan.instance == null)
            {
                return;
            }

            if (invalid)
            {
                MaterialMan.instance.SetValue(
                    preview,
                    ShaderProps._Color,
                    Color.red);

                MaterialMan.instance.SetValue(
                    preview,
                    ShaderProps._EmissionColor,
                    Color.red * 0.7f);
            }
            else
            {
                MaterialMan.instance.ResetValue(
                    preview,
                    ShaderProps._Color);

                MaterialMan.instance.ResetValue(
                    preview,
                    ShaderProps._EmissionColor);
            }
        }

        private static void EnsurePreviewGhosts(GameObject rootGhost, int count)
        {
            int expectedPreviewCount = Mathf.Max(0, count - 1);
            int rootInstanceId = rootGhost.GetInstanceID();

            if (_previewRootInstanceId == rootInstanceId &&
                _previewCount == expectedPreviewCount &&
                PreviewGhosts.Count == expectedPreviewCount)
            {
                return;
            }

            DestroyPreviewGhosts();

            if (expectedPreviewCount == 0)
            {
                return;
            }

            bool previousNetworkInitializationState =
                ZNetView.m_forceDisableInit;

            bool previousTerrainOperationState =
                TerrainOp.m_forceDisableTerrainOps;

            ZNetView.m_forceDisableInit = true;
            TerrainOp.m_forceDisableTerrainOps = true;

            try
            {
                for (int index = 0; index < expectedPreviewCount; index++)
                {
                    GameObject preview = CreateVisualPreview(
                        rootGhost,
                        index + 1);

                    PreviewGhosts.Add(preview);
                }
            }
            finally
            {
                ZNetView.m_forceDisableInit =
                    previousNetworkInitializationState;

                TerrainOp.m_forceDisableTerrainOps =
                    previousTerrainOperationState;
            }

            _previewRootInstanceId = rootInstanceId;
            _previewCount = expectedPreviewCount;
        }

        private static void UpdatePreviewGhosts(Player player)
        {
            GameObject? rootGhost = player.m_placementGhost;
            Piece? rootPiece = rootGhost == null
                ? null
                : rootGhost.GetComponent<Piece>();

            Piece? selectedPiece =
                player.m_buildPieces == null
                    ? null
                    : player.m_buildPieces.GetSelectedPiece();

            if (rootGhost == null ||
                rootPiece == null ||
                selectedPiece == null ||
                !IsLocalGridPlant(player, selectedPiece))
            {
                _hasGridCenter = false;
                _rootGridPositionValid = false;
                DestroyPreviewGhosts();
                return;
            }

            Plant? plant = selectedPiece.GetComponent<Plant>();
            if (plant == null)
            {
                _hasGridCenter = false;
                _rootGridPositionValid = false;
                DestroyPreviewGhosts();
                return;
            }

            int configuredCount = GetGridCount();
            int fillCount = GetResourceLimitedGridCount(
                player,
                selectedPiece,
                configuredCount);

            bool basePlacementIsValid =
                player.GetPlacementStatus() ==
                Player.PlacementStatus.Valid;
            bool rootIsVisible = rootGhost.activeInHierarchy;
            float spacing = GetGridSpacing(plant);

            _gridCenterPosition = rootGhost.transform.position;
            _gridCenterRotation = rootGhost.transform.rotation;
            _hasGridCenter = true;

            Vector3 firstPosition = GetGridPosition(
                _gridCenterPosition,
                _gridCenterRotation,
                spacing,
                0);

            rootGhost.transform.position = firstPosition;
            rootGhost.transform.rotation = _gridCenterRotation;

            _rootGridPositionValid =
                basePlacementIsValid &&
                ValidatePlantPosition(
                    selectedPiece,
                    plant,
                    firstPosition);

            rootPiece.SetInvalidPlacementHeightlight(
                fillCount <= 0 ||
                !_rootGridPositionValid);

            if (configuredCount <= 1 ||
                ShowGridPreview == null ||
                !ShowGridPreview.Value)
            {
                DestroyPreviewGhosts();
                return;
            }

            EnsurePreviewGhosts(rootGhost, configuredCount);

            for (int index = 0; index < PreviewGhosts.Count; index++)
            {
                GameObject preview = PreviewGhosts[index];
                if (preview == null)
                {
                    continue;
                }

                preview.SetActive(rootIsVisible);
                if (!rootIsVisible)
                {
                    continue;
                }

                int gridIndex = index + 1;
                Vector3 position = GetGridPosition(
                    _gridCenterPosition,
                    _gridCenterRotation,
                    spacing,
                    gridIndex);

                preview.transform.position = position;
                preview.transform.rotation = _gridCenterRotation;

                bool positionIsValid =
                    basePlacementIsValid &&
                    ValidatePlantPosition(
                        selectedPiece,
                        plant,
                        position);

                bool willBeFilled = gridIndex < fillCount;
                SetPreviewInvalid(
                    preview,
                    !willBeFilled || !positionIsValid);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool Player_TryPlacePiece_Prefix(
            Player __instance,
            Piece piece,
            ref bool __result)
        {
            if (!IsLocalGridPlant(__instance, piece))
            {
                return true;
            }

            int resourceCount = GetResourceLimitedGridCount(
                __instance,
                piece,
                GetGridCount());

            if (resourceCount <= 0)
            {
                __instance.Message(
                    MessageHud.MessageType.Center,
                    "$msg_missingrequirement");

                __result = false;
                return false;
            }

            if (_hasGridCenter && !_rootGridPositionValid)
            {
                __instance.Message(
                    MessageHud.MessageType.Center,
                    "$msg_invalidplacement");

                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        private static void Player_PlacePiece_Postfix(
            Player __instance,
            Piece piece,
            Vector3 pos,
            Quaternion rot)
        {
            if (_placingExtraPlants ||
                !IsLocalGridPlant(__instance, piece))
            {
                return;
            }

            Piece? selectedPiece =
                __instance.m_buildPieces == null
                    ? null
                    : __instance.m_buildPieces.GetSelectedPiece();

            if (selectedPiece == null ||
                !IsLocalGridPlant(__instance, selectedPiece))
            {
                return;
            }

            int count = GetResourceLimitedGridCount(
                __instance,
                selectedPiece,
                GetGridCount());

            if (count <= 1)
            {
                return;
            }

            Plant plant = selectedPiece.GetComponent<Plant>();
            float spacing = GetGridSpacing(plant);
            int placedExtras = 0;

            bool freeBuild =
                ZoneSystem.instance != null &&
                ZoneSystem.instance.GetGlobalKey(
                    selectedPiece.FreeBuildKey());

            _placingExtraPlants = true;

            try
            {
                for (int index = 1; index < count; index++)
                {
                    Vector3 position = GetGridPositionFromFirstPlant(
                        pos,
                        rot,
                        spacing,
                        index);

                    if (!ValidatePlantPosition(
                            selectedPiece,
                            plant,
                            position))
                    {
                        continue;
                    }

                    if (!freeBuild)
                    {
                        int remainingPlants =
                            GetResourceLimitedGridCount(
                                __instance,
                                selectedPiece,
                                2);

                        if (remainingPlants < 2 ||
                            !ConsumeOnePlant(
                                __instance,
                                selectedPiece))
                        {
                            break;
                        }
                    }

                    if (!CreateExtraPlant(
                        __instance,
                        selectedPiece,
                        position,
                        rot))
                    {
                        break;
                    }

                    placedExtras++;
                }
            }
            finally
            {
                _placingExtraPlants = false;
            }

            _hasGridCenter = false;

            if (placedExtras <= 0)
            {
                return;
            }

            if (ConsumeStamina != null &&
                ConsumeStamina.Value)
            {
                float staminaCostPerExtraPlant =
                    ExtraPlantStaminaCost == null
                        ? 2f
                        : Mathf.Clamp(
                            ExtraPlantStaminaCost.Value,
                            0f,
                            100f);

                __instance.UseStamina(
                    staminaCostPerExtraPlant *
                    placedExtras);
            }

            ItemDrop.ItemData rightItem = __instance.GetRightItem();
            if (rightItem != null &&
                rightItem.m_shared.m_useDurability)
            {
                rightItem.m_durability -=
                    GetPlaceDurability(__instance, rightItem) *
                    placedExtras;
            }

            if (placedExtras + 1 < count)
            {
                __instance.Message(
                    MessageHud.MessageType.TopLeft,
                    "Plant grid placed " +
                    (placedExtras + 1) +
                    " of " +
                    count +
                    " plants because some positions were blocked.");
            }

            ValheimQoLPlugin.Log.LogInfo(
                "[PlantGrid] Planted " +
                (placedExtras + 1) +
                " of " +
                count +
                " available grid position(s). NoCostCheat=" +
                __instance.NoCostCheat() +
                ", FreeBuildWorldModifier=" +
                freeBuild +
                ", FirstPlant=" +
                pos +
                ", Spacing=" +
                spacing +
                ", ConsumeStamina=" +
                (ConsumeStamina != null &&
                 ConsumeStamina.Value) +
                ", ExtraPlantStaminaCost=" +
                (ExtraPlantStaminaCost == null
                    ? 2f
                    : ExtraPlantStaminaCost.Value) +
                ".");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Player_UpdatePlacementGhost_Postfix(
            Player __instance)
        {
            if (__instance == Player.m_localPlayer)
            {
                UpdatePreviewGhosts(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(InventoryGui),
            nameof(InventoryGui.SetupRequirement))]
        private static void InventoryGui_SetupRequirement_Postfix(
            Transform elementRoot,
            Piece.Requirement req,
            Player player,
            bool craft,
            int quality,
            ref bool __result)
        {
            if (!__result ||
                craft ||
                ShowTotalCost == null ||
                !ShowTotalCost.Value ||
                player == null ||
                player.m_buildPieces == null)
            {
                return;
            }

            Piece selectedPiece = player.m_buildPieces.GetSelectedPiece();
            if (!IsLocalGridPlant(player, selectedPiece))
            {
                return;
            }

            int count = GetResourceLimitedGridCount(
                player,
                selectedPiece,
                GetGridCount());
            if (count <= 1 || req.m_resItem == null)
            {
                return;
            }

            TMP_Text? amountText =
                elementRoot.transform.Find("res_amount")?.GetComponent<TMP_Text>();

            if (amountText == null)
            {
                return;
            }

            int totalAmount = req.GetAmount(quality) * count;
            amountText.text = totalAmount.ToString();

            string sharedName = req.m_resItem.m_itemData.m_shared.m_name;
            int available = NearbyContainerManager.CountAvailableForPiece(
                player,
                selectedPiece,
                sharedName);

            amountText.color = available < totalAmount &&
                               ZoneSystem.instance != null &&
                               !ZoneSystem.instance.GetGlobalKey(
                                   selectedPiece.FreeBuildKey())
                ? ((Mathf.Sin(Time.time * 10f) > 0f)
                    ? Color.red
                    : Color.white)
                : Color.white;
        }
    }
}
