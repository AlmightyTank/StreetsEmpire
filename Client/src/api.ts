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
  hqRestTurnCost: number
  hqRestCashCost: number
  hqRestMoraleGain: number
  hqPartyTurnCost: number
  hqPartyCashCost: number
  hqPartyBeerCost: number
  hqPartyWeedCost: number
  hqPartyHoeMoraleGain: number
  hqPartyThugMoraleGain: number
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
  combatCrew: CombatCrew
  combatStatus: CombatStatus
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

export type CombatReadiness = {
  attackPower: number
  defensePower: number
  armedThugs: number
  uncoveredThugs: number
  weaponCoveragePercent: number
  averageMorale: number
  riskBand: string
}

export type CombatStatus = {
  isProtected: boolean
  protectionUntilUtc?: string | null
  lastAttackAtUtc?: string | null
  lastAttackedAtUtc?: string | null
  attackCooldownUntilUtc?: string | null
  canAttackNow: boolean
  attackTurnCost: number
  recentAttacksMade: number
  recentDefenses: number
  eligibility: string
}

export type CombatCrew = {
  committedPimps: number
  committedThugs: number
  committedWeapons: number
  availablePimps: number
  availableThugs: number
  availableWeapons: number
  activeAttackMissions: number
  maxActiveAttackMissions: number
}

export type PlayerTarget = {
  playerId: string
  name: string
  city: string
  isBot: boolean
  aiPersonality?: string | null
  rank: number
  netWorth: number
  pimps: number
  hoes: number
  thugs: number
  weapons: number
  averageMorale: number
  combatReadiness: CombatReadiness
  combatStatus: CombatStatus
}

export type PlayerProfile = PlayerTarget & {
  cash: number
  bankCash: number
  weed: number
  coke: number
  hoeHappiness: number
  thugHappiness: number
  publicActivity: Activity[]
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

export type CombatLog = {
  id: number
  attackerId: string
  attackerName: string
  defenderId: string
  defenderName: string
  outcome: string
  summary: string
  turnsSpent: number
  attackerPower: number
  defenderPower: number
  cashStolen: number
  weedStolen: number
  cokeStolen: number
  attackerPimpsLost: number
  attackerHoesLost: number
  attackerThugsLost: number
  attackerWeaponsLost: number
  defenderPimpsLost: number
  defenderHoesLost: number
  defenderThugsLost: number
  defenderWeaponsLost: number
  defenderProtectionUntilUtc?: string | null
  resolvesAtUtc?: string | null
  resolvedAtUtc?: string | null
  createdAtUtc: string
}

export type CombatMissionEvent = {
  id: number
  round: number
  kind: string
  summary: string
  attackRoll: number
  defenseRoll: number
  attackerMorale: number
  defenderMorale: number
  attackerThugsLost: number
  defenderThugsLost: number
  attackerWeaponsLost: number
  defenderWeaponsLost: number
  createdAtUtc: string
}

export type CombatMission = {
  id: number
  attackerId: string
  attackerName: string
  defenderId: string
  defenderName: string
  status: string
  outcome: string
  summary: string
  turnsSpent: number
  assignedPimps: number
  assignedThugs: number
  assignedWeapons: number
  remainingAttackers: number
  remainingWeapons: number
  attackerMorale: number
  defenderMorale: number
  currentRound: number
  maxRounds: number
  attackerPower: number
  defenderPower: number
  cashStolen: number
  weedStolen: number
  cokeStolen: number
  startedAtUtc: string
  arrivesAtUtc: string
  nextRoundAtUtc?: string | null
  returnsAtUtc?: string | null
  completedAtUtc?: string | null
  defenderProtectionUntilUtc?: string | null
  canCancel: boolean
  cancelCashCost: number
  events: CombatMissionEvent[]
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
    passiveRecoveryPerTick: number
    hqRestTurnCost: number
    hqRestCashPerCrew: number
    hqRestMoraleGain: number
    hqPartyTurnCost: number
    hqPartyCashPerCrew: number
    hqPartyBeerPerThug: number
    hqPartyWeedPerHoes: number
    hqPartyHoeMoraleGain: number
    hqPartyThugMoraleGain: number
  }
  crew: {
    maxCrewTransactionQuantity: number
    hirePimpCost: number
    hireHoeCost: number
    hireThugCost: number
    minHoeMoraleToHire: number
    minThugMoraleToHire: number
  }
  combat: {
    attackTurnCost: number
    attackCooldownMinutes: number
    attackTravelSecondsMin: number
    attackTravelSecondsMax: number
    returnTravelSecondsMin: number
    returnTravelSecondsMax: number
    fightRoundSeconds: number
    maxFightRounds: number
    maxActiveAttackMissions: number
    moraleBreakThreshold: number
    defenderProtectionMinutes: number
    powerRandomnessPercent: number
    minCashLootPercent: number
    maxCashLootPercent: number
    minProductLootPercent: number
    maxProductLootPercent: number
    winnerCrewLossPercent: number
    loserCrewLossPercent: number
    weaponLossPercent: number
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
  targets: (query = '') => request<PlayerTarget[]>(`/api/game/targets${query ? `?query=${encodeURIComponent(query)}` : ''}`),
  playerProfile: (playerId: string) => request<PlayerProfile>(`/api/game/players/${encodeURIComponent(playerId)}/profile`),
  combatLogs: () => request<CombatLog[]>('/api/game/combat/logs'),
  combatMissions: () => request<CombatMission[]>('/api/game/combat/missions'),
  attack: (defenderId: string, pimps: number, thugs: number, weapons: number) => request<ActionResult>('/api/game/combat/attack', {
    method: 'POST',
    body: JSON.stringify({ defenderId, pimps, thugs, weapons }),
  }),
  cancelCombatMission: (missionId: number) => request<ActionResult>(`/api/game/combat/missions/${missionId}/cancel`, { method: 'POST' }),
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
  recoverMorale: (strategy: 'rest' | 'party') => request<ActionResult>('/api/game/hideout/recover', {
    method: 'POST',
    body: JSON.stringify({ strategy }),
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
