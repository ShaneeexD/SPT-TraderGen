import type { TraderDefinition, QuestPackDefinition, ValidationError } from './types'

const HEX_24 = /^[0-9a-fA-F]{24}$/
const VALID_CURRENCIES = ['RUB', 'USD', 'EUR']
const VALID_OBJECTIVE_TYPES = ['kill_enemy', 'handover_item', 'handover_fir_item', 'survive_location', 'extract_location']
const VALID_ROTATION_TYPES = ['daily', 'weekly']

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
        unlimitedStock: item.unlimitedStock,
      }
      if (!item.unlimitedStock) out.stock = item.stock
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

export function validateQuestPack(pack: QuestPackDefinition, traderId: string): ValidationError[] {
  const errors: ValidationError[] = []
  const seenIds = new Set<string>()

  for (let i = 0; i < pack.storyQuests.length; i++) {
    const q = pack.storyQuests[i]
    const prefix = `Quest[${i}]`

    if (!q.id || !HEX_24.test(q.id)) {
      errors.push({ field: `quest.${i}.id`, message: `${prefix}: ID must be a 24-char hex string.` })
    } else if (seenIds.has(q.id)) {
      errors.push({ field: `quest.${i}.id`, message: `${prefix}: Duplicate quest ID "${q.id}".` })
    }
    seenIds.add(q.id)

    if (!q.name.trim()) {
      errors.push({ field: `quest.${i}.name`, message: `${prefix}: Name is required.` })
    }
    if (!q.description.trim()) {
      errors.push({ field: `quest.${i}.description`, message: `${prefix}: Description is required.` })
    }
    if (q.objectives.length === 0) {
      errors.push({ field: `quest.${i}.objectives`, message: `${prefix}: At least one objective is required.` })
    }

    if (q.requirements.previousQuest && !HEX_24.test(q.requirements.previousQuest)) {
      errors.push({ field: `quest.${i}.previousQuest`, message: `${prefix}: Previous quest ID must be a 24-char hex string.` })
    }

    for (let j = 0; j < q.objectives.length; j++) {
      const obj = q.objectives[j]
      const objPrefix = `${prefix}.Objective[${j}]`

      if (!VALID_OBJECTIVE_TYPES.includes(obj.type)) {
        errors.push({ field: `quest.${i}.obj.${j}.type`, message: `${objPrefix}: Invalid objective type "${obj.type}".` })
      }
      if (obj.count < 1) {
        errors.push({ field: `quest.${i}.obj.${j}.count`, message: `${objPrefix}: Count must be >= 1.` })
      }
      if ((obj.type === 'handover_item' || obj.type === 'handover_fir_item') && (!obj.itemTpl || !HEX_24.test(obj.itemTpl))) {
        errors.push({ field: `quest.${i}.obj.${j}.itemTpl`, message: `${objPrefix}: Item template ID required (24-char hex).` })
      }
      if ((obj.type === 'survive_location' || obj.type === 'extract_location') && !obj.location) {
        errors.push({ field: `quest.${i}.obj.${j}.location`, message: `${objPrefix}: Location is required.` })
      }
    }

    if (q.rewards.xp < 0) {
      errors.push({ field: `quest.${i}.rewards.xp`, message: `${prefix}: XP reward cannot be negative.` })
    }
  }

  for (let i = 0; i < pack.rotatingQuests.length; i++) {
    const t = pack.rotatingQuests[i]
    const prefix = `Rotating[${i}]`

    if (!t.id || !HEX_24.test(t.id)) {
      errors.push({ field: `rotating.${i}.id`, message: `${prefix}: ID must be a 24-char hex string.` })
    }
    if (!VALID_ROTATION_TYPES.includes(t.rotation)) {
      errors.push({ field: `rotating.${i}.rotation`, message: `${prefix}: Rotation must be "daily" or "weekly".` })
    }
    if (t.namePool.length === 0) {
      errors.push({ field: `rotating.${i}.namePool`, message: `${prefix}: At least one name is required in namePool.` })
    }
    if (t.objectives.length === 0) {
      errors.push({ field: `rotating.${i}.objectives`, message: `${prefix}: At least one objective is required.` })
    }
    for (let j = 0; j < t.objectives.length; j++) {
      const obj = t.objectives[j]
      const objPrefix = `${prefix}.Objective[${j}]`
      if (!VALID_OBJECTIVE_TYPES.includes(obj.type)) {
        errors.push({ field: `rotating.${i}.obj.${j}.type`, message: `${objPrefix}: Invalid type "${obj.type}".` })
      }
      if (obj.countRange.min < 1) {
        errors.push({ field: `rotating.${i}.obj.${j}.countMin`, message: `${objPrefix}: countRange.min must be >= 1.` })
      }
      if (obj.countRange.max < obj.countRange.min) {
        errors.push({ field: `rotating.${i}.obj.${j}.countMax`, message: `${objPrefix}: countRange.max must be >= min.` })
      }
      if (obj.type === 'kill_enemy' && obj.targetPool.length === 0) {
        errors.push({ field: `rotating.${i}.obj.${j}.targetPool`, message: `${objPrefix}: targetPool is required for kill_enemy.` })
      }
      if ((obj.type === 'handover_item' || obj.type === 'handover_fir_item') && obj.itemPool.length === 0) {
        errors.push({ field: `rotating.${i}.obj.${j}.itemPool`, message: `${objPrefix}: itemPool is required for handover objectives.` })
      }
    }
    if (t.questCount < 1) {
      errors.push({ field: `rotating.${i}.questCount`, message: `${prefix}: questCount must be >= 1.` })
    }
  }

  return errors
}

export function buildQuestExportJson(pack: QuestPackDefinition): object | null {
  const hasQuests = pack.storyQuests.length > 0 || pack.rotatingQuests.length > 0
  if (!hasQuests) return null

  const output: Record<string, unknown> = {}

  if (pack.defaultQuestIcon) {
    output.defaultQuestIcon = pack.defaultQuestIcon
  }

  if (pack.storyQuests.length > 0) {
    output.storyQuests = pack.storyQuests.map(q => {
      const quest: Record<string, unknown> = {
        id: q.id,
        traderId: q.traderId,
        name: q.name,
        description: q.description,
        successMessage: q.successMessage,
        startedMessage: q.startedMessage,
        location: q.location,
        requirements: {
          playerLevel: q.requirements.playerLevel,
          ...(q.requirements.previousQuest ? { previousQuest: q.requirements.previousQuest } : {}),
        },
        objectives: q.objectives.map(obj => {
          const o: Record<string, unknown> = { type: obj.type, count: obj.count }
          if (obj.target) o.target = obj.target
          if (obj.location) o.location = obj.location
          if (obj.itemTpl) o.itemTpl = obj.itemTpl
          if (obj.description) o.description = obj.description
          if (obj.useAutoCounter !== undefined) o.useAutoCounter = obj.useAutoCounter
          return o
        }),
        rewards: buildRewardsJson(q.rewards),
      }
      if (q.image) quest.image = q.image
      return quest
    })
  } else {
    output.storyQuests = []
  }

  if (pack.rotatingQuests.length > 0) {
    output.rotatingQuests = pack.rotatingQuests.map(t => {
      const tpl: Record<string, unknown> = {
        id: t.id,
        rotation: t.rotation,
        namePool: t.namePool,
        descriptionPool: t.descriptionPool,
        objectives: t.objectives.map(obj => {
          const o: Record<string, unknown> = {
            type: obj.type,
            countRange: obj.countRange,
          }
          if (obj.targetPool?.length > 0) o.targetPool = obj.targetPool
          if (obj.locationPool?.length > 0) o.locationPool = obj.locationPool
          if (obj.itemPool?.length > 0) o.itemPool = obj.itemPool
          if (obj.foundInRaid) o.foundInRaid = true
          return o
        }),
        rewardScaling: t.rewardScaling,
        questCount: t.questCount,
      }
      if (t.image) tpl.image = t.image
      return tpl
    })
  } else {
    output.rotatingQuests = []
  }

  return output
}

function buildRewardsJson(rewards: QuestPackDefinition['storyQuests'][0]['rewards']) {
  const r: Record<string, unknown> = { xp: rewards.xp }
  if (rewards.money && rewards.money.amount > 0) {
    r.money = { currency: rewards.money.currency, amount: rewards.money.amount }
  }
  if (rewards.traderStanding !== 0) r.traderStanding = rewards.traderStanding
  if (rewards.items && rewards.items.length > 0) r.items = rewards.items
  if (rewards.unlockAssortItems && rewards.unlockAssortItems.length > 0) {
    r.unlockAssortItems = rewards.unlockAssortItems
  }
  return r
}
