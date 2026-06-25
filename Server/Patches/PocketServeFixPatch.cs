using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using TraderGen.Helpers;

namespace TraderGen.Patches;

/// <summary>
/// Ensures the client always receives the correct TraderGen custom pocket template ID.
///
/// Even though PocketRestoreService fixes pockets immediately after profile load, other
/// code paths (e.g. equipment-stand upgrades, client round-trips, or migration edge
/// cases) can reset the pocket to the default TPL before /client/game/profile/list is
/// served. This postfix on ProfileHelper.GetCompleteProfile restores the correct TPL
/// on the cloned profile every time it is returned to the client.
/// </summary>
public class PocketServeFixPatch : AbstractPatch
{
    private static ProfileHelper? _profileHelper;
    private static Dictionary<string, string>? _questPocketMap;

    public static void SetDependencies(ProfileHelper profileHelper)
    {
        _profileHelper = profileHelper;
    }

    public static void BuildMap(string modPath)
    {
        _questPocketMap = PocketRestoreHelper.BuildQuestPocketMap(modPath);
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ProfileHelper), nameof(ProfileHelper.GetCompleteProfile));
    }

    [PatchPostfix]
    public static void Postfix(MongoId sessionId, ref List<PmcData> __result)
    {
        if (_questPocketMap == null || _questPocketMap.Count == 0) return;
        if (__result == null || __result.Count == 0) return;

        var pmc = __result[0];
        if (pmc?.Inventory?.Items == null) return;

        try
        {
            PocketRestoreHelper.RestorePockets(pmc, _questPocketMap);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TraderGen] PocketServeFixPatch error: {ex.Message}");
        }
    }
}
