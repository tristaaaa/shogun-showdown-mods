using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace WeaponCooldowns
{
    [BepInPlugin("com.flargington.shogunshowdown.weaponcooldowns", "Weapon Cooldowns", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static readonly Dictionary<Type, ConfigEntry<int>> Cooldowns = new Dictionary<Type, ConfigEntry<int>>();

        private void Awake()
        {
            int gameMaxCooldown = Attack.maxCooldown;
            ConfigEntry<int> maxCooldown = Config.Bind("General", "MaxCooldown", gameMaxCooldown,
                new ConfigDescription($"Highest cooldown any weapon can have, including after upgrades. Weapons above it are lowered to it, and upgrades that would raise a weapon past it are not offered. Game default: {gameMaxCooldown}",
                    new AcceptableValueRange<int>(0, gameMaxCooldown)));
            // A plain static field that the Cooldown setter and the upgrade checks read every time
            Attack.maxCooldown = maxCooldown.Value;

            IEnumerable<Type> attackTypes = typeof(Attack).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Attack)) && !t.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (Type type in attackTypes)
            {
                int gameDefault;
                try
                {
                    // InitialCooldown is a constant getter, so an instance that skipped its constructor can
                    // still report it. This reads each weapon's default from the game instead of a copied table.
                    gameDefault = ((Attack)FormatterServices.GetUninitializedObject(type)).InitialCooldown;
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Could not read the default cooldown of {type.Name}, skipping it: {e.Message}");
                    continue;
                }

                string weapon = type.Name.EndsWith("Attack") ? type.Name.Substring(0, type.Name.Length - "Attack".Length) : type.Name;
                Cooldowns[type] = Config.Bind("Cooldowns", weapon, gameDefault,
                    new ConfigDescription($"Starting cooldown in turns. Game default: {gameDefault}",
                        new AcceptableValueRange<int>(0, gameMaxCooldown)));
            }

            new Harmony("com.flargington.shogunshowdown.weaponcooldowns").PatchAll();
            int changed = Cooldowns.Count(pair => pair.Value.Value != (int)pair.Value.DefaultValue);
            Logger.LogInfo($"Weapon Cooldowns loaded ({Cooldowns.Count} weapons, {changed} changed from the game default, max cooldown {Attack.maxCooldown})");
        }
    }

    // Initialize is where every new tile gets Cooldown = InitialCooldown. Upgrades then adjust from this
    // value, and tiles restored from a saved run keep the cooldown that was saved with them.
    [HarmonyPatch(typeof(Attack), nameof(Attack.Initialize))]
    internal static class AttackInitializePatch
    {
        private static void Postfix(Attack __instance)
        {
            if (Plugin.Cooldowns.TryGetValue(__instance.GetType(), out ConfigEntry<int> cooldown))
            {
                __instance.Cooldown = cooldown.Value;
            }
        }
    }
}
