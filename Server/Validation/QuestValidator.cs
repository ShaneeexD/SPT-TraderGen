using TraderGen.Models;

namespace TraderGen.Validation;

// Validates quest definitions and returns clear, actionable error messages for non-programmers.
public static class QuestValidator
{
    private static readonly HashSet<string> ValidObjectiveTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "handover_item",
        "handover_fir_item",
        "find_item",
        "leave_item_at_location",
        "kill_enemy",
        "survive_location",
        "extract_location",
        "zone_visit",
        "zone_kill",
        "zone_place_item",
    };

    private static readonly HashSet<string> ValidZoneTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "visit", "placeitem", "botkillzone", "flarezone", "salvage", // note: use "visit" for kill zone quests, not "botkillzone"
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
        "factory4",       // Factory (Day or Night)
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

        // Validate zone definitions
        var seenZoneIds = new HashSet<string>();
        for (var i = 0; i < pack.Zones.Count; i++)
        {
            var zone = pack.Zones[i];
            var zPrefix = $"{prefix} zones[{i}]";
            if (string.IsNullOrWhiteSpace(zone.ZoneId))
                errors.Add($"{zPrefix}: 'zoneId' is required.");
            else if (!seenZoneIds.Add(zone.ZoneId))
                errors.Add($"{zPrefix}: Duplicate zoneId '{zone.ZoneId}'.");
            if (string.IsNullOrWhiteSpace(zone.ZoneLocation))
                errors.Add($"{zPrefix}: 'zoneLocation' is required.");
            if (!ValidZoneTypes.Contains(zone.ZoneType))
                errors.Add($"{zPrefix}: Invalid 'zoneType' '{zone.ZoneType}'. Valid: {string.Join(", ", ValidZoneTypes)}");
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
            case "find_item":
                if (string.IsNullOrWhiteSpace(obj.ItemTpl))
                    errors.Add($"{prefix}: 'itemTpl' is required for {obj.Type} objectives.");
                else if (obj.ItemTpl.Length != 24 || !IsHexString(obj.ItemTpl))
                    errors.Add($"{prefix}: 'itemTpl' must be a 24-character hex string. Got: '{obj.ItemTpl}'");
                break;

            case "leave_item_at_location":
                if (string.IsNullOrWhiteSpace(obj.ItemTpl))
                    errors.Add($"{prefix}: 'itemTpl' is required for {obj.Type} objectives.");
                else if (obj.ItemTpl.Length != 24 || !IsHexString(obj.ItemTpl))
                    errors.Add($"{prefix}: 'itemTpl' must be a 24-character hex string. Got: '{obj.ItemTpl}'");
                if (string.IsNullOrWhiteSpace(obj.ZoneId))
                    errors.Add($"{prefix}: 'zoneId' is required for {obj.Type} objectives.");
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

            case "zone_visit":
            case "zone_kill":
            case "zone_place_item":
                if (string.IsNullOrWhiteSpace(obj.ZoneId))
                    errors.Add($"{prefix}: 'zoneId' is required for {obj.Type} objectives.");
                if (obj.Type.Equals("zone_kill", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(obj.Target))
                    errors.Add($"{prefix}: 'target' is required for zone_kill objectives.");
                if (obj.Type.Equals("zone_place_item", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(obj.PlantItemTpl))
                    errors.Add($"{prefix}: 'plantItemTpl' is required for zone_place_item objectives.");
                break;
        }

        if (obj.Location != null && !ValidLocations.Contains(obj.Location))
            errors.Add($"{prefix}: Invalid 'location': '{obj.Location}'. Valid: {string.Join(", ", ValidLocations)}");

        // --- Advanced kill condition validation ---

        if (obj.MinDistance.HasValue && obj.MinDistance.Value < 0)
            errors.Add($"{prefix}: 'minDistance' must be >= 0.");

        if (obj.MaxDistance.HasValue && obj.MaxDistance.Value < 0)
            errors.Add($"{prefix}: 'maxDistance' must be >= 0.");

        if (obj.MinDistance.HasValue && obj.MaxDistance.HasValue)
            errors.Add($"{prefix}: Cannot set both 'minDistance' and 'maxDistance'. Use one or the other.");

        if (obj.TimeFrom.HasValue && (obj.TimeFrom.Value < 0 || obj.TimeFrom.Value > 23))
            errors.Add($"{prefix}: 'timeFrom' must be between 0 and 23.");

        if (obj.TimeTo.HasValue && (obj.TimeTo.Value < 0 || obj.TimeTo.Value > 23))
            errors.Add($"{prefix}: 'timeTo' must be between 0 and 23.");

        if (obj.WeaponTpls != null)
        {
            for (var wi = 0; wi < obj.WeaponTpls.Count; wi++)
            {
                var wtpl = obj.WeaponTpls[wi];
                if (string.IsNullOrWhiteSpace(wtpl) || wtpl.Length != 24 || !IsHexString(wtpl))
                    errors.Add($"{prefix}: 'weaponTpls[{wi}]' must be a 24-character hex string. Got: '{wtpl}'");
            }
        }

        if (obj.Wearing != null)
        {
            for (var wi = 0; wi < obj.Wearing.Count; wi++)
            {
                var wtpl = obj.Wearing[wi];
                if (string.IsNullOrWhiteSpace(wtpl) || wtpl.Length != 24 || !IsHexString(wtpl))
                    errors.Add($"{prefix}: 'wearing[{wi}]' must be a 24-character hex string. Got: '{wtpl}'");
            }
        }

        if (obj.NotWearing != null)
        {
            for (var wi = 0; wi < obj.NotWearing.Count; wi++)
            {
                var wtpl = obj.NotWearing[wi];
                if (string.IsNullOrWhiteSpace(wtpl) || wtpl.Length != 24 || !IsHexString(wtpl))
                    errors.Add($"{prefix}: 'notWearing[{wi}]' must be a 24-character hex string. Got: '{wtpl}'");
            }
        }

        if (obj.BodyPart != null)
        {
            for (var bi = 0; bi < obj.BodyPart.Count; bi++)
            {
                if (string.IsNullOrWhiteSpace(obj.BodyPart[bi]))
                    errors.Add($"{prefix}: 'bodyPart[{bi}]' cannot be empty.");
            }
        }
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

            // Validate child attachments on reward items
            if (item.Children != null)
            {
                ValidateRewardChildren(item.Children, $"{prefix}.items[{i}]", errors);
            }
        }

        if (rewards.TraderStanding < 0)
            errors.Add($"{prefix}: 'traderStanding' cannot be negative.");

        if (rewards.StashRows < 0)
            errors.Add($"{prefix}: 'stashRows' cannot be negative.");

        for (var si = 0; si < rewards.Skills.Count; si++)
        {
            var skill = rewards.Skills[si];
            if (string.IsNullOrWhiteSpace(skill.Name))
                errors.Add($"{prefix}.skills[{si}]: 'name' is required.");
            if (skill.Points < 1)
                errors.Add($"{prefix}.skills[{si}]: 'points' must be >= 1.");
        }

        if (!string.IsNullOrWhiteSpace(rewards.Pockets) && (rewards.Pockets.Length != 24 || !IsHexString(rewards.Pockets)))
            errors.Add($"{prefix}: 'pockets' must be a 24-character hex string. Got: '{rewards.Pockets}'");

        for (var i = 0; i < rewards.UnlockAssortItems.Count; i++)
        {
            var id = rewards.UnlockAssortItems[i];
            if (string.IsNullOrWhiteSpace(id) || id.Length != 24 || !IsHexString(id))
                errors.Add($"{prefix}.unlockAssortItems[{i}]: must be a 24-character hex string. Got: '{id}'");
        }

        for (var i = 0; i < rewards.Recipes.Count; i++)
        {
            var id = rewards.Recipes[i];
            if (string.IsNullOrWhiteSpace(id) || id.Length != 24 || !IsHexString(id))
                errors.Add($"{prefix}.recipes[{i}]: must be a 24-character hex string. Got: '{id}'");
        }
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
                    case "find_item":
                        if (obj.ItemPool.Count == 0)
                            errors.Add($"{oPrefix}: 'itemPool' is required for {obj.Type} templates.");
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

    private static void ValidateRewardChildren(List<AssortChildItem> children, string prefix, List<string> errors)
    {
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var childPrefix = $"{prefix}.children[{i}]";

            if (string.IsNullOrWhiteSpace(child.ItemTpl))
                errors.Add($"{childPrefix}: 'itemTpl' is required.");
            else if (child.ItemTpl.Length != 24 || !IsHexString(child.ItemTpl))
                errors.Add($"{childPrefix}: 'itemTpl' must be a 24-character hex string. Got: '{child.ItemTpl}'");

            if (string.IsNullOrWhiteSpace(child.SlotId))
                errors.Add($"{childPrefix}: 'slotId' is required.");

            if (child.Children != null)
            {
                ValidateRewardChildren(child.Children, childPrefix, errors);
            }
        }
    }

    private static bool IsHexString(string s) => s.All(c => Uri.IsHexDigit(c));
}
