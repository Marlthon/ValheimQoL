using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using System;
using System.IO;

namespace ValheimQoL
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public sealed class ValheimQoLPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ValheimQoL";
        internal const string ModVersion = "0.0.1";
        internal const string Author = "marlthon";
        internal const string ModGUID = Author + "." + ModName;

        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath =
            Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        private readonly Harmony _harmony = new Harmony(ModGUID);
        private FileSystemWatcher? _configWatcher;

        internal static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new ConfigSync(ModGUID)
        {
            DisplayName = ModName,
            CurrentVersion = ModVersion,
            MinimumRequiredVersion = ModVersion,
            ModRequired = true
        };

        private static ConfigEntry<bool> _serverConfigLocked = null!;

        internal static bool IsLocalPlayerAdmin()
        {
            return ConfigSync != null && ConfigSync.IsAdmin;
        }

        private void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Locks synchronized gameplay settings to the server. Example: when true, clients use the server's DayNight and WearNTear values instead of their local values.");

            ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            Patch_Base.InitConfig(this);
            Patch_BulkHarvest.InitConfig(this);
            MenuQuickJoin.InitConfig(this);
            Patch_Clock.InitConfig(this);
            Patch_Container.InitConfig(this);
            Patch_DayNight.InitConfig(this);
            Patch_DeleteItem.InitConfig(this);
            Patch_HoverTimers.InitConfig(this);
            Patch_InfiniteFuel.InitConfig(this);
            Patch_ItemBalance.InitConfig(this);
            Patch_MapShare.InitConfig(this);
            NearbyContainerManager.InitConfig(this);
            Patch_PlantGrid.InitConfig(this);
            Patch_PortalProtection.InitConfig(this);
            Patch_Server.InitConfig(this);
            Patch_Smelter.InitConfig(this);
            Patch_ServerQoLFeatures.InitConfig(this);
            Patch_SwimmingEquipment.InitConfig(this);
            Patch_WearNTear.InitConfig(this);
            Patch_Workbench.InitConfig(this);

            _harmony.PatchAll();
            Patch_DayNight.LogHarmonyPatchStatus();
            SetupConfigWatcher();

            Log.LogInfo(ModName + " " + ModVersion + " carregado.");
        }

        private void OnDestroy()
        {
            if (_configWatcher != null)
            {
                _configWatcher.Changed -= ReadConfigValues;
                _configWatcher.Created -= ReadConfigValues;
                _configWatcher.Renamed -= ReadConfigValues;
                _configWatcher.Dispose();
                _configWatcher = null;
            }

            Patch_InfiniteFuel.Shutdown();
            Patch_ServerQoLFeatures.Shutdown();
            Patch_Server.Shutdown();
            _harmony.UnpatchSelf();
        }

        private void SetupConfigWatcher()
        {
            string configDirectory = Path.GetDirectoryName(ConfigFileFullPath);
            string configName = Path.GetFileName(ConfigFileFullPath);

            if (string.IsNullOrEmpty(configDirectory) || string.IsNullOrEmpty(configName))
            {
                return;
            }

            _configWatcher = new FileSystemWatcher(configDirectory, configName)
            {
                IncludeSubdirectories = false,
                SynchronizingObject = ThreadingHelper.SynchronizingObject,
                EnableRaisingEvents = true
            };

            _configWatcher.Changed += ReadConfigValues;
            _configWatcher.Created += ReadConfigValues;
            _configWatcher.Renamed += ReadConfigValues;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath))
            {
                return;
            }

            try
            {
                Config.Reload();
            }
            catch (Exception exception)
            {
                Log.LogError("Falha ao recarregar a configuração: " + exception);
            }
        }

        public ConfigEntry<T> config<T>(
            string group,
            string name,
            T value,
            ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription = new ConfigDescription(
                description.Description +
                (synchronizedSetting
                    ? " [Synced with Server]"
                    : " [Not Synced with Server]"),
                description.AcceptableValues,
                new ConfigurationManagerAttributes());

            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
            return configEntry;
        }

        public ConfigEntry<T> config<T>(
            string group,
            string name,
            T value,
            string description,
            bool synchronizedSetting = true)
        {
            return config(
                group,
                name,
                value,
                new ConfigDescription(description),
                synchronizedSetting);
        }

        private sealed class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }
    }
}
