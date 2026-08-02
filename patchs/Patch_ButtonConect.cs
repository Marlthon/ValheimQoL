using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimQoL
{
    [HarmonyPatch]
    internal static class MenuQuickJoin
    {
        private static ConfigEntry<bool> Enabled = null!;
        private static ConfigEntry<string> ButtonText = null!;
        private static ConfigEntry<string> ServerAddress = null!;
        private static ConfigEntry<int> ServerPort = null!;
        private static ConfigEntry<string> ServerPassword = null!;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            Enabled = plugin.config(
                "QuickConnect",
                "Enabled",
                false,
                "Adds a direct-connect button to the main menu. Example: true displays the button; false keeps the vanilla menu unchanged.",
                false);

            ButtonText = plugin.config(
                "QuickConnect",
                "ButtonText",
                "My Server",
                "Sets the text shown on the direct-connect button. Example: My Community Server.",
                false);

            ServerAddress = plugin.config(
                "QuickConnect",
                "ServerAddress",
                "127.0.0.1",
                "Sets the server IP address or domain name. Example: 192.168.1.50 or play.example.com.",
                false);

            ServerPort = plugin.config(
                "QuickConnect",
                "ServerPort",
                2456,
                "Sets the main Valheim server port. Example: 2456 is the standard game port.",
                false);

            ServerPassword = plugin.config(
                "QuickConnect",
                "ServerPassword",
                "",
                "Sets the server password. Example: Viking123. Leave empty for no password. Warning: this value is stored as plain text in the cfg file.",
                false);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.SetupGui))]
        private static void SetupGui_Postfix(FejdStartup __instance)
        {
            if (!Enabled.Value)
            {
                return;
            }

            TryCreateButton(__instance);
        }

        private static void TryCreateButton(FejdStartup startup)
        {
            if (startup == null || startup.m_menuList == null)
            {
                return;
            }

            Button[] existingButtons = startup.m_menuList.GetComponentsInChildren<Button>(true);
            if (existingButtons.Any(button =>
                    button != null &&
                    string.Equals(button.name, "ValheimQoL_QuickJoin", StringComparison.Ordinal)))
            {
                return;
            }

            Button template = existingButtons.FirstOrDefault(button => button != null);
            if (template == null || template.transform.parent == null)
            {
                ValheimQoLPlugin.Log.LogWarning(
                    "[QuickConnect] Nenhum botão vanilla foi encontrado para servir como modelo.");
                return;
            }

            GameObject buttonObject = UnityEngine.Object.Instantiate(
                template.gameObject,
                template.transform.parent,
                false);

            buttonObject.name = "ValheimQoL_QuickJoin";
            buttonObject.transform.SetSiblingIndex(0);
            SetButtonText(buttonObject, ButtonText.Value);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(buttonObject);
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Connect);

            Button[] menuButtons = startup.m_menuButtons ?? Array.Empty<Button>();
            if (!menuButtons.Contains(button))
            {
                startup.m_menuButtons = new[] { button }.Concat(menuButtons).ToArray();
            }
        }

        private static void Connect()
        {
            FejdStartup startup = FejdStartup.m_instance;
            if (startup == null)
            {
                return;
            }

            string address = ServerAddress.Value == null
                ? string.Empty
                : ServerAddress.Value.Trim();

            int port = Mathf.Clamp(ServerPort.Value, 1, 65535);
            if (string.IsNullOrEmpty(address))
            {
                ValheimQoLPlugin.Log.LogWarning(
                    "[QuickConnect] ServerAddress está vazio no arquivo de configuração.");
                return;
            }

            FejdStartup.ServerPassword = ServerPassword.Value ?? string.Empty;
            startup.m_queuedJoinServer = new ServerJoinData(
                new ServerJoinDataDedicated(address + ":" + port));

            startup.HideAll();
            startup.ShowCharacterSelection();
        }

        private static void SetButtonText(GameObject buttonObject, string text)
        {
            string finalText = string.IsNullOrWhiteSpace(text)
                ? "Meu Servidor"
                : text.Trim();

            Text[] legacyTexts = buttonObject.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < legacyTexts.Length; index++)
            {
                if (legacyTexts[index] != null)
                {
                    legacyTexts[index].text = finalText;
                }
            }

            TMP_Text[] tmpTexts = buttonObject.GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < tmpTexts.Length; index++)
            {
                if (tmpTexts[index] != null)
                {
                    tmpTexts[index].text = finalText;
                }
            }
        }
    }
}
