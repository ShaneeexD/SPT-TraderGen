using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using TraderGen.Models;

// Discovers and loads quest pack JSON files from trader pack folders.
namespace TraderGen.Services;

public static class QuestLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Loaded quest pack with source information.
    public record LoadedQuestPack(
        QuestPackDefinition Definition,
        string SourceFile,
        string PackFolder,
        string TraderId
    );

    // Load quest packs from trader folders.
    public static List<LoadedQuestPack> LoadAllQuestPacks(
        IEnumerable<TraderLoader.LoadedTrader> loadedTraders,
        ISptLogger<TraderGenPlugin> logger)
    {
        var results = new List<LoadedQuestPack>();

        foreach (var loaded in loadedTraders)
        {
            var questFile = Path.Combine(loaded.PackFolder, "quests.json");
            if (!File.Exists(questFile))
                continue;

            var pack = TryLoadQuestFile(questFile, loaded.PackFolder, loaded.Definition.Id, logger);
            if (pack != null)
            {
                results.Add(pack);
            }
        }

        return results;
    }

    private static LoadedQuestPack? TryLoadQuestFile(
        string questFilePath,
        string packFolder,
        string traderId,
        ISptLogger<TraderGenPlugin> logger)
    {
        var fileName = Path.GetFileName(questFilePath);
        var packName = Path.GetFileName(packFolder);
        try
        {
            var jsonContent = File.ReadAllText(questFilePath);
            var questPack = JsonSerializer.Deserialize<QuestPackDefinition>(jsonContent, JsonOptions);

            if (questPack == null)
            {
                logger.LogWithColor(
                    $"[TraderGen] Failed to parse quest file '{packName}/{fileName}': JSON deserialized to null.",
                    LogColor.Red);
                return null;
            }

            var totalQuests = questPack.StoryQuests.Count + questPack.RotatingQuests.Count;
            if (totalQuests == 0 && questPack.ProductionSchemes.Count == 0 && questPack.Zones.Count == 0)
            {
                logger.LogWithColor(
                    $"[TraderGen] Quest file '{packName}/{fileName}' has no quests, schemes, or zones. Skipping.",
                    LogColor.Yellow);
                return null;
            }

            logger.LogWithColor(
                $"[TraderGen] Loaded quest pack from '{packName}/{fileName}': " +
                $"{questPack.StoryQuests.Count} story, {questPack.RotatingQuests.Count} rotating template(s)",
                LogColor.Green);

            return new LoadedQuestPack(questPack, questFilePath, packFolder, traderId);
        }
        catch (JsonException ex)
        {
            logger.LogWithColor(
                $"[TraderGen] JSON parse error in '{packName}/{fileName}': {ex.Message}",
                LogColor.Red);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWithColor(
                $"[TraderGen] Error loading quest file '{packName}/{fileName}': {ex.Message}",
                LogColor.Red);
            return null;
        }
    }
}
