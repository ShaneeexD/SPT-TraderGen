using System;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using HarmonyLib;

namespace TraderGen.Client.Patches
{
    // Shows the actual pocket slot count difference for Pockets quest rewards.
    internal static class QuestPocketRewardPatch
    {
        internal static ManualLogSource Log;

        internal static void Init(ManualLogSource log) => Log = log;

        // Overrides the reward display text for Pockets rewards.
        [HarmonyPatch(typeof(GClass3812), "smethod_1")]
        internal static class Smethod1Patch
        {
            static void Postfix(
                QuestRewardDataClass reward,
                ref string typeText,
                ref string nameText,
                ref string valueText,
                ref string descriptionText)
            {
                if (reward.type != ERewardType.Pockets)
                    return;

                try
                {
                    int diff = CalculatePocketDifference(reward.target);
                    string sign = diff > 0 ? "+" : "";
                    nameText = $"{sign}{diff} pocket cells";
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"[TraderGen] Pocket reward patch failed: {ex.Message}");
                }
            }
        }

        // Calculates the total pocket cell difference between reward and current templates.
        private static int CalculatePocketDifference(string rewardTemplateId)
        {
            string currentTemplateId = GetCurrentPocketTemplateId();
            if (string.IsNullOrEmpty(currentTemplateId))
                return CountPocketCells(rewardTemplateId);

            int currentCells = CountPocketCells(currentTemplateId);
            int rewardCells = CountPocketCells(rewardTemplateId);
            return rewardCells - currentCells;
        }

        // Gets the template ID of the player's currently equipped pocket item.
        private static string GetCurrentPocketTemplateId()
        {
            try
            {
                var app = Singleton<ClientApplication<ISession>>.Instance;
                if (app == null) return null;

                var session = app.Session;
                if (session == null) return null;

                var profile = session.Profile;
                if (profile?.Inventory?.Equipment == null) return null;

                var pocketSlot = profile.Inventory.Equipment.GetSlot(EquipmentSlot.Pockets);
                if (pocketSlot == null) return null;

                var pocketItem = pocketSlot.ContainedItem;
                if (pocketItem == null) return null;

                return pocketItem.TemplateId;
            }
            catch
            {
                return null;
            }
        }

        // Counts the total pocket cells for a given template ID using reflection.
        private static int CountPocketCells(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
                return 0;

            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            if (itemFactory == null)
                return 0;

            var tempItem = itemFactory.CreateItem(MongoID.Generate(true), templateId, null);
            if (tempItem == null)
                return 0;

            var compoundItem = tempItem as CompoundItem;
            if (compoundItem == null)
                return 0;

            var grids = compoundItem.Grids;
            if (grids == null)
                return 0;

            int totalCells = 0;
            foreach (var grid in grids)
            {
                if (grid == null) continue;

                int gridW = 0, gridH = 0;

                // Client-side grids use GridWidth / GridHeight
                var wProp = grid.GetType().GetProperty("GridWidth");
                var hProp = grid.GetType().GetProperty("GridHeight");
                if (wProp != null && hProp != null)
                {
                    gridW = (int)wProp.GetValue(grid);
                    gridH = (int)hProp.GetValue(grid);
                }
                else
                {
                    // Fallback to fields
                    var wField = grid.GetType().GetField("GridWidth");
                    var hField = grid.GetType().GetField("GridHeight");
                    if (wField != null && hField != null)
                    {
                        gridW = (int)wField.GetValue(grid);
                        gridH = (int)hField.GetValue(grid);
                    }
                }

                totalCells += gridW * gridH;
            }

            return totalCells;
        }
    }
}
