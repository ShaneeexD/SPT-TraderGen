using System.Text.Json;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using TraderGen.Models;

namespace TraderGen.Services;

// Manages rotating quest generation, persistence, and expiration.
// Generated quests are cached to disk and reused across server restarts.
// Expired quests are regenerated when the server starts.
public static class RotatingQuestService
{
    private const string CacheFileName = "rotating_quests_cache.json";

    private static readonly Random Rng = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // Resolve rotating quest templates into concrete StoryQuestDefinitions.
    // Uses cached quests if still valid, regenerates expired ones, generates new ones as needed.
    // Returns the concrete quests ready for QuestBuilder.
    public static List<StoryQuestDefinition> ResolveRotatingQuests(
        List<RotatingQuestTemplate> templates,
        string traderId,
        string modPath,
        ISptLogger<TraderGenPlugin> logger)
    {
        var cachePath = Path.Combine(modPath, CacheFileName);
        var cache = LoadCache(cachePath);
        var now = DateTime.UtcNow;
        var results = new List<StoryQuestDefinition>();
        var cacheModified = false;

        foreach (var template in templates)
        {
            // Find existing cached quests for this template
            var cached = cache.ActiveQuests
                .Where(q => q.TemplateId == template.Id && q.TraderId == traderId)
                .ToList();

            // Separate valid and expired
            var valid = cached.Where(q => q.ExpiresAtUtc > now).ToList();
            var expired = cached.Where(q => q.ExpiresAtUtc <= now).ToList();

            // Remove expired entries
            if (expired.Count > 0)
            {
                foreach (var exp in expired)
                {
                    cache.ActiveQuests.Remove(exp);
                    logger.LogWithColor(
                        $"[TraderGen] Rotating quest '{exp.Quest.Name}' (template: {template.TemplateName}) expired. Regenerating.",
                        LogTextColor.Yellow);
                }
                cacheModified = true;
            }

            // How many new quests do we need?
            var needed = template.QuestCount - valid.Count;

            if (needed > 0)
            {
                for (var i = 0; i < needed; i++)
                {
                    var generated = GenerateConcreteQuest(template, traderId);
                    var expiration = CalculateExpiration(template.Rotation, now);

                    var entry = new CachedRotatingQuest
                    {
                        TemplateId = template.Id,
                        TraderId = traderId,
                        Rotation = template.Rotation,
                        GeneratedAtUtc = now,
                        ExpiresAtUtc = expiration,
                        Quest = generated,
                    };

                    cache.ActiveQuests.Add(entry);
                    cacheModified = true;

                    logger.LogWithColor(
                        $"[TraderGen] Generated rotating quest '{generated.Name}' " +
                        $"(template: {template.TemplateName}, expires: {expiration:yyyy-MM-dd HH:mm} UTC)",
                        LogTextColor.Green);
                }
            }
            else if (valid.Count > 0)
            {
                var nextExpiry = valid.Min(q => q.ExpiresAtUtc);
                logger.LogWithColor(
                    $"[TraderGen] Reusing {valid.Count} cached quest(s) for template '{template.TemplateName}' " +
                    $"(next expiry: {nextExpiry:yyyy-MM-dd HH:mm} UTC)",
                    LogTextColor.Cyan);
            }

            // Collect all valid quests (existing + newly generated) for this template
            results.AddRange(cache.ActiveQuests
                .Where(q => q.TemplateId == template.Id && q.TraderId == traderId && q.ExpiresAtUtc > now)
                .Select(q => q.Quest));
        }

        // Save cache if modified
        if (cacheModified)
        {
            SaveCache(cachePath, cache);
        }

        return results;
    }

    // Generate a concrete StoryQuestDefinition from a rotating template.
    private static StoryQuestDefinition GenerateConcreteQuest(RotatingQuestTemplate template, string traderId)
    {
        var questId = GenerateId();
        var name = template.NamePool[Rng.Next(template.NamePool.Count)];
        var description = template.DescriptionPool.Count > 0
            ? template.DescriptionPool[Rng.Next(template.DescriptionPool.Count)]
            : $"Complete the assigned {template.Rotation} task.";

        // Build concrete objectives
        var objectives = new List<QuestObjective>();
        var totalCount = 0;
        var pickedLocation = "";

        foreach (var objTemplate in template.Objectives)
        {
            var count = Rng.Next(objTemplate.CountRange.Min, objTemplate.CountRange.Max + 1);
            totalCount += count;

            var location = objTemplate.LocationPool.Count > 0
                ? objTemplate.LocationPool[Rng.Next(objTemplate.LocationPool.Count)]
                : null;
            if (!string.IsNullOrWhiteSpace(location))
                pickedLocation = location;

            var obj = new QuestObjective
            {
                Type = objTemplate.Type,
                Count = count,
                Location = location,
            };

            switch (objTemplate.Type.ToLowerInvariant())
            {
                case "kill_enemy":
                    obj.Target = objTemplate.TargetPool.Count > 0
                        ? objTemplate.TargetPool[Rng.Next(objTemplate.TargetPool.Count)]
                        : "Savage";
                    break;
                case "handover_item":
                case "handover_fir_item":
                    obj.ItemTpl = objTemplate.ItemPool.Count > 0
                        ? objTemplate.ItemPool[Rng.Next(objTemplate.ItemPool.Count)]
                        : null;
                    break;
            }

            objectives.Add(obj);
        }

        // Apply {location} placeholder using display names
        var locationDisplay = !string.IsNullOrWhiteSpace(pickedLocation)
            ? LocationHelper.ToDisplayName(pickedLocation)
            : "Tarkov";
        name = name.Replace("{location}", locationDisplay);
        description = description.Replace("{location}", locationDisplay);

        // Calculate scaled rewards
        var scaling = template.RewardScaling;
        var rewards = new QuestRewards
        {
            Xp = scaling.XpPerObjectiveCount * totalCount,
            Money = new MoneyReward
            {
                Currency = scaling.Currency,
                Amount = scaling.BaseMoney + (scaling.MoneyPerObjectiveCount * totalCount),
            },
            TraderStanding = scaling.Standing,
        };

        return new StoryQuestDefinition
        {
            Id = questId,
            TraderId = traderId,
            Name = name,
            Description = description,
            Location = !string.IsNullOrWhiteSpace(pickedLocation) ? pickedLocation : "any",
            Requirements = new QuestRequirements { PlayerLevel = 1 },
            Objectives = objectives,
            Rewards = rewards,
        };
    }

    // Calculate expiration based on rotation type.
    // Daily: expires at the next midnight UTC.
    // Weekly: expires at the next Monday midnight UTC.
    private static DateTime CalculateExpiration(string rotation, DateTime now)
    {
        return rotation.ToLowerInvariant() switch
        {
            "daily" => now.Date.AddDays(1),
            "weekly" => now.Date.AddDays(((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7 + (now.DayOfWeek == DayOfWeek.Monday ? 7 : 0)),
            _ => now.Date.AddDays(1),
        };
    }

    private static RotatingQuestCache LoadCache(string path)
    {
        if (!File.Exists(path))
            return new RotatingQuestCache();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RotatingQuestCache>(json, JsonOptions) ?? new RotatingQuestCache();
        }
        catch
        {
            return new RotatingQuestCache();
        }
    }

    private static void SaveCache(string path, RotatingQuestCache cache)
    {
        var json = JsonSerializer.Serialize(cache, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GenerateId()
    {
        var bytes = new byte[12];
        Rng.NextBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
