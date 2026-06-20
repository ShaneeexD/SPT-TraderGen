import type { TraderDefinition, AssortItem, StoryQuestDefinition, QuestPackDefinition } from './types'

export interface VanillaTraderEntry {
  id: string
  nickname: string
  firstName: string
  lastName: string
  fullName?: string
  location: string
  description: string
  avatar: string
  currency: string
  loyaltyLevels: { level: number; minLevel: number; minSalesSum: number; minStanding: number; buyPriceCoef: number }[]
  insuranceEnabled: boolean
  repairEnabled: boolean
  balanceRub: number
  balanceDol: number
  balanceEur: number
  buyerEnabled: boolean
  unlockedByDefault: boolean
  refreshTimeMin: number
  refreshTimeMax: number
  buyCategories: string[]
  assort: AssortItem[]
}

let cache: VanillaTraderEntry[] = []
let loading = false
let listeners: Array<(entries: VanillaTraderEntry[]) => void> = []

async function loadVanillaTraders(): Promise<VanillaTraderEntry[]> {
  if (cache.length > 0) return cache
  if (loading) {
    return new Promise(resolve => {
      listeners.push(resolve)
    })
  }
  loading = true
  try {
    const res = await fetch('/vanilla-traders.json')
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    cache = (await res.json()) as VanillaTraderEntry[]
    return cache
  } catch {
    cache = []
    return cache
  } finally {
    loading = false
    listeners.forEach(fn => fn(cache))
    listeners = []
  }
}

export async function getVanillaTraderList(): Promise<Pick<VanillaTraderEntry, 'id' | 'nickname'>[]> {
  const traders = await loadVanillaTraders()
  return traders.map(t => ({ id: t.id, nickname: t.nickname }))
}

export async function loadVanillaTraderById(id: string): Promise<TraderDefinition | null> {
  const traders = await loadVanillaTraders()
  const entry = traders.find(t => t.id === id)
  if (!entry) return null

  return {
    packName: entry.nickname.replace(/\s+/g, '_').replace(/[^a-zA-Z0-9_-]/g, ''),
    avatarDataUrl: undefined,
    enabled: true,
    id: entry.id,
    nickname: entry.nickname,
    firstName: entry.firstName,
    lastName: entry.lastName,
    fullName: entry.fullName,
    location: entry.location,
    description: entry.description,
    avatar: entry.avatar,
    currency: entry.currency,
    unlockedByDefault: entry.unlockedByDefault,
    buyerEnabled: entry.buyerEnabled,
    ragfairEnabled: true,
    balanceRub: entry.balanceRub,
    balanceDol: entry.balanceDol,
    balanceEur: entry.balanceEur,
    refreshTimeMin: entry.refreshTimeMin,
    refreshTimeMax: entry.refreshTimeMax,
    insuranceEnabled: entry.insuranceEnabled,
    repairEnabled: entry.repairEnabled,
    buyCategories: entry.buyCategories,
    loyaltyLevels: entry.loyaltyLevels,
    assort: entry.assort,
  }
}

// ==================== Vanilla Quests ====================

export interface VanillaQuestEntry extends StoryQuestDefinition {}

let questCache: VanillaQuestEntry[] | null = null
let questLoading = false
let questListeners: Array<(entries: VanillaQuestEntry[]) => void> = []

async function loadVanillaQuests(): Promise<VanillaQuestEntry[]> {
  if (questCache) return questCache
  if (questLoading) {
    return new Promise(resolve => {
      questListeners.push(resolve)
    })
  }
  questLoading = true
  try {
    const res = await fetch('/vanilla-quests.json')
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    questCache = (await res.json()) as VanillaQuestEntry[]
    return questCache
  } catch {
    questCache = []
    return questCache
  } finally {
    questLoading = false
    questListeners.forEach(fn => fn(questCache!))
    questListeners = []
  }
}

export async function getVanillaQuestList(): Promise<Pick<VanillaQuestEntry, 'id' | 'name' | 'traderId'>[]> {
  const quests = await loadVanillaQuests()
  return quests.map(q => ({ id: q.id, name: q.name, traderId: q.traderId }))
}

export async function getVanillaQuestsByTraderId(traderId: string): Promise<VanillaQuestEntry[]> {
  const quests = await loadVanillaQuests()
  return quests.filter(q => q.traderId === traderId)
}

export async function loadVanillaQuestById(id: string): Promise<StoryQuestDefinition | null> {
  const quests = await loadVanillaQuests()
  const entry = quests.find(q => q.id === id)
  return entry || null
}

export async function loadVanillaQuestPackByTraderId(traderId: string): Promise<QuestPackDefinition> {
  const quests = await getVanillaQuestsByTraderId(traderId)
  return {
    storyQuests: quests,
    rotatingQuests: [],
  }
}
