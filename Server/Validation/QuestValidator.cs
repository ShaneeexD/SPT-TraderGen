using TraderGen.Models;

namespace TraderGen.Validation;

// Validates quest definitions and returns clear, actionable error messages for non-programmers.
public static class QuestValidator
{
    private static readonly HashSet<string> ValidObjectiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "handover_item",
        "handover_fir_item",
        "kill_enemy",
        "survive_location",
        "extract_location",
    };

    private static readonly HashSet<string> ValidEnemyTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Savage",       // Scavs
        "AnyPmc",       // Any PMC
        "Any",          // Any enemy
        "exUsec",       // Rogues
        "pmcBot",       // Raider
        "sectantPriest", // Cultist priest
        "sectantWarrior", // Cultist warrior
        "bossKnight",   // Knight
        "bossBully",    // Reshala
        "bossKilla",    // Killa
        "bossKojaniy",  // Shturman
        "bossSanitar",  // Sanitar
        "bossTagilla",  // Tagilla
        "bossGluhar",   // Gluhar
        "bossZryachiy",  // Zryachiy
        "bossBoar",     // Kaban
        "bossPartisan", // Partisan
        "bossKolontay", // Kolontay
    };

    private static readonly HashSet<string> ValidLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "any",
        "bigmap",         // Customs
        "factory4_day",   // Factory (Day)
        "factory4_night", // Factory (Night)
        "Woods",
        "Shoreline",
        "Interchange",
        "Lighthouse",
        "Reserve",         // Reserve (also "RezervBase")
        "RezervBase",
        "laboratory",
        "TarkovStreets",   // Streets of Tarkov
        "Sandbox",         // Ground Zero
        "Sandbox_high",    // Ground Zero (high level)
    };

    private static readonly HashSet<string> ValidRotations = new(StringComparer.OrdinalIgnoreCase)
    {
        "daily",
        "weekly",
    };

    // Validate an entire quest pack. Returns a list of error messages (empty = valid).
    public static List<string> Validate(QuestPackDefinition pack, string traderId, string packName)
    {
        var errors = new List<string>();
        var prefix = $"[{packName}/quests.json]";
        var seenIds = new HashSet<string>();

        // Validate story quests
        for (var i = 0; i < pack.StoryQuests.Count; i++)
        {
            var quest = pack.StoryQuests[i];
            var qPrefix = $"{prefix} storyQuests[{i}]";
            ValidateStoryQuest(quest, traderId, qPrefix, seenIds, errors);
        }

        // Validate rotating quest templates
        for (var i = 0; i < pack.RotatingQuests.Count; i++)
        {
            var template = pack.RotatingQuests[i];
            var tPrefix = $"{prefix} rotatingQuests[{i}]";
            ValidateRotatingTemplate(template, traderId, tPrefix, seenIds, errors);
        }

        return errors;
    }

    private static void ValidateStoryQuest(
        StoryQuestDefinition quest,
        string traderId,
        string prefix,
        HashSet<string> seenIds,
        List<string> errors)
    {
        // ID
        if (string.IsNullOrWhiteSpace(quest.Id))
        {
            errors.Add($"{prefix}: 'id' is required. Use a unique 24-character hex string.");
        }
        else
        {
            if (quest.Id.Length != 24 || !IsHexString(quest.Id))
                errors.Add($"{prefix}: 'id' must be a 24-character hex string. Got: '{quest.Id}'");
            if (!seenIds.Add(quest.Id))
                errors.Add($"{prefix}: Duplicate quest ID '{quest.Id}'. Each quest must have a unique ID.");
        }

        // Trader ID
        if (string.IsNullOrWhiteSpace(quest.TraderId))
        {
            errors.Add($"{prefix}: 'traderId' is required. It should match your trader's ID.");
        }
        else if (quest.TraderId != traderId)
        {
            errors.Add($"{prefix}: 'traderId' ({quest.TraderId}) doesn't match the trader pack's trader ID ({traderId}).");
        }

        // Name & description
        if (string.IsNullOrWhiteSpace(quest.Name))
            errors.Add($"{prefix}: 'name' is required. This is shown in the quest log.");
        if (string.IsNullOrWhiteSpace(quest.Description))
            errors.Add($"{prefix}: 'description' is required. This is shown when accepting the quest.");

        // Location
        if (!string.IsNullOrWhiteSpace(quest.Location) && !ValidLocations.Contains(quest.Location))
            errors.Add($"{prefix}: Invalid 'location': '{quest.Location}'. Valid: {string.Join(", ", ValidLocations)}");

        // Requirements
        if (quest.Requirements.PlayerLevel < 1)
            errors.Add($"{prefix}: requirements.playerLevel must be >= 1.");
        if (quest.Requirements.PreviousQuest != null)
        {
            if (quest.Requirements.PreviousQuest.Length != 24 || !IsHexString(quest.Requirements.PreviousQuest))
                errors.Add($"{prefix}: requirements.previousQuest must be a 24-character hex string. Got: '{quest.Requirements.PreviousQuest}'");
        }

        // Objectives
        if (quest.Objectives.Count == 0)
        {
            errors.Add($"{prefix}: At least one objective is required.");
        }
        else
        {
            for (var j = 0; j < quest.Objectives.Count; j++)
            {
                ValidateObjective(quest.Objectives[j], $"{prefix}.objectives[{j}]", errors);
            }
        }

        // Rewards
        ValidateRewards(quest.Rewards, $"{prefix}.rewards", errors);
    }

    private static void ValidateObjective(QuestObjective obj, string prefix, List<string> errors)
    {
        if (!ValidObjectiveTypes.Contains(obj.Type))
        {
            errors.Add($"{prefix}: Invalid objective 'type': '{obj.Type}'. " +
                        $"Valid: {string.Join(", ", ValidObjectiveTypes)}");
            return;
        }

        if (obj.Count < 1)
            errors.Add($"{prefix}: 'count' must be >= 1.");

        switch (obj.Type.ToLowerInvariant())
        {
            case "handover_item":
            case "handover_fir_item":
                if (string.IsNullOrWhiteSpace(obj.ItemTpl))
                    errors.Add($"{prefix}: 'itemTpl' is required for {obj.Type} objectives.");
                else if (obj.ItemTpl.Length != 24 || !IsHexString(obj.ItemTpl))
                    errors.Add($"{prefix}: 'itemTpl' must be a 24-character hex string. Got: '{obj.ItemTpl}'");
                break;

            case "kill_enemy":
                if (string.IsNullOrWhiteSpace(obj.Target))
                    errors.Add($"{prefix}: 'target' is required for kill_enemy objectives. Examples: Savage, AnyPmc, exUsec");
                else if (!ValidEnemyTargets.Contains(obj.Target))
                    errors.Add($"{prefix}: Invalid 'target': '{obj.Target}'. Valid: {string.Join(", ", ValidEnemyTargets)}");
                break;

            case "survive_location":
            case "extract_location":
                if (string.IsNullOrWhiteSpace(obj.Location))
                    errors.Add($"{prefix}: 'location' is required for {obj.Type} objectives.");
                else if (!ValidLocations.Contains(obj.Location) || obj.Location.Equals("any", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{prefix}: '{obj.Type}' requires a specific location, not 'any'. Valid: {string.Join(", ", ValidLocations.Where(l => !l.Equals("any", StringComparison.OrdinalIgnoreCase)))}");
                break;
        }

        if (obj.Location != null && !ValidLocations.Contains(obj.Location))
            errors.Add($"{prefix}: Invalid 'location': '{obj.Location}'. Valid: {string.Join(", ", ValidLocations)}");
    }

    private static void ValidateRewards(QuestRewards rewards, string prefix, List<string> errors)
    {
        if (rewards.Xp < 0)
            errors.Add($"{prefix}: 'xp' cannot be negative.");

        if (rewards.Money != null)
        {
            if (!CurrencyHelper.IsValid(rewards.Money.Currency))
                errors.Add($"{prefix}: money.currency is invalid: '{rewards.Money.Currency}'. Use RUB, USD, or EUR.");
            if (rewards.Money.Amount < 0)
                errors.Add($"{prefix}: money.amount cannot be negative.");
        }

        for (var i = 0; i < rewards.Items.Count; i++)
        {
            var item = rewards.Items[i];
            if (string.IsNullOrWhiteSpace(item.ItemTpl))
                errors.Add($"{prefix}.items[{i}]: 'itemTpl' is required.");
            else if (item.ItemTpl.Length != 24 || !IsHexString(item.ItemTpl))
                errors.Add($"{prefix}.items[{i}]: 'itemTpl' must be a 24-character hex string. Got: '{item.ItemTpl}'");
            if (item.Count < 1)
                errors.Add($"{prefix}.items[{i}]: 'count' must be >= 1.");
        }

        if (rewards.TraderStanding < 0)
            errors.Add($"{prefix}: 'traderStanding' cannot be negative.");
    }

    private static void ValidateRotatingTemplate(
        RotatingQuestTemplate template,
        string traderId,
        string prefix,
        HashSet<string> seenIds,
        List<string> errors)
    {
        // ID
        if (string.IsNullOrWhiteSpace(template.Id))
            errors.Add($"{prefix}: 'id' is required. Use a unique 24-character hex string.");
        else
        {
            if (template.Id.Length != 24 || !IsHexString(template.Id))
                errors.Add($"{prefix}: 'id' must be a 24-character hex string. Got: '{template.Id}'");
            if (!seenIds.Add(template.Id))
                errors.Add($"{prefix}: Duplicate template ID '{template.Id}'.");
        }

        // Rotation
        if (!ValidRotations.Contains(template.Rotation))
            errors.Add($"{prefix}: Invalid 'rotation': '{template.Rotation}'. Use 'daily' or 'weekly'.");

        // Name pool
        if (template.NamePool.Count == 0)
            errors.Add($"{prefix}: 'namePool' must contain at least one name.");

        // Objectives
        if (template.Objectives.Count == 0)
        {
            errors.Add($"{prefix}: At least one objective template is required.");
        }
        else
        {
            for (var j = 0; j < template.Objectives.Count; j++)
            {
                var obj = template.Objectives[j];
                var oPrefix = $"{prefix}.objectives[{j}]";

                if (!ValidObjectiveTypes.Contains(obj.Type))
                    errors.Add($"{oPrefix}: Invalid objective 'type': '{obj.Type}'. Valid: {string.Join(", ", ValidObjectiveTypes)}");

                if (obj.CountRange.Min < 1)
                    errors.Add($"{oPrefix}: countRange.min must be >= 1.");
                if (obj.CountRange.Max < obj.CountRange.Min)
                    errors.Add($"{oPrefix}: countRange.max ({obj.CountRange.Max}) must be >= countRange.min ({obj.CountRange.Min}).");

                switch (obj.Type.ToLowerInvariant())
                {
                    case "kill_enemy":
                        if (obj.TargetPool.Count == 0)
                            errors.Add($"{oPrefix}: 'targetPool' is required for kill_enemy templates.");
                        else
                        {
                            foreach (var t in obj.TargetPool.Where(t => !ValidEnemyTargets.Contains(t)))
                                errors.Add($"{oPrefix}: Invalid target in targetPool: '{t}'.");
                        }
                        break;

                    case "handover_item":
                    case "handover_fir_item":
                        if (obj.ItemPool.Count == 0)
                            errors.Add($"{oPrefix}: 'itemPool' is required for handover templates.");
                        else
                        {
                            foreach (var item in obj.ItemPool.Where(item => item.Length != 24 || !IsHexString(item)))
                                errors.Add($"{oPrefix}: Invalid item template ID in itemPool: '{item}'.");
                        }
                        break;

                    case "survive_location":
                    case "extract_location":
                        if (obj.LocationPool.Count == 0)
                            errors.Add($"{oPrefix}: 'locationPool' is required for {obj.Type} templates.");
                        break;
                }

                foreach (var loc in obj.LocationPool.Where(loc => !ValidLocations.Contains(loc) || loc.Equals("any", StringComparison.OrdinalIgnoreCase)))
                    errors.Add($"{oPrefix}: Invalid location in locationPool: '{loc}'.");
            }
        }

        // Quest count
        if (template.QuestCount < 1)
            errors.Add($"{prefix}: 'questCount' must be >= 1.");

        // Reward scaling
        if (template.RewardScaling.XpPerObjectiveCount < 0)
            errors.Add($"{prefix}: rewardScaling.xpPerObjectiveCount cannot be negative.");
        if (template.RewardScaling.BaseMoney < 0)
            errors.Add($"{prefix}: rewardScaling.baseMoney cannot be negative.");
        if (!CurrencyHelper.IsValid(template.RewardScaling.Currency))
            errors.Add($"{prefix}: rewardScaling.currency is invalid. Use RUB, USD, or EUR.");
    }

    private static bool IsHexString(string s) => s.All(c => Uri.IsHexDigit(c));
}
