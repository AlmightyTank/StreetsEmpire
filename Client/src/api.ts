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
  hoesStorageCanSupply: number
  thugsStorageCanSupply: number
  storageLevelToSupplyCrew?: number | null
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
  weedLabPassivePerHour: number
  cokeLabPassivePerHour: number
  maxOfflineProductionHours: number
  storageUpgrade?: HideoutRoomUpgrade | null
  safeUpgrade?: HideoutRoomUpgrade | null
  weedLabUpgrade?: HideoutRoomUpgrade | null
  cokeLabUpgrade?: HideoutRoomUpgrade | null
  nextTier?: HideoutTierUpgrade | null
  building?: HideoutBuild | null
}

export type HideoutRoomUpgrade = {
  level: number
  cost: number
  requiredTier: number
  requiredTierName: string
  tierLocked: boolean
}

export type HideoutTierUpgrade = {
  level: number
  name: string
  cost: number
  turns: number
  buildMinutes: number
  maxPimps: number
  maxHoes: number
  maxThugs: number
}

export type HideoutBuild = {
  tier: number
  name: string
  completesAtUtc: string
  secondsRemaining: number
}

export type HideoutRoom = 'tier' | 'storage' | 'safe' | 'weedlab' | 'cokelab'

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
  currentMarket: CityMarket
  cityMarkets: CityMarket[]
  travel: TravelStatus
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
  moraleTrend: MoraleTrend
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
  unreadDefenceAlerts: number
  store: StoreItem[]
  recentActivity: Activity[]
}

export type CityMarket = {
  city: string
  weed: string
  coke: string
  risk: string
  bustChancePercent: number
  breakEvenSeizurePercent: number | null
  weedSellPrice: number
  cokeSellPrice: number
  travelTurns: number
  current: boolean
}

export type TravelStatus = {
  blockedReason: string | null
  carriedValue: number
  seizureMinPercent: number
  seizureMaxPercent: number
}

export type MoraleDirection = 'up' | 'down' | 'steady' | 'unknown'

export type MoraleTrend = {
  hoeDelta?: number | null
  thugDelta?: number | null
  hoeDirection: MoraleDirection
  thugDirection: MoraleDirection
  windowHours: number
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
  mismatchReason?: string | null
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

export type CatchUpItem = {
  kind: string
  headline: string
  detail: string
  tone: 'good' | 'bad' | 'neutral'
}

export type CatchUp = {
  sinceUtc: string
  awayMinutes: number
  hasNews: boolean
  items: CatchUpItem[]
}

export type WorldNewsEntry = {
  id: number
  playerName: string
  city: string
  action: string
  category: 'combat' | 'build' | 'arrival' | 'crew' | 'money'
  summary: string
  turnsSpent: number
  createdAtUtc: string
}

export type WorldHeadline = {
  kind: string
  title: string
  detail: string
}

export type WorldNews = {
  headlines: WorldHeadline[]
  feed: WorldNewsEntry[]
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
  lootMultiplierPercent: number
  defenderRecentHits: number
  defenderProtectionMinutes: number
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

// Anything that happened to you rather than because of you: raids, lab output, a build landing.
export type Alert = {
  id: string
  kind: 'attack' | 'labs' | 'hideout'
  headline: string
  detail: string
  tone: 'good' | 'bad' | 'neutral'
  isUnread: boolean
  createdAtUtc: string
}

export type Alerts = {
  unreadCount: number
  lastSeenAtUtc?: string | null
  alerts: Alert[]
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
  defaultTickSeconds: number
  defaultRoundsPerTick: number
  minTickSeconds: number
  maxTickSeconds: number
  minRoundsPerTick: number
  maxRoundsPerTick: number
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
  cities: () => request<string[]>('/api/auth/cities'),
  register: (username: string, password: string, playerName: string, city: string) =>
    request('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, password, playerName, city }),
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
  worldNews: () => request<WorldNews>('/api/world/news'),
  territories: () => request<TerritoryBoard>('/api/game/territories'),
  market: () => request<MarketBoard>('/api/game/market'),
  listOnMarket: (item: string, quantity: number, pricePerUnit: number) =>
    request<ActionResult>('/api/game/market/list', { method: 'POST', body: JSON.stringify({ item, quantity, pricePerUnit }) }),
  buyOnMarket: (listingId: number, quantity: number) =>
    request<ActionResult>('/api/game/market/buy', { method: 'POST', body: JSON.stringify({ listingId, quantity }) }),
  cancelListing: (listingId: number) =>
    request<ActionResult>('/api/game/market/cancel', { method: 'POST', body: JSON.stringify({ listingId }) }),
  forge: (turns: number) => request<ActionResult>('/api/game/workshop/forge', { method: 'POST', body: JSON.stringify({ turns }) }),
  travel: (city: string) => request<ActionResult>('/api/game/travel', { method: 'POST', body: JSON.stringify({ city }) }),
  claimTerritory: (territoryId: number, thugs: number, pimpId: number | null) =>
    request<ActionResult>('/api/game/territories/claim', { method: 'POST', body: JSON.stringify({ territoryId, thugs, pimpId }) }),
  setGarrison: (territoryId: number, thugs: number, pimpId: number | null) =>
    request<ActionResult>('/api/game/territories/garrison', { method: 'POST', body: JSON.stringify({ territoryId, thugs, pimpId }) }),
  raidTerritory: (territoryId: number, thugs: number, weapons: number) =>
    request<ActionResult>('/api/game/territories/raid', { method: 'POST', body: JSON.stringify({ territoryId, thugs, weapons }) }),
  catchUp: () => request<CatchUp>('/api/game/catch-up'),
  alerts: () => request<Alerts>('/api/game/alerts'),
  markAlertsSeen: () => request<Alerts>('/api/game/alerts/seen', { method: 'POST' }),
  adminOverview: () => request<AdminOverview>('/api/admin/overview'),
  adminSeedBots: (count: number) => request<ActionResult>('/api/admin/bots/seed', {
    method: 'POST',
    body: JSON.stringify({ count }),
  }),
  adminRunBots: (rounds: number) => request<ActionResult>('/api/admin/bots/run', {
    method: 'POST',
    body: JSON.stringify({ rounds }),
  }),
  adminSetBotAutomation: (enabled: boolean, timing?: { tickSeconds?: number, roundsPerTick?: number, resetTiming?: boolean }) =>
    request<ActionResult>('/api/admin/bots/automation', {
      method: 'PUT',
      body: JSON.stringify({ enabled, ...timing }),
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
// Every field is optional because each action needs a different few.
export type BotDirective = {
  action: string
  turns?: number
  product?: string
  item?: string
  role?: string
  quantity?: number
  amount?: number
  strategy?: string
  room?: string
  defenderId?: string
  thugs?: number
  weapons?: number
}

export type Territory = {
  id: number
  name: string
  city: string
  type: string
  typeLabel: string
  effect: string
  holderId?: string | null
  holderName?: string | null
  heldByYou: boolean
  garrisonThugs: number
  garrisonPimpName?: string | null
  garrisonBonusPercent: number
  heldSinceUtc?: string | null
  isProtected: boolean
  protectedUntilUtc?: string | null
  canClaim: boolean
  canRaid: boolean
  blockedReason?: string | null
}

export type MarketListing = {
  id: number
  item: string
  itemLabel: string
  quantity: number
  originalQuantity: number
  pricePerUnit: number
  sellerName: string
  yours: boolean
  referencePrice: number
  createdAtUtc: string
}

export type MarketGood = {
  item: string
  label: string
  referencePrice: number
  held: number
  room: number
  bestPrice?: number | null
}

export type MarketBoard = {
  houseCutPercent: number
  maxListingsPerPlayer: number
  yourOpenListings: number
  goods: MarketGood[]
  listings: MarketListing[]
}

export type TerritoryBoard = {
  city: string
  held: number
  holdingCap: number
  minimumGarrison: number
  claimTurnCost: number
  freeThugs: number
  effects: {
    streetIncomePercent: number
    productionYieldPercent: number
    moraleRecoveryPercent: number
    lootPercent: number
  }
  territories: Territory[]
}

export type AdminBotHealth = {
  playerId: string
  name: string
  personality: string
  netWorth: number
  lastActionAtUtc?: string | null
  minutesIdle: number
  isPaused: boolean
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
  setBotPaused: (playerId: string, paused: boolean) =>
    request<ActionResult>(`/api/admin/bots/${playerId}/pause`, { method: 'PUT', body: JSON.stringify({ paused }) }),
  actNow: (playerId: string) =>
    request<ActionResult>(`/api/admin/bots/${playerId}/act`, { method: 'POST' }),
  directBot: (playerId: string, body: BotDirective) =>
    request<ActionResult>(`/api/admin/bots/${playerId}/do`, { method: 'POST', body: JSON.stringify(body) }),
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
