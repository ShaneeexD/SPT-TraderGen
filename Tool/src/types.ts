export interface TraderDefinition {
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

export interface ValidationError {
  field: string
  message: string
}

export function createDefaultTrader(): TraderDefinition {
  return {
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

export function generateMongoId(): string {
  const hex = '0123456789abcdef'
  let id = ''
  for (let i = 0; i < 24; i++) {
    id += hex[Math.floor(Math.random() * 16)]
  }
  return id
}
