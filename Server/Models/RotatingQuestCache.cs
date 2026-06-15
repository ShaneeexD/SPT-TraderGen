using System.Text.Json.Serialization;

namespace TraderGen.Models;

// Persisted cache of generated rotating quests, stored in rotating_quests_cache.json.
// This ensures rotating quests survive server restarts and only regenerate when expired.
public class RotatingQuestCache
{
    // All currently active rotating quest entries.
    [JsonPropertyName("activeQuests")]
    public List<CachedRotatingQuest> ActiveQuests { get; set; } = [];
}

// A single cached rotating quest with its expiration time.
public class CachedRotatingQuest
{
    // The template ID this quest was generated from.
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    // The trader ID this quest belongs to.
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = string.Empty;

    // Rotation type: "daily" or "weekly".
    [JsonPropertyName("rotation")]
    public string Rotation { get; set; } = "daily";

    // UTC timestamp when this quest was generated.
    [JsonPropertyName("generatedAtUtc")]
    public DateTime GeneratedAtUtc { get; set; }

    // UTC timestamp when this quest expires and should be regenerated.
    [JsonPropertyName("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }

    // The generated quest as a concrete StoryQuestDefinition (simplified format).
    // This is what gets fed into QuestBuilder on subsequent loads.
    [JsonPropertyName("quest")]
    public StoryQuestDefinition Quest { get; set; } = new();
}
