using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services;
using TraderGen.Models;

namespace TraderGen.Services;

// Generates custom pocket templates and injects them into the SPT items database.
public class CustomPocketInjector(DatabaseService databaseService)
{
    // Cache to avoid generating duplicate templates for the same layout.
    private readonly Dictionary<string, string> _injectedLayouts = new();

    // Generates and registers a custom pocket template for the given definition.
    public string Inject(CustomPocketDefinition definition, string customItemsDir)
    {
        var layoutKey = ComputeLayoutKey(definition);

        if (_injectedLayouts.TryGetValue(layoutKey, out var existingId))
        {
            return existingId;
        }

        var items = databaseService.GetItems();

        // Clone the default pocket template as a base so special slots are preserved.
        // 557ffd194bdc2d28148b457f and 5af99e9186f7747c447120b8 have empty Slots,
        // while the default 627a4e6b255f7527fb05a0f6 contains the SpecialSlot1/2/3 entries.
        var baseId = new MongoId("627a4e6b255f7527fb05a0f6");
        if (!items.TryGetValue(baseId, out var baseTemplate))
        {
            // Fallback: try the root pocket template
            baseId = new MongoId("557ffd194bdc2d28148b457f");
            items.TryGetValue(baseId, out baseTemplate);
        }

        if (baseTemplate == null || baseTemplate.Properties == null)
        {
            throw new InvalidOperationException("Could not find a base pocket template in the database.");
        }

        var generatedId = GenerateDeterministicId(layoutKey);

        // Build new grids from the custom slot definition
        var grids = new List<Grid>();
        for (var i = 0; i < definition.Slots.Count; i++)
        {
            var slot = definition.Slots[i];
            var slotId = GenerateSlotId(generatedId, i);
            grids.Add(new Grid
            {
                Id = slotId,
                Name = $"pocket{i + 1}",
                Parent = generatedId,
                Properties = new GridProperties
                {
                    CellsH = slot.Width,
                    CellsV = slot.Height,
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

        // Clone the base template using record copy semantics — keeps every other property intact
        var newTemplate = baseTemplate with
        {
            Id = new MongoId(generatedId),
            Name = $"TraderGen Custom Pocket {generatedId[..8]}",
            Properties = baseTemplate.Properties with
            {
                Grids = grids,
            },
        };

        // Serialize to JSON and write to db/TraderGenPockets/ for persistence
        Directory.CreateDirectory(customItemsDir);
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };
        var templateJson = JsonSerializer.Serialize(newTemplate, jsonOptions);
        var templateNode = JsonNode.Parse(templateJson)!;
        // Wrap in the items.json format: { "<id>": { ...item... } }
        var wrapped = new JsonObject { [generatedId] = templateNode };
        var filePath = System.IO.Path.Combine(customItemsDir, $"custom_pocket_{generatedId}.json");
        File.WriteAllText(filePath, wrapped.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Also inject directly into the in-memory database
        items[new MongoId(generatedId)] = newTemplate;
        _injectedLayouts[layoutKey] = generatedId;

        return generatedId;
    }

    // Computes a deterministic key for a pocket layout.
    private static string ComputeLayoutKey(CustomPocketDefinition definition)
    {
        var sb = new StringBuilder();
        foreach (var slot in definition.Slots)
        {
            sb.Append(slot.Width);
            sb.Append('x');
            sb.Append(slot.Height);
            sb.Append(',');
        }
        return sb.ToString();
    }

    // Deterministic slot IDs derived from template ID + slot index
    private static string GenerateSlotId(string templateId, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(templateId + "_slot_" + index));
        return Convert.ToHexStringLower(hash[..12]);
    }

    // Generates a deterministic 24-hex-char template ID from a layout key.
    private static string GenerateDeterministicId(string layoutKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("tradergen_pocket_" + layoutKey));
        return Convert.ToHexStringLower(hash[..12]);
    }
}
