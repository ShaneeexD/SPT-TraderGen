using System.Collections;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TraderGen.ClientPatch;

// Patches the quest reward icon for TraderUnlock rewards to show the trader's avatar.
[HarmonyPatch(typeof(CommonHorizontalRewardView), nameof(CommonHorizontalRewardView.Show))]
public static class TraderUnlockRewardIconPatch
{
    public static void Postfix(CommonHorizontalRewardView __instance, QuestRewardDataClass reward)
    {
        if (reward?.type != ERewardType.TraderUnlock)
        {
            return;
        }

        if (string.IsNullOrEmpty(reward.target))
        {
            return;
        }

        var tradersSettings = Singleton<BackendConfigSettingsClass>.Instance?.TradersSettings;
        if (tradersSettings == null || !tradersSettings.TryGetValue(reward.target, out var traderSettings))
        {
            return;
        }

        var icon = Traverse.Create(__instance).Field<Image>("_icon").Value;
        if (icon == null)
        {
            return;
        }

        // If the avatar is already loaded, use it immediately.
        if (traderSettings.Avatar != null)
        {
            icon.sprite = traderSettings.Avatar;
            return;
        }

        // Otherwise load it asynchronously and assign when ready.
        __instance.StartCoroutine(LoadAvatarCoroutine(traderSettings, icon));
    }

    private static IEnumerator LoadAvatarCoroutine(BackendConfigSettingsClass.TraderSettings traderSettings, Image icon)
    {
        Task<Sprite> task = traderSettings.GetAvatar();
        while (task != null && !task.IsCompleted)
        {
            yield return null;
        }

        if (task != null && task.Status == TaskStatus.RanToCompletion && task.Result != null)
        {
            icon.sprite = task.Result;
        }
    }
}
