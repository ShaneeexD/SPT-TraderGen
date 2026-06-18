using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace TraderGen.Client.Patches
{
    internal static class TraderPricePatches
    {
        internal static ManualLogSource Log;

        internal static void Init(ManualLogSource log) => Log = log;

        // Keep the Convert patch to reduce log noise from vanilla zero-price items
        [HarmonyPatch(typeof(Convert), nameof(Convert.ToInt32), new[] { typeof(double) })]
        internal static class ConvertToInt32Patch
        {
            static bool Prefix(double value, ref int __result)
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value > int.MaxValue || value < int.MinValue)
                {
                    Log?.LogDebug($"[TraderGen] Convert.ToInt32 suppressed bad value ({value}), returning 1.");
                    __result = 1;
                    return false;
                }
                return true;
            }
        }

        // Log when the trading screen opens so we know which trader is being viewed
        [HarmonyPatch]
        internal static class TradingScreenShowPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("EFT.UI.TraderScreensGroup") ?? AccessTools.TypeByName("EFT.UI.TradingScreen");
                if (type == null) return null;
                var method = AccessTools.Method(type, "Show");
                if (method == null)
                    method = AccessTools.Method(type, "Awake");
                return method;
            }

            static void Postfix(object __instance)
            {
                try
                {
                    var traderProp = AccessTools.Property(__instance.GetType(), "Trader");
                    var trader = traderProp?.GetValue(__instance);
                    if (trader == null)
                    {
                        Log?.LogInfo("[TraderGen] Trading screen opened (trader unknown).");
                        return;
                    }

                    var nicknameField = AccessTools.Property(trader.GetType(), "Nickname");
                    var idField = AccessTools.Property(trader.GetType(), "Id");
                    var baseProp = AccessTools.Property(trader.GetType(), "Base");
                    var nickname = nicknameField?.GetValue(trader)?.ToString() ?? "unknown";
                    var id = idField?.GetValue(trader)?.ToString() ?? "unknown";

                    var buyCategories = Array.Empty<string>();
                    var baseObj = baseProp?.GetValue(trader);
                    if (baseObj != null)
                    {
                        var itemsBuyProp = AccessTools.Property(baseObj.GetType(), "ItemsBuy");
                        var itemsBuy = itemsBuyProp?.GetValue(baseObj);
                        if (itemsBuy != null)
                        {
                            var catProp = AccessTools.Property(itemsBuy.GetType(), "Category");
                            var cats = catProp?.GetValue(itemsBuy) as System.Collections.IEnumerable;
                            if (cats != null)
                            {
                                buyCategories = cats.Cast<object>().Select(c => c?.ToString() ?? "null").ToArray();
                            }
                        }
                    }

                    Log?.LogInfo($"[TraderGen] Trading screen opened for '{nickname}' ({id}). Buy categories: {string.Join(", ", buyCategories)}");
                }
                catch (Exception ex)
                {
                    Log?.LogDebug($"[TraderGen] TradingScreenShowPostfix failed: {ex}");
                }
            }
        }

        // Log every item the grid tries to render in the sell tab so we can see what causes the bug
        [HarmonyPatch]
        internal static class GridViewMethod4Patch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("EFT.UI.DragAndDrop.GridView");
                if (type == null) return null;
                // method_4 is the one that creates item views for the grid
                var method = AccessTools.Method(type, "method_4");
                return method;
            }

            static void Prefix(Item item)
            {
                try
                {
                    if (item == null) return;
                    var parentId = item.Template?.ParentId ?? "no-parent";
                    Log?.LogDebug($"[TraderGen] GridView rendering item: '{item.Name?.Localized()}' tpl={item.TemplateId} parent={parentId}");
                }
                catch { }
            }

            static Exception Finalizer(Exception __exception, Item item)
            {
                if (__exception != null && item != null)
                {
                    try
                    {
                        Log?.LogWarning($"[TraderGen] GridView EXCEPTION for item '{item.Name?.Localized()}' ({item.TemplateId}): {__exception.GetType().Name}: {__exception.Message}");
                    }
                    catch { }
                }
                return __exception;
            }
        }

        // Log exceptions in TradingPlayerItemView to catch UI crashes per-item
        [HarmonyPatch]
        internal static class TradingPlayerItemViewCtorPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("EFT.UI.DragAndDrop.TradingPlayerItemView");
                if (type == null) return null;
                var method = AccessTools.Method(type, "Create");
                return method;
            }

            static void Prefix(Item item, object trader)
            {
                try
                {
                    if (item == null) return;
                    var traderName = AccessTools.Property(trader?.GetType(), "Nickname")?.GetValue(trader)?.ToString() ?? "unknown";
                    Log?.LogDebug($"[TraderGen] TradingPlayerItemView.Create for item '{item.Name?.Localized()}' ({item.TemplateId}), trader={traderName}");
                }
                catch { }
            }

            static Exception Finalizer(Exception __exception, Item item)
            {
                if (__exception != null && item != null)
                {
                    try
                    {
                        Log?.LogWarning($"[TraderGen] TradingPlayerItemView EXCEPTION for item '{item.Name?.Localized()}' ({item.TemplateId}): {__exception.GetType().Name}: {__exception.Message}");
                    }
                    catch { }
                }
                return __exception;
            }
        }
    }
}
