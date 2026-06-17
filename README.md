# TraderGen — Custom Trader Framework for SPTarkov 4.0.13

A dependency/framework mod that lets anyone, including **non-programmers**, create custom traders for SPTarkov using simple JSON files. Includes a web-based TraderGen Tool for generating trader packs visually.

## Overview

TraderGen has two parts:

1. **TraderGen Mod** (SPT server mod) — Reads trader pack JSON files at startup and registers them into the SPT database
2. **TraderGen Tool** (web app) — A visual editor for creating trader packs without writing any code

**Tool URL**: https://tradergen-tool.netlify.app

---

## Quick Start

### For Players (Installing a Trader Pack)

1. Install the TraderGen mod by dragging the `SPT` folder into your SPT install directory
2. If the trader pack is pre-packaged as an `SPT/` folder structure, drag it in the same way — it drops into the right place automatically
3. Otherwise, place the trader pack folder into `SPT/user/mods/TraderGen/traders/`
4. Start the SPT server — the trader appears automatically

### For Creators (Making a Trader Pack)

1. Open the **TraderGen Tool**: https://tradergen-tool.netlify.app
2. Fill in trader details, loyalty levels, assort items, and optionally add quests
3. Click **Export** — the tool packages everything into a ready-to-use zip
4. Extract the zip and drag the `SPT` folder into your SPT install directory to test
5. Publish your pack — users only need TraderGen installed as a dependency

---

## Export Format

The TraderGen Tool export zip is pre-packaged and ready to drag straight into any SPT install:

```
SPT/
└── user/
    └── mods/
        └── TraderGen/
            └── traders/
                └── YourTraderName/
                    ├── trader.json      # Trader identity, loyalty levels, assort
                    ├── quests.json      # Optional: story and rotating quests
                    └── assets/
                        ├── avatar.jpg   # Trader portrait
                        └── tpl_*.jpg    # Optional: custom quest icons
```

If the pack has no quests defined, `quests.json` is omitted. All paths are pre-configured — no manual editing needed.

---

## Trader Pack JSON (`trader.json`)

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

  // Optional: Location text shown on trader screen
  "location": "Customs Warehouse",

  // Optional: Description text
  "description": "A reliable trader specialising in military surplus.",

  // Required: Relative path to avatar image
  "avatar": "assets/avatar.jpg",

  // Optional: Default currency — "RUB", "USD", or "EUR" (default: "RUB")
  "currency": "RUB",

  // Optional: Unlocked from game start (default: true)
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

Find item template IDs at: https://db.sp-tarkov.com/search

---

## Quest Pack JSON (`quests.json`)

Quests are defined in a separate `quests.json` alongside `trader.json`. Two quest types are supported.

```jsonc
{
  "storyQuests": [ /* see below */ ],
  "rotatingQuests": [ /* see below */ ],

  // Optional: Default quest icon for all quests in this pack (relative to pack folder)
  "defaultQuestIcon": "assets/my_icon.jpg"
}
```

### Story Quests

Fixed, hand-authored quests that appear permanently in the trader's quest list.

```jsonc
{
  "id": "aabbccdd11223344eeff0001",       // Unique 24-char hex ID
  "traderId": "aabbccdd11223344eeff5566",
  "name": "Supply Run",
  "description": "We need supplies. Go get them.",
  "successMessage": "Good work. Here's your cut.",
  "startedMessage": "Get it done.",
  "location": "bigmap",                   // Map ID, or "any" for no map restriction
  "requirements": {
    "playerLevel": 5,
    "previousQuest": "aabbccdd11223344eeff0000"  // Optional: must complete this quest first
  },
  "objectives": [
    // Kill objective
    {
      "type": "kill_enemy",
      "count": 10,
      "target": "Savage",                 // "Savage", "pmcBot", "exUsec", "Bear", "Usec"
      "location": "bigmap"                // Optional: restrict kills to a specific map
    },
    // Handover objective
    {
      "type": "handover_item",
      "count": 3,
      "itemTpl": "590c678286f77426c9660122",
      "description": "Hand over 3 of the item"  // Optional: override auto-generated text
    },
    // Found-in-raid handover
    {
      "type": "handover_fir_item",
      "count": 1,
      "itemTpl": "590c678286f77426c9660122"
    },
    // Survive and extract
    {
      "type": "survive_location",
      "count": 2,
      "location": "Woods"
    }
  ],
  "rewards": {
    "xp": 5000,
    "money": { "currency": "RUB", "amount": 50000 },
    "traderStanding": 0.02,
    "items": [
      { "itemTpl": "590c678286f77426c9660122", "count": 1 }
    ],
    "unlockAssortItems": [
      "590c5d4b86f774784e1b9c45"      // Unlocks an assort item on quest completion
    ]
  },

  // Optional: Custom quest icon (relative to pack folder)
  "image": "assets/tpl_aabbccdd11223344eeff0001.jpg"
}
```

**Supported map IDs**: `any`, `bigmap` (Customs), `factory4_day`, `factory4_night`, `Woods`, `Shoreline`, `Interchange`, `Lighthouse`, `Reserve`, `RezervBase`, `laboratory`, `TarkovStreets`, `Sandbox`, `sandbox_high`

### Rotating Quests

Template-based quests that are procedurally generated fresh each server start and appear in the trader's daily/weekly repeatable quest pool.

Objectives are generated randomly from the pools you define. Rewards (XP, money, standing) scale with the randomly generated objective counts according to the `rewardScaling` values you set.

```jsonc
{
  "id": "aabbccdd11223344eeff0010",    // Unique 24-char hex ID for the template
  "rotation": "daily",                 // "daily" or "weekly"
  "questCount": 1,                     // How many quests to generate from this template per cycle

  // Name and description pools — one entry is picked randomly per generation.
  // Use {location} as a placeholder — it is replaced with the chosen map name.
  "namePool": [
    "Cleanup {location}",
    "Eliminate Threats at {location}"
  ],
  "descriptionPool": [
    "Head to {location} and deal with the threat.",
    "Eliminate targets at {location} for a reward."
  ],

  "objectives": [
    // Kill objective template
    {
      "type": "kill_enemy",
      "countRange": { "min": 5, "max": 15 },
      "targetPool": ["Savage", "pmcBot"],          // One target picked randomly
      "locationPool": ["bigmap", "Woods", "Shoreline"]  // One map picked randomly
    },
    // Handover objective template
    {
      "type": "handover_item",
      "countRange": { "min": 1, "max": 5 },
      "itemPool": [
        "590c678286f77426c9660122",
        "5449016a4bdc2d6f028b456f"
      ],
      "foundInRaid": false
    },
    // Survive and extract objective template
    {
      "type": "survive_location",
      "countRange": { "min": 1, "max": 3 },
      "locationPool": ["bigmap", "Woods"]
    }
  ],

  // Reward scaling — controls XP, money, and standing given on completion.
  // Final values scale with the randomly generated objective counts.
  "rewardScaling": {
    "xpPerObjectiveCount": 500,         // XP per unit of objective count (e.g. per kill)
    "baseMoney": 20000,                 // Base money reward before scaling
    "moneyPerObjectiveCount": 5000,     // Additional money per objective count
    "currency": "RUB",                  // "RUB", "USD", or "EUR"
    "standing": 0.01                    // Trader standing gain per completion
  },

  // Optional: Custom quest icon (relative to pack folder)
  "image": "assets/tpl_aabbccdd11223344eeff0010.jpg"
}
```

**Supported objective types**: `kill_enemy`, `handover_item`, `handover_fir_item`, `survive_location`, `extract_location`

---

## Publishing a Trader Pack

The TraderGen Tool export zip is already structured for distribution — users just drag the `SPT` folder into their install directory.

When publishing:

1. **State the dependencies**: Your mod requires `com.serenity.tradergen` v1.5.0+ and `com.wtt.commonlib` (WTT CommonLib)
2. **Do not include** the TraderGen DLL or other authors' packs in your zip
3. **Include your assets**: Ensure `assets/avatar.jpg` and any quest icons are present
4. **Test** by extracting and running the server before publishing

### Distribution layout (pre-packaged):

```
YourTraderPack.zip
└── SPT/
    └── user/
        └── mods/
            └── TraderGen/
                └── traders/
                    └── YourTraderName/
                        ├── trader.json
                        ├── quests.json         (if quests are included)
                        └── assets/
                            ├── avatar.jpg
                            └── tpl_*.jpg       (custom quest icons, if any)
```

Users extract and drag the `SPT/` folder into their SPT install directory.

---

## Validation & Error Handling

TraderGen validates all JSON files on load and logs clear errors to the server console:

- Missing required fields (`id`, `nickname`, `firstName`, `avatar`)
- Invalid ID format (must be 24-char hex)
- Invalid currency values
- Missing or duplicate loyalty levels
- Assort items referencing undefined loyalty levels
- Missing prices or barter requirements
- Invalid quest objective types or map IDs
- Invalid reward scaling values

If a pack has errors it is **skipped** — other packs still load normally.

---

## Technical Details

- **SPT Version**: 4.0.13
- **Framework**: .NET 9.0, C#
- **DI Pattern**: `[Injectable]` + `IOnLoad` (runs at `PostDBModLoader + 1`)
- **NuGet Packages**: `SPTarkov.Common`, `SPTarkov.DI`, `SPTarkov.Server.Core` (4.0.13)
- **Quest integration**: Story quests via WTT CustomQuests library; rotating quests injected directly into the SPT repeatable quest pool
- **Runtime dependency**: `com.wtt.commonlib` (WTT CommonLib) — required for quest registration

## License

MIT — Use freely for your SPT mods.
