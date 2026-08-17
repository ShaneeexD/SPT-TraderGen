using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;
using SptTables = SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TraderGen.Models;
using System.Globalization;
using System.Linq;

namespace TraderGen.Services;

// Entry for patching item names into quest locales after all mods (e.g. ItemGen) have loaded.
public record LocaleFixupEntry(string CondId, string? ItemTpl, int Count, string ObjectiveType, string? CustomDescription);

// Translates TraderGen quest definitions into BSG quest format.
public static class QuestBuilder
{
    private static readonly Random Rng = new();

    // Locale entries that need item names resolved after custom item mods have loaded.
    public static readonly List<LocaleFixupEntry> LocaleFixups = new();

    // Build quest files for a trader.
    public static int BuildQuestFiles(
        string traderId,
        List<StoryQuestDefinition> allStoryQuests,
        List<ProductionSchemeDefinition> productionSchemes,
        string outputBaseDir,
        string packFolder,
        string? defaultQuestIcon,
        TemplateTable templateTable,
        TradersTable tradersTable,
        LocaleTable localeTable,
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
        var schemeDict = productionSchemes.ToDictionary(s => s.Id, s => s);

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
            var bsgQuest = BuildStoryQuest(quest, allLocales, iconFileName, templateTable, localeTable, schemeDict);
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

        logger.LogWithColor(
            $"[TraderGen] Built {count} quest(s) for trader {traderId} → {traderDir}",
            LogColor.Green);

        // Resolve quest-assort keys against the trader's already-built assort and apply to the live database.
        var trader = tradersTable.GetValueOrDefault(traderId);
        if (trader != null)
        {
            if (!trader.QuestAssort.TryGetValue("success", out var successMap))
            {
                successMap = new Dictionary<MongoId, MongoId>();
                trader.QuestAssort["success"] = successMap;
            }

            var resolved = new JsonObject();
            foreach (var kvp in questAssortSuccess)
            {
                var questId = kvp.Value?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(questId))
                    continue;

                var rawId = kvp.Key;
                var item = trader.Assort.Items.FirstOrDefault(i => i.Id == rawId)
                    ?? trader.Assort.Items.FirstOrDefault(i => i.Template == rawId);

                string itemId;
                if (item != null)
                {
                    itemId = item.Id;
                }
                else
                {
                    // Auto-create a placeholder assort item so the quest-locked item actually shows.
                    itemId = MongoId.IsValidMongoId(rawId) ? rawId : new MongoId().ToString();
                    var tpl = MongoId.IsValidMongoId(rawId) ? rawId : rawId;
                    trader.Assort.Items.Add(new SptTables.Item
                    {
                        Id = itemId,
                        Template = tpl,
                        ParentId = "hideout",
                        SlotId = "hideout",
                        Upd = new SptTables.Upd
                        {
                            UnlimitedCount = true,
                            StackObjectsCount = 999999,
                        },
                    });

                    // 1 RUB placeholder; user can define a real barter in the trader's assort list if desired.
                    var rubTpl = "5449016a4bdc2d6f028b456f";
                    trader.Assort.BarterScheme[itemId] =
                    [
                        [new SptTables.BarterScheme { Template = rubTpl, Count = 1 }],
                    ];
                    trader.Assort.LoyalLevelItems[itemId] = 1;
                }

                successMap[new MongoId(itemId)] = new MongoId(questId);
                resolved[itemId] = questId;
            }

            File.WriteAllText(
                Path.Combine(assortDir, "questAssort.json"),
                new JsonObject
                {
                    ["started"] = new JsonObject(),
                    ["success"] = resolved,
                    ["fail"] = new JsonObject(),
                }.ToJsonString(jsonOpts));
        }
        else
        {
            var questAssortObj = new JsonObject
            {
                ["started"] = new JsonObject(),
                ["success"] = questAssortSuccess,
                ["fail"] = new JsonObject(),
            };
            File.WriteAllText(
                Path.Combine(assortDir, "questAssort.json"),
                questAssortObj.ToJsonString(jsonOpts));
        }

        return count;
    }

    // ==================== Story Quest Builder ====================

    private static JsonObject BuildStoryQuest(
        StoryQuestDefinition quest,
        JsonObject locales,
        string iconFileName,
        TemplateTable templateTable,
        LocaleTable localeTable,
        Dictionary<string, ProductionSchemeDefinition> schemes)
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
        var conditionIndex = 0;
        for (var i = 0; i < quest.Objectives.Count; i++)
        {
            var obj = quest.Objectives[i];
            var conditions = BuildObjectiveCondition(obj, i, conditionIndex, locales, questId, templateTable, localeTable).ToList();
            foreach (var c in conditions)
            {
                finishConditions.Add(c);
            }

            conditionIndex += conditions.Count;
        }

        // Build rewards
        var successRewards = BuildRewards(quest.Rewards, quest.TraderId, schemes);

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
                ["Started"] = BuildInitialEquipmentRewards(quest.Rewards.InitialEquipment),
                ["Success"] = successRewards,
                ["Fail"] = new JsonArray(),
            },
        };
    }

    // Objective builders

    private static IEnumerable<JsonNode> BuildObjectiveCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId, TemplateTable templateTable, LocaleTable localeTable)
    {
        switch (obj.Type.ToLowerInvariant())
        {
            case "handover_item":
                yield return BuildHandoverCondition(obj, objectiveIndex, conditionIndex, false, locales, questId, templateTable, localeTable);
                break;
            case "handover_fir_item":
                yield return BuildHandoverCondition(obj, objectiveIndex, conditionIndex, true, locales, questId, templateTable, localeTable);
                break;
            case "find_item":
                foreach (var c in BuildFindItemCondition(obj, objectiveIndex, conditionIndex, locales, questId, templateTable, localeTable))
                {
                    yield return c;
                }
                break;
            case "leave_item_at_location":
                yield return BuildLeaveItemAtLocationCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            case "kill_enemy":
                yield return BuildKillCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            case "survive_location":
                yield return BuildSurviveCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            case "extract_location":
                yield return BuildExtractCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            case "zone_visit":
                yield return BuildZoneVisitCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            case "zone_kill":
                yield return BuildZoneKillCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            case "zone_place_item":
                yield return BuildZonePlaceItemCondition(obj, objectiveIndex, conditionIndex, locales, questId);
                break;
            default:
                throw new InvalidOperationException($"Unknown objective type: {obj.Type}");
        }
    }

    private static JsonObject BuildHandoverCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, bool foundInRaid, JsonObject locales, string questId, TemplateTable templateTable, LocaleTable localeTable)
    {
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");

        // Build locale for this objective
        var firText = foundInRaid ? "found in raid " : "";
        var itemName = GetItemName(templateTable, localeTable, obj.ItemTpl);
        var desc = obj.Description ?? $"Hand over {obj.Count} {firText}{itemName}";
        locales[condId] = desc;
        LocaleFixups.Add(new LocaleFixupEntry(condId, obj.ItemTpl, obj.Count, "handover", obj.Description));

        return new JsonObject
        {
            ["conditionType"] = "HandoverItem",
            ["dogtagLevel"] = 0,
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = condId,
            ["index"] = conditionIndex,
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

    private static IEnumerable<JsonNode> BuildFindItemCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId, TemplateTable templateTable, LocaleTable localeTable)
    {
        var findCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:find");
        var handoverCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:handover");
        var visCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:vis");

        var itemName = GetItemName(templateTable, localeTable, obj.ItemTpl);
        var desc = obj.Description ?? $"Find {obj.Count} {itemName}";
        locales[findCondId] = desc;
        LocaleFixups.Add(new LocaleFixupEntry(findCondId, obj.ItemTpl, obj.Count, "find", obj.Description));

        var findCondition = new JsonObject
        {
            ["conditionType"] = "FindItem",
            ["id"] = findCondId,
            ["index"] = conditionIndex,
            ["target"] = new JsonArray { obj.ItemTpl! },
            ["value"] = obj.Count,
            ["onlyFoundInRaid"] = false,
            ["countInRaid"] = obj.CountInRaid,
            ["minDurability"] = 0,
            ["maxDurability"] = 100,
            ["dogtagLevel"] = 0,
            ["dynamicLocale"] = false,
            ["isEncoded"] = false,
            ["parentId"] = "",
            ["visibilityConditions"] = new JsonArray(),
        };

        yield return findCondition;

        if (!obj.HandoverAfterFind)
        {
            yield break;
        }

        var handoverDesc = $"Hand over {obj.Count} {itemName}";
        locales[handoverCondId] = handoverDesc;
        LocaleFixups.Add(new LocaleFixupEntry(handoverCondId, obj.ItemTpl, obj.Count, "handover", null));

        yield return new JsonObject
        {
            ["conditionType"] = "HandoverItem",
            ["id"] = handoverCondId,
            ["index"] = conditionIndex + 1,
            ["target"] = new JsonArray { obj.ItemTpl! },
            ["value"] = obj.Count,
            ["onlyFoundInRaid"] = false,
            ["dogtagLevel"] = 0,
            ["dynamicLocale"] = false,
            ["isEncoded"] = false,
            ["minDurability"] = 0,
            ["maxDurability"] = 100,
            ["parentId"] = "",
            ["visibilityConditions"] = new JsonArray
            {
                new JsonObject
                {
                    ["conditionType"] = "CompleteCondition",
                    ["id"] = visCondId,
                    ["target"] = findCondId,
                },
            },
        };
    }

    private static string GetItemName(TemplateTable templateTable, LocaleTable localeTable, string? itemTpl)
    {
        if (string.IsNullOrWhiteSpace(itemTpl)) return "item";

        try
        {
            var globalLocales = localeTable.Global;
            if (globalLocales.TryGetValue("en", out var enLocale))
            {
                var data = enLocale.Value;
                if (data != null)
                {
                    if (data.TryGetValue($"{itemTpl} Name", out var name) && !string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }

                    if (data.TryGetValue($"{itemTpl} ShortName", out var shortName) && !string.IsNullOrWhiteSpace(shortName))
                    {
                        return shortName;
                    }
                }
            }
        }
        catch
        {
            // Fallback to template name/id below
        }

        try
        {
            var items = templateTable.Items;
            if (items.TryGetValue(itemTpl, out var itemTemplate) && !string.IsNullOrWhiteSpace(itemTemplate.Name))
            {
                return itemTemplate.Name;
            }
        }
        catch
        {
            // Fallback to template id on error
        }

        return itemTpl;
    }

    private static JsonObject BuildKillCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");
        var killCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:kill");
        var counterId = DeriveStableId($"{questId}:obj{objectiveIndex}:counter");

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
            var equipCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:equip");
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
            var locCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:loc");
            counterConditions.Add(new JsonObject
            {
                ["conditionType"] = "Location",
                ["dynamicLocale"] = false,
                ["id"] = locCondId,
                ["target"] = BuildLocationTargets(obj.Location),
            });
        }

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
            ["index"] = conditionIndex,
            ["oneSessionOnly"] = obj.OneSessionOnly,
            ["parentId"] = "",
            ["type"] = "Elimination",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildSurviveCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");
        var counterId = DeriveStableId($"{questId}:obj{objectiveIndex}:counter");
        var exitCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:exit");
        var locCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:loc");

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

        if (!string.IsNullOrWhiteSpace(obj.RequiredExtract))
        {
            var exitNameCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:exitname");
            counterConditions.Add(new JsonObject
            {
                ["conditionType"] = "ExitName",
                ["dynamicLocale"] = false,
                ["id"] = exitNameCondId,
                ["exitName"] = obj.RequiredExtract,
            });
        }

        var locationDisplayName = LocationHelper.ToDisplayName(obj.Location!);
        var extractSuffix = !string.IsNullOrWhiteSpace(obj.RequiredExtract) ? $" via {obj.RequiredExtract}" : "";
        var desc = obj.Description ?? $"Survive and extract from {locationDisplayName}{extractSuffix} {obj.Count} time(s)";
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
            ["index"] = conditionIndex,
            ["oneSessionOnly"] = obj.OneSessionOnly,
            ["parentId"] = "",
            ["type"] = "Exploration",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildExtractCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        // extract_location is structurally the same as survive_location in BSG format
        return BuildSurviveCondition(obj, objectiveIndex, conditionIndex, locales, questId);
    }

    private static JsonObject BuildZoneVisitCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");
        var counterId = DeriveStableId($"{questId}:obj{objectiveIndex}:counter");
        var visitCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:visit");

        var desc = obj.Description ?? "Visit the designated zone";
        locales[condId] = desc;

        var counterConditions = new JsonArray
        {
            new JsonObject
            {
                ["conditionType"] = "VisitPlace",
                ["dynamicLocale"] = false,
                ["id"] = visitCondId,
                ["target"] = obj.ZoneId!,
                ["value"] = 1,
            },
        };

        return new JsonObject
        {
            ["completeInSeconds"] = obj.PlantTime ?? 0,
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
            ["index"] = conditionIndex,
            ["oneSessionOnly"] = obj.OneSessionOnly,
            ["parentId"] = "",
            ["type"] = "Exploration",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildZoneKillCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        // Build a standard kill condition then inject an InZone sub-condition
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");
        var killCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:kill");
        var counterId = DeriveStableId($"{questId}:obj{objectiveIndex}:counter");
        var zoneCondId = DeriveStableId($"{questId}:obj{objectiveIndex}:zone");

        var target = obj.Target ?? "Savage";
        var savageRole = new JsonArray();
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
            savageRole.Add(target);

        var desc = obj.Description ?? $"Eliminate {obj.Count} enemies in the designated zone";
        locales[condId] = desc;

        var killCond = new JsonObject
        {
            ["bodyPart"] = new JsonArray(),
            ["compareMethod"] = ">=",
            ["conditionType"] = "Kills",
            ["daytime"] = new JsonObject { ["from"] = 0, ["to"] = 0 },
            ["distance"] = new JsonObject { ["compareMethod"] = ">=", ["value"] = 0 },
            ["dynamicLocale"] = false,
            ["enemyEquipmentExclusive"] = new JsonArray(),
            ["enemyEquipmentInclusive"] = new JsonArray(),
            ["enemyHealthEffects"] = new JsonArray(),
            ["id"] = killCondId,
            ["resetOnSessionEnd"] = obj.OneSessionOnly,
            ["savageRole"] = savageRole,
            ["target"] = bsgTarget,
            ["value"] = 1,
            ["weapon"] = new JsonArray(),
            ["weaponCaliber"] = new JsonArray(),
            ["weaponModsInclusive"] = new JsonArray(),
            ["weaponModsExclusive"] = new JsonArray(),
        };

        var counterConditions = new JsonArray
        {
            killCond,
            new JsonObject
            {
                ["conditionType"] = "InZone",
                ["dynamicLocale"] = false,
                ["id"] = zoneCondId,
                ["zoneIds"] = new JsonArray { obj.ZoneId! },
            },
        };

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
            ["index"] = conditionIndex,
            ["oneSessionOnly"] = obj.OneSessionOnly,
            ["parentId"] = "",
            ["type"] = "Elimination",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildZonePlaceItemCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");

        var desc = obj.Description ?? $"Place the required item at the designated location";
        locales[condId] = desc;

        return new JsonObject
        {
            ["conditionType"] = "PlaceBeacon",
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = condId,
            ["index"] = conditionIndex,
            ["parentId"] = "",
            ["plantTime"] = obj.PlantTime ?? 3,
            ["target"] = new JsonArray { obj.PlantItemTpl ?? "" },
            ["zoneId"] = obj.ZoneId ?? "",
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
        };
    }

    private static JsonObject BuildLeaveItemAtLocationCondition(QuestObjective obj, int objectiveIndex, int conditionIndex, JsonObject locales, string questId)
    {
        var condId = DeriveStableId($"{questId}:obj{objectiveIndex}:cond");

        var itemName = obj.ItemTpl ?? "item";
        var desc = obj.Description ?? $"Leave {itemName} at the designated location";
        locales[condId] = desc;
        LocaleFixups.Add(new LocaleFixupEntry(condId, obj.ItemTpl, obj.Count, "leave", obj.Description));

        return new JsonObject
        {
            ["conditionType"] = "LeaveItemAtLocation",
            ["countInRaid"] = false,
            ["dogtagLevel"] = 0,
            ["dynamicLocale"] = false,
            ["globalQuestCounterId"] = "",
            ["id"] = condId,
            ["index"] = conditionIndex,
            ["isEncoded"] = false,
            ["maxDurability"] = 100,
            ["minDurability"] = 0,
            ["onlyFoundInRaid"] = false,
            ["parentId"] = "",
            ["plantTime"] = obj.PlantTime ?? 20,
            ["target"] = new JsonArray { obj.ItemTpl ?? "" },
            ["value"] = obj.Count,
            ["visibilityConditions"] = new JsonArray(),
            ["zoneId"] = obj.ZoneId ?? "",
        };
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
        return new JsonArray { LocationHelper.ToBsgLocationTarget(location) };
    }

    // Reward builder

    private static JsonArray BuildInitialEquipmentRewards(List<ItemReward> items)
    {
        var result = new JsonArray();
        var index = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemTpl))
                continue;

            var itemId = GenerateId();
            var rewardItems = new JsonArray
            {
                new JsonObject
                {
                    ["_id"] = itemId,
                    ["_tpl"] = item.ItemTpl,
                    ["upd"] = new JsonObject
                    {
                        ["StackObjectsCount"] = item.Count,
                    },
                },
            };

            if (item.Children is { Count: > 0 })
                FlattenRewardChildren(item.Children, itemId, rewardItems);

            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["findInRaid"] = false,
                ["id"] = GenerateId(),
                ["index"] = index++,
                ["items"] = rewardItems,
                ["target"] = itemId,
                ["type"] = "Item",
                ["value"] = item.Count.ToString(),
                ["unknown"] = false,
            });
        }

        return result;
    }

    private static JsonArray BuildRewards(QuestRewards rewards, string traderId, Dictionary<string, ProductionSchemeDefinition> schemes)
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
                ["unknown"] = rewards.XpHidden ?? false,
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
                ["unknown"] = rewards.Money.Hidden ?? false,
            });
        }

        // Item rewards
        foreach (var item in rewards.Items)
        {
            var itemRewardId = GenerateId();
            var rewardItems = new JsonArray
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
            };

            // Flatten child attachments into the reward item list
            if (item.Children is { Count: > 0 })
            {
                FlattenRewardChildren(item.Children, itemRewardId, rewardItems);
            }

            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["findInRaid"] = true,
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["items"] = rewardItems,
                ["target"] = itemRewardId,
                ["type"] = "Item",
                ["value"] = item.Count.ToString(),
                ["unknown"] = item.Hidden ?? false,
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
                ["value"] = rewards.TraderStanding.ToString("F2", CultureInfo.InvariantCulture),
                ["unknown"] = rewards.TraderStandingHidden ?? false,
            });
        }

        // Assortment unlock rewards
        for (var assortIdx = 0; assortIdx < rewards.UnlockAssortItems.Count; assortIdx++)
        {
            var assortItemId = rewards.UnlockAssortItems[assortIdx];
            var assortHidden = rewards.HiddenAssortItems != null && assortIdx < rewards.HiddenAssortItems.Count
                ? rewards.HiddenAssortItems[assortIdx]
                : false;
            var targetItemId = GenerateId();
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["gameMode"] = new JsonArray { "regular", "pve" },
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["isHidden"] = false,
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["_id"] = targetItemId,
                        ["_tpl"] = assortItemId,
                    },
                },
                ["loyaltyLevel"] = 1,
                ["target"] = targetItemId,
                ["traderId"] = traderId,
                ["type"] = "AssortmentUnlock",
                ["unknown"] = assortHidden,
            });
        }

        // Recipe unlock rewards
        for (var recipeIdx = 0; recipeIdx < rewards.Recipes.Count; recipeIdx++)
        {
            var recipe = rewards.Recipes[recipeIdx];
            if (string.IsNullOrWhiteSpace(recipe)) continue;
            var recipeHidden = rewards.HiddenRecipes != null && recipeIdx < rewards.HiddenRecipes.Count
                ? rewards.HiddenRecipes[recipeIdx]
                : false;
            var targetItemId = GenerateId();
            var scheme = schemes.GetValueOrDefault(recipe);
            var recipeItems = new JsonArray
            {
                new JsonObject
                {
                    ["_id"] = targetItemId,
                    ["_tpl"] = scheme?.EndProduct ?? recipe,
                    ["upd"] = new JsonObject
                    {
                        ["StackObjectsCount"] = scheme?.Count ?? 1,
                    },
                },
            };

            var areaReq = scheme?.Requirements.FirstOrDefault(r =>
                r.Type.Equals("Area", StringComparison.OrdinalIgnoreCase));
            var loyaltyLevel = areaReq?.RequiredLevel ?? 1;

            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["gameMode"] = new JsonArray { "regular", "pve" },
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["isHidden"] = false,
                ["items"] = recipeItems,
                ["loyaltyLevel"] = loyaltyLevel,
                ["target"] = targetItemId,
                ["traderId"] = scheme?.AreaType ?? 0,
                ["type"] = "ProductionScheme",
                ["unknown"] = recipeHidden,
            });
        }

        // Stash row expansion reward
        if (rewards.StashRows > 0)
        {
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["type"] = "StashRows",
                ["value"] = rewards.StashRows.ToString(),
                ["unknown"] = rewards.StashRowsHidden ?? false,
            });
        }

        // Skill point rewards
        foreach (var skill in rewards.Skills)
        {
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["target"] = skill.Name,
                ["type"] = "Skill",
                ["value"] = skill.Points.ToString(),
                ["unknown"] = skill.Hidden ?? false,
            });
        }

        // Pocket upgrade reward
        if (!string.IsNullOrWhiteSpace(rewards.Pockets))
        {
            result.Add(new JsonObject
            {
                ["availableInGameEditions"] = new JsonArray(),
                ["id"] = GenerateId(),
                ["index"] = idx++,
                ["target"] = rewards.Pockets,
                ["type"] = "Pockets",
                ["unknown"] = rewards.PocketsHidden ?? false,
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
            "handover_item" or "handover_fir_item" or "find_item" => "PickUp",
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

    // Recursively flatten reward child items into a flat JsonArray for BSG quest rewards.
    private static void FlattenRewardChildren(List<AssortChildItem> children, string parentId, JsonArray output)
    {
        foreach (var child in children)
        {
            var childId = child.ItemId ?? GenerateId();
            output.Add(new JsonObject
            {
                ["_id"] = childId,
                ["_tpl"] = child.ItemTpl,
                ["parentId"] = parentId,
                ["slotId"] = child.SlotId,
            });

            if (child.Children is { Count: > 0 })
            {
                FlattenRewardChildren(child.Children, childId, output);
            }
        }
    }

    // Generate 24-char hex ID.
    private static string GenerateId()
    {
        var bytes = new byte[12];
        Rng.NextBytes(bytes);
        return Convert.ToHexStringLower(bytes);
    }
}
