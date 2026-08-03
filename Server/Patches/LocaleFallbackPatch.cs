using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Locales;
using TraderGen.Models;

namespace TraderGen.Patches;

// Maps ISO/launcher locale codes to SPT's internal locale keys. TraderGen config overrides SPT's locale.json.
public class LocaleFallbackPatch : AbstractPatch
{
    private static string? _configPath;

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

    public static void SetDependencies(string modPath)
    {
        _configPath = Path.Combine(modPath, "config", "locale.json");
    }

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocaleService), nameof(LocaleService.GetDesiredGameLocale));
    }

    [PatchPrefix]
    public static bool Prefix(LocaleConfig ___LocaleConfig, ref string __result)
    {
        var configured = LoadTraderGenConfig().Language;

        if (string.IsNullOrWhiteSpace(configured) ||
            string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase))
        {
            configured = ___LocaleConfig.GameLocale;
        }

        if (string.Equals(configured, "system", StringComparison.OrdinalIgnoreCase))
        {
            configured = CultureInfo.InstalledUICulture.Name.ToLowerInvariant();
        }

        if (LocaleAliases.TryGetValue(configured, out var actualKey))
        {
            __result = actualKey;
        }
        else
        {
            __result = configured.ToLowerInvariant();
        }

        return false;
    }

    private static TraderGenLocaleConfig LoadTraderGenConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(_configPath) || !File.Exists(_configPath)) return new TraderGenLocaleConfig();

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<TraderGenLocaleConfig>(json) ?? new TraderGenLocaleConfig();
        }
        catch
        {
            return new TraderGenLocaleConfig();
        }
    }
}
