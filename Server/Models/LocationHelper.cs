namespace TraderGen.Models;

// Maps user-friendly location names to BSG database IDs and display names.
// The quest-level "location" field requires the BSG database ID (24-char hex).
// The condition-level "target" in Location conditions uses the string map name.
public static class LocationHelper
{
    // BSG database IDs for each map location.
    private static readonly Dictionary<string, string> LocationToDbId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["any"] = "any",
        ["bigmap"] = "56f40101d2720b2a4d8b45d6",          // Customs
        ["factory4_day"] = "55f2d3fd4bdc2d5f408b4567",     // Factory (Day)
        ["factory4_night"] = "59fc81d786f774390775787e",    // Factory (Night)
        ["Woods"] = "5704e554d2720bac5b8b456e",            // Woods
        ["Shoreline"] = "5704e4dad2720bb55b8b4567",        // Shoreline
        ["Interchange"] = "5714dbc024597771384a510d",       // Interchange
        ["Lighthouse"] = "5704e5fad2720bc05b8b4567",       // Lighthouse
        ["Reserve"] = "5b0fc42d86f7744a585f9105",           // Reserve
        ["RezervBase"] = "5b0fc42d86f7744a585f9105",        // Reserve (alias)
        ["laboratory"] = "5b0fc42d86f7744a585f9106",        // Labs
        ["TarkovStreets"] = "5714dc692459777137212e12",     // Streets of Tarkov
        ["Sandbox"] = "653e6760052c01c1c805532f",           // Ground Zero
        ["Sandbox_high"] = "65b8d6f5cdde2479cb2a3125",      // Ground Zero (high)
    };

    // Display names for location condition text and objective descriptions.
    private static readonly Dictionary<string, string> LocationToDisplayName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["any"] = "Any Location",
        ["bigmap"] = "Customs",
        ["factory4_day"] = "Factory",
        ["factory4_night"] = "Factory (Night)",
        ["Woods"] = "Woods",
        ["Shoreline"] = "Shoreline",
        ["Interchange"] = "Interchange",
        ["Lighthouse"] = "Lighthouse",
        ["Reserve"] = "Reserve",
        ["RezervBase"] = "Reserve",
        ["laboratory"] = "The Lab",
        ["TarkovStreets"] = "Streets of Tarkov",
        ["Sandbox"] = "Ground Zero",
        ["Sandbox_high"] = "Ground Zero",
    };

    // Convert a user-friendly location name to the BSG database ID for the quest-level location field.
    public static string ToLocationDbId(string location)
    {
        return LocationToDbId.TryGetValue(location, out var dbId) ? dbId : location;
    }

    // Get the display name for a location (for objective text and locale strings).
    public static string ToDisplayName(string location)
    {
        return LocationToDisplayName.TryGetValue(location, out var name) ? name : location;
    }

    // Check if a location string is valid.
    public static bool IsValid(string location)
    {
        return LocationToDbId.ContainsKey(location);
    }
}
