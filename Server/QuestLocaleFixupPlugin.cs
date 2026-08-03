using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TraderGen.Services;

namespace TraderGen;

// Runs after all mods have loaded (including ItemGen) so that custom item names can be resolved
// into TraderGen-generated quest locale entries. TraderGen builds quest files before ItemGen
// injects its items, so the initial locale files may contain raw template IDs instead of names.
[Injectable(TypePriority = OnLoadOrder.PostLoad + 2)]
public class QuestLocaleFixupPlugin(
    ISptLogger<QuestLocaleFixupPlugin> logger,
    LocaleTable localeTable)
    : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        if (QuestBuilder.LocaleFixups.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            var globalLocales = localeTable.Global;
            if (!globalLocales.TryGetValue("en", out var enLocale))
            {
                logger.LogWithColor("[TraderGen] Could not find English locale for quest item name fixup.", LogColor.Yellow);
                return Task.CompletedTask;
            }

            // Register a lazy transformer so the patch is applied every time the locale is deserialized.
            // This is needed because LazyLoad.Value returns a fresh dictionary each access.
            var entriesToPatch = QuestBuilder.LocaleFixups.Count;
            enLocale.AddTransformer(en =>
            {
                if (en == null)
                {
                    return en;
                }

                foreach (var entry in QuestBuilder.LocaleFixups)
                {
                    if (entry.CustomDescription != null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(entry.ItemTpl))
                    {
                        continue;
                    }

                    var itemName = ResolveItemName(en, entry.ItemTpl);
                    if (string.IsNullOrWhiteSpace(itemName))
                    {
                        continue;
                    }

                    var newText = entry.ObjectiveType switch
                    {
                        "find" => $"Find {entry.Count} {itemName}",
                        "leave" => $"Leave {itemName} at the designated location",
                        _ => $"Hand over {entry.Count} {itemName}",
                    };

                    // Avoid touching the dictionary if the value is already correct.
                    if (!en.TryGetValue(entry.CondId, out var current) || current != newText)
                    {
                        en[entry.CondId] = newText;
                    }
                }

                return en;
            });

            if (entriesToPatch > 0)
            {
                logger.LogWithColor($"[TraderGen] Registered locale transformer for {entriesToPatch} quest objective(s); item names will be resolved after custom item mods load.", LogColor.Cyan);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[TraderGen] Quest locale fixup failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static string? ResolveItemName(Dictionary<string, string> en, string itemTpl)
    {
        if (en.TryGetValue($"{itemTpl} Name", out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (en.TryGetValue($"{itemTpl} ShortName", out var shortName) && !string.IsNullOrWhiteSpace(shortName))
        {
            return shortName;
        }

        return null;
    }
}
