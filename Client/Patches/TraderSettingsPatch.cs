using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using EFT;
using HarmonyLib;

namespace TraderGen.Client.Patches
{
    // Adds custom TraderGen traders to the client's GlobalConfiguration.TradersSettings dictionary.
    //
    // The client populates TradersSettings at runtime via UpdateTradersSettings(), which is called
    // by SPT's client patches when the global config is received from the server. Custom traders
    // are not included in the vanilla global config, so they're missing from the dictionary.
    //
    // This causes KeyNotFoundException crashes when the client tries to look up a custom trader's
    // settings — e.g. in the messenger (SocialNetwork.GetDialogueHeaderByType), quest reward views,
    // quest requirement views, insurance display, and trader dialog.
    //
    // This patch reads trader IDs and avatar paths from the TraderGen server mod's trader pack
    // JSON files and adds minimal TraderSettings entries to the dictionary.
    internal static class TraderSettingsPatch
    {
        internal static ManualLogSource Log;

        // List of custom traders found in the TraderGen traders folder.
        // Populated by Init() at plugin load time.
        private static readonly List<(string Id, string AvatarPath)> CustomTraders = new();

        internal static void Init(ManualLogSource log)
        {
            Log = log;
            ScanTraderPacks();
        }

        // Scans the TraderGen server mod's traders/ folder for trader.json files and extracts
        // the trader ID and avatar path from each one.
        private static void ScanTraderPacks()
        {
            try
            {
                // Find the SPT root directory. In a standard SPT install, the game executable
                // is at the root, and user/mods/TraderGen/traders/ contains the trader packs.
                var sptRoot = AppDomain.CurrentDomain.BaseDirectory;
                var tradersFolder = Path.Combine(sptRoot, "user", "mods", "TraderGen", "traders");

                if (!Directory.Exists(tradersFolder))
                {
                    Log?.LogInfo($"[TraderGen] Trader packs folder not found at {tradersFolder} — skipping TraderSettings patch.");
                    return;
                }

                var idRegex = new Regex(@"""id""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                var avatarRegex = new Regex(@"""avatar""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);

                foreach (var dir in Directory.GetDirectories(tradersFolder))
                {
                    var traderJsonPath = Path.Combine(dir, "trader.json");
                    if (!File.Exists(traderJsonPath))
                        continue;

                    try
                    {
                        var content = File.ReadAllText(traderJsonPath);
                        var idMatch = idRegex.Match(content);
                        var avatarMatch = avatarRegex.Match(content);

                        if (!idMatch.Success)
                            continue;

                        var traderId = idMatch.Groups[1].Value;
                        var avatarPath = avatarMatch.Success ? avatarMatch.Groups[1].Value : null;

                        // Build the avatar URL in the same format the server uses:
                        // /files/trader/avatar/{traderId}
                        var avatarUrl = $"/files/trader/avatar/{traderId}";

                        CustomTraders.Add((traderId, avatarUrl));
                        Log?.LogInfo($"[TraderGen] Found custom trader '{traderId}' in {Path.GetFileName(dir)}");
                    }
                    catch (Exception ex)
                    {
                        Log?.LogWarning($"[TraderGen] Failed to read trader.json in {Path.GetFileName(dir)}: {ex.Message}");
                    }
                }

                Log?.LogInfo($"[TraderGen] Found {CustomTraders.Count} custom trader(s) to register in TradersSettings.");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[TraderGen] Error scanning trader packs: {ex}");
            }
        }

        // Harmony Postfix for GlobalConfiguration.UpdateTradersSettings.
        // After the vanilla traders are added, add our custom traders too.
        [HarmonyPatch(typeof(GlobalConfiguration), nameof(GlobalConfiguration.UpdateTradersSettings))]
        internal static class UpdateTradersSettingsPatch
        {
            static void Postfix(GlobalConfiguration __instance)
            {
                try
                {
                    if (CustomTraders.Count == 0)
                        return;

                    var tradersSettings = __instance.TradersSettings;
                    var added = 0;

                    foreach (var (id, avatarUrl) in CustomTraders)
                    {
                        if (tradersSettings.ContainsKey(id))
                            continue; // Already present (vanilla or added by another mod)

                        // Create a minimal TraderSettings with just the fields needed for
                        // messenger headers, quest reward views, and quest requirement views.
                        // The Nickname/FirstName/FullName/Description/Location properties are
                        // all computed from Id, so locale lookups work automatically.
                        var settings = new GlobalConfiguration.TraderSettings
                        {
                            Id = id,
                            AvatarURL = avatarUrl,
                        };

                        // Set LoyaltyLevels to an empty array to prevent NullReferenceException
                        // in GetLoyaltyLevel() if any code path calls it.
                        settings.LoyaltyLevels = Array.Empty<GlobalConfiguration.TraderLoyaltyLevel>();

                        tradersSettings[id] = settings;
                        added++;
                    }

                    if (added > 0)
                    {
                        Log?.LogInfo($"[TraderGen] Added {added} custom trader(s) to GlobalConfiguration.TradersSettings.");
                    }
                }
                catch (Exception ex)
                {
                    Log?.LogError($"[TraderGen] Failed to add custom traders to TradersSettings: {ex}");
                }
            }
        }
    }
}
