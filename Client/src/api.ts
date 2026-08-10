export type Activity = {
  id: number
  action: string
  summary: string
  turnsSpent: number
  cashDelta: number
  bankDelta: number
  createdAtUtc: string
}

export type StoreItem = {
  key: string
  name: string
  category: string
  price: number
  description: string
}

export type CrewReport = {
  managementCapacity: number
  unmanagedHoes: number
  armedThugs: number
  uncoveredThugs: number
  condomsNeededForMaxStreetAction: number
  beerNeededForMaxStreetAction: number
  condomCostForMaxStreetAction: number
  beerCostForMaxStreetAction: number
  supplyCostForMaxStreetAction: number
  hirePimpCost: number
  hireHoeCost: number
  hireThugCost: number
  minHoeMoraleToHire: number
  minThugMoraleToHire: number
}

export type Dashboard = {
  playerId: string
  name: string
  isAdmin: boolean
  city: string
  cash: number
  bankCash: number
  netWorth: number
  rank: number
  turns: number
  maxTurns: number
  maxActionTurns: number
  turnsPerTick: number
  turnTickMinutes: number
  secondsUntilNextTurnTick: number
  pimps: number
  hoes: number
  thugs: number
  hoeCutPercent: number
  hoeHappiness: number
  thugHappiness: number
  condoms: number
  beer: number
  weapons: number
  weed: number
  coke: number
  weedSellPrice: number
  cokeSellPrice: number
  crewReport: CrewReport
  store: StoreItem[]
  recentActivity: Activity[]
}

export type LeaderboardEntry = {
  rank: number
  playerName: string
  city: string
  netWorth: number
  cash: number
  bankCash: number
  pimps: number
  hoes: number
  thugs: number
}

export type WorldNewsEntry = {
  id: number
  playerName: string
  city: string
  action: string
  summary: string
  turnsSpent: number
  createdAtUtc: string
}

export type ActionResult = {
  summary: string
  turnsRemaining: number
  breakdown?: Record<string, unknown>
}

export type GameOptions = {
  turnsPerTick: number
  turnTickMinutes: number
  maxTurns: number
  maxActionTurns: number
  startingTurns: number
  startingCash: number
  startingBankCash: number
  condomPrice: number
  beerPrice: number
  weaponPrice: number
  weedSellPrice: number
  cokeSellPrice: number
  streetAction: {
    baseGrossPerTurn: number
    pimpRecruitChance: number
    hoeRecruitChance: number
    thugRecruitChance: number
  }
  production: {
    weed: { costPerTurn: number, unitsMin: number, unitsMax: number }
    coke: { costPerTurn: number, unitsMin: number, unitsMax: number }
  }
  morale: {
    hoesManagedPerPimp: number
    turnsPerCondom: number
    turnsPerBeer: number
    desertionThreshold: number
    maxDesertionChance: number
  }
  crew: {
    maxCrewTransactionQuantity: number
    hirePimpCost: number
    hireHoeCost: number
    hireThugCost: number
    minHoeMoraleToHire: number
    minThugMoraleToHire: number
  }
}

export type AdminOverview = {
  generatedAtUtc: string
  totalAccounts: number
  adminAccounts: number
  botAccounts: number
  totalPlayers: number
  totalCashOnHand: number
  totalBankCash: number
  totalLiquidCash: number
  totalNetWorth: number
  totalTurnsBanked: number
  averageHoeMorale: number
  averageThugMorale: number
  botAutomation: BotAutomationStatus
  economy: GameOptions
}

export type BotAutomationStatus = {
  enabled: boolean
  tickSeconds: number
  roundsPerTick: number
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    ...options,
  })

  if (!response.ok) {
    let message = response.status === 401 ? 'Unauthorized' : `Request failed (${response.status})`
    try {
      const body = await response.json()
      if (body?.error) message = body.error
    } catch { /* empty */ }
    throw new Error(message)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  register: (username: string, password: string, playerName: string) =>
    request('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, password, playerName }),
    }),
  login: (username: string, password: string) =>
    request('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    }),
  logout: () => request('/api/auth/logout', { method: 'POST' }),
  dashboard: () => request<Dashboard>('/api/game/dashboard'),
  leaderboard: () => request<LeaderboardEntry[]>('/api/game/leaderboard'),
  worldNews: () => request<WorldNewsEntry[]>('/api/world/news'),
  adminOverview: () => request<AdminOverview>('/api/admin/overview'),
  adminCheat: (cheat: string, amount: number) => request<ActionResult>('/api/admin/cheats', {
    method: 'POST',
    body: JSON.stringify({ cheat, amount }),
  }),
  adminSeedBots: (count: number) => request<ActionResult>('/api/admin/bots/seed', {
    method: 'POST',
    body: JSON.stringify({ count }),
  }),
  adminRunBots: (rounds: number) => request<ActionResult>('/api/admin/bots/run', {
    method: 'POST',
    body: JSON.stringify({ rounds }),
  }),
  adminSetBotAutomation: (enabled: boolean) => request<ActionResult>('/api/admin/bots/automation', {
    method: 'PUT',
    body: JSON.stringify({ enabled }),
  }),
  workStreet: (turns: number) => request<ActionResult>('/api/game/street', {
    method: 'POST',
    body: JSON.stringify({ turns }),
  }),
  produce: (product: 'weed' | 'coke', turns: number) => request<ActionResult>('/api/game/production', {
    method: 'POST',
    body: JSON.stringify({ product, turns }),
  }),
  sellProduct: (product: 'weed' | 'coke', quantity: number) => request<ActionResult>('/api/game/product/sell', {
    method: 'POST',
    body: JSON.stringify({ product, quantity }),
  }),
  buyStoreItem: (itemKey: string, quantity: number) => request<ActionResult>('/api/game/store/buy', {
    method: 'POST',
    body: JSON.stringify({ itemKey, quantity }),
  }),
  deposit: (amount: number) => request<ActionResult>('/api/game/bank/deposit', {
    method: 'POST',
    body: JSON.stringify({ amount }),
  }),
  withdraw: (amount: number) => request<ActionResult>('/api/game/bank/withdraw', {
    method: 'POST',
    body: JSON.stringify({ amount }),
  }),
  setHoeCut: (hoeCutPercent: number) => request<ActionResult>('/api/game/crew/settings', {
    method: 'PUT',
    body: JSON.stringify({ hoeCutPercent }),
  }),
  hireCrew: (role: 'pimps' | 'hoes' | 'thugs', quantity: number) => request<ActionResult>('/api/game/crew/hire', {
    method: 'POST',
    body: JSON.stringify({ role, quantity }),
  }),
  fireCrew: (role: 'pimps' | 'hoes' | 'thugs', quantity: number) => request<ActionResult>('/api/game/crew/fire', {
    method: 'POST',
    body: JSON.stringify({ role, quantity }),
  }),
}
