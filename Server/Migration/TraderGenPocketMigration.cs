using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Migration;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Migration.Migrations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services;

namespace TraderGen.Migration;

/// <summary>
/// Ensures TraderGen custom pocket templates survive the InvalidPocketFix migration.
/// Phase 1 (CanMigrate): injects templates into GetItems() early.
/// Phase 2 (PostMigrate): runs AFTER InvalidPocketFix (declared as prerequisite) and
/// restores any pocket TPL that InvalidPocketFix incorrectly reset to default, by
/// checking which TraderGen pocket-reward quests the player has completed.
/// </summary>
[Injectable]
public class TraderGenPocketMigration(DatabaseService databaseService, ModHelper modHelper, ISptLogger<TraderGenPocketMigration> logger) : AbstractProfileMigration
{
#pragma warning disable CS0618
    public override string FromVersion => "~4.0";
    public override string ToVersion => "~4.0";
#pragma warning restore CS0618

    public override string MigrationName => "TraderGenPocketMigration";

    // Run AFTER InvalidPocketFix so PostMigrate can fix what it may have reset
    public override IEnumerable<Type> PrerequisiteMigrations => [typeof(InvalidPocketFix)];

    public override bool CanMigrate(JsonObject profile, IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        InjectAllPocketTemplates();
        return true;
    }

    public override JsonObject? Migrate(JsonObject profile) => profile;

    public override bool PostMigrate(SptProfile profile)
    {
        try
        {
            var pmc = profile?.CharacterData?.PmcData;
            if (pmc?.Quests == null || pmc.Inventory?.Items == null) return true;

            // Build questId -> pocketTpl map from all TraderGen quest packs
            var questPocketRewards = BuildQuestPocketRewardMap();
            if (questPocketRewards.Count == 0) return true;

            // Find the most recently completed quest with a pocket reward
            string? correctTpl = null;
            foreach (var quest in pmc.Quests)
            {
                if (quest.Status != QuestStatusEnum.Success) continue;
                if (questPocketRewards.TryGetValue(quest.QId.ToString(), out var tpl))
                    correctTpl = tpl; // last one wins
            }

            if (correctTpl == null) return true;

            // Restore ALL pocket items that have been reset (main pocket + equipment stand pocket)
            foreach (var pocketItem in pmc.Inventory.Items.Where(i => i.SlotId == "Pockets"))
            {
                if (pocketItem.Template.ToString() != correctTpl)
                    pocketItem.Template = new MongoId(correctTpl);
            }
        }
        catch
        {
            // Never fail a migration
        }

        return true;
    }

    private Dictionary<string, string> BuildQuestPocketRewardMap()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        if (string.IsNullOrEmpty(modPath)) return result;

        var tradersDir = System.IO.Path.Combine(modPath, "traders");
        if (!System.IO.Directory.Exists(tradersDir)) return result;

        foreach (var questFile in System.IO.Directory.EnumerateFiles(tradersDir, "quests.json", SearchOption.AllDirectories))
        {
            try
            {
                var node = JsonNode.Parse(System.IO.File.ReadAllText(questFile));
                var storyQuests = node?["storyQuests"]?.AsArray();
                if (storyQuests == null) continue;

                foreach (var questNode in storyQuests)
                {
                    var questId = questNode?["id"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(questId)) continue;

                    var slots = questNode?["rewards"]?["customPocket"]?["slots"]?.AsArray();
                    if (slots == null || slots.Count == 0) continue;

                    result[questId] = GenerateDeterministicId(ComputeLayoutKey(slots));
                }
            }
            catch { }
        }

        return result;
    }

    private int InjectAllPocketTemplates()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        if (string.IsNullOrEmpty(modPath)) return 0;

        var questsDir = System.IO.Path.Combine(modPath, "traders");
        if (!System.IO.Directory.Exists(questsDir)) return 0;

        var items = databaseService.GetItems();

        // Use the default pocket template as the base so special slots are preserved.
        // The default 627a4e6b255f7527fb05a0f6 contains SpecialSlot1/2/3 entries, while
        // 557ffd194bdc2d28148b457f and 5af99e9186f7747c447120b8 have empty Slots.
        var baseId = new MongoId("627a4e6b255f7527fb05a0f6");
        if (!items.TryGetValue(baseId, out var baseTemplate))
        {
            baseId = new MongoId("557ffd194bdc2d28148b457f");
            items.TryGetValue(baseId, out baseTemplate);
        }

        if (baseTemplate?.Properties == null) return 0;

        var count = 0;

        foreach (var questFile in System.IO.Directory.EnumerateFiles(questsDir, "quests.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(questFile);
                var node = JsonNode.Parse(json);
                var storyQuests = node?["storyQuests"]?.AsArray();
                if (storyQuests == null) continue;

                foreach (var questNode in storyQuests)
                {
                    var customPocket = questNode?["rewards"]?["customPocket"];
                    if (customPocket == null) continue;

                    var slots = customPocket["slots"]?.AsArray();
                    if (slots == null || slots.Count == 0) continue;

                    var layoutKey = ComputeLayoutKey(slots);
                    var templateId = GenerateDeterministicId(layoutKey);

                    if (items.ContainsKey(new MongoId(templateId))) continue;

                    // Build grids for this layout
                    var grids = new List<Grid>();
                    for (var i = 0; i < slots.Count; i++)
                    {
                        var slot = slots[i];
                        var w = slot?["width"]?.GetValue<int>() ?? 1;
                        var h = slot?["height"]?.GetValue<int>() ?? 2;
                        grids.Add(new Grid
                        {
                            Id = GenerateSlotId(templateId, i),
                            Name = $"pocket{i + 1}",
                            Parent = templateId,
                            Properties = new GridProperties
                            {
                                CellsH = w,
                                CellsV = h,
                                Filters =
                                [
                                    new GridFilter
                                    {
                                        Filter = [new MongoId("54009119af1c881c07000029")],
                                        ExcludedFilter = [new MongoId("5448bf274bdc2dfc2f8b456a")],
                                    },
                                ],
                                IsSortingTable = false,
                                MinCount = 0,
                                MaxCount = 0,
                                MaxWeight = 0,
                            },
                            Prototype = "55d329c24bdc2d892f8b4567",
                        });
                    }

                    var newTemplate = baseTemplate with
                    {
                        Id = new MongoId(templateId),
                        Name = $"TraderGen Custom Pocket {templateId[..8]}",
                        Properties = baseTemplate.Properties! with { Grids = grids },
                    };

                    items[new MongoId(templateId)] = newTemplate;
                    count++;
                }
            }
            catch
            {
                // Silently skip malformed quest files — main loader will report errors
            }
        }

        return count;
    }

    private static string ComputeLayoutKey(JsonArray slots)
    {
        var sb = new StringBuilder();
        foreach (var slot in slots)
        {
            var w = slot?["width"]?.GetValue<int>() ?? 1;
            var h = slot?["height"]?.GetValue<int>() ?? 2;
            sb.Append(w);
            sb.Append('x');
            sb.Append(h);
            sb.Append(',');
        }
        return sb.ToString();
    }

    private static string GenerateDeterministicId(string layoutKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("tradergen_pocket_" + layoutKey));
        return Convert.ToHexStringLower(hash[..12]);
    }

    // Deterministic slot IDs derived from template ID + slot index
    private static string GenerateSlotId(string templateId, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(templateId + "_slot_" + index));
        return Convert.ToHexStringLower(hash[..12]);
    }
}
