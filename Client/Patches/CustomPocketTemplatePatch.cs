using System;
using System.Reflection;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;

namespace TraderGen.Client.Patches
{
    /// <summary>
    /// Prevents custom TraderGen pocket template IDs from being silently dropped during
    /// client-side inventory deserialization.
    ///
    /// Root cause: ItemFactoryClass.method_7 is used as a delegate filter inside
    /// method_0/FlatItemsToTree. It returns false for any item whose _tpl is not in
    /// ItemTemplates, causing the item to be silently skipped. When the pocket item is
    /// dropped, EFT spawns a new default-pocket item and syncs it back to the server,
    /// overwriting the custom pocket TPL with the default one.
    ///
    /// Fix: Prefix on method_7 (applied manually via reflection since HarmonyPatch
    /// attribute cannot target compiler-generated method names). If the _tpl is not in
    /// ItemTemplates but slotId is "Pockets", remap _tpl to the default pocket TPL so
    /// method_7 returns true and the pocket item survives deserialization.
    ///
    /// Safety net: Prefix on CreateItem for the binary deserialization path.
    /// </summary>
    internal static class CustomPocketTemplatePatch
    {
        private const string DefaultPocketTpl = "627a4e6b255f7527fb05a0f6";

        internal static ManualLogSource Log;

        internal static void Init(ManualLogSource log) => Log = log;

        internal static void Apply(Harmony harmony)
        {
            // Patch method_7 by name — it's a compiler-generated instance method
            var method7 = typeof(ItemFactoryClass).GetMethod("method_7",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (method7 == null)
            {
                Log?.LogError("[TraderGen] Could not find ItemFactoryClass.method_7 — pocket persistence patch NOT applied.");
            }
            else
            {
                harmony.Patch(method7,
                    prefix: new HarmonyMethod(typeof(CustomPocketTemplatePatch), nameof(Method7Prefix)));
            }

            // Safety net: patch CreateItem for binary deserialization path
            var createItem = typeof(ItemFactoryClass).GetMethod(
                nameof(ItemFactoryClass.CreateItem),
                new[] { typeof(string), typeof(string), typeof(GClass846) });

            if (createItem != null)
            {
                harmony.Patch(createItem,
                    prefix: new HarmonyMethod(typeof(CustomPocketTemplatePatch), nameof(CreateItemPrefix)));
            }
        }

        static void Method7Prefix(FlatItemsDataClass x, ItemFactoryClass __instance)
        {
            try
            {
                if (x == null) return;
                if (x.slotId != "Pockets") return;
                if (__instance.ItemTemplates.ContainsKey(x._tpl)) return;

                var customTpl = (string)x._tpl;

                // Clone the default pocket template and register under the custom ID
                ItemTemplate defaultTpl;
                if (!__instance.ItemTemplates.TryGetValue(new MongoID(DefaultPocketTpl), out defaultTpl) || defaultTpl == null)
                {
                    return;
                }

                var cloned = CloneWithNewId(defaultTpl, customTpl);
                __instance.ItemTemplates[new MongoID(customTpl)] = cloned;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[TraderGen] Method7Prefix error: {ex.Message}");
            }
        }

        private static ItemTemplate CloneWithNewId(ItemTemplate source, string newId)
        {
            // Use Newtonsoft JSON round-trip to deep clone — available in the game
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            var clone = Newtonsoft.Json.JsonConvert.DeserializeObject<ItemTemplate>(json);
            clone._id = new MongoID(newId);
            return clone;
        }

        static void CreateItemPrefix(ref string templateId, ItemFactoryClass __instance)
        {
            try
            {
                if (__instance.ItemTemplates.ContainsKey(templateId)) return;
                if (templateId == null || templateId.Length != 24) return;
                if (!IsHexString(templateId)) return;

                templateId = DefaultPocketTpl;
            }
            catch (Exception ex)
            {
                Log?.LogError($"[TraderGen] CreateItemPrefix error: {ex.Message}");
            }
        }

        private static bool IsHexString(string s)
        {
            foreach (var c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            return true;
        }
    }
}

