import type React from 'react'
import type { FormEvent } from 'react'
import type { ActionResult, AdminOverview, AttackMethodKey, CombatLog, CombatMission, CrewReport, Dashboard,
  LeaderboardEntry, PlayerProfile, PlayerTarget, WorldNews } from './api'

/*
  Where a page is, what one is handed, and how anything gets sent to another.

  Split out of main.tsx so a page module can say what it needs without importing the shell that
  renders it - which would be a cycle, and is what kept all of this in one file.
*/
export type AppPage = 'overview' | 'street' | 'crew' | 'market' | 'casino' | 'recon' | 'seasons' | 'updates' | 'alliance' | 'account' | 'admin'

/**
 * The pages that keep a permanent slot in the phone's bottom bar. A tab bar stops being navigation
 * somewhere around five items - past that the targets get too narrow to hit and the labels too short
 * to read - so the rest live behind More. These four are the loop the ladder itself teaches: work the
 * streets, staff the crew, sell what you made, and a home to see it from.
 */
export const primaryPages: AppPage[] = ['overview', 'street', 'crew', 'market']

export const pageMeta: Record<AppPage, { label: string, short: string, kicker: string }> = {
  overview: { label: 'Overview', short: 'OV', kicker: 'Command centre' },
  street: { label: 'Street', short: 'ST', kicker: 'Turns and cash' },
  crew: { label: 'Crew', short: 'CR', kicker: 'Morale, rooms and craft' },
  market: { label: 'Business', short: 'BZ', kicker: 'Shop, market and runs' },
  casino: { label: 'Casino', short: 'CA', kicker: 'Slots and house money' },
  recon: { label: 'Raids & Map', short: 'RM', kicker: 'Targets and territory' },
  seasons: { label: 'Seasons', short: 'SN', kicker: 'The clock and the record' },
  updates: { label: 'Updates', short: 'UP', kicker: 'Patch notes and events' },
  alliance: { label: 'Alliance', short: 'AL', kicker: 'Who you run with' },
  account: { label: 'Account', short: 'AC', kicker: 'How you get in' },
  admin: { label: 'Admin', short: 'AD', kicker: 'Control centre' },
}

/**
 * Somewhere to send a player: a page, the tab on it, and the panel on that.
 *
 * All three, because all three are the address. A page was never enough, a tab is not either: the
 * Business page is four screens tall and the crew page longer, so "we took you to the right tab" can
 * still mean the thing you were sent for is off the bottom of the screen with nothing pointing at it.
 */
export type GoTo = (page: AppPage, tab?: string, area?: string) => void

/**
 * Turns a name written elsewhere into somewhere to go.
 *
 * The server names a thing rather than a screen. Guidance says "hideout" when it wants a room upgraded
 * and "bank" when it wants cash put away, and an announcement's action link is a path somebody typed
 * into an admin form. None of those know which page a section lives on, or should have to: this is the
 * single place that does, and the only thing that has to move when a section does.
 *
 * Which is what makes it worth reading as a list. Every row is a promise that a name means a place, and
 * two of them have already been quietly broken by things moving underneath: "sell product" pointed at
 * Business for as long as the panel has existed, and selling has not been on Business since the bench
 * took it over - so the one move the game makes when your store is full sent people to a page with no
 * way to sell anything on it.
 */
export function flowTarget(name: string): { page: AppPage, tab?: string, area?: string } {
  // The crew, and the three things you do to it.
  if (name === 'crew') return { page: 'crew', tab: 'roster', area: 'crew' }
  if (name === 'crew-hiring') return { page: 'crew', tab: 'roster', area: 'crew-hiring' }
  if (name === 'arrests') return { page: 'crew', tab: 'roster', area: 'arrests' }

  // The building. Rooms are what "hideout" has always meant; recovery is a room's other use.
  if (name === 'hideout') return { page: 'crew', tab: 'hideout', area: 'rooms' }
  if (name === 'recovery') return { page: 'crew', tab: 'hideout', area: 'recovery' }

  // The bench, which makes, produces and sells. Three verbs on one panel, so one destination.
  if (name === 'production') return { page: 'crew', tab: 'production', area: 'craft-queue' }

  // The counter and the money.
  if (name === 'store') return { page: 'market', tab: 'trade', area: 'store' }
  if (name === 'standing') return { page: 'market', tab: 'trade', area: 'standing' }
  if (name === 'bank') return { page: 'market', tab: 'trade', area: 'bank' }
  if (name === 'market') return { page: 'market', tab: 'trade' }
  if (name === 'flea') return { page: 'market', tab: 'flea' }
  if (name === 'mules') return { page: 'market', tab: 'routes' }
  if (name === 'casino') return { page: 'casino' }

  if (name === 'street') return { page: 'street', area: 'street-action' }
  if (name === 'supplies') return { page: 'street', area: 'supplies' }
  if (name === 'territory') return { page: 'recon', tab: 'ground' }
  if (name === 'patch-notes' || name === 'news') return { page: 'updates' }
  return { page: name in pageMeta ? name as AppPage : 'overview' }
}

/** The same answer for the places that only need the page, like the callout deciding it is on it. */
export function flowPage(name: string): AppPage {
  return flowTarget(name).page
}

/** Sends somebody at a name. The one call every "take me there" button should be making. */
export function goToFlow(onPage: GoTo, name: string): void {
  const { page, tab, area } = flowTarget(name)
  onPage(page, tab, area)
}

/**
 * Navigation for a phone.
 *
 * The desktop rail collapsed to a horizontal strip of two-letter codes that scrolled sideways, which
 * failed twice over: destinations sat off the edge with nothing to say they were
 * there, and the six you could see were abbreviations you had to learn. A thumb also reaches the
 * bottom of a phone far more easily than the top, which is where the strip was.
 *
 * So: a fixed bottom bar of four named destinations plus More, and a sheet for the rest. Every
 * destination keeps its word, nothing hides off an edge, and the sheet closes on pick, on backdrop,
 * and on Escape.
 */

/**
 * What a large purchase can actually be paid out of, which mirrors Capital.Available on the server.
 *
 * The bank always, cash always, and the safe only while the player is standing in front of it - money
 * locked in a safe in New York is worth nothing at all to somebody in Las Vegas, and a page that
 * counted it would quote a total the server will refuse.
 */
export function spendable(dashboard: Dashboard) {
  return dashboard.cash + dashboard.bankCash + (dashboard.hideout.atHideout ? dashboard.hideout.safeCash : 0)
}

/**
 * What an action needs re-read once it has been taken.
 *
 * "full" is everything on the screen and costs seven requests. "dashboard" is the money, the turns
 * and the clock, which is all most actions actually move. "none" is for a caller that wants to say
 * when itself - the slots hold the numbers back until the reels have landed, because a balance that
 * updates before the last reel stops has told the player the answer.
 */
export type RefreshScope = 'full' | 'dashboard' | 'none'

/** Why a hideout action cannot be taken from here, or false when it can. */
export function awayFromHideout(dashboard: Dashboard, what: string) {
  return !dashboard.hideout.atHideout
    && `Your hideout is in ${dashboard.hideout.city} and you are in ${dashboard.city}. ${what} needs you there.`
}

export type PageContext = {
  /** Opens the walkthrough at its first step. A shell control, like setActivePage beside it. */
  openTour: () => void
  dashboard: Dashboard
  adminOverview: AdminOverview | null
  leaders: LeaderboardEntry[]
  cityLeaders: LeaderboardEntry[]
  targets: PlayerTarget[]
  selectedTarget: PlayerProfile | null
  worldNews: WorldNews
  combatLogs: CombatLog[]
  combatMissions: CombatMission[]
  targetQuery: string
  busy: boolean
  streetTurns: number
  autoBuySupplies: boolean
  productionTurns: number
  hoeCut: number
  bankAmount: number
  crewQty: Record<'pimps' | 'hoes' | 'thugs', number>
  attackCrew: { thugs: number, weapons: number }
  commanderId: number | null
  attackMethod: AttackMethodKey
  poachCoke: number
  borrowedThugs: number
  district: string
  storeQty: Record<string, number>
  sellQty: Record<'weed' | 'coke', number>
  nextTurn: string
  totalCrew: number
  weaponCoverage: number
  managementCapacity: number
  setActivePage: GoTo
  refresh: () => Promise<void>
  refreshDashboard: () => Promise<void>
  setTargetQuery: (query: string) => void
  setStreetTurns: (turns: number) => void
  setAutoBuySupplies: (enabled: boolean) => void
  setProductionTurns: (turns: number) => void
  setHoeCut: (cut: number) => void
  setBankAmount: (amount: number) => void
  setCrewQty: React.Dispatch<React.SetStateAction<Record<'pimps' | 'hoes' | 'thugs', number>>>
  setAttackCrew: React.Dispatch<React.SetStateAction<{ thugs: number, weapons: number }>>
  setAttackMethod: (method: AttackMethodKey) => void
  setPoachCoke: (coke: number) => void
  setBorrowedThugs: (thugs: number) => void
  setDistrict: (district: string) => void
  setCommanderId: (id: number | null) => void
  setStoreQty: React.Dispatch<React.SetStateAction<Record<string, number>>>
  setSellQty: React.Dispatch<React.SetStateAction<Record<'weed' | 'coke', number>>>
  act: (fn: () => Promise<ActionResult | unknown>, after?: RefreshScope) => Promise<void>
  searchTargets: (event: FormEvent<HTMLFormElement>) => void
  inspectTarget: (playerId: string) => void
  attackTarget: (defenderId: string) => void
  cancelMission: (missionId: number) => void
  seedBots: (count: number) => void
  runBots: (rounds: number) => void
  setBotAutomation: (enabled: boolean, timing?: { tickSeconds?: number, roundsPerTick?: number, resetTiming?: boolean }) => void
}
