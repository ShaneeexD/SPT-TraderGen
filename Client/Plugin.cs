using System;
using BepInEx;
using HarmonyLib;
using TraderGen.Client.Patches;

namespace TraderGen.Client
{
    [BepInPlugin("com.tradergen.client", "TraderGen Client", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                TraderPricePatches.Init(Logger);
                WeaponBuildExportPatch.Init(Logger);
                TraderCompoundItemPatch.Init(Logger);
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
