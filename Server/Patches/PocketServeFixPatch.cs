using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using TraderGen.Helpers;

namespace TraderGen.Patches;

// Restores the correct custom pocket TPL every time the complete profile is served to the client.
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
