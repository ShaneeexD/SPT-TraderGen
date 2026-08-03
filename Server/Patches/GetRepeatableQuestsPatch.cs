using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace TraderGen.Patches;

// Patches SPT's GetClientRepeatableQuests to integrate TraderGen quests.
// Quests are persisted to pmcData.RepeatableQuests for proper lifecycle management.
public class GetRepeatableQuestsPatch : AbstractPatch
{
    // Dependencies set by TraderGenPlugin before enabling
    private static ProfileHelper? _profileHelper;
    private static TimeUtil? _timeUtil;

    // Trader data registered by TraderGenPlugin
    private static List<TraderGenData> _registeredTraders = new();

    // Trader data with rotating quest templates
    public class TraderGenData
    {
        public string TraderId { get; set; } = string.Empty;
        public List<Models.RotatingQuestTemplate> Templates { get; set; } = new();
        public string PackFolder { get; set; } = string.Empty;
    }

    public static void SetDependencies(ProfileHelper profileHelper, TimeUtil timeUtil)
    {
        _profileHelper = profileHelper;
        _timeUtil = timeUtil;
    }

    public static void RegisterTrader(TraderGenData data)
    {
        _registeredTraders.Add(data);
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RepeatableQuestController), nameof(RepeatableQuestController.GetClientRepeatableQuests));
    }

    // Harmony postfix - generates and persists TraderGen quests
    [PatchPostfix]
    public static void Postfix(MongoId sessionID, ref List<PmcDataRepeatableQuest> __result)
    {
        Console.WriteLine($"[TraderGen] GetRepeatableQuestsPatch.Postfix called - sessionID: {sessionID}, registeredTraders: {_registeredTraders.Count}");
        
        if (_registeredTraders.Count == 0)
        {
            Console.WriteLine("[TraderGen] No registered traders, skipping");
            return;
        }

        if (_profileHelper == null || _timeUtil == null)
        {
            Console.WriteLine("[TraderGen] Dependencies not set - profileHelper or timeUtil is null");
            return;
        }

        try
        {
            // Get player profile
            var fullProfile = _profileHelper.GetFullProfile(sessionID);
            if (fullProfile?.CharacterData?.PmcData == null)
            {
                Console.WriteLine("[TraderGen] Could not get pmcData from profile");
                return;
            }
            
            var pmcData = fullProfile.CharacterData.PmcData;
            var currentTime = _timeUtil.GetTimeStamp();
            
            Console.WriteLine($"[TraderGen] Processing {pmcData.RepeatableQuests?.Count ?? 0} existing repeatable quest groups");

            // Get or create TraderGen quest group
            var traderGenGroup = GetOrCreateTraderGenGroup(pmcData, currentTime);

            // Generate quests for each registered trader
            foreach (var traderData in _registeredTraders)
            {
                Console.WriteLine($"[TraderGen] Processing trader {traderData.TraderId} with {traderData.Templates.Count} templates");
                
                // Check if trader is unlocked
                if (!IsTraderUnlocked(pmcData, traderData.TraderId))
                {
                    Console.WriteLine($"[TraderGen] Trader {traderData.TraderId} is not unlocked, skipping");
                    continue;
                }
                Console.WriteLine($"[TraderGen] Trader {traderData.TraderId} is unlocked, generating quests");

                // Generate quests for this trader
                var playerId = pmcData.Id?.ToString() ?? sessionID.ToString();
                var quests = GenerateQuestsForTrader(traderData, sessionID, currentTime, playerId);
                Console.WriteLine($"[TraderGen] Generated {quests.Count} quests for {traderData.TraderId} with playerId: {playerId}");

                // Add new quests, skip existing ones
                foreach (var quest in quests)
                {
                    var existingIndex = traderGenGroup.ActiveQuests?.FindIndex(q => q.Id == quest.Id) ?? -1;
                    if (existingIndex < 0)
                    {
                        // New quest - add it
                        traderGenGroup.ActiveQuests ??= new List<RepeatableQuest>();
                        traderGenGroup.ActiveQuests.Add(quest);
                        
                        traderGenGroup.ChangeRequirement ??= new Dictionary<MongoId, ChangeRequirement>();
                        traderGenGroup.ChangeRequirement[quest.Id] = CreateChangeRequirement(quest);
                    }
                    // Keep existing quests to preserve progress
                }

                // Clean up stale quests
                CleanupStaleQuests(traderGenGroup, traderData.TraderId, quests, pmcData);
            }

            // Ensure TraderGen group is in result
            if (!__result.Any(r => r.Name == "TraderGen"))
            {
                __result.Add(traderGenGroup);
                Console.WriteLine($"[TraderGen] Added TraderGen group with {traderGenGroup.ActiveQuests?.Count ?? 0} quests to result");
            }
            else
            {
                // Update existing group
                var existingIndex = __result.FindIndex(r => r.Name == "TraderGen");
                if (existingIndex >= 0)
                {
                    __result[existingIndex] = traderGenGroup;
                    Console.WriteLine($"[TraderGen] Updated existing TraderGen group with {traderGenGroup.ActiveQuests?.Count ?? 0} quests");
                }
            }
            
            Console.WriteLine($"[TraderGen] Patch complete - result now has {__result.Count} quest groups");
            
            // Register locale entries for new quests
            RepeatableQuestLocaleRegistrar.RegisterNewLocales();
        }
        catch (Exception ex)
        {
            // Log error but don't break SPT
            Console.WriteLine($"[TraderGen] Error in repeatable quest patch: {ex.Message}");
            Console.WriteLine($"[TraderGen] Stack trace: {ex.StackTrace}");
        }
    }

    private static PmcDataRepeatableQuest GetOrCreateTraderGenGroup(PmcData pmcData, long currentTime)
    {
        const string groupName = "TraderGen";

        var existing = pmcData.RepeatableQuests?.FirstOrDefault(r => r.Name == groupName);
        if (existing != null)
        {
            // Update expired EndTime
            if (existing.EndTime < currentTime)
            {
                // Set to 24 hours from now
                existing.EndTime = currentTime + 86400;
                Console.WriteLine($"[TraderGen] Updated expired EndTime to {existing.EndTime}");
            }
            return existing;
        }

        // Create new group with fixed ID, EndTime 24 hours from now
        var endTime = currentTime + 86400;
        Console.WriteLine($"[TraderGen] Creating new TraderGen group with EndTime: {endTime}");
        
        var newGroup = new PmcDataRepeatableQuest
        {
            Id = new MongoId("9999999911111111aaaa0001"),
            Name = groupName,
            ActiveQuests = new List<RepeatableQuest>(),
            InactiveQuests = new List<RepeatableQuest>(),
            EndTime = endTime,
            FreeChanges = 0,
            FreeChangesAvailable = 0,
            ChangeRequirement = new Dictionary<MongoId, ChangeRequirement>(),
        };

        pmcData.RepeatableQuests ??= new List<PmcDataRepeatableQuest>();
        pmcData.RepeatableQuests.Add(newGroup);

        return newGroup;
    }

    private static bool IsTraderUnlocked(PmcData pmcData, string traderId)
    {
        Console.WriteLine($"[TraderGen] Checking if trader {traderId} is unlocked...");
        if (pmcData.TradersInfo.TryGetValue(traderId, out var traderInfo))
        {
            var unlocked = traderInfo.Unlocked.GetValueOrDefault(false);
            Console.WriteLine($"[TraderGen] Trader {traderId} unlocked status: {unlocked}");
            return unlocked;
        }
        Console.WriteLine($"[TraderGen] Trader {traderId} not found in TradersInfo");
        return false;
    }

    private static List<RepeatableQuest> GenerateQuestsForTrader(TraderGenData traderData, MongoId sessionId, long currentTime, string playerId)
    {
        var result = new List<RepeatableQuest>();

        foreach (var template in traderData.Templates)
        {
            // Generate each quest slot
            for (var i = 0; i < template.QuestCount; i++)
            {
                var quest = Services.RepeatableQuestGenerator.GenerateQuestForPatch(
                    template, 
                    traderData.TraderId, 
                    traderData.PackFolder,
                    i,
                    playerId);
                
                if (quest != null)
                    result.Add(quest);
            }
        }

        return result;
    }

    private static void CleanupStaleQuests(
        PmcDataRepeatableQuest group, 
        string traderId, 
        List<RepeatableQuest> currentQuests,
        PmcData pmcData)
    {
        if (group.ActiveQuests == null)
            return;

        var currentQuestIds = currentQuests.Select(q => q.Id).ToHashSet();
        var staleQuests = group.ActiveQuests
            .Where(q => q.TraderId == traderId && !currentQuestIds.Contains(q.Id))
            .ToList();

        foreach (var staleQuest in staleQuests)
        {
            // Check quest status
            var questStatus = pmcData.Quests?.FirstOrDefault(q => q.QId == staleQuest.Id);
            
            // Only remove if not in progress
            if (questStatus == null || 
                questStatus.Status is QuestStatusEnum.AvailableForStart or QuestStatusEnum.Locked)
            {
                group.ActiveQuests.RemoveAll(q => q.Id == staleQuest.Id);
                group.ChangeRequirement?.Remove(staleQuest.Id);
                
                // Add to inactive list
                group.InactiveQuests ??= new List<RepeatableQuest>();
                group.InactiveQuests.Add(staleQuest);
            }
            // Keep in-progress quests in ActiveQuests
        }
    }

    private static ChangeRequirement CreateChangeRequirement(RepeatableQuest quest)
    {
        return new ChangeRequirement
        {
            ChangeCost = quest.ChangeCost,
            ChangeStandingCost = quest.ChangeStandingCost ?? 0.01,
        };
    }
}

