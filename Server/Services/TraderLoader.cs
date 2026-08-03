using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Common.Models.Logging;
using TraderGen.Models;
using TraderGen.Validation;
using Path = System.IO.Path;

namespace TraderGen.Services;


// Discovers and loads trader definition JSON files.

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class TraderLoader(ISptLogger<TraderLoader> logger, ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Loaded trader with source information.
    public record LoadedTrader(TraderDefinition Definition, string SourceFile, string PackFolder);

    // Load all trader definitions from TraderGen/traders/ directory.
    public List<LoadedTrader> LoadAllTraders()
    {
        var results = new List<LoadedTrader>();
        var assembly = Assembly.GetExecutingAssembly();
        var modPath = modHelper.GetAbsolutePathToModFolder(assembly);
        var tradersDir = Path.Combine(modPath, "traders");

        if (!Directory.Exists(tradersDir))
        {
            Directory.CreateDirectory(tradersDir);
            logger.LogWithColor("[TraderGen] Created traders/ directory. Place trader pack folders here.", LogColor.Yellow);
            return results;
        }

        // Load from subfolders
        foreach (var packDir in Directory.GetDirectories(tradersDir))
        {
            var traderFile = Path.Combine(packDir, "trader.json");
            if (!File.Exists(traderFile))
                continue;

            var loaded = TryLoadTraderFile(traderFile, packDir);
            if (loaded != null)
            {
                results.Add(loaded);
            }
        }

        // Load loose JSON files
        foreach (var jsonFile in Directory.GetFiles(tradersDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            var loaded = TryLoadTraderFile(jsonFile, tradersDir);
            if (loaded != null)
            {
                results.Add(loaded);
            }
        }

        return results;
    }

    // Load a single trader definition.
    public LoadedTrader? LoadTraderFromPath(string jsonFilePath, string packFolder)
    {
        return TryLoadTraderFile(jsonFilePath, packFolder);
    }

    private LoadedTrader? TryLoadTraderFile(string jsonFilePath, string packFolder)
    {
        var fileName = Path.GetFileName(jsonFilePath);
        try
        {
            var jsonContent = File.ReadAllText(jsonFilePath);
            var trader = JsonSerializer.Deserialize<TraderDefinition>(jsonContent, JsonOptions);

            if (trader == null)
            {
                logger.LogWithColor($"[TraderGen] Failed to parse '{fileName}': JSON deserialized to null.", LogColor.Red);
                return null;
            }

            // Skip disabled traders
            if (!trader.Enabled)
            {
                logger.LogWithColor($"[TraderGen] Skipping disabled trader in '{fileName}'", LogColor.Yellow);
                return null;
            }

            // Validate the trader definition
            var errors = TraderValidator.Validate(trader, fileName);
            if (errors.Count > 0)
            {
                logger.LogWithColor($"[TraderGen] Validation errors in '{fileName}':", LogColor.Red);
                foreach (var error in errors)
                {
                    logger.LogWithColor($"  ✗ {error}", LogColor.Red);
                }
                return null;
            }

            // Log warnings (non-fatal)
            var warnings = TraderValidator.GetWarnings(trader, fileName);
            foreach (var warning in warnings)
            {
                logger.LogWithColor($"  ⚠ {warning}", LogColor.Yellow);
            }

            logger.LogWithColor($"[TraderGen] Loaded trader '{trader.Nickname}' from '{fileName}'", LogColor.Green);
            return new LoadedTrader(trader, jsonFilePath, packFolder);
        }
        catch (JsonException ex)
        {
            logger.LogWithColor($"[TraderGen] JSON parse error in '{fileName}': {ex.Message}", LogColor.Red);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[TraderGen] Error loading '{fileName}': {ex.Message}", LogColor.Red);
            return null;
        }
    }
}
