using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace TraderGen.Patches;

/// <summary>
/// Logs server-side pocket reward application so we can see if SPT
/// is calling ReplaceProfilePocketTpl and with what template ID.
/// </summary>
public class PocketRewardLogPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ProfileHelper), nameof(ProfileHelper.ReplaceProfilePocketTpl));
    }

    [PatchPrefix]
    public static void Prefix(PmcData pmcProfile, string newPocketTpl)
    {
        var pockets = pmcProfile.Inventory?.Items?.Where(i => i.SlotId == "Pockets");
        var count = pockets?.Count() ?? 0;
        Console.WriteLine($"[TraderGen] ReplaceProfilePocketTpl PREFIX — profileId={pmcProfile.Id}, newTpl={newPocketTpl}, pocketsFound={count}");
        if (count == 0)
        {
            Console.WriteLine("[TraderGen]   WARNING: No pocket items found in profile before replacement!");
        }
        else
        {
            foreach (var p in pockets!)
            {
                Console.WriteLine($"[TraderGen]   Current pocket item: _id={p.Id}, oldTpl={p.Template}");
            }
        }
    }

    [PatchPostfix]
    public static void Postfix(PmcData pmcProfile, string newPocketTpl)
    {
        var pockets = pmcProfile.Inventory?.Items?.Where(i => i.SlotId == "Pockets");
        var count = pockets?.Count() ?? 0;
        Console.WriteLine($"[TraderGen] ReplaceProfilePocketTpl POSTFIX — profileId={pmcProfile.Id}, pocketsFound={count}");
        foreach (var p in pockets!)
        {
            Console.WriteLine($"[TraderGen]   After replacement: _id={p.Id}, tpl={p.Template}");
        }
    }
}
