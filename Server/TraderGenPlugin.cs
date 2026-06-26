using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using TraderGen.Generators;
using TraderGen.Patches;
using TraderGen.Services;
using TraderGen.Validation;

namespace TraderGen;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.serenity.tradergen";
    public override string Name { get; init; } = "TraderGen";
    public override string Author { get; init; } = "Serenity";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.2");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("4.0.13");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new("~2.0.20") }
    };
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
    DatabaseService databaseService,
    ImageRouter imageRouter,
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    ProfileHelper profileHelper,
    TimeUtil timeUtil
) : IOnLoad
{
    public async Task OnLoad()
    {
        logger.LogWithColor("[TraderGen] ====================================", LogTextColor.Cyan);
        logger.LogWithColor("[TraderGen] TraderGen Framework v2.0.2 loading...", LogTextColor.Cyan);
        logger.LogWithColor("[TraderGen] ====================================", LogTextColor.Cyan);

        // Load trader JSON files from traders/ directory
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
            // Register each trader independently
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

        // Load and register quests
        await LoadAndRegisterQuests(loadedTraders);
    }

    private async Task LoadAndRegisterQuests(List<TraderLoader.LoadedTrader> loadedTraders)
    {
        // Load quest packs from trader folders
        var questPacks = QuestLoader.LoadAllQuestPacks(loadedTraders, logger);
        if (questPacks.Count == 0)
            return;

        logger.LogWithColor($"[TraderGen] Found {questPacks.Count} quest pack(s). Processing...", LogTextColor.Cyan);

        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var questOutputDir = Path.Combine(modPath, "db", "CustomQuests");

        // Clean previous generated files (rebuilt every startup)
        if (Directory.Exists(questOutputDir))
            Directory.Delete(questOutputDir, true);

        var totalStoryQuests = 0;
        var questPacksFailed = 0;
        var allRotatingTemplates = new List<(List<Models.RotatingQuestTemplate> Templates, string TraderId, string PackFolder)>();

        foreach (var questPack in questPacks)
        {
            var packName = Path.GetFileName(questPack.PackFolder);

            // Validate quest pack
            var errors = QuestValidator.Validate(questPack.Definition, questPack.TraderId, packName);
            if (errors.Count > 0)
            {
                logger.LogWithColor($"[TraderGen] Quest validation errors in '{packName}':", LogTextColor.Red);
                foreach (var error in errors)
                    logger.LogWithColor($"  \u2717 {error}", LogTextColor.Red);
                questPacksFailed++;
                continue;
            }

            // Process custom pocket templates — generate item JSON in db/TraderGenPockets/ and replace customPocket with pockets ID
            var pocketInjector = new CustomPocketInjector(databaseService);
            var traderGenPocketsDir = System.IO.Path.Combine(modPath, "db", "TraderGenPockets");
            Directory.CreateDirectory(traderGenPocketsDir);
            foreach (var quest in questPack.Definition.StoryQuests)
            {
                if (quest.Rewards.CustomPocket is { Slots.Count: > 0 })
                {
                    var pocketId = pocketInjector.Inject(quest.Rewards.CustomPocket, traderGenPocketsDir);
                    quest.Rewards.Pockets = pocketId;
                    quest.Rewards.CustomPocket = null;
                }
            }

            // Collect story quests
            var storyQuests = new List<Models.StoryQuestDefinition>(questPack.Definition.StoryQuests);

            // Collect rotating templates for later processing
            if (questPack.Definition.RotatingQuests.Count > 0)
            {
                allRotatingTemplates.Add((questPack.Definition.RotatingQuests, questPack.TraderId, questPack.PackFolder));
            }

            // Build BSG-format quest files
            if (storyQuests.Count > 0)
            {
                var count = QuestBuilder.BuildQuestFiles(
                    questPack.TraderId, storyQuests, questOutputDir,
                    questPack.PackFolder, questPack.Definition.DefaultQuestIcon, databaseService, logger);
                if (count > 0)
                    totalStoryQuests += count;
                else
                    questPacksFailed++;
            }
        }

        // Register custom zones from all quest packs
        await RegisterQuestZones(questPacks, modPath);

        // Build the quest->custom pocket map and enable the server-side fix that
        // restores the correct pocket TPL every time the profile is served to the client.
        PocketServeFixPatch.BuildMap(modPath);
        PocketServeFixPatch.SetDependencies(profileHelper);
        new PocketServeFixPatch().Enable();

        if (totalStoryQuests > 0)
        {
            // Register story quests into SPT database
            var assembly = Assembly.GetExecutingAssembly();
            await wttCommon.CustomQuestService.CreateCustomQuests(assembly);

            logger.LogWithColor(
                $"[TraderGen] Registered {totalStoryQuests} story quest(s) via WTT CustomQuestService.",
                LogTextColor.Green);
        }

        // Process rotating quests via repeatable quest system
        if (allRotatingTemplates.Count > 0)
        {
            SetupRepeatableQuests(allRotatingTemplates);
        }

        if (questPacksFailed > 0)
        {
            logger.LogWithColor(
                $"[TraderGen] {questPacksFailed} quest pack(s) failed validation or building.",
                LogTextColor.Yellow);
        }
    }

    private async Task RegisterQuestZones(List<QuestLoader.LoadedQuestPack> questPacks, string modPath)
    {
        var allZones = questPacks
            .SelectMany(p => p.Definition.Zones)
            .ToList();

        if (allZones.Count == 0)
            return;

        // Write zone JSON into db/CustomQuestZones/ so WTT can pick them up
        var zoneOutputDir = Path.Combine(modPath, "db", "CustomQuestZones");
        if (Directory.Exists(zoneOutputDir))
            Directory.Delete(zoneOutputDir, true);
        Directory.CreateDirectory(zoneOutputDir);

        var zoneJsonOpts = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        };

        // WTT loads files as List<CustomQuestZone> — write one file per pack
        foreach (var pack in questPacks)
        {
            if (pack.Definition.Zones.Count == 0) continue;
            var packName = Path.GetFileName(pack.PackFolder);
            var zoneFile = Path.Combine(zoneOutputDir, $"{packName}_zones.json");

            var wttZones = pack.Definition.Zones.Select(z => new WTTServerCommonLib.Models.CustomQuestZone
            {
                ZoneId = z.ZoneId,
                ZoneName = z.ZoneName,
                ZoneLocation = z.ZoneLocation.ToLowerInvariant(),
                ZoneType = z.ZoneType,
                FlareType = z.FlareType,
                Position = new WTTServerCommonLib.Models.ZoneTransform(z.Position.X, z.Position.Y, z.Position.Z),
                Rotation = new WTTServerCommonLib.Models.ZoneTransform(z.Rotation.X, z.Rotation.Y, z.Rotation.Z, z.Rotation.W),
                Scale = new WTTServerCommonLib.Models.ZoneTransform(z.Scale.X, z.Scale.Y, z.Scale.Z),
            }).ToList();

            File.WriteAllText(zoneFile, System.Text.Json.JsonSerializer.Serialize(wttZones, zoneJsonOpts));
        }

        var assembly = Assembly.GetExecutingAssembly();
        await wttCommon.CustomQuestZoneService.CreateCustomQuestZones(assembly);

        logger.LogWithColor(
            $"[TraderGen] Registered {allZones.Count} quest zone(s) via WTT CustomQuestZoneService.",
            LogTextColor.Green);
    }

    private void SetupRepeatableQuests(List<(List<Models.RotatingQuestTemplate> Templates, string TraderId, string PackFolder)> allTemplates)
    {
        logger.LogWithColor("[TraderGen] Setting up repeatable quests via Harmony patch...", LogTextColor.Cyan);

        var totalTemplates = 0;
        foreach (var (templates, traderId, packFolder) in allTemplates)
        {
            // Register image routes for template icons
            var imagePaths = RepeatableQuestGenerator.GetTemplateImagePaths(templates, packFolder);
            foreach (var (routePath, absFilePath) in imagePaths)
            {
                imageRouter.AddRoute(routePath, absFilePath);
            }

            // Register templates with the patch
            GetRepeatableQuestsPatch.RegisterTrader(new GetRepeatableQuestsPatch.TraderGenData
            {
                TraderId = traderId,
                Templates = templates,
                PackFolder = packFolder,
            });

            totalTemplates += templates.Count;
        }

        if (totalTemplates == 0)
        {
            logger.LogWithColor("[TraderGen] No repeatable quest templates found.", LogTextColor.Yellow);
            return;
        }

        // Pre-register locale entries
        PreRegisterLocales(allTemplates);

        // Enable Harmony patches
        GetRepeatableQuestsPatch.SetDependencies(profileHelper, timeUtil);
        new GetRepeatableQuestsPatch().Enable();

        // Register locale entries via standard registrar
        RepeatableQuestLocaleRegistrar.RegisterLocales(databaseService, logger);

        logger.LogWithColor(
            $"[TraderGen] Registered {totalTemplates} quest template(s) from {allTemplates.Count} trader(s). Quests will be generated on-demand via patch.",
            LogTextColor.Green);
    }

    // Pre-registers locale entries at startup so quest descriptions work when generated.
    private void PreRegisterLocales(List<(List<Models.RotatingQuestTemplate> Templates, string TraderId, string PackFolder)> allTemplates)
    {
        logger.LogWithColor("[TraderGen] Pre-registering locale entries for repeatable quests...", LogTextColor.Cyan);

        var totalEntries = 0;
        var localeTable = databaseService.GetLocales().Global;

        foreach (var (templates, traderId, packFolder) in allTemplates)
        {
            foreach (var template in templates)
            {
                // Generate locale entries per quest slot
                for (var i = 0; i < template.QuestCount; i++)
                {
                    var questId = RepeatableQuestGenerator.DeriveQuestId(template.Id, i);
                    
                    // Use the same seeded RNG as quest generation for consistency
                    // This ensures the pre-registered locale matches the actual quest
                    var rng = new Random((template.Id + ":" + i).GetHashCode());
                    var name = template.NamePool[rng.Next(template.NamePool.Count)];
                    var description = template.DescriptionPool.Count > 0
                        ? template.DescriptionPool[rng.Next(template.DescriptionPool.Count)]
                        : "Complete the assigned task.";
                    
                    // Pick location for {location} placeholder
                    string? pickedLocation = null;
                    foreach (var obj in template.Objectives)
                    {
                        if (obj.LocationPool.Count > 0)
                        {
                            pickedLocation = obj.LocationPool[rng.Next(obj.LocationPool.Count)];
                            break;
                        }
                    }
                    
                    // Replace placeholder
                    var locationDisplay = !string.IsNullOrWhiteSpace(pickedLocation) && pickedLocation != "any"
                        ? Services.LocationHelper.ToDisplayName(pickedLocation)
                        : "Tarkov";
                    name = name.Replace("{location}", locationDisplay);
                    description = description.Replace("{location}", locationDisplay);
                    
                    // Store in locale store
                    RepeatableQuestLocaleStore.Add(questId.ToString(), name, description);
                    totalEntries++;
                }
            }
        }

        // Register locales with SPT database
        var locales = RepeatableQuestLocaleStore.GetAll();
        var conditionLocales = RepeatableQuestLocaleStore.GetAllConditions();

        foreach (var (locale, lazyDict) in localeTable)
        {
            lazyDict.AddTransformer(dict =>
            {
                foreach (var (questId, (name, description)) in locales)
                {
                    dict.TryAdd($"{questId} name", name);
                    dict.TryAdd($"{questId} description", description);
                    dict.TryAdd($"{questId} note", "");
                    dict.TryAdd($"{questId} successMessageText", "Quest complete. Well done.");
                    dict.TryAdd($"{questId} failMessageText", "Quest failed.");
                    dict.TryAdd($"{questId} startedMessageText", "Quest accepted.");
                    dict.TryAdd($"{questId} changeQuestMessageText", "Quest replaced.");
                    dict.TryAdd($"{questId} acceptPlayerMessage", "");
                    dict.TryAdd($"{questId} declinePlayerMessage", "");
                    dict.TryAdd($"{questId} completePlayerMessage", "");
                }

                foreach (var (conditionId, text) in conditionLocales)
                {
                    dict.TryAdd(conditionId, text);
                }

                return dict;
            });
        }

        logger.LogWithColor(
            $"[TraderGen] Pre-registered {totalEntries} locale entries across {locales.Count} quests.",
            LogTextColor.Green);
    }
}
