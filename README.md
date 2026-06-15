# TraderGen — Custom Trader Framework for SPTarkov 4.0.13

A dependency/framework mod that lets anyone, including **non-programmers** create custom traders for SPTarkov using simple JSON files. Includes a web-based TraderGen Tool for generating trader packs visually.

## Overview

TraderGen has two parts:

1. **TraderGen Mod** (SPT server mod) — Reads trader pack JSON files at startup and registers them into the SPT database
2. **TraderGen Tool** (web app) — A visual editor for creating trader packs without writing code

## Quick Start

### For Players (Installing a Trader Pack)

1. Install the TraderGen mod (Drag the `SPT` folder into your SPT main directory)
2. Place the trader pack folder into `user/mods/TraderGen/traders/`, it may also be pre-packaged so you can simply drag the `SPT` folder into the main directory, as done with the TraderGen mod
3. Start the SPT server — the trader appears automatically

### For Creators (Making a Trader Pack)

1. Open the **TraderGen Tool** (see `Tool/` folder)
2. Fill in trader details, add items, configure loyalty levels
3. Click **Export** to download your trader pack
4. Test by placing the exported folder in `user/mods/TraderGen/traders/`
5. Publish your pack — users just need TraderGen installed as a dependency

## Installation (Server Mod)

```
SPT/
└── user/
    └── mods/
        └── TraderGen/
            ├── TraderGen.dll          # The compiled mod
            └── traders/               # Trader packs go here
                └── MyTraderPack/
                    ├── trader.json    # Trader definition
                    └── assets/
                        └── avatar.jpg # Trader image
```

## Trader Pack Structure

A trader pack is a folder containing:

```
MyTraderPack/
├── trader.json        # Required: trader definition
└── assets/
    └── avatar.jpg     # Optional: square trader portrait
```

## JSON Schema

```jsonc
{
  // Required: Unique 24-char hex ID (MongoDB ObjectId format)
  "id": "aabbccdd11223344eeff5566",

  // Required: Display name in trader list
  "nickname": "Viktor",

  // Required: First name for locale
  "firstName": "Viktor",

  // Optional: Last name (default: "Unknown")
  "lastName": "Kozlov",

  // Optional: Full display name (default: "nickname lastName")
  "fullName": "Viktor Kozlov",

  // Optional: Location text (default: "Unknown")
  "location": "Customs Warehouse",

  // Optional: Description text
  "description": "A reliable trader specializing in military surplus.",

  // Required: Relative path to avatar image
  "avatar": "assets/avatar.jpg",

  // Optional: Default currency — "RUB", "USD", or "EUR" (default: "RUB")
  "currency": "RUB",

  // Optional: Unlocked from start (default: true)
  "unlockedByDefault": true,

  // Optional: Can buy items from player (default: true)
  "buyerEnabled": true,

  // Optional: Appears on flea market (default: true)
  "ragfairEnabled": true,

  // Optional: Cash balances
  "balanceRub": 3000000,
  "balanceDol": 0,
  "balanceEur": 0,

  // Optional: Restock timing in seconds
  "refreshTimeMin": 1800,
  "refreshTimeMax": 7200,

  // Optional: Services
  "insuranceEnabled": false,
  "repairEnabled": false,

  // Required: At least one loyalty level
  "loyaltyLevels": [
    {
      "level": 1,
      "minLevel": 1,
      "minSalesSum": 0,
      "minStanding": 0,
      "buyPriceCoef": 40
    },
    {
      "level": 2,
      "minLevel": 15,
      "minSalesSum": 500000,
      "minStanding": 0,
      "buyPriceCoef": 45
    }
  ],

  // Items the trader sells
  "assort": [
    // Money purchase
    {
      "itemTpl": "590c678286f77426c9660122",
      "loyaltyLevel": 1,
      "stock": 50,
      "unlimitedStock": false,
      "price": 15000,
      "currency": "RUB"
    },
    // Barter trade
    {
      "itemTpl": "590c5d4b86f774784e1b9c45",
      "loyaltyLevel": 1,
      "stock": 10,
      "unlimitedStock": false,
      "barter": [
        { "itemTpl": "544fb6cc4bdc2d34748b456e", "count": 2 }
      ]
    },
    // Item with buy limit
    {
      "itemTpl": "5b432d215acfc4771e1c6624",
      "loyaltyLevel": 2,
      "stock": 5,
      "unlimitedStock": false,
      "buyLimit": 2,
      "price": 85000,
      "currency": "RUB"
    }
  ]
}
```

## Publishing a Trader Pack

When publishing your trader pack for others to use:

1. **State the dependency**: Your mod requires `com.serenity.tradergen` v1.0.0+
2. **Include clear instructions**: Tell users to place the pack folder in `user/mods/TraderGen/traders/` or pre-package it as `SPT\user\mods\TraderGen\traders\YourTraderPack` - if you do the second option, make sure not to include the TraderGen DLL or any other mod authors packs.
3. **Include the avatar image**: Make sure `assets/avatar.jpg` is in the pack
4. **Test thoroughly**: Start SPT and verify the trader appears with correct items and prices

### Example folder layout for distribution:

```
YourTraderPack.zip
└── YourTraderName/
    ├── trader.json
    └── assets/
        └── avatar.jpg
```

Users extract `YourTraderName/` into `user/mods/TraderGen/traders/`.

OR

```
SPT/
└── user/
    └── mods/
        └── TraderGen/
            └── traders/               
                └── MyTraderPack/      # Your Trader Pack
                    ├── trader.json    # Trader definition
                    └── assets/
                        └── avatar.jpg # Trader image
```
Users extract `SPT/` into the main SPT install directory.

## Validation & Error Handling

TraderGen validates all JSON files on load and provides clear error messages:

- Missing required fields (id, nickname, firstName, avatar)
- Invalid ID format (must be 24-char hex)
- Invalid currency values
- Missing or duplicate loyalty levels
- Items referencing undefined loyalty levels
- Missing prices or barter requirements

If a trader pack has errors, it is **skipped** — other trader packs still load normally. Check the server console for error details.

## Technical Details

- **SPT Version**: 4.0.13
- **Framework**: .NET 9.0, C#
- **DI Pattern**: `[Injectable]` + `IOnLoad` (runs at `PostDBModLoader + 1`)
- **NuGet Packages**: `SPTarkov.Common`, `SPTarkov.DI`, `SPTarkov.Server.Core` (4.0.13)

## License

MIT — Use freely for your SPT mods.
