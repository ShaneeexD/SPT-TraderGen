using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils.Json;
using TraderGen.Models;

namespace TraderGen.Services;

// Generates proper SPT RepeatableQuest objects from our RotatingQuestTemplate definitions.
// These are injected into the daily/weekly quest pools via the Harmony patch.
public static class RepeatableQuestGenerator
{
    private static readonly Random Rng = new();

    // Generate RepeatableQuest objects from rotating quest templates for a specific trader.
    // Returns quests grouped by rotation type ("Daily" or "Weekly").
    public static Dictionary<string, List<RepeatableQuest>> GenerateRepeatableQuests(
        List<RotatingQuestTemplate> templates,
        string traderId,
        string packFolder,
        ISptLogger<TraderGenPlugin> logger)
    {
        var result = new Dictionary<string, List<RepeatableQuest>>
        {
            ["Daily"] = new(),
            ["Weekly"] = new(),
        };

        foreach (var template in templates)
        {
            var groupName = template.Rotation.ToLowerInvariant() switch
            {
                "daily" => "Daily",
                "weekly" => "Weekly",
                _ => "Daily",
            };

            for (var i = 0; i < template.QuestCount; i++)
            {
                var quest = GenerateQuest(template, traderId, packFolder, logger);
                if (quest != null)
                    result[groupName].Add(quest);
            }
        }

        return result;
    }

    // Generate change requirements for the generated quests (cost to replace them).
    public static Dictionary<string, Dictionary<MongoId, ChangeRequirement>> GenerateChangeRequirements(
        Dictionary<string, List<RepeatableQuest>> questsByGroup)
    {
        var result = new Dictionary<string, Dictionary<MongoId, ChangeRequirement>>();

        foreach (var (groupName, quests) in questsByGroup)
        {
            var requirements = new Dictionary<MongoId, ChangeRequirement>();
            foreach (var quest in quests)
            {
                requirements[quest.Id] = new ChangeRequirement
                {
                    ChangeCost = quest.ChangeCost,
                    ChangeStandingCost = 0.01,
                };
            }
            result[groupName] = requirements;
        }

        return result;
    }

    private static RepeatableQuest? GenerateQuest(
        RotatingQuestTemplate template,
        string traderId,
        string packFolder,
        ISptLogger<TraderGenPlugin> logger)
    {
        var questId = new MongoId();

        // Pick random name and description
        var name = template.NamePool[Rng.Next(template.NamePool.Count)];
        var description = template.DescriptionPool.Count > 0
            ? template.DescriptionPool[Rng.Next(template.DescriptionPool.Count)]
            : $"Complete the assigned {template.Rotation} task.";

        // Build objectives and determine quest type + location
        var (conditions, questType, location) = BuildConditions(template, traderId);

        // Apply {location} placeholder
        var locationDisplay = !string.IsNullOrWhiteSpace(location)
            ? LocationHelper.ToDisplayName(location)
            : "Tarkov";
        name = name.Replace("{location}", locationDisplay);
        description = description.Replace("{location}", locationDisplay);

        // Calculate rewards
        var totalCount = template.Objectives.Sum(o => Rng.Next(o.CountRange.Min, o.CountRange.Max + 1));
        var scaling = template.RewardScaling;
        var rewards = BuildRewards(traderId, totalCount, scaling);

        // Build the change cost (currency to replace the quest)
        var changeCost = new List<ChangeCost>
        {
            new()
            {
                TemplateId = CurrencyHelper.ToTemplateId(scaling.Currency),
                Count = Math.Max(1000, scaling.BaseMoney / 2),
            }
        };

        var quest = new RepeatableQuest
        {
            Id = questId,
            TraderId = traderId,
            Location = GetQuestLocation(location),
            Image = ResolveQuestImageRoute(template, packFolder),
            Type = questType,
            IsKey = false,
            Restartable = false,
            InstantComplete = false,
            SecretQuest = false,
            CanShowNotificationsInGame = true,
            Name = questId.ToString() + " name",
            Note = questId.ToString() + " note",
            Description = questId.ToString() + " description",
            SuccessMessageText = questId.ToString() + " successMessageText",
            FailMessageText = questId.ToString() + " failMessageText",
            StartedMessageText = questId.ToString() + " startedMessageText",
            AcceptPlayerMessage = questId.ToString() + " acceptPlayerMessage",
            DeclinePlayerMessage = questId.ToString() + " declinePlayerMessage",
            CompletePlayerMessage = questId.ToString() + " completePlayerMessage",
            ChangeQuestMessageText = questId.ToString() + " changeQuestMessageText",
            AcceptanceAndFinishingSource = null,
            Side = "Pmc",
            Conditions = conditions,
            Rewards = rewards,
            ChangeCost = changeCost,
            ChangeStandingCost = 0,
            TemplateId = GetTemplateIdForType(questType),
            QuestStatus = new RepeatableQuestStatus
            {
                Id = new MongoId(),
                Uid = "",
                QId = questId,
                StartTime = 0,
                Status = 0,
                StatusTimers = null,
            }
        };

        // Store locale data for the quest
        RepeatableQuestLocaleStore.Add(questId.ToString(), name, description);

        logger.LogWithColor(
            $"[TraderGen] Generated repeatable quest '{name}' ({template.Rotation}) for trader {traderId}",
            LogTextColor.Green);

        return quest;
    }

    private static (QuestConditionTypes conditions, QuestTypeEnum questType, string? pickedLocation) BuildConditions(
        RotatingQuestTemplate template, string traderId)
    {
        var availableForFinish = new List<QuestCondition>();
        string? pickedLocation = null;
        var questType = QuestTypeEnum.Elimination;

        foreach (var objTemplate in template.Objectives)
        {
            var count = Rng.Next(objTemplate.CountRange.Min, objTemplate.CountRange.Max + 1);
            var location = objTemplate.LocationPool.Count > 0
                ? objTemplate.LocationPool[Rng.Next(objTemplate.LocationPool.Count)]
                : null;

            if (!string.IsNullOrWhiteSpace(location))
                pickedLocation = location;

            switch (objTemplate.Type.ToLowerInvariant())
            {
                case "kill_enemy":
                    questType = QuestTypeEnum.Elimination;
                    availableForFinish.Add(BuildKillCondition(objTemplate, count, location));
                    break;
                case "handover_item":
                case "handover_fir_item":
                    questType = QuestTypeEnum.Completion;
                    availableForFinish.Add(BuildHandoverCondition(objTemplate, count, location,
                        objTemplate.Type.ToLowerInvariant() == "handover_fir_item"));
                    break;
                case "survive_location":
                case "extract_location":
                    questType = QuestTypeEnum.Exploration;
                    availableForFinish.Add(BuildSurviveCondition(count, location));
                    break;
            }
        }

        // AvailableForStart: level >= 1
        var availableForStart = new List<QuestCondition>
        {
            new()
            {
                Id = new MongoId(),
                DynamicLocale = true,
                ConditionType = "Level",
                CompareMethod = ">=",
                Value = 1,
                VisibilityConditions = [],
            }
        };

        var conditions = new QuestConditionTypes
        {
            AvailableForStart = availableForStart,
            AvailableForFinish = availableForFinish,
            Started = [],
            Success = [],
            Fail = [],
        };

        return (conditions, questType, pickedLocation);
    }

    private static QuestCondition BuildKillCondition(RotatingObjectiveTemplate objTemplate, int count, string? location)
    {
        var target = objTemplate.TargetPool.Count > 0
            ? objTemplate.TargetPool[Rng.Next(objTemplate.TargetPool.Count)]
            : "Savage";

        var counterConditions = new List<QuestConditionCounterCondition>
        {
            new()
            {
                Id = new MongoId(),
                DynamicLocale = true,
                ConditionType = "Kills",
                Target = new ListOrT<string>(null, target),
                Value = 1,
            }
        };

        // Add location condition if specific
        if (!string.IsNullOrWhiteSpace(location) && location != "any")
        {
            counterConditions.Add(new QuestConditionCounterCondition
            {
                Id = new MongoId(),
                DynamicLocale = true,
                ConditionType = "Location",
                Target = new ListOrT<string>(new List<string> { location }, null),
            });
        }

        var conditionId = new MongoId();
        var locationDisplay = !string.IsNullOrWhiteSpace(location) && location != "any"
            ? $" on {LocationHelper.ToDisplayName(location)}"
            : "";
        var targetDisplay = target == "Savage" ? "Scavs" : target == "exUsec" ? "Rogues" : target == "pmcBot" ? "PMCs" : target;
        RepeatableQuestLocaleStore.AddCondition(conditionId.ToString(), $"Eliminate {count} {targetDisplay}{locationDisplay}");

        return new QuestCondition
        {
            Id = conditionId,
            DynamicLocale = true,
            ConditionType = "CounterCreator",
            CompareMethod = ">=",
            Value = count,
            VisibilityConditions = [],
            Counter = new QuestConditionCounter
            {
                Id = new MongoId().ToString(),
                Conditions = counterConditions,
            },
        };
    }

    private static QuestCondition BuildHandoverCondition(
        RotatingObjectiveTemplate objTemplate, int count, string? location, bool foundInRaid)
    {
        var itemTpl = objTemplate.ItemPool.Count > 0
            ? objTemplate.ItemPool[Rng.Next(objTemplate.ItemPool.Count)]
            : "5449016a4bdc2d6f028b456f"; // Roubles fallback

        var conditionId = new MongoId();
        var firText = foundInRaid ? " (found in raid)" : "";
        RepeatableQuestLocaleStore.AddCondition(conditionId.ToString(), $"Hand over {count}x item{firText}");

        return new QuestCondition
        {
            Id = conditionId,
            DynamicLocale = true,
            ConditionType = "HandoverItem",
            CompareMethod = ">=",
            Value = count,
            Target = new ListOrT<string>(new List<string> { itemTpl }, null),
            OnlyFoundInRaid = foundInRaid,
            VisibilityConditions = [],
        };
    }

    private static QuestCondition BuildSurviveCondition(int count, string? location)
    {
        var counterConditions = new List<QuestConditionCounterCondition>
        {
            new()
            {
                Id = new MongoId(),
                DynamicLocale = true,
                ConditionType = "ExitStatus",
                Status = new List<string> { "Survived", "Runner" },
            }
        };

        if (!string.IsNullOrWhiteSpace(location) && location != "any")
        {
            counterConditions.Add(new QuestConditionCounterCondition
            {
                Id = new MongoId(),
                DynamicLocale = true,
                ConditionType = "Location",
                Target = new ListOrT<string>(new List<string> { location }, null),
            });
        }

        var conditionId = new MongoId();
        var locationDisplay = !string.IsNullOrWhiteSpace(location) && location != "any"
            ? LocationHelper.ToDisplayName(location)
            : "the location";
        RepeatableQuestLocaleStore.AddCondition(conditionId.ToString(), $"Survive and extract from {locationDisplay} {count} time(s)");

        return new QuestCondition
        {
            Id = conditionId,
            DynamicLocale = true,
            ConditionType = "CounterCreator",
            CompareMethod = ">=",
            Value = count,
            VisibilityConditions = [],
            Counter = new QuestConditionCounter
            {
                Id = new MongoId().ToString(),
                Conditions = counterConditions,
            },
        };
    }

    private static Dictionary<string, List<Reward>> BuildRewards(string traderId, int totalCount, RewardScaling scaling)
    {
        var moneyItemId = new MongoId();

        var rewards = new Dictionary<string, List<Reward>>
        {
            ["Started"] = [],
            ["Success"] = new List<Reward>
            {
                // XP reward
                new()
                {
                    Id = new MongoId(),
                    Type = RewardType.Experience,
                    Value = scaling.XpPerObjectiveCount * totalCount,
                    Index = 0,
                },
                // Money reward
                new()
                {
                    Id = new MongoId(),
                    Type = RewardType.Item,
                    Index = 1,
                    Target = moneyItemId.ToString(),
                    Value = scaling.BaseMoney + (scaling.MoneyPerObjectiveCount * totalCount),
                    Items = new List<Item>
                    {
                        new()
                        {
                            Id = moneyItemId,
                            Template = CurrencyHelper.ToTemplateId(scaling.Currency),
                            Upd = new Upd { StackObjectsCount = scaling.BaseMoney + (scaling.MoneyPerObjectiveCount * totalCount) },
                        }
                    },
                },
                // Standing reward
                new()
                {
                    Id = new MongoId(),
                    Type = RewardType.TraderStanding,
                    Value = scaling.Standing,
                    Index = 2,
                    Target = traderId,
                },
            },
            ["Fail"] = [],
        };

        return rewards;
    }

    private static string GetQuestLocation(string? location)
    {
        return LocationHelper.ToQuestLocationId(location);
    }

    private static string GetTemplateIdForType(QuestTypeEnum type)
    {
        // These are the standard BSG repeatable quest template IDs for PMC dailies
        return type switch
        {
            QuestTypeEnum.Elimination => "616052ea3054fc0e2c24ce6e",
            QuestTypeEnum.Completion => "61604635c725987e815b1a46",
            QuestTypeEnum.Exploration => "616041eb031af660100c9967",
            _ => "616052ea3054fc0e2c24ce6e",
        };
    }

    private static readonly string DefaultRepeatableQuestIcon = "/files/quest/icon/616d993bc8c5ad2ab30ff6ba.jpg";

    // Resolves the quest image route path for a template.
    // If the template specifies an image and the file exists in the pack folder,
    // returns a route like "/files/quest/icon/tpl_{templateId}".
    // Otherwise falls back to the default repeatable quest icon.
    private static string ResolveQuestImageRoute(RotatingQuestTemplate template, string packFolder)
    {
        if (!string.IsNullOrWhiteSpace(template.Image))
        {
            var absPath = System.IO.Path.Combine(packFolder, template.Image);
            if (File.Exists(absPath))
            {
                return $"/files/quest/icon/tpl_{template.Id}";
            }
        }

        return DefaultRepeatableQuestIcon;
    }

    // Collects all template image paths that need to be registered with ImageRouter.
    // Returns a list of (routePath, absoluteFilePath) tuples.
    public static List<(string RoutePath, string AbsoluteFilePath)> GetTemplateImagePaths(
        List<RotatingQuestTemplate> templates,
        string packFolder)
    {
        var result = new List<(string RoutePath, string AbsoluteFilePath)>();

        foreach (var template in templates)
        {
            if (string.IsNullOrWhiteSpace(template.Image))
                continue;

            var absPath = System.IO.Path.Combine(packFolder, template.Image);
            if (!File.Exists(absPath))
                continue;

            var routePath = $"/files/quest/icon/tpl_{template.Id}";
            result.Add((routePath, absPath));
        }

        return result;
    }
}
