using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SpectreColor = Spectre.Console.Color;

namespace TraderGen.Services;

// Registers a custom "Random Item" placeholder item that is used as the display
// template for random item pool quest rewards. The actual item is rolled from
// the pool at quest completion time by RandomItemPoolPatch.
//
// The placeholder is never granted to the player — it only exists so the client
// can display an icon and name for the reward while the quest is in progress.
// The client plugin patches the icon to show randomitem.png and the name to
// show "Random Item" for this template ID.
//
// We clone a vanilla item (AI-2 medkit) so the client always has a valid template
// to render, but use our own unique ID so vanilla items used as normal rewards
// are never accidentally overridden by the client icon/name patch.
public static class RandomItemPlaceholder
{
    // Fixed template ID for the "Random Item" placeholder.
    // The client plugin also references this constant to patch the icon and name.
    public const string PlaceholderTplId = "6988f0a1c0ffee1234567890";

    // Clone the AI-2 medkit as the base — it's a simple 1x1 item that always exists.
    private const string CloneFromTpl = "5755356824597772cb798962";

    // Barter item parent — the AI-2 is a Meds item, but we want it to behave as a
    // simple barter-style item with no special behaviour. Using the barter parent
    // keeps it out of medicine slots and flea.
    private const string BarterParentId = "5448eb774bdc2d0a728b4567";
    private const string BarterHandbookParentId = "5b47574386f77428ca22b33e";

    private static bool _registered = false;

    public static bool TryRegister(
        CustomItemService customItemService,
        TemplateTable templateTable,
        ISptLogger<TraderGenPlugin> logger)
    {
        if (_registered)
            return true;

        // Check if already registered (e.g. by a previous load or another mod)
        if (templateTable.Items.ContainsKey(new MongoId(PlaceholderTplId)))
        {
            _registered = true;
            return true;
        }

        try
        {
            var overrides = new TemplateItemProperties
            {
                Name = "Random Item",
                ShortName = "Random Item",
                Description = "A random reward item. The actual item will be determined when the quest is completed.",
                BackgroundColor = "blue",
                Width = 1,
                Height = 1,
                StackMaxSize = 1,
                CanSellOnRagfair = false,
                RarityPvE = "Rare",
            };

            var details = new NewItemFromCloneDetails
            {
                NewId = new MongoId(PlaceholderTplId),
                NewItemName = "tradergen_random_item",
                ItemTplToClone = new MongoId(CloneFromTpl),
                ParentId = new MongoId(BarterParentId),
                HandbookParentId = BarterHandbookParentId,
                HandbookPriceRoubles = 1000,
                FleaPriceRoubles = 0,
                OverrideProperties = overrides,
                AddToHandbook = false,
                AddToFleaPriceDb = false,
                AddToWeaponShelf = false,
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new LocaleDetails
                    {
                        Name = "Random Item",
                        ShortName = "Random Item",
                        Description = "A random reward item. The actual item will be determined when the quest is completed.",
                    },
                },
            };

            var result = customItemService.CreateItemFromClone(details);

            if (result.Success == true)
            {
                _registered = true;
                logger.LogWithColor(
                    "[TraderGen] Registered 'Random Item' placeholder template for random item pool rewards.",
                    SpectreColor.Green);
                return true;
            }

            logger.LogWithColor(
                $"[TraderGen] Failed to register 'Random Item' placeholder: {string.Join(", ", result.Errors ?? [])}",
                SpectreColor.Red);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWithColor(
                $"[TraderGen] Exception registering 'Random Item' placeholder: {ex.Message}",
                SpectreColor.Red);
            return false;
        }
    }
}
