using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using TraderGen.Models;
using Path = System.IO.Path;

namespace TraderGen.Services;

// Registers trader definitions into the SPT database.
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TraderRegistrar(
    ISptLogger<TraderRegistrar> logger,
    ICloner cloner,
    DatabaseService databaseService,
    ImageRouter imageRouter,
    ConfigServer configServer,
    ModHelper modHelper
)
{
    private readonly TraderConfig _traderConfig = configServer.GetConfig<TraderConfig>();
    private readonly RagfairConfig _ragfairConfig = configServer.GetConfig<RagfairConfig>();

    // Default buy categories (all known vanilla item parent IDs).
    private static readonly List<string> DefaultBuyCategories =
    [
        "5422acb9af1c889c16000029", // Weapon
        "5448fe124bdc2da5018b4567", // Mod
        "5485a8684bdc2da71d8b4567", // Ammo
        "57864c8c245977548867e7f1", // MedicalSupplies
        "543be6674bdc2df1348b4569", // FoodDrink
        "543be5664bdc2dd4348b4569", // Meds
        "57864ee62459775490116fc1", // Battery
        "5447e1d04bdc2dff2f8b4567", // Knife
        "5795f317245977243854e041", // SimpleContainer
        "5671435f4bdc2d96058b4569", // LockableContainer
        "5448e53e4bdc2d60728b4567", // Backpack
        "5448e5284bdc2dcb718b4567", // Vest
        "57864e4c24597754843f8723", // Lubricant
        "57864ada245977548638de91", // BuildingMaterial
        "5447b6194bdc2d67278b4567", // MarksmanRifle
        "5447b6094bdc2dc3278b4567", // Shotgun
        "5447b6254bdc2dc3278b4568", // SniperRifle
        "55818ae44bdc2dde698b456c", // OpticScope
        "55818aeb4bdc2ddc698b456a", // SpecialScope
        "55818add4bdc2d5b648b456f", // AssaultScope
        "555ef6e44bdc2de9068b457e", // Barrel
        "55818b224bdc2dde698b456f", // Mount
        "55818a594bdc2db9688b456a", // Stock
        "543be5f84bdc2dd4348b456a", // Equipment
        "6759673c76e93d8eb20b2080", // Flyer
        "5661632d4bdc2d903d8b456b", // StackableItem
        "5447e0e74bdc2d3c308b4567", // SpecItem
        "567849dd4bdc2d150f8b456e", // Map
        "543be6564bdc2df4348b4568", // ThrowWeap
        "5448eb774bdc2d0a728b4567", // BarterItem
        "5448ecbe4bdc2d60728b4568", // Info
        "616eb7aea207f41933308f46", // RepairKits
        "543be5e94bdc2df1348b4568", // Key
        "543be5cb4bdc2deb348b4568", // AmmoBox
        "57864a66245977548f04a81f", // Electronics
        "57864bb7245977548b3b66c2", // Tool
        "5c164d2286f774194c5e69fa", // Keycard
        "57864a3d24597754843f8721", // Jewelry
        "590c745b86f7743cc433c5f2", // Other
        "5448f3a64bdc2d60728b456a", // Stimulator
        "5d650c3e815116009f6201d2", // Fuel
        "5448e54d4bdc2dcc718b4568", // Armor
        "5c99f98d86f7745c314214b3", // KeyMechanical
        "57bef4c42459772e8d35a53b", // ArmoredEquipment
        "5448f39d4bdc2d0a728b4568", // MedKit
        "5448f3ac4bdc2dce718b4569", // Medical
        "5448e8d04bdc2ddf718b4569", // Food
        "5a341c4086f77401f2541505", // Headwear
        "543be5dd4bdc2deb348b4569", // Money
    ];

    // Register a single trader.
    public bool RegisterTrader(TraderLoader.LoadedTrader loaded)
    {
        var trader = loaded.Definition;
        var packFolder = loaded.PackFolder;

        try
        {
            // Build TraderBase
            var traderBase = BuildTraderBase(trader, packFolder);

            // Register avatar
            RegisterAvatar(trader, traderBase, packFolder);

            // Set refresh time
            SetTraderUpdateTime(traderBase, trader.RefreshTimeMin, trader.RefreshTimeMax);

            // Enable ragfair
            if (trader.RagfairEnabled)
            {
                _ragfairConfig.Traders.TryAdd(traderBase.Id, true);
            }

            // Add trader to database
            AddTraderToDatabase(traderBase);

            // Add locale entries
            AddTraderLocales(traderBase, trader);

            // Build and assign assort
            BuildAndAssignAssort(trader);

            logger.LogWithColor(
                $"[TraderGen] Successfully registered trader '{trader.Nickname}' (ID: {trader.Id})",
                LogTextColor.Green
            );
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWithColor(
                $"[TraderGen] Failed to register trader '{trader.Nickname}': {ex.Message}",
                LogTextColor.Red
            );
            return false;
        }
    }

    // Build TraderBase from trader definition.
    private TraderBase BuildTraderBase(TraderDefinition trader, string packFolder)
    {
        var rawBuyCategories = new List<string>(trader.BuyCategories ?? DefaultBuyCategories);
        var buyProhibited = trader.BuyProhibitedItems ?? new List<string>();

        // Validate buy categories against actual item parent IDs in the DB
        var allItems = databaseService.GetItems();
        var validParentIds = new HashSet<string>(allItems.Values.Select(i => i.Parent.ToString()).Where(p => !string.IsNullOrEmpty(p)));
        var buyCategories = new List<string>();
        var skippedCategories = new List<string>();
        foreach (var cat in rawBuyCategories.Distinct())
        {
            if (validParentIds.Contains(cat))
            {
                buyCategories.Add(cat);
            }
            else
            {
                skippedCategories.Add(cat);
            }
        }
        if (skippedCategories.Count > 0)
        {
            logger.LogWithColor(
                $"[TraderGen] Skipped invalid buy categories for '{trader.Nickname}': {string.Join(", ", skippedCategories)}",
                LogTextColor.Red
            );
        }
        if (buyCategories.Count == 0)
        {
            logger.LogWithColor(
                $"[TraderGen] WARNING: No valid buy categories for '{trader.Nickname}', using all valid parents as fallback.",
                LogTextColor.Red
            );
            buyCategories = validParentIds.Take(20).ToList();
        }

        // Auto-prohibit items with zero/missing handbook prices to prevent client crashes
        var handbook = databaseService.GetHandbook();
        var pricedItems = new HashSet<string>(handbook.Items
            .Where(h => h.Price > 0)
            .Select(h => h.Id.ToString()));
        var zeroPriceItems = allItems
            .Where(kvp => buyCategories.Contains(kvp.Value.Parent.ToString()))
            .Where(kvp => !pricedItems.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var itemId in zeroPriceItems)
        {
            if (!buyProhibited.Contains(itemId))
            {
                buyProhibited.Add(itemId);
            }
        }
        if (zeroPriceItems.Count > 0)
        {
            logger.LogWithColor(
                $"[TraderGen] Auto-prohibited {zeroPriceItems.Count} zero-price item(s) for '{trader.Nickname}' to prevent client crashes.",
                LogTextColor.Yellow
            );
        }

        // Auto-prohibit currency items to prevent client overflow on large stacks
        var currencyIds = new[]
        {
            "5449016a4bdc2d6f028b456f", // Roubles
            "5696686a4bdc2da3298b456a", // Dollars
            "569668774bdc2da2298b4568", // Euros
        };
        foreach (var currencyId in currencyIds)
        {
            if (!buyProhibited.Contains(currencyId))
            {
                buyProhibited.Add(currencyId);
            }
        }
        logger.LogWithColor(
            $"[TraderGen] Auto-prohibited currency items for '{trader.Nickname}'.",
            LogTextColor.Yellow
        );

        var currency = trader.Currency.ToUpperInvariant() switch
        {
            "USD" or "DOLLARS" or "DOLLAR" => "USD",
            "EUR" or "EUROS" or "EURO" => "EUR",
            _ => "RUB"
        };

        // Build base.json content
        var baseJson = new
        {
            _id = trader.Id,
            availableInRaid = false,
            avatar = $"/files/trader/avatar/{trader.Id}.jpg",
            balance_rub = trader.BalanceRub,
            balance_dol = trader.BalanceDol,
            balance_eur = trader.BalanceEur,
            buyer_up = trader.BuyerEnabled,
            currency = currency,
            customization_seller = false,
            discount = 0,
            discount_end = 0,
            gridHeight = 500,
            insurance = new
            {
                availability = trader.InsuranceEnabled,
                min_payment = 0,
                min_return_hour = 0,
                max_return_hour = 1,
                max_storage_time = 144,
                excluded_category = Array.Empty<string>(),
            },
            isCanTransferItems = false,
            items_buy = new
            {
                category = buyCategories,
                id_list = Array.Empty<string>(),
            },
            items_buy_prohibited = new
            {
                category = Array.Empty<string>(),
                id_list = buyProhibited,
            },
            location = trader.Location,
            medic = false,
            name = trader.FullName ?? $"{trader.Nickname} {trader.LastName}",
            nextResupply = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            nickname = trader.Nickname,
            prohibitedTransferableItems = new
            {
                category = Array.Empty<string>(),
                id_list = Array.Empty<string>(),
            },
            repair = new
            {
                availability = trader.RepairEnabled,
                currency = CurrencyHelper.Roubles,
                currency_coefficient = 1,
                excluded_category = Array.Empty<string>(),
                excluded_id_list = Array.Empty<string>(),
                quality = "2",
            },
            sell_category = new List<string>(), // Vanilla: always empty
            sell_modifier_for_prohibited_items = 0,
            surname = trader.LastName,
            transferableItems = new
            {
                category = Array.Empty<string>(),
                id_list = Array.Empty<string>(),
            },
            unlockedByDefault = trader.UnlockedByDefault,
            loyaltyLevels = trader.LoyaltyLevels
                .OrderBy(ll => ll.Level)
                .Select(ll => new
                {
                    minLevel = ll.MinLevel,
                    minSalesSum = ll.MinSalesSum,
                    minStanding = ll.MinStanding,
                    buy_price_coef = ll.BuyPriceCoef,
                    repair_price_coef = 150,
                    insurance_price_coef = 10,
                    exchange_price_coef = 0,
                    heal_price_coef = 0,
                })
                .ToArray(),
        };

        // Serialize to temp file
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        var jsonContent = JsonSerializer.Serialize(baseJson, jsonOptions);
        var baseFilePath = Path.Combine(packFolder, $".tradergen_base_{trader.Id}.json");
        File.WriteAllText(baseFilePath, jsonContent);

        // Load via SPT ModHelper
        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(packFolder, $".tradergen_base_{trader.Id}.json");

        // Clean up temp file
        try { File.Delete(baseFilePath); } catch { /* ignore */ }

        return traderBase;
    }

    // Register avatar image.
    private void RegisterAvatar(TraderDefinition trader, TraderBase traderBase, string packFolder)
    {
        var avatarRelPath = trader.Avatar.Replace('/', Path.DirectorySeparatorChar);
        var avatarAbsPath = Path.Combine(packFolder, avatarRelPath);

        if (!File.Exists(avatarAbsPath))
        {
            logger.LogWithColor(
                $"[TraderGen] Warning: Avatar file not found at '{avatarAbsPath}'. Trader will have no image.",
                LogTextColor.Yellow
            );
            return;
        }

        // Route must match avatar path
        var routePath = traderBase.Avatar.Replace(".jpg", "");
        imageRouter.AddRoute(routePath, avatarAbsPath);
    }

    // Configure inventory refresh time.
    private void SetTraderUpdateTime(TraderBase traderBase, int minSeconds, int maxSeconds)
    {
        var updateTime = new UpdateTime
        {
            TraderId = traderBase.Id,
            Seconds = new MinMax<int>(minSeconds, maxSeconds),
        };
        _traderConfig.UpdateTime.Add(updateTime);
    }

    // Add trader to database with empty assort.
    private void AddTraderToDatabase(TraderBase traderBase)
    {
        var emptyAssort = new TraderAssort
        {
            Items = [],
            BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
            LoyalLevelItems = new Dictionary<MongoId, int>(),
        };

        var traderData = new Trader
        {
            Assort = emptyAssort,
            Base = cloner.Clone(traderBase),
            QuestAssort = new()
            {
                { "started", new() },
                { "success", new() },
                { "fail", new() },
            },
            Dialogue = [],
        };

        if (!databaseService.GetTables().Traders.TryAdd(traderBase.Id, traderData))
        {
            logger.LogWithColor(
                $"[TraderGen] Warning: Trader ID '{traderBase.Id}' already exists in database. Skipping.",
                LogTextColor.Yellow
            );
        }
    }

    // Add locale entries for all languages.
    private void AddTraderLocales(TraderBase traderBase, TraderDefinition trader)
    {
        var locales = databaseService.GetTables().Locales.Global;
        var traderId = traderBase.Id;

        foreach (var (localeKey, localeKvP) in locales)
        {
            localeKvP.AddTransformer(localeData =>
            {
                localeData.TryAdd($"{traderId} FullName", trader.FullName ?? $"{trader.Nickname} {trader.LastName}");
                localeData.TryAdd($"{traderId} FirstName", trader.FirstName);
                localeData.TryAdd($"{traderId} Nickname", trader.Nickname);
                localeData.TryAdd($"{traderId} Location", trader.Location);
                localeData.TryAdd($"{traderId} Description", trader.Description);
                return localeData;
            });
        }
    }

    // Build trader's assort and assign to database.
    private void BuildAndAssignAssort(TraderDefinition trader)
    {
        var traderData = databaseService.GetTables().Traders.GetValueOrDefault(trader.Id);
        if (traderData == null)
        {
            logger.LogWithColor($"[TraderGen] Cannot build assort: trader '{trader.Id}' not found in database.", LogTextColor.Red);
            return;
        }

        foreach (var assortItem in trader.Assort)
        {
            try
            {
                // Generate or use specified item ID
                var itemId = assortItem.ItemId ?? new MongoId().ToString();

                // Create root item
                var upd = new Upd
                {
                    UnlimitedCount = assortItem.UnlimitedStock,
                    StackObjectsCount = assortItem.Stock,
                };
                if (assortItem.StackSize is > 1)
                    upd.StackObjectsCount = assortItem.StackSize.Value;
                var item = new Item
                {
                    Id = itemId,
                    Template = assortItem.ItemTpl,
                    ParentId = "hideout",
                    SlotId = "hideout",
                    Upd = upd,
                };

                // Apply buy restriction
                if (assortItem.BuyLimit > 0)
                {
                    item.Upd.BuyRestrictionMax = assortItem.BuyLimit;
                    item.Upd.BuyRestrictionCurrent = 0;
                }

                traderData.Assort.Items.Add(item);

                // Recursively flatten child tree into flat item list
                if (assortItem.Children is { Count: > 0 })
                {
                    FlattenChildren(assortItem.Children, itemId, traderData.Assort.Items);
                }

                // Build barter scheme
                var barterSchemeList = BuildBarterScheme(assortItem, trader.Currency);
                traderData.Assort.BarterScheme[itemId] = barterSchemeList;

                // Set loyalty level
                traderData.Assort.LoyalLevelItems[itemId] = assortItem.LoyaltyLevel;

                // Quest-lock this item if specified
                if (!string.IsNullOrWhiteSpace(assortItem.LockedByQuest))
                {
                    traderData.QuestAssort["success"][itemId] = assortItem.LockedByQuest;
                }
            }
            catch (Exception ex)
            {
                logger.LogWithColor(
                    $"[TraderGen] Error adding assort item '{assortItem.ItemTpl}' to trader '{trader.Nickname}': {ex.Message}",
                    LogTextColor.Red
                );
            }
        }
    }

    // Build barter scheme for an assort item.
    private List<List<BarterScheme>> BuildBarterScheme(AssortItemDefinition assortItem, string defaultCurrency)
    {
        var schemeItems = new List<BarterScheme>();

        if (assortItem.Barter is { Count: > 0 })
        {
            // Barter trade
            foreach (var barter in assortItem.Barter)
            {
                var isDogtag = DogtagIds.Contains(barter.ItemTpl);
                var scheme = new BarterScheme
                {
                    Template = NormalizeDogtagId(barter.ItemTpl, barter.Side),
                    Count = barter.Count,
                };
                if (barter.Level.HasValue)
                {
                    scheme.Level = barter.Level.Value;
                }
                else if (isDogtag)
                {
                    scheme.Level = 1;
                }
                if (!string.IsNullOrEmpty(barter.Side))
                {
                    scheme.Side = Enum.Parse<DogtagExchangeSide>(barter.Side, true);
                }
                else if (isDogtag)
                {
                    scheme.Side = DogtagExchangeSide.Any;
                }
                schemeItems.Add(scheme);
            }
        }
        else
        {
            // Money purchase
            var currencyTpl = CurrencyHelper.ToTemplateId(assortItem.Currency ?? defaultCurrency);
            schemeItems.Add(new BarterScheme
            {
                Template = currencyTpl,
                Count = assortItem.Price,
            });
        }

        // SPT expects List<List<BarterScheme>>
        return [schemeItems];
    }

    // Base vanilla dogtag IDs used in barter schemes.
    private static readonly string[] DogtagIds =
    [
        "59f32bb586f774757e1e8442", // BEAR
        "59f32c3b86f77472a31742f0", // USEC
        "6662e9aca7e0b43baa3d5f74", // BEAR
        "6662e9cda7e0b43baa3d5f76", // BEAR
        "6662e9f37fa79a6d83730fa0", // USEC
        "6662ea05f6259762c56f3189", // USEC
        "675dc9d37ae1a8792107ca96", // BEAR
        "675dcb0545b1a2d108011b2b", // BEAR
    ];

    private static readonly string DogtagBaseBear = "59f32bb586f774757e1e8442";
    private static readonly string DogtagBaseUsec = "59f32c3b86f77472a31742f0";

    // Normalizes any dogtag ID to the two base vanilla IDs used in assort.json.
    private static string NormalizeDogtagId(string id, string? side)
    {
        if (!DogtagIds.Contains(id)) return id;
        return side == "Bear" ? DogtagBaseBear : DogtagBaseUsec;
    }

    // Recursively walk a tree of AssortChildItem and flatten into a flat SPT Item list.
    // Each child's parentId is set to the ID of the item it attaches to.
    private void FlattenChildren(List<AssortChildItem> children, string parentId, List<Item> items)
    {
        foreach (var child in children)
        {
            var childId = child.ItemId ?? new MongoId().ToString();
            var upd = new Upd();
            if (child.Amount is > 1)
                upd.StackObjectsCount = child.Amount.Value;
            items.Add(new Item
            {
                Id = childId,
                Template = child.ItemTpl,
                ParentId = parentId,
                SlotId = child.SlotId,
                Upd = upd,
            });

            if (child.Children is { Count: > 0 })
            {
                FlattenChildren(child.Children, childId, items);
            }
        }
    }

}
