using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TraderGen.Models;

namespace TraderGen.Services;

#pragma warning disable CS0618

// Loads the active game locale file from a pack's locales/ folder into SPT's global locale tables.
[Injectable(InjectionType.Singleton)]
public class LocaleLoader(LocaleTable localeTable, LocaleConfig localeConfig, ModHelper modHelper)
#pragma warning restore CS0618
{
    private static readonly Dictionary<string, string> LocaleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["es-es"] = "es",
        ["de"] = "ge",
        ["de-de"] = "ge",
        ["de-at"] = "ge",
        ["de-ch"] = "ge",
        ["pt"] = "po",
        ["pt-pt"] = "po",
        ["pt-br"] = "po",
        ["zh-cn"] = "ch",
        ["zh-tw"] = "ch",
        ["zh-hk"] = "ch",
        ["zh"] = "ch",
        ["cs"] = "cz",
        ["ja"] = "jp",
        ["ko"] = "kr",
        ["tr"] = "tu",
    };

    public void LoadPackLocales(string packFolder, string traderId)
    {
        var localesDir = Path.Combine(packFolder, "locales");
        if (!Directory.Exists(localesDir)) return;

        var localeKey = ResolveLocaleKey();
        var filePath = Path.Combine(localesDir, $"{localeKey}.json");

        if (!File.Exists(filePath))
        {
            // Fallback to English if the pack doesn't have the active language.
            filePath = Path.Combine(localesDir, "en.json");
            if (!File.Exists(filePath)) return;
        }

        var globalLocales = localeTable.Global;
        if (!globalLocales.TryGetValue(localeKey, out var localeLazy))
        {
            Console.WriteLine($"[TraderGen.LocaleLoader] WARN: language key '{localeKey}' not found in Global locales");
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (entries == null || entries.Count == 0)
            {
                Console.WriteLine($"[TraderGen.LocaleLoader] file {filePath} is empty or null");
                return;
            }

            Console.WriteLine($"[TraderGen.LocaleLoader] applying {entries.Count} entries for {localeKey} (pack={traderId})");
            localeLazy.AddTransformer(localeData =>
            {
                if (localeData == null) return localeData;
                foreach (var (key, value) in entries)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    localeData[key] = value;
                }
                return localeData;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TraderGen.LocaleLoader] ERROR loading {filePath}: {ex.Message}");
        }
    }

    // Resolves the locale key to use. TraderGen config overrides SPT's locale.json.
    private string ResolveLocaleKey()
    {
        var traderGenConfig = LoadTraderGenConfig();
        var configured = traderGenConfig.Language;

        if (string.IsNullOrWhiteSpace(configured) ||
            string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase))
        {
            configured = localeConfig.GameLocale;
        }

        if (string.Equals(configured, "system", StringComparison.OrdinalIgnoreCase))
        {
            configured = CultureInfo.InstalledUICulture.Name.ToLowerInvariant();
        }

        if (LocaleAliases.TryGetValue(configured, out var mapped))
        {
            return mapped;
        }

        return configured.ToLowerInvariant();
    }

    private TraderGenLocaleConfig LoadTraderGenConfig()
    {
        try
        {
            var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            if (string.IsNullOrEmpty(modPath)) return new TraderGenLocaleConfig();

            var configPath = Path.Combine(modPath, "config", "locale.json");
            if (!File.Exists(configPath)) return new TraderGenLocaleConfig();

            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<TraderGenLocaleConfig>(json) ?? new TraderGenLocaleConfig();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TraderGen.LocaleLoader] ERROR reading config/locale.json: {ex.Message}");
            return new TraderGenLocaleConfig();
        }
    }
}
