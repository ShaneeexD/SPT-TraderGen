/**
 * Build script that bundles SPT vanilla trader data into a single JSON
 * file for the TraderGen web tool to consume at runtime.
 */
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const VANILLA_DIR = path.resolve(__dirname, '..', 'database', 'traders')
const OUT_PATH = path.resolve(__dirname, '..', 'public', 'vanilla-traders.json')

// Locale and config data live inside the workspace database/configs folder
const LOCALE_PATH = path.resolve(__dirname, '..', 'database', 'locales', 'global', 'en.json')
const TRADER_CONFIG_PATH = path.resolve(__dirname, '..', 'configs', 'trader.json')

const CURRENCY_TPLS = {
  '5449016a4bdc2d6f028b456f': 'RUB',
  '5696686a4bdc2da2298b4568': 'USD',
  '569668774bdc2da2298b4568': 'EUR',
}

function loadJson(path) {
  try {
    return JSON.parse(fs.readFileSync(path, 'utf-8'))
  } catch {
    return {}
  }
}

function getTraderLocale(locales, traderId) {
  return {
    nickname: locales[`${traderId} Nickname`] || '',
    firstName: locales[`${traderId} FirstName`] || '',
    location: locales[`${traderId} Location`] || '',
    description: locales[`${traderId} Description`] || '',
    fullName: locales[`${traderId} FullName`] || '',
  }
}

function isCurrencyTpl(tpl) {
  return Object.prototype.hasOwnProperty.call(CURRENCY_TPLS, tpl)
}

function convertBase(base, locales, refreshTimes) {
  const id = base._id || ''
  const locale = getTraderLocale(locales, id)

  const ll = (base.loyaltyLevels || []).map((l, idx) => ({
    level: idx + 1,
    minLevel: typeof l.minLevel === 'number' ? l.minLevel : 1,
    minSalesSum: typeof l.minSalesSum === 'number' ? l.minSalesSum : 0,
    minStanding: typeof l.minStanding === 'number' ? l.minStanding : 0,
    buyPriceCoef: typeof l.buy_price_coef === 'number' ? l.buy_price_coef : 40,
  }))

  const refresh = refreshTimes[id] || { min: 1800, max: 7200 }

  // Vanilla base.json doesn't store description/location/firstName reliably;
  // they live in locale files.
  const nickname = locale.nickname || base.nickname || ''
  const firstName = locale.firstName || base.name || nickname || ''
  const lastName = base.surname || 'Unknown'
  const fullName = locale.fullName || (base.name && base.surname ? `${base.name} ${base.surname}` : undefined)

  return {
    id,
    nickname,
    firstName,
    lastName,
    fullName,
    location: locale.location || '',
    description: locale.description || '',
    avatar: base.avatar || 'assets/avatar.jpg',
    currency: base.currency || 'RUB',
    unlockedByDefault: base.unlockedByDefault !== false,
    buyerEnabled: true, // SPT enables buying programmatically; vanilla buyer_up is always false in DB
    ragfairEnabled: true,
    balanceRub: typeof base.balance_rub === 'number' ? base.balance_rub : 0,
    balanceDol: typeof base.balance_dol === 'number' ? base.balance_dol : 0,
    balanceEur: typeof base.balance_eur === 'number' ? base.balance_eur : 0,
    refreshTimeMin: refresh.min,
    refreshTimeMax: refresh.max,
    insuranceEnabled: base.insurance?.availability === true,
    repairEnabled: base.repair?.availability === true,
    loyaltyLevels: ll.length > 0 ? ll : [{ level: 1, minLevel: 1, minSalesSum: 0, minStanding: 0, buyPriceCoef: 40 }],
    buyCategories: base.items_buy?.category || [],
  }
}

function convertAssort(assort) {
  const items = assort?.items || []
  const barterScheme = assort?.barter_scheme || {}
  const loyalLevelItems = assort?.loyal_level_items || {}

  const result = []

  for (const item of items) {
    // Only top-level assort items (parentId === 'hideout' or slotId === 'hideout')
    if (item.parentId !== 'hideout' && item.slotId !== 'hideout') continue

    const itemId = item._id
    const tpl = item._tpl
    if (!tpl) continue

    const upd = item.upd || {}
    const unlimited = upd.UnlimitedCount === true
    const stock = typeof upd.StackObjectsCount === 'number' ? upd.StackObjectsCount : 999999
    const buyLimit = typeof upd.BuyRestrictionMax === 'number' ? upd.BuyRestrictionMax : 0
    const loyaltyLevel = typeof loyalLevelItems[itemId] === 'number' ? loyalLevelItems[itemId] : 1

    const scheme = barterScheme[itemId]
    let price = 0
    let currency = undefined
    let barter = undefined

    if (Array.isArray(scheme) && scheme.length > 0 && Array.isArray(scheme[0])) {
      const reqs = scheme[0]
      // If every entry is a currency, treat as money purchase (use first currency)
      const allCurrency = reqs.every(r => isCurrencyTpl(r._tpl))

      if (allCurrency && reqs.length > 0) {
        price = Math.round(reqs[0].count)
        currency = CURRENCY_TPLS[reqs[0]._tpl] || 'RUB'
      } else {
        barter = reqs.map(r => ({
          itemTpl: r._tpl,
          count: typeof r.count === 'number' ? Math.round(r.count) : 1,
          level: r.level,
          side: r.side,
        })).filter(b => b.itemTpl)
      }
    }

    result.push({
      itemTpl: tpl,
      loyaltyLevel,
      stock,
      unlimitedStock: unlimited,
      price,
      currency,
      barter: barter && barter.length > 0 ? barter : undefined,
      buyLimit: buyLimit > 0 ? buyLimit : 0,
      children: undefined,
    })
  }

  return result
}

function main() {
  const traderDirs = fs
    .readdirSync(VANILLA_DIR, { withFileTypes: true })
    .filter(d => d.isDirectory())
    .map(d => d.name)

  const locales = loadJson(LOCALE_PATH)
  const traderConfig = loadJson(TRADER_CONFIG_PATH)

  // Build a map of traderId -> { min, max } from the SPT trader config
  const refreshTimes = {}
  for (const entry of traderConfig.updateTime || []) {
    if (entry.traderId && entry.seconds) {
      refreshTimes[entry.traderId] = {
        min: entry.seconds.min,
        max: entry.seconds.max,
      }
    }
  }

  const traders = []

  for (const dir of traderDirs) {
    const basePath = path.join(VANILLA_DIR, dir, 'base.json')
    const assortPath = path.join(VANILLA_DIR, dir, 'assort.json')

    if (!fs.existsSync(basePath)) continue

    const base = JSON.parse(fs.readFileSync(basePath, 'utf-8'))
    const traderDef = convertBase(base, locales, refreshTimes)

    let assort = []
    if (fs.existsSync(assortPath)) {
      const assortJson = JSON.parse(fs.readFileSync(assortPath, 'utf-8'))
      assort = convertAssort(assortJson)
    }

    traders.push({
      id: traderDef.id,
      nickname: traderDef.nickname,
      firstName: traderDef.firstName,
      lastName: traderDef.lastName,
      fullName: traderDef.fullName,
      location: traderDef.location,
      description: traderDef.description,
      avatar: traderDef.avatar,
      currency: traderDef.currency,
      loyaltyLevels: traderDef.loyaltyLevels,
      insuranceEnabled: traderDef.insuranceEnabled,
      repairEnabled: traderDef.repairEnabled,
      balanceRub: traderDef.balanceRub,
      balanceDol: traderDef.balanceDol,
      balanceEur: traderDef.balanceEur,
      buyerEnabled: traderDef.buyerEnabled,
      unlockedByDefault: traderDef.unlockedByDefault,
      refreshTimeMin: traderDef.refreshTimeMin,
      refreshTimeMax: traderDef.refreshTimeMax,
      buyCategories: traderDef.buyCategories,
      assort,
    })
  }

  // Sort by nickname for stable ordering
  traders.sort((a, b) => a.nickname.localeCompare(b.nickname))

  fs.writeFileSync(OUT_PATH, JSON.stringify(traders, null, 2))
  console.log(`[build-vanilla] Bundled ${traders.length} vanilla traders → public/vanilla-traders.json`)
}

main()
