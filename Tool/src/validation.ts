import type { TraderDefinition, QuestPackDefinition, ValidationError } from './types'
import { isDogtagId, normalizeDogtagId } from './types'

const HEX_24 = /^[0-9a-fA-F]{24}$/
const VALID_CURRENCIES = ['RUB', 'USD', 'EUR']
const VALID_OBJECTIVE_TYPES = ['kill_enemy', 'handover_item', 'handover_fir_item', 'find_item', 'leave_item_at_location', 'survive_location', 'extract_location', 'zone_visit', 'zone_kill', 'zone_place_item']
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

  if (!trader.unlockedByDefault && trader.unlockQuestId?.trim() && !HEX_24.test(trader.unlockQuestId.trim())) {
    errors.push({ field: 'unlockQuestId', message: 'Unlock Quest ID must be a 24-character hex string.' })
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
        if (isDogtagId(b.itemTpl)) {
          if (b.level === undefined || b.level === null || b.level < 1) {
            errors.push({ field: `assort.${i}.barter.${j}.level`, message: `${prefix}.barter[${j}]: Dogtag barter requires a level >= 1.` })
          }
          const validSides = ['Bear', 'Usec', 'Any']
          if (!b.side || !validSides.includes(b.side)) {
            errors.push({ field: `assort.${i}.barter.${j}.side`, message: `${prefix}.barter[${j}]: Dogtag barter requires side (Bear / Usec / Any).` })
          }
        }
      }
    }

    if (item.lockedByQuest && !HEX_24.test(item.lockedByQuest)) {
      errors.push({ field: `assort.${i}.lockedByQuest`, message: `${prefix}: lockedByQuest must be a 24-char hex quest ID.` })
    }

    validateChildren(item.children, errors, prefix, 'children')
  }

  return errors
}

function validateChildren(
  children: import('./types').AssortChildItem[] | undefined,
  errors: ValidationError[],
  prefix: string,
  path: string
) {
  if (!children || children.length === 0) return
  for (let j = 0; j < children.length; j++) {
    const c = children[j]
    const childPrefix = `${prefix}.${path}.${j}`
    if (!c.itemTpl || !HEX_24.test(c.itemTpl)) {
      errors.push({ field: `${childPrefix}.itemTpl`, message: `${childPrefix}: itemTpl must be 24-char hex.` })
    }
    if (!c.slotId || c.slotId.trim().length === 0) {
      errors.push({ field: `${childPrefix}.slotId`, message: `${childPrefix}: slotId is required.` })
    }
    if (c.children && c.children.length > 0) {
      validateChildren(c.children, errors, childPrefix, 'children')
    }
  }
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
    ...(trader.unlockedByDefault ? {} : { unlockQuestId: trader.unlockQuestId?.trim() || '' }),
    buyerEnabled: trader.buyerEnabled,
    ragfairEnabled: trader.ragfairEnabled,
    balanceRub: trader.balanceRub,
    balanceDol: trader.balanceDol,
    balanceEur: trader.balanceEur,
    refreshTimeMin: trader.refreshTimeMin,
    refreshTimeMax: trader.refreshTimeMax,
    insuranceEnabled: trader.insuranceEnabled,
    insuranceMinReturnHour: trader.insuranceMinReturnHour,
    insuranceMaxReturnHour: trader.insuranceMaxReturnHour,
    insuranceMaxStorageTime: trader.insuranceMaxStorageTime,
    repairEnabled: trader.repairEnabled,
    loyaltyLevels: trader.loyaltyLevels.map(ll => ({
      level: ll.level,
      minLevel: ll.minLevel,
      minSalesSum: ll.minSalesSum,
      minStanding: ll.minStanding,
      buyPriceCoef: ll.buyPriceCoef,
      insurancePriceCoef: ll.insurancePriceCoef ?? 10,
    })),
    assort: trader.assort.map(item => {
      const out: Record<string, unknown> = {
        itemTpl: item.itemTpl,
        loyaltyLevel: item.loyaltyLevel,
        unlimitedStock: item.unlimitedStock,
      }
      if (!item.unlimitedStock) out.stock = item.stock
      if (item.stackSize && item.stackSize > 1) out.stackSize = item.stackSize
      if (item.barter && item.barter.length > 0) {
        out.barter = item.barter.map(b => {
          const isDogtag = isDogtagId(b.itemTpl)
          return {
            ...b,
            itemTpl: normalizeDogtagId(b.itemTpl, b.side),
            level: isDogtag ? (b.level ?? 1) : b.level,
            side: isDogtag ? (b.side || 'Any') : b.side,
          }
        })
      } else {
        out.price = item.price
        if (item.currency) out.currency = item.currency
      }
      if (item.buyLimit > 0) out.buyLimit = item.buyLimit
      if (item.children && item.children.length > 0) out.children = item.children
      if (item.lockedByQuest) out.lockedByQuest = item.lockedByQuest
      return out
    }),
  }

  if (trader.fullName) output.fullName = trader.fullName
  if (trader.buyCategories && trader.buyCategories.length > 0) output.buyCategories = trader.buyCategories

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
      if ((obj.type === 'handover_item' || obj.type === 'handover_fir_item' || obj.type === 'find_item' || obj.type === 'leave_item_at_location') && (!obj.itemTpl || !HEX_24.test(obj.itemTpl))) {
        errors.push({ field: `quest.${i}.obj.${j}.itemTpl`, message: `${objPrefix}: Item template ID required (24-char hex).` })
      }
      if ((obj.type === 'survive_location' || obj.type === 'extract_location') && !obj.location) {
        errors.push({ field: `quest.${i}.obj.${j}.location`, message: `${objPrefix}: Location is required.` })
      }
      if ((obj.type === 'zone_visit' || obj.type === 'zone_kill' || obj.type === 'zone_place_item' || obj.type === 'leave_item_at_location') && !obj.zoneId?.trim()) {
        errors.push({ field: `quest.${i}.obj.${j}.zoneId`, message: `${objPrefix}: Zone ID is required for ${obj.type}.` })
      }
      if (obj.type === 'zone_place_item' && !obj.plantItemTpl?.trim()) {
        errors.push({ field: `quest.${i}.obj.${j}.plantItemTpl`, message: `${objPrefix}: Item Template ID is required for zone_place_item.` })
      }

      // Advanced condition validation
      if (obj.minDistance !== undefined && obj.minDistance !== null && (obj.minDistance < 0 || !Number.isFinite(obj.minDistance))) {
        errors.push({ field: `quest.${i}.obj.${j}.minDistance`, message: `${objPrefix}: minDistance must be >= 0.` })
      }
      if (obj.maxDistance !== undefined && obj.maxDistance !== null && (obj.maxDistance < 0 || !Number.isFinite(obj.maxDistance))) {
        errors.push({ field: `quest.${i}.obj.${j}.maxDistance`, message: `${objPrefix}: maxDistance must be >= 0.` })
      }
      if (obj.minDistance != null && obj.maxDistance != null) {
        errors.push({ field: `quest.${i}.obj.${j}.minDistance`, message: `${objPrefix}: Cannot set both minDistance and maxDistance at the same time. Use one or the other.` })
      }
      if (obj.timeFrom !== undefined && obj.timeFrom !== null && (obj.timeFrom < 0 || obj.timeFrom > 23)) {
        errors.push({ field: `quest.${i}.obj.${j}.timeFrom`, message: `${objPrefix}: timeFrom must be 0-23.` })
      }
      if (obj.timeTo !== undefined && obj.timeTo !== null && (obj.timeTo < 0 || obj.timeTo > 23)) {
        errors.push({ field: `quest.${i}.obj.${j}.timeTo`, message: `${objPrefix}: timeTo must be 0-23.` })
      }
      if (obj.weaponTpls) {
        for (let wi = 0; wi < obj.weaponTpls.length; wi++) {
          if (!HEX_24.test(obj.weaponTpls[wi])) {
            errors.push({ field: `quest.${i}.obj.${j}.weaponTpls[${wi}]`, message: `${objPrefix}: weaponTpls[${wi}] must be a 24-char hex string.` })
          }
        }
      }
      if (obj.wearing) {
        for (let wi = 0; wi < obj.wearing.length; wi++) {
          if (!HEX_24.test(obj.wearing[wi])) {
            errors.push({ field: `quest.${i}.obj.${j}.wearing[${wi}]`, message: `${objPrefix}: wearing[${wi}] must be a 24-char hex string.` })
          }
        }
      }
      if (obj.notWearing) {
        for (let wi = 0; wi < obj.notWearing.length; wi++) {
          if (!HEX_24.test(obj.notWearing[wi])) {
            errors.push({ field: `quest.${i}.obj.${j}.notWearing[${wi}]`, message: `${objPrefix}: notWearing[${wi}] must be a 24-char hex string.` })
          }
        }
      }
      if (obj.bodyPart) {
        for (let bi = 0; bi < obj.bodyPart.length; bi++) {
          if (!obj.bodyPart[bi]?.trim()) {
            errors.push({ field: `quest.${i}.obj.${j}.bodyPart[${bi}]`, message: `${objPrefix}: bodyPart[${bi}] cannot be empty.` })
          }
        }
      }
    }

    if (q.rewards.xp < 0) {
      errors.push({ field: `quest.${i}.rewards.xp`, message: `${prefix}: XP reward cannot be negative.` })
    }

    if (q.rewards.stashRows !== undefined && q.rewards.stashRows < 0) {
      errors.push({ field: `quest.${i}.rewards.stashRows`, message: `${prefix}: stashRows cannot be negative.` })
    }

    if (q.rewards.skills) {
      for (let si = 0; si < q.rewards.skills.length; si++) {
        const skill = q.rewards.skills[si]
        if (!skill.name.trim()) {
          errors.push({ field: `quest.${i}.rewards.skills[${si}].name`, message: `${prefix}: skill[${si}] name is required.` })
        }
        if (skill.points < 1) {
          errors.push({ field: `quest.${i}.rewards.skills[${si}].points`, message: `${prefix}: skill[${si}] points must be >= 1.` })
        }
      }
    }

    if (q.rewards.pockets && !/^[0-9a-fA-F]{24}$/.test(q.rewards.pockets)) {
      errors.push({ field: `quest.${i}.rewards.pockets`, message: `${prefix}: pockets must be a 24-character hex string.` })
    }

    if (q.rewards.customPocket) {
      if (q.rewards.customPocket.slots.length === 0) {
        errors.push({ field: `quest.${i}.rewards.customPocket`, message: `${prefix}: custom pocket must have at least one slot.` })
      }
      for (let pi = 0; pi < q.rewards.customPocket.slots.length; pi++) {
        const slot = q.rewards.customPocket.slots[pi]
        if (slot.width < 1 || slot.width > 4) {
          errors.push({ field: `quest.${i}.rewards.customPocket.slots[${pi}].width`, message: `${prefix}: custom pocket slot[${pi}] width must be 1-4.` })
        }
        if (slot.height < 1 || slot.height > 4) {
          errors.push({ field: `quest.${i}.rewards.customPocket.slots[${pi}].height`, message: `${prefix}: custom pocket slot[${pi}] height must be 1-4.` })
        }
      }
    }

    if (q.rewards.items) {
      for (let ri = 0; ri < q.rewards.items.length; ri++) {
        const item = q.rewards.items[ri]
        if (!item.itemTpl || !HEX_24.test(item.itemTpl)) {
          errors.push({ field: `quest.${i}.rewards.items[${ri}].itemTpl`, message: `${prefix}: reward item[${ri}] itemTpl must be 24-char hex.` })
        }
        if (item.count < 1) {
          errors.push({ field: `quest.${i}.rewards.items[${ri}].count`, message: `${prefix}: reward item[${ri}] count must be >= 1.` })
        }
        if (item.children) {
          validateChildren(item.children, errors, `${prefix}.rewards.items[${ri}]`, 'children')
        }
      }
    }

    if (q.rewards.unlockAssortItems) {
      for (let ui = 0; ui < q.rewards.unlockAssortItems.length; ui++) {
        if (!HEX_24.test(q.rewards.unlockAssortItems[ui])) {
          errors.push({ field: `quest.${i}.rewards.unlockAssortItems[${ui}]`, message: `${prefix}: unlockAssortItems[${ui}] must be a 24-char hex item ID.` })
        }
      }
    }

    if (q.rewards.recipes) {
      for (let ri = 0; ri < q.rewards.recipes.length; ri++) {
        if (!HEX_24.test(q.rewards.recipes[ri])) {
          errors.push({ field: `quest.${i}.rewards.recipes[${ri}]`, message: `${prefix}: recipes[${ri}] must be a 24-char hex production scheme ID.` })
        }
      }
    }
  }

  for (let i = 0; i < (pack.productionSchemes || []).length; i++) {
    const s = pack.productionSchemes![i]
    const prefix = `Scheme[${i}]`
    if (!s._id || !HEX_24.test(s._id)) {
      errors.push({ field: `scheme.${i}._id`, message: `${prefix}: _id must be a 24-char hex string.` })
    }
    if (s.areaType < 0 || !Number.isInteger(s.areaType)) {
      errors.push({ field: `scheme.${i}.areaType`, message: `${prefix}: areaType must be a non-negative integer.` })
    }
    if (!s.endProduct || !HEX_24.test(s.endProduct)) {
      errors.push({ field: `scheme.${i}.endProduct`, message: `${prefix}: endProduct must be a 24-char hex item TPL.` })
    }
    if (s.count < 1) {
      errors.push({ field: `scheme.${i}.count`, message: `${prefix}: count must be >= 1.` })
    }
    if (s.productionTime < 0) {
      errors.push({ field: `scheme.${i}.productionTime`, message: `${prefix}: productionTime cannot be negative.` })
    }
    if (s.productionLimitCount !== undefined && s.productionLimitCount < 0) {
      errors.push({ field: `scheme.${i}.productionLimitCount`, message: `${prefix}: productionLimitCount cannot be negative.` })
    }
    const validReqTypes = ['Item', 'Tool', 'Area', 'Resource']
    for (let ri = 0; ri < (s.requirements || []).length; ri++) {
      const r = s.requirements![ri]
      if (!validReqTypes.includes(r.type)) {
        errors.push({ field: `scheme.${i}.requirements[${ri}].type`, message: `${prefix}: requirement[${ri}] type must be Item, Tool, Area or Resource.` })
      }
      if ((r.type === 'Item' || r.type === 'Tool' || r.type === 'Resource') && (!r.templateId || !HEX_24.test(r.templateId))) {
        errors.push({ field: `scheme.${i}.requirements[${ri}].templateId`, message: `${prefix}: requirement[${ri}] templateId must be a 24-char hex item TPL.` })
      }
      if ((r.type === 'Item' || r.type === 'Tool') && (r.count === undefined || r.count < 0)) {
        errors.push({ field: `scheme.${i}.requirements[${ri}].count`, message: `${prefix}: requirement[${ri}] count must be >= 0.` })
      }
      if (r.type === 'Area' && (r.areaType === undefined || r.requiredLevel === undefined)) {
        errors.push({ field: `scheme.${i}.requirements[${ri}]`, message: `${prefix}: requirement[${ri}] areaType and requiredLevel are required for Area.` })
      }
      if (r.type === 'Resource' && (r.resource === undefined || r.resource < 0)) {
        errors.push({ field: `scheme.${i}.requirements[${ri}].resource`, message: `${prefix}: requirement[${ri}] resource amount must be >= 0.` })
      }
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
      if ((obj.type === 'handover_item' || obj.type === 'handover_fir_item' || obj.type === 'find_item') && obj.itemPool.length === 0) {
        errors.push({ field: `rotating.${i}.obj.${j}.itemPool`, message: `${objPrefix}: itemPool is required for ${obj.type} objectives.` })
      }
    }
    if (t.questCount < 1) {
      errors.push({ field: `rotating.${i}.questCount`, message: `${prefix}: questCount must be >= 1.` })
    }
  }

  return errors
}

export function buildQuestExportJson(pack: QuestPackDefinition, trader?: TraderDefinition): object | null {
  const hasQuests = pack.storyQuests.length > 0 || pack.rotatingQuests.length > 0
  if (!hasQuests) return null

  const output: Record<string, unknown> = {}

  if (pack.defaultQuestIcon) {
    output.defaultQuestIcon = pack.defaultQuestIcon
  }

  const lockedByQuest = new Map<string, string[]>()
  if (trader?.assort) {
    for (const item of trader.assort) {
      if (item.lockedByQuest && HEX_24.test(item.lockedByQuest)) {
        const list = lockedByQuest.get(item.lockedByQuest) || []
        if (HEX_24.test(item.itemTpl) && !list.includes(item.itemTpl)) {
          list.push(item.itemTpl)
        }
        lockedByQuest.set(item.lockedByQuest, list)
      }
    }
  }

  if (pack.storyQuests.length > 0) {
    output.storyQuests = pack.storyQuests.map(q => {
      const questUnlocks = lockedByQuest.get(q.id) || []
      const rewards: typeof q.rewards = { ...q.rewards }
      if (questUnlocks.length > 0) {
        const existing = new Set(rewards.unlockAssortItems || [])
        for (const tpl of questUnlocks) existing.add(tpl)
        rewards.unlockAssortItems = Array.from(existing)
      }
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
          if (obj.type === 'find_item') {
            o.handoverAfterFind = obj.handoverAfterFind !== false
            if (obj.countInRaid) o.countInRaid = true
          }
          if (obj.description) o.description = obj.description
          if (obj.minDistance != null) o.minDistance = obj.minDistance
          if (obj.maxDistance != null) o.maxDistance = obj.maxDistance
          if (obj.weaponTpls?.length) o.weaponTpls = obj.weaponTpls
          if (obj.wearing?.length) o.wearing = obj.wearing
          if (obj.notWearing?.length) o.notWearing = obj.notWearing
          if (obj.timeFrom != null) o.timeFrom = obj.timeFrom
          if (obj.timeTo != null) o.timeTo = obj.timeTo
          if (obj.bodyPart?.length) o.bodyPart = obj.bodyPart
          if (obj.requiredExtract) o.requiredExtract = obj.requiredExtract
          if (obj.oneSessionOnly) o.oneSessionOnly = true
          if (obj.zoneId) o.zoneId = obj.zoneId
          if (obj.plantTime != null) o.plantTime = obj.plantTime
          if (obj.plantItemTpl) o.plantItemTpl = obj.plantItemTpl
          return o
        }),
        rewards: buildRewardsJson(rewards),
      }
      if (q.image) quest.image = q.image
      return quest
    })
  } else {
    output.storyQuests = []
  }

  if (pack.zones && pack.zones.length > 0) {
    output.zones = pack.zones
  }

  if (pack.productionSchemes && pack.productionSchemes.length > 0) {
    output.productionSchemes = pack.productionSchemes
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
  if (rewards.initialEquipment && rewards.initialEquipment.length > 0) {
    r.initialEquipment = rewards.initialEquipment.map(item => {
      const out: Record<string, unknown> = { itemTpl: item.itemTpl, count: item.count }
      if (item.children && item.children.length > 0) out.children = item.children
      return out
    })
  }
  if (rewards.items && rewards.items.length > 0) {
    r.items = rewards.items.map(item => {
      const out: Record<string, unknown> = { itemTpl: item.itemTpl, count: item.count }
      if (item.children && item.children.length > 0) out.children = item.children
      return out
    })
  }
  if (rewards.unlockAssortItems && rewards.unlockAssortItems.length > 0) {
    r.unlockAssortItems = rewards.unlockAssortItems
  }
  if (rewards.recipes && rewards.recipes.length > 0) {
    r.recipes = rewards.recipes
  }
  if (rewards.stashRows && rewards.stashRows > 0) r.stashRows = rewards.stashRows
  if (rewards.skills && rewards.skills.length > 0) r.skills = rewards.skills
  if (rewards.pockets) r.pockets = rewards.pockets
  if (rewards.customPocket) r.customPocket = rewards.customPocket
  return r
}
