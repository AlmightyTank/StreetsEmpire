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
  suppliedStreetActionTurns: number
  fireHoeMoralePenalty: number
  fireThugMoralePenalty: number
  maxFireMoralePenalty: number
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

export type Contract = {
  id: number
  buyer: string
  good: string
  quantity: number
  pricePerUnit: number
  listPricePerUnit: number
  payout: number
  premiumOverFlat: number
  minimumPurityPercent?: number | null
  minutesRemaining: number
  held: number
  /** How much is already in, how much is still wanted, and what finishing pays on top. */
  delivered: number
  remaining: number
  completionBonus: number
  /** How much of the remainder you could hand over right now. */
  canDeliverNow: number
  /** True once you have started this one, so nobody else can. */
  yours: boolean
  blockedReason?: string | null
}
export type ContractBoard = { city: string, contracts: Contract[] }

export type ChatChannelKey = 'Global' | 'City' | 'Alliance'

/** One line in a room, as it is shown. */
export type ChatMessage = {
  id: number
  author: string
  /** Your own lines are marked so the eye can find them without reading the names. */
  yours: boolean
  body: string
  sentAtUtc: string
}

/** A room you can open, and whether you can say anything in it. */
export type ChatChannel = {
  channel: ChatChannelKey
  label: string
  detail: string
  canPost: boolean
  blockedReason?: string | null
}

export type ChatBoard = {
  channel: ChatChannelKey
  /** What this room is for you: the town you are in, or your crew's name. */
  scope: string
  channels: ChatChannel[]
  messages: ChatMessage[]
  maxLength: number
}

/** A conversation in the list: what it is called, who is in it, and where it got to. */
export type ChatConversationSummary = {
  id: number
  name: string
  isGroup: boolean
  others: string[]
  lastBody: string
  sentAtUtc: string
  unread: number
}

export type ChatConversationList = { conversations: ChatConversationSummary[], unread: number }

export type ChatConversation = {
  id: number
  name: string
  isGroup: boolean
  others: string[]
  messages: ChatMessage[]
  maxLength: number
}

/** Somebody the picker found. */
export type Person = { playerId: string, name: string, city: string }
export type PeopleSearch = { people: Person[] }

export type BlockedPlayer = { playerId: string, name: string }
export type BlockedList = { blocked: BlockedPlayer[] }

export type NextMove = { label: string, why: string, page: string, cost: number, urgent: boolean }
export type Objective = { label: string, why: string, page: string, done: boolean }
export type Guidance = {
  moves: NextMove[]
  objectives: Objective[]
  objectivesDone: number
  objectivesTotal: number
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
  maxRides: number
  maxCash: number
  maxCondoms: number
  maxBeer: number
  maxWeapons: number
  maxWeed: number
  maxCoke: number
  maxMoonshine: number
  maxCut: number
  maxMedicine: number
  maxPoison: number
  weedLabYieldBonusPercent: number
  cokeLabYieldBonusPercent: number
  weedLabPassivePerHour: number
  cokeLabPassivePerHour: number
  maxOfflineProductionHours: number
  heat: number
  heatLabel: string
  heatDetail: string
  heatNote: string
  /** What the building and its rooms are worth on the board: every pound spent on them. */
  value: number
  storageUpgrade?: HideoutRoomUpgrade | null
  safeUpgrade?: HideoutRoomUpgrade | null
  weedLabUpgrade?: HideoutRoomUpgrade | null
  cokeLabUpgrade?: HideoutRoomUpgrade | null
  intelligenceUpgrade?: HideoutRoomUpgrade | null
  intelligenceLevel: number
  concurrentRunCap: number
  lookoutUpgrade?: HideoutRoomUpgrade | null
  lookoutLevel: number
  bustRiskReductionPercent: number
  nextTier?: HideoutTierUpgrade | null
  building?: HideoutBuild | null
  craftMinutesPerWork: number
  workshopCraft?: WorkshopCraft | null
  production: ProductionStation[]
  stations: HideoutStation[]
}

export type ProductionStation = {
  key: 'weed' | 'coke'
  name: string
  minPerWork: number
  maxPerWork: number
  costPerWork: number
  sellPrice: number
  sellLabel: string
  labBonusPercent: number
  requiredWorkshopLevel: number
  heatPerUnit: number
}

export type WorkshopCraft = {
  id: number
  good: string
  label: string
  quantity: number
  unitCost: number
  totalCost: number
  workUnits: number
  workshopLevel: number
  startedAtUtc: string
  completesAtUtc: string
  secondsRemaining: number
}

export type HideoutStation = {
  key: string
  name: string
  good: string
  level: number
  perTurn: number
  costPerUnit: number
  comparePrice: number
  compareLabel: string
  heatPerUnit: number
  requiredWorkshopLevel: number
  upgrade?: HideoutRoomUpgrade | null
}

export type HideoutRoomUpgrade = {
  level: number
  cost: number
  requiredTier: number
  requiredTierName: string
  tierLocked: boolean
  requiredWorkshopLevel: number
  workshopLocked: boolean
  /** Days of this room's own output before the upgrade pays for itself. Null when it makes nothing. */
  paybackDays: number | null
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

/** The rooms you can buy. The still and the mix house were folded into the workshop. */
export type HideoutRoom = 'tier' | 'storage' | 'safe' | 'weedlab' | 'cokelab' | 'workshop' | 'intelligence' | 'lookout'

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
  cityRank: number
  cityPlayers: number
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
  /** Guns of every kind: the coverage number, since one gun covers one thug. */
  weapons: number
  /** And which guns they are, which is what decides a fight. */
  weaponRack: WeaponTier[]
  medicine: number
  /** Doses on the shelf. What it costs you to infest somebody else's house. */
  poison: number
  rides: number
  weed: number
  coke: number
  moonshine: number
  cut: number
  weedSellPrice: number
  cokeSellPrice: number
  cokePurityPercent: number
  cokeSellPriceAtPurity: number
  crewReport: CrewReport
  guidance: Guidance
  hideout: Hideout
  crew: Pimp[]
  fallenCrew: Pimp[]
  combatCrew: CombatCrew
  combatStatus: CombatStatus
  unreadDefenceAlerts: number
  store: StoreItem[]
  attackMethods: AttackMethod[]
  /** Where a shift can be worked, and what each place is for. */
  districts: StreetDistrict[]
  /** The crew, or null when running alone. */
  alliance?: AllianceBrief | null
  recentActivity: Activity[]
}

/** One entry on the attack menu, already priced and gated by the server. */
export type AttackMethod = {
  key: AttackMethodKey
  label: string
  turnCost: number
  description: string
  blockedReason?: string | null
}

export type AttackMethodKey = 'raid' | 'driveby' | 'jack' | 'infest' | 'poach'

/** One shelf of the gun rack, priced and rated by the server. */
export type WeaponTier = {
  key: WeaponTierKey
  label: string
  held: number
  price: number
  /** What carrying one is worth in a fight, in pistols. */
  firepower: number
  /** What making one costs, or null for a gun nobody makes in a back room. */
  forgeCost: number | null
  minWorkshopLevel: number | null
}

export type WeaponTierKey = 'pistols' | 'shotguns' | 'smgs' | 'rifles'

/**
 * The cheapest gun, and the one the street's quick-buy stocks. Named rather than written out
 * wherever it is needed: the supplies panel used to ask the counter for a key called 'weapons',
 * which stopped existing the day guns split into tiers, and nothing said so.
 */
export const cheapestWeapon: WeaponTierKey = 'pistols'

/** The shrine: what the gods want this week, and whether they will hear you. */
export type PrayerBoard = {
  canPray: boolean
  nextPrayerAtUtc?: string | null
  good: string
  label: string
  quantity: number
  approximateValue: number
  held: number
  generousQuantity: number
  blockedReason?: string | null
}

/** One place to work a shift, and what going there is worth. */
export type StreetDistrict = {
  key: string
  name: string
  blurb: string
  isDefault: boolean
  grossPercent: number
  hoeRecruitPercent: number
  thugRecruitPercent: number
  pimpRecruitPercent: number
  findPercent: number
  heatPercent: number
}

/** One crew on the board, worth the sum of what its members are worth. */
export type AllianceSummary = {
  id: number
  name: string
  motto?: string | null
  members: number
  maxMembers: number
  netWorth: number
  duesPercent: number
  offensiveThugs: number
  defensiveThugs: number
  /** How they take people on, and what that means in the words the board shows. */
  door: AllianceDoorKey
  doorLabel: string
  doorDetail: string
  yours: boolean
  youFounded: boolean
  cityControlThugs: number
  controlledCities: AllianceCityControl[]
  rank: number
}

export type AllianceCityControl = {
  city: string
  territories: number
  bonusThugs: number
}

/** Just enough of the crew for the pages that are not about the crew. */
export type AllianceBrief = {
  id: number
  name: string
  offensiveThugs: number
  defensiveThugs: number
  borrowLimit: number
  yourDefenders: number
}

export type AllianceMember = {
  playerId: string
  name: string
  city: string
  netWorth: number
  pimps: number
  hoes: number
  thugs: number
  isFounder: boolean
  isYou: boolean
  rank: string
  rankLabel: string
  /** Whether the viewer stands above them, which is what expelling and promoting need. */
  youOutrankThem: boolean
  defenders: number
  joinedAtUtc?: string | null
}

/** One outstanding ask, from whichever side it came. */
export type AllianceRequest = {
  id: number
  kind: 'Invitation' | 'Application'
  allianceId: number
  allianceName: string
  playerId: string
  playerName: string
  note?: string | null
  yoursToAnswer: boolean
  createdAtUtc: string
}

export type AlliancePact = {
  id: number
  requestingAllianceId: number
  requestingAllianceName: string
  targetAllianceId: number
  targetAllianceName: string
  status: 'Pending' | 'Active' | 'Declined' | 'Canceled'
  yoursToAnswer: boolean
  createdAtUtc: string
}

export type AllianceAssistCall = {
  /** What came home when the ally asked for it back. Zero until they do, and possibly zero after. */
  thugsReturned: number
  pistolsReturned: number
  shotgunsReturned: number
  smgsReturned: number
  riflesReturned: number
  /** Whoever sent the help, and the only person who can take it back. */
  respondedByPlayerId: string | null
  id: number
  combatMissionId: number
  defenderAllianceId: number
  allyAllianceId: number
  attackerName: string
  defenderName: string
  defenderAllianceName: string
  allyAllianceName: string
  missionStatus: string
  status: 'Open' | 'Answered' | 'Closed'
  thugsSent: number
  pistolsSent: number
  shotgunsSent: number
  smgsSent: number
  riflesSent: number
  createdAtUtc: string
}

export type AllianceTransfer = {
  id: number
  fromPlayerName: string
  toPlayerName: string
  item: string
  label: string
  quantity: number
  createdAtUtc: string
}

export type AllianceDoorKey = 'Open' | 'Application' | 'InviteOnly'

/** One of the three ways a crew can take people on. */
export type AllianceDoor = {
  door: AllianceDoorKey
  label: string
  detail: string
}

/** A power, the rank it needs here, and whether the viewer has it. */
export type AlliancePower = {
  power: string
  label: string
  minRank: string
  youHaveIt: boolean
}

export type AllianceBoard = {
  yours?: AllianceSummary | null
  members: AllianceMember[]
  treasury: number
  foundingCost: number
  maxDuesPercent: number
  offensiveThugCost: number
  defensiveThugCost: number
  /** Borrowed thugs you may field, which is the size of your own crew. */
  borrowLimit: number
  yourDefenders: number
  yourRank: string
  powers: AlliancePower[]
  ranks: string[]
  doors: AllianceDoor[]
  requests: AllianceRequest[]
  pacts: AlliancePact[]
  assistCalls: AllianceAssistCall[]
  transfers: AllianceTransfer[]
  board: AllianceSummary[]
}

/** A name somebody earned today, and what earned it. */
export type PlayerTitle = {
  key: string
  title: string
  playerId: string
  playerName: string
  value: number
  detail: string
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
  avatarUrl: string | null
  profileTagline: string | null
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
  /** What the guns actually carried are worth, in pistols. */
  firepower: number
  uncoveredThugs: number
  weaponCoveragePercent: number
  averageMorale: number
  riskBand: string
}

export type CombatStatus = {
  isProtected: boolean
  protectionUntilUtc?: string | null
  isStrikeProtected: boolean
  strikeProtectionUntilUtc?: string | null
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
  avatarUrl: string | null
  profileTagline: string | null
  profilePronouns: string | null
  profileLocation: string | null
  profileAccent: 'Gold' | 'Teal' | 'Rose' | 'Steel'
  publicDiscordUsername: string | null
  city: string
  isBot: boolean
  aiPersonality?: string | null
  rank: number
  netWorth: number
  pimps: number
  hoes: number
  thugs: number
  weapons: number
  /** Names they have earned today. Empty for almost everybody, which is what makes them worth having. */
  titles: string[]
  rides: number
  averageMorale: number
  combatReadiness: CombatReadiness
  combatStatus: CombatStatus
  canMessage: boolean
  messageBlockedReason: string | null
}

/** Preset gradients behind the name on a profile. The stylesheet decides what each one looks like. */
export type ProfileBanner = 'None' | 'Neon' | 'Smoke' | 'Chrome' | 'Rust' | 'Velvet'

export const profileBanners: { key: ProfileBanner; label: string }[] = [
  { key: 'None', label: 'None' },
  { key: 'Neon', label: 'Neon' },
  { key: 'Smoke', label: 'Smoke' },
  { key: 'Chrome', label: 'Chrome' },
  { key: 'Rust', label: 'Rust' },
  { key: 'Velvet', label: 'Velvet' },
]

export type PlayerProfile = PlayerTarget & {
  /** Why each strike cannot be thrown at this person, keyed by method. Absent when it can. */
  strikeBlockers: Record<string, string | undefined>
  profileBanner: ProfileBanner
  /** When they started. Only on the profile somebody opened, never on a leaderboard row. */
  joinedAtUtc: string
  cash: number
  bankCash: number
  /** What they are armed with, not merely how many. A house of rifles is a different fight. */
  weaponRack: WeaponTier[]
  medicine: number
  weed: number
  coke: number
  hoeHappiness: number
  thugHappiness: number
  publicActivity: Activity[]
  /** True when they turned it off, as opposed to having done nothing yet. */
  activityHidden: boolean
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
  method: AttackMethodKey
  methodLabel: string
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
  hoesTaken: number
  ridesTaken: number
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

/**
 * A refusal from the server, carrying the code it came with.
 *
 * Worth the extra type because a caller often needs to tell a refusal from a wobble: the server saying
 * this cannot be read is final and worth acting on, while a request that never arrived is worth trying
 * again in eight seconds. Both used to arrive as the same bare Error.
 */
export class RequestError extends Error {
  constructor(message: string, readonly status: number) {
    super(message)
    this.name = 'RequestError'
  }

  /** The server understood and said no, rather than never having answered at all. */
  get refused() { return this.status >= 400 && this.status < 500 && this.status !== 401 }
}

async function request<T>(url: string, options?: RequestInit): Promise<T> {
  const uploadingForm = options?.body instanceof FormData
  const response = await fetch(url, {
    credentials: 'include',
    headers: uploadingForm
      ? { ...(options?.headers ?? {}) }
      : { 'Content-Type': 'application/json', ...(options?.headers ?? {}) },
    ...options,
  })

  if (!response.ok) {
    let message = response.status === 401 ? 'Unauthorized' : `Request failed (${response.status})`
    try {
      const body = await response.json()
      if (body?.error) message = body.error
    } catch { /* empty */ }
    throw new RequestError(message, response.status)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export type MuleRun = {
  id: number
  destinationCity: string
  good: string
  status: string
  outcome: string
  pimpName: string
  hoes: number
  capacity: number
  cashSent: number
  unitsBought: number
  seizedUnits: number
  cashReturned: number
  bustChancePercent: number
  defectChancePercent: number
  arrivesAtUtc: string
  returnsAtUtc: string
  secondsRemaining: number
  summary: string
}
export type MuleDestination = {
  city: string
  risk: string
  travelTurns: number
  flightMinutes: number
  weedPrice: number
  cokePrice: number
  bustChancePercent: number
}
export type MuleCandidate = {
  id: number
  name: string
  specialty: string
  loyalty: number
  isAway: boolean
  awayReason?: string | null
}
export type MuleQuote = {
  destinationCity: string
  good: string
  hoes: number
  capacity: number
  turns: number
  flightMinutes: number
  tripMinutes: number
  fare: number
  upkeep: number
  cashSent: number
  totalCost: number
  unitPriceThere: number
  unitsAffordable: number
  homePrice: number
  projectedGross: number
  projectedSpend: number
  projectedProfit: number
  bustChancePercent: number
  defectChancePercent: number
}
export type MuleBoard = {
  concurrentRunCap: number
  runsOut: number
  intelligenceLevel: number
  hoesAvailable: number
  maxHoesPerRun: number
  hoeCarryCapacity: number
  destinations: MuleDestination[]
  pimps: MuleCandidate[]
  runs: MuleRun[]
}

/** Which ways in this server can actually offer. Asked before the login box draws its buttons. */
export type AuthProviders = { discord: boolean }

/**
 * The code in flight, described without being given away. Enough to run a clock and count down the
 * guesses; not one digit of the code itself.
 */
export type EmailVerificationState = {
  /** The address it went to, which is not always the address on the account a minute later. */
  sentTo: string
  expiresAtUtc: string
  attemptsRemaining: number
  /** When the resend button comes back. Null when it is already pressable. */
  resendableAtUtc: string | null
}

/** Everything the account page shows. Note what is missing: the password itself, ever. */
export type Account = {
  username: string
  playerName: string
  email: string | null
  /** Unverified, the address cannot be signed in with. That is what the tick is for. */
  emailVerified: boolean
  emailVerifiedAtUtc: string | null
  verification: EmailVerificationState | null
  /**
   * False when no email provider is configured and the message goes to the server log instead of the
   * wire. Surfaced rather than hidden: a code in a log is fine on a laptop and a quiet disaster
   * anywhere else, so the page says which one is happening.
   */
  emailDelivers: boolean
  hasPassword: boolean
  discordConnected: boolean
  discordUsername: string | null
  discordAvatarUrl: string | null
  discordLinkedAtUtc: string | null
  /** When Discord was last asked, which is not when it was connected. Null until it has been. */
  discordSyncedAtUtc: string | null
  avatarSource: 'None' | 'Discord' | 'Custom'
  avatarUrl: string | null
  customAvatarUrl: string | null
  profileTagline: string | null
  profilePronouns: string | null
  profileLocation: string | null
  profileAccent: 'Gold' | 'Teal' | 'Rose' | 'Steel'
  profileBanner: ProfileBanner
  showDiscordOnProfile: boolean
  directMessagePolicy: 'Everyone' | 'Alliance' | 'AllianceAndPacts' | 'Nobody'
  showActivityOnProfile: boolean
  syncDiscordAvatar: boolean
  /** The bell, by category. Separate from the email switches below - a different channel. */
  noticeCombat: boolean
  noticeCrew: boolean
  noticeMarket: boolean
  emailSecurityNotices: boolean
  emailCombatNotices: boolean
  emailAllianceNotices: boolean
  /** False when the server has no Discord credentials, which hides the connect button entirely. */
  discordConfigured: boolean
  createdAtUtc: string
}

/** A Discord login that turned out to belong to nobody yet, waiting on a name and a town. */
export type DiscordSignUpTicket = { suggestedUsername: string, discordUsername: string }

/**
 * What the server said about the trip through Discord, read off the query string on arrival and then
 * wiped from the address bar so a reload does not replay the message.
 */
export type DiscordOutcome =
  | 'signed-in' | 'connected' | 'sign-up' | 'cancelled'
  | 'failed' | 'locked' | 'already-connected' | 'unavailable' | 'synced'

/**
 * A full-page navigation, not a fetch: the browser has to be handed to Discord and handed back, and
 * the origin travels with it so the server knows where to put the player down afterwards - in
 * development that is a port nobody could have written into a config file.
 */
export const discordStartUrl = () =>
  `/api/auth/discord/start?return=${encodeURIComponent(window.location.origin)}`

export const api = {
  cities: () => request<string[]>('/api/auth/cities'),
  providers: () => request<AuthProviders>('/api/auth/providers'),
  /** One name: the server uses it for the sign-in name and the name on the leaderboard alike. */
  register: (username: string, password: string, city: string, email?: string) =>
    request('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, password, city, email: email || null }),
    }),
  /** `identifier` is a username or an email address; the server decides which by the @. */
  login: (identifier: string, password: string) =>
    request('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username: identifier, password }),
    }),
  logout: () => request('/api/auth/logout', { method: 'POST' }),

  /*
    Getting back in without the password.

    Both legs answer the same way whether or not the account exists, so nothing the client does with
    the result can be used to find out - which is the point, and why there is no "we could not find
    that account" to render.
  */
  startPasswordReset: (identifier: string) =>
    request<{ message: string }>('/api/auth/reset/start', {
      method: 'POST',
      body: JSON.stringify({ identifier }),
    }),
  confirmPasswordReset: (identifier: string, code: string, newPassword: string) =>
    request('/api/auth/reset/confirm', {
      method: 'POST',
      body: JSON.stringify({ identifier, code, newPassword }),
    }),

  account: () => request<Account>('/api/account'),
  /** An empty address removes it. The current password is required unless there is not one yet. */
  setEmail: (email: string, currentPassword: string) =>
    request<Account>('/api/account/email', {
      method: 'PUT',
      body: JSON.stringify({ email: email || null, currentPassword }),
    }),
  setPassword: (currentPassword: string, newPassword: string) =>
    request<Account>('/api/account/password', {
      method: 'PUT',
      body: JSON.stringify({ currentPassword, newPassword }),
    }),
  setAvatarSource: (source: Account['avatarSource']) =>
    request<Account>('/api/account/avatar', {
      method: 'PUT',
      body: JSON.stringify({ source }),
    }),
  setProfile: (
    tagline: string,
    pronouns: string,
    location: string,
    accent: Account['profileAccent'],
    banner: ProfileBanner,
  ) =>
    request<Account>('/api/account/profile', {
      method: 'PUT',
      body: JSON.stringify({
        tagline: tagline || null,
        pronouns: pronouns || null,
        location: location || null,
        accent,
        banner,
      }),
    }),
  setPrivacy: (
    showDiscordOnProfile: boolean,
    directMessagePolicy: Account['directMessagePolicy'],
    showActivityOnProfile: boolean,
  ) =>
    request<Account>('/api/account/privacy', {
      method: 'PUT',
      body: JSON.stringify({ showDiscordOnProfile, directMessagePolicy, showActivityOnProfile }),
    }),
  setNotificationPreferences: (
    syncDiscordAvatar: boolean,
    emailSecurityNotices: boolean,
    emailCombatNotices: boolean,
    emailAllianceNotices: boolean,
    noticeCombat: boolean,
    noticeCrew: boolean,
    noticeMarket: boolean) =>
    request<Account>('/api/account/notifications', {
      method: 'PUT',
      body: JSON.stringify({
        syncDiscordAvatar,
        emailSecurityNotices,
        emailCombatNotices,
        emailAllianceNotices,
        noticeCombat,
        noticeCrew,
        noticeMarket,
      }),
    }),
  uploadCustomAvatar: (file: File) => {
    const form = new FormData()
    form.append('avatar', file)
    return request<Account>('/api/account/avatar/custom', { method: 'POST', body: form })
  },
  deleteCustomAvatar: () => request<Account>('/api/account/avatar/custom', { method: 'DELETE' }),
  disconnectDiscord: () => request<Account>('/api/account/discord', { method: 'DELETE' }),
  /** Issues a fresh code and retires whatever was outstanding. Refused inside the resend cooldown. */
  sendEmailCode: () => request<Account>('/api/account/email/verify/send', { method: 'POST' }),
  confirmEmail: (code: string) =>
    request<Account>('/api/account/email/verify', { method: 'POST', body: JSON.stringify({ code }) }),
  /** Ends every session on this account except the one asking. */
  revokeSessions: () => request<Account>('/api/account/sessions/revoke', { method: 'POST' }),

  discordTicket: () => request<DiscordSignUpTicket>('/api/auth/discord/ticket'),
  discardDiscordTicket: () => request('/api/auth/discord/ticket', { method: 'DELETE' }),
  /** `email` is optional and the only way a Discord-made account gets a second way back in. */
  completeDiscordSignUp: (username: string, city: string, email?: string) =>
    request('/api/auth/discord/complete', {
      method: 'POST',
      body: JSON.stringify({ username, city, email: email || null }),
    }),
  dashboard: () => request<Dashboard>('/api/game/dashboard'),
  leaderboard: (city?: string) => request<LeaderboardEntry[]>(
    city ? `/api/game/leaderboard?city=${encodeURIComponent(city)}` : '/api/game/leaderboard'),
  targets: (query = '') => request<PlayerTarget[]>(`/api/game/targets${query ? `?query=${encodeURIComponent(query)}` : ''}`),
  playerProfile: (playerId: string) => request<PlayerProfile>(`/api/game/players/${encodeURIComponent(playerId)}/profile`),
  combatLogs: () => request<CombatLog[]>('/api/game/combat/logs'),
  combatMissions: () => request<CombatMission[]>('/api/game/combat/missions'),
  // Exactly one pimp commands. Pass a commanderPimpId to pick them, or null to let the server field
  // the best Enforcer available.
  attack: (defenderId: string, thugs: number, weapons: number, commanderPimpId: number | null, allianceThugs = 0) =>
    request<ActionResult>('/api/game/combat/attack', {
      method: 'POST',
      body: JSON.stringify({ defenderId, thugs, weapons, commanderPimpId, allianceThugs }),
    }),
  // The four quick strikes share the attack route with the raid: one shape, one endpoint, and the
  // server decides whether the request becomes a travelling mission or settles on the spot. Coke is
  // only read by a poaching run.
  strike: (defenderId: string, method: AttackMethodKey, coke = 0) =>
    request<ActionResult>('/api/game/combat/attack', {
      method: 'POST',
      body: JSON.stringify({ defenderId, method, coke }),
    }),
  cancelCombatMission: (missionId: number) => request<ActionResult>(`/api/game/combat/missions/${missionId}/cancel`, { method: 'POST' }),
  worldNews: () => request<WorldNews>('/api/world/news'),
  titles: () => request<PlayerTitle[]>('/api/world/titles'),
  alliances: () => request<AllianceBoard>('/api/game/alliances'),
  foundAlliance: (name: string, motto: string) =>
    request<ActionResult>('/api/game/alliances', { method: 'POST', body: JSON.stringify({ name, motto }) }),
  joinAlliance: (allianceId: number) =>
    request<ActionResult>('/api/game/alliances/join', { method: 'POST', body: JSON.stringify({ allianceId }) }),
  leaveAlliance: () => request<ActionResult>('/api/game/alliances/leave', { method: 'POST' }),
  expelMember: (memberId: string) =>
    request<ActionResult>('/api/game/alliances/expel', { method: 'POST', body: JSON.stringify({ memberId }) }),
  updateAlliance: (settings: { duesPercent?: number, door?: AllianceDoorKey, motto?: string, powers?: Record<string, string> }) =>
    request<ActionResult>('/api/game/alliances', { method: 'PUT', body: JSON.stringify(settings) }),
  setAllianceRank: (memberId: string, rank: string) =>
    request<ActionResult>('/api/game/alliances/rank', { method: 'POST', body: JSON.stringify({ memberId, rank }) }),
  handOverAlliance: (memberId: string) =>
    request<ActionResult>('/api/game/alliances/hand-over', { method: 'POST', body: JSON.stringify({ memberId }) }),
  invitePlayer: (playerId: string, note = '') =>
    request<ActionResult>('/api/game/alliances/invite', { method: 'POST', body: JSON.stringify({ playerId, note }) }),
  applyToAlliance: (allianceId: number, note = '') =>
    request<ActionResult>('/api/game/alliances/apply', { method: 'POST', body: JSON.stringify({ allianceId, note }) }),
  answerAllianceRequest: (requestId: number, accept: boolean) =>
    request<ActionResult>('/api/game/alliances/answer', { method: 'POST', body: JSON.stringify({ requestId, accept }) }),
  withdrawAllianceRequest: (requestId: number) =>
    request<ActionResult>('/api/game/alliances/withdraw', { method: 'POST', body: JSON.stringify({ requestId, accept: false }) }),
  sendAllianceResource: (memberId: string, item: string, quantity: number) =>
    request<ActionResult>('/api/game/alliances/transfer', { method: 'POST', body: JSON.stringify({ memberId, item, quantity }) }),
  requestAlliancePact: (allianceId: number) =>
    request<ActionResult>('/api/game/alliances/pacts', { method: 'POST', body: JSON.stringify({ allianceId }) }),
  answerAlliancePact: (pactId: number, accept: boolean) =>
    request<ActionResult>('/api/game/alliances/pacts/answer', { method: 'POST', body: JSON.stringify({ pactId, accept }) }),
  cancelAlliancePact: (pactId: number) =>
    request<ActionResult>('/api/game/alliances/pacts/cancel', { method: 'POST', body: JSON.stringify({ pactId, accept: false }) }),
  /** Takes back whatever is left of what you sent, once the fight it was sent to is over. */
  recallAllianceAssist: (assistCallId: number) =>
    request<ActionResult>('/api/game/alliances/assist/recall', { method: 'POST', body: JSON.stringify({ assistCallId }) }),
  answerAllianceAssist: (assistCallId: number, thugs: number, pistols: number, shotguns: number, smgs: number, rifles: number) =>
    request<ActionResult>('/api/game/alliances/assist', { method: 'POST', body: JSON.stringify({ assistCallId, thugs, pistols, shotguns, smgs, rifles }) }),
  buyAllianceThugs: (kind: 'offensive' | 'defensive', quantity: number) =>
    request<ActionResult>('/api/game/alliances/thugs', { method: 'POST', body: JSON.stringify({ kind, quantity }) }),
  // Negative sends them back to the pool.
  postDefenders: (quantity: number) =>
    request<ActionResult>('/api/game/alliances/defenders', { method: 'POST', body: JSON.stringify({ quantity }) }),
  prayer: () => request<PrayerBoard>('/api/game/prayer'),
  pray: (offered: number) =>
    request<ActionResult>('/api/game/prayer', { method: 'POST', body: JSON.stringify({ offered }) }),
  territories: () => request<TerritoryBoard>('/api/game/territories'),
  market: () => request<MarketBoard>('/api/game/market'),
  listOnMarket: (item: string, quantity: number, pricePerUnit: number) =>
    request<ActionResult>('/api/game/market/list', { method: 'POST', body: JSON.stringify({ item, quantity, pricePerUnit }) }),
  buyOnMarket: (listingId: number, quantity: number) =>
    request<ActionResult>('/api/game/market/buy', { method: 'POST', body: JSON.stringify({ listingId, quantity }) }),
  cancelListing: (listingId: number) =>
    request<ActionResult>('/api/game/market/cancel', { method: 'POST', body: JSON.stringify({ listingId }) }),
  // Null weapon asks the workshop for the best gun it can manage, which is what every caller wanted
  // back when it could only make one kind.
  forge: (turns: number, station: string, weapon?: WeaponTierKey) =>
    request<ActionResult>('/api/game/workshop/forge', { method: 'POST', body: JSON.stringify({ turns, station, weapon }) }),
  travel: (city: string) => request<ActionResult>('/api/game/travel', { method: 'POST', body: JSON.stringify({ city }) }),
  cutCoke: (turns: number) =>
    request<ActionResult>('/api/game/cut', { method: 'POST', body: JSON.stringify({ turns, product: 'coke' }) }),
  mules: () => request<MuleBoard>('/api/game/mules'),
  contracts: () => request<ContractBoard>('/api/game/contracts'),
  chat: (channel: ChatChannelKey) =>
    request<ChatBoard>(`/api/game/chat?channel=${encodeURIComponent(channel)}`),
  say: (channel: ChatChannelKey, body: string) =>
    request<ChatMessage>('/api/game/chat', { method: 'POST', body: JSON.stringify({ channel, body }) }),
  conversations: () => request<ChatConversationList>('/api/game/chat/conversations'),
  conversation: (id: number) => request<ChatConversation>(`/api/game/chat/conversations/${id}`),
  openDirect: (playerId: string) =>
    request<{ id: number }>('/api/game/chat/conversations/direct', { method: 'POST', body: JSON.stringify({ playerId }) }),
  startGroup: (playerIds: string[], title: string) =>
    request<{ id: number }>('/api/game/chat/conversations/group', { method: 'POST', body: JSON.stringify({ playerIds, title }) }),
  sayIn: (id: number, body: string) =>
    request<ChatMessage>(`/api/game/chat/conversations/${id}/say`, { method: 'POST', body: JSON.stringify({ body }) }),
  findPeople: (q: string) => request<PeopleSearch>(`/api/game/chat/people?q=${encodeURIComponent(q)}`),
  blocked: () => request<BlockedList>('/api/game/chat/blocked'),
  block: (playerId: string) =>
    request<{ blocked: boolean }>('/api/game/chat/block', { method: 'POST', body: JSON.stringify({ playerId }) }),
  unblock: (playerId: string) =>
    request<{ blocked: boolean }>('/api/game/chat/unblock', { method: 'POST', body: JSON.stringify({ playerId }) }),
  /** Hands over part of an order, or as much as will go when no amount is given. */
  fillContract: (id: number, quantity?: number) =>
    request<ActionResult>(`/api/game/contracts/${id}/fill`, {
      method: 'POST',
      body: JSON.stringify({ quantity: quantity ?? null }),
    }),
  muleQuote: (city: string, good: string, hoes: number, cash: number) =>
    request<MuleQuote>('/api/game/mules/quote', { method: 'POST', body: JSON.stringify({ city, good, hoes, cash }) }),
  launchMule: (city: string, good: string, hoes: number, cash: number, pimpId: number) =>
    request<ActionResult>('/api/game/mules/launch', { method: 'POST', body: JSON.stringify({ city, good, hoes, cash, pimpId }) }),
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
  // Null district works the neutral one, which is what this call meant before there was anywhere to
  // choose between.
  workStreet: (turns: number, autoBuySupplies = false, district?: string) => request<ActionResult>('/api/game/street', {
    method: 'POST',
    body: JSON.stringify({ turns, autoBuySupplies, district }),
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
  sellStoreItem: (itemKey: string, quantity: number) => request<ActionResult>('/api/game/store/sell', {
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
  /** What a moderator needs to connect a returning account to the one they banned. */
  email: string | null
  emailVerified: boolean
  discordUsername: string | null
  /** The snowflake, not the handle: a handle is renamed in a second, this is not. */
  discordUserId: string | null
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
  medicine: number
  rides: number
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
  maxGarrisonThugs: number
  maxRaidThugs: number
  claimTurnCost: number
  freeThugs: number
  effects: {
    streetIncomePercent: number
    productionYieldPercent: number
    moraleRecoveryPercent: number
    lootPercent: number
  }
  allianceCityControl?: AllianceCityControl | null
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
  isInSession: boolean
  sessionActionsLeft: number
  nextSessionAtUtc?: string | null
  habits: string
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
