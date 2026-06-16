namespace TraderGen.Services;

// Stores locale text for generated repeatable quests.
// These are injected into the locale database so the client can display quest names/descriptions.
public static class RepeatableQuestLocaleStore
{
    // QuestId -> (name, description)
    private static readonly Dictionary<string, (string Name, string Description)> _locales = new();

    // ConditionId -> description text (for objective display)
    private static readonly Dictionary<string, string> _conditionLocales = new();

    public static void Add(string questId, string name, string description)
    {
        _locales[questId] = (name, description);
    }

    public static void AddCondition(string conditionId, string text)
    {
        _conditionLocales[conditionId] = text;
    }

    public static Dictionary<string, (string Name, string Description)> GetAll() => _locales;

    public static Dictionary<string, string> GetAllConditions() => _conditionLocales;

    public static void Clear()
    {
        _locales.Clear();
        _conditionLocales.Clear();
    }
}
