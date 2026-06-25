using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;

namespace TraderGen.Services;

// Restores the correct custom pocket TPL after profile load.
[Injectable(TypePriority = OnLoadOrder.SaveCallbacks + 1)]
public class PocketRestoreService(
    ISptLogger<PocketRestoreService> logger,
    SaveServer saveServer,
    ModHelper modHelper
) : IOnLoad
{
    public async Task OnLoad()
    {
        try
        {
            var questPocketMap = BuildMap();
            if (questPocketMap.Count == 0) return;

            var profiles = saveServer.GetProfiles();
            foreach (var (_, profile) in profiles)
            {
                var pmc = profile?.CharacterData?.PmcData;
                if (pmc?.Quests == null || pmc.Inventory?.Items == null) continue;

                string? correctTpl = null;
                foreach (var quest in pmc.Quests)
                {
                    if (quest.Status != QuestStatusEnum.Success) continue;
                    if (questPocketMap.TryGetValue(quest.QId.ToString(), out var tpl))
                        correctTpl = tpl;
                }
                if (correctTpl == null) continue;

                foreach (var item in pmc.Inventory.Items.Where(i => i.SlotId == "Pockets"))
                {
                    if (item.Template.ToString() != correctTpl)
                        item.Template = new MongoId(correctTpl);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[TraderGen] PocketRestoreService error: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    private Dictionary<string, string> BuildMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            if (string.IsNullOrEmpty(modPath)) return result;
            var tradersDir = Path.Combine(modPath, "traders");
            if (!Directory.Exists(tradersDir)) return result;

            foreach (var questFile in Directory.EnumerateFiles(tradersDir, "quests.json", SearchOption.AllDirectories))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(questFile));
                    if (!doc.RootElement.TryGetProperty("storyQuests", out var sq)) continue;
                    foreach (var quest in sq.EnumerateArray())
                    {
                        if (!quest.TryGetProperty("id", out var idEl)) continue;
                        if (!quest.TryGetProperty("rewards", out var rw)) continue;
                        if (!rw.TryGetProperty("customPocket", out var cp)) continue;
                        if (!cp.TryGetProperty("slots", out var slots)) continue;
                        var sb = new StringBuilder();
                        foreach (var s in slots.EnumerateArray())
                        {
                            var w = s.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
                            var h = s.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0;
                            sb.Append(w).Append('x').Append(h).Append(',');
                        }
                        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("tradergen_pocket_" + sb));
                        result[idEl.GetString()!] = Convert.ToHexStringLower(hash[..12]);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { logger.Error($"[TraderGen] PocketRestoreService.BuildMap: {ex.Message}"); }
        return result;
    }
}

// Restores the correct custom pocket TPL before every profile save.
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
public class PocketGuardService(
    ISptLogger<PocketGuardService> logger,
    SaveServer saveServer,
    ModHelper modHelper
) : IOnLoad
{
    private const string DefaultPocketTpl = "627a4e6b255f7527fb05a0f6";
    private const string CallbackId = "TraderGen_PocketGuard";

    // questId -> correctTpl, built once at startup
    private Dictionary<string, string>? _questPocketMap;

    public async Task OnLoad()
    {
        _questPocketMap = BuildQuestPocketRewardMap();
        if (_questPocketMap.Count == 0) return;

#pragma warning disable CS0618
        saveServer.AddBeforeSaveCallback(CallbackId, GuardProfile);
#pragma warning restore CS0618
        await Task.CompletedTask;
    }

    private SptProfile GuardProfile(SptProfile profile)
    {
        try
        {
            var pmc = profile?.CharacterData?.PmcData;
            if (pmc?.Quests == null || pmc.Inventory?.Items == null || _questPocketMap == null)
                return profile!;

            string? correctTpl = null;
            foreach (var quest in pmc.Quests)
            {
                if (quest.Status != QuestStatusEnum.Success) continue;
                if (_questPocketMap.TryGetValue(quest.QId.ToString(), out var tpl))
                    correctTpl = tpl;
            }

            if (correctTpl == null) return profile!;

            foreach (var item in pmc.Inventory.Items.Where(i => i.SlotId == "Pockets"))
            {
                if (item.Template.ToString() != correctTpl)
                    item.Template = new MongoId(correctTpl);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[TraderGen] PocketGuard error: {ex.Message}");
        }

        return profile!;
    }

    private Dictionary<string, string> BuildQuestPocketRewardMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
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

                        var templateId = GenerateDeterministicId(layoutKey);
                        result[idProp.GetString()!] = templateId;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[TraderGen] PocketGuard failed to build quest map: {ex.Message}");
        }

        return result;
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
