using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using TraderGen.Models;
using TraderGen.Services;

namespace TraderGen.Patches;

// Rolls random item pool rewards at quest completion time.
//
// When a quest with random item pool rewards is completed, QuestRewardHelper.ApplyQuestReward
// calls RewardHelper.ApplyRewards with the quest's reward list. This patch intercepts that call
// (Prefix) and, for any reward whose ID is registered in QuestBuilder.RandomPoolRegistry,
// replaces the placeholder "Random Item" with a weighted random pick from the pool.
//
// The replacement happens before ApplyRewards processes the rewards, so the selected item
// is granted to the player normally and appears in the success mail message.
public class RandomItemPoolPatch : AbstractPatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(RewardHelper), nameof(RewardHelper.ApplyRewards));
    }

    [PatchPrefix]
    public static void Prefix(
        IEnumerable<Reward> rewards,
        MongoId rewardSourceId)
    {
        // Fast exit: if the registry is empty, no pools were registered
        if (QuestBuilder.RandomPoolRegistry.Count == 0)
            return;

        foreach (var reward in rewards)
        {
            if (reward.Type != RewardType.Item)
                continue;

            var rewardId = reward.Id.ToString();
            if (string.IsNullOrEmpty(rewardId))
                continue;

            if (!QuestBuilder.RandomPoolRegistry.TryGetValue(rewardId, out var entries) || entries.Count == 0)
                continue;

            // Roll a weighted random entry from the pool
            var selected = RollWeightedRandom(entries);
            if (selected == null)
                continue;

            // Build the replacement item list
            var newItemId = new MongoId().ToString();
            var newItems = new List<Item>
            {
                new()
                {
                    Id = newItemId,
                    Template = new MongoId(selected.ItemTpl),
                    Upd = new Upd
                    {
                        StackObjectsCount = selected.Count,
                    },
                },
            };

            // Flatten child attachments
            if (selected.Children is { Count: > 0 })
            {
                FlattenChildren(selected.Children, newItemId, newItems);
            }

            // Replace the reward's items, target, and value
            reward.Items = newItems;
            reward.Target = newItemId;
            reward.Value = selected.Count;
        }
    }

    // Roll a weighted random entry from the pool.
    private static RandomItemPoolEntry? RollWeightedRandom(List<RandomItemPoolEntry> entries)
    {
        var totalWeight = 0;
        foreach (var entry in entries)
        {
            totalWeight += Math.Max(1, entry.Weight);
        }

        var roll = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        foreach (var entry in entries)
        {
            cumulative += Math.Max(1, entry.Weight);
            if (roll < cumulative)
                return entry;
        }

        return entries[^1]; // Fallback
    }

    // Recursively flatten child items into the reward item list.
    private static void FlattenChildren(List<AssortChildItem> children, string parentId, List<Item> output)
    {
        foreach (var child in children)
        {
            var childId = child.ItemId ?? new MongoId().ToString();
            output.Add(new Item
            {
                Id = childId,
                Template = new MongoId(child.ItemTpl),
                ParentId = parentId,
                SlotId = child.SlotId,
            });

            if (child.Children is { Count: > 0 })
            {
                FlattenChildren(child.Children, childId, output);
            }
        }
    }
}
