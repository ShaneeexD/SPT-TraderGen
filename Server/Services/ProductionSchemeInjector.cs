using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TraderGen.Models;

namespace TraderGen.Services;

public static class ProductionSchemeInjector
{
    public static void InjectSchemes(
        List<QuestLoader.LoadedQuestPack> packs,
        HideoutTable hideoutTable,
        ISptLogger<TraderGenPlugin> logger)
    {
        var recipes = hideoutTable.Production.Recipes;
        if (recipes == null)
        {
            hideoutTable.Production.Recipes = recipes = [];
        }

        // Map each scheme to the quests that reward it.
        var schemeToQuests = new Dictionary<string, List<string>>();
        foreach (var pack in packs)
        {
            foreach (var quest in pack.Definition.StoryQuests)
            {
                foreach (var recipe in quest.Rewards.Recipes)
                {
                    if (string.IsNullOrWhiteSpace(recipe)) continue;
                    if (!schemeToQuests.TryGetValue(recipe, out var list))
                    {
                        list = [];
                        schemeToQuests[recipe] = list;
                    }
                    list.Add(quest.Id);
                }
            }
        }

        var added = 0;
        var updated = 0;
        foreach (var pack in packs)
        {
            foreach (var scheme in pack.Definition.ProductionSchemes)
            {
                if (string.IsNullOrWhiteSpace(scheme.Id) || scheme.Id.Length != 24)
                {
                    logger.LogWithColor($"[TraderGen] Skipping invalid production scheme ID '{scheme.Id}'", LogColor.Yellow);
                    continue;
                }

                var existing = recipes.FindIndex(r => r.Id.ToString().Equals(scheme.Id, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                {
                    recipes.RemoveAt(existing);
                    updated++;
                }
                else
                {
                    added++;
                }

                var requirements = new List<Requirement>();
                foreach (var req in scheme.Requirements)
                {
                    var requirement = new Requirement
                    {
                        Type = req.Type,
                        AreaType = req.AreaType,
                        RequiredLevel = req.RequiredLevel,
                        Resource = req.Resource,
                    };

                    if (!string.IsNullOrWhiteSpace(req.TemplateId))
                    {
                        requirement.TemplateId = new MongoId(req.TemplateId);
                    }

                    if (req.Type == "Item")
                    {
                        requirement.Count = req.Count;
                        requirement.IsEncoded = req.IsEncoded ?? false;
                        requirement.IsFunctional = req.IsFunctional ?? false;
                        requirement.IsSpawnedInSession = req.IsSpawnedInSession ?? false;
                    }
                    else if (req.Type == "Tool" && req.Count is > 0)
                    {
                        requirement.Count = req.Count;
                    }

                    requirements.Add(requirement);
                }

                if (!scheme.UnlockedByDefault && schemeToQuests.TryGetValue(scheme.Id, out var questIds))
                {
                    foreach (var questId in questIds.Distinct())
                    {
                        requirements.Add(new Requirement
                        {
                            Type = "QuestComplete",
                            QuestId = questId,
                        });
                    }
                }

                recipes.Add(new HideoutProduction
                {
                    Id = scheme.Id,
                    AreaType = (HideoutAreas)scheme.AreaType,
                    Requirements = requirements,
                    ProductionTime = scheme.ProductionTime,
                    EndProduct = new MongoId(scheme.EndProduct),
                    IsEncoded = false,
                    Locked = !scheme.UnlockedByDefault,
                    NeedFuelForAllProductionTime = scheme.NeedFuelForAllProductionTime,
                    Continuous = scheme.Continuous,
                    Count = scheme.Count,
                    ProductionLimitCount = scheme.ProductionLimitCount,
                    IsCodeProduction = false,
                });
            }
        }

        if (added > 0 || updated > 0)
        {
            logger.LogWithColor($"[TraderGen] Injected {added} new and {updated} updated production scheme(s)", LogColor.Green);
        }
    }
}
