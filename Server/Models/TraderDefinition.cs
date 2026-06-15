using System.Text.Json.Serialization;

namespace TraderGen.Models;

// Root model for a trader pack JSON file.
// This is the schema that users fill out (or generate via the TraderGen Tool).
public class TraderDefinition
{
    // Whether this trader is enabled. Set to false to skip loading without deleting the file.
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    // Unique trader ID. Should be a 24-character hex string (MongoDB ObjectId format).
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Short display name shown in the trader list (e.g. "Viktor").
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    // First name used in locale data.
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    // Last name / surname used in locale data.
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = "Unknown";

    // Full display name. If empty, defaults to "nickname lastName".
    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    // Location text shown in trader screen (e.g. "Reserve Underground Bunker").
    [JsonPropertyName("location")]
    public string Location { get; set; } = "Unknown";

    // Trader description shown when clicking the trader info button.
    [JsonPropertyName("description")]
    public string Description { get; set; } = "A custom trader.";

    // Relative path to the trader avatar image file inside the trader pack folder.
    // Example: "assets/avatar.jpg"
    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;

    // Default currency the trader uses: "RUB", "USD", or "EUR".
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    // Whether the trader is unlocked from the start (default true).
    [JsonPropertyName("unlockedByDefault")]
    public bool UnlockedByDefault { get; set; } = true;

    // Whether the trader can buy items from the player.
    [JsonPropertyName("buyerEnabled")]
    public bool BuyerEnabled { get; set; } = true;

    // Whether the trader appears on the flea market / ragfair.
    [JsonPropertyName("ragfairEnabled")]
    public bool RagfairEnabled { get; set; } = true;

    // Trader's ruble balance.
    [JsonPropertyName("balanceRub")]
    public long BalanceRub { get; set; } = 5000000;

    // Trader's dollar balance.
    [JsonPropertyName("balanceDol")]
    public long BalanceDol { get; set; } = 0;

    // Trader's euro balance.
    [JsonPropertyName("balanceEur")]
    public long BalanceEur { get; set; } = 0;

    // Item category IDs the trader will buy from the player.
    // If empty, uses a sensible default set of categories.
    [JsonPropertyName("buyCategories")]
    public List<string>? BuyCategories { get; set; }

    // Item template IDs that the trader will NOT buy.
    [JsonPropertyName("buyProhibitedItems")]
    public List<string>? BuyProhibitedItems { get; set; }

    // Whether this trader offers insurance (default false).
    [JsonPropertyName("insuranceEnabled")]
    public bool InsuranceEnabled { get; set; } = false;

    // Whether this trader offers repair services (default false).
    [JsonPropertyName("repairEnabled")]
    public bool RepairEnabled { get; set; } = false;

    // Minimum seconds between trader inventory refreshes.
    [JsonPropertyName("refreshTimeMin")]
    public int RefreshTimeMin { get; set; } = 1800;

    // Maximum seconds between trader inventory refreshes.
    [JsonPropertyName("refreshTimeMax")]
    public int RefreshTimeMax { get; set; } = 7200;

    // Loyalty levels for this trader. Must have at least one.
    [JsonPropertyName("loyaltyLevels")]
    public List<LoyaltyLevelDefinition> LoyaltyLevels { get; set; } = [];

    // Items this trader sells / barters.
    [JsonPropertyName("assort")]
    public List<AssortItemDefinition> Assort { get; set; } = [];
}

// Defines a single loyalty level for a trader.
public class LoyaltyLevelDefinition
{
    // Loyalty level number (1-4 typically).
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    // Minimum player level required to reach this loyalty level.
    [JsonPropertyName("minLevel")]
    public int MinLevel { get; set; } = 1;

    // Minimum total sales sum (roubles spent) required.
    [JsonPropertyName("minSalesSum")]
    public long MinSalesSum { get; set; } = 0;

    // Minimum standing/reputation required.
    [JsonPropertyName("minStanding")]
    public double MinStanding { get; set; } = 0;

    // Buy price coefficient (percentage the trader pays when buying from player).
    // Higher = trader pays more. Typical range 30-60.
    [JsonPropertyName("buyPriceCoef")]
    public int BuyPriceCoef { get; set; } = 40;
}

// Defines a single item or barter offer in the trader's assortment.
public class AssortItemDefinition
{
    // The SPT item template ID (e.g. "544a11ac4bdc2d470e8b456a").
    [JsonPropertyName("itemTpl")]
    public string ItemTpl { get; set; } = string.Empty;

    // Optional fixed item ID. If omitted, one is auto-generated.
    // Useful for weapon presets where child items reference this ID.
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    // Which loyalty level this item is available at (1-based).
    [JsonPropertyName("loyaltyLevel")]
    public int LoyaltyLevel { get; set; } = 1;

    // Stock count. Only matters if unlimitedStock is false.
    [JsonPropertyName("stock")]
    public int Stock { get; set; } = 999999;

    // Whether the item has unlimited stock.
    [JsonPropertyName("unlimitedStock")]
    public bool UnlimitedStock { get; set; } = true;

    // Price in the specified currency. Ignored if barter items are specified.
    [JsonPropertyName("price")]
    public int Price { get; set; } = 0;

    // Currency for the price: "RUB", "USD", or "EUR".
    // Uses the trader's default currency if omitted.
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    // Barter requirements. If specified, this is a barter trade instead of a money purchase.
    // Multiple barter items = all are required together.
    [JsonPropertyName("barter")]
    public List<BarterRequirement>? Barter { get; set; }

    // Maximum number a player can buy per restock (0 = no limit).
    [JsonPropertyName("buyLimit")]
    public int BuyLimit { get; set; } = 0;
}

// A single barter ingredient requirement.
public class BarterRequirement
{
    // Item template ID of the required barter item.
    [JsonPropertyName("itemTpl")]
    public string ItemTpl { get; set; } = string.Empty;

    // How many of this item are required.
    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}
