using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace TraderGen.Client.Patches
{
    internal static class TraderCompoundItemPatch
    {
        internal static ManualLogSource Log;

        internal static void Init(ManualLogSource log) => Log = log;

        private static readonly FieldInfo BarterSchemeDictField = typeof(Assortment)
            .GetField("_schemes", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo CloneDictField = typeof(Assortment)
            .GetField("_clones", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Assortment.GetSchemeForItem returns null for non-empty CompoundItems
        // because GClass3750.IsExchangeable rejects them. This patch bypasses that check and
        // looks up the barter scheme directly so backpacks/rigs with items can be purchased.
        [HarmonyPatch(typeof(Assortment), "GetSchemeForItem")]
        internal static class GetSchemeForItemPatch
        {
            static bool Prefix(Item item, Assortment __instance, ref BarterScheme __result)
            {
                if (item == null || !(item is CompoundItem))
                {
                    return true;
                }

                var dict = BarterSchemeDictField?.GetValue(__instance) as Dictionary<string, BarterScheme>;
                if (dict != null && dict.TryGetValue(item.Id, out var scheme))
                {
                    __result = scheme;
                    return false;
                }

                return true;
            }
        }

        // Same fix for GetSchemeForClone
        [HarmonyPatch(typeof(Assortment), "GetSchemeForClone")]
        internal static class GetSchemeForClonePatch
        {
            static bool Prefix(Item item, Assortment __instance, ref BarterScheme __result)
            {
                if (item == null || !(item is CompoundItem))
                {
                    return true;
                }

                var cloneDict = CloneDictField?.GetValue(__instance) as Dictionary<Item, Item>;
                if (cloneDict == null || !cloneDict.TryGetValue(item, out var originalItem))
                {
                    return true;
                }

                var dict = BarterSchemeDictField?.GetValue(__instance) as Dictionary<string, BarterScheme>;
                if (dict != null && dict.TryGetValue(originalItem.Id, out var scheme))
                {
                    __result = scheme;
                    return false;
                }

                return true;
            }
        }

    }
}
