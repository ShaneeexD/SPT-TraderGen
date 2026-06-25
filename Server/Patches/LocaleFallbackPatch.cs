using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Services;

namespace TraderGen.Patches;

public class LocaleFallbackPatch : AbstractPatch
{
    private static readonly Dictionary<string, string> LocaleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["es-es"] = "es",
        ["es-mx"] = "es",
        ["es-ar"] = "es",
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
        ["cs"] = "cz",
        ["ja"] = "jp",
        ["ko"] = "kr",
        ["tr"] = "tu",
    };

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocaleService), nameof(LocaleService.GetLocaleDb));
    }

    [HarmonyPrefix]
    public static bool Prefix(string? language, LocaleService __instance, ref Dictionary<string, string> __result)
    {
        if (string.IsNullOrWhiteSpace(language)) return true;

        var normalized = language.ToLowerInvariant();
        if (!LocaleAliases.TryGetValue(normalized, out var actualKey)) return true;

        // Recursively call GetLocaleDb with the mapped key. The Prefix will see the actual key,
        // not match any alias, and return true so the original SPT method runs.
        __result = __instance.GetLocaleDb(actualKey);
        return false;
    }
}
