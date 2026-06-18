using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace TraderGen.Client.Patches
{
    internal static class TraderCompoundItemPatch
    {
        internal static ManualLogSource Log;

        internal static void Init(ManualLogSource log) => Log = log;

        private static readonly FieldInfo BarterSchemeDictField = typeof(TraderAssortmentControllerClass)
            .GetField("Dictionary_1", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo CloneDictField = typeof(TraderAssortmentControllerClass)
            .GetField("Dictionary_0", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // TraderAssortmentControllerClass.GetSchemeForItem returns null for non-empty CompoundItems
        // because GClass3750.IsExchangeable rejects them. This patch bypasses that check and
        // looks up the barter scheme directly so backpacks/rigs with items can be purchased.
        [HarmonyPatch(typeof(TraderAssortmentControllerClass), "GetSchemeForItem")]
        internal static class GetSchemeForItemPatch
        {
            static bool Prefix(Item item, TraderAssortmentControllerClass __instance, ref BarterScheme __result)
            {
                if (item == null)
                {
                    __result = null;
                    return false;
                }

                var dict = BarterSchemeDictField?.GetValue(__instance) as Dictionary<string, BarterScheme>;
                if (dict != null && dict.TryGetValue(item.Id, out var scheme))
                {
                    __result = scheme;
                    return false;
                }

                __result = null;
                return false;
            }
        }

        // Same fix for GetSchemeForClone
        [HarmonyPatch(typeof(TraderAssortmentControllerClass), "GetSchemeForClone")]
        internal static class GetSchemeForClonePatch
        {
            static bool Prefix(Item item, TraderAssortmentControllerClass __instance, ref BarterScheme __result)
            {
                if (item == null)
                {
                    __result = null;
                    return false;
                }

                var cloneDict = CloneDictField?.GetValue(__instance) as Dictionary<Item, Item>;
                if (cloneDict == null || !cloneDict.TryGetValue(item, out var originalItem))
                {
                    __result = null;
                    return false;
                }

                var dict = BarterSchemeDictField?.GetValue(__instance) as Dictionary<string, BarterScheme>;
                if (dict != null && dict.TryGetValue(originalItem.Id, out var scheme))
                {
                    __result = scheme;
                    return false;
                }

                __result = null;
                return false;
            }
        }
    }
}
