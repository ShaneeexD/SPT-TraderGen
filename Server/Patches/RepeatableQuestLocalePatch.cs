using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using TraderGen.Services;

namespace TraderGen.Patches;

// Registers locale entries for TraderGen repeatable quests into SPT's locale database.
// Called during mod initialization after quests are generated.
public static class RepeatableQuestLocaleRegistrar
{
    public static void RegisterLocales(DatabaseService databaseService, ISptLogger<TraderGenPlugin> logger)
    {
        var locales = RepeatableQuestLocaleStore.GetAll();
        if (locales.Count == 0)
            return;

        var localeTable = databaseService.GetLocales().Global;
        var conditionLocales = RepeatableQuestLocaleStore.GetAllConditions();

        foreach (var (locale, lazyDict) in localeTable)
        {
            lazyDict.AddTransformer(dict =>
            {
                foreach (var (questId, (name, description)) in locales)
                {
                    // SPT repeatable quest locale keys follow this pattern:
                    dict.TryAdd($"{questId} name", name);
                    dict.TryAdd($"{questId} description", description);
                    dict.TryAdd($"{questId} note", "");
                    dict.TryAdd($"{questId} successMessageText", "Quest complete. Well done.");
                    dict.TryAdd($"{questId} failMessageText", "Quest failed.");
                    dict.TryAdd($"{questId} startedMessageText", "Quest accepted.");
                    dict.TryAdd($"{questId} changeQuestMessageText", "Quest replaced.");
                    dict.TryAdd($"{questId} acceptPlayerMessage", "");
                    dict.TryAdd($"{questId} declinePlayerMessage", "");
                    dict.TryAdd($"{questId} completePlayerMessage", "");
                }

                // Register objective/condition text entries
                foreach (var (conditionId, text) in conditionLocales)
                {
                    dict.TryAdd(conditionId, text);
                }

                return dict;
            });
        }

        logger.LogWithColor(
            $"[TraderGen] Registered locale entries for {locales.Count} repeatable quest(s).",
            LogTextColor.Green);
    }
}
