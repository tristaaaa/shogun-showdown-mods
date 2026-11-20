using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Performance
{
    [BepInPlugin("com.flargington.shogunshowdown.performance", "Performance", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource log;
        private static ConfigEntry<bool> reduceLogging;
        private static ConfigEntry<int> frameRateCap;
        private static ConfigEntry<bool> disableAntiAliasing;

        private void Awake()
        {
            log = Logger;
            reduceLogging = Config.Bind("General", "ReduceLogging", true,
                "Stop the game writing ordinary Debug.Log messages to Player.log. Warnings and errors are still logged.");
            frameRateCap = Config.Bind("General", "FrameRateCap", 120,
                "Maximum frames per second, 0 for no cap. Only applies while VSync is Off in the game's options; with VSync on, the display's refresh rate sets the limit.");
            disableAntiAliasing = Config.Bind("General", "DisableAntiAliasing", true,
                "Turn off MSAA in the quality settings and URP asset, and post-process anti-aliasing on every camera.");

            if (reduceLogging.Value)
            {
                // Exceptions are always let through by Unity's logger regardless of this filter
                Debug.unityLogger.filterLogType = LogType.Warning;
            }
            if (frameRateCap.Value > 0)
            {
                Application.targetFrameRate = frameRateCap.Value;
            }
            if (disableAntiAliasing.Value)
            {
                DisablePipelineAntiAliasing();
                // Static handler, so it keeps working after GameStateResetter destroys this plugin's GameObject
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            log.LogInfo($"Performance loaded (ReduceLogging={reduceLogging.Value}, FrameRateCap={frameRateCap.Value}, DisableAntiAliasing={disableAntiAliasing.Value})");
        }

        private static void DisablePipelineAntiAliasing()
        {
            int qualityMsaa = QualitySettings.antiAliasing;
            QualitySettings.antiAliasing = 0;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                log.LogInfo($"MSAA was {qualityMsaa}x in quality settings, {urp.msaaSampleCount}x in the URP asset; setting both to off");
                urp.msaaSampleCount = 1;
            }
            else
            {
                log.LogInfo($"MSAA was {qualityMsaa}x in quality settings; setting it to off (no URP asset found)");
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            int changed = 0;
            foreach (Camera camera in Camera.allCameras)
            {
                bool hadAntiAliasing = camera.allowMSAA;
                camera.allowMSAA = false;
                UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data != null && data.antialiasing != AntialiasingMode.None)
                {
                    hadAntiAliasing = true;
                    data.antialiasing = AntialiasingMode.None;
                }
                if (hadAntiAliasing)
                {
                    changed++;
                }
            }
            if (changed > 0)
            {
                log.LogInfo($"Scene '{scene.name}': turned off anti-aliasing on {changed} camera(s)");
            }
        }
    }
}
