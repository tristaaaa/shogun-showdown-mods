using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoinMultiplier
{
    [BepInPlugin("com.flargington.shogunshowdown.coinmultiplier", "Coin Multiplier", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const float MinMultiplier = 1f;
        public const float MaxMultiplier = 3f;

        internal static ConfigEntry<float> Multiplier;
        internal static ManualLogSource Log;

        // Fractional part of the multiplier carried between pickups, so 1.5x averages to exactly 1.5x
        private static float carry;

        private void Awake()
        {
            Log = Logger;
            Multiplier = Config.Bind("General", "CoinMultiplier", 1.5f,
                new ConfigDescription("Multiplier applied to every coin picked up",
                    new AcceptableValueRange<float>(MinMultiplier, MaxMultiplier)));

            new Harmony("com.flargington.shogunshowdown.coinmultiplier").PatchAll();
            Logger.LogInfo($"Coin Multiplier loaded (x{Multiplier.Value:0.00})");
        }

        internal static int ExtraCoins(int baseValue)
        {
            carry += baseValue * (Multiplier.Value - 1f);
            int extra = Mathf.FloorToInt(carry);
            carry -= extra;
            return extra;
        }
    }

    // Fallback slider, only created if the in-game slider cannot be added. It is disabled whenever it is not
    // showing, since any active OnGUI makes Unity run its IMGUI pass every frame.
    // Lives on its own hidden GameObject: GameStateResetter destroys every object FindObjectsOfType
    // returns (including the BepInEx manager), and HideAndDontSave objects are excluded from that
    internal class CoinMultiplierGUI : MonoBehaviour
    {
        private static CoinMultiplierGUI instance;

        private Rect windowRect = new Rect(20f, 20f, 320f, 110f);

        internal static void SetVisible(bool visible)
        {
            if (instance == null)
            {
                if (!visible)
                {
                    return;
                }
                GameObject go = new GameObject("CoinMultiplierGUI");
                go.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CoinMultiplierGUI>();
                // Leaving a run from the pause menu loads a new scene without calling ResumeGame
                SceneManager.sceneLoaded += (scene, mode) => SetVisible(false);
            }
            instance.enabled = visible;
        }

        private void OnGUI()
        {
            // Scale the panel with screen height so it stays readable on Retina displays
            float scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            windowRect = GUI.Window(0x5C01, windowRect, DrawWindow, "Coin Multiplier");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label($"Coin drops: x{Plugin.Multiplier.Value:0.00}");
            float value = GUILayout.HorizontalSlider(Plugin.Multiplier.Value, Plugin.MinMultiplier, Plugin.MaxMultiplier);
            // Snap to the same 0.1 steps as the in-game slider
            value = Mathf.Round(value * 10f) / 10f;
            if (!Mathf.Approximately(value, Plugin.Multiplier.Value))
            {
                Plugin.Multiplier.Value = value;
            }
            GUILayout.Space(6f);
            GUI.DragWindow();
        }
    }

    [HarmonyPatch(typeof(CoinPickup), "PickUpEffect")]
    internal static class CoinPickupPatch
    {
        private static void Postfix(CoinPickup __instance)
        {
            int extra = Plugin.ExtraCoins(__instance.value);
            if (extra > 0)
            {
                Globals.Coins += extra;
            }
        }
    }

    [HarmonyPatch(typeof(PauseMenu), "PauseGame")]
    internal static class PauseGamePatch
    {
        // Runs before PauseGame enables the menu page, so the page picks the new item up as a navigation target.
        // __state carries whether the in-game slider is in place over to the postfix.
        private static void Prefix(PauseMenu __instance, out bool __state)
        {
            try
            {
                __state = MenuItemInjector.EnsureInjected(__instance);
            }
            catch (Exception e)
            {
                __state = false;
                Plugin.Log.LogError($"Could not add the slider to the pause menu, using overlay slider: {e}");
            }
        }

        // PauseGame does nothing when the menu object is inactive, so only show the overlay if time actually stopped
        private static void Postfix(bool __state)
        {
            CoinMultiplierGUI.SetVisible(!__state && Time.timeScale == 0f);
        }
    }

    [HarmonyPatch(typeof(PauseMenu), "ResumeGame")]
    internal static class ResumeGamePatch
    {
        private static void Postfix()
        {
            CoinMultiplierGUI.SetVisible(false);
        }
    }
}
