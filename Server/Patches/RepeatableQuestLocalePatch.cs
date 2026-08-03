using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TraderGen.Services;

namespace TraderGen.Patches;

// Registers locale entries for repeatable quests into SPT's locale database.
public static class RepeatableQuestLocaleRegistrar
{
    private static LocaleTable? _localeTable;
    private static ISptLogger<TraderGenPlugin>? _logger;

    public static void RegisterLocales(LocaleTable localeTable, ISptLogger<TraderGenPlugin> logger)
    {
        _localeTable = localeTable;
        _logger = logger;

        var locales = RepeatableQuestLocaleStore.GetAll();
        if (locales.Count == 0)
            return;

        var globalLocales = localeTable.Global;
        var conditionLocales = RepeatableQuestLocaleStore.GetAllConditions();

        foreach (var (locale, lazyDict) in globalLocales)
        {
            lazyDict.AddTransformer(dict =>
            {
                foreach (var (questId, (name, description)) in locales)
                {
                    // SPT locale key pattern
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
            LogColor.Green);
    }

    // Registers new locales after initial registration.
    public static void RegisterNewLocales()
    {
        if (_localeTable == null)
        {
            Console.WriteLine("[TraderGen] Cannot register new locales - locale table not initialized");
            return;
        }

        var locales = RepeatableQuestLocaleStore.GetAll();
        if (locales.Count == 0)
            return;

        try
        {
            var localeTable = _localeTable.Global;
            var conditionLocales = RepeatableQuestLocaleStore.GetAllConditions();
            
            Console.WriteLine($"[TraderGen] RegisterNewLocales: {locales.Count} quests, {conditionLocales.Count} conditions");

            foreach (var (locale, lazyDict) in localeTable)
            {
                // Try to get underlying dictionary via reflection first
                var dict = GetDictionaryFromLazy(lazyDict);
                
                if (dict != null)
                {
                    // Direct dictionary access worked
                    Console.WriteLine($"[TraderGen] Using direct dictionary access for {locale}");
                    
                    int addedCount = 0;
                    foreach (var (questId, (name, description)) in locales)
                    {
                        dict[$"{questId} name"] = name;
                        dict[$"{questId} description"] = description;
                        if (!dict.ContainsKey($"{questId} note")) dict[$"{questId} note"] = "";
                        if (!dict.ContainsKey($"{questId} successMessageText")) dict[$"{questId} successMessageText"] = "Quest complete. Well done.";
                        if (!dict.ContainsKey($"{questId} failMessageText")) dict[$"{questId} failMessageText"] = "Quest failed.";
                        if (!dict.ContainsKey($"{questId} startedMessageText")) dict[$"{questId} startedMessageText"] = "Quest accepted.";
                        if (!dict.ContainsKey($"{questId} changeQuestMessageText")) dict[$"{questId} changeQuestMessageText"] = "Quest replaced.";
                        if (!dict.ContainsKey($"{questId} acceptPlayerMessage")) dict[$"{questId} acceptPlayerMessage"] = "";
                        if (!dict.ContainsKey($"{questId} declinePlayerMessage")) dict[$"{questId} declinePlayerMessage"] = "";
                        if (!dict.ContainsKey($"{questId} completePlayerMessage")) dict[$"{questId} completePlayerMessage"] = "";
                        addedCount++;
                    }

                    int conditionCount = 0;
                    foreach (var (conditionId, text) in conditionLocales)
                    {
                        if (!dict.ContainsKey(conditionId))
                        {
                            dict[conditionId] = text;
                            conditionCount++;
                        }
                    }

                    Console.WriteLine($"[TraderGen] Direct access: Added {addedCount} quest locales and {conditionCount} condition locales to {locale}");
                }
                else
                {
                    // Fallback to AddTransformer
                    Console.WriteLine($"[TraderGen] Using AddTransformer fallback for {locale}");
                    
                    lazyDict.AddTransformer(dict =>
                    {
                        foreach (var (questId, (name, description)) in locales)
                        {
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

                        foreach (var (conditionId, text) in conditionLocales)
                        {
                            dict.TryAdd(conditionId, text);
                        }

                        return dict;
                    });
                    
                    Console.WriteLine($"[TraderGen] AddTransformer: Registered {locales.Count} quests and {conditionLocales.Count} conditions to {locale}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TraderGen] Error registering locales: {ex.Message}");
        }
    }

    // Uses reflection to get the underlying dictionary from a LazyDictionary
    private static Dictionary<string, string>? GetDictionaryFromLazy(object lazyDict)
    {
        try
        {
            // Try to access underlying dictionary
            var type = lazyDict.GetType();
            
            // Try Value property
            var valueProperty = type.GetProperty("Value");
            if (valueProperty != null)
            {
                return valueProperty.GetValue(lazyDict) as Dictionary<string, string>;
            }

            // Try to find dictionary field
            var fields = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Dictionary<string, string>))
                {
                    return field.GetValue(lazyDict) as Dictionary<string, string>;
                }
            }

            // Try GetValue method
            var getValueMethod = type.GetMethod("GetValue");
            if (getValueMethod != null && getValueMethod.GetParameters().Length == 0)
            {
                return getValueMethod.Invoke(lazyDict, null) as Dictionary<string, string>;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TraderGen] Reflection error accessing lazy dict: {ex.Message}");
        }
        return null;
    }
}
