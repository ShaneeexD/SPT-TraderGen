import type { TraderDefinition, ValidationError } from './types'

const HEX_24 = /^[0-9a-fA-F]{24}$/
const VALID_CURRENCIES = ['RUB', 'USD', 'EUR']

export function validateTrader(trader: TraderDefinition): ValidationError[] {
  const errors: ValidationError[] = []

  if (!trader.id || !HEX_24.test(trader.id)) {
    errors.push({ field: 'id', message: 'ID must be a 24-character hex string.' })
  }

  if (!trader.nickname.trim()) {
    errors.push({ field: 'nickname', message: 'Nickname is required.' })
  }

  if (!trader.firstName.trim()) {
    errors.push({ field: 'firstName', message: 'First name is required.' })
  }

  if (!trader.avatar.trim()) {
    errors.push({ field: 'avatar', message: 'Avatar path is required (e.g. assets/avatar.jpg).' })
  }

  if (!VALID_CURRENCIES.includes(trader.currency)) {
    errors.push({ field: 'currency', message: 'Currency must be RUB, USD, or EUR.' })
  }

  if (trader.loyaltyLevels.length === 0) {
    errors.push({ field: 'loyaltyLevels', message: 'At least one loyalty level is required.' })
  }

  const seenLevels = new Set<number>()
  for (const ll of trader.loyaltyLevels) {
    if (ll.level < 1 || ll.level > 10) {
      errors.push({ field: 'loyaltyLevels', message: `Level ${ll.level} is out of range (1-10).` })
    }
    if (seenLevels.has(ll.level)) {
      errors.push({ field: 'loyaltyLevels', message: `Duplicate level ${ll.level}.` })
    }
    seenLevels.add(ll.level)
    if (ll.minLevel < 1) {
      errors.push({ field: 'loyaltyLevels', message: `Level ${ll.level}: minLevel must be >= 1.` })
    }
  }

  for (let i = 0; i < trader.assort.length; i++) {
    const item = trader.assort[i]
    const prefix = `Assort[${i}]`

    if (!item.itemTpl || !HEX_24.test(item.itemTpl)) {
      errors.push({ field: `assort.${i}.itemTpl`, message: `${prefix}: itemTpl must be a 24-char hex string.` })
    }

    if (item.loyaltyLevel < 1) {
      errors.push({ field: `assort.${i}.loyaltyLevel`, message: `${prefix}: loyaltyLevel must be >= 1.` })
    } else if (!trader.loyaltyLevels.some(ll => ll.level === item.loyaltyLevel)) {
      errors.push({ field: `assort.${i}.loyaltyLevel`, message: `${prefix}: loyaltyLevel ${item.loyaltyLevel} not defined.` })
    }

    const hasBarter = item.barter && item.barter.length > 0
    const hasPrice = item.price > 0

    if (!hasBarter && !hasPrice) {
      errors.push({ field: `assort.${i}.price`, message: `${prefix}: Must have price > 0 or barter items.` })
    }

    if (hasBarter) {
      for (let j = 0; j < item.barter!.length; j++) {
        const b = item.barter![j]
        if (!b.itemTpl || !HEX_24.test(b.itemTpl)) {
          errors.push({ field: `assort.${i}.barter.${j}`, message: `${prefix}.barter[${j}]: itemTpl must be 24-char hex.` })
        }
        if (b.count < 1) {
          errors.push({ field: `assort.${i}.barter.${j}`, message: `${prefix}.barter[${j}]: count must be >= 1.` })
        }
      }
    }
  }

  return errors
}

export function buildExportJson(trader: TraderDefinition): object {
  const output: Record<string, unknown> = {
    enabled: trader.enabled,
    id: trader.id,
    nickname: trader.nickname,
    firstName: trader.firstName,
    lastName: trader.lastName,
    location: trader.location,
    description: trader.description,
    avatar: trader.avatar,
    currency: trader.currency,
    unlockedByDefault: trader.unlockedByDefault,
    buyerEnabled: trader.buyerEnabled,
    ragfairEnabled: trader.ragfairEnabled,
    balanceRub: trader.balanceRub,
    balanceDol: trader.balanceDol,
    balanceEur: trader.balanceEur,
    refreshTimeMin: trader.refreshTimeMin,
    refreshTimeMax: trader.refreshTimeMax,
    insuranceEnabled: trader.insuranceEnabled,
    repairEnabled: trader.repairEnabled,
    loyaltyLevels: trader.loyaltyLevels,
    assort: trader.assort.map(item => {
      const out: Record<string, unknown> = {
        itemTpl: item.itemTpl,
        loyaltyLevel: item.loyaltyLevel,
        stock: item.stock,
        unlimitedStock: item.unlimitedStock,
      }
      if (item.barter && item.barter.length > 0) {
        out.barter = item.barter
      } else {
        out.price = item.price
        if (item.currency) out.currency = item.currency
      }
      if (item.buyLimit > 0) out.buyLimit = item.buyLimit
      return out
    }),
  }

  if (trader.fullName) output.fullName = trader.fullName

  return output
}
