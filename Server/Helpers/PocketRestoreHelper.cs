using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace TraderGen.Helpers;

/// <summary>
/// Shared helper for determining the correct TraderGen custom pocket template ID
/// for a profile and restoring the pocket TPL on the PmcData.
/// </summary>
public static class PocketRestoreHelper
{
    /// <summary>
    /// Builds a questId -> custom pocket template ID map from the TraderGen quest packs.
    /// </summary>
    public static Dictionary<string, string> BuildQuestPocketMap(string modPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(modPath)) return result;

        var tradersDir = Path.Combine(modPath, "traders");
        if (!Directory.Exists(tradersDir)) return result;

        foreach (var questFile in Directory.EnumerateFiles(tradersDir, "quests.json", SearchOption.AllDirectories))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(questFile));
                if (!doc.RootElement.TryGetProperty("storyQuests", out var storyQuests)) continue;

                foreach (var quest in storyQuests.EnumerateArray())
                {
                    if (!quest.TryGetProperty("id", out var idProp)) continue;
                    if (!quest.TryGetProperty("rewards", out var rewards)) continue;
                    if (!rewards.TryGetProperty("customPocket", out var customPocket)) continue;
                    if (!customPocket.TryGetProperty("slots", out var slots)) continue;

                    var layoutKey = ComputeLayoutKey(slots);
                    if (string.IsNullOrEmpty(layoutKey)) continue;

                    result[idProp.GetString()!] = GenerateDeterministicId(layoutKey);
                }
            }
            catch { }
        }

        return result;
    }

    /// <summary>
    /// Returns the correct custom pocket template ID for a PMC profile based on the
    /// most recently completed TraderGen pocket-reward quest.
    /// </summary>
    public static string? GetCorrectPocketTpl(PmcData pmc, Dictionary<string, string> questPocketMap)
    {
        if (pmc?.Quests == null || questPocketMap.Count == 0) return null;

        string? correctTpl = null;
        foreach (var quest in pmc.Quests)
        {
            if (quest.Status != QuestStatusEnum.Success) continue;
            if (questPocketMap.TryGetValue(quest.QId.ToString(), out var tpl))
                correctTpl = tpl;
        }

        return correctTpl;
    }

    /// <summary>
    /// Restores every pocket item in the PMC profile to the correct custom template ID.
    /// </summary>
    /// <returns>True if any pocket was changed.</returns>
    public static bool RestorePockets(PmcData pmc, Dictionary<string, string> questPocketMap)
    {
        if (pmc?.Inventory?.Items == null) return false;

        var correctTpl = GetCorrectPocketTpl(pmc, questPocketMap);
        if (correctTpl == null) return false;

        var changed = false;
        foreach (var item in pmc.Inventory.Items.Where(i => i.SlotId == "Pockets"))
        {
            if (item.Template.ToString() != correctTpl)
            {
                item.Template = new MongoId(correctTpl);
                changed = true;
            }
        }

        return changed;
    }

    private static string ComputeLayoutKey(JsonElement slots)
    {
        var sb = new StringBuilder();
        foreach (var slot in slots.EnumerateArray())
        {
            var w = slot.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
            var h = slot.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0;
            sb.Append(w).Append('x').Append(h).Append(',');
        }
        return sb.ToString();
    }

    private static string GenerateDeterministicId(string layoutKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("tradergen_pocket_" + layoutKey));
        return Convert.ToHexStringLower(hash[..12]);
    }
}
