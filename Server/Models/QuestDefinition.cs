using System.Text.Json.Serialization;

namespace TraderGen.Models;

// Root model for a quest pack JSON file (quests.json).
// Users write this simplified format and TraderGen translates it to BSG quest objects.
public class QuestPackDefinition
{
    // Default quest icon for all quests in this pack (relative path from pack folder, e.g. "assets/quest_icon.png").
    // Individual quests can override this with their own "image" field.
    [JsonPropertyName("defaultQuestIcon")]
    public string? DefaultQuestIcon { get; set; }

    // List of story quests — static quests with fixed objectives, chaining, and rewards.
    [JsonPropertyName("storyQuests")]
    public List<StoryQuestDefinition> StoryQuests { get; set; } = [];

    // List of rotating quest templates — used to generate daily/weekly quests at server start.
    [JsonPropertyName("rotatingQuests")]
    public List<RotatingQuestTemplate> RotatingQuests { get; set; } = [];
}

// A single story quest with fixed objectives and rewards.
public class StoryQuestDefinition
{
    // Unique quest ID. Must be a 24-character hex string.
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // The trader ID that gives this quest. Must match the trader pack's trader ID.
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = string.Empty;

    // Display name shown in the quest log.
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    // Quest description shown when accepting.
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    // Message shown when the quest is completed successfully.
    [JsonPropertyName("successMessage")]
    public string SuccessMessage { get; set; } = "Good work. Come back when you're ready.";

    // Message shown when the quest becomes available.
    [JsonPropertyName("startedMessage")]
    public string StartedMessage { get; set; } = "Get it done.";

    // Quest icon image (relative path from pack folder, e.g. "assets/quest_icon.png").
    // If not set, falls back to the pack's defaultQuestIcon or a generated placeholder.
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    // Map location ID for this quest (use "any" for no location restriction).
    [JsonPropertyName("location")]
    public string Location { get; set; } = "any";

    // Requirements to unlock this quest.
    [JsonPropertyName("requirements")]
    public QuestRequirements Requirements { get; set; } = new();

    // List of objectives the player must complete.
    [JsonPropertyName("objectives")]
    public List<QuestObjective> Objectives { get; set; } = [];

    // Rewards given on successful completion.
    [JsonPropertyName("rewards")]
    public QuestRewards Rewards { get; set; } = new();
}

// Requirements to unlock a quest (AvailableForStart conditions).
public class QuestRequirements
{
    // Minimum player level required. Set to 1 for no level gate.
    [JsonPropertyName("playerLevel")]
    public int PlayerLevel { get; set; } = 1;

    // ID of a quest that must be completed first. Null = no prerequisite.
    [JsonPropertyName("previousQuest")]
    public string? PreviousQuest { get; set; }
}

// A single quest objective the player must complete.
public class QuestObjective
{
    // The type of objective. Supported: handover_item, handover_fir_item, kill_enemy, survive_location, extract_location
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // --- handover_item / handover_fir_item fields ---

    // Item template ID to hand over (24-char hex).
    [JsonPropertyName("itemTpl")]
    public string? ItemTpl { get; set; }

    // --- kill_enemy fields ---

    // Enemy target type: "Savage" (Scav), "AnyPmc", "Any", or a specific bot role like "exUsec", "pmcBot", etc.
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    // --- Shared fields ---

    // How many (kills, items, etc.) are required.
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    // Map location ID for this specific objective. Null = any location.
    // Use BSG location IDs: "bigmap" (Customs), "factory4_day", "Woods", "Shoreline",
    // "Interchange", "Lighthouse", "Reserve", "laboratory", "TarkovStreets", "Sandbox", etc.
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    // Optional: description override for this objective shown in the quest log.
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    // If true, the game shows a progress counter (e.g. "3/15") in the quest UI.
    [JsonPropertyName("useAutoCounter")]
    public bool UseAutoCounter { get; set; } = true;

    // --- Advanced kill condition fields (all optional) ---

    // Minimum kill distance in meters (e.g. 40 for "from 40m+").
    [JsonPropertyName("minDistance")]
    public int? MinDistance { get; set; }

    // Maximum kill distance in meters.
    [JsonPropertyName("maxDistance")]
    public int? MaxDistance { get; set; }

    // List of weapon template IDs that must be used for the kill.
    [JsonPropertyName("weaponTpls")]
    public List<string>? WeaponTpls { get; set; }

    // Weapon categories (e.g. "AssaultRifle", "SMG"). NOT YET IMPLEMENTED — reserved for future use.
    [JsonPropertyName("weaponCategories")]
    public List<string>? WeaponCategories { get; set; }

    // Item template IDs the player must be wearing when making the kill.
    [JsonPropertyName("wearing")]
    public List<string>? Wearing { get; set; }

    // Item template IDs the player must NOT be wearing when making the kill.
    [JsonPropertyName("notWearing")]
    public List<string>? NotWearing { get; set; }

    // In-game hour the kill window starts (0-23). Use with timeTo for night-only quests.
    [JsonPropertyName("timeFrom")]
    public int? TimeFrom { get; set; }

    // In-game hour the kill window ends (0-23).
    [JsonPropertyName("timeTo")]
    public int? TimeTo { get; set; }

    // Body parts that must be hit (e.g. "Head", "Chest"). If empty, any body part counts.
    [JsonPropertyName("bodyPart")]
    public List<string>? BodyPart { get; set; }

    // If true, the player must survive and extract after completing kills.
    // NOT YET IMPLEMENTED — add a separate survive/extract objective instead.
    [JsonPropertyName("surviveAfterKill")]
    public bool? SurviveAfterKill { get; set; }

    // Required extract name for survive/extract objectives (e.g. "Factory gate 0").
    // For kill objectives this has no effect.
    [JsonPropertyName("requiredExtract")]
    public string? RequiredExtract { get; set; }
}

// Rewards given when the quest is completed successfully.
public class QuestRewards
{
    // Experience points awarded.
    [JsonPropertyName("xp")]
    public int Xp { get; set; } = 0;

    // Money reward. Null = no money reward.
    [JsonPropertyName("money")]
    public MoneyReward? Money { get; set; }

    // Item rewards given on completion.
    [JsonPropertyName("items")]
    public List<ItemReward> Items { get; set; } = [];

    // Standing increase with the quest's trader.
    [JsonPropertyName("traderStanding")]
    public double TraderStanding { get; set; } = 0;

    // Assort items that become unlocked after completing this quest.
    // Each entry is an assort item ID from the trader's assort list.
    [JsonPropertyName("unlockAssortItems")]
    public List<string> UnlockAssortItems { get; set; } = [];
}

// A money reward (currency + amount).
public class MoneyReward
{
    // Currency type: "RUB", "USD", or "EUR".
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    // Amount of money.
    [JsonPropertyName("amount")]
    public int Amount { get; set; } = 0;
}

// An item reward given on quest completion.
public class ItemReward
{
    // Item template ID (24-char hex).
    [JsonPropertyName("itemTpl")]
    public string ItemTpl { get; set; } = string.Empty;

    // How many of this item to give.
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

// ==================== Rotating Quest Templates ====================

// A template used to generate daily/weekly rotating quests at server startup.
// The traderId is inherited from the trader pack and not specified per-template.
public class RotatingQuestTemplate
{
    // Unique template ID (24-char hex). Used to track and identify this template.
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Rotation type: "daily" or "weekly".
    [JsonPropertyName("rotation")]
    public string Rotation { get; set; } = "daily";

    // Pool of possible quest names. Use {location} placeholder to insert the map name.
    [JsonPropertyName("namePool")]
    public List<string> NamePool { get; set; } = [];

    // Pool of possible quest descriptions. Use {location} placeholder to insert the map name.
    [JsonPropertyName("descriptionPool")]
    public List<string> DescriptionPool { get; set; } = [];

    // Objective templates used to generate random objectives.
    [JsonPropertyName("objectives")]
    public List<RotatingObjectiveTemplate> Objectives { get; set; } = [];

    // How rewards scale based on objective difficulty.
    [JsonPropertyName("rewardScaling")]
    public RewardScaling RewardScaling { get; set; } = new();

    // Quest icon image (relative path from pack folder, e.g. "assets/tpl_abc123.jpg").
    // If not set, falls back to the default repeatable quest icon.
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    // How many quests to generate from this template at server start. Default 1.
    [JsonPropertyName("questCount")]
    public int QuestCount { get; set; } = 1;
}

// A template for generating random objectives in rotating quests.
public class RotatingObjectiveTemplate
{
    // Objective type: handover_item, handover_fir_item, kill_enemy, survive_location, extract_location.
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // --- kill_enemy fields ---

    // Pool of possible enemy targets to pick from randomly.
    [JsonPropertyName("targetPool")]
    public List<string> TargetPool { get; set; } = [];

    // --- Shared location fields ---

    // Pool of map location IDs to pick from randomly.
    [JsonPropertyName("locationPool")]
    public List<string> LocationPool { get; set; } = [];

    // --- handover fields ---

    // Pool of item template IDs to pick from randomly (for handover objectives).
    [JsonPropertyName("itemPool")]
    public List<string> ItemPool { get; set; } = [];

    // Whether handed-over items must be found in raid (for handover objectives).
    [JsonPropertyName("foundInRaid")]
    public bool FoundInRaid { get; set; } = false;

    // --- Count range ---

    // Random count range for the objective.
    [JsonPropertyName("countRange")]
    public CountRange CountRange { get; set; } = new();

    // --- Advanced condition fields (all optional) ---

    [JsonPropertyName("minDistance")]
    public int? MinDistance { get; set; }

    [JsonPropertyName("maxDistance")]
    public int? MaxDistance { get; set; }

    [JsonPropertyName("weaponTpls")]
    public List<string>? WeaponTpls { get; set; }

    [JsonPropertyName("weaponCategories")]
    public List<string>? WeaponCategories { get; set; }

    [JsonPropertyName("wearing")]
    public List<string>? Wearing { get; set; }

    [JsonPropertyName("notWearing")]
    public List<string>? NotWearing { get; set; }

    [JsonPropertyName("timeFrom")]
    public int? TimeFrom { get; set; }

    [JsonPropertyName("timeTo")]
    public int? TimeTo { get; set; }

    [JsonPropertyName("bodyPart")]
    public List<string>? BodyPart { get; set; }

    [JsonPropertyName("surviveAfterKill")]
    public bool? SurviveAfterKill { get; set; }

    [JsonPropertyName("requiredExtract")]
    public string? RequiredExtract { get; set; }
}

// How rewards scale based on the generated objective values.
public class RewardScaling
{
    // XP given per unit of objective count (e.g. per kill).
    [JsonPropertyName("xpPerObjectiveCount")]
    public int XpPerObjectiveCount { get; set; } = 400;

    // Base money reward (RUB) before scaling.
    [JsonPropertyName("baseMoney")]
    public int BaseMoney { get; set; } = 10000;

    // Additional money per unit of objective count.
    [JsonPropertyName("moneyPerObjectiveCount")]
    public int MoneyPerObjectiveCount { get; set; } = 2500;

    // Currency for money reward.
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    // Trader standing increase.
    [JsonPropertyName("standing")]
    public double Standing { get; set; } = 0.01;
}

// Min/max range for random count generation.
public class CountRange
{
    [JsonPropertyName("min")]
    public int Min { get; set; } = 1;

    [JsonPropertyName("max")]
    public int Max { get; set; } = 5;
}

