using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace DevMode
{
    [BepInPlugin("com.flargington.shogunshowdown.devmode", "Dev Mode", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ConfigEntry<bool> enabled = Config.Bind("General", "Enabled", true,
                "Turns on the game's built-in developer mode (hotkeys and the C panel)");
            if (!enabled.Value)
            {
                Logger.LogInfo("Dev Mode disabled in config");
                return;
            }

            Harmony harmony = new Harmony("com.flargington.shogunshowdown.devmode");
            harmony.CreateClassProcessor(typeof(ResetDeveloperUtilsPatch)).Patch();

            // get_Developer just reads a static readonly field, so Mono can inline it or fold it into a
            // constant when callers are compiled. Setting the field before any game code is JIT-compiled
            // covers both cases. Patching the getter only helps if the field cannot be found, since inlined
            // callers never call the getter.
            FieldInfo field = AccessTools.Field(typeof(Globals), "<Developer>k__BackingField");
            if (field != null)
            {
                field.SetValue(null, true);
            }
            else
            {
                Logger.LogWarning("Globals.Developer backing field not found (the game may have been updated); patching the getter instead");
                harmony.CreateClassProcessor(typeof(DeveloperGetterPatch)).Patch();
            }

            Logger.LogInfo($"Dev Mode loaded (Globals.Developer = {Globals.Developer})");
        }
    }

    [HarmonyPatch(typeof(Globals), nameof(Globals.Developer), MethodType.Getter)]
    internal static class DeveloperGetterPatch
    {
        private static void Postfix(ref bool __result)
        {
            __result = true;
        }
    }

    // The shipped DeveloperUtils asset may still hold the developers' own editor toggles, which
    // only took effect while Developer was true. Clear them so dev mode starts as a normal run.
    [HarmonyPatch(typeof(Globals), nameof(Globals.SetDeveloperUtils))]
    internal static class ResetDeveloperUtilsPatch
    {
        private static void Prefix(DeveloperUtils developerUtils)
        {
            if (developerUtils == null)
            {
                return;
            }
            developerUtils._quick = false;
            developerUtils._skipTitleScreen = false;
            developerUtils._shortLocations = false;
            developerUtils._invulnerable = false;
            developerUtils._customLocation = false;
            developerUtils._customHero = false;
            developerUtils._customLoadout = false;
            developerUtils._customDay = false;
        }
    }
}
