namespace TraderGen.Services;

// Maps BSG location IDs to human-readable display names for quest text.
public static class LocationHelper
{
    private static readonly Dictionary<string, string> LocationDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["any"] = "Tarkov",
        ["bigmap"] = "Customs",
        ["factory4_day"] = "Factory (Day)",
        ["factory4_night"] = "Factory (Night)",
        ["Woods"] = "Woods",
        ["Shoreline"] = "Shoreline",
        ["Interchange"] = "Interchange",
        ["Lighthouse"] = "Lighthouse",
        ["Reserve"] = "Reserve",
        ["laboratory"] = "The Lab",
        ["TarkovStreets"] = "Streets of Tarkov",
        ["Sandbox"] = "Ground Zero",
        ["Sandbox_high"] = "Ground Zero (High Level)",
    };

    public static string ToDisplayName(string locationId)
    {
        return LocationDisplayNames.GetValueOrDefault(locationId, locationId);
    }

    // Maps location keys (e.g. "laboratory") to the GUID the client expects for the quest's Location field.
    // Sourced from SPT's quest.json locationIdMap.
    private static readonly Dictionary<string, string> LocationIdMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["any"] = "any",
        ["factory4_day"] = "55f2d3fd4bdc2d5f408b4567",
        ["factory4_night"] = "59fc81d786f774390775787e",
        ["bigmap"] = "56f40101d2720b2a4d8b45d6",
        ["Woods"] = "5704e3c2d2720bac5b8b4567",
        ["Shoreline"] = "5704e554d2720bac5b8b456e",
        ["Interchange"] = "5714dbc024597771384a510d",
        ["Lighthouse"] = "5704e4dad2720bb55b8b4567",
        ["laboratory"] = "5b0fc42d86f7744a585f9105",
        ["RezervBase"] = "5704e5fad2720bc05b8b4567",
        ["Reserve"] = "5704e5fad2720bc05b8b4567", // alias
        ["TarkovStreets"] = "5714dc692459777137212e12",
        ["Sandbox"] = "653e6760052c01c1c805532f",
        ["Sandbox_high"] = "653e6760052c01c1c805532f",
    };

    // Converts a location key to the GUID used in quest.Location field.
    public static string ToQuestLocationId(string? location)
    {
        if (string.IsNullOrWhiteSpace(location) || location == "any")
            return "any";
        return LocationIdMap.GetValueOrDefault(location, location);
    }

    // Maps a location string to its BSG database location ID.
    // If the input is already a valid BSG location key, returns it as-is.
    // Used by QuestBuilder when generating BSG-format quest JSON.
    public static string ToLocationDbId(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return "any";

        // Already a valid BSG key
        var lower = location.ToLowerInvariant();
        if (lower is "any" or "bigmap" or "factory4_day" or "factory4_night" or "woods"
            or "shoreline" or "interchange" or "lighthouse" or "reserve"
            or "laboratory" or "tarkovstreets" or "sandbox" or "sandbox_high")
        {
            return location;
        }

        // Try reverse lookup from display name to ID
        foreach (var (id, name) in LocationDisplayNames)
        {
            if (string.Equals(name, location, StringComparison.OrdinalIgnoreCase))
                return id;
        }

        return location; // Return as-is (might be a raw BSG ID we don't know about)
    }
}
