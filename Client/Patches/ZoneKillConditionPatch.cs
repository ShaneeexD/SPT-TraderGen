using EFT.Quests;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;

namespace TraderGen.Client.Patches
{
    // Extends quest zone condition handlers to support InZone conditions for custom zone_kill objectives.
    internal static class ZoneKillConditionPatch
    {
        internal static ManualLogSource Log;

        internal static void Init(ManualLogSource log, Harmony harmony)
        {
            Log = log;
            try
            {
                var target = AccessTools.Method(
                    typeof(QuestBookClass),
                    nameof(QuestBookClass.GetConditionHandlersByZone),
                    new[] { typeof(string) },
                    new[] { typeof(ConditionZone) });

                var postfix = new HarmonyMethod(typeof(ZoneKillConditionPatch), nameof(Postfix));
                harmony.Patch(target, postfix: postfix);
                log.LogInfo("[TraderGen] ZoneKillConditionPatch applied.");
            }
            catch (Exception ex)
            {
                log.LogWarning($"[TraderGen] ZoneKillConditionPatch failed to apply: {ex.Message}");
            }
        }

        public static void Postfix(
            QuestBookClass __instance,
            string zoneId,
            ref IEnumerable<ConditionProgressChecker> __result)
        {
            try
            {
                var list = __result?.ToList() ?? new List<ConditionProgressChecker>();

                foreach (var quest in __instance)
                {
                    if (quest.QuestStatus != EQuestStatus.Started &&
                        quest.QuestStatus != EQuestStatus.AvailableForFinish)
                        continue;

                    if (quest.Conditions == null)
                        continue;

                    foreach (var kvp in quest.Conditions)
                    {
                        var status = kvp.Key;
                        var conditions = kvp.Value;

                        if (!quest.CurrentStatusTransitions.Contains(status) &&
                            status != quest.QuestStatus)
                            continue;

                        foreach (var cond in conditions)
                        {
                            if (cond is not ConditionCounterCreator cc || cc.Conditions == null)
                                continue;

                            foreach (var child in cc.Conditions)
                            {
                                if (child is not ConditionInZone inZone)
                                    continue;

                                if (inZone.zoneIds == null || !inZone.zoneIds.Contains(zoneId))
                                    continue;

                                if (!quest.ProgressCheckers.TryGetValue(child, out var cpc) || cpc == null)
                                    continue;

                                if (!list.Contains(cpc))
                                    list.Add(cpc);
                            }
                        }
                    }
                }

                __result = list;
            }
            catch
            {
                // Silently fail — don't break quest tracking if patch errors
            }
        }
    }
}
