using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators.RepeatableQuests;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Repeatable;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TraderGen.Models;
using TraderGen.Services;

namespace TraderGen.Generators;

// Custom SPT quest generator that integrates TraderGen rotating quests into SPT's native repeatable quest system.
[Injectable]
public class TraderGenQuestGenerator : IRepeatableQuestGenerator
{
    private readonly ISptLogger<TraderGenQuestGenerator> _logger;
    private readonly LocaleTable _localeTable;
    private readonly ProfileHelper _profileHelper;
    private readonly TimeUtil _timeUtil;

    // Static storage for trader templates registered during plugin load
    private static readonly List<TraderGenData> _registeredTraders = new();

    // Data about a loaded trader and their rotating quest templates
    public class TraderGenData
    {
        public string TraderId { get; set; } = string.Empty;
        public List<RotatingQuestTemplate> Templates { get; set; } = new();
        public string PackFolder { get; set; } = string.Empty;
    }

    public TraderGenQuestGenerator(
        ISptLogger<TraderGenQuestGenerator> logger,
        LocaleTable localeTable,
        ProfileHelper profileHelper,
        TimeUtil timeUtil)
    {
        _logger = logger;
        _localeTable = localeTable;
        _profileHelper = profileHelper;
        _timeUtil = timeUtil;
        
        Console.WriteLine("[TG-Quests] === TraderGenQuestGenerator CONSTRUCTOR called by DI ===");
        Console.WriteLine($"[TG-Quests] Dependencies injected: logger={logger != null}, localeTable={localeTable != null}, profile={profileHelper != null}, time={timeUtil != null}");
    }

    // Registers a trader's quest templates with the generator.
    public static void RegisterTrader(TraderGenData data)
    {
        Console.WriteLine($"[TG-Quests] RegisterTrader() called for {data.TraderId} with {data.Templates.Count} templates");
        _registeredTraders.Add(data);
        Console.WriteLine($"[TG-Quests] Total registered traders: {_registeredTraders.Count}");
    }

    // Main entry point
    public RepeatableQuest? Generate(
        MongoId sessionId,
        int pmcLevel,
        MongoId traderId,
        QuestTypePool questTypePool,
        RepeatableQuestConfig repeatableConfig)
    {
        Console.WriteLine($"[TG-Quests] >>> IRepeatableQuestGenerator.Generate() called <<<");
        Console.WriteLine($"[TG-Quests] Session: {sessionId}, Trader: {traderId}, PMC Level: {pmcLevel}");
        Console.WriteLine($"[TG-Quests] Registered traders: {_registeredTraders.Count}");
        
        // Check if this trader is a TraderGen trader
        var traderData = _registeredTraders.FirstOrDefault(t => t.TraderId == traderId.ToString());
        if (traderData == null)
        {
            Console.WriteLine($"[TG-Quests] Trader {traderId} is NOT a TraderGen trader - skipping");
            // Not a TraderGen trader - return null
            return null;
        }

        Console.WriteLine($"[TG-Quests] *** Trader {traderId} IS a TraderGen trader with {traderData.Templates.Count} templates ***");

        // Get player profile to check unlock status and get player ID
        var fullProfile = _profileHelper.GetFullProfile(sessionId);
        var pmcData = fullProfile.CharacterData.PmcData;

        // Check if trader is unlocked for this player
        if (!IsTraderUnlocked(pmcData, traderData.TraderId))
        {
            Console.WriteLine($"[TG-Quests] Trader {traderId} is LOCKED for this player - skipping");
            return null;
        }
        
        Console.WriteLine($"[TG-Quests] *** Trader {traderId} is UNLOCKED - generating quests ***");

        // Generate a quest using SPT's quest pool mechanism
        var playerId = pmcData.Id?.ToString() ?? sessionId.ToString();
        
        // Generate the next available quest that hasn't been added to the pool yet
        var quest = GenerateNextAvailableQuest(traderData, sessionId, playerId, questTypePool);
        
        if (quest != null)
        {
            Console.WriteLine($"[TG-Quests] *** SUCCESS: Generated quest {quest.Id} ({quest.Type}) for trader {traderId} ***");
            
            // Register locale entries immediately
            RegisterLocaleForQuest(quest);
        }
        else
        {
            Console.WriteLine($"[TG-Quests] No more quests to generate for trader {traderId}");
        }

        return quest;
    }

    private RepeatableQuest? GenerateNextAvailableQuest(
        TraderGenData traderData, 
        MongoId sessionId, 
        string playerId,
        QuestTypePool questTypePool)
    {
        // For now, generate the first available quest
        // SPT's pool system will handle duplicates
        foreach (var template in traderData.Templates)
        {
            for (var i = 0; i < template.QuestCount; i++)
            {
                // Generate the quest
                var quest = RepeatableQuestGenerator.GenerateQuestForPatch(
                    template,
                    traderData.TraderId,
                    traderData.PackFolder,
                    i,
                    playerId);

                if (quest != null)
                {
                    return quest;
                }
            }
        }

        return null;
    }

    private bool IsTraderUnlocked(PmcData pmcData, string traderId)
    {
        if (pmcData.TradersInfo.TryGetValue(traderId, out var traderInfo))
        {
            return traderInfo.Unlocked.GetValueOrDefault(false);
        }
        return false;
    }

    private void RegisterLocaleForQuest(RepeatableQuest quest)
    {
        try
        {
            var questId = quest.Id.ToString();
            var locales = RepeatableQuestLocaleStore.GetAll();
            
            if (!locales.TryGetValue(questId, out var localeData))
                return;

            var localeTable = _localeTable.Global;
            var conditionLocales = RepeatableQuestLocaleStore.GetAllConditions();

            foreach (var (locale, lazyDict) in localeTable)
            {
                lazyDict.AddTransformer(dict =>
                {
                    // Add quest locale entries
                    dict[$"{questId} name"] = localeData.Name;
                    dict[$"{questId} description"] = localeData.Description;
                    dict.TryAdd($"{questId} note", "");
                    dict.TryAdd($"{questId} successMessageText", "Quest complete. Well done.");
                    dict.TryAdd($"{questId} failMessageText", "Quest failed.");
                    dict.TryAdd($"{questId} startedMessageText", "Quest accepted.");
                    dict.TryAdd($"{questId} changeQuestMessageText", "Quest replaced.");
                    dict.TryAdd($"{questId} acceptPlayerMessage", "");
                    dict.TryAdd($"{questId} declinePlayerMessage", "");
                    dict.TryAdd($"{questId} completePlayerMessage", "");

                    // Add condition locales
                    foreach (var (conditionId, text) in conditionLocales)
                    {
                        dict.TryAdd(conditionId, text);
                    }

                    return dict;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"[TraderGen] Error registering locale: {ex.Message}");
        }
    }
}
