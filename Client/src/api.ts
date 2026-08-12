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

export type Hideout = {
  tierName: string
  tier: number
  storageLevel: number
  safeLevel: number
  weedLabLevel: number
  cokeLabLevel: number
  maxPimps: number
  maxHoes: number
  maxThugs: number
  maxCash: number
  maxCondoms: number
  maxBeer: number
  maxWeapons: number
  maxWeed: number
  maxCoke: number
  weedLabYieldBonusPercent: number
  cokeLabYieldBonusPercent: number
  storageUpgradeCost?: number | null
  safeUpgradeCost?: number | null
  weedLabUpgradeCost?: number | null
  cokeLabUpgradeCost?: number | null
}

export type HideoutRoom = 'storage' | 'safe' | 'weedlab' | 'cokelab'

export type Pimp = {
  id: number
  name: string
  specialty: string
  bonusPercent: number
  loyalty: number
  missionsLed: number
  victories: number
  isCommanding: boolean
  hiredAtUtc: string
  lostAtUtc?: string | null
  lostReason?: string | null
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
  hideout: Hideout
  crew: Pimp[]
  fallenCrew: Pimp[]
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
  commanderName?: string | null
  commanderBonusPercent: number
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
  // Exactly one pimp commands. Pass a commanderPimpId to pick them, or null to let the server field
  // the best Enforcer available.
  attack: (defenderId: string, thugs: number, weapons: number, commanderPimpId: number | null) =>
    request<ActionResult>('/api/game/combat/attack', {
      method: 'POST',
      body: JSON.stringify({ defenderId, thugs, weapons, commanderPimpId }),
    }),
  cancelCombatMission: (missionId: number) => request<ActionResult>(`/api/game/combat/missions/${missionId}/cancel`, { method: 'POST' }),
  worldNews: () => request<WorldNewsEntry[]>('/api/world/news'),
  adminOverview: () => request<AdminOverview>('/api/admin/overview'),
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
  workStreet: (turns: number, autoBuySupplies = false) => request<ActionResult>('/api/game/street', {
    method: 'POST',
    body: JSON.stringify({ turns, autoBuySupplies }),
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
  upgradeHideout: (room: HideoutRoom) => request<ActionResult>('/api/game/hideout/upgrade', {
    method: 'POST',
    body: JSON.stringify({ room }),
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

export type AdminPlayerSummary = {
  playerId: string
  name: string
  username: string
  city: string
  isBot: boolean
  isAdmin: boolean
  isBanned: boolean
  suspendedUntilUtc?: string | null
  enforcementReason?: string | null
  netWorth: number
  cash: number
  bankCash: number
  turns: number
  pimps: number
  hoes: number
  thugs: number
  createdAtUtc: string
}

export type AdminAuditEntry = {
  id: number
  actorUsername: string
  action: string
  targetPlayerId?: string | null
  targetName?: string | null
  summary: string
  reason?: string | null
  createdAtUtc: string
}

export type AdminPlayerDetail = {
  summary: AdminPlayerSummary
  condoms: number
  beer: number
  weapons: number
  weed: number
  coke: number
  hoeHappiness: number
  thugHappiness: number
  hoeCutPercent: number
  lastAttackAtUtc?: string | null
  lastAttackedAtUtc?: string | null
  combatProtectionUntilUtc?: string | null
  hideout: Hideout
  crew: Pimp[]
  recentActivity: Activity[]
  auditTrail: AdminAuditEntry[]
  adjustableResources: string[]
}

export const adminApi = {
  searchPlayers: (query: string) =>
    request<AdminPlayerSummary[]>(`/api/admin/players${query ? `?query=${encodeURIComponent(query)}` : ''}`),
  playerDetail: (playerId: string) =>
    request<AdminPlayerDetail>(`/api/admin/players/${encodeURIComponent(playerId)}`),
  adjust: (playerId: string, resource: string, delta: number, reason: string) =>
    request<ActionResult>(`/api/admin/players/${playerId}/adjust`, {
      method: 'POST',
      body: JSON.stringify({ resource, delta, reason }),
    }),
  setMorale: (playerId: string, morale: number, reason: string) =>
    request<ActionResult>(`/api/admin/players/${playerId}/morale`, {
      method: 'POST',
      body: JSON.stringify({ morale, reason }),
    }),
  enforcement: (playerId: string, action: 'ban' | 'suspend' | 'clear', untilUtc: string | null, reason: string) =>
    request<ActionResult>(`/api/admin/players/${playerId}/enforcement`, {
      method: 'POST',
      body: JSON.stringify({ action, untilUtc, reason }),
    }),
  forceLogout: (playerId: string, reason: string) =>
    request<ActionResult>(`/api/admin/players/${playerId}/force-logout`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
  rename: (playerId: string, name: string, reason: string) =>
    request<ActionResult>(`/api/admin/players/${playerId}/rename`, {
      method: 'POST',
      body: JSON.stringify({ name, reason }),
    }),
  setAdminRights: (playerId: string, isAdmin: boolean, reason: string) =>
    request<ActionResult>(`/api/admin/players/${playerId}/admin-rights`, {
      method: 'POST',
      body: JSON.stringify({ isAdmin, reason }),
    }),
  audit: () => request<AdminAuditEntry[]>('/api/admin/audit'),
}

export type AdminWealthBand = { label: string, players: number, totalNetWorth: number }
export type AdminMover = {
  playerId: string
  name: string
  isBot: boolean
  netWorth: number
  cashGained24h: number
  actionsLast24h: number
}
export type AdminMission = {
  missionId: number
  attackerName: string
  defenderName: string
  commanderName?: string | null
  status: string
  outcome: string
  currentRound: number
  maxRounds: number
  startedAtUtc: string
  nextEventAtUtc?: string | null
  isOverdue: boolean
}
export type AdminBotHealth = {
  playerId: string
  name: string
  personality: string
  netWorth: number
  lastActionAtUtc?: string | null
  minutesIdle: number
}
export type AdminOversight = {
  medianNetWorth: number
  topNetWorth: number
  giniPercent: number
  wealthBands: AdminWealthBand[]
  fastestMovers: AdminMover[]
  activeMissions: AdminMission[]
  bots: AdminBotHealth[]
}

export type LiveOps = {
  maintenanceMode: boolean
  maintenanceMessage?: string | null
  announcement?: string | null
  updatedAtUtc: string
  updatedBy?: string | null
}

export const opsApi = {
  oversight: () => request<AdminOversight>('/api/admin/oversight'),
  forceResolve: (missionId: number) =>
    request<ActionResult>(`/api/admin/missions/${missionId}/force-resolve`, { method: 'POST' }),
  liveOps: () => request<LiveOps>('/api/game/live-ops'),
  setLiveOps: (body: { maintenanceMode?: boolean, maintenanceMessage?: string, announcement?: string, reason?: string }) =>
    request<LiveOps>('/api/admin/live-ops', { method: 'PUT', body: JSON.stringify(body) }),
}

export type AdminConfigEntry = {
  path: string
  type: string
  effectiveValue: string
  overrideValue?: string | null
  isOverridden: boolean
}

export type AdminConfig = {
  version: number
  overrideCount: number
  settings: AdminConfigEntry[]
}

export const configApi = {
  get: () => request<AdminConfig>('/api/admin/config'),
  set: (path: string, value: string, reason: string) =>
    request<ActionResult>('/api/admin/config', {
      method: 'PUT',
      body: JSON.stringify({ path, value, reason }),
    }),
  // An empty value clears the override and falls back to appsettings.
  clear: (path: string, reason: string) =>
    request<ActionResult>('/api/admin/config', {
      method: 'PUT',
      body: JSON.stringify({ path, value: '', reason }),
    }),
}
