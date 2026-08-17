using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TraderGen.Client.Patches
{
    // Replaces the icon and name for "Random Item" placeholder quest rewards.
    //
    // The server uses a custom cloned item (based on AI-2 medkit) as the placeholder
    // template for random item pool rewards. The actual item is rolled from the pool
    // at quest completion by RandomItemPoolPatch on the server. While the quest is in
    // progress (and on the quest completion screen), the client shows this placeholder.
    // This patch overrides the vanilla icon with randomitem.png and the name with
    // "Random Item".
    //
    // The icon load for cloned items is async — ItemViewFactory.LoadItemIcon returns
    // an ItemIcon with a null Sprite, and the sprite is set later via the OnIconChanged
    // callback. So we patch both Show (for the name + sync icon path) and OnIconChanged
    // (to override the sprite when the async load completes).
    internal static class RandomItemIconPatch
    {
        // Must match RandomItemPlaceholder.PlaceholderTplId in the server project.
        private const string PlaceholderTplId = "6988f0a1c0ffee1234567890";

        internal static ManualLogSource Log;

        private static Sprite _randomItemSprite;
        private static bool _loaded;

        // Tracks which ItemWideView instances are currently showing the placeholder.
        // ConditionalWeakTable avoids memory leaks — entries are GC'd with the key.
        private static readonly ConditionalWeakTable<ItemWideView, object> _placeholderViews = new();

        internal static void Init(ManualLogSource log)
        {
            Log = log;
            LoadSprite();
        }

        private static void LoadSprite()
        {
            try
            {
                var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (pluginDir == null)
                {
                    Log?.LogWarning("[TraderGen] Could not determine plugin directory for randomitem.png");
                    return;
                }

                var pngPath = Path.Combine(pluginDir, "randomitem.png");
                if (!File.Exists(pngPath))
                {
                    Log?.LogWarning($"[TraderGen] randomitem.png not found at {pngPath}");
                    return;
                }

                var bytes = File.ReadAllBytes(pngPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes))
                {
                    Log?.LogWarning("[TraderGen] Failed to load randomitem.png as Texture2D");
                    return;
                }

                _randomItemSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );

                _loaded = true;
                Log?.LogInfo($"[TraderGen] Loaded random item icon ({texture.width}x{texture.height}) from {pngPath}");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[TraderGen] Error loading randomitem.png: {ex}");
            }
        }

        private static void ApplyIcon(Image mainImage)
        {
            if (_loaded && _randomItemSprite != null)
            {
                mainImage.sprite = _randomItemSprite;
                mainImage.gameObject.SetActive(true);
            }
        }

        // Postfix on ItemWideView.Show(QuestReward) — overrides the name text and
        // registers the view for icon override. Also sets the icon immediately in
        // case the sprite was already loaded synchronously.
        [HarmonyPatch(typeof(ItemWideView), nameof(ItemWideView.Show), typeof(QuestReward))]
        internal static class ItemWideViewShowPatch
        {
            static void Postfix(ItemWideView __instance, QuestReward reward)
            {
                try
                {
                    var item = reward.GetItem();
                    if (item == null)
                        return;

                    if (item.StringTemplateId != PlaceholderTplId)
                        return;

                    // Register this view so OnIconChanged can override the sprite
                    // when the async icon load completes.
                    _placeholderViews.GetValue(__instance, _ => new object());

                    // Override icon immediately (works if sprite was loaded synchronously)
                    ApplyIcon(__instance.MainImage);

                    // Override the name text to "Random Item"
                    var nameField = typeof(ItemWideView).GetField("_name", BindingFlags.Public | BindingFlags.Instance);
                    if (nameField?.GetValue(__instance) is TMP_Text nameText)
                        nameText.text = "Random Item";

                    // Clear the count text — the actual count is unknown until the roll
                    var countField = typeof(ItemWideView).GetField("_count", BindingFlags.Public | BindingFlags.Instance);
                    if (countField?.GetValue(__instance) is TMP_Text countText)
                        countText.text = "";
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"[TraderGen] Random item Show patch failed: {ex.Message}");
                }
            }
        }

        // Postfix on ItemIconView.OnIconChanged — fires when the async icon load
        // completes. If this view is registered as showing the placeholder, override
        // the sprite that the vanilla code just set.
        [HarmonyPatch(typeof(ItemIconView), nameof(ItemIconView.OnIconChanged))]
        internal static class ItemIconChangedPatch
        {
            static void Postfix(ItemIconView __instance)
            {
                try
                {
                    if (__instance is ItemWideView wideView && _placeholderViews.TryGetValue(wideView, out _))
                    {
                        ApplyIcon(__instance.MainImage);
                    }
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"[TraderGen] Random item OnIconChanged patch failed: {ex.Message}");
                }
            }
        }
    }
}
