/**
 * Build script that bundles SPT vanilla quest data into a single JSON
 * file for the TraderGen web tool to consume at runtime.
 */
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const QUESTS_PATH = path.resolve(__dirname, '..', 'database', 'templates', 'quests.json')
const LOCALE_PATH = path.resolve(__dirname, '..', 'database', 'locales', 'global', 'en.json')
const OUT_PATH = path.resolve(__dirname, '..', 'public', 'vanilla-quests.json')

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

function getLocale(locales, key) {
  return locales[key] || ''
}

// SPT quest location can be a template ID or a name string
const LOCATION_ID_MAP = {
  '56f40101d2720b2a4d8b45d6': 'bigmap',
  '5704e3c2d2720bac5b8b4567': 'Woods',
  '5704e554d2720bac5b8b456e': 'Shoreline',
  '5714dbc024597771384a510d': 'Interchange',
  '5704e4dad2720bb55b8b4567': 'Lighthouse',
  '5704e5fad2720bc05b8b4567': 'Reserve',
  '5b0fc42d86f7744a585f9105': 'laboratory',
  '5714dc692459777137212e12': 'TarkovStreets',
  '653e6760052c01c1c805532f': 'Sandbox',
  '65b8d6f5cdde2479cb2a3125': 'Sandbox',
  '55f2d3fd4bdc2d5f408b4567': 'factory4_day',
  '59fc81d786f774390775787e': 'factory4_night',
  '56db0b3bd2720bb0678b4567': 'any',
}

function resolveLocation(sptLoc) {
  if (LOCATION_ID_MAP[sptLoc]) return LOCATION_ID_MAP[sptLoc]

  const map = {
    'any': 'any',
    'bigmap': 'bigmap',
    'factory4_day': 'factory4_day',
    'factory4_night': 'factory4_night',
    'factory4': 'factory4',
    'Woods': 'Woods',
    'Shoreline': 'Shoreline',
    'Interchange': 'Interchange',
    'Lighthouse': 'Lighthouse',
    'RezervBase': 'Reserve',
    'laboratory': 'laboratory',
    'TarkovStreets': 'TarkovStreets',
    'Sandbox': 'Sandbox',
    'Sandbox_high': 'Sandbox',
    'develop': 'any',
  }
  return map[sptLoc] || 'any'
}

function convertRequirements(conditions) {
  let playerLevel = 1
  let previousQuest = undefined

  for (const c of conditions || []) {
    if (c.conditionType === 'Level' && c.compareMethod === '>=') {
      playerLevel = Math.max(playerLevel, c.value || 1)
    } else if (c.conditionType === 'Quest' && c.target) {
      // Only map if status includes completed (4) or success (5)
      const status = Array.isArray(c.status) ? c.status : [c.status]
      if (status.some(s => s === 4 || s === 5)) {
        previousQuest = c.target
      }
    }
  }

  return { playerLevel, previousQuest }
}

function convertObjective(c, questLocation) {
  // Direct HandoverItem / FindItem
  if (c.conditionType === 'HandoverItem' || c.conditionType === 'FindItem') {
    const fir = c.onlyFoundInRaid === true
    const itemTpl = Array.isArray(c.target) ? c.target[0] : c.target
    return {
      type: fir ? 'handover_fir_item' : 'handover_item',
      count: c.value || 1,
      itemTpl,
      location: questLocation !== 'any' ? questLocation : undefined,
    }
  }

  // CounterCreator: look inside counter.conditions
  if (c.conditionType === 'CounterCreator') {
    const inner = c.counter?.conditions || []

    // Check for Kills
    const killsCond = inner.find(x => x.conditionType === 'Kills')
    if (killsCond) {
      const locCond = inner.find(x => x.conditionType === 'Location')
      const loc = locCond ? (Array.isArray(locCond.target) ? locCond.target[0] : locCond.target) : questLocation

      return {
        type: 'kill_enemy',
        count: c.value || 1,
        target: killsCond.target || 'Any',
        location: loc !== 'any' ? resolveLocation(loc) : undefined,
        minDistance: killsCond.distance?.value > 0 ? killsCond.distance.value : undefined,
        maxDistance: undefined,
        weaponTpls: (killsCond.weapon || []).length > 0 ? killsCond.weapon : undefined,
        bodyPart: (killsCond.bodyPart || []).length > 0 ? killsCond.bodyPart : undefined,
        timeFrom: killsCond.daytime?.from || undefined,
        timeTo: killsCond.daytime?.to || undefined,
      }
    }

    // Check for ExitStatus → survive_location or extract_location
    const exitCond = inner.find(x => x.conditionType === 'ExitStatus')
    if (exitCond) {
      const statuses = Array.isArray(exitCond.status) ? exitCond.status : [exitCond.status]
      const isSurvive = statuses.includes('Survived') || statuses.includes('Runner') || statuses.includes('Transit')
      const locCond = inner.find(x => x.conditionType === 'Location')
      const loc = locCond ? (Array.isArray(locCond.target) ? locCond.target[0] : locCond.target) : questLocation

      return {
        type: isSurvive ? 'survive_location' : 'extract_location',
        count: c.value || 1,
        location: loc !== 'any' ? resolveLocation(loc) : undefined,
      }
    }

    // Check for VisitPlace
    const visitCond = inner.find(x => x.conditionType === 'VisitPlace')
    if (visitCond) {
      return {
        type: 'survive_location',
        count: c.value || 1,
        location: questLocation !== 'any' ? resolveLocation(questLocation) : undefined,
      }
    }
  }

  return null
}

function dedupeObjectives(objectives) {
  const seen = new Map()
  for (const obj of objectives) {
    const key = `${obj.type}|${obj.itemTpl || ''}|${obj.target || ''}|${obj.location || ''}`
    const existing = seen.get(key)
    if (!existing) {
      seen.set(key, obj)
    } else if ((obj.type === 'handover_item' || obj.type === 'handover_fir_item') && existing.count !== obj.count) {
      // Prefer handover over find (they often appear in pairs); keep max count
      existing.count = Math.max(existing.count, obj.count)
    }
  }
  return Array.from(seen.values())
}

function convertRewards(rewards) {
  let xp = 0
  let traderStanding = 0
  let money = undefined
  const items = []
  const unlockAssortItems = []
  const skills = []
  let pockets = undefined
  let stashRows = 0

  for (const r of rewards || []) {
    switch (r.type) {
      case 'Experience':
        xp += r.value || 0
        break
      case 'TraderStanding':
        traderStanding += r.value || 0
        break
      case 'Item': {
        const tpl = r.items?.[0]?._tpl
        if (tpl && CURRENCY_TPLS[tpl]) {
          money = { currency: CURRENCY_TPLS[tpl], amount: r.value || 0 }
        } else if (tpl) {
          // Use the root item tpl from items[0]
          items.push({ itemTpl: tpl, count: r.value || 1 })
        }
        break
      }
      case 'AssortmentUnlock':
        for (const item of r.items || []) {
          if (item._tpl) unlockAssortItems.push(item._tpl)
        }
        break
      case 'Skill':
        skills.push({ name: r.target || '', points: r.value || 0 })
        break
      case 'Pockets':
        pockets = r.target
        break
      case 'Stash':
        stashRows += r.value || 0
        break
    }
  }

  const result = { xp, traderStanding }
  if (money) result.money = money
  if (items.length > 0) result.items = items
  if (unlockAssortItems.length > 0) result.unlockAssortItems = unlockAssortItems
  if (skills.length > 0) result.skills = skills
  if (pockets) result.pockets = pockets
  if (stashRows > 0) result.stashRows = stashRows
  return result
}

function convertQuest(quest, locales) {
  const location = resolveLocation(quest.location || 'any')
  const reqs = convertRequirements(quest.conditions?.AvailableForStart)

  const rawObjectives = []
  for (const c of quest.conditions?.AvailableForFinish || []) {
    const obj = convertObjective(c, location)
    if (obj) rawObjectives.push(obj)
  }

  const objectives = dedupeObjectives(rawObjectives)
  const rewards = convertRewards(quest.rewards?.Success)

  return {
    id: quest._id,
    traderId: quest.traderId || '',
    name: quest.QuestName || getLocale(locales, quest.name) || quest._id,
    description: getLocale(locales, quest.description),
    successMessage: getLocale(locales, quest.successMessageText),
    startedMessage: getLocale(locales, quest.acceptPlayerMessage),
    image: quest.image || '',
    location,
    requirements: reqs,
    objectives,
    rewards,
  }
}

function main() {
  const questsData = loadJson(QUESTS_PATH)
  const locales = loadJson(LOCALE_PATH)

  const quests = []
  for (const id in questsData) {
    const q = questsData[id]
    // Only convert quests that have a traderId and are from known vanilla traders
    if (!q.traderId) continue
    quests.push(convertQuest(q, locales))
  }

  // Sort by trader then by name
  quests.sort((a, b) => {
    if (a.traderId !== b.traderId) return a.traderId.localeCompare(b.traderId)
    return a.name.localeCompare(b.name)
  })

  fs.writeFileSync(OUT_PATH, JSON.stringify(quests, null, 2))
  console.log(`[build-vanilla-quests] Bundled ${quests.length} vanilla quests → public/vanilla-quests.json`)
}

main()
