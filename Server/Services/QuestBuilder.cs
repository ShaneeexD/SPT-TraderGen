using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using TraderGen.Models;

namespace TraderGen.Services;

// Translates TraderGen quest definitions into BSG quest format.
public static class QuestBuilder
{
    private static readonly Random Rng = new();

    // Build quest files for a trader.
    public static int BuildQuestFiles(
        string traderId,
        List<StoryQuestDefinition> allStoryQuests,
        string outputBaseDir,
        string packFolder,
        string? defaultQuestIcon,
        ISptLogger<TraderGenPlugin> logger)
    {
        var traderDir = Path.Combine(outputBaseDir, traderId);

        // Create directory structure
        var questsDir = Path.Combine(traderDir, "Quests");
        var localesDir = Path.Combine(traderDir, "Locales");
        var assortDir = Path.Combine(traderDir, "QuestAssort");
        var imagesDir = Path.Combine(traderDir, "Images");
        Directory.CreateDirectory(questsDir);
        Directory.CreateDirectory(localesDir);
        Directory.CreateDirectory(assortDir);
        Directory.CreateDirectory(imagesDir);

        // Generate default quest icon
        var defaultIconPath = Path.Combine(imagesDir, "default_quest_icon.png");
        if (!File.Exists(defaultIconPath))
            GenerateDefaultQuestIcon(defaultIconPath);

        // Copy default icon if specified
        if (!string.IsNullOrWhiteSpace(defaultQuestIcon))
        {
            var sourceIcon = Path.Combine(packFolder, defaultQuestIcon);
            if (File.Exists(sourceIcon))
            {
                var destIcon = Path.Combine(imagesDir, Path.GetFileName(defaultQuestIcon));
                File.Copy(sourceIcon, destIcon, overwrite: true);
            }
        }

        var allQuests = new JsonObject();
        var allLocales = new JsonObject();
        var questAssortSuccess = new JsonObject();
        var count = 0;

        // Location locale entries
        allLocales["any Name"] = "Any location";
        allLocales["bigmap Name"] = "Customs";
        allLocales["factory4_day Name"] = "Factory (Day)";
        allLocales["factory4_night Name"] = "Factory (Night)";
        allLocales["Woods Name"] = "Woods";
        allLocales["Shoreline Name"] = "Shoreline";
        allLocales["Interchange Name"] = "Interchange";
        allLocales["Lighthouse Name"] = "Lighthouse";
        allLocales["Reserve Name"] = "Reserve";
        allLocales["RezervBase Name"] = "Reserve";
        allLocales["laboratory Name"] = "The Lab";
        allLocales["TarkovStreets Name"] = "Streets of Tarkov";
        allLocales["Sandbox Name"] = "Ground Zero";
        allLocales["sandbox_high Name"] = "Ground Zero (High Level)";

        foreach (var quest in allStoryQuests)
        {
            // Resolve quest icon
            var iconFileName = ResolveQuestIcon(quest, packFolder, defaultQuestIcon, imagesDir);
            var bsgQuest = BuildStoryQuest(quest, allLocales, iconFileName);
            allQuests[quest.Id] = bsgQuest;
            BuildQuestAssortUnlocks(quest.Id, quest.Rewards, questAssortSuccess);
            count++;
        }

        // Write files
        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };

        File.WriteAllText(
            Path.Combine(questsDir, "quests.json"),
            allQuests.ToJsonString(jsonOpts));

        File.WriteAllText(
            Path.Combine(localesDir, "en.json"),
            allLocales.ToJsonString(jsonOpts));

        var questAssortObj = new JsonObject
        {
            ["started"] = new JsonObject(),
            ["success"] = questAssortSuccess,
            ["fail"] = new JsonObject(),
        };
        File.WriteAllText(
            Path.Combine(assortDir, "questAssort.json"),
            questAssortObj.ToJsonString(jsonOpts));

        logger.LogWithColor(
            $"[TraderGen] Built {count} quest(s) for trader {traderId} → {traderDir}",
            LogTextColor.Green);

        return count;
    }

    // ==================== Story Quest Builder ====================

    private static JsonObject BuildStoryQuest(StoryQuestDefinition quest, JsonObject locales, string iconFileName)
    {
        var questId = quest.Id;

        // Build AvailableForStart conditions
        var startConditions = new JsonArray();

        // Player level requirement
        startConditions.Add(new JsonObject
        {
            ["compareMethod"] = ">=",
            ["conditionType"] = "Level",
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = DeriveStableId($"{questId}:start:level"),
            ["index"] = 0,
            ["parentId"] = "",
            ["value"] = quest.Requirements.PlayerLevel,
            ["visibilityConditions"] = new JsonArray(),
        });

        // Previous quest requirement
        if (!string.IsNullOrWhiteSpace(quest.Requirements.PreviousQuest))
        {
            startConditions.Add(new JsonObject
            {
                ["availableAfter"] = 0,
                ["conditionType"] = "Quest",
                ["dispersion"] = 0,
                ["dynamicLocale"] = false,
                ["globalQuestCounterId"] = "",
                ["id"] = DeriveStableId($"{questId}:start:quest"),
                ["index"] = 1,
                ["parentId"] = "",
                ["status"] = new JsonArray { 4 }, // 4 = Success
                ["target"] = quest.Requirements.PreviousQuest,
                ["visibilityConditions"] = new JsonArray(),
            });
        }

        // Build AvailableForFinish conditions (objectives)
        var finishConditions = new JsonArray();
        for (var i = 0; i < quest.Objectives.Count; i++)
        {
            var obj = quest.Objectives[i];
            var conditionNode = BuildObjectiveCondition(obj, i, locales, questId);
            finishConditions.Add(conditionNode);
        }

        // Build rewards
        var successRewards = BuildRewards(quest.Rewards, quest.TraderId);

        // Determine quest type based on objectives
        var questType = DetermineQuestType(quest.Objectives);

        // Build locales
        locales[$"{questId} name"] = quest.Name;
        locales[$"{questId} description"] = quest.Description;
        locales[$"{questId} successMessageText"] = quest.SuccessMessage;
        locales[$"{questId} startedMessageText"] = quest.StartedMessage;
        locales[$"{questId} acceptPlayerMessage"] = quest.StartedMessage;
        locales[$"{questId} completePlayerMessage"] = quest.SuccessMessage;

        // Build BSG quest object
        return new JsonObject
        {
            ["QuestName"] = quest.Name,
            ["_id"] = questId,
            ["traderId"] = quest.TraderId,
            ["type"] = questType,
            ["location"] = LocationHelper.ToQuestLocationId(quest.Location),
            ["image"] = $"/files/quest/icon/{iconFileName}",
            ["instantComplete"] = false,
            ["isKey"] = false,
            ["secretQuest"] = false,
            ["side"] = "Pmc",
            ["sideExclusive"] = "Pmc",
            ["description"] = $"{questId} description",
            ["startedMessageText"] = $"{questId} startedMessageText",
            ["successMessageText"] = $"{questId} successMessageText",
            ["failMessageText"] = $"{questId} failMessageText",
            ["acceptPlayerMessage"] = $"{questId} acceptPlayerMessage",
            ["declinePlayerMessage"] = $"{questId} declinePlayerMessage",
            ["completePlayerMessage"] = $"{questId} completePlayerMessage",
            ["changeQuestMessageText"] = $"{questId} changeQuestMessageText",
            ["name"] = $"{questId} name",
            ["note"] = $"{questId} note",
            ["restartable"] = false,
            ["canShowNotificationsInGame"] = true,
            ["conditions"] = new JsonObject
            {
                ["AvailableForStart"] = startConditions,
                ["AvailableForFinish"] = finishConditions,
                ["Fail"] = new JsonArray(),
            },
            ["rewards"] = new JsonObject
            {
                ["Started"] = new JsonArray(),
                ["Success"] = successRewards,
                ["Fail"] = new JsonArray(),
            },
        };
    }

    // Objective builders

    private static JsonNode BuildObjectiveCondition(QuestObjective obj, int index, JsonObject locales, string questId)
    {
        return obj.Type.ToLowerInvariant() switch
        {
            "handover_item" => BuildHandoverCondition(obj, index, false, locales, questId),
            "handover_fir_item" => BuildHandoverCondition(obj, index, true, locales, questId),
            "kill_enemy" => BuildKillCondition(obj, index, locales, questId),
            "survive_location" => BuildSurviveCondition(obj, index, locales, questId),
            "extract_location" => BuildExtractCondition(obj, index, locales, questId),
            _ => throw new InvalidOperationException($"Unknown objective type: {obj.Type}"),
        };
    }

    private static JsonObject BuildHandoverCondition(QuestObjective obj, int index, bool foundInRaid, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{index}:cond");

        // Build locale for this objective
        var firText = foundInRaid ? "found in raid " : "";
        var desc = obj.Description ?? $"Hand over {obj.Count} {firText}items ({obj.ItemTpl})";
        locales[condId] = desc;

        return new JsonObject
        {
            ["conditionType"] = "HandoverItem",
            ["dogtagLevel"] = 0,
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = condId,
            ["index"] = index,
            ["isEncoded"] = false,
            ["maxDurability"] = 100,
            ["minDurability"] = 0,
            ["onlyFoundInRaid"] = foundInRaid,
            ["parentId"] = "",
            ["target"] = new JsonArray { obj.ItemTpl! },
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildKillCondition(QuestObjective obj, int index, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{index}:cond");
        var killCondId = DeriveStableId($"{questId}:obj{index}:kill");
        var counterId = DeriveStableId($"{questId}:obj{index}:counter");

        // Determine target and savageRole
        var target = obj.Target ?? "Savage";
        var savageRole = new JsonArray();

        // Certain targets map to savageRole rather than the "target" field
        var bsgTarget = target switch
        {
            "exUsec" or "pmcBot" or "sectantPriest" or "sectantWarrior"
                or "bossKnight" or "bossBully" or "bossKilla" or "bossKojaniy"
                or "bossSanitar" or "bossTagilla" or "bossGluhar" or "bossZryachiy"
                or "bossBoar" or "bossPartisan" or "bossKolontay"
                => "Savage",
            _ => target,
        };

        if (bsgTarget == "Savage" && target != "Savage" && target != "Any")
        {
            savageRole.Add(target);
        }

        // Build the kill sub-condition with advanced fields applied on top of the BSG defaults.
        var killCond = new JsonObject
        {
            ["bodyPart"] = obj.BodyPart?.Count > 0
                ? new JsonArray(obj.BodyPart.Select(bp => (JsonNode)bp).ToArray())
                : new JsonArray(),
            ["compareMethod"] = ">=",
            ["conditionType"] = "Kills",
            ["daytime"] = (obj.TimeFrom.HasValue || obj.TimeTo.HasValue)
                ? new JsonObject { ["from"] = obj.TimeFrom ?? 0, ["to"] = obj.TimeTo ?? 0 }
                : new JsonObject { ["from"] = 0, ["to"] = 0 },
            ["distance"] = (obj.MinDistance.HasValue, obj.MaxDistance.HasValue) switch
            {
                (true, _) => new JsonObject { ["compareMethod"] = ">=", ["value"] = obj.MinDistance.Value },
                (false, true) => new JsonObject { ["compareMethod"] = "<=", ["value"] = obj.MaxDistance.Value },
                _ => new JsonObject { ["compareMethod"] = ">=", ["value"] = 0 },
            },
            ["dynamicLocale"] = false,
            ["enemyEquipmentExclusive"] = new JsonArray(),
            ["enemyEquipmentInclusive"] = new JsonArray(),
            ["enemyHealthEffects"] = new JsonArray(),
            ["id"] = killCondId,
            ["resetOnSessionEnd"] = false,
            ["savageRole"] = savageRole,
            ["target"] = bsgTarget,
            ["value"] = 1,
            ["weapon"] = obj.WeaponTpls?.Count > 0
                ? new JsonArray(obj.WeaponTpls.Select(w => (JsonNode)w).ToArray())
                : new JsonArray(),
            ["weaponCaliber"] = new JsonArray(),
            ["weaponModsInclusive"] = new JsonArray(),
            ["weaponModsExclusive"] = new JsonArray(),
        };

        var counterConditions = new JsonArray { killCond };

        // Add Equipment condition for player-worn item restrictions.
        // In BSG format, equipmentInclusive/equipmentExclusive live on a separate
        // "Equipment" counter condition, NOT on "Kills". Each inner array is an OR group.
        if (obj.Wearing?.Count > 0 || obj.NotWearing?.Count > 0)
        {
            var equipCondId = DeriveStableId($"{questId}:obj{index}:equip");
            var equipCond = new JsonObject
            {
                ["conditionType"] = "Equipment",
                ["dynamicLocale"] = false,
                ["id"] = equipCondId,
                ["IncludeNotEquippedItems"] = false,
            };
            if (obj.Wearing?.Count > 0)
            {
                equipCond["equipmentInclusive"] = new JsonArray(
                    obj.Wearing.Select(id => new JsonArray((JsonNode)id)).ToArray());
            }
            if (obj.NotWearing?.Count > 0)
            {
                equipCond["equipmentExclusive"] = new JsonArray(
                    obj.NotWearing.Select(id => new JsonArray((JsonNode)id)).ToArray());
            }
            counterConditions.Add(equipCond);
        }

        // Add location condition
        if (!string.IsNullOrWhiteSpace(obj.Location))
        {
            var locCondId = DeriveStableId($"{questId}:obj{index}:loc");
            counterConditions.Add(new JsonObject
            {
                ["conditionType"] = "Location",
                ["dynamicLocale"] = false,
                ["id"] = locCondId,
                ["target"] = BuildLocationTargets(obj.Location),
            });
        }

        // TODO: weaponCategories — BSG uses weaponCaliber, not generic categories. Mapping not yet implemented.
        // TODO: surviveAfterKill — Not directly representable in a single counter condition.
        //       In vanilla quests this is always split into two separate objectives (kill + survive/extract).

        // Build locale for this objective with advanced condition hints.
        var locationDisplay = !string.IsNullOrWhiteSpace(obj.Location) ? LocationHelper.ToDisplayName(obj.Location) : null;
        var locationText = locationDisplay != null ? $" on {locationDisplay}" : "";
        var targetDisplay = GetTargetDisplayName(target);

        var distanceHint = obj.MinDistance.HasValue
            ? $" from {obj.MinDistance.Value}m+"
            : obj.MaxDistance.HasValue
                ? $" within {obj.MaxDistance.Value}m"
                : "";

        var timeHint = (obj.TimeFrom.HasValue || obj.TimeTo.HasValue)
            ? $" at night"
            : "";

        var bodyPartHint = obj.BodyPart?.Count > 0
            ? $" ({string.Join(", ", obj.BodyPart)})"
            : "";

        var desc = obj.Description ?? $"Eliminate {obj.Count} {targetDisplay}{locationText}{distanceHint}{timeHint}{bodyPartHint}";
        locales[condId] = desc;

        return new JsonObject
        {
            ["completeInSeconds"] = 0,
            ["conditionType"] = "CounterCreator",
            ["counter"] = new JsonObject
            {
                ["conditions"] = counterConditions,
                ["id"] = counterId,
            },
            ["doNotResetIfCounterCompleted"] = false,
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = condId,
            ["index"] = index,
            ["oneSessionOnly"] = false,
            ["parentId"] = "",
            ["type"] = "Elimination",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildSurviveCondition(QuestObjective obj, int index, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{index}:cond");
        var counterId = DeriveStableId($"{questId}:obj{index}:counter");
        var exitCondId = DeriveStableId($"{questId}:obj{index}:exit");
        var locCondId = DeriveStableId($"{questId}:obj{index}:loc");

        var counterConditions = new JsonArray
        {
            new JsonObject
            {
                ["conditionType"] = "ExitStatus",
                ["dynamicLocale"] = false,
                ["id"] = exitCondId,
                ["status"] = new JsonArray { "Survived", "Runner" },
            },
            new JsonObject
            {
                ["conditionType"] = "Location",
                ["dynamicLocale"] = false,
                ["id"] = locCondId,
                ["target"] = BuildLocationTargets(obj.Location!),
            },
        };

        var locationDisplayName = LocationHelper.ToDisplayName(obj.Location!);
        var desc = obj.Description ?? $"Survive and extract from {locationDisplayName} {obj.Count} time(s)";
        locales[condId] = desc;

        return new JsonObject
        {
            ["completeInSeconds"] = 0,
            ["conditionType"] = "CounterCreator",
            ["counter"] = new JsonObject
            {
                ["conditions"] = counterConditions,
                ["id"] = counterId,
            },
            ["doNotResetIfCounterCompleted"] = false,
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = condId,
            ["index"] = index,
            ["oneSessionOnly"] = false,
            ["parentId"] = "",
            ["type"] = "Exploration",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildExtractCondition(QuestObjective obj, int index, JsonObject locales, string questId)
    {
        // extract_location is structurally the same as survive_location in BSG format
        return BuildSurviveCondition(obj, index, locales, questId);
    }

    // Returns a JsonArray of location target strings for counter conditions.
    // Ground Zero has two variants (Sandbox / sandbox_high) so both are included
    // to ensure objectives track regardless of player level.
    private static JsonArray BuildLocationTargets(string location)
    {
        if (string.Equals(location, "Sandbox", StringComparison.OrdinalIgnoreCase))
            return new JsonArray { "Sandbox", "Sandbox_high" };
        if (string.Equals(location, "Sandbox_high", StringComparison.OrdinalIgnoreCase))
            return new JsonArray { "Sandbox_high", "Sandbox" };
        if (string.Equals(location, "factory4", StringComparison.OrdinalIgnoreCase))
            return new JsonArray { "factory4_day", "factory4_night" };
        return new JsonArray { location };
    }

    // Reward builder

    private static JsonArray BuildRewards(QuestRewards rewards, string traderId)
    {
        var result = new JsonArray();
        var idx = 0;

        // XP reward
        if (rewards.Xp > 0)
        {
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["type"] = "Experience",
                ["value"] = rewards.Xp.ToString(),
                ["unknown"] = false,
            });
        }

        // Money reward
        if (rewards.Money is { Amount: > 0 })
        {
            var moneyTpl = CurrencyHelper.ToTemplateId(rewards.Money.Currency);
            var moneyItemId = GenerateId();
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["findInRaid"] = true,
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["_id"] = moneyItemId,
                        ["_tpl"] = moneyTpl,
                        ["upd"] = new JsonObject
                        {
                            ["StackObjectsCount"] = rewards.Money.Amount,
                        },
                    },
                },
                ["target"] = moneyItemId,
                ["type"] = "Item",
                ["value"] = rewards.Money.Amount.ToString(),
                ["unknown"] = false,
            });
        }

        // Item rewards
        foreach (var item in rewards.Items)
        {
            var itemRewardId = GenerateId();
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["findInRaid"] = true,
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["_id"] = itemRewardId,
                        ["_tpl"] = item.ItemTpl,
                        ["upd"] = new JsonObject
                        {
                            ["StackObjectsCount"] = item.Count,
                        },
                    },
                },
                ["target"] = itemRewardId,
                ["type"] = "Item",
                ["value"] = item.Count.ToString(),
                ["unknown"] = false,
            });
        }

        // Trader standing reward
        if (rewards.TraderStanding > 0)
        {
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["target"] = traderId,
                ["type"] = "TraderStanding",
                ["value"] = rewards.TraderStanding.ToString("F2"),
                ["unknown"] = false,
            });
        }

        // Assortment unlock rewards
        foreach (var assortItemId in rewards.UnlockAssortItems)
        {
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["_id"] = assortItemId,
                        ["_tpl"] = assortItemId, // The assort item ID is also the tpl reference
                    },
                },
                ["loyaltyLevel"] = 1,
                ["target"] = assortItemId,
                ["traderId"] = traderId,
                ["type"] = "AssortmentUnlock",
                ["unknown"] = false,
            });
        }

        return result;
    }

    private static void BuildQuestAssortUnlocks(string questId, QuestRewards rewards, JsonObject questAssortSuccess)
    {
        foreach (var assortItemId in rewards.UnlockAssortItems)
        {
            questAssortSuccess[assortItemId] = questId;
        }
    }

    // Icon resolution

    // Resolve quest icon filename.
    private static string ResolveQuestIcon(
        StoryQuestDefinition quest,
        string packFolder,
        string? defaultQuestIcon,
        string imagesDir)
    {
        // Per-quest icon
        if (!string.IsNullOrWhiteSpace(quest.Image))
        {
            var source = Path.Combine(packFolder, quest.Image);
            if (File.Exists(source))
            {
                var fileName = Path.GetFileName(quest.Image);
                var dest = Path.Combine(imagesDir, fileName);
                File.Copy(source, dest, overwrite: true);
                return fileName;
            }
        }

        // Pack default icon
        if (!string.IsNullOrWhiteSpace(defaultQuestIcon))
        {
            var source = Path.Combine(packFolder, defaultQuestIcon);
            if (File.Exists(source))
            {
                return Path.GetFileName(defaultQuestIcon);
            }
        }

        // Generated placeholder
        return "default_quest_icon.png";
    }

    // Helpers

    private static string DetermineQuestType(List<QuestObjective> objectives)
    {
        if (objectives.Count == 0) return "PickUp";

        var firstType = objectives[0].Type.ToLowerInvariant();
        return firstType switch
        {
            "kill_enemy" => "Elimination",
            "survive_location" or "extract_location" => "Exploration",
            "handover_item" or "handover_fir_item" => "PickUp",
            _ => "PickUp",
        };
    }

    private static string GetTargetDisplayName(string target)
    {
        return target switch
        {
            "Savage" => "Scavs",
            "AnyPmc" => "PMCs",
            "Any" => "enemies",
            "exUsec" => "Rogues",
            "pmcBot" => "Raiders",
            "sectantPriest" => "Cultist Priests",
            "sectantWarrior" => "Cultist Warriors",
            "bossKnight" => "Knight",
            "bossBully" => "Reshala",
            "bossKilla" => "Killa",
            "bossKojaniy" => "Shturman",
            "bossSanitar" => "Sanitar",
            "bossTagilla" => "Tagilla",
            "bossGluhar" => "Gluhar",
            "bossZryachiy" => "Zryachiy",
            "bossBoar" => "Kaban",
            "bossPartisan" => "Partisan",
            "bossKolontay" => "Kolontay",
            _ => target,
        };
    }

    // Generate minimal transparent PNG placeholder.
    private static void GenerateDefaultQuestIcon(string path)
    {
        // 1x1 transparent PNG (67 bytes)
        byte[] pngBytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, // RGBA, 8-bit
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x78, 0x9C, 0x62, 0x00, 0x00, 0x00, 0x02, // compressed pixel
            0x00, 0x01, 0xE5, 0x27, 0xDE, 0xFC, 0x00, 0x00, // ...
            0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, // IEND chunk
            0x60, 0x82,
        ];
        File.WriteAllBytes(path, pngBytes);
    }

    // Derive a stable 24-char hex ID from a seed string.
    // Used for quest condition IDs so objective progress persists across server restarts.
    private static string DeriveStableId(string seed)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexStringLower(hash)[..24];
    }

    // Generate 24-char hex ID.
    private static string GenerateId()
    {
        var bytes = new byte[12];
        Rng.NextBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
