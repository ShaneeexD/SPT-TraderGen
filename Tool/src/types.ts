export interface TraderDefinition {
  // Tool-only fields (not exported to trader JSON)
  packName: string
  avatarDataUrl?: string
  enabled: boolean
  id: string
  nickname: string
  firstName: string
  lastName: string
  fullName?: string
  location: string
  description: string
  avatar: string
  currency: string
  unlockedByDefault: boolean
  buyerEnabled: boolean
  ragfairEnabled: boolean
  balanceRub: number
  balanceDol: number
  balanceEur: number
  refreshTimeMin: number
  refreshTimeMax: number
  insuranceEnabled: boolean
  repairEnabled: boolean
  loyaltyLevels: LoyaltyLevel[]
  assort: AssortItem[]
}

export interface LoyaltyLevel {
  level: number
  minLevel: number
  minSalesSum: number
  minStanding: number
  buyPriceCoef: number
}

export interface AssortItem {
  itemTpl: string
  loyaltyLevel: number
  stock: number
  unlimitedStock: boolean
  price: number
  currency?: string
  barter?: BarterRequirement[]
  buyLimit: number
}

export interface BarterRequirement {
  itemTpl: string
  count: number
}

// ==================== Quest Types ====================

export interface QuestPackDefinition {
  defaultQuestIcon?: string
  defaultQuestIconDataUrl?: string  // Tool-only: holds the drag-and-drop image data
  storyQuests: StoryQuestDefinition[]
  rotatingQuests: RotatingQuestTemplate[]
}

export interface StoryQuestDefinition {
  id: string
  traderId: string
  name: string
  description: string
  successMessage: string
  startedMessage: string
  image?: string
  imageDataUrl?: string  // Tool-only: holds the drag-and-drop image data
  location: string
  requirements: QuestRequirements
  objectives: QuestObjective[]
  rewards: QuestRewards
}

export interface QuestRequirements {
  playerLevel: number
  previousQuest?: string
}

export interface QuestObjective {
  type: string
  count: number
  target?: string
  location?: string
  itemTpl?: string
  description?: string
  // Advanced kill conditions (all optional)
  minDistance?: number | null
  maxDistance?: number | null
  weaponTpls?: string[]
  weaponCategories?: string[]
  wearing?: string[]
  notWearing?: string[]
  timeFrom?: number | null
  timeTo?: number | null
  bodyPart?: string[]
  surviveAfterKill?: boolean
  requiredExtract?: string
}

export interface QuestRewards {
  xp: number
  money?: MoneyReward
  traderStanding: number
  items?: RewardItem[]
  unlockAssortItems?: string[]
}

export interface MoneyReward {
  currency: string
  amount: number
}

export interface RewardItem {
  itemTpl: string
  count: number
}

export interface RotatingQuestTemplate {
  id: string
  rotation: string
  namePool: string[]
  descriptionPool: string[]
  objectives: RotatingObjectiveTemplate[]
  rewardScaling: RewardScaling
  image?: string
  imageDataUrl?: string  // Tool-only: holds the drag-and-drop image data
  questCount: number
}

export interface RotatingObjectiveTemplate {
  type: string
  countRange: { min: number; max: number }
  targetPool: string[]
  locationPool: string[]
  itemPool: string[]
  foundInRaid: boolean
}

export interface RewardScaling {
  xpPerObjectiveCount: number
  baseMoney: number
  moneyPerObjectiveCount: number
  currency: string
  standing: number
}

// Map location constants
export const MAP_LOCATIONS = [
  { value: 'any', label: 'Any Location' },
  { value: 'bigmap', label: 'Customs' },
  { value: 'factory4', label: 'Factory' },
  { value: 'factory4_day', label: 'Factory (Day)' },
  { value: 'factory4_night', label: 'Factory (Night)' },
  { value: 'Woods', label: 'Woods' },
  { value: 'Shoreline', label: 'Shoreline' },
  { value: 'Interchange', label: 'Interchange' },
  { value: 'Lighthouse', label: 'Lighthouse' },
  { value: 'Reserve', label: 'Reserve' },
  { value: 'laboratory', label: 'The Lab' },
  { value: 'TarkovStreets', label: 'Streets of Tarkov' },
  { value: 'Sandbox', label: 'Ground Zero' },
] as const

export const OBJECTIVE_TYPES = [
  { value: 'kill_enemy', label: 'Kill Enemies' },
  { value: 'handover_item', label: 'Hand Over Items' },
  { value: 'handover_fir_item', label: 'Hand Over Items (Found in Raid)' },
  { value: 'survive_location', label: 'Survive & Extract' },
  { value: 'extract_location', label: 'Extract from Location' },
] as const

export const ENEMY_TARGETS = [
  { value: 'Savage', label: 'Scavs' },
  { value: 'AnyPmc', label: 'PMCs' },
  { value: 'Any', label: 'Any Enemy' },
  { value: 'exUsec', label: 'Rogues' },
  { value: 'pmcBot', label: 'Raiders' },
  { value: 'bossBully', label: 'Reshala' },
  { value: 'bossKilla', label: 'Killa' },
  { value: 'bossKojaniy', label: 'Shturman' },
  { value: 'bossSanitar', label: 'Sanitar' },
  { value: 'bossTagilla', label: 'Tagilla' },
  { value: 'bossGluhar', label: 'Gluhar' },
  { value: 'bossZryachiy', label: 'Zryachiy' },
  { value: 'bossBoar', label: 'Kaban' },
  { value: 'bossPartisan', label: 'Partisan' },
  { value: 'bossKolontay', label: 'Kolontay' },
  { value: 'bossKnight', label: 'Knight' },
  { value: 'sectantPriest', label: 'Cultist Priest' },
  { value: 'sectantWarrior', label: 'Cultist Warrior' },
] as const

export const ROTATION_TYPES = [
  { value: 'daily', label: 'Daily' },
  { value: 'weekly', label: 'Weekly' },
] as const

export interface ValidationError {
  field: string
  message: string
}

export function createDefaultTrader(): TraderDefinition {
  return {
    packName: 'MyTraderPack',
    avatarDataUrl: undefined,
    enabled: true,
    id: generateMongoId(),
    nickname: '',
    firstName: '',
    lastName: 'Unknown',
    fullName: '',
    location: 'Unknown',
    description: '',
    avatar: 'assets/avatar.jpg',
    currency: 'RUB',
    unlockedByDefault: true,
    buyerEnabled: true,
    ragfairEnabled: true,
    balanceRub: 5000000,
    balanceDol: 0,
    balanceEur: 0,
    refreshTimeMin: 1800,
    refreshTimeMax: 7200,
    insuranceEnabled: false,
    repairEnabled: false,
    loyaltyLevels: [
      { level: 1, minLevel: 1, minSalesSum: 0, minStanding: 0, buyPriceCoef: 40 },
    ],
    assort: [],
  }
}

export function createDefaultAssortItem(): AssortItem {
  return {
    itemTpl: '',
    loyaltyLevel: 1,
    stock: 999999,
    unlimitedStock: true,
    price: 0,
    currency: undefined,
    barter: undefined,
    buyLimit: 0,
  }
}

export function createDefaultBarter(): BarterRequirement {
  return { itemTpl: '', count: 1 }
}

export function createDefaultQuestPack(): QuestPackDefinition {
  return {
    storyQuests: [],
    rotatingQuests: [],
  }
}

export function createDefaultStoryQuest(traderId: string): StoryQuestDefinition {
  return {
    id: generateMongoId(),
    traderId,
    name: '',
    description: '',
    successMessage: 'Good work. Come back when you\'re ready.',
    startedMessage: 'Get it done.',
    location: 'any',
    requirements: { playerLevel: 1 },
    objectives: [],
    rewards: { xp: 5000, money: { currency: 'RUB', amount: 50000 }, traderStanding: 0.02 },
  }
}

export function createDefaultObjective(): QuestObjective {
  return { type: 'kill_enemy', count: 5, target: 'Savage' }
}

export function createDefaultRotatingTemplate(): RotatingQuestTemplate {
  return {
    id: generateMongoId(),
    rotation: 'daily',
    namePool: ['Cleanup {location}'],
    descriptionPool: ['Head to {location} and deal with the threat.'],
    objectives: [createDefaultRotatingObjective()],
    rewardScaling: {
      xpPerObjectiveCount: 500,
      baseMoney: 20000,
      moneyPerObjectiveCount: 5000,
      currency: 'RUB',
      standing: 0.01,
    },
    questCount: 1,
  }
}

export function createDefaultRotatingObjective(): RotatingObjectiveTemplate {
  return {
    type: 'kill_enemy',
    countRange: { min: 3, max: 10 },
    targetPool: ['Savage'],
    locationPool: ['bigmap', 'factory4_day', 'Woods'],
    itemPool: [],
    foundInRaid: false,
  }
}

export function generateMongoId(): string {
  const hex = '0123456789abcdef'
  let id = ''
  for (let i = 0; i < 24; i++) {
    id += hex[Math.floor(Math.random() * 16)]
  }
  return id
}
