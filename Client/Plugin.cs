using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TraderGen.Client.Patches;

namespace TraderGen.Client
{
    [BepInPlugin("com.tradergen.client", "TraderGen Client", "1.7.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<bool> EnableExportButton { get; private set; }

        private void Awake()
        {
            try
            {
                EnableExportButton = Config.Bind(
                    "TraderGen",
                    "EnableExportButton",
                    false,
                    "Show the 'Export to TG' button in the stash item context menu.");

                TraderPricePatches.Init(Logger);
                WeaponBuildExportPatch.Init(Logger);
                TraderCompoundItemPatch.Init(Logger);
                QuestPocketRewardPatch.Init(Logger);
                var harmony = new Harmony("com.tradergen.client");
                harmony.PatchAll();
                Logger.LogInfo("[TraderGen] Client patch loaded.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TraderGen] Client patch failed to load: {ex}");
            }
        }
    }
}
