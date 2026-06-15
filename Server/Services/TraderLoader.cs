using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using TraderGen.Models;
using TraderGen.Validation;
using Path = System.IO.Path;

namespace TraderGen.Services;


// Discovers and loads trader definition JSON files from:
//   1. The TraderGen/traders/ folder (user-placed packs)
//   2. Other mod folders that register themselves as trader packs

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TraderLoader(ISptLogger<TraderLoader> logger, ModHelper modHelper)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Holds a loaded trader definition along with its source path information.
    public record LoadedTrader(TraderDefinition Definition, string SourceFile, string PackFolder);

    // Load all trader definitions from the TraderGen/traders/ directory.
    // Each subfolder is treated as a trader pack.
    // Also loads any loose .json files directly in the traders/ folder.
    public List<LoadedTrader> LoadAllTraders()
    {
        var results = new List<LoadedTrader>();
        var assembly = Assembly.GetExecutingAssembly();
        var modPath = modHelper.GetAbsolutePathToModFolder(assembly);
        var tradersDir = Path.Combine(modPath, "traders");

        if (!Directory.Exists(tradersDir))
        {
            Directory.CreateDirectory(tradersDir);
            logger.LogWithColor("[TraderGen] Created traders/ directory. Place trader pack folders here.", LogTextColor.Yellow);
            return results;
        }

        // Load from subfolders (recommended structure: traders/MyTraderPack/trader.json)
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

        // Also load loose .json files directly in traders/
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

    // Load a single trader definition from another mod's folder.
    // Other mods can call this to register their own trader packs.
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
                logger.LogWithColor($"[TraderGen] Failed to parse '{fileName}': JSON deserialized to null.", LogTextColor.Red);
                return null;
            }

            // Skip disabled traders
            if (!trader.Enabled)
            {
                logger.LogWithColor($"[TraderGen] Skipping disabled trader in '{fileName}'", LogTextColor.Yellow);
                return null;
            }

            // Validate the trader definition
            var errors = TraderValidator.Validate(trader, fileName);
            if (errors.Count > 0)
            {
                logger.LogWithColor($"[TraderGen] Validation errors in '{fileName}':", LogTextColor.Red);
                foreach (var error in errors)
                {
                    logger.LogWithColor($"  ✗ {error}", LogTextColor.Red);
                }
                return null;
            }

            // Log warnings (non-fatal)
            var warnings = TraderValidator.GetWarnings(trader, fileName);
            foreach (var warning in warnings)
            {
                logger.LogWithColor($"  ⚠ {warning}", LogTextColor.Yellow);
            }

            logger.LogWithColor($"[TraderGen] Loaded trader '{trader.Nickname}' from '{fileName}'", LogTextColor.Green);
            return new LoadedTrader(trader, jsonFilePath, packFolder);
        }
        catch (JsonException ex)
        {
            logger.LogWithColor($"[TraderGen] JSON parse error in '{fileName}': {ex.Message}", LogTextColor.Red);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWithColor($"[TraderGen] Error loading '{fileName}': {ex.Message}", LogTextColor.Red);
            return null;
        }
    }
}
