using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using TraderGen.Services;
using TraderGen.Validation;

namespace TraderGen;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.serenity.tradergen";
    public override string Name { get; init; } = "TraderGen";
    public override string Author { get; init; } = "Serenity";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("4.0.13");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TraderGenPlugin(
    ISptLogger<TraderGenPlugin> logger,
    ModHelper modHelper,
    TraderLoader traderLoader,
    TraderRegistrar traderRegistrar,
    WTTServerCommonLib.WTTServerCommonLib wttCommon
) : IOnLoad
{
    public async Task OnLoad()
    {
        logger.LogWithColor("[TraderGen] ====================================", LogTextColor.Cyan);
        logger.LogWithColor("[TraderGen] TraderGen Framework v1.0.0 loading...", LogTextColor.Cyan);
        logger.LogWithColor("[TraderGen] ====================================", LogTextColor.Cyan);

        // Discover and load all trader JSON files from the traders/ directory
        var loadedTraders = traderLoader.LoadAllTraders();

        if (loadedTraders.Count == 0)
        {
            logger.LogWithColor(
                "[TraderGen] No trader packs found. Place trader pack folders in: user/mods/TraderGen/traders/",
                LogTextColor.Yellow
            );
            return;
        }

        logger.LogWithColor($"[TraderGen] Found {loadedTraders.Count} trader definition(s). Registering...", LogTextColor.Cyan);

        var successCount = 0;
        var failCount = 0;

        foreach (var loaded in loadedTraders)
        {
            // Each trader is registered independently — one failure won't crash the others
            var success = traderRegistrar.RegisterTrader(loaded);
            if (success)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        logger.LogWithColor("[TraderGen] ====================================", LogTextColor.Cyan);
        logger.LogWithColor(
            $"[TraderGen] Done! {successCount} trader(s) registered, {failCount} failed.",
            failCount > 0 ? LogTextColor.Yellow : LogTextColor.Green
        );
        logger.LogWithColor("[TraderGen] ====================================", LogTextColor.Cyan);

        // ==================== Quest Loading Pipeline ====================
        await LoadAndRegisterQuests(loadedTraders);
    }

    private async Task LoadAndRegisterQuests(List<TraderLoader.LoadedTrader> loadedTraders)
    {
        // Discover quest packs from trader pack folders
        var questPacks = QuestLoader.LoadAllQuestPacks(loadedTraders, logger);
        if (questPacks.Count == 0)
            return;

        logger.LogWithColor($"[TraderGen] Found {questPacks.Count} quest pack(s). Processing...", LogTextColor.Cyan);

        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var questOutputDir = Path.Combine(modPath, "db", "CustomQuests");

        // Clean previous generated BSG-format files (they are rebuilt every startup)
        if (Directory.Exists(questOutputDir))
            Directory.Delete(questOutputDir, true);

        var totalQuests = 0;
        var questPacksFailed = 0;

        foreach (var questPack in questPacks)
        {
            var packName = Path.GetFileName(questPack.PackFolder);

            // Validate
            var errors = QuestValidator.Validate(questPack.Definition, questPack.TraderId, packName);
            if (errors.Count > 0)
            {
                logger.LogWithColor($"[TraderGen] Quest validation errors in '{packName}':", LogTextColor.Red);
                foreach (var error in errors)
                    logger.LogWithColor($"  \u2717 {error}", LogTextColor.Red);
                questPacksFailed++;
                continue;
            }

            // Collect story quests
            var allQuests = new List<Models.StoryQuestDefinition>(questPack.Definition.StoryQuests);

            // Resolve rotating quests (load cached or generate new, with expiration tracking)
            if (questPack.Definition.RotatingQuests.Count > 0)
            {
                var rotatingQuests = RotatingQuestService.ResolveRotatingQuests(
                    questPack.Definition.RotatingQuests,
                    questPack.TraderId,
                    modPath,
                    logger);
                allQuests.AddRange(rotatingQuests);
            }

            // Build BSG-format quest files for WTT lib
            var count = QuestBuilder.BuildQuestFiles(
                questPack.TraderId, allQuests, questOutputDir,
                questPack.PackFolder, questPack.Definition.DefaultQuestIcon, logger);
            if (count > 0)
                totalQuests += count;
            else
                questPacksFailed++;
        }

        if (totalQuests > 0)
        {
            // Use WTT library to register the generated quests into the SPT database
            var assembly = Assembly.GetExecutingAssembly();
            await wttCommon.CustomQuestService.CreateCustomQuests(assembly);

            logger.LogWithColor(
                $"[TraderGen] Registered {totalQuests} quest(s) via WTT CustomQuestService.",
                LogTextColor.Green);
        }

        if (questPacksFailed > 0)
        {
            logger.LogWithColor(
                $"[TraderGen] {questPacksFailed} quest pack(s) failed validation or building.",
                LogTextColor.Yellow);
        }
    }
}
