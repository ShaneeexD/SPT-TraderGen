using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;

namespace TraderGen.Patches;

// Fixes a bug in StringOrInt.ToString() where it returns null when only one of String/Int is set.
// The original code checks "String is null || Int is null" but the JSON converter only
// populates one field, so ToString() always returns null for deserialized values.
// This breaks RewardHelper.GetRewardProductionMatch which calls TraderId?.ToString()
// to get the hideout area type for ProductionScheme quest rewards.
public class StringOrIntToStringPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(StringOrInt), nameof(StringOrInt.ToString));
    }

    [PatchPrefix]
    public static bool Prefix(StringOrInt __instance, ref string? __result)
    {
        if (__instance.Int.HasValue)
        {
            __result = __instance.Int.Value.ToString();
            return false;
        }

        __result = __instance.String;
        return false;
    }
}

// Fixes RewardHelper.GetMatchingProductions so that when a single quest unlocks
// multiple production schemes, each ProductionScheme reward matches exactly one
// scheme. The original code matches by questId alone in the first pass, which
// returns all schemes for that quest (count > 1) and then falls through to a
// fragile fallback. This patch adds an endProduct filter to the initial match
// so each reward (which carries its own endProduct tpl) matches exactly one scheme.
public class ProductionSchemeMatchPatch : AbstractPatch
{
    private static HideoutTable? _hideoutTable;

    public static void SetDependencies(HideoutTable hideoutTable)
    {
        _hideoutTable = hideoutTable;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RewardHelper), "GetMatchingProductions");
    }

    [PatchPrefix]
    public static bool Prefix(
        HideoutAreas desiredHideoutAreaType,
        MongoId questId,
        Reward craftUnlockReward,
        ref List<HideoutProduction> __result)
    {
        if (_hideoutTable?.Production?.Recipes == null)
            return true; // Fall through to original method

        var rewardItemTpl = craftUnlockReward.Items?.FirstOrDefault()?.Template;
        if (rewardItemTpl is null)
            return true; // Fall through to original method

        // Match by questId AND endProduct so that quests unlocking multiple
        // schemes still match exactly one production per reward.
        var match = _hideoutTable.Production.Recipes
            .Where(p =>
                p.Requirements?.Any(req => req.QuestId == questId) == true
                && p.EndProduct == rewardItemTpl.Value)
            .ToList();

        if (match.Count == 1)
        {
            __result = match;
            return false; // Skip original method
        }

        // Fall through to original method for fallback matching
        return true;
    }
}
