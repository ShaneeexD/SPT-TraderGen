using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Services;

namespace TraderGen.Services;

// Loads locale files from a pack's locales/ folder into SPT's global locale tables.
[Injectable(InjectionType.Singleton)]
public class LocaleLoader(DatabaseService databaseService)
{
    public void LoadPackLocales(string packFolder, string traderId)
    {
        var localesDir = Path.Combine(packFolder, "locales");
        if (!Directory.Exists(localesDir)) return;

        var globalLocales = databaseService.GetLocales().Global;
        Console.WriteLine($"[TraderGen.LocaleLoader] pack={traderId}, dir={localesDir}, globalKeys={string.Join(",", globalLocales.Keys)}");

        foreach (var file in Directory.EnumerateFiles(localesDir, "*.json"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            Console.WriteLine($"[TraderGen.LocaleLoader] found file={file}, lang={lang}");
            if (!globalLocales.TryGetValue(lang, out var localeLazy))
            {
                Console.WriteLine($"[TraderGen.LocaleLoader] WARN: language key '{lang}' not found in Global locales");
                continue;
            }

            try
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (entries == null || entries.Count == 0)
                {
                    Console.WriteLine($"[TraderGen.LocaleLoader] file {file} is empty or null");
                    continue;
                }

                Console.WriteLine($"[TraderGen.LocaleLoader] applying {entries.Count} entries for {lang}");
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
                Console.WriteLine($"[TraderGen.LocaleLoader] ERROR loading {file}: {ex.Message}");
            }
        }
    }
}
