using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace TraderGen.Patches;

// Logs SPT quest reward application for debugging.
public class QuestRewardDebugPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(QuestRewardHelper), nameof(QuestRewardHelper.ApplyQuestReward));
    }

    [PatchPrefix]
    public static void Prefix(PmcData profileData, MongoId questId, QuestStatusEnum state)
    {
        Console.WriteLine($"[TraderGen] QuestRewardHelper.ApplyQuestReward PREFIX — questId={questId}, state={state}, profileId={profileData?.Id}");
    }

    [PatchPostfix]
    public static void Postfix(PmcData profileData, MongoId questId, QuestStatusEnum state, IEnumerable<Item> __result)
    {
        var resultCount = __result?.Count() ?? 0;
        Console.WriteLine($"[TraderGen] QuestRewardHelper.ApplyQuestReward POSTFIX — questId={questId}, state={state}, resultItems={resultCount}");
    }
}

// Logs every reward processed by RewardHelper.ApplyRewards.
public class RewardHelperDebugPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RewardHelper), nameof(RewardHelper.ApplyRewards));
    }

    [PatchPrefix]
    public static void Prefix(IEnumerable<Reward> rewards)
    {
        var list = rewards?.ToList() ?? new List<Reward>();
        Console.WriteLine($"[TraderGen] RewardHelper.ApplyRewards PREFIX — rewardCount={list.Count}");
        foreach (var r in list)
        {
            Console.WriteLine($"[TraderGen]   Reward: type={r.Type}, target={r.Target}, value={r.Value}");
        }
    }
}
