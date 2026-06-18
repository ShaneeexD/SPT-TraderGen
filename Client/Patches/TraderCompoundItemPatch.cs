using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
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

        private static readonly FieldInfo TradeModeField = typeof(TradingItemView)
            .GetField("etradeMode_0", BindingFlags.NonPublic | BindingFlags.Instance);

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

        // TradingItemView.method_39 greys out (CanvasGroup.alpha = 0.3) any CompoundItem
        // that has items in its grids. This prevents backpacks/rigs with contents from
        // appearing buyable in the trader grid even though the scheme lookup works.
        // We skip this for purchase mode items owned by a trader.
        [HarmonyPatch(typeof(TradingItemView), "method_39")]
        internal static class Method39Patch
        {
            static bool Prefix(TradingItemView __instance)
            {
                var tradeMode = (ETradeMode?)TradeModeField?.GetValue(__instance);
                if (tradeMode == ETradeMode.Purchase)
                {
                    return false; // skip original method_39 entirely
                }
                return true;
            }
        }
    }
}
