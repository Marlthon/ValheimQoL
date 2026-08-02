using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine;
using TMPro;
using System.Linq;

namespace ValheimQoL
{
    [HarmonyPatch]
    public static class Patch_Clock
    {
        private static ConfigEntry<bool> ClockEnabled = null!;
        private static ConfigEntry<float> ClockUpdateInterval = null!;
        private static ConfigEntry<int> ClockFontSize = null!;
        private static ConfigEntry<bool> ShowRealTime = null!;
        private static ConfigEntry<bool> RealTimeUse24H = null!;
        private static ConfigEntry<int> RealTimeFontSize = null!;

        private static float _nextUpdateTime;

        private static GameObject _clockRoot = null!;
        private static TextMeshProUGUI _timeText = null!;
        private static TextMeshProUGUI _realText = null!;

        // offsets e tamanhos padrão
        private const float ROOT_OFFSET_Y = -60f;
        private const float ROOT_WIDTH = 600f;
        private const float ROOT_HEIGHT = 120f;

        public static void InitConfig(ValheimQoLPlugin plugin)
        {
            ClockEnabled = plugin.config("Clock", "ClockEnabled", true, "Shows the in-game day and time on the HUD. Example: false completely hides the custom clock.");
            ClockUpdateInterval = plugin.config("Clock", "ClockUpdateInterval", 1f, "Sets how often the clock refreshes, in seconds. Example: 0.5 updates twice per second; 2 updates every two seconds.");
            ClockFontSize = plugin.config("Clock", "ClockFontSize", 40, "Sets the font size of the in-game clock. Example: 32 produces smaller text; 48 produces larger text.");
            ShowRealTime = plugin.config("Clock", "ShowRealTime", true, "Shows the computer's local time below the in-game clock. Example: false displays only the Valheim time.");
            RealTimeUse24H = plugin.config("Clock", "RealTimeUse24H", true, "Selects the real-time format. Example: true displays 18:30; false displays 06:30 PM.");
            RealTimeFontSize = plugin.config("Clock", "RealTimeFontSize", 28, "Sets the font size of the real-world clock. Example: 20 makes the secondary clock less prominent.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
        private static void Hud_Update_Postfix(Hud __instance)
        {
            if (!ClockEnabled.Value)
            {
                if (_clockRoot != null) Object.Destroy(_clockRoot);
                return;
            }

            if (_clockRoot == null)
                CreateClock(__instance);

            if (Time.unscaledTime < _nextUpdateTime)
                return;

            _nextUpdateTime = Time.unscaledTime + Mathf.Max(0.2f, ClockUpdateInterval.Value);
            UpdateClockTexts();
        }

        private static void CreateClock(Hud hud)
        {
            _clockRoot = new GameObject("MarlthonClockRoot");
            _clockRoot.transform.SetParent(hud.m_rootObject.transform, false);

            var rect = _clockRoot.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, ROOT_OFFSET_Y);
            rect.sizeDelta = new Vector2(ROOT_WIDTH, ROOT_HEIGHT);

            var norse = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(f => f.name.ToLower().Contains("norsebold"))
                ?? TMP_Settings.defaultFontAsset;

            // Texto da hora do jogo (centro)
            _timeText = CreateTMP("ClockTime", norse, ClockFontSize.Value, new Color(1f, 0.85f, 0.2f));
            _timeText.alignment = TextAlignmentOptions.Center;
            _timeText.transform.SetParent(_clockRoot.transform, false);
            _timeText.rectTransform.anchoredPosition = new Vector2(0, 10f);

            // Texto da hora real (abaixo)
            _realText = CreateTMP("ClockReal", norse, RealTimeFontSize.Value, new Color(0.8f, 0.8f, 0.8f));
            _realText.alignment = TextAlignmentOptions.Center;
            _realText.transform.SetParent(_clockRoot.transform, false);
            _realText.rectTransform.anchoredPosition = new Vector2(0, -30f);
        }

        private static TextMeshProUGUI CreateTMP(string name, TMP_FontAsset font, int size, Color color)
        {
            var go = new GameObject(name);
            go.SetActive(false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font ?? TMP_Settings.defaultFontAsset;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.richText = true;

            var mat = new Material(tmp.fontSharedMaterial);
            mat.SetFloat("_OutlineWidth", 0.1f);
            mat.SetColor("_OutlineColor", new Color(0, 0, 0, 0.6f));
            tmp.fontSharedMaterial = mat;

            go.SetActive(true);
            return tmp;
        }

        private static void UpdateClockTexts()
        {
            if (EnvMan.instance == null) return;

            // hora do jogo
            float day = EnvMan.instance.GetDay();
            float f = EnvMan.instance.GetDayFraction();
            int hh = Mathf.FloorToInt(f * 24f);
            int mm = Mathf.FloorToInt((f * 24f - hh) * 60f);

            _timeText.text = $"Day {day} - {hh:00}:{mm:00}";

            if (ShowRealTime.Value)
            {
                var now = System.DateTime.Now;
                string real = RealTimeUse24H.Value ? now.ToString("HH:mm") : now.ToString("hh:mm tt");
                _realText.text = $"<color=#CCCCCC>Real Life: {real}</color>";
            }
            else
            {
                _realText.text = "";
            }
        }
    }
}
