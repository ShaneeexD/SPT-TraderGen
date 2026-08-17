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

    // Custom quest zones defined for this pack. Registered via WTT-CommonLib on startup.
    [JsonPropertyName("zones")]
    public List<QuestZoneDefinition> Zones { get; set; } = [];

    // Custom hideout production schemes added to the SPT database.
    [JsonPropertyName("productionSchemes")]
    public List<ProductionSchemeDefinition> ProductionSchemes { get; set; } = [];
}

public class ProductionSchemeDefinition
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    // Hideout area index (e.g. 2 = Lavatory, 7 = Nutrition).
    [JsonPropertyName("areaType")]
    public int AreaType { get; set; }

    // Item template produced by this scheme.
    [JsonPropertyName("endProduct")]
    public string EndProduct { get; set; } = string.Empty;

    // Number of end products created per craft.
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    // Craft duration in seconds.
    [JsonPropertyName("productionTime")]
    public double ProductionTime { get; set; }

    [JsonPropertyName("needFuelForAllProductionTime")]
    public bool NeedFuelForAllProductionTime { get; set; }

    // If false, the scheme is locked by default and unlocked by quests that reward it.
    [JsonPropertyName("unlockedByDefault")]
    public bool UnlockedByDefault { get; set; }

    // If true, the craft runs continuously (e.g. water/fuel).
    [JsonPropertyName("continuous")]
    public bool Continuous { get; set; }

    // Maximum number of crafts that can be queued, 0 for unlimited.
    [JsonPropertyName("productionLimitCount")]
    public int ProductionLimitCount { get; set; }

    [JsonPropertyName("requirements")]
    public List<SchemeRequirement> Requirements { get; set; } = [];
}

public class SchemeRequirement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Item";

    [JsonPropertyName("templateId")]
    public string? TemplateId { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("areaType")]
    public int? AreaType { get; set; }

    [JsonPropertyName("requiredLevel")]
    public int? RequiredLevel { get; set; }

    [JsonPropertyName("resource")]
    public int? Resource { get; set; }

    [JsonPropertyName("isEncoded")]
    public bool? IsEncoded { get; set; }

    [JsonPropertyName("isFunctional")]
    public bool? IsFunctional { get; set; }

    [JsonPropertyName("isSpawnedInSession")]
    public bool? IsSpawnedInSession { get; set; }
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
    // The type of objective. Supported: handover_item, handover_fir_item, find_item, leave_item_at_location, kill_enemy, survive_location, extract_location
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    // --- handover_item / handover_fir_item / find_item fields ---

    // Item template ID to hand over or find (24-char hex).
    [JsonPropertyName("itemTpl")]
    public string? ItemTpl { get; set; }

    // --- find_item fields ---

    // If true (default), a paired HandoverItem condition is generated after the FindItem condition.
    // The handover only becomes visible once the FindItem condition is complete.
    [JsonPropertyName("handoverAfterFind")]
    public bool HandoverAfterFind { get; set; } = true;

    // If true, only found-in-raid items count toward the counter.
    [JsonPropertyName("countInRaid")]
    public bool CountInRaid { get; set; } = false;

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

    // Required extract name for survive/extract objectives (e.g. "Factory gate 0").
    // For kill objectives this has no effect.
    [JsonPropertyName("requiredExtract")]
    public string? RequiredExtract { get; set; }

    // If true, objective progress resets between raid sessions.
    // Use for "kill X in one raid" style objectives. Default false.
    [JsonPropertyName("oneSessionOnly")]
    public bool OneSessionOnly { get; set; } = false;

    // --- leave_item_at_location / zone_visit / zone_place_item / zone_kill fields ---

    // Zone ID referencing a zone defined in this quest pack's zones list.
    [JsonPropertyName("zoneId")]
    public string? ZoneId { get; set; }

    // Time in seconds the player must remain in the zone (for zone_visit). Default 0 = instant.
    [JsonPropertyName("plantTime")]
    public int? PlantTime { get; set; }

    // Item template ID to place at the zone (for zone_place_item).
    [JsonPropertyName("plantItemTpl")]
    public string? PlantItemTpl { get; set; }
}

// Rewards given when the quest is completed successfully.
public class QuestRewards
{
    // Experience points awarded.
    [JsonPropertyName("xp")]
    public int Xp { get; set; } = 0;

    // Hide XP reward from the player until the quest is ready to hand in.
    [JsonPropertyName("xpHidden")]
    public bool? XpHidden { get; set; }

    // Money reward. Null = no money reward.
    [JsonPropertyName("money")]
    public MoneyReward? Money { get; set; }

    // Item rewards given on completion.
    [JsonPropertyName("items")]
    public List<ItemReward> Items { get; set; } = [];

    // Items given immediately when the player accepts the quest.
    [JsonPropertyName("initialEquipment")]
    public List<ItemReward> InitialEquipment { get; set; } = [];

    // Standing increase with the quest's trader.
    [JsonPropertyName("traderStanding")]
    public double TraderStanding { get; set; } = 0;

    // Hide trader standing reward from the player until the quest is ready to hand in.
    [JsonPropertyName("traderStandingHidden")]
    public bool? TraderStandingHidden { get; set; }

    // Assort items that become unlocked after completing this quest.
    // Each entry is an assort item ID from the trader's assort list.
    [JsonPropertyName("unlockAssortItems")]
    public List<string> UnlockAssortItems { get; set; } = [];

    // Parallel to unlockAssortItems: if true at index i, that assort unlock is hidden
    // from the player until the quest is ready to hand in.
    [JsonPropertyName("hiddenAssortItems")]
    public List<bool>? HiddenAssortItems { get; set; }

    // Crafting recipe (production scheme) IDs unlocked on completion.
    [JsonPropertyName("recipes")]
    public List<string> Recipes { get; set; } = [];

    // Parallel to recipes: if true at index i, that recipe unlock is hidden
    // from the player until the quest is ready to hand in.
    [JsonPropertyName("hiddenRecipes")]
    public List<bool>? HiddenRecipes { get; set; }

    // Number of stash rows to add on completion. Requires client restart.
    [JsonPropertyName("stashRows")]
    public int StashRows { get; set; } = 0;

    // Hide stash rows reward from the player until the quest is ready to hand in.
    [JsonPropertyName("stashRowsHidden")]
    public bool? StashRowsHidden { get; set; }

    // Skill point rewards on completion. Each entry gives skill points to a specific skill.
    // Use skill names like "Endurance", "Strength", "Vitality", etc.
    [JsonPropertyName("skills")]
    public List<SkillReward> Skills { get; set; } = [];

    // Pocket template ID to upgrade the player's pockets to.
    // Common values: "557ffd194bdc2d3a0f6c6c84" (2x2), "627a4e6b8792715e648a5e36" (2x3).
    [JsonPropertyName("pockets")]
    public string? Pockets { get; set; }

    // Hide pocket upgrade reward from the player until the quest is ready to hand in.
    [JsonPropertyName("pocketsHidden")]
    public bool? PocketsHidden { get; set; }

    // Custom pocket definition — the server will generate a template ID and inject it.
    [JsonPropertyName("customPocket")]
    public CustomPocketDefinition? CustomPocket { get; set; }

    // Random item pool rewards. Each pool is a set of weighted items from which
    // one entry is randomly selected when the quest is completed. The reward is
    // shown as "Random Item" (with a custom icon) while the quest is in progress,
    // and the actual item is rolled and granted on completion.
    [JsonPropertyName("randomItemPools")]
    public List<RandomItemPool> RandomItemPools { get; set; } = [];
}

// A random item pool — a set of weighted entries from which one is chosen at quest completion.
public class RandomItemPool
{
    // The pool entries. One entry is selected at random based on weights.
    [JsonPropertyName("entries")]
    public List<RandomItemPoolEntry> Entries { get; set; } = [];
}

// A single entry in a random item pool.
public class RandomItemPoolEntry
{
    // Item template ID (24-char hex).
    [JsonPropertyName("itemTpl")]
    public string ItemTpl { get; set; } = string.Empty;

    // How many of this item to give if selected.
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    // Weight for random selection. Higher = more likely. Default 1.
    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 1;

    // Child items attached to this entry (e.g. weapon attachments, armour plates).
    // Uses the same structure as assort child items.
    [JsonPropertyName("children")]
    public List<AssortChildItem>? Children { get; set; }
}

// Custom pocket slot definition.
public class CustomPocketSlot
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 1;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 2;
}

// Custom pocket definition — used to generate a new pocket template on the fly.
public class CustomPocketDefinition
{
    [JsonPropertyName("slots")]
    public List<CustomPocketSlot> Slots { get; set; } = [];
}

// Skill point reward given on quest completion.
public class SkillReward
{
    // Skill name. Use SPT skill enum values: Endurance, Strength, Vitality, Health,
    // StressResistance, Metabolism, Immunity, Perception, Intellect, Attention, Charisma,
    // Memory, MagDrills, RecoilControl, CovertMovement, etc.
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    // Points to add. In BSG, 100 points = +1 level. Use 100 for a full level.
    [JsonPropertyName("points")]
    public int Points { get; set; } = 100;

    // Hide this skill reward from the player until the quest is ready to hand in.
    [JsonPropertyName("hidden")]
    public bool? Hidden { get; set; }
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

    // Hide this money reward from the player until the quest is ready to hand in.
    [JsonPropertyName("hidden")]
    public bool? Hidden { get; set; }
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

    // Child items attached to this reward item (e.g. weapon attachments, armour plates).
    // Uses the same structure as assort child items.
    [JsonPropertyName("children")]
    public List<AssortChildItem>? Children { get; set; }

    // Hide this item reward from the player until the quest is ready to hand in.
    [JsonPropertyName("hidden")]
    public bool? Hidden { get; set; }
}

// ==================== Zone Definitions ====================

// A custom quest zone registered with WTT-CommonLib's CustomQuestZoneService.
// Zones are physical trigger volumes placed in the game world.
public class QuestZoneDefinition
{
    // Unique zone ID. Referenced by zone_visit / zone_place_item / zone_kill objectives.
    [JsonPropertyName("zoneId")]
    public string ZoneId { get; set; } = string.Empty;

    // Display name for the zone (used internally).
    [JsonPropertyName("zoneName")]
    public string ZoneName { get; set; } = string.Empty;

    // Map location ID (lowercase, e.g. "woods", "bigmap", "shoreline").
    [JsonPropertyName("zoneLocation")]
    public string ZoneLocation { get; set; } = string.Empty;

    // Zone type: "visit", "placeitem", "transition", "flare", "salvagehint".
    [JsonPropertyName("zoneType")]
    public string ZoneType { get; set; } = "visit";

    // Flare type (only for flare zones, leave empty otherwise).
    [JsonPropertyName("flareType")]
    public string FlareType { get; set; } = string.Empty;

    // World position of the zone centre.
    [JsonPropertyName("position")]
    public ZoneVector3 Position { get; set; } = new();

    // Rotation of the zone (Euler angles, W=1 for identity quaternion).
    [JsonPropertyName("rotation")]
    public ZoneVector4 Rotation { get; set; } = new();

    // Scale / size of the zone trigger volume.
    [JsonPropertyName("scale")]
    public ZoneVector3 Scale { get; set; } = new();
}

// XYZ float vector for zone position/scale.
public class ZoneVector3
{
    [JsonPropertyName("x")] public string X { get; set; } = "0";
    [JsonPropertyName("y")] public string Y { get; set; } = "0";
    [JsonPropertyName("z")] public string Z { get; set; } = "0";
}

// XYZW float vector for zone rotation quaternion.
public class ZoneVector4
{
    [JsonPropertyName("x")] public string X { get; set; } = "0";
    [JsonPropertyName("y")] public string Y { get; set; } = "0";
    [JsonPropertyName("z")] public string Z { get; set; } = "0";
    [JsonPropertyName("w")] public string W { get; set; } = "1";
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

    [JsonPropertyName("requiredExtract")]
    public string? RequiredExtract { get; set; }

    [JsonPropertyName("oneSessionOnly")]
    public bool OneSessionOnly { get; set; } = false;
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

