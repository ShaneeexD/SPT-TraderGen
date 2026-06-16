using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace TraderGen.Patches;

// Postfix patch on RepeatableQuestController.GetClientRepeatableQuests
// Injects TraderGen rotating quests into the daily/weekly quest pools returned to the client.
public class GetRepeatableQuestsPatch : AbstractPatch
{
    // Stored rotating quests to inject, keyed by rotation type ("Daily" or "Weekly")
    private static Dictionary<string, List<RepeatableQuest>> _questsToInject = new();
    private static Dictionary<string, Dictionary<MongoId, ChangeRequirement>> _changeRequirements = new();

    public static void SetQuestsToInject(
        Dictionary<string, List<RepeatableQuest>> quests,
        Dictionary<string, Dictionary<MongoId, ChangeRequirement>> changeRequirements)
    {
        _questsToInject = quests;
        _changeRequirements = changeRequirements;
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(RepeatableQuestController).GetMethod(
            nameof(RepeatableQuestController.GetClientRepeatableQuests));
    }

    [PatchPostfix]
    public static void Postfix(ref List<PmcDataRepeatableQuest> __result)
    {
        if (_questsToInject.Count == 0)
            return;

        foreach (var repeatableGroup in __result)
        {
            if (repeatableGroup.Name == null)
                continue;

            // Match our quests to the correct group (Daily/Weekly)
            if (!_questsToInject.TryGetValue(repeatableGroup.Name, out var questsForGroup))
                continue;

            if (questsForGroup.Count == 0)
                continue;

            // Add our quests to the active quests list
            repeatableGroup.ActiveQuests ??= [];
            foreach (var quest in questsForGroup)
            {
                // Don't add duplicates (in case of multiple calls)
                if (repeatableGroup.ActiveQuests.Any(q => q.Id == quest.Id))
                    continue;

                repeatableGroup.ActiveQuests.Add(quest);
            }

            // Add change requirements for our quests
            if (_changeRequirements.TryGetValue(repeatableGroup.Name, out var requirements))
            {
                repeatableGroup.ChangeRequirement ??= [];
                foreach (var (questId, requirement) in requirements)
                {
                    repeatableGroup.ChangeRequirement.TryAdd(questId, requirement);
                }
            }
        }
    }
}
