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
  buyCategories?: string[]
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

export interface AssortChildItem {
  itemTpl: string
  slotId: string
  amount?: number
  children?: AssortChildItem[]
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
  children?: AssortChildItem[]
  lockedByQuest?: string
}

export interface BarterRequirement {
  itemTpl: string
  count: number
  level?: number
  side?: string
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
  wearing?: string[]
  notWearing?: string[]
  timeFrom?: number | null
  timeTo?: number | null
  bodyPart?: string[]
  requiredExtract?: string
  oneSessionOnly?: boolean
}

export interface QuestRewards {
  xp: number
  money?: MoneyReward
  traderStanding: number
  items?: RewardItem[]
  unlockAssortItems?: string[]
  stashRows?: number
  skills?: SkillReward[]
  pockets?: string
  customPocket?: CustomPocketDefinition
}

export interface SkillReward {
  name: string
  points: number
}

export interface CustomPocketDefinition {
  slots: PocketSlot[]
}

export interface PocketSlot {
  width: number
  height: number
}

export interface MoneyReward {
  currency: string
  amount: number
}

export interface RewardItem {
  itemTpl: string
  count: number
  children?: AssortChildItem[]
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

// Vanilla buy category mappings (item parent ID → display name).
export const VANILLA_BUY_CATEGORIES: { id: string; name: string }[] = [
  { id: '5422acb9af1c889c16000029', name: 'Weapon' },
  { id: '5448fe124bdc2da5018b4567', name: 'Mod' },
  { id: '5485a8684bdc2da71d8b4567', name: 'Ammo' },
  { id: '57864c8c245977548867e7f1', name: 'MedicalSupplies' },
  { id: '543be6674bdc2df1348b4569', name: 'FoodDrink' },
  { id: '543be5664bdc2dd4348b4569', name: 'Meds' },
  { id: '57864ee62459775490116fc1', name: 'Battery' },
  { id: '5447e1d04bdc2dff2f8b4567', name: 'Knife' },
  { id: '5795f317245977243854e041', name: 'SimpleContainer' },
  { id: '5671435f4bdc2d96058b4569', name: 'LockableContainer' },
  { id: '5448e53e4bdc2d60728b4567', name: 'Backpack' },
  { id: '5448e5284bdc2dcb718b4567', name: 'Vest' },
  { id: '57864e4c24597754843f8723', name: 'Lubricant' },
  { id: '57864ada245977548638de91', name: 'BuildingMaterial' },
  { id: '5447b6194bdc2d67278b4567', name: 'MarksmanRifle' },
  { id: '5447b6094bdc2dc3278b4567', name: 'Shotgun' },
  { id: '5447b6254bdc2dc3278b4568', name: 'SniperRifle' },
  { id: '55818ae44bdc2dde698b456c', name: 'OpticScope' },
  { id: '55818aeb4bdc2ddc698b456a', name: 'SpecialScope' },
  { id: '55818add4bdc2d5b648b456f', name: 'AssaultScope' },
  { id: '555ef6e44bdc2de9068b457e', name: 'Barrel' },
  { id: '55818b224bdc2dde698b456f', name: 'Mount' },
  { id: '55818a594bdc2db9688b456a', name: 'Stock' },
  { id: '543be5f84bdc2dd4348b456a', name: 'Equipment' },
  { id: '6759673c76e93d8eb20b2080', name: 'Flyer' },
  { id: '5661632d4bdc2d903d8b456b', name: 'StackableItem' },
  { id: '5447e0e74bdc2d3c308b4567', name: 'SpecItem' },
  { id: '567849dd4bdc2d150f8b456e', name: 'Map' },
  { id: '543be6564bdc2df4348b4568', name: 'ThrowWeap' },
  { id: '5448eb774bdc2d0a728b4567', name: 'BarterItem' },
  { id: '5448ecbe4bdc2d60728b4568', name: 'Info' },
  { id: '616eb7aea207f41933308f46', name: 'RepairKits' },
  { id: '543be5e94bdc2df1348b4568', name: 'Key' },
  { id: '543be5cb4bdc2deb348b4568', name: 'AmmoBox' },
  { id: '57864a66245977548f04a81f', name: 'Electronics' },
  { id: '57864bb7245977548b3b66c2', name: 'Tool' },
  { id: '5c164d2286f774194c5e69fa', name: 'Keycard' },
  { id: '57864a3d24597754843f8721', name: 'Jewelry' },
  { id: '590c745b86f7743cc433c5f2', name: 'Other' },
  { id: '5448f3a64bdc2d60728b456a', name: 'Stimulator' },
  { id: '5d650c3e815116009f6201d2', name: 'Fuel' },
  { id: '5448e54d4bdc2dcc718b4568', name: 'Armor' },
  { id: '5c99f98d86f7745c314214b3', name: 'KeyMechanical' },
  { id: '57bef4c42459772e8d35a53b', name: 'ArmoredEquipment' },
  { id: '5448f39d4bdc2d0a728b4568', name: 'MedKit' },
  { id: '5448f3ac4bdc2dce718b4569', name: 'Medical' },
  { id: '5448e8d04bdc2ddf718b4569', name: 'Food' },
  { id: '5a341c4086f77401f2541505', name: 'Headwear' },
  { id: '543be5dd4bdc2deb348b4569', name: 'Money' },
]

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
    children: undefined,
  }
}

export function createDefaultAssortChild(): AssortChildItem {
  return { itemTpl: '', slotId: '' }
}

export function createDefaultBarter(): BarterRequirement {
  return { itemTpl: '', count: 1 }
}

// Dogtag template IDs that support level/side barter requirements.
export const DOGTAG_IDS: string[] = [
  '59f32bb586f774757e1e8442', // BEAR
  '59f32c3b86f77472a31742f0', // USEC
  '6662e9aca7e0b43baa3d5f74', // BEAR
  '6662e9cda7e0b43baa3d5f76', // BEAR
  '6662e9f37fa79a6d83730fa0', // USEC
  '6662ea05f6259762c56f3189', // USEC
  '675dc9d37ae1a8792107ca96', // BEAR
  '675dcb0545b1a2d108011b2b', // BEAR
]

export function isDogtagId(id: string): boolean {
  return DOGTAG_IDS.includes(id)
}

// Returns the faction side for a known dogtag ID, or undefined.
export function getDogtagSide(id: string): string | undefined {
  const bear = [
    '59f32bb586f774757e1e8442',
    '6662e9aca7e0b43baa3d5f74',
    '6662e9cda7e0b43baa3d5f76',
    '675dc9d37ae1a8792107ca96',
    '675dcb0545b1a2d108011b2b',
  ]
  const usec = [
    '59f32c3b86f77472a31742f0',
    '6662e9f37fa79a6d83730fa0',
    '6662ea05f6259762c56f3189',
  ]
  if (bear.includes(id)) return 'Bear'
  if (usec.includes(id)) return 'Usec'
  return undefined
}

// Base vanilla dogtag IDs used in barter schemes.
const DOGTAG_BASE_BEAR = '59f32bb586f774757e1e8442'
const DOGTAG_BASE_USEC = '59f32c3b86f77472a31742f0'

// Normalizes any dogtag ID to the two base vanilla IDs used in assort.json.
export function normalizeDogtagId(id: string, side?: string): string {
  if (!isDogtagId(id)) return id
  if (side === 'Bear') return DOGTAG_BASE_BEAR
  return DOGTAG_BASE_USEC
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
