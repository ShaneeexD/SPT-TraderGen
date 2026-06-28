using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;

namespace TraderGen.Patches;

// Ensures a trader exists in the player's profile before SPT tries to set its unlocked state.
// This fixes TraderUnlock quest rewards for traders added by mods after the profile was created.
public class TraderUnlockEnsureInProfilePatch : AbstractPatch
{
    private static TraderHelper? _traderHelper;
    private static ProfileHelper? _profileHelper;

    public static void SetDependencies(TraderHelper traderHelper, ProfileHelper profileHelper)
    {
        _traderHelper = traderHelper;
        _profileHelper = profileHelper;
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TraderHelper), nameof(TraderHelper.SetTraderUnlockedState));
    }

    [PatchPrefix]
    public static void Prefix(MongoId traderId, MongoId sessionId)
    {
        if (_traderHelper == null || _profileHelper == null)
        {
            return;
        }

        try
        {
            var pmcData = _profileHelper.GetPmcProfile(sessionId);
            if (pmcData != null && !pmcData.TradersInfo.ContainsKey(traderId))
            {
                _traderHelper.ResetTrader(sessionId, traderId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TraderGen] TraderUnlockEnsureInProfilePatch error: {ex.Message}");
        }
    }
}
