import React, { FormEvent, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { adminApi, api, cheapestWeapon, configApi, discordStartUrl, opsApi, RequestError } from './api'
import { applyPreferences, loadPreferences, savePreferences, systemPrefersReducedMotion, watchSystemMotion, type Preferences } from './preferences'
import { onRouteChange, routePage, routeTab, writeRoute } from './route'
import { profileBanners, type ProfileBanner } from './api'
import type { ArrestBoard, PlayerSession, Account, AccountInviteKey, AuthProviders, DiscordOutcome, DiscordSignUpTicket, DiscordIntegrationSettings, DiscordCrewChannelSyncResult, DiscordRoleSyncResult, BlockedList, ChatBoard, ChatChannelKey, ChatConversation, ChatConversationList, Person, ActionResult, AdminAuditEntry, AdminBetaKey, Alert, AdminConfig, AdminConfigEntry, AdminCustomTitle, AdminCustomTitleDraft, CustomTitleCriteria, AdminGameAnnouncement, AdminGameAnnouncementDraft, AnnouncementDeliverySettings, AdminOverview, AdminBotHealth, AdminOversight, AdminPlayerDetail, AdminPlayerSummary, AllianceAssistCall, AllianceBoard, AllianceBrief, AllianceDoorKey, AllianceMember, AlliancePact, AlliancePower, AllianceRequest, AllianceSummary, AllianceTransfer, AttackMethod, AttackMethodKey, PrayerBoard, PlayerTitle, StreetDistrict, WeaponTier, WeaponTierKey, CombatLog, CombatMission, Dashboard, CrewReport, GameAnnouncement, GameUpdates, BreakableRoom, HideoutDamage, HideoutRepair, HideoutRoom, HideoutRoomUpgrade, LeaderboardEntry, LiveOps, Pimp, BotDirective, MoraleDirection, MoraleTrend, MarketBoard, MuleBoard, MuleQuote, TraderJobBoard, CasinoBoard, CasinoMachine, CasinoTransaction, ClaimedComp, CompReward, SlotSpin, PlayerProfile, PlayerTarget, TerritoryBoard, Season, SeasonArchiveEntry, SeasonStanding, SeasonTable, TravelStatus, WorldNews, WorldNewsEntry, CatchUp, CityMarket, PublicStats } from './api'
import './styles/main.scss'
/*
  Bootstrap's JavaScript. Imported as a namespace rather than for a side effect, for two reasons:
  the ES module build is the one Vite can tree-shake and Popper comes along with it, and importing
  it by name gives something to hand to `window`.

  Loading it registers the data-attribute API, which is the part that matters here: a control marked
  data-bs-toggle finds its own behaviour without anything constructing it. The overlays this game
  already had - the walkthrough, the chat dock, the catch-up dialog - stay driven by React state,
  because their visibility is state the rest of the app reads, and a plugin that wants to own show
  and hide would be a second source of truth for it.
*/
import * as bootstrap from 'bootstrap'
/*
  Bootstrap's own icon set, as a stylesheet of one class per glyph. Pulled in for the alerts bell and
  available to anything else that wants a mark rather than a word.
*/
import 'bootstrap-icons/font/bootstrap-icons.css'

// Bootstrap's own docs assume a <script> tag and therefore a global. Keeping one means a plugin can
// be reached from the console when debugging, and that any future code can construct one the way
// every Bootstrap example does.
declare global {
  interface Window { bootstrap: typeof bootstrap }
}
window.bootstrap = bootstrap

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('en-US')

function signedMoney(value: number) {
  return `${value >= 0 ? '+' : '-'}${money.format(Math.abs(value))}`
}

/**
 * The faces on a reel, drawn rather than abbreviated.
 *
 * They used to be two-letter codes - LR for the Low-Rider, CH for the Gold Chain, K for the Crew
 * Crown - which is a legend a player has to learn before the machine means anything, and the reel is
 * the one part of a slot machine that has to be readable at a glance. It also made the paytable read
 * "LR Low-Rider", teaching the legend in the one place the full name was already printed.
 *
 * Matched on the label rather than the key because that is what both callers are holding, and loosely
 * because the idle animation runs its own names past this that the server never sends.
 *
 * Solid shapes in one colour. A face is about forty pixels across inside a gold disc, and anything
 * finer than this is gone at that size.
 */
function slotGlyph(symbol: string) {
  const name = symbol.toLowerCase()

  // -- Neon Fortune ---------------------------------------------------------------------------------
  if (name.includes('cherry')) return <>
    <circle cx="8" cy="17" r="4.1" />
    <circle cx="17.1" cy="18.4" r="3.4" />
    <path d="M8 12.6c1.1-5 4.2-8 9.2-9.4M17.1 14.7c-1-3.9 0-6.9 2.1-9.4"
      fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" />
  </>

  if (name.includes('bell')) return <>
    <path d="M12 2.6c.9 0 1.6.7 1.6 1.6v.6a6.6 6.6 0 0 1 4.5 6.3v3.9l1.7 2.2a.8.8 0 0 1-.6 1.3H4.8a.8.8 0 0 1-.6-1.3l1.7-2.2v-3.9a6.6 6.6 0 0 1 4.5-6.3v-.6c0-.9.7-1.6 1.6-1.6z" />
    <circle cx="12" cy="20.5" r="1.9" />
  </>

  if (name.includes('champagne')) return <path d="M3.4 3.6h17.2l-7.4 8.6v5.9h4.3v2.3H6.5v-2.3h4.3v-5.9z" />

  if (name.includes('dice')) return <>
    <rect x="3.4" y="3.4" width="17.2" height="17.2" rx="3.4" fill="none" stroke="currentColor" strokeWidth="2.2" />
    <circle cx="8.4" cy="8.4" r="1.7" /><circle cx="12" cy="12" r="1.7" /><circle cx="15.6" cy="15.6" r="1.7" />
  </>

  if (name.includes('diamond')) return <path d="M12 2.4l9.2 9.6-9.2 9.6L2.8 12z" />

  // -- Kingpin --------------------------------------------------------------------------------------
  if (name.includes('chip')) return <>
    <circle cx="12" cy="12" r="8.9" fill="none" stroke="currentColor" strokeWidth="2.4" />
    <circle cx="12" cy="12" r="3.5" />
    <path d="M12 2.4v3.2M12 18.4v3.2M2.4 12h3.2M18.4 12h3.2"
      stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
  </>

  if (name.includes('whiskey')) return <>
    <path d="M5.2 3.6h13.6l-1.4 15.6a1.7 1.7 0 0 1-1.7 1.5H8.3a1.7 1.7 0 0 1-1.7-1.5z"
      fill="none" stroke="currentColor" strokeWidth="2.2" />
    <path d="M6.6 11.4h10.8l-.7 7.9a.6.6 0 0 1-.6.5H7.9a.6.6 0 0 1-.6-.5z" />
  </>

  if (name.includes('cigar')) return <>
    <rect x="1.8" y="12.2" width="17.4" height="4.6" rx="2.3" transform="rotate(-20 10.5 14.5)" />
    <circle cx="20.3" cy="8.2" r="2.1" />
  </>

  if (name.includes('watch')) return <>
    <circle cx="12" cy="14.2" r="7.2" fill="none" stroke="currentColor" strokeWidth="2.2" />
    <path d="M12 10.4v4l2.7 1.7" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    <rect x="10.4" y="3.4" width="3.2" height="2.6" rx=".8" />
    <circle cx="12" cy="2.4" r="1.6" fill="none" stroke="currentColor" strokeWidth="1.7" />
  </>

  if (name.includes('ring')) return <>
    <circle cx="12" cy="15.4" r="6.1" fill="none" stroke="currentColor" strokeWidth="2.4" />
    <path d="M12 2.2l3.5 4.3-3.5 3.5-3.5-3.5z" />
  </>

  // -- The Vault ------------------------------------------------------------------------------------
  if (name.includes('ledger')) return <>
    <path d="M5 4.6a1.8 1.8 0 0 1 1.8-1.8h12.6v18.4H6.8A1.8 1.8 0 0 1 5 19.4z"
      fill="none" stroke="currentColor" strokeWidth="2.2" />
    <path d="M9.2 2.8v18.4" fill="none" stroke="currentColor" strokeWidth="2.2" />
  </>

  // Before the Vault itself: the Vault room's key is called a Vault Key, and it would
  // otherwise be drawn as a safe.
  if (name.includes('key')) return <>
    <circle cx="7.6" cy="7.9" r="4.6" fill="none" stroke="currentColor" strokeWidth="2.4" />
    <path d="M10.8 11.2l9.4 9.4M16.6 17l2.3-2.3M13.7 14.1l2.3-2.3"
      fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
  </>

  if (name.includes('bar')) return <>
    <path d="M7 8.4l1.5-2.6h7l1.5 2.6z" />
    <path d="M6.2 10h11.6l3.2 8.4H3z" />
  </>

  if (name.includes('spade')) return <path d="M12 2.4S3.6 8.8 3.6 13.2a4.3 4.3 0 0 0 7 3.4l-1.5 5h5.8l-1.5-5a4.3 4.3 0 0 0 7-3.4C20.4 8.8 12 2.4 12 2.4z" />

  if (name.includes('skull')) return <>
    <path d="M12 2.8c-4.9 0-8.3 3.3-8.3 7.9 0 2.6 1.2 4.5 2.8 5.7v2.9a1.7 1.7 0 0 0 1.7 1.7h7.6a1.7 1.7 0 0 0 1.7-1.7v-2.9c1.6-1.2 2.8-3.1 2.8-5.7 0-4.6-3.4-7.9-8.3-7.9z"
      fill="none" stroke="currentColor" strokeWidth="2.1" />
    <circle cx="8.9" cy="10.6" r="1.9" /><circle cx="15.1" cy="10.6" r="1.9" />
    <path d="M10.2 18.2v2.8M13.8 18.2v2.8" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
  </>

  // -- Sidewalk Slots, and the two faces every room shares ------------------------------------------
  if (name.includes('cash')) return <>
    <rect x="2.5" y="15" width="19" height="4.6" rx="1.3" />
    <rect x="1.5" y="9.4" width="21" height="4.6" rx="1.3" />
    <rect x="4" y="3.8" width="16" height="4.6" rx="1.3" />
  </>

  if (name.includes('chain')) return <g fill="none" stroke="currentColor" strokeWidth="2.6">
    <ellipse cx="8.6" cy="15.4" rx="3.6" ry="5.6" transform="rotate(-45 8.6 15.4)" />
    <ellipse cx="15.4" cy="8.6" rx="3.6" ry="5.6" transform="rotate(-45 15.4 8.6)" />
  </g>

  // Three shapes rather than one outline: a receiver, a barrel off its front, and a grip hung from
  // the back of it. Drawn as one path this came out a T - a bar of even weight with a grip under the
  // middle of it - and what makes the shape read as a gun is the grip being at one end and the barrel
  // running out of the other.
  if (name.includes('pistol')) return <>
    <rect x="2.6" y="5.2" width="10.8" height="6.4" rx="1" />
    <rect x="12.4" y="6.6" width="9" height="3.6" rx="1" />
    <path d="M2.8 10.6h5.6L6.6 20.4H2z" />
  </>

  if (name.includes('rider')) return <>
    <path d="M2 15.2c0-.6.4-1.1 1-1.2l2.3-.4 2.5-3.3c.4-.5 1-.8 1.7-.8h5.6c.7 0 1.3.3 1.7.8l2.4 3.4 1.3.3c.6.1 1 .6 1 1.2v1.6c0 .5-.3.8-.8.8H2.8c-.5 0-.8-.3-.8-.8z" />
    <circle cx="7" cy="17.8" r="2.3" />
    <circle cx="17" cy="17.8" r="2.3" />
  </>

  if (name.includes('crown')) return <path d="M2.6 7.4l3.7 3.8L12 4.4l5.7 6.8 3.7-3.8-1.9 10.2c-.1.6-.7 1.1-1.3 1.1H5.8c-.6 0-1.2-.5-1.3-1.1z" />

  if (name.includes('seven')) return <path d="M5.6 4.2h12.8v3.3L11.4 19.8H6.7L13.8 8H5.6z" />

  if (name.includes('vault')) return <>
    <rect x="2.6" y="3.4" width="18.8" height="17.2" rx="2.6" fill="none" stroke="currentColor" strokeWidth="2.3" />
    <circle cx="12" cy="12" r="3.9" fill="none" stroke="currentColor" strokeWidth="2.3" />
    <path d="M12 5.6v1.7M12 16.7v1.7M5.6 12h1.7M16.7 12h1.7" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" />
  </>

  return <circle cx="12" cy="12" r="5.5" />
}

/**
 * Always hidden from a screen reader. Both callers print the symbol's name in text beside it, so
 * announcing it here would say everything twice.
 */
function SlotGlyph({ symbol, className }: { symbol: string, className?: string }) {
  return <svg className={className} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
    {slotGlyph(symbol)}
  </svg>
}

/** How long every reel turns before the first of them is allowed to stop. */
const slotSpinDurationMs = 900
/** The gap between the first reel stopping and the second. */
const slotReelStopMs = 380
/**
 * How much longer each gap is than the one before it.
 *
 * The reels come down 380, 440, 500 and 560 milliseconds apart, so the machine takes its time over
 * the reels that are still live. The last one is the only one that can still change the answer and
 * it should be the one you wait longest on.
 */
const slotReelStopRampMs = 60

/**
 * The same landing for somebody who has asked the game to stop moving.
 *
 * Under reduced motion the reel strip does not animate at all, so every one of those milliseconds is
 * a still grid and a wait for nothing. The reels still come down in order, because the order is
 * information rather than decoration, but they do it fast enough not to be a delay.
 */
function slotReelTiming() {
  const reduced = document.documentElement.getAttribute('data-motion') === 'reduced'
  return reduced
    ? { hold: 120, gap: 60, ramp: 0 }
    : { hold: slotSpinDurationMs, gap: slotReelStopMs, ramp: slotReelStopRampMs }
}
const slotColumns = 5
const slotRows = 3
const slotGridSize = slotColumns * slotRows

/**
 * What a reel shows while it is turning, and what the grid shows before it ever has.
 *
 * Both take the faces of the machine being played rather than one set for the whole floor, because
 * the rooms no longer share a reel: a Vault that idled on Low-Riders would be showing symbols that
 * are not on it and cannot come up.
 */
function slotReelSymbols(reel: number, faces: string[]) {
  return Array.from({ length: 12 }, (_, index) => faces[(index + reel * 2) % faces.length])
}

function slotGridSymbols(faces: string[], symbols?: string[]) {
  const base = symbols && symbols.length >= slotGridSize ? symbols.slice(0, slotGridSize) : faces
  return Array.from({ length: slotGridSize }, (_, index) => base[index % base.length])
}

function slotGridText(faces: string[], symbols: string[]) {
  const grid = slotGridSymbols(faces, symbols)
  return Array.from({ length: slotRows }, (_, row) =>
    grid.slice(row * slotColumns, row * slotColumns + slotColumns).join(' / ')).join(' | ')
}

/**
 * What a pull is called, and the colour it is said in.
 *
 * The name used to be decided by the net alone, so a spin that paid a lane and still came back under
 * the stake was announced as "No hit" while the reels were drawing a line through the winners. That
 * is the most common non-losing result on five lanes by a distance - paying out less than the stake
 * is most of what a slot machine does - so it is the one that most needed a word of its own.
 */
function spinVerdict(transaction: CasinoTransaction) {
  if (transaction.jackpotAmount > 0) return { label: 'The pot', tone: 'text-warning', edge: 'border-warning casino-jackpot' }
  if (transaction.jackpot) return { label: 'Top award', tone: 'text-warning', edge: 'border-warning casino-jackpot' }
  if (transaction.netResult > 0) return { label: 'Paid out', tone: 'text-success', edge: 'border-success' }
  if (transaction.netResult === 0) return { label: 'Broke even', tone: 'text-body-secondary', edge: 'border-secondary' }
  // Paid something, still down on the pull. The reels are highlighting a winning lane while this says
  // so, which is the whole reason it is not called a miss.
  if (transaction.payoutAmount > 0) return { label: 'Short', tone: 'text-body-secondary', edge: 'border-secondary' }
  return { label: 'No hit', tone: 'text-body-secondary', edge: 'border-secondary' }
}

/**
 * Where a winning line runs, in the overlay's own coordinates.
 *
 * The overlay is inset to span symbol-centre to symbol-centre rather than covering the whole grid, so
 * against its 2x2 viewBox a cell's column and row index *are* its coordinates. It used to emit cell
 * centres - index plus a half - against a 3x3 viewBox covering everything, which is only the same
 * thing if a cell is all symbol, and every cell carries a label under its symbol.
 */
function slotPaylinePoints(cells: number[]) {
  return cells.map(cell => `${cell % slotColumns},${Math.floor(cell / slotColumns)}`).join(' ')
}

function wait(ms: number) {
  return new Promise(resolve => window.setTimeout(resolve, ms))
}

async function copyToClipboard(value: string) {
  if (navigator.clipboard) {
    await navigator.clipboard.writeText(value)
    return
  }

  const area = document.createElement('textarea')
  area.value = value
  area.setAttribute('readonly', '')
  area.style.position = 'fixed'
  area.style.left = '-9999px'
  document.body.appendChild(area)
  area.select()
  document.execCommand('copy')
  document.body.removeChild(area)
}

function compactDateTime(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' }) : 'Never'
}

/** Three states and no fourth: a key is waiting, spent, or taken back. It never goes off on its own. */
function betaKeyStatusClass(status: AccountInviteKey['status'] | AdminBetaKey['status']) {
  return status === 'Available'
    ? 'text-bg-success'
    : status === 'Used'
      ? 'text-bg-primary'
      : 'text-bg-secondary'
}

/*
  Why a button will not go.

  A greyed-out control is a question the player is already asking - why not? - and the answer is
  nearly always something the screen in front of them knows: you are eighty short, the crew is out,
  the shift is longer than the turns left in the day. The `disabled` attribute cannot give that
  answer. A disabled button takes no focus, and Bootstrap puts pointer-events: none on top of that,
  so hovering one lands on the panel behind it and the player is left to work it out from the
  numbers.

  So a button that cannot be pressed is blocked rather than disabled, and the prop that blocks it is
  the sentence explaining why rather than a boolean. The reason therefore has to be written before
  the button can be switched off, which is the whole point of the exercise. A blocked button looks
  the way a disabled one did, refuses the click and the Enter key that would have submitted its
  form, and gives the reason up to a hover, a tab stop or a tap.
*/
type Blocked = string | false | null | undefined

/**
 * The first reason that applies, or null when the button is good to go.
 *
 * Written to be fed `condition && 'why not'` in the order the player would think of them, so the
 * one thing they are told is the first thing standing in the way rather than the last:
 *
 *   blocked={firstReason(
 *     busy && BUSY,
 *     turns < cost && `That run wants ${cost} turns and you have ${turns}.`,
 *   )}
 */
function firstReason(...reasons: Blocked[]): string | null {
  return reasons.find((reason): reason is string => typeof reason === 'string' && reason !== '') ?? null
}

// The reason almost every button in the game can give, because almost every one of them waits on the
// same in-flight request.
const BUSY = 'Hold on - your last move is still going through.'

// The same thing said behind the admin desk, where the buttons act on the game rather than play it.
const WORKING = 'Hold on - the last request is still going through.'

function Button({ blocked, className, title, onClick, children, ...rest }: {
  blocked?: Blocked
} & Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, 'disabled'>) {
  const button = useRef<HTMLButtonElement>(null)
  const reason = firstReason(blocked)

  /*
    Bootstrap's tooltip rather than the browser's title bubble: the browser's waits the best part of a
    second, is styled by the operating system rather than by this game, and never appears for someone
    arriving by keyboard. Constructed by hand because tooltips are the one Bootstrap plugin that stays
    opt-in under the data-attribute API, and disposed on the way out so a button that unmounts while
    the bubble is up does not leave it behind on the page.
  */
  useEffect(() => {
    if (!button.current || !reason) return
    const tip = new bootstrap.Tooltip(button.current, {
      title: reason,
      trigger: 'hover focus',
      customClass: 'blocked-reason',
      // On the body, so a reason raised from inside a dialog or the chat dock is not clipped by it.
      container: 'body',
    })
    return () => tip.dispose()
  }, [reason])

  return <button
    {...rest}
    ref={button}
    className={reason ? `${className ?? ''} is-blocked` : className}
    // Not the disabled attribute: this button keeps its place in the tab order precisely so that
    // someone who never touches a mouse can land on it and be told why it is off.
    aria-disabled={reason ? true : undefined}
    title={reason ? undefined : title}
    onClick={event => {
      if (reason) {
        // Stops the click, and with it the submit that a button inside a form would otherwise fire -
        // including the one the browser sends here when Enter is pressed in a text field.
        event.preventDefault()
        return
      }
      onClick?.(event)
    }}
  >{children}</button>
}

function LandingView({ stats }: { stats: PublicStats | null }) {
  const leaders = stats?.leaders ?? []
  const headlines = stats?.headlines ?? []
  const top = leaders[0]

  return <section className="landing-view d-grid gap-4">
    <div className="landing-hero d-grid gap-3">
      <div className="brand-mark d-grid place-items-center border border-primary text-primary fw-bolder">SE</div>
      <div>
        <span className="eyebrow text-primary">Browser strategy crime sim</span>
        <h1>Street Empire</h1>
      </div>
      <p className="lead mb-0">
        Build a crew, hold ground, move product, and climb a live city ladder where every run leaves a mark.
      </p>
    </div>

    <div className="landing-stats d-grid gtc-2 gtc-md-4 gap-2">
      <Stat label="Players" value={stats ? number.format(stats.players) : '...'} />
      <Stat label="Cities" value={stats ? number.format(stats.cities) : '...'} />
      <Stat label="Alliances" value={stats ? number.format(stats.alliances) : '...'} />
      <Stat label="Net Worth" value={stats ? money.format(stats.totalNetWorth) : '...'} />
    </div>

    <div className="landing-screens d-grid gap-3">
      <div className="landing-screen screen-command">
        <div className="screen-bar"><span /><span /><span /></div>
        <div className="screen-grid">
          <div>
            <span className="eyebrow">Overview</span>
            <strong>{top?.playerName ?? 'The city is waking up'}</strong>
            <small>{top ? `${top.city} / ${money.format(top.netWorth)}` : 'Be first on the board'}</small>
          </div>
          <div>
            <span className="eyebrow">Crew</span>
            <strong>{top ? number.format(top.crew) : '0'}</strong>
            <small>people on payroll</small>
          </div>
          <div className="screen-wide">
            <span className="eyebrow">Clock</span>
            <div className="screen-progress"><span /></div>
          </div>
        </div>
      </div>

      <div className="landing-screen screen-map">
        <div className="screen-bar"><span /><span /><span /></div>
        <div className="map-grid" aria-hidden="true">
          {Array.from({ length: 18 }, (_, index) => <span key={index} className={index % 5 === 0 ? 'held' : index % 7 === 0 ? 'hot' : ''} />)}
        </div>
        <div className="d-flex justify-content-between small">
          <strong>{stats ? number.format(stats.territoriesHeld) : '...'} pieces held</strong>
          <span className="text-body-tertiary">{stats ? number.format(stats.activeMissions) : '...'} live fights</span>
        </div>
      </div>

      <div className="landing-screen screen-feed">
        <div className="screen-bar"><span /><span /><span /></div>
        {(headlines.length > 0 ? headlines.slice(0, 3) : [
          { kind: 'street', title: 'Work the street', detail: 'Spend turns for cash and reputation.' },
          { kind: 'market', title: 'Move goods', detail: 'Buy, craft, list, and deliver.' },
          { kind: 'crew', title: 'Pick a fight', detail: 'Scout before you swing.' },
        ]).map(item => <div className="feed-line" key={item.kind}>
          <strong>{item.title}</strong>
          <small>{item.detail}</small>
        </div>)}
      </div>
    </div>

    {leaders.length > 0 && <div className="landing-leaders border rounded p-3">
      <div className="panel-title mb-2"><h2 className="h5 mb-0">Top Houses</h2><span>{compactDateTime(stats?.generatedAtUtc)}</span></div>
      <div className="d-grid gap-2">
        {leaders.map(leader => <div className="d-grid landing-leader-row gap-2 align-items-center" key={`${leader.rank}-${leader.playerName}`}>
          <span className="tnum text-body-tertiary">#{leader.rank}</span>
          <strong className="text-truncate">{leader.playerName}</strong>
          <span className="text-body-tertiary text-truncate">{leader.city}</span>
          <span className="tnum text-end">{money.format(leader.netWorth)}</span>
        </div>)}
      </div>
    </div>}
  </section>
}

type AppPage = 'overview' | 'street' | 'crew' | 'market' | 'casino' | 'recon' | 'seasons' | 'updates' | 'alliance' | 'account' | 'admin'

// Quick grants for the selected player. Every one goes through the audited adjust endpoint, so
// unlike the old self-only cheats these work on anybody and leave a record with a reason.
const adjustPresets: { label: string, resource: string, delta: number }[] = [
  { label: '+$10k cash', resource: 'cash', delta: 10_000 },
  { label: '+$10k bank', resource: 'bank', delta: 10_000 },
  { label: '+50 turns', resource: 'turns', delta: 50 },
  { label: '+5 pimps', resource: 'pimps', delta: 5 },
  { label: '+25 hoes', resource: 'hoes', delta: 25 },
  { label: '+10 thugs', resource: 'thugs', delta: 10 },
  { label: '+100 condoms', resource: 'condoms', delta: 100 },
  { label: '+100 beer', resource: 'beer', delta: 100 },
  // Pistols rather than "weapons": the adjust endpoint takes a tier, and there has been no such column
  // since guns split into four. The button answered 400 to every press.
  { label: '+10 pistols', resource: 'pistols', delta: 10 },
  { label: '+250 weed', resource: 'weed', delta: 250 },
  { label: '+100 coke', resource: 'coke', delta: 100 },
]

/**
 * The pages that keep a permanent slot in the phone's bottom bar. A tab bar stops being navigation
 * somewhere around five items - past that the targets get too narrow to hit and the labels too short
 * to read - so the rest live behind More. These four are the loop the ladder itself teaches: work the
 * streets, staff the crew, sell what you made, and a home to see it from.
 */
const primaryPages: AppPage[] = ['overview', 'street', 'crew', 'market']

const pageMeta: Record<AppPage, { label: string, short: string, kicker: string }> = {
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

const updateCategories: GameAnnouncement['category'][] = ['Info', 'Patch', 'Balance', 'Event', 'Maintenance']
const updateSeverities: GameAnnouncement['severity'][] = ['Info', 'Warning', 'Event', 'Maintenance']

/**
 * Somewhere to send a player: a page, the tab on it, and the panel on that.
 *
 * All three, because all three are the address. A page was never enough, a tab is not either: the
 * Business page is four screens tall and the crew page longer, so "we took you to the right tab" can
 * still mean the thing you were sent for is off the bottom of the screen with nothing pointing at it.
 */
type GoTo = (page: AppPage, tab?: string, area?: string) => void

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
function flowTarget(name: string): { page: AppPage, tab?: string, area?: string } {
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
function flowPage(name: string): AppPage {
  return flowTarget(name).page
}

/** Sends somebody at a name. The one call every "take me there" button should be making. */
function goToFlow(onPage: GoTo, name: string): void {
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
function MobileNav({ pages, active, onPick, onLogout }: {
  pages: AppPage[]
  active: AppPage
  onPick: (page: AppPage) => void
  onLogout: () => void
}) {
  const [open, setOpen] = useState(false)
  const primary = primaryPages.filter(page => pages.includes(page))
  const rest = pages.filter(page => !primary.includes(page))

  // A sheet that outlives its page would cover whatever you navigated to.
  useEffect(() => {
    if (!open) return
    const escape = (event: KeyboardEvent) => { if (event.key === 'Escape') setOpen(false) }
    window.addEventListener('keydown', escape)
    return () => window.removeEventListener('keydown', escape)
  }, [open])

  const go = (page: AppPage) => { onPick(page); setOpen(false) }

  return <>
    {open && <div className="nav-sheet-backdrop position-fixed top-0 bottom-0 start-0 end-0 d-flex align-items-end d-md-none" onClick={() => setOpen(false)}>
      <div
        className="nav-sheet w-100 bg-body-secondary border-top rounded-top-3"
        role="dialog"
        aria-label="All pages"
        onClick={event => event.stopPropagation()}
      >
        <div className="nav-sheet-grip mx-auto mb-3 rounded-pill bg-secondary" />
        <div className="d-grid gtc-2 gap-2">
          {rest.map(page => <button
            className={`btn btn-secondary d-grid gap-1 text-start min-h-tap ${active === page ? 'border-primary text-primary' : ''}`}
            key={page}
            type="button"
            onClick={() => go(page)}
          >
            <strong className="">{pageMeta[page].label}</strong>
            <small className="small text-body-tertiary">{pageMeta[page].kicker}</small>
          </button>)}
        </div>
        <button className="btn btn-secondary w-100 mt-2" type="button" onClick={onLogout}>Logout</button>
      </div>
    </div>}

    <nav className="tab-bar d-grid d-md-none position-fixed bottom-0 start-0 end-0 border-top gap-1" aria-label="Primary">
      {primary.map(page => <button
        className={`btn btn-sm fw-bold min-h-tap ${active === page ? 'text-dark bg-primary' : 'text-body-secondary'}`}
        key={page}
        type="button"
        aria-current={active === page ? 'page' : undefined}
        onClick={() => onPick(page)}
      >{pageMeta[page].label}</button>)}
      {/* More carries the name of wherever you are when you are somewhere it holds, so the bar never
          shows a page you cannot see yourself on. */}
      <button
        className={`btn btn-sm fw-bold min-h-tap ${rest.includes(active) ? 'text-dark bg-primary' : 'text-body-secondary'}`}
        type="button"
        aria-expanded={open}
        onClick={() => setOpen(value => !value)}
      >{rest.includes(active) ? pageMeta[active].label : 'More'}</button>
    </nav>
  </>
}

/**
 * The walkthrough.
 *
 * A new player arrives at a wall of numbers with no idea which of them is the one that matters
 * today. The opening ladder tells them what to do next, but not what any of it is or where it lives,
 * and reading a panel does not tell you why you would ever open it.
 *
 * So: one thing lit at a time, everything else dimmed, and a sentence saying what it is and what it is
 * for. The tour drives the pages itself, because half of what a newcomer needs to learn is which tab a
 * thing lives on - being taken there is the lesson.
 */
/**
 * A half-finished Discord sign-up has to survive a reload, and a reload takes the query string with
 * it. This remembers that one is in flight; the identity behind it never leaves the server, so the
 * worst a tampered flag can do is ask for a ticket that is not there and be told so.
 */
const discordPendingKey = 'street-empire.discord.pending'

/**
 * What came back from the round trip. Discord hands the browser back as an ordinary page load, so one
 * word in the query string is the whole of what the server can say - these are the sentences it means.
 * Signing in needs no line of its own: the dashboard appearing is the message.
 */
const discordOutcomes: Partial<Record<DiscordOutcome, { text: string, bad?: boolean }>> = {
  connected: { text: 'Discord connected.' },
  'connected-reward': { text: 'Discord connected. Link reward paid: $10,000, 25 condoms, 25 beer.' },
  synced: { text: 'Discord profile refreshed.' },
  'already-connected': { text: 'This account already has a Discord connected. Disconnect that one first.', bad: true },
  cancelled: { text: 'Discord sign-in was cancelled.' },
  failed: { text: 'Discord could not finish signing you in. Try again.', bad: true },
  locked: { text: 'That account is banned or suspended.', bad: true },
  unavailable: { text: 'Discord sign-in is not set up on this server.', bad: true },
}

// A step is a page, a tab and a panel, which is the same address everything else navigates by. It was
// a page and a target, and the target was looked up under a marker only the tour used - so the tour and
// the guidance list were two vocabularies for one question, and only one of them was ever checked.
/*
  A step is a page, a tab and a panel - the same address everything else navigates by - plus the thing
  the player does there.

  The six steps this replaces were a guided read: here is the status strip, here is the hideout, here
  are other players. Nobody remembers a tour of panels. What a first session actually has to teach is
  the loop, and the loop is four moves - see what a shift pays, work one, put the money somewhere it
  cannot be taken, buy what the next one burns.

  `done` is what makes a beat a beat rather than a caption. It reads the live dashboard, so the tick
  appears because the player did the thing and not because they pressed Next. Next is never blocked on
  it: somebody who already knows this game should be able to leave, and a tutorial that traps people
  is worse than one nobody reads.
*/
type TourStep = {
  page: AppPage
  tab?: string
  area: string
  title: string
  body: string
  /** What doing it looks like from the outside, against the dashboard as it was when the step opened. */
  done?: (now: Dashboard, before: Dashboard) => boolean
  doing?: string
}

const tourSteps: TourStep[] = [
  {
    page: 'street',
    area: 'street-action',
    title: 'Working the streets',
    body: 'Turns are the real currency here. Cash comes back with the next shift; turns come back at '
      + 'twelve an hour, so a bank spent badly is tomorrow gone. Work twenty rather than the lot - '
      + 'enough to see what a shift does, cheap enough to be wrong about. The cut beside it is what your '
      + 'hoes keep before anything reaches you: raise it and they stay happy on less of your money, drop '
      + 'it and the reverse. Thirty is the middle.',
    doing: 'Work a shift',
    done: (now, before) => now.turns < before.turns,
  },
  {
    page: 'street',
    area: 'bank',
    title: 'Put it somewhere it cannot be taken',
    body: 'Cash on hand is what a raider walks off with. Banked cash is not, and it still counts towards '
      + 'your standing. This is the single cheapest habit in the game and the one nothing tells you about '
      + 'until the night it costs you.',
    doing: 'Bank what you just earned',
    done: (now, before) => now.bankCash > before.bankCash,
  },
  {
    page: 'street',
    area: 'supplies',
    title: 'Buy what the next one burns',
    body: 'Your hoes work through condoms and your thugs drink; a shift you cannot supply pays the same '
      + 'and sours the crew, and a sour crew starts walking out. You started with about one full shift '
      + 'of both. That checkbox under the button buys the shortfall for you - that is what it is for.',
    doing: 'Restock, or tick auto-buy',
    done: (now, before) => now.condoms > before.condoms || now.beer > before.beer,
  },
]


function Walkthrough({ active, stepIndex, dashboard, onPage, onStep, onClose }: {
  active: boolean
  stepIndex: number
  dashboard: Dashboard
  onPage: GoTo
  onStep: (index: number) => void
  onClose: () => void
}) {
  const [rect, setRect] = useState<DOMRect | null>(null)
  /*
    The dashboard as it stood when this step opened, so "did they do it" is a comparison rather than a
    threshold nobody could write. Banking is not "bank over a thousand", it is "there is more in there
    than when I asked" - which works the same for somebody on their first shift and somebody who came
    back to the walkthrough a month in.
  */
  const opened = useRef(dashboard)
  useEffect(() => { opened.current = dashboard }, [stepIndex])
  // The card measures itself. The first version guessed 200px of height and placed the card against
  // that guess, which put it off the top of the screen the moment a target was tall - the opening
  // ladder is long enough that the step explaining it was the step you could not read.
  const boxRef = useRef<HTMLDivElement | null>(null)
  const [boxSize, setBoxSize] = useState({ width: 330, height: 190 })
  const step = tourSteps[stepIndex]

  useEffect(() => {
    if (!boxRef.current) return
    const measured = boxRef.current.getBoundingClientRect()
    if (measured.height < 1) return
    setBoxSize(previous =>
      Math.abs(previous.height - measured.height) < 2 && Math.abs(previous.width - measured.width) < 2
        ? previous
        : { width: measured.width, height: measured.height })
  }, [stepIndex, rect])

  // Drive the page first: a target on another tab does not exist to be measured yet.
  useEffect(() => {
    if (!active || !step) return
    onPage(step.page, step.tab)
  }, [active, stepIndex])

  // Measure after the page has had a frame to render, and keep measuring while things move.
  useEffect(() => {
    if (!active || !step) return

    let frame = 0
    const measure = () => {
      const node = document.querySelector(`[data-area="${step.area}"]`)
      if (!node) { setRect(null); return }
      node.scrollIntoView({ block: 'center', behavior: 'smooth' })
      setRect(node.getBoundingClientRect())
    }

    // Two frames, then a settle: the page swap, then the scroll.
    frame = requestAnimationFrame(() => requestAnimationFrame(measure))
    const settle = setTimeout(measure, 380)
    window.addEventListener('resize', measure)
    window.addEventListener('scroll', measure, true)
    return () => {
      cancelAnimationFrame(frame)
      clearTimeout(settle)
      window.removeEventListener('resize', measure)
      window.removeEventListener('scroll', measure, true)
    }
  }, [active, stepIndex])

  useEffect(() => {
    if (!active) return
    const keys = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
      if (event.key === 'ArrowRight') onStep(Math.min(tourSteps.length - 1, stepIndex + 1))
      if (event.key === 'ArrowLeft') onStep(Math.max(0, stepIndex - 1))
    }
    window.addEventListener('keydown', keys)
    return () => window.removeEventListener('keydown', keys)
  }, [active, stepIndex])

  if (!active || !step) return null

  const last = stepIndex === tourSteps.length - 1
  const didIt = step?.done?.(dashboard, opened.current) ?? false
  const pad = 8
  const gap = pad + 10
  const edge = 12
  const view = { w: window.innerWidth, h: window.innerHeight }

  // Beside the highlight wherever it fits, which is the placement that works for every shape of
  // target: a panel can be tall enough to leave no room above or below it and still have a whole
  // empty column next to it. Above and below are the fallbacks, and every one of them is clamped
  // into the viewport afterwards, so no arithmetic here can put the card somewhere unreadable.
  const boxStyle: React.CSSProperties = (() => {
    if (!rect) return { top: '50%', left: '50%', transform: 'translate(-50%, -50%)' }

    const fitsRight = view.w - rect.right - gap - edge >= boxSize.width
    const fitsLeft = rect.left - gap - edge >= boxSize.width
    const fitsBelow = view.h - rect.bottom - gap - edge >= boxSize.height

    const left = fitsRight ? rect.right + gap
      : fitsLeft ? rect.left - gap - boxSize.width
      : rect.left
    const top = fitsRight || fitsLeft
      // Beside: centred on the target, so the eye travels sideways rather than hunting.
      ? rect.top + rect.height / 2 - boxSize.height / 2
      : fitsBelow ? rect.bottom + gap
      // Nowhere left to stand - the target is bigger than the screen. Sit at the foot of it rather
      // than the head, so the panel's own heading stays readable underneath the dimming and the
      // player can still tell what is being pointed at.
      : view.h - boxSize.height - edge

    return {
      left: Math.round(Math.min(Math.max(edge, left), Math.max(edge, view.w - boxSize.width - edge))),
      top: Math.round(Math.min(Math.max(edge, top), Math.max(edge, view.h - boxSize.height - edge))),
    }
  })()

  return <div className="tour position-fixed top-0 bottom-0 start-0 end-0" role="dialog" aria-label={step.title}>
    {/* The dimming is one enormous shadow cast outward from the hole, so there is exactly one element
        to keep in step with the target rather than four panels around it. */}
    {rect && <div
      className="tour-spotlight"
      style={{
        top: rect.top - pad,
        left: rect.left - pad,
        width: rect.width + pad * 2,
        height: rect.height + pad * 2,
      }}
    />}
    {!rect && <div className="tour-dim position-fixed top-0 bottom-0 start-0 end-0" />}

    <div className="tour-box d-grid gap-1 border border-primary rounded-3 bg-body-tertiary" ref={boxRef} style={boxStyle}>
      <span className="eyebrow text-body-tertiary">Step {stepIndex + 1} of {tourSteps.length}</span>
      <strong className="text-primary fs-5">{step.title}</strong>
      <p className="text-body-secondary small lh-base m-0">{step.body}</p>
      {/* The thing to actually do, and a tick once the dashboard says it happened. Never a gate: the
          Next button below stays live either way, because a walkthrough nobody can leave is worse
          than one nobody reads. */}
      {step.doing && <span className={`small fw-bold ${didIt ? 'text-success-emphasis' : 'text-primary'}`}>
        {didIt ? '✓ ' : '→ '}{step.doing}
      </span>}
      <div className="d-flex align-items-center justify-content-between gap-2 mt-2">
        <button className="btn btn-secondary btn-sm" type="button" onClick={onClose}>
          {last ? 'Done' : 'Skip'}
        </button>
        <div className="d-flex gap-2">
          {stepIndex > 0 && <button
            className="btn btn-secondary btn-sm"
            type="button"
            onClick={() => onStep(stepIndex - 1)}
          >Back</button>}
          <button
            className="btn btn-primary btn-sm"
            type="button"
            onClick={() => (last ? onClose() : onStep(stepIndex + 1))}
          >{last ? 'Finish' : 'Next'}</button>
        </div>
      </div>
    </div>
  </div>
}

/**
 * Talking.
 *
 * Three rooms behind three tabs: the whole board, the town you are standing in, and your crew. The
 * room follows you rather than being chosen once - travel and the city tab is a different town, leave
 * a crew and the crew tab closes - because that is what those words mean.
 *
 * Polled rather than pushed. A socket would be the right answer for a chat that has to feel instant,
 * and this one does not: it sits beside a game whose turns arrive every ten minutes, and a few seconds
 * of lag on a line costs nothing against a connection that has to be held open, reconnected, and
 * reasoned about every time the tab sleeps.
 */
const chatDockKey = 'street-empire.chat.state'
const chatOpenKey = 'street-empire.chat.open'

type ChatDockState = 'open' | 'minimised' | 'closed'

/**
 * Talking.
 *
 * One window for the rooms and the list of conversations, and a window of its own for each conversation
 * you have open - which is the shape people already know from every messenger, and the reason it is
 * worth the extra state: reading one conversation should not close another, and a group is only useful
 * if you can keep an eye on it while you answer somebody else.
 *
 * The open windows are remembered across page changes and reloads, because a window that closes itself
 * when you go to work the streets is the thing this whole dock exists to stop.
 */
function ChatWindows({ dashboard, busy }: { dashboard: Dashboard, busy: boolean }) {
  const [open, setOpen] = useState<number[]>(() => {
    try { return JSON.parse(localStorage.getItem(chatOpenKey) ?? '[]') as number[] }
    catch { return [] }
  })

  const remember = (next: number[]) => {
    // Only so many fit along the bottom before they start stacking on top of each other.
    const capped = next.slice(0, 3)
    setOpen(capped)
    localStorage.setItem(chatOpenKey, JSON.stringify(capped))
  }

  const openOne = (id: number) => remember([id, ...open.filter(x => x !== id)])
  const closeOne = (id: number) => remember(open.filter(x => x !== id))

  useEffect(() => {
    const fromElsewhere = (event: Event) => {
      const detail = (event as CustomEvent<{ conversationId?: number }>).detail
      if (detail?.conversationId) openOne(detail.conversationId)
    }
    window.addEventListener('street-empire:conversation', fromElsewhere)
    return () => window.removeEventListener('street-empire:conversation', fromElsewhere)
  }, [open])

  return <>
    {open.map((id, index) => <ConversationWindow
      busy={busy}
      index={index}
      key={id}
      conversationId={id}
      onClose={() => closeOne(id)}
    />)}
    <ChatDock dashboard={dashboard} busy={busy} onOpenConversation={openOne} />
  </>
}

/** The rooms, and the list of everything you have open elsewhere. */
function ChatDock({ dashboard, busy, onOpenConversation }: {
  dashboard: Dashboard
  busy: boolean
  onOpenConversation: (id: number) => void
}) {
  const [state, setState] = useState<ChatDockState>(() => {
    const saved = localStorage.getItem(chatDockKey)
    return saved === 'open' || saved === 'minimised' || saved === 'closed' ? saved : 'minimised'
  })
  const [channel, setChannel] = useState<ChatChannelKey | 'Direct'>('Global')
  const [board, setBoard] = useState<ChatBoard | null>(null)
  const [list, setList] = useState<ChatConversationList | null>(null)
  const [blocked, setBlocked] = useState<BlockedList | null>(null)
  const [showBlocked, setShowBlocked] = useState(false)
  const [picking, setPicking] = useState(false)
  const [draft, setDraft] = useState('')
  const [error, setError] = useState('')
  const [sending, setSending] = useState(false)
  const [unread, setUnread] = useState(0)
  const log = useRef<HTMLDivElement | null>(null)
  const pinned = useRef(true)
  const seen = useRef(0)

  const move = (next: ChatDockState) => {
    setState(next)
    localStorage.setItem(chatDockKey, next)
  }

  const loadRoom = async (which: ChatChannelKey, isOpen: boolean) => {
    try {
      const next = await api.chat(which)
      setBoard(next)
      setError('')
      const newest = next.messages.length > 0 ? next.messages[next.messages.length - 1].id : 0
      if (isOpen) { seen.current = newest; setUnread(0) }
      else setUnread(next.messages.filter(m => m.id > seen.current && !m.yours).length)
    } catch (e) { setError((e as Error).message) }
  }

  useEffect(() => {
    if (state === 'closed' || channel === 'Direct') return
    void loadRoom(channel, state === 'open')
  }, [channel, state, dashboard.city, dashboard.alliance?.id])

  useEffect(() => {
    if (state === 'closed') return
    const every = state === 'open' ? 8000 : 25000
    const tick = setInterval(() => {
      if (document.visibilityState !== 'visible') return
      if (channel !== 'Direct') void loadRoom(channel, state === 'open')
    }, every)
    return () => clearInterval(tick)
  }, [channel, state])

  // The conversation list is kept up to date whatever tab is showing, so the badge is honest while you
  // are reading the global room.
  const refreshList = async () => {
    try { setList(await api.conversations()); setBlocked(await api.blocked()) }
    catch { /* the panel shows read errors from the room it is on */ }
  }

  useEffect(() => {
    if (state === 'closed') return
    void refreshList()
    const tick = setInterval(() => { if (document.visibilityState === 'visible') void refreshList() }, 20000)
    const again = () => void refreshList()
    window.addEventListener('street-empire:blocked', again)
    window.addEventListener('street-empire:conversation', again)
    return () => { clearInterval(tick); window.removeEventListener('street-empire:blocked', again); window.removeEventListener('street-empire:conversation', again) }
  }, [state])

  useEffect(() => {
    const node = log.current
    if (node && pinned.current) node.scrollTop = node.scrollHeight
  }, [board, state, channel])

  const onScroll = () => {
    const node = log.current
    if (node) pinned.current = node.scrollHeight - node.scrollTop - node.clientHeight < 40
  }

  const current = channel === 'Direct' ? undefined : board?.channels.find(x => x.channel === channel)
  const max = board?.maxLength ?? 280
  const over = draft.length > max

  /*
    How much of the bottom of the screen this is sitting on, published for the page to pad itself by.

    On a phone the dock spans the full width and pins itself just above the tab bar, so the last inch
    of every page was underneath it and could not be scrolled into view - the tab bar was allowed for
    in the page padding and this was not. Measured rather than written into the stylesheet, because
    the dock is a bar when minimised and a panel when open, and a hardcoded number would be right for
    one of those and wrong for the other.

    Only while it is minimised. Open, it is a panel somebody is reading and will close, and padding
    the page by the height of it would leave most of a screen of nothing under the last card.
  */
  const dock = useRef<HTMLElement | null>(null)
  useEffect(() => {
    const node = dock.current
    const clear = () => document.documentElement.style.removeProperty('--chat-dock-height')
    if (!node || state !== 'minimised') { clear(); return clear }

    const publish = () => document.documentElement.style.setProperty(
      '--chat-dock-height',
      `${Math.ceil(node.getBoundingClientRect().height)}px`)
    publish()
    const observer = new ResizeObserver(publish)
    observer.observe(node)
    return () => { observer.disconnect(); clear() }
  }, [state])

  const say = async () => {
    const body = draft.trim()
    if (!body || sending || channel === 'Direct') return
    setSending(true)
    try {
      await api.say(channel, body)
      setDraft('')
      pinned.current = true
      await loadRoom(channel, true)
    } catch (e) { setError((e as Error).message) }
    finally { setSending(false) }
  }

  if (state === 'closed') {
    return <button
      className="chat-launcher position-fixed btn rounded-pill border-primary text-primary fw-bold bg-body-tertiary px-3 py-2"
      type="button"
      onClick={() => move('open')}
      aria-label="Open chat"
    >
      Talk{(list?.unread ?? 0) > 0 && <b className="badge rounded-pill bg-danger text-white ms-1">{list!.unread}</b>}
    </button>
  }

  const isOpen = state === 'open'
  const totalUnread = unread + (list?.unread ?? 0)

  return <section
    ref={dock}
    className={`chat-dock position-fixed d-grid border border-bottom-0 rounded-top-3 bg-body-secondary p-2 ${isOpen ? 'open gap-2' : ''}`}
    aria-label="Chat"
  >
    <header className="d-flex align-items-center gap-2">
      <button
        className="btn btn-link flex-fill min-w-0 d-flex align-items-baseline gap-2 text-start text-decoration-none p-1"
        type="button"
        onClick={() => move(isOpen ? 'minimised' : 'open')}
      >
        <strong className="text-primary">Talk</strong>
        <span className="min-w-0 text-body-tertiary small text-truncate">{channel === 'Direct' ? 'Messages' : board?.scope ?? ''}</span>
        {!isOpen && totalUnread > 0 && <b className="badge rounded-pill bg-danger text-white">{totalUnread > 99 ? '99+' : totalUnread}</b>}
      </button>
      <div className="chat-dock-controls d-flex gap-1">
        <button className="btn btn-secondary p-0 lh-1" type="button" title={isOpen ? 'Minimise' : 'Maximise'} onClick={() => move(isOpen ? 'minimised' : 'open')}>
          {isOpen ? '–' : '▲'}
        </button>
        <button className="btn btn-secondary p-0 lh-1" type="button" title="Close" onClick={() => move('closed')}>{'×'}</button>
      </div>
    </header>

    {isOpen && <>
      <div className="chat-tabs d-grid gap-1">
        {(board?.channels ?? []).map(tab => <button
          className={`btn btn-sm flex-fill small fw-bold ${tab.channel === channel ? 'border-primary bg-body-tertiary text-primary' : 'btn-secondary text-body-secondary'}`}
          key={tab.channel}
          type="button"
          title={tab.blockedReason ?? tab.detail}
          onClick={() => { setChannel(tab.channel); setPicking(false) }}
        >{tab.label}</button>)}
        <button
          className={`btn btn-sm flex-fill small fw-bold ${channel === 'Direct' ? 'border-primary bg-body-tertiary text-primary' : 'btn-secondary text-body-secondary'}`}
          type="button"
          title="People you are talking to"
          onClick={() => setChannel('Direct')}
        >
          Messages{(list?.unread ?? 0) > 0 && <b className="badge rounded-pill bg-danger text-white ms-1">{list!.unread}</b>}
        </button>
      </div>

      {channel === 'Direct' && <div className="chat-direct-actions d-flex align-items-center justify-content-between gap-2">
        <button className="btn btn-secondary btn-sm" type="button" onClick={() => { setPicking(v => !v); setShowBlocked(false) }}>
          {picking ? 'Cancel' : 'New message'}
        </button>
        {(blocked?.blocked.length ?? 0) > 0 && <button
          className="btn btn-link small text-body-secondary"
          type="button"
          onClick={() => { setShowBlocked(v => !v); setPicking(false) }}
        >{showBlocked ? 'Conversations' : `Blocked (${blocked!.blocked.length})`}</button>}
      </div>}

      {channel === 'Direct' && picking
        ? <PeoplePicker
          busy={busy}
          onCancel={() => setPicking(false)}
          onStarted={id => { setPicking(false); onOpenConversation(id); void refreshList() }}
        />
        : <div className={`chat-log d-grid align-content-start gap-1 border rounded bg-body-tertiary p-2 overflow-y-auto ${channel === 'Direct' ? 'chat-log-direct' : ''}`} ref={log} onScroll={onScroll}>
          {channel === 'Direct' && showBlocked && blocked?.blocked.map(person => <div className="chat-thread d-grid border rounded bg-body-secondary p-2" key={person.playerId}>
            <strong>{person.name}</strong>
            <button className="btn btn-secondary btn-sm" type="button" onClick={() => void (async () => {
              try { await api.unblock(person.playerId); window.dispatchEvent(new CustomEvent('street-empire:blocked')) }
              catch (e) { setError((e as Error).message) }
            })()}>Unblock</button>
          </div>)}

          {channel === 'Direct' && !showBlocked && <>
            {!list && <p className="text-body-tertiary small m-0">Looking.</p>}
            {list && list.conversations.length === 0 && <p className="text-body-tertiary small m-0">
              Nobody yet. Use New message to find somebody.
            </p>}
            {list?.conversations.map(row => <button
              className={`chat-thread chat-conversation-row d-grid text-start border rounded bg-body-secondary p-2 ${row.unread > 0 ? 'border-primary' : ''}`}
              key={row.id}
              type="button"
              onClick={() => onOpenConversation(row.id)}
            >
              <strong className="d-flex align-items-center gap-2 text-primary small">
                {row.name}
                {row.isGroup && <em className="badge rounded-pill border border-primary text-primary fst-normal small px-1 py-0">{row.others.length + 1}</em>}
                {row.unread > 0 && <b className="badge rounded-pill bg-danger text-white">{row.unread}</b>}
              </strong>
              <span className="min-w-0 text-body-secondary small text-truncate">{row.lastBody}</span>
              <small className="text-body-tertiary small">{new Date(row.sentAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short' })}</small>
            </button>)}
          </>}

          {channel !== 'Direct' && <>
            {!board && <p className="text-body-tertiary small m-0">Listening.</p>}
            {board && board.messages.length === 0 && <p className="text-body-tertiary small m-0">
              {current?.blockedReason ?? 'Nobody has said anything here yet.'}
            </p>}
            {board?.messages.map(line => <div className="chat-line d-grid gap-2 align-items-baseline small" key={line.id}>
              <strong className={`text-nowrap ${line.yours ? 'text-body' : 'text-primary'}`}><PlayerName playerId={line.authorId}>{line.author}</PlayerName></strong>
              <span className="text-body text-break">{line.body}</span>
              <small className="text-body-tertiary small text-nowrap">{new Date(line.sentAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</small>
            </div>)}
          </>}
        </div>}

      {error && <div className="alert alert-danger"><span>{error}</span></div>}

      {channel !== 'Direct' && <form className="d-grid gtc-1-auto gap-2" onSubmit={event => { event.preventDefault(); void say() }}>
        <input
          className="form-control"
          aria-label={`Say something in ${current?.label ?? 'chat'}`}
          disabled={busy || sending || !(current?.canPost ?? false)}
          maxLength={max + 40}
          placeholder={current?.canPost === false ? current.blockedReason ?? 'You cannot speak here.' : 'Say something'}
          value={draft}
          onChange={event => setDraft(event.target.value)}
        />
        <Button className="btn btn-primary btn-sm" type="submit" blocked={firstReason(
          busy && BUSY,
          sending && 'Your last line is still on its way.',
          over && `That is ${draft.length} characters and the room takes ${max}.`,
          draft.trim().length === 0 && 'Write something first.',
        )}>Send</Button>
      </form>}
      {channel !== 'Direct' && draft.length > max * 0.75 && <small className={`d-block small text-end ${over ? 'text-danger' : 'text-body-tertiary'}`}>
        {draft.length} / {max}
      </small>}
    </>}
  </section>
}

/**
 * Finding somebody to write to.
 *
 * Search rather than a roster: the board can hold any number of players and a list of all of them is
 * not a thing anybody reads. Picking more than one turns it into a group, which is the only difference
 * between the two - a group is a conversation with more people in it, not a separate feature.
 */
function PeoplePicker({ busy, onCancel, onStarted }: {
  busy: boolean
  onCancel: () => void
  onStarted: (conversationId: number) => void
}) {
  const [term, setTerm] = useState('')
  const [found, setFound] = useState<Person[]>([])
  const [chosen, setChosen] = useState<Person[]>([])
  const [title, setTitle] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  // Debounced, because a search on every keystroke is a request per letter for a list nobody has
  // finished typing the name of yet.
  useEffect(() => {
    if (term.trim().length < 2) { setFound([]); return }
    const timer = setTimeout(() => {
      void (async () => {
        try { setFound((await api.findPeople(term)).people); setError('') }
        catch (e) { setError((e as Error).message) }
      })()
    }, 250)
    return () => clearTimeout(timer)
  }, [term])

  const toggle = (person: Person) => setChosen(list =>
    list.some(x => x.playerId === person.playerId)
      ? list.filter(x => x.playerId !== person.playerId)
      : [...list, person])

  const start = async () => {
    if (chosen.length === 0 || working) return
    setWorking(true)
    try {
      const result = chosen.length === 1
        ? await api.openDirect(chosen[0].playerId)
        : await api.startGroup(chosen.map(x => x.playerId), title)
      onStarted(result.id)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  return <div className="d-grid gap-2">
    <input
      className="form-control"
      aria-label="Find somebody"
      autoFocus
      placeholder="Find somebody by name"
      value={term}
      onChange={event => setTerm(event.target.value)}
    />

    {chosen.length > 0 && <div className="d-flex flex-wrap gap-1">
      {chosen.map(person => <button
        className="btn rounded-pill border-primary bg-body-tertiary text-primary small px-2 py-1"
        key={person.playerId}
        type="button"
        onClick={() => toggle(person)}
      >
        {person.name} {'×'}
      </button>)}
    </div>}

    {chosen.length > 1 && <input
      className="form-control"
      aria-label="Name this group"
      maxLength={48}
      placeholder="Name this group (optional)"
      value={title}
      onChange={event => setTitle(event.target.value)}
    />}

    <div className="chat-log d-grid align-content-start gap-1 border rounded bg-body-tertiary p-2 overflow-y-auto">
      {term.trim().length > 0 && term.trim().length < 2 && <p className="text-body-tertiary small m-0">Two letters at least.</p>}
      {term.trim().length >= 2 && found.length === 0 && <p className="text-body-tertiary small m-0">Nobody by that name.</p>}
      {found.map(person => <button
        className={`chat-thread d-grid text-start border rounded bg-body-secondary p-2 ${chosen.some(x => x.playerId === person.playerId) ? 'border-primary' : ''}`}
        key={person.playerId}
        type="button"
        onClick={() => toggle(person)}
      >
        <strong className="d-flex align-items-center gap-2 text-primary small">{person.name}</strong>
        <span className="min-w-0 text-body-secondary small text-truncate">{person.city}</span>
      </button>)}
    </div>

    {error && <div className="alert alert-danger"><span>{error}</span></div>}

    <div className="d-flex justify-content-between gap-2">
      <button className="btn btn-secondary btn-sm" type="button" onClick={onCancel}>Cancel</button>
      <Button className="btn btn-primary btn-sm" type="button" blocked={firstReason(
        busy && BUSY,
        working && 'Opening the room now.',
        chosen.length === 0 && 'Pick somebody to talk to first.',
      )} onClick={() => void start()}>
        {chosen.length > 1 ? `Start group of ${chosen.length + 1}` : 'Open'}
      </Button>
    </div>
  </div>
}

/** One conversation, in a window of its own. */
function ConversationWindow({ conversationId, index, busy, onClose }: {
  conversationId: number
  index: number
  busy: boolean
  onClose: () => void
}) {
  const [talk, setTalk] = useState<ChatConversation | null>(null)
  const [draft, setDraft] = useState('')
  const [error, setError] = useState('')
  const [sending, setSending] = useState(false)
  const [minimised, setMinimised] = useState(false)
  const log = useRef<HTMLDivElement | null>(null)
  const pinned = useRef(true)

  const load = async () => {
    try { setTalk(await api.conversation(conversationId)); setError('') }
    catch (e) {
      // A conversation can stop being readable while its window is still on the screen: it was swept
      // for age, or you were put out of the group. Left alone the window sat there saying Loading for
      // ever, and because the open list is kept in storage it came back on every reload - a dead
      // window you could close but never be rid of.
      //
      // Only a refusal closes it. A request that never landed is a wobble, and the next tick will
      // pick the conversation back up rather than throwing it away because the server was restarting.
      if (e instanceof RequestError && e.refused && !talk) { onClose(); return }
      setError((e as Error).message)
    }
  }

  useEffect(() => { void load() }, [conversationId])

  useEffect(() => {
    const tick = setInterval(() => {
      if (document.visibilityState === 'visible' && !minimised) void load()
    }, 8000)
    return () => clearInterval(tick)
  }, [conversationId, minimised])

  useEffect(() => {
    const node = log.current
    if (node && pinned.current) node.scrollTop = node.scrollHeight
  }, [talk, minimised])

  const onScroll = () => {
    const node = log.current
    if (node) pinned.current = node.scrollHeight - node.scrollTop - node.clientHeight < 40
  }

  const max = talk?.maxLength ?? 280
  const over = draft.length > max

  const say = async () => {
    const body = draft.trim()
    if (!body || sending) return
    setSending(true)
    try {
      await api.sayIn(conversationId, body)
      setDraft('')
      pinned.current = true
      await load()
      window.dispatchEvent(new CustomEvent('street-empire:conversation'))
    } catch (e) { setError((e as Error).message) }
    finally { setSending(false) }
  }

  // Stacked leftwards from the dock, so the newest conversation is nearest to hand.
  const style = { right: `calc(1rem + ${(index + 1) * 352}px)` } as React.CSSProperties

  return <section
    className={`chat-dock chat-window position-fixed d-grid border border-bottom-0 rounded-top-3 bg-body-secondary p-2 ${minimised ? '' : 'open gap-2'}`}
    style={style}
  >
    <header className="d-flex align-items-center gap-2">
      <button
        className="btn btn-link flex-fill min-w-0 d-flex align-items-baseline gap-2 text-start text-decoration-none p-1"
        type="button"
        onClick={() => setMinimised(v => !v)}
      >
        <strong className="text-primary">{talk?.name ?? 'Loading'}</strong>
        {talk?.isGroup && <span className="min-w-0 text-body-tertiary small text-truncate">{talk.others.length + 1} people</span>}
      </button>
      <div className="chat-dock-controls d-flex gap-1">
        <button className="btn btn-secondary p-0 lh-1" type="button" title={minimised ? 'Maximise' : 'Minimise'} onClick={() => setMinimised(v => !v)}>
          {minimised ? '▲' : '–'}
        </button>
        <button className="btn btn-secondary p-0 lh-1" type="button" title="Close" onClick={onClose}>{'×'}</button>
      </div>
    </header>

    {!minimised && <>
      <div className="chat-log d-grid align-content-start gap-1 border rounded bg-body-tertiary p-2 overflow-y-auto" ref={log} onScroll={onScroll}>
        {!talk && <p className="text-body-tertiary small m-0">Looking.</p>}
        {talk && talk.messages.length === 0 && <p className="text-body-tertiary small m-0">Nothing said yet. Say something.</p>}
        {talk?.messages.map(line => <div className="chat-line d-grid gap-2 align-items-baseline small" key={line.id}>
          <strong className={`text-nowrap ${line.yours ? 'text-body' : 'text-primary'}`}><PlayerName playerId={line.authorId}>{line.author}</PlayerName></strong>
          <span className="text-body text-break">{line.body}</span>
          <small className="text-body-tertiary small text-nowrap">{new Date(line.sentAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</small>
        </div>)}
      </div>

      {error && <div className="alert alert-danger"><span>{error}</span></div>}

      <form className="d-grid gtc-1-auto gap-2" onSubmit={event => { event.preventDefault(); void say() }}>
        <input
          className="form-control"
          aria-label={`Write to ${talk?.name ?? 'them'}`}
          disabled={busy || sending}
          maxLength={max + 40}
          placeholder={`Write to ${talk?.name ?? 'them'}`}
          value={draft}
          onChange={event => setDraft(event.target.value)}
        />
        <Button className="btn btn-primary btn-sm" type="submit" blocked={firstReason(
          busy && BUSY,
          sending && 'Your last line is still on its way.',
          over && `That is ${draft.length} characters and a message takes ${max}.`,
          draft.trim().length === 0 && 'Write something first.',
        )}>Send</Button>
      </form>
    </>}
  </section>
}

function App() {
  // Whichever name was last clicked, from anywhere in the app. See PlayerName for why this arrives on
  // the window rather than through props.
  const [openProfileId, setOpenProfileId] = useState<string | null>(null)
  useEffect(() => {
    const onOpen = (event: Event) => {
      const detail = (event as CustomEvent<{ playerId?: string }>).detail
      if (detail?.playerId) setOpenProfileId(detail.playerId)
    }
    window.addEventListener('street-empire:profile', onOpen)
    return () => window.removeEventListener('street-empire:profile', onOpen)
  }, [])

  // The inline script in index.html has already put these on <html>, so this is not what applies them
  // for the first time - it is what keeps following the system while nothing here has overridden it.
  // Somebody who turns reduced motion on mid-session should see the game stop moving without a reload.
  useEffect(() => {
    const preferences = loadPreferences()
    applyPreferences(preferences)
    return watchSystemMotion(loadPreferences)
  }, [])

  // Shown once unasked, then only when somebody wants it. A walkthrough that reappears is a nag.
  const [tourStep, setTourStep] = useState<number | null>(null)
  const tourOffered = useRef(false)
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [adminOverview, setAdminOverview] = useState<AdminOverview | null>(null)
  const [leaders, setLeaders] = useState<LeaderboardEntry[]>([])
  const [cityLeaders, setCityLeaders] = useState<LeaderboardEntry[]>([])
  const [targets, setTargets] = useState<PlayerTarget[]>([])
  const [selectedTarget, setSelectedTarget] = useState<PlayerProfile | null>(null)
  const [combatLogs, setCombatLogs] = useState<CombatLog[]>([])
  const [combatMissions, setCombatMissions] = useState<CombatMission[]>([])
  const [worldNews, setWorldNews] = useState<WorldNews>({ headlines: [], feed: [] })
  const [targetQuery, setTargetQuery] = useState('')
  const [activePage, setPage] = useState<AppPage>(() => {
    const asked = routePage()
    return asked in pageMeta ? asked as AppPage : 'overview'
  })
  /*
    The page is written here, from whatever moved it, rather than from an effect like the tabs.

    Effects run children before parents, so a shell that wrote its page in one would be writing it
    after the tab that had just mounted underneath - and since the tab writes the pair, the page it
    named would be the one being left rather than the one being opened. Writing on the way in puts
    the two in the only order that reads correctly.
  */
  /*
    The tab is optional and, when it is given, is what the address is written with. Naming one is how
    anything that knows where it is sending somebody gets them the whole way there: a room rather than
    the page the room is on, the shop rather than Business. Left out, the page opens on whatever tab it
    would have anyway, which is what a plain nav click wants.
  */
  /*
    The area is the third part of the address and the only one that is not in it.

    A tab is where a panel lives and survives a reload, so it belongs in the bar. Which panel somebody
    was pointed at is a thing that happened once, on the way in - keeping it would mean every reload for
    the rest of the session dragging the screen back down to a room they upgraded an hour ago. So it is
    carried as a request rather than a location, and answered once.

    The counter is what makes it a request rather than a value. Sending somebody to the panel they are
    already looking at has to scroll again - "take me there" pressed twice is somebody saying they
    cannot see it - and a plain string would be the same string, which React would rightly ignore.
  */
  const [scrollRequest, setScrollRequest] = useState<{ area: string, id: number } | null>(null)
  const scrollRequests = useRef(0)
  const setActivePage: GoTo = (page, tab, area) => {
    setPage(page)
    writeRoute(page, tab)
    if (area) setScrollRequest({ area, id: ++scrollRequests.current })
  }

  /*
    Answered after the page has had a frame to draw it, because a panel on a tab that has not rendered
    yet is not in the document to be found. Two frames then a settle, which is what the walkthrough
    learned to do for exactly the same reason - a page swap and then whatever the swap sets off.

    Missing is not an error. Guidance is written against the state of the world and the world moves: a
    panel can be absent because the thing it is for has been done, and the page it was on is still the
    right page to have been taken to.
  */
  useEffect(() => {
    if (!scrollRequest) return

    let frame = 0
    let settle = 0
    let clear = 0
    // Held so the cleanup can take the mark off. Two requests inside the two seconds is the ordinary
    // case rather than a strange one - somebody following the guidance list down it - and without this
    // the first panel keeps its ring for the rest of the session, pointing at nothing.
    let marked: HTMLElement | null = null

    const find = () => {
      const node = document.querySelector<HTMLElement>(`[data-area="${scrollRequest.area}"]`)
      if (!node) return false
      // Read off the root rather than out of React state: the preference layer already resolves the
      // saved choice against the system one and stamps the answer there, and a second reading of the
      // same question is a second answer waiting to disagree.
      const reduced = document.documentElement.getAttribute('data-motion') === 'reduced'
      node.scrollIntoView({ block: 'center', behavior: reduced ? 'auto' : 'smooth' })
      // A mark on the panel itself, because scrolling alone only says "somewhere around here". It
      // comes off on a timer rather than on a click: anything that waits to be dismissed is one more
      // thing to dismiss.
      marked = node
      node.classList.add('area-found')
      clear = window.setTimeout(() => node.classList.remove('area-found'), 2_000)
      return true
    }

    frame = requestAnimationFrame(() => requestAnimationFrame(() => { if (!find()) settle = window.setTimeout(find, 380) }))
    return () => {
      cancelAnimationFrame(frame)
      window.clearTimeout(settle)
      window.clearTimeout(clear)
      marked?.classList.remove('area-found')
    }
  }, [scrollRequest])
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  // Which doors this server can actually open. A button for a provider with no credentials behind it
  // is a button that fails, so it is never drawn.
  const [providers, setProviders] = useState<AuthProviders>({ discord: false, betaKeyRequired: false })
  useEffect(() => {
    void api.providers().then(setProviders).catch(() => setProviders({ discord: false, betaKeyRequired: false }))
  }, [])
  const [publicStats, setPublicStats] = useState<PublicStats | null>(null)
  useEffect(() => { void api.publicStats().then(setPublicStats).catch(() => setPublicStats(null)) }, [])
  // A Discord login that turned out to belong to nobody yet, waiting on a player name.
  const [discordTicket, setDiscordTicket] = useState<DiscordSignUpTicket | null>(null)
  /*
    Where the sign-in card is in the reset flow, if it is in it at all. Two steps rather than one
    screen, because the second needs a code that does not exist until the first has run - and the
    identifier is carried between them rather than re-typed, since it is the thing the server matches
    the code against.
  */
  const [resetStep, setResetStep] = useState<'off' | 'asking' | 'confirming' | 'code'>('off')
  const [resetIdentifier, setResetIdentifier] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [lastBreakdown, setLastBreakdown] = useState<Record<string, unknown> | null>(null)
  const [busy, setBusy] = useState(false)
  const [streetTurns, setStreetTurns] = useState(5)
  const [autoBuySupplies, setAutoBuySupplies] = useState(false)
  const [productionTurns, setProductionTurns] = useState(5)
  const [hoeCut, setHoeCut] = useState(30)
  const [bankAmount, setBankAmount] = useState(1000)
  const [crewQty, setCrewQty] = useState<Record<'pimps' | 'hoes' | 'thugs', number>>({ pimps: 1, hoes: 1, thugs: 1 })
  const [attackCrew, setAttackCrew] = useState({ thugs: 1, weapons: 0 })
  // Borrowed thugs to bring on a raid. Capped server-side at the size of your own party.
  const [borrowedThugs, setBorrowedThugs] = useState(0)
  const [commanderId, setCommanderId] = useState<number | null>(null)
  const [attackMethod, setAttackMethod] = useState<AttackMethodKey>('raid')
  // Empty means the neutral district, which is what a shift was before there was a choice.
  const [district, setDistrict] = useState('')
  const [poachCoke, setPoachCoke] = useState(50)
  // Left empty so each page derives its own default until the player types a quantity.
  const [storeQty, setStoreQty] = useState<Record<string, number>>({})
  const [sellQty, setSellQty] = useState<Record<'weed' | 'coke', number>>({ weed: 10, coke: 5 })
  const [tickSeconds, setTickSeconds] = useState(0)
  const [catchUp, setCatchUp] = useState<CatchUp | null>(null)
  const [dismissedUpdateStamp, setDismissedUpdateStamp] = useState<string | null>(null)
  const refreshInFlight = useRef(false)

  /**
   * Full reload after an action. `pollMissions` instead re-reads only what a running mission changes,
   * which keeps the 5-second poll from re-fetching the leaderboard, world news and target list.
   */
  const refresh = async () => {
    if (refreshInFlight.current) return
    refreshInFlight.current = true
    try {
      const [d, l, news, targetList, combatHistory, missions] = await Promise.all([api.dashboard(), api.leaderboard(), api.worldNews(), api.targets(targetQuery), api.combatLogs(), api.combatMissions()])
      // The town's own ladder, fetched alongside the global one so switching between them is instant.
      const cityLeaders = await api.leaderboard(d.city)
      const admin = d.isAdmin ? await api.adminOverview() : null
      setDashboard(d)
      setAdminOverview(admin)
      setLeaders(l)
      setCityLeaders(cityLeaders)
      setWorldNews(news)
      setTargets(targetList)
      setCombatLogs(combatHistory)
      setCombatMissions(missions)
      setTickSeconds(d.secondsUntilNextTurnTick)
      setHoeCut(d.hoeCutPercent)
      setError('')
    } catch (e) {
      if ((e as Error).message === 'Unauthorized') {
        setDashboard(null)
        setAdminOverview(null)
        setWorldNews({ headlines: [], feed: [] })
        setCombatLogs([])
        setCombatMissions([])
        setTargets([])
        setSelectedTarget(null)
        setActivePage('overview')
      } else setError((e as Error).message)
    } finally {
      refreshInFlight.current = false
    }
  }

  // A boolean, not the mission array: depending on the array rebuilt the interval on every poll.
  const hasActiveMission = combatMissions.some(mission => mission.status !== 'Complete')
  const hadActiveMission = useRef(false)
  const catchUpFetched = useRef(false)

  const pollMissions = async () => {
    try {
      const [missions, d] = await Promise.all([api.combatMissions(), api.dashboard()])
      setCombatMissions(missions)
      setDashboard(d)
      setTickSeconds(d.secondsUntilNextTurnTick)
    } catch {
      // A dropped poll is not worth surfacing; the next tick or action will resync.
    }
  }

  useEffect(() => { void refresh() }, [])
  useEffect(() => {
    if (!dashboard) return
    const refreshVisible = () => {
      if (document.visibilityState !== 'hidden') void refresh()
    }
    const offRoute = onRouteChange(refreshVisible)
    window.addEventListener('focus', refreshVisible)
    document.addEventListener('visibilitychange', refreshVisible)
    return () => {
      offRoute()
      window.removeEventListener('focus', refreshVisible)
      document.removeEventListener('visibilitychange', refreshVisible)
    }
  }, [dashboard?.playerId, targetQuery])
  useEffect(() => {
    /*
      The far end of the Discord round trip.

      It arrives as an ordinary page load with one word in the query string, because that is all that
      survives being handed to somebody else's site and handed back. Read it, say what it means, and
      take it straight back out of the address bar - left there, every reload would replay the message
      and a shared link would carry somebody else's outcome.
    */
    const params = new URLSearchParams(window.location.search)
    const outcome = params.get('discord') as DiscordOutcome | null
    if (outcome) {
      params.delete('discord')
      const query = params.toString()
      window.history.replaceState({}, '', window.location.pathname + (query ? `?${query}` : '') + window.location.hash)
      if (outcome === 'sign-up') sessionStorage.setItem(discordPendingKey, '1')
      const said = discordOutcomes[outcome]
      if (said?.bad) setError(said.text)
      else if (said) setNotice(said.text)
      // Connecting is something you were doing on the account page, so that is where you come back to.
      if (outcome === 'connected' || outcome === 'synced' || outcome === 'already-connected') setActivePage('account')
    }

    if (sessionStorage.getItem(discordPendingKey) === null) return
    void api.discordTicket()
      .then(setDiscordTicket)
      .catch(() => { sessionStorage.removeItem(discordPendingKey); setDiscordTicket(null) })
  }, [])
  useEffect(() => {
    // Once per arrival, and never from refresh(): reading the digest advances the server's watermark,
    // so calling it after every action would consume the news before it could be shown.
    if (!dashboard || catchUpFetched.current) return
    catchUpFetched.current = true
    void api.catchUp().then(news => { if (news.hasNews) setCatchUp(news) }).catch(() => {})
  }, [dashboard?.playerId])
  useEffect(() => {
    setDismissedUpdateStamp(null)
  }, [dashboard?.playerId])
  useEffect(() => {
    if (activePage === 'admin' && !adminOverview)
      setActivePage('overview')
  }, [activePage, adminOverview])
  useEffect(() => {
    /*
      Tidies up an address that asked for somewhere that is not here - a link from an older build, a
      typo, a first visit with no hash at all. The page itself has already fallen back; this is only
      the bar catching up, so that reloading again lands where the screen says rather than repeating
      the same wrong guess.

      Guarded rather than unconditional because this runs after the tabs underneath have written
      themselves, and their write is what makes the address valid again.
    */
    if (!(routePage() in pageMeta)) writeRoute(activePage)
  }, [])
  useEffect(() => {
    // Still stops at a full bank: this one is the countdown to the next turn, and there is nothing to
    // count towards. What must not stop is the asking, which is the effect below.
    if (!dashboard || dashboard.turns >= dashboard.maxTurns) return
    const timer = window.setInterval(() => {
      setTickSeconds(s => {
        if (s <= 1) {
          void refresh()
          return dashboard.turnTickMinutes * 60
        }
        return s - 1
      })
    }, 1000)
    return () => window.clearInterval(timer)
  }, [dashboard?.playerId, dashboard?.turns, dashboard?.maxTurns, dashboard?.turnTickMinutes])
  /*
    A full turn bank used to stop this screen refreshing at all, and turns are not the only thing the
    server settles when somebody is looked at. A finished building, a mule landing, work finishing on
    a corner, a bail window running out, a war's clock - every one of those is settled on the player's
    own clock, and none of them ran for a player sitting on a full bank doing nothing.

    Which is the state a player is in precisely when they are waiting: upgrade the building, spend
    nothing for thirty minutes, and the tier that decides how much ground you may run never lands.
    The countdown above still stops - there is nothing to count towards - but the asking does not.
  */
  useEffect(() => {
    if (!dashboard || dashboard.turns < dashboard.maxTurns) return
    const timer = window.setInterval(() => { void refresh() }, 60_000)
    return () => window.clearInterval(timer)
  }, [dashboard?.playerId, dashboard?.turns, dashboard?.maxTurns])
  useEffect(() => {
    if (!dashboard || !hasActiveMission) return
    let inFlight = false
    const timer = window.setInterval(() => {
      // Skip a tick rather than stacking requests when a poll outlives the interval.
      if (inFlight) return
      inFlight = true
      void pollMissions().finally(() => { inFlight = false })
    }, 5000)
    return () => window.clearInterval(timer)
  }, [dashboard?.playerId, hasActiveMission])
  useEffect(() => {
    // The narrow poll leaves combat history and world news behind, so resync once on the way out.
    if (hadActiveMission.current && !hasActiveMission) void refresh()
    hadActiveMission.current = hasActiveMission
  }, [hasActiveMission])
  useEffect(() => {
    // A hideout build lands server-side on the next refresh. Without this the new caps wait for the
    // turn tick, so a 30 minute build can read as finished for another ten.
    const readyAt = dashboard?.hideout.building?.completesAtUtc
    if (!readyAt) return
    const timer = window.setTimeout(() => void refresh(), Math.max(1000, new Date(readyAt).getTime() - Date.now() + 1000))
    return () => window.clearTimeout(timer)
  }, [dashboard?.hideout.building?.completesAtUtc])

  const nextTurn = useMemo(() => {
    if (!dashboard || dashboard.turns >= dashboard.maxTurns) return 'MAX'
    const m = Math.floor(tickSeconds / 60)
    const s = tickSeconds % 60
    return `${m}:${String(s).padStart(2, '0')}`
  }, [dashboard, tickSeconds])

  const auth = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true); setError('')
    try {
      if (authMode === 'register')
        await api.register(
          String(form.get('username')),
          String(form.get('password')),
          String(form.get('email') ?? ''),
          String(form.get('betaKey') ?? ''))
      else
        await api.login(String(form.get('username')), String(form.get('password')))
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  // The half of a Discord sign-up Discord cannot answer. The identity is already sitting in a signed
  // cookie on the server, so this form carries only the two things the game needs and no claim at all
  // about who is filling it in.
  const finishDiscordSignUp = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true); setError('')
    try {
      await api.completeDiscordSignUp(
        String(form.get('username')),
        String(form.get('email') ?? ''),
        String(form.get('betaKey') ?? ''))
      sessionStorage.removeItem(discordPendingKey)
      setDiscordTicket(null)
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const startReset = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const identifier = String(new FormData(event.currentTarget).get('identifier') ?? '').trim()
    setBusy(true); setError('')
    try {
      // The answer is the same sentence whether or not that account exists, so there is nothing here
      // to branch on - which is the point. The step advances either way.
      const answer = await api.startPasswordReset(identifier)
      setResetIdentifier(identifier)
      setResetStep('confirming')
      setNotice(answer.message)
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const finishReset = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const next = String(form.get('newPassword') ?? '')
    if (next !== String(form.get('confirmPassword') ?? '')) { setError('The two passwords do not match.'); return }
    setBusy(true); setError('')
    try {
      await api.confirmPasswordReset(resetIdentifier, String(form.get('code') ?? ''), next)
      setResetStep('off'); setResetIdentifier(''); setNotice('')
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  // The other way back in. Nothing is sent anywhere: the code is already on a sheet of paper, so this
  // is one form rather than two steps.
  const useRecoveryCode = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const next = String(form.get('newPassword') ?? '')
    if (next !== String(form.get('confirmPassword') ?? '')) { setError('The two passwords do not match.'); return }
    setBusy(true); setError('')
    try {
      await api.useRecoveryCode(
        String(form.get('identifier') ?? ''), String(form.get('code') ?? ''), next)
      setResetStep('off'); setResetIdentifier(''); setNotice('')
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const leaveReset = () => { setResetStep('off'); setResetIdentifier(''); setError(''); setNotice('') }

  const abandonDiscordSignUp = () => {
    sessionStorage.removeItem(discordPendingKey)
    setDiscordTicket(null)
    setError('')
    // Best effort. The ticket expires on its own in twenty minutes either way.
    void api.discardDiscordTicket().catch(() => {})
  }

  const act = async (fn: () => Promise<ActionResult | unknown>) => {
    setBusy(true); setError(''); setNotice(''); setLastBreakdown(null)
    try {
      const result = await fn() as ActionResult | undefined
      if (result?.summary) setNotice(result.summary)
      if (result?.breakdown) setLastBreakdown(result.breakdown)
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const searchTargets = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true); setError('')
    try {
      setTargets(await api.targets(targetQuery))
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const inspectTarget = async (playerId: string) => {
    setBusy(true); setError('')
    try {
      setSelectedTarget(await api.playerProfile(playerId))
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const attackTarget = async (defenderId: string) => {
    // The picked method decides which shape goes out. Kept in one place so the recon page only has to
    // know that it is attacking somebody, not which of five things that means.
    await act(() => attackMethod === 'raid'
      ? api.attack(defenderId, attackCrew.thugs, attackCrew.weapons, commanderId, borrowedThugs)
      : api.strike(defenderId, attackMethod, attackMethod === 'poach' ? poachCoke : 0))
    try {
      setSelectedTarget(await api.playerProfile(defenderId))
    } catch {
      // The action result already refreshed the main screen; this only keeps the inspected card current.
    }
  }

  if (!dashboard) {
    /*
      One shell, two things it can be showing.

      A Discord login that belongs to nobody yet cannot become a player on its own: Discord knows who
      you are and has no opinion about what you want to be called.
      So the sign-in card steps aside for a shorter form that asks for that one name, and the identity
      behind it stays where it was put - in a signed cookie the browser cannot read or forge.
    */
    return <main className="auth-shell landing-shell d-grid gap-4 p-4">
      <LandingView stats={publicStats} />
      <section className="auth-card card p-4">
        <div className="brand-mark d-grid place-items-center border border-primary text-primary fw-bolder mb-3">SE</div>
        <h2 className="h1">Street Empire</h2>

        {resetStep !== 'off'
          ? <>
            <p className="text-body-secondary">
              {resetStep === 'code'
                ? 'A recovery code is one of the ten you were given on the account page. It works without any email at all, and is spent the moment it is used.'
                : resetStep === 'asking'
                ? 'A code goes to the confirmed email address on the account. Without one there is no way back in - which is what confirming an address is for.'
                : 'Type the code from that email and pick a new password. Every other session on the account will be signed out.'}
            </p>
            {resetStep === 'code'
              ? <form className="d-grid gap-3 mt-4" onSubmit={useRecoveryCode}>
                <label className="field">
                  Username or Email
                  <input className="form-control" name="identifier" required />
                </label>
                <label className="field">
                  Recovery code
                  <input className="form-control" name="code" placeholder="ABCDE-FGHJK" required />
                  <small className="form-text">One of the ten off your sheet. It is spent once used.</small>
                </label>
                <label className="field">
                  New password
                  <input className="form-control" name="newPassword" type="password" minLength={8} required />
                </label>
                <label className="field">
                  Confirm password
                  <input className="form-control" name="confirmPassword" type="password" minLength={8} required />
                </label>
                {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
                <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Use This Code'}</Button>
                <button className="btn btn-link text-body-secondary" type="button" onClick={leaveReset}>Back to signing in</button>
              </form>
              : resetStep === 'asking'
              ? <form className="d-grid gap-3 mt-4" onSubmit={startReset}>
                <label className="field">
                  Username or Email
                  <input className="form-control" name="identifier" maxLength={254} required autoFocus />
                </label>
                {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
                <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Send Me a Code'}</Button>
                <button className="btn btn-link text-body-secondary" type="button" onClick={leaveReset}>Back to signing in</button>
              </form>
              : <form className="d-grid gap-3 mt-4" onSubmit={finishReset}>
                <label className="field">
                  Code
                  <input
                    className="form-control tnum fs-4 text-center"
                    style={{ letterSpacing: '.4em' }}
                    name="code"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    pattern="[0-9]*"
                    maxLength={6}
                    placeholder="000000"
                    required
                    autoFocus
                  />
                </label>
                <label className="field">
                  New password
                  <input className="form-control" name="newPassword" type="password" autoComplete="new-password" minLength={8} required />
                  <small className="form-text">Eight characters at the very least.</small>
                </label>
                <label className="field">
                  New password again
                  <input className="form-control" name="confirmPassword" type="password" autoComplete="new-password" minLength={8} required />
                </label>
                {notice && <DismissibleMessage className="alert alert-success" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
                {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
                <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Set My Password'}</Button>
                <button className="btn btn-link text-body-secondary" type="button" onClick={() => setResetStep('asking')}>Send another code</button>
                <button className="btn btn-link text-body-secondary" type="button" onClick={leaveReset}>Back to signing in</button>
              </form>}
          </>
          : discordTicket
          ? <>
            <p className="text-body-secondary">
              Signed in as <strong className="text-primary">{discordTicket.discordUsername}</strong> on Discord.
              One thing left before you have an empire.
            </p>
            <form className="d-grid gap-3 mt-4" onSubmit={finishDiscordSignUp}>
              <label className="field">
                Name
                <input className="form-control" name="username" defaultValue={discordTicket.suggestedUsername} minLength={3} maxLength={32} required />
                <small className="form-text">
                  What other players see, and what you would sign in as if you ever set a password.
                </small>
              </label>
              <p className="text-body-secondary small mb-0">Everyone starts in New York. You can move later from the Travel panel.</p>
              {providers.betaKeyRequired && <label className="field">
                Beta key
                <input className="form-control tnum" name="betaKey" placeholder="SE-4K7XQ-9MTBH" required />
                <small className="form-text">The dash does not matter. Paste it however you were given it.</small>
              </label>}
              {/*
                Offered here rather than left to the account page, because an account made this way has
                no password and no address: Discord is the only way in, and losing the Discord loses the
                empire. This is the one moment the player is already filling in a form, which makes it
                the cheapest moment to hand them a second way back.
              */}
              <label className="field">
                Email <span className="text-body-tertiary">(optional)</span>
                <input className="form-control" name="email" type="email" maxLength={254} />
                <small className="form-text">
                  Without one, Discord is the only way into this empire and there is no way back if you
                  lose it. You can add one later on the Account page.
                </small>
              </label>
              {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
              <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Build My Empire'}</Button>
              <button className="btn btn-link text-body-secondary" type="button" onClick={abandonDiscordSignUp}>Use a username and password instead</button>
            </form>
          </>
          : <>
            <p className="text-body-secondary">Old-school browser strategy, rebuilt.</p>
            {/*
              Bootstrap's pill nav rather than the hand-rolled pair of buttons. It is the same two
              controls, and it brings the roles and the active state with it.
            */}
            <ul className="nav nav-pills gap-2 my-4" role="tablist">
              <li className="nav-item" role="presentation">
                <button
                  className={`nav-link px-3 py-2 ${authMode === 'login' ? 'active' : ''}`}
                  type="button"
                  role="tab"
                  aria-selected={authMode === 'login'}
                  onClick={() => setAuthMode('login')}
                >Login</button>
              </li>
              <li className="nav-item" role="presentation">
                <button
                  className={`nav-link px-3 py-2 ${authMode === 'register' ? 'active' : ''}`}
                  type="button"
                  role="tab"
                  aria-selected={authMode === 'register'}
                  onClick={() => setAuthMode('register')}
                >Create Account</button>
              </li>
            </ul>
            <form className="d-grid gap-3" onSubmit={auth}>
              {/*
                One box either way, and for two different reasons. Signing in, what is in it is decided
                by the @, server-side - which is why the limit there is an address's rather than a
                name's. Signing up, it is the only name asked for: it becomes the sign-in name and the
                name on the leaderboard, which every account here had set to the same string anyway
                back when the form asked twice and never said how the two differed.
              */}
              <label className="field">
                {authMode === 'login' ? 'Username or Email' : 'Name'}
                <input
                  className="form-control"
                  name="username"
                  minLength={3}
                  maxLength={authMode === 'login' ? 254 : 32}
                  required
                />
                {authMode === 'register' && <small className="form-text">
                  What you sign in with, and the name other players see on the leaderboard.
                </small>}
              </label>
              {/*
                Required on this door, and the helper says why rather than just marking it with a star.
                An account made here has one way in and one way back, and the way back is a code to this
                address - without it, one forgotten password ends the empire.

                The old copy here said "nothing is ever sent to it", which was true the day it was
                written and stopped being true the day codes started going out.
              */}
              {authMode === 'register' && <label className="field">
                Email
                <input className="form-control" name="email" type="email" maxLength={254} required />
                <small className="form-text">
                  A second name to sign in under, and the way back in if the password goes. A code comes
                  to confirm it, and a note lands there if a way in ever changes.
                </small>
              </label>}
              {authMode === 'register' && <p className="text-body-secondary small mb-0">Everyone starts in New York. You can move later from the Travel panel.</p>}
              {authMode === 'register' && providers.betaKeyRequired && <label className="field">
                Beta key
                <input className="form-control tnum" name="betaKey" placeholder="SE-4K7XQ-9MTBH" required />
                <small className="form-text">The dash does not matter. Paste it however you were given it.</small>
              </label>}
              <label className="field">Password<input className="form-control" name="password" type="password" minLength={8} required /></label>
              {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
              {notice && <DismissibleMessage className="alert alert-success" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
              <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : authMode === 'login' ? 'Enter the City' : 'Build My Empire'}</Button>
              {/* Only on the login side. Offering it while somebody is creating an account is offering
                  to reset a password they have not chosen yet. */}
              {authMode === 'login' && <>
                <button
                  className="btn btn-link text-body-secondary"
                  type="button"
                  onClick={() => { setResetStep('asking'); setError(''); setNotice('') }}
                >Forgotten your password?</button>
                {/* The way in for somebody who has lost the mailbox as well as the password, which is
                    the case the emailed code cannot answer at all. */}
                <button
                  className="btn btn-link text-body-secondary"
                  type="button"
                  onClick={() => { setResetStep('code'); setError(''); setNotice('') }}
                >Use a recovery code</button>
              </>}
            </form>
            {/*
              A real link rather than a button, because this is a full-page navigation to somebody
              else's site and back. A fetch cannot make that trip; the browser has to carry it.
            */}
            {providers.discord && <>
              <div className="d-flex align-items-center gap-3 my-3 text-body-tertiary">
                <hr className="flex-fill my-0" /><small className="eyebrow">or</small><hr className="flex-fill my-0" />
              </div>
              {/* One button, one round trip, and it works out for itself whether the identity coming
                  back belongs to somebody. It still says which of the two the player came here to do,
                  because "Continue" under a Create Account tab reads like it will not make one. */}
              <a className="btn btn-secondary d-flex align-items-center justify-content-center gap-2" href={discordStartUrl()}>
                <i className="bi bi-discord" aria-hidden="true" />
                {authMode === 'login' ? 'Sign in with Discord' : 'Create Account with Discord'}
              </a>
              {authMode === 'register' && <small className="form-text mt-2">
                Signing up with Discord does not need an email - the Discord account is your way back in.
              </small>}
            </>}
          </>}
      </section>
    </main>
  }

  const totalCrew = dashboard.pimps + dashboard.hoes + dashboard.thugs
  const weaponCoverage = dashboard.thugs === 0 ? 100 : Math.min(100, (dashboard.weapons / dashboard.thugs) * 100)
  const managementCapacity = dashboard.crewReport.managementCapacity
  const modalUpdates = dashboard.updates.updates.filter(update => update.isNew && update.showOnce)
  const modalUpdateStamp = modalUpdates[0]?.publishedAtUtc ?? null
  const contextualUpdate = activePage === 'updates'
    ? null
    : dashboard.updates.updates.find(update =>
      (update.isNew || update.isPinned)
      && update.actionUrl
      && updateActionPage(update.actionUrl) === activePage) ?? null
  const showUpdatesDialog = Boolean(
    modalUpdates.length > 0
    && modalUpdateStamp
    && modalUpdateStamp !== dismissedUpdateStamp
    && !catchUp
    && tourStep === null)
  const visiblePages = (Object.keys(pageMeta) as AppPage[]).filter(page => page !== 'admin' || adminOverview)
  const contentContext: PageContext = {
    dashboard,
    adminOverview,
    leaders,
    cityLeaders,
    targets,
    selectedTarget,
    worldNews,
    combatLogs,
    combatMissions,
    targetQuery,
    busy,
    streetTurns,
    autoBuySupplies,
    productionTurns,
    hoeCut,
    bankAmount,
    crewQty,
    attackCrew,
    commanderId,
    attackMethod,
    poachCoke,
    borrowedThugs,
    district,
    storeQty,
    sellQty,
    nextTurn,
    totalCrew,
    weaponCoverage,
    managementCapacity,
    setActivePage,
    refresh,
    openTour: () => setTourStep(0),
    setTargetQuery,
    setStreetTurns,
    setAutoBuySupplies,
    setProductionTurns,
    setHoeCut,
    setBankAmount,
    setCrewQty,
    setAttackCrew,
    setCommanderId,
    setAttackMethod,
    setPoachCoke,
    setBorrowedThugs,
    setDistrict,
    setStoreQty,
    setSellQty,
    act,
    searchTargets,
    inspectTarget,
    attackTarget: defenderId => void attackTarget(defenderId),
    cancelMission: missionId => void act(() => api.cancelCombatMission(missionId)),
    seedBots: count => void act(() => api.adminSeedBots(count)),
    runBots: rounds => void act(() => api.adminRunBots(rounds)),
    setBotAutomation: (enabled, timing) => void act(() => api.adminSetBotAutomation(enabled, timing)),
  }

  // Waits for real data: a tour of empty panels teaches nothing, and the first dashboard is also the
  // first moment there is anything to point at. Whether it is due comes off the account rather than
  // out of browser storage, so it runs once after the account is made rather than once per browser.
  if (!tourOffered.current && dashboard?.walkthroughDue) {
    tourOffered.current = true
    queueMicrotask(() => setTourStep(0))
  }

  return <main className="game-shell d-grid">
    {catchUp && <CatchUpDialog news={catchUp} onClose={() => setCatchUp(null)} />}
    {showUpdatesDialog && modalUpdateStamp && <UpdatesDialog
      updates={modalUpdates}
      unread={modalUpdates.length}
      busy={busy}
      onClose={() => setDismissedUpdateStamp(modalUpdateStamp)}
      onRead={() => {
        setDismissedUpdateStamp(modalUpdateStamp)
        void act(api.markUpdatesSeen)
      }}
      onViewAll={() => {
        setDismissedUpdateStamp(modalUpdateStamp)
        setActivePage('updates')
      }}
      onOpenAction={page => {
        setDismissedUpdateStamp(modalUpdateStamp)
        setActivePage(page)
      }}
      onPage={setActivePage}
    />}
    {openProfileId && dashboard && <PlayerProfileDialog
      playerId={openProfileId}
      currentPlayerId={dashboard.playerId}
      onClose={() => setOpenProfileId(null)}
    />}
    <ChatWindows dashboard={dashboard} busy={busy} />
    {dashboard && <Walkthrough
      active={tourStep !== null}
      stepIndex={tourStep ?? 0}
      dashboard={dashboard}
      onPage={setActivePage}
      onStep={setTourStep}
      // Written through rather than remembered locally, and the refresh is what stops it reappearing
      // on the next dashboard poll - the flag it reads is on the row it just wrote.
      onClose={() => { setTourStep(null); void act(() => api.setWalkthroughSeen(true)) }}
    />}
    <MobileNav
      pages={visiblePages}
      active={activePage}
      onPick={setActivePage}
      onLogout={() => void act(api.logout)}
    />
    <aside className="app-nav d-none d-md-grid position-sticky top-0 gap-3 bg-body-tertiary border-end">
      <div className="nav-brand d-grid gap-1 border-bottom p-1 pb-3">
        <span className="d-grid place-items-center text-dark bg-primary fw-bolder rounded">SE</span>
        <strong className="">Street Empire</strong>
        <button className="btn btn-link btn-sm text-body-tertiary p-0 text-start" type="button" onClick={() => setActivePage('updates')}>
          StreetEmpire {__APP_VERSION__}
        </button>
      </div>
      <nav className="d-grid gap-1 align-content-start">
        {visiblePages.map(page => <button
          className={`nav-page btn d-grid gap-2 align-items-center text-start p-1 ${activePage === page ? 'active' : ''}`}
          key={page}
          type="button"
          /* Between the md and xl breakpoints the rail keeps the badge and drops the word to save
             room, which leaves a two-letter code standing on its own. The title says it on hover. */
          title={pageMeta[page].label}
          onClick={() => setActivePage(page)}
        >
          <span className={`d-grid place-items-center border rounded small fw-bolder ${activePage === page ? 'text-bg-primary border-primary' : ''}`}>{pageMeta[page].short}</span>
          <strong className="text-truncate">{pageMeta[page].label}</strong>
        </button>)}
      </nav>
      <button className="btn btn-outline-danger w-100" onClick={() => void act(api.logout)}>Logout</button>
    </aside>

    <section className="app-main min-w-0 mx-auto">
      <header className="command-header d-flex justify-content-between align-items-end gap-3 mb-3">
        <div className="min-w-0 flex-fill">
          <span className="eyebrow d-block text-truncate">{pageMeta[activePage].kicker}</span>
          <h1 className="mt-1 text-truncate">{pageMeta[activePage].label}</h1>
        </div>
        <div className="d-flex align-items-stretch gap-2 flex-shrink-0">
          <AlertBell unread={dashboard.unreadDefenceAlerts} onRead={() => void refresh()} />
          <div className="player-plate tnum d-grid justify-items-end gap-1 border rounded p-3">
            <strong className="text-primary">{dashboard.name}</strong>
            <span className="eyebrow text-end">{dashboard.city} / Rank #{dashboard.rank}</span>
          </div>
        </div>
      </header>

      <StatusStrip dashboard={dashboard} nextTurn={nextTurn} />
      {contextualUpdate && <ContextualUpdateCallout update={contextualUpdate} onPage={setActivePage} />}

      <section className="d-grid gap-2">
        {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
        {notice && <DismissibleMessage className="alert alert-success" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
        {/*
          The raw action breakdown: internal keys, unrounded figures, every field the endpoint
          happened to return. It is a debugging aid and reads like one, so only an admin sees it.
          Players get the summary sentence above, which is written for them.
        */}
        {lastBreakdown && dashboard.isAdmin && <div className="alert alert-info tnum d-flex align-items-center justify-content-between gap-3">
          <div className="min-w-0 d-flex flex-wrap gap-1">
            {Object.entries(lastBreakdown).filter(([, value]) => value !== 0 && value !== null).slice(0, 18).map(([key, value]) =>
              <span className="d-grid small" key={key}><strong className="eyebrow">{formatBreakdownKey(key)}</strong>{formatBreakdownValue(key, value)}</span>
            )}
          </div>
          <button className="btn-close flex-shrink-0" type="button" aria-label="Close breakdown" onClick={() => setLastBreakdown(null)} />
        </div>}
      </section>

      {renderPage(activePage, contentContext)}
    </section>
  </main>
}

type PageContext = {
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
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  searchTargets: (event: FormEvent<HTMLFormElement>) => void
  inspectTarget: (playerId: string) => void
  attackTarget: (defenderId: string) => void
  cancelMission: (missionId: number) => void
  seedBots: (count: number) => void
  runBots: (rounds: number) => void
  setBotAutomation: (enabled: boolean, timing?: { tickSeconds?: number, roundsPerTick?: number, resetTiming?: boolean }) => void
}

function renderPage(page: AppPage, ctx: PageContext) {
  switch (page) {
    case 'street': return <StreetPage {...ctx} />
    case 'crew': return <CrewPage {...ctx} />
    case 'market': return <MarketPage {...ctx} />
    case 'casino': return <CasinoPage {...ctx} />
    case 'recon': return <CombatPage {...ctx} />
    case 'seasons': return <SeasonsPage {...ctx} />
    case 'updates': return <UpdatesPage {...ctx} />
    case 'alliance': return <AlliancePage {...ctx} />
    case 'account': return <AccountPage {...ctx} />
    case 'admin': return ctx.adminOverview
      ? <AdminPage {...ctx} overview={ctx.adminOverview} />
      : <OverviewPage {...ctx} />
    default: return <OverviewPage {...ctx} />
  }
}

/**
 * The clock everybody is playing against, and the shelf their trophies sit on.
 *
 * Both halves are here on purpose, because they are the same idea seen from either end: what a season
 * takes away is the empire, and what it never takes away is what you did with it. Showing the countdown
 * without the honours would be a threat; showing the honours without the countdown would be a museum.
 *
 * Renders nothing at all in a world where the operator has not turned seasons on and nobody has ever
 * finished one - an inert countdown to a date that will pass quietly is worse than no countdown.
 */
function SeasonPanel({ onPage }: { onPage: (page: AppPage) => void }) {
  const [season, setSeason] = useState<Season | null>(null)
  useEffect(() => {
    let live = true
    void api.season().then(value => { if (live) setSeason(value) }).catch(() => {})
    return () => { live = false }
  }, [])

  useSecondHand(season?.enabled === true)

  if (!season) return null
  if (!season.enabled && season.honours.length === 0) return null

  return <section className="card p-3">
    <div className="panel-title">
      <h2>{season.name}</h2>
      <span>{season.enabled ? `${timeLeft(season.endsAtUtc)} left` : 'Running on'}</span>
    </div>
    {season.enabled
      ? <p>
        This season is a raid race: cash, weed, and coke taken from other players decide the table.
        Everything you have built goes back to day one when this runs out, and the finish stays. Finish top and next season opens with{' '}
        {money.format(season.championHeadStart)} on account - top three{' '}
        {money.format(season.topThreeHeadStart)}, top ten {money.format(season.topTenHeadStart)}. It is
        paid once, off this season alone, and against a Warehouse it is a rounding error.
      </p>
      : <p className="text-body-tertiary small">
        Seasons are not running on this world. Nothing resets, and the date above is only a marker.
      </p>}

    {season.honours.length > 0 && <div className="d-grid gap-1 mt-2">
      <strong className="d-block text-primary small">What you have won</strong>
      {season.honours.map(honour => <div key={honour.number} className="d-flex justify-content-between align-items-baseline gap-2 border-top py-1">
        <small className={honour.honour ? 'text-warning-emphasis' : 'text-body-secondary'}>
          {honour.name}: {honour.honour ?? `finished #${honour.rank}`}
        </small>
        <small className="text-body-tertiary tnum">{money.format(honour.raidScore)}</small>
      </div>)}
    </div>}

    {/*
      Ten names off the last season used to sit here, and that was the whole of the archive this game
      had. It is a page now, so this is a door to it rather than a tenth of it.
    */}
    <button className="btn btn-secondary btn-sm mt-3" type="button" onClick={() => onPage('seasons')}>
      {season.lastSeasonName ? `Standings, and how ${season.lastSeasonName} finished` : 'Standings and the season record'}
    </button>
  </section>
}

/**
 * A second hand, so a countdown moves while somebody is looking at it rather than only when something
 * else happens to redraw the page.
 */
function useSecondHand(active: boolean) {
  const [, setTick] = useState(0)
  useEffect(() => {
    if (!active) return
    const timer = window.setInterval(() => setTick(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [active])
}

/**
 * Seasons, in the round: the clock everybody is playing against, the board they are playing on, and
 * every season the world has already finished.
 *
 * All of this was one card on the dashboard showing a countdown, ten names off the last season, and
 * nothing else - which is a strange amount of room to give the frame the entire game sits inside. A
 * season is what the climb is measured in: what it takes, what it never takes, how far through it is,
 * who is winning it today, and who won the ones before.
 *
 * Three tabs because there are three genuinely different questions - where does the world stand right
 * now, how did the ones before it end, and what have I got to show for any of it - and trying to
 * answer all three in one card on the dashboard is how it ended up answering none of them.
 */
const SEASON_TABS = ['now', 'past', 'you'] as const

function SeasonsPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('seasons', SEASON_TABS, 'now')
  const [season, setSeason] = useState<Season | null>(null)
  const [shelf, setShelf] = useState<SeasonArchiveEntry[] | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let live = true
    void Promise.all([api.season(), api.seasons()])
      .then(([current, all]) => { if (!live) return; setSeason(current); setShelf(all) })
      .catch(() => { if (live) setFailed(true) })
    return () => { live = false }
  }, [])

  return <div className="d-grid gap-3">
    <SectionTabs
      label="Season sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'now', label: 'This Season' },
        { key: 'past', label: 'Finished' },
        { key: 'you', label: 'Your Record' },
      ]}
    />
    {failed && <p className="alert alert-danger mb-0">The season record would not load. The standings are live either way.</p>}
    {tab === 'now' && <ThisSeasonTab ctx={ctx} season={season} now={shelf?.find(entry => entry.running) ?? null} />}
    {tab === 'past' && <FinishedSeasonsTab shelf={shelf} you={ctx.dashboard.playerId} />}
    {tab === 'you' && <YourRecordTab season={season} name={ctx.dashboard.name} />}
  </div>
}

/*
  What a roll does, said as two lists rather than as a paragraph.

  The rule is one sentence - the empire goes and the person stays - and somebody about to lose a month
  of work does not want the sentence, they want the inventory. Held here rather than inline because the
  two columns are the same shape, and the whole point of them is being read against each other.
*/
const SEASON_KEEPS = [
  'Your account and how you sign in',
  'Your player name and your town',
  'Your alliance and who you run with',
  'Every honour you have ever won',
  'Every season result ever recorded',
]

const SEASON_TAKES = [
  'Cash and bank',
  'Your pimps, hoes, thugs, and named roster reset to the starting crew',
  'The building and every room in it',
  'All stock, at whatever it was worth',
  'All held ground, and the work put into it',
  'Every combat clock and shield',
  'The alliance treasury and its thug pool',
]

function ThisSeasonTab({ ctx, season, now }: {
  ctx: PageContext
  season: Season | null
  now: SeasonArchiveEntry | null
}) {
  const { dashboard } = ctx
  const yourRaidRow = season?.currentStandings.find(row => row.playerId === dashboard.playerId && row.raidScore > 0) ?? null
  useSecondHand(season?.enabled === true)

  return <div className="d-grid gtc-1 gtc-xl-split-108 gap-3 align-items-start">
    <div className="d-grid gap-3 align-items-start">
      {season && <section className="card p-3">
        <div className="panel-title">
          <h2>{season.name}</h2>
          <span>{season.enabled ? `${timeLeft(season.endsAtUtc)} left` : 'No end date'}</span>
        </div>

        {season.enabled
          ? <>
            <SeasonProgress season={season} />
            <div className="tnum d-grid gtc-fill-140 gap-2 mt-3">
              <AdminMetric label="Day" value={`${dayOfSeason(season)} of ${season.lengthDays}`} />
              <AdminMetric
                label="Ends"
                value={new Date(season.endsAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short' })}
                sub={new Date(season.endsAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              />
              <AdminMetric label="Empires in it" value={now ? number.format(now.players) : '-'} />
              <AdminMetric
                label="Your raid rank"
                value={yourRaidRow ? `#${yourRaidRow.rank}` : 'Unranked'}
                sub={yourRaidRow ? `${money.format(yourRaidRow.raidScore)} taken` : 'No raid of yours has scored yet'}
              />
            </div>
          </>
          : <p className="text-body-tertiary mt-3 mb-0">
            Seasons are not running on this world. Nothing resets, the date on the clock is only a
            marker, and the raid board is only a record of what has happened so far.
          </p>}
      </section>}

      <section className="card p-3">
        <div className="panel-title"><h2>What a roll does</h2><span>The empire goes, the person stays</span></div>
        <div className="d-grid gtc-1 gtc-md-2 gap-3 mt-3">
          <div className="d-grid gap-1 align-content-start">
            <strong className="text-success-emphasis small">Comes through untouched</strong>
            {SEASON_KEEPS.map(item => <small className="text-body-secondary border-top py-1" key={item}>{item}</small>)}
          </div>
          <div className="d-grid gap-1 align-content-start">
            <strong className="text-danger-emphasis small">Goes back to day one</strong>
            {SEASON_TAKES.map(item => <small className="text-body-secondary border-top py-1" key={item}>{item}</small>)}
          </div>
        </div>
      </section>

      {season && <section className="card p-3">
        <div className="panel-title"><h2>What finishing well is worth</h2><span>Opening cash, next season</span></div>
        <StatusRow label="Champion" value={money.format(season.championHeadStart)} />
        <StatusRow label="Top three" value={money.format(season.topThreeHeadStart)} />
        <StatusRow label="Top ten" value={money.format(season.topTenHeadStart)} />
        {/* The run itself, which is the part worth protecting and the reason to keep playing a season
            you have already won. Only shown to somebody who has one - a zero here is noise. */}
        {season.yourTopTenStreak > 0 && <div className="d-grid gap-1 mt-3 border border-primary rounded bg-body-tertiary px-3 py-2">
          <span className="eyebrow text-primary">Your run</span>
          <strong className="fs-5">{money.format(season.yourHeadStart)}</strong>
          <small className="text-body-secondary lh-sm">
            Stacked over {number.format(season.yourTopTenStreak)} season
            {season.yourTopTenStreak === 1 ? '' : 's'} running in the top ten. Finish in the top ten
            again and this season's prize is added to it. Finish outside it, even once, and all of it
            goes.
          </small>
        </div>}
        <p className="text-body-tertiary small mt-3 mb-0">
          These stack. Finish in the top ten and what you won is added to whatever your last run was
          worth, season after season - and the whole pile is emptied the first time you finish outside
          it, whether you came eleventh or last. A long run is the biggest prize in the game and it is
          never more than one ordinary season from nothing.
        </p>
      </section>}
    </div>

    {/* The live board: this season's raid table, and the table it will finish on. */}
    <section className="card p-3">
      <SeasonRaidBoard rows={season?.currentStandings ?? []} you={dashboard.playerId} />
    </section>
  </div>
}

/** <param name="you">Your player id. Rows are matched on it, never on a name two empires can share.</param> */
function SeasonRaidBoard({ rows, you }: { rows: SeasonStanding[], you: string }) {
  const scored = rows.filter(row => row.raidScore > 0)
  return <>
    <div className="panel-title">
      <h2>Raid Take</h2>
      <span>{scored.length > 0 ? 'Cash and product stolen' : 'No raids scored yet'}</span>
    </div>
    {scored.length === 0
      ? <p className="text-body-tertiary small mt-3 mb-0">No completed raids have put money or product on the season board yet.</p>
      : <div className="leaderboard tnum d-grid overflow-y-auto mt-3">
        {scored.map(row => <SeasonRow key={row.rank} row={row} mine={row.playerId === you} />)}
      </div>}
  </>
}

/** How far through the season is, as a bar rather than two dates to subtract in your head. */
function SeasonProgress({ season }: { season: Season }) {
  const start = new Date(season.startedAtUtc).getTime()
  const end = new Date(season.endsAtUtc).getTime()
  const percent = end <= start ? 100 : Math.min(100, Math.max(0, ((Date.now() - start) / (end - start)) * 100))
  return <div
    className="progress mt-3"
    role="progressbar"
    aria-label="Season progress"
    aria-valuenow={Math.round(percent)}
    aria-valuemin={0}
    aria-valuemax={100}
  >
    <div className="progress-bar bg-primary" style={{ width: `${Math.max(2, percent)}%` }} />
  </div>
}

/** Day one is the day it opened, not day zero. Capped, because a season can sit past its end date. */
function dayOfSeason(season: Season) {
  const elapsed = Date.now() - new Date(season.startedAtUtc).getTime()
  return Math.min(season.lengthDays, Math.max(1, Math.floor(elapsed / 86_400_000) + 1))
}

/**
 * The seasons that have ended, and one of them in full.
 *
 * A list beside a table rather than a table per season down one column: the archive only ever grows,
 * and the question is nearly always about one particular season.
 */
/** <param name="you">Your player id, passed down to the table so a row knows whether it is yours.</param> */
function FinishedSeasonsTab({ shelf, you }: { shelf: SeasonArchiveEntry[] | null, you: string }) {
  const [picked, setPicked] = useState<number | null>(null)
  const [table, setTable] = useState<SeasonTable | null>(null)
  const [loading, setLoading] = useState(false)

  const finished = (shelf ?? []).filter(entry => !entry.running)
  const chosen = picked ?? finished[0]?.number ?? null

  useEffect(() => {
    if (chosen === null) return
    let live = true
    setLoading(true)
    void api.seasonTable(chosen)
      .then(value => { if (live) setTable(value) })
      .catch(() => { if (live) setTable(null) })
      .finally(() => { if (live) setLoading(false) })
    return () => { live = false }
  }, [chosen])

  if (shelf === null) return <p className="text-body-tertiary mb-0">Reading the record.</p>

  if (finished.length === 0) return <section className="card p-3">
    <div className="panel-title"><h2>Nothing has finished yet</h2><span>The world is on its first</span></div>
    <p className="mt-3 mb-0">
      When this season ends, everybody in it gets a line here - where they came, what they took in raids,
      what town they did it in and what the season was called. Written for everybody rather than only
      the top, because a season somebody came fortieth in is still a season they played.
    </p>
  </section>

  return <div className="d-grid gtc-1 gtc-xl-split-280 gap-3 align-items-start">
    <section className="card p-3">
      <div className="panel-title"><h2>Seasons</h2><span>{finished.length} finished</span></div>
      <div className="d-grid gap-2 mt-3">
        {finished.map(entry => <button
          className={`btn btn-secondary d-grid gap-1 text-start ${chosen === entry.number ? 'border-primary text-primary' : ''}`}
          key={entry.number}
          type="button"
          aria-current={chosen === entry.number ? 'true' : undefined}
          onClick={() => setPicked(entry.number)}
        >
          <span className="d-flex justify-content-between align-items-baseline gap-2">
            <strong className="min-w-0 text-truncate">{entry.name}</strong>
            <small className="text-body-tertiary flex-shrink-0">
              {entry.endedAtUtc ? new Date(entry.endedAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' }) : ''}
            </small>
          </span>
          <small className="text-body-tertiary text-truncate">
            {entry.championName ? `${entry.championName} took ${money.format(entry.championRaidScore)}` : 'Nobody was in it'} / {number.format(entry.players)} finished
          </small>
          {/* The line that makes the archive worth opening for somebody who never came top ten. */}
          {typeof entry.yourRank === 'number' && <small className={entry.yourHonour ? 'text-warning-emphasis' : 'text-body-secondary'}>
            You finished #{entry.yourRank}{entry.yourHonour ? ` - ${entry.yourHonour}` : ''}
          </small>}
        </button>)}
      </div>
    </section>

    <section className="card p-3">
      {table
        ? <SeasonFinalTable table={table} you={you} />
        : <p className="text-body-tertiary mb-0">{loading ? 'Reading the table.' : 'That season has no table on the record.'}</p>}
    </section>
  </div>
}

function SeasonFinalTable({ table, you }: { table: SeasonTable, you: string }) {
  const rows = table.table
  const yours = table.you

  return <>
    <div className="panel-title">
      <h2>{table.name}</h2>
      <span>{table.endedAtUtc ? `Ended ${new Date(table.endedAtUtc).toLocaleDateString()}` : 'Still running'}</span>
    </div>

    <div className="tnum d-grid gtc-fill-140 gap-2 mt-3">
      <AdminMetric label="Finished" value={number.format(table.players)} sub="empires in it" />
      <AdminMetric
        label="Won by"
        value={rows[0]?.playerName ?? '-'}
        sub={rows[0] ? money.format(rows[0].raidScore) : undefined}
      />
      <AdminMetric
        label="You"
        value={yours ? `#${yours.rank}` : '-'}
        sub={yours ? (yours.honour ?? 'a season played') : 'you were not in this one'}
      />
    </div>

    {rows.length === 0
      ? <p className="text-body-tertiary small mt-3 mb-0">No table was written for this one.</p>
      : <div className="leaderboard tnum d-grid overflow-y-auto mt-3">
        {rows.map(row => <SeasonRow key={row.rank} row={row} mine={row.playerId === you} />)}
      </div>}

    {/* The page stops at a hundred; the record does not. Somebody past it still gets their own line. */}
    {yours && !rows.some(row => row.rank === yours.rank) && <div className="mt-3">
      <strong className="d-block text-body-secondary small">Your line, past the end of the table above</strong>
      <div className="tnum d-grid"><SeasonRow row={yours} mine /></div>
    </div>}

    {rows.length >= 100 && <p className="text-body-tertiary small mt-2 mb-0">
      The first hundred of {number.format(table.players)}. Every finish is on the record whether or not
      it is on this page.
    </p>}
  </>
}

function SeasonRow({ row, mine }: { row: SeasonStanding, mine: boolean }) {
  return <div className={`leader d-grid gap-2 p-2 border-top ${mine ? 'bg-success-subtle' : ''}`}>
    <span className="text-body-secondary">#{row.rank}</span>
    <span className="d-grid min-w-0">
      <strong className="min-w-0 text-truncate">{row.playerName}</strong>
      <small className="text-body-tertiary text-truncate">{row.crewName ? `${row.crewName} / ` : ''}{row.city}</small>
    </span>
    <span className="d-grid justify-items-end gap-1">
      <span className="text-body-secondary">{money.format(row.raidScore)}</span>
      <small className="text-body-tertiary">{raidTake(row)}</small>
      <HonourBadge honour={row.honour} />
    </span>
  </div>
}

function raidTake(row: SeasonStanding) {
  return `${money.format(row.raidCashTaken)} / ${number.format(row.raidWeedTaken)} weed / ${number.format(row.raidCokeTaken)} coke`
}

/**
 * The three finishes worth a name, and nothing for the rest.
 *
 * Kept few on purpose: an honour everybody has is a participation sticker, and the point of these is
 * that they are the only thing a reset does not take.
 */
function HonourBadge({ honour }: { honour?: string | null }) {
  if (!honour) return null
  const tone = honour === 'Champion'
    ? 'text-bg-warning'
    : honour === 'Top Three' ? 'text-bg-light border' : 'border text-body-secondary'
  return <span className={`badge rounded-pill ${tone}`}>{honour}</span>
}

/** What somebody has to show for every season they have been through. The half of the game that lasts. */
function YourRecordTab({ season, name }: { season: Season | null, name: string }) {
  if (!season) return <p className="text-body-tertiary mb-0">Reading the record.</p>

  const honours = season.honours
  const best = honours.reduce<number | null>((low, x) => low === null || x.rank < low ? x.rank : low, null)
  const championships = honours.filter(x => x.honour === 'Champion').length
  const topTens = honours.filter(x => x.rank <= 10).length

  return <div className="d-grid gap-3">
    <section className="card p-3">
      <div className="panel-title">
        <h2>{name}</h2>
        <span>{honours.length === 0 ? 'No seasons finished' : `${honours.length} season${honours.length === 1 ? '' : 's'} finished`}</span>
      </div>
      {honours.length === 0
        ? <p className="mt-3 mb-0">
          You have not been through a roll yet. When this season ends you get a line here - where you
          came, what you took in raids, and what it was called - and it stays there through every season
          after it, which is more than anything else you own can say.
        </p>
        : <div className="tnum d-grid gtc-fill-140 gap-2 mt-3">
          <AdminMetric label="Seasons" value={number.format(honours.length)} />
          <AdminMetric label="Best finish" value={best === null ? '-' : `#${best}`} />
          <AdminMetric label="Championships" value={number.format(championships)} />
          <AdminMetric label="Top ten finishes" value={number.format(topTens)} />
        </div>}
    </section>

    {honours.length > 0 && <section className="card p-3">
      <div className="panel-title"><h2>Every finish</h2><span>Newest first</span></div>
      <div className="d-grid mt-3">
        {honours.map(honour => <div className="d-flex justify-content-between align-items-baseline gap-2 border-top py-2" key={honour.number}>
          <span className="d-grid min-w-0">
            <strong className="min-w-0 text-truncate">{honour.name}</strong>
            <small className="text-body-tertiary">
              #{honour.rank}{honour.endedAtUtc ? ` / ended ${new Date(honour.endedAtUtc).toLocaleDateString()}` : ''}
            </small>
          </span>
          <span className="d-grid justify-items-end gap-1 flex-shrink-0">
            <span className="tnum text-body-secondary">{money.format(honour.raidScore)}</span>
            <HonourBadge honour={honour.honour} />
          </span>
        </div>)}
      </div>
    </section>}
  </div>
}

function OverviewPage(ctx: PageContext) {
  const { dashboard, leaders, worldNews, totalCrew, weaponCoverage, managementCapacity, busy, act, setActivePage } = ctx
  return <div className="d-grid gtc-1 gtc-xl-split-108 gap-3 align-items-start">
    <div className="d-grid gap-3 align-items-start">
      <section className="card p-3 hero-panel d-grid align-content-between">
        <span className="eyebrow">Empire Snapshot</span>
        <h2 className="fs-1 my-2 mb-3">{dashboard.name}</h2>
        <div className="tnum d-grid gtc-1 gtc-md-3 gap-2">
          <AdminMetric label="Net worth" value={money.format(dashboard.netWorth)} />
          <AdminMetric label="Crew" value={number.format(totalCrew)} />
          <AdminMetric label="Turns" value={`${dashboard.turns}/${dashboard.maxTurns}`} />
        </div>
        <div className="d-flex flex-wrap gap-2 mt-4">
          <button className="btn btn-primary" onClick={() => setActivePage('street')}>Work Streets</button>
          <button className="btn btn-secondary" onClick={() => setActivePage('crew')}>Manage Crew</button>
          <button className="btn btn-secondary" onClick={() => setActivePage('market')}>Open Business</button>
          <button className="btn btn-secondary" onClick={() => setActivePage('casino')}>Hit Casino</button>
          <button className="btn btn-secondary" onClick={() => setActivePage('recon')}>Raids & Map</button>
        </div>
      </section>

      <SeasonPanel onPage={setActivePage} />
      <NextMovePanel dashboard={dashboard} onPage={setActivePage} />
      <UpdatesPanel updates={dashboard.updates.updates} unread={dashboard.updates.unreadCount} busy={busy} act={act} onPage={setActivePage} />
      <OpeningLadderPanel dashboard={dashboard} onPage={setActivePage} />

      <TravelPanel markets={dashboard.cityMarkets} turns={dashboard.turns} travel={dashboard.travel} busy={busy} act={act} />
    </div>

    <div className="d-grid gap-3 align-items-start">
      <section className="card p-3">
        <div className="panel-title"><h2>Readiness</h2><span>Combat prep</span></div>
        <StatusRow
          label="Hoe morale"
          value={`${dashboard.hoeHappiness.toFixed(0)}%`}
          warn={dashboard.hoeHappiness < 40}
          trend={<MoraleArrow trend={dashboard.moraleTrend} crew="hoe" />}
        />
        <StatusRow
          label="Thug morale"
          value={`${dashboard.thugHappiness.toFixed(0)}%`}
          warn={dashboard.thugHappiness < 40}
          trend={<MoraleArrow trend={dashboard.moraleTrend} crew="thug" />}
        />
        <StatusRow label="Management" value={`${dashboard.hoes}/${managementCapacity} hoes`} warn={dashboard.hoes > managementCapacity} />
        <StatusRow label="Armed thugs" value={`${Math.min(dashboard.weapons, dashboard.thugs)}/${dashboard.thugs}`} warn={dashboard.weapons < dashboard.thugs} />
        <StatusRow label="Weapon coverage" value={`${weaponCoverage.toFixed(0)}%`} warn={weaponCoverage < 75} />
        <StatusRow label="Combat status" value={dashboard.combatStatus.eligibility} warn={dashboard.combatStatus.isProtected} />
        <StatusRow label="Crew heat" value={`${heatAmount(dashboard.crewReport.crewHeat)} heat`} warn={dashboard.hideout.heatLabel !== 'Quiet' && dashboard.crewReport.crewHeat > dashboard.hideout.heat / 2} />
        <StatusRow label="Condoms for a full shift" value={`${dashboard.condoms}/${dashboard.crewReport.condomsNeededForMaxStreetAction}`} warn={dashboard.condoms < dashboard.crewReport.condomsNeededForMaxStreetAction} />
        <StatusRow label="Beer for a full shift" value={`${dashboard.beer}/${dashboard.crewReport.beerNeededForMaxStreetAction}`} warn={dashboard.beer < dashboard.crewReport.beerNeededForMaxStreetAction} />
        <StatusRow
          label="Hourly keep"
          value={hourlyUpkeepLabel(dashboard)}
          warn={hourlyUpkeepWarn(dashboard)}
        />
      </section>

      {/* Directly under readiness, because the last two readiness rows are counts of these same piles. */}
      <section className="card p-3">
        <div className="panel-title"><h2>Inventory</h2><span>On hand</span></div>
        <MiniInventory dashboard={dashboard} />
      </section>

      <section className="card p-3">
        <StandingsPanel dashboard={dashboard} leaders={leaders} cityLeaders={ctx.cityLeaders} limit={8} />
      </section>
    </div>

    <WorldNewsPanel news={worldNews} currentPlayerId={dashboard.playerId} />
  </div>
}

function UpdatesPage(ctx: PageContext) {
  const { dashboard, busy, act, setActivePage } = ctx
  const [feed, setFeed] = useState<GameUpdates | null>(null)
  const [category, setCategory] = useState<GameAnnouncement['category'] | 'All'>('All')
  const [severity, setSeverity] = useState<GameAnnouncement['severity'] | 'All'>('All')
  const [newOnly, setNewOnly] = useState(false)
  const [query, setQuery] = useState('')
  const [error, setError] = useState('')
  const source = feed ?? dashboard.updates

  const load = async () => {
    try {
      setFeed(await api.updates())
      setError('')
    } catch (e) {
      setError((e as Error).message)
    }
  }
  useEffect(() => { void load() }, [])

  const updates = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return source.updates.filter(update =>
      (category === 'All' || update.category === category)
      && (severity === 'All' || update.severity === severity)
      && (!newOnly || update.isNew)
      && (needle.length === 0
        || update.title.toLowerCase().includes(needle)
        || update.body.toLowerCase().includes(needle)
        || (update.version ?? '').toLowerCase().includes(needle)
        || (update.added ?? '').toLowerCase().includes(needle)
        || (update.changed ?? '').toLowerCase().includes(needle)
        || (update.fixed ?? '').toLowerCase().includes(needle)
        || (update.knownIssues ?? '').toLowerCase().includes(needle)))
  }, [source.updates, category, severity, newOnly, query])

  const markRead = async () => {
    await act(api.markUpdatesSeen)
    await load()
  }

  return <div className="d-grid gtc-1 gtc-xl-split-108 gap-3 align-items-start">
    <section className="card p-3">
      <div className="panel-title">
        <h2>Updates</h2>
        <span>{source.unreadCount > 0 ? `${source.unreadCount} new` : 'Caught up'}</span>
      </div>
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      <div className="d-grid gap-3">
        {updates.length === 0
          ? <p className="text-body-tertiary small mb-0">Nothing matches those filters.</p>
          : updates.map(update => <UpdateArticle update={update} onPage={setActivePage} key={update.id} />)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Filter</h2><span>{updates.length} shown</span></div>
      <label className="field">
        Search
        <input className="form-control" value={query} onChange={event => setQuery(event.target.value)} placeholder="Patch, rival, raid..." />
      </label>
      <div className="d-grid gtc-1 gtc-md-2 gap-3 mt-3">
        <label className="field">
          Category
          <select className="form-select" value={category} onChange={event => setCategory(event.target.value as GameAnnouncement['category'] | 'All')}>
            {(['All', ...updateCategories] as const).map(value => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>
        <label className="field">
          Severity
          <select className="form-select" value={severity} onChange={event => setSeverity(event.target.value as GameAnnouncement['severity'] | 'All')}>
            {(['All', ...updateSeverities] as const).map(value => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>
      </div>
      <label className="form-check form-switch d-flex align-items-center gap-2 mt-3">
        <input className="form-check-input" type="checkbox" checked={newOnly} onChange={event => setNewOnly(event.target.checked)} />
        <span>New only</span>
      </label>
      <div className="d-flex flex-wrap gap-2 mt-3">
        <Button className="btn btn-secondary btn-sm" type="button" blocked={firstReason(
          busy && BUSY,
          source.unreadCount === 0 && 'Nothing here is unread.',
        )} onClick={() => void markRead()}>
          Mark all read
        </Button>
        <button className="btn btn-secondary btn-sm" type="button" onClick={() => void load()}>
          Refresh
        </button>
      </div>
    </section>
  </div>
}

function UpdatesDialog({ updates, unread, busy, onClose, onRead, onViewAll, onOpenAction, onPage }: {
  updates: GameAnnouncement[]
  unread: number
  busy: boolean
  onClose: () => void
  onRead: () => void
  onViewAll: () => void
  onOpenAction: (page: AppPage) => void
  onPage: (page: AppPage) => void
}) {
  const actionUpdate = updates.find(update => update.actionUrl && updateActionPage(update.actionUrl))
  const actionPage = actionUpdate?.actionUrl ? updateActionPage(actionUpdate.actionUrl) : null
  return <div className="modal-backdrop-soft d-grid place-items-center position-fixed top-0 bottom-0 start-0 end-0 p-3" role="presentation">
    <section className="card p-3 shadow-lg update-dialog" role="dialog" aria-modal="true" aria-labelledby="updates-dialog-title">
      <div className="panel-title">
        <h2 id="updates-dialog-title">What Changed</h2>
        <span>{unread} new</span>
      </div>
      <div className="d-grid gap-3 mt-2">
        {updates.slice(0, 2).map(update => <UpdateArticle update={update} compact onPage={onPage} key={update.id} />)}
      </div>
      <div className="d-flex flex-wrap justify-content-between gap-2 mt-3">
        <button className="btn btn-secondary btn-sm" type="button" onClick={onClose}>Later</button>
        <div className="d-flex flex-wrap gap-2">
          <Button className="btn btn-secondary btn-sm" type="button" blocked={busy && BUSY} onClick={onRead}>Got it</Button>
          {actionUpdate && actionPage && <button className="btn btn-secondary btn-sm" type="button" onClick={() => onOpenAction(actionPage)}>{actionUpdate.actionLabel ?? 'Open'}</button>}
          <button className="btn btn-primary btn-sm" type="button" onClick={onViewAll}>View all</button>
        </div>
      </div>
    </section>
  </div>
}

function UpdatesPanel({ updates, unread, busy, act, onPage }: {
  updates: GameAnnouncement[]
  unread: number
  busy: boolean
  act: PageContext['act']
  onPage: (page: AppPage) => void
}) {
  return <section className={`card p-3 ${unread > 0 ? 'border-primary' : ''}`}>
    <div className="panel-title">
      <h2>Street Wire</h2>
      <span>{unread > 0 ? `${unread} new` : 'Caught up'}</span>
    </div>
    {updates.length === 0
      ? <p className="text-body-tertiary small mb-0">No updates posted yet.</p>
      : <div className="d-grid">
        {updates.map(update => <UpdateArticle update={update} compact onPage={onPage} key={update.id} />)}
      </div>}
    <div className="d-flex flex-wrap gap-2 mt-3">
      {unread > 0 && <Button
        className="btn btn-secondary btn-sm"
        type="button"
        blocked={busy && BUSY}
        onClick={() => void act(api.markUpdatesSeen)}
      >Mark read</Button>}
      <button className="btn btn-secondary btn-sm" type="button" onClick={() => onPage('updates')}>View all updates</button>
    </div>
  </section>
}

function UpdateArticle({ update, compact = false, onPage }: {
  update: GameAnnouncement
  compact?: boolean
  onPage: (page: AppPage) => void
}) {
  const sections = updateSections(update)
  return <article className={`feed-item py-3 border-top ${update.isPinned ? 'border-primary' : ''}`}>
    <div className="d-flex flex-wrap justify-content-between gap-2">
      <strong className={update.isNew ? 'text-primary' : 'text-body'}>{update.title}</strong>
      <span className="d-flex flex-wrap gap-1">
        {update.isNew && <span className="badge rounded-pill text-bg-light border">New</span>}
        {update.isPinned && <span className="badge rounded-pill text-bg-primary">Pinned</span>}
        <span className={`badge rounded-pill ${updateCategoryClass(update.category)}`}>{update.category}</span>
        <span className={`badge rounded-pill ${updateSeverityClass(update.severity)}`}>{update.severity}</span>
      </span>
    </div>
    <div className="d-flex flex-wrap gap-2 mt-1">
      {update.version && <small className="eyebrow text-body-tertiary">{update.version}</small>}
      <small className="text-body-tertiary">{new Date(update.publishedAtUtc).toLocaleString()}</small>
    </div>
    <p className={`${compact ? 'small' : ''} mt-1 mb-2 text-body-secondary preserve-lines`}>{compact ? clampText(update.body, 260) : update.body}</p>
    {!compact && sections.length > 0 && <div className="d-grid gtc-1 gtc-md-2 gap-2 my-2">
      {sections.map(section => <div className="border rounded bg-body-tertiary p-2" key={section.label}>
        <strong className="eyebrow d-block mb-1">{section.label}</strong>
        <p className="small mb-0 preserve-lines">{section.value}</p>
      </div>)}
    </div>}
    <div className="d-flex flex-wrap align-items-center gap-2">
      <UpdateAction update={update} onPage={onPage} />
    </div>
  </article>
}

function ContextualUpdateCallout({ update, onPage }: { update: GameAnnouncement, onPage: (page: AppPage) => void }) {
  return <div className={`alert d-flex flex-wrap align-items-center justify-content-between gap-2 ${update.severity === 'Maintenance' ? 'alert-danger' : update.severity === 'Warning' ? 'alert-warning' : 'alert-info'}`}>
    <span><strong>{update.version ?? 'Street Wire'}:</strong> {update.title}</span>
    <button className="btn btn-sm btn-secondary" type="button" onClick={() => onPage('updates')}>Read update</button>
  </div>
}

function UpdateAction({ update, onPage }: { update: GameAnnouncement, onPage: GoTo }) {
  if (!update.actionLabel || !update.actionUrl) return null
  const name = updateActionName(update.actionUrl)
  if (name)
    return <button className="btn btn-secondary btn-sm" type="button" onClick={() => goToFlow(onPage, name)}>{update.actionLabel}</button>
  return <a className="btn btn-secondary btn-sm" href={update.actionUrl}>{update.actionLabel}</a>
}

/**
 * The section an announcement's action link names, or null when it points somewhere off this app.
 *
 * The name rather than the page, because the caller wants the tab as well and a page cannot be turned
 * back into one. The check is still here so a link to somewhere that does not exist stays an anchor:
 * flowTarget answers every string, and falling back to the Overview would turn a typo into a button
 * that silently goes to the wrong place.
 */
function updateActionName(url: string): string | null {
  if (!url.startsWith('/')) return null
  const name = url.slice(1).split(/[/?#]/)[0] || 'overview'
  return name === 'hideout' || name === 'mules' || name === 'territory' || name === 'patch-notes' || name === 'news' || name in pageMeta
    ? name
    : null
}

function updateActionPage(url: string): AppPage | null {
  const name = updateActionName(url)
  return name === null ? null : flowPage(name)
}

function clampText(value: string, max: number) {
  return value.length <= max ? value : `${value.slice(0, Math.max(0, max - 1)).trimEnd()}...`
}

function updateSections(update: GameAnnouncement) {
  return [
    ['Added', update.added],
    ['Changed', update.changed],
    ['Fixed', update.fixed],
    ['Known issues', update.knownIssues],
  ].flatMap(([label, value]) => typeof value === 'string' && value.trim().length > 0 ? [{ label, value }] : [])
}

function updateCategoryClass(category: GameAnnouncement['category']) {
  return category === 'Patch'
    ? 'text-bg-primary'
    : category === 'Balance'
      ? 'text-bg-warning'
      : category === 'Event'
        ? 'text-bg-success'
        : category === 'Maintenance'
          ? 'text-bg-danger'
          : 'text-bg-secondary'
}

function updateSeverityClass(severity: GameAnnouncement['severity']) {
  return severity === 'Warning'
    ? 'text-bg-warning'
    : severity === 'Event'
      ? 'text-bg-success'
      : severity === 'Maintenance'
        ? 'text-bg-danger'
        : 'text-bg-light border'
}

function CasinoPage(ctx: PageContext) {
  const { dashboard, busy, refresh, act } = ctx
  const [board, setBoard] = useState<CasinoBoard | null>(null)
  const [activeKey, setActiveKey] = useState('')
  const [bet, setBet] = useState(10)
  const [paylines, setPaylines] = useState(1)
  const [lastSpin, setLastSpin] = useState<SlotSpin | null>(null)
  // How many columns have come to rest, left to right. Reels that all stop together read as a
  // picture appearing rather than as a machine landing, and the column that has not stopped yet is
  // the only reason to keep watching - so this is a count rather than a flag.
  const [stoppedColumns, setStoppedColumns] = useState(slotColumns)
  const spinning = stoppedColumns < slotColumns
  const [compNote, setCompNote] = useState('')
  const [loadError, setLoadError] = useState('')

  useEffect(() => {
    let live = true
    void api.casino()
      .then(next => {
        if (!live) return
        setBoard(next)
        const firstOpen = next.slotMachines.find(machine => !machine.locked) ?? next.slotMachines[0]
        if (firstOpen) {
          setActiveKey(firstOpen.key)
          setBet(firstOpen.minBet)
        }
        setLoadError('')
      })
      .catch(error => { if (live) setLoadError((error as Error).message) })
    return () => { live = false }
  }, [dashboard.playerId])

  const active = board?.slotMachines.find(machine => machine.key === activeKey)
    ?? board?.slotMachines.find(machine => !machine.locked)
    ?? board?.slotMachines[0]
  const lineLimit = active ? Math.max(1, Math.min(active.maxPaylines, board?.paylines.length ?? active.maxPaylines)) : 1
  const lineCount = Math.min(Math.max(paylines, 1), lineLimit)
  const clampedBet = active ? Math.min(Math.max(bet, active.minBet), active.maxBet) : bet
  const totalBet = clampedBet * lineCount

  useEffect(() => {
    if (active && bet !== clampedBet) setBet(clampedBet)
    if (paylines !== lineCount) setPaylines(lineCount)
  }, [active?.key, bet, clampedBet, paylines, lineCount])

  const runSpin = async () => {
    if (!active || spinning) return
    const started = window.performance.now()
    let spin: SlotSpin | null = null
    setStoppedColumns(0)
    await act(async () => {
      spin = await api.spinSlots(active.key, clampedBet, lineCount)
      return spin
    })
    // Asserted because the assignment happens inside the callback handed to act, which TypeScript's
    // flow analysis does not follow - without this it still reads the variable as its initialiser.
    const settled = spin as SlotSpin | null
    if (!settled) {
      // The cage refused it. Put the reels back rather than leaving them turning on a spin that
      // never happened.
      setStoppedColumns(slotColumns)
      return
    }

    // The result is known now, and the board takes it immediately: a column that has stopped has to
    // be showing what it actually landed on. What is still held back is everything that reads as the
    // verdict - the winning lines, the lit cells, the receipt - all of which wait on the last reel.
    setLastSpin(settled)
    // The whole floor comes back with the spin. It has to: the pot on every machine moved, and the one
    // that was just taken has gone back to its seed with somebody's name against it.
    setBoard(settled.board)

    const timing = slotReelTiming()
    const spun = window.performance.now() - started
    if (spun < timing.hold) await wait(timing.hold - spun)
    for (let column = 1; column <= slotColumns; column++) {
      setStoppedColumns(column)
      if (column < slotColumns) await wait(timing.gap + (column - 1) * timing.ramp)
    }
    await refresh()
  }

  const claimComp = async (rewardKey: string) => {
    let claimed: ClaimedComp | null = null
    await act(async () => {
      const result = await api.claimComp(rewardKey)
      claimed = result
      return result
    })
    // Same reason the spin path asserts: the assignment happens inside the callback handed to act,
    // which TypeScript's flow analysis does not follow.
    const settled = claimed as ClaimedComp | null
    if (!settled) return
    setCompNote(settled.summary)
    setBoard(settled.board)
    await refresh()
  }

  if (loadError) return <section className="card p-3"><div className="panel-title"><h2>Casino Floor</h2><span>Closed</span></div><p>{loadError}</p></section>
  if (!board || !active) return <section className="card p-3"><div className="panel-title"><h2>Casino Floor</h2><span>Loading</span></div><p>The cage is counting chips.</p></section>

  const biggestPot = board.slotMachines.reduce((best, machine) => Math.max(best, machine.progressive), 0)
  // A spin the house owes replays its own ticket, so it is blocked on neither cash nor turns and
  // does not care what the stake box currently says.
  const onTheHouse = board.freeSpins.enabled && board.freeSpins.owed > 0
  const spinBlocked = firstReason(
    spinning && 'The reels are still turning.',
    busy && BUSY,
    active.locked && (active.lockedReason ?? 'That machine is locked.'),
    !onTheHouse && dashboard.turns < board.spinTurnCost && `A pull is ${board.spinTurnCost} turn${board.spinTurnCost === 1 ? '' : 's'} and you have ${dashboard.turns}.`,
    !onTheHouse && clampedBet < active.minBet && `${active.name} starts at ${money.format(active.minBet)}.`,
    !onTheHouse && clampedBet > active.maxBet && `${active.name} tops out at ${money.format(active.maxBet)}.`,
    !onTheHouse && dashboard.cash < totalBet && `You are carrying ${money.format(dashboard.cash)}.`,
  )
  const winningLines = !spinning && lastSpin
    ? board.paylines.filter(line => lastSpin.transaction.winningPaylineIndexes.includes(line.index))
    : []
  const winningCells = new Set(winningLines.flatMap(line => line.cells))
  const verdict = lastSpin ? spinVerdict(lastSpin.transaction) : null
  // Richest first off the paytable, so an idle reel shows the room's own faces.
  const activeFaces = active.paytable.map(pay => pay.label)

  return <div className="d-grid gtc-1 gtc-xl-split-135 gap-3 align-items-start">
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Casino Floor</h2><span>{dashboard.city}</span></div>
      {board.jackpotRules.enabled && <p className="text-body-secondary mb-0">
        Every machine keeps a pot fed by {board.jackpotRules.contributionPercent}% of each stake played on it.
        Land {board.jackpotRules.symbolsRequired} <strong>{board.jackpotRules.symbolLabel}</strong> anywhere on the grid
        {board.jackpotRules.requireAllPaylines ? ' with every lane bought' : ''} and the whole thing is yours.
      </p>}
      <div className="d-grid gtc-fill-180 gap-2 mt-3">
        {board.slotMachines.map(machine => <CasinoMachineTile
          machine={machine}
          active={machine.key === active.key}
          bet={bet}
          busy={busy}
          onPick={() => {
            setActiveKey(machine.key)
            setBet(Math.min(Math.max(bet, machine.minBet), machine.maxBet))
          }}
          key={machine.key}
        />)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>{active.name}</h2><span>{money.format(active.minBet)} min</span></div>
      <p>{active.blurb}</p>
      <div className="d-flex flex-wrap gap-2 align-items-center">
        {board.jackpotRules.enabled && active.progressive > 0 &&
          <span className="badge text-bg-warning casino-meter">Pot {money.format(active.progressive)}</span>}
        <span className="badge text-bg-primary">Top award {money.format(active.topAward)}</span>
        <span className="badge text-bg-secondary">Returns {active.returnPercent}%</span>
        {active.minRepLevel > 1 && <span className="badge text-bg-secondary">{active.minRepLevelName} floor</span>}
      </div>
      <details className="mt-2">
        <summary className="text-body-secondary">What {active.name} pays</summary>
        <div className="table-responsive mt-2">
          <table className="table table-sm game-table align-middle mb-0">
            <thead><tr>
              <th>Symbol</th>
              <th className="text-end">Two</th><th className="text-end">Three</th>
              <th className="text-end">Four</th><th className="text-end">Five</th>
            </tr></thead>
            <tbody>
              {active.paytable.map(pay => <tr key={pay.label}>
                <td><SlotGlyph symbol={pay.label} className="slot-pay-glyph" /> {pay.label}</td>
                <td className="text-end tnum">{pay.pair > 0 ? `${pay.pair}x` : '-'}</td>
                <td className="text-end tnum">{pay.triple > 0 ? `${pay.triple}x` : '-'}</td>
                <td className="text-end tnum">{pay.quad > 0 ? `${pay.quad}x` : '-'}</td>
                <td className="text-end tnum">{pay.quint}x</td>
              </tr>)}
            </tbody>
          </table>
        </div>
        <small className="text-body-tertiary">
          A lane pays on the run it opens with, counted from the left. Every machine runs its own reel.
        </small>
      </details>
      <div className="slot-reels d-grid gap-2 my-3" aria-label="Slot reels">
        {slotGridSymbols(activeFaces, lastSpin?.symbols).map((symbol, index) => {
          // A cell belongs to the column it sits in, and its column stops on its own.
          const turning = index % slotColumns >= stoppedColumns
          const reelSymbols = turning ? slotReelSymbols(index, activeFaces) : [symbol]
          return <div
            className={`slot-reel d-grid border rounded bg-body-tertiary ${turning ? 'is-spinning' : ''} ${winningCells.has(index) ? 'is-winning' : ''}`}
            aria-label={turning ? `Reel ${index % slotColumns + 1} spinning` : `Slot ${index + 1}: ${symbol}`}
            key={`${active.key}-${index}`}
          >
            <div className="slot-reel-window" aria-hidden="true">
              <div
                className="slot-reel-strip"
                // Timed off the column rather than the cell, so the three cells of a reel turn as one
                // piece of machinery instead of three loose tiles. Each reel out to the right is a
                // little slower than the one before it, which is the same order they come to rest in.
                style={{
                  animationDelay: `${(index % slotColumns) * -130}ms`,
                  animationDuration: `${620 + (index % slotColumns) * 110}ms`,
                }}
              >
                {reelSymbols.map((reelSymbol, reelIndex) =>
                  <div className="slot-reel-face" key={`${reelSymbol}-${reelIndex}`}>
                    <span><SlotGlyph symbol={reelSymbol} /></span>
                  </div>)}
              </div>
            </div>
            <strong className="slot-reel-label">{turning ? 'Spinning' : symbol}</strong>
          </div>
        })}
        {winningLines.length > 0 && <svg className="slot-payline-overlay" viewBox={`0 0 ${slotColumns - 1} ${slotRows - 1}`} preserveAspectRatio="none" aria-hidden="true">
          {winningLines.map(line => <polyline className="slot-payline-hit" points={slotPaylinePoints(line.cells)} key={line.index} />)}
        </svg>}
      </div>
      {onTheHouse && <p className="text-warning mb-3">
        The house owes you {board.freeSpins.owed} spin{board.freeSpins.owed === 1 ? '' : 's'} on
        {' '}{board.freeSpins.machineName ?? 'this machine'} - {money.format(board.freeSpins.bet)} across
        {' '}{board.freeSpins.paylines} lane{board.freeSpins.paylines === 1 ? '' : 's'}, the pull that won them.
        They cost no cash and no turn, and the stake box does not apply until they are gone.
      </p>}
      <div className="control-block mb-3">
        <div className="d-flex justify-content-between gap-3 align-items-baseline">
          <strong>Lanes</strong>
          <small className="text-body-tertiary">{money.format(clampedBet)} each, {money.format(totalBet)} total</small>
        </div>
        <div className="btn-group w-100" role="group" aria-label="Paylines">
          {Array.from({ length: lineLimit }, (_, index) => index + 1).map(count =>
            <button
              className={`btn ${lineCount === count ? 'btn-primary' : 'btn-secondary'}`}
              type="button"
              disabled={busy || spinning}
              onClick={() => setPaylines(count)}
              key={count}
            >
              {count}
            </button>)}
        </div>
        <small className="text-body-tertiary">
          {board.paylines.slice(0, lineCount).map(line => line.name).join(', ')}
        </small>
      </div>
      <div className="control-row">
        <label className="field">Bet / lane
          <input
            className="form-control"
            type="number"
            min={active.minBet}
            max={active.maxBet}
            step={active.minBet}
            value={bet}
            onChange={event => setBet(Number(event.target.value))}
          />
        </label>
        <button className="btn btn-secondary" type="button" disabled={busy} onClick={() => setBet(active.minBet)}>Min</button>
        <button className="btn btn-secondary" type="button" disabled={busy} onClick={() => setBet(Math.min(active.maxBet, Math.floor(dashboard.cash / lineCount)))}>Max</button>
        <Button className="btn btn-primary" blocked={spinBlocked} onClick={() => void runSpin()}>
          {onTheHouse
            ? `Free spin (${board.freeSpins.owed} left)`
            : `Spin ${money.format(totalBet)}${board.spinTurnCost > 0 ? ` / ${board.spinTurnCost}t` : ''}`}
        </Button>
      </div>
      {lastSpin && verdict && !spinning && <div className={`border rounded p-3 mt-3 ${verdict.edge}`}>
        <div className="d-flex justify-content-between gap-3 align-items-baseline">
          <strong>{verdict.label}</strong>
          <span className={`tnum ${verdict.tone}`}>{signedMoney(lastSpin.transaction.netResult)}</span>
        </div>
        <small className="text-body-tertiary">
          {lastSpin.wasFreeSpin ? 'On the house' : `Bet ${money.format(lastSpin.transaction.betAmount)}`}. Won {money.format(lastSpin.transaction.payoutAmount)}.
          {lastSpin.freeSpinsAwarded > 0 && ` The house owes you ${lastSpin.freeSpinsAwarded} free spins.`}
          {lastSpin.transaction.jackpotAmount > 0 && ` ${money.format(lastSpin.transaction.jackpotAmount)} of it was the progressive.`}
          {lastSpin.turnsSpent > 0 && ` ${lastSpin.turnsSpent} turn${lastSpin.turnsSpent === 1 ? '' : 's'}.`}
          {lastSpin.repEarned > 0 && ` +${number.format(lastSpin.repEarned)} casino rep.`}
        </small>
      </div>}
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Floor Standing</h2><span>{board.reputation.levelName}</span></div>
      <div className="d-grid gap-2">
        <div className="d-flex justify-content-between align-items-baseline gap-3">
          <strong>{number.format(board.reputation.rep)} rep</strong>
          <small className="text-body-tertiary">
            {board.reputation.nextLevelName
              ? `${number.format(board.reputation.repToNextLevel)} to ${board.reputation.nextLevelName}`
              : 'Top of the floor'}
          </small>
        </div>
        <div className="progress" role="progressbar" aria-label="Casino standing" aria-valuenow={board.reputation.progressPercent} aria-valuemin={0} aria-valuemax={100}>
          <div className="progress-bar bg-primary" style={{ width: `${Math.max(2, board.reputation.progressPercent)}%` }} />
        </div>
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>The Cage</h2>
        <span>{money.format(board.comps.balance)} in comps</span>
      </div>
      <p className="text-body-secondary">
        Every pull is rated whether it lands or not - {money.format(board.comps.dollarsWageredPerComp)} through a
        machine is a dollar back on the books. Standing says what the cage will do for you; comps pay for it.
      </p>
      <div className="d-grid gtc-fill-220 gap-2">
        {board.comps.rewards.map(reward => <CompRewardTile
          reward={reward}
          busy={busy || spinning}
          onClaim={() => void claimComp(reward.key)}
          key={reward.key}
        />)}
      </div>
      {compNote && <p className="text-body-tertiary mb-0 mt-3">{compNote}</p>}
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Casino Stats</h2><span>{number.format(board.stats.spins)} spins</span></div>
      <div className="d-grid gtc-2 gap-2">
        <AdminMetric label="Wagered" value={money.format(board.stats.wagered)} />
        <AdminMetric label="Returned" value={money.format(board.stats.won)} />
        <AdminMetric label="Net" value={signedMoney(board.stats.net)} />
        <AdminMetric label="Biggest pot" value={money.format(biggestPot)} />
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Pots Taken</h2><span>House record</span></div>
      {board.recentJackpots.length === 0
        ? <p className="text-body-tertiary mb-0">Nobody has taken one yet. Every pot on the floor is still building.</p>
        : <ul className="list-unstyled d-grid gap-2 mb-0">
            {board.recentJackpots.map(drop => <li className="d-flex justify-content-between gap-3 align-items-baseline" key={`${drop.machineKey}-${drop.wonAtUtc}`}>
              <span><strong>{drop.playerName}</strong> <small className="text-body-tertiary">{drop.machineName}</small></span>
              <span className="tnum text-warning">{money.format(drop.amount)}</span>
            </li>)}
          </ul>}
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Casino Ledger</h2><span>Recent pulls</span></div>
      {board.recent.length === 0
        ? <p className="text-body-tertiary mb-0">No spins yet.</p>
        : <div className="table-responsive">
            <table className="table table-sm game-table align-middle mb-0">
              <thead><tr><th>Machine</th><th>Grid</th><th>Lines</th><th>Bet</th><th>Payout</th><th>Pot</th><th>Net</th><th>When</th></tr></thead>
              <tbody>
                {board.recent.map(entry => <tr key={entry.id}>
                  <td>{entry.machineName}{entry.jackpotAmount > 0
                    ? <span className="badge text-bg-warning ms-2">Pot</span>
                    : entry.jackpot ? <span className="badge text-bg-primary ms-2">Top</span> : null}
                    {entry.isFreeSpin && <span className="badge text-bg-secondary ms-2">Free</span>}</td>
                  <td>{slotGridText(activeFaces, entry.symbols)}</td>
                  <td>{entry.winningPaylines}/{entry.paylines}</td>
                  <td className={entry.isFreeSpin ? 'text-body-tertiary' : undefined}>{money.format(entry.betAmount)}</td>
                  <td>{money.format(entry.payoutAmount)}</td>
                  <td className={entry.jackpotAmount > 0 ? 'text-warning' : 'text-body-tertiary'}>
                    {entry.jackpotAmount > 0 ? money.format(entry.jackpotAmount) : '-'}
                  </td>
                  <td className={entry.netResult >= 0 ? 'text-success' : 'text-danger'}>{signedMoney(entry.netResult)}</td>
                  <td>{new Date(entry.createdAtUtc).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</td>
                </tr>)}
              </tbody>
            </table>
          </div>}
    </section>
  </div>
}

function CompRewardTile({ reward, busy, onClaim }: {
  reward: CompReward
  busy: boolean
  onClaim: () => void
}) {
  const gives = [
    reward.turns > 0 && `${number.format(reward.turns)} turns`,
    reward.cash > 0 && money.format(reward.cash),
    reward.heat > 0 && `${number.format(reward.heat)} heat off`,
  ].filter(Boolean).join(' / ')

  return <div className={`d-grid gap-1 border rounded p-2 ${reward.locked ? 'bg-body-tertiary opacity-75' : 'bg-body-tertiary border-primary'}`}>
    <div className="d-flex justify-content-between gap-2 align-items-baseline">
      <strong className="text-body">{reward.name}</strong>
      <span className="tnum text-warning small">{money.format(reward.cost)}</span>
    </div>
    <small className="text-body-tertiary small">{reward.blurb}</small>
    <small className="text-primary small">{gives}</small>
    {reward.locked
      ? <small className="text-warning small">{reward.lockedReason}</small>
      : <button className="btn btn-secondary btn-sm" type="button" disabled={busy} onClick={onClaim}>Take it</button>}
  </div>
}

function CasinoMachineTile({ machine, active, bet, busy, onPick }: {
  machine: CasinoMachine
  active: boolean
  bet: number
  busy: boolean
  onPick: () => void
}) {
  return <button
    className={`tile d-grid gap-1 text-start border rounded p-2 ${active ? 'active border-primary' : 'bg-body-tertiary'} ${machine.locked ? 'opacity-75' : ''}`}
    type="button"
    disabled={busy}
    title={machine.lockedReason ?? machine.blurb}
    onClick={onPick}
  >
    <strong className="text-body">{machine.name}</strong>
    <small className="text-body-tertiary small">
      {money.format(machine.minBet)}-{money.format(machine.maxBet)} / pot {money.format(machine.progressive)}
    </small>
    <small className="text-body-tertiary small">
      Returns {machine.returnPercent}% / tops out at {money.format(machine.topAward)}
    </small>
    {machine.locked
      ? <small className="text-warning small">{machine.lockedReason}</small>
      : <small className="text-primary small">
          {machine.minRepLevel > 1 ? `${machine.minRepLevelName} floor / ` : ''}Current pull {money.format(Math.min(Math.max(bet, machine.minBet), machine.maxBet))}
        </small>}
  </button>
}

function StreetPage(ctx: PageContext) {
  const { dashboard, combatMissions, busy, streetTurns, autoBuySupplies, hoeCut, bankAmount, storeQty, district, setActivePage, setStreetTurns, setAutoBuySupplies, setHoeCut, setBankAmount, setStoreQty, setDistrict, act } = ctx
  const pendingOutgoingAttack = combatMissions.find(mission => mission.attackerId === dashboard.playerId && mission.status !== 'Complete')
  const maxStreetTurns = streetTurnLimit(dashboard)
  const clampedStreetTurns = Math.max(1, maxStreetTurns)
  useEffect(() => {
    if (streetTurns > clampedStreetTurns) setStreetTurns(clampedStreetTurns)
  }, [clampedStreetTurns, setStreetTurns, streetTurns])
  const restock = restockEstimate(dashboard, streetTurns)
  const pickedDistrict = selectedDistrict(dashboard, district)
  const projectedHeat = streetHeatFor(dashboard, streetTurns, district)
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    <section className="card p-3 gcol-full" data-area="street-action">
      <div className="panel-title"><h2>Work the Streets</h2><span>Income + recruiting</span></div>
      <p>Your hoes earn, and their cut comes off the top before anything reaches your pocket. A shift also turns up new crew and whatever is lying about.</p>
      {pendingOutgoingAttack && <div className="d-flex justify-content-between align-items-center gap-3 border border-primary rounded bg-body-tertiary px-3 py-2 mt-3">
        <strong className="text-primary">Crew is out</strong>
        <span className="text-body-secondary text-end">Street work unlocks after the next mission update in {timeUntil(nextMissionTime(pendingOutgoingAttack))}.</span>
      </div>}
      <DistrictPicker districts={dashboard.districts} selected={district} onSelect={setDistrict} />
      <div className="tnum d-grid gtc-1 gtc-md-3 gap-2 my-3">
        <AdminMetric label="Shift heat" value={`+${heatAmount(projectedHeat)}`} sub={`${pickedDistrict?.name ?? 'Selected district'} / ${streetTurnCount(dashboard, streetTurns)} turns`} />
        <AdminMetric label="Crew heat" value={heatAmount(dashboard.crewReport.crewHeat)} sub={crewHeatLabel(dashboard.crewReport)} />
        <AdminMetric label="Hourly keep" value={hourlyUpkeepShort(dashboard)} sub="Passive upkeep while time passes" />
      </div>
      <StorageSupplyNotice dashboard={dashboard} />
      <StreetSupplyPanel
        dashboard={dashboard}
        busy={busy}
        streetTurns={streetTurns}
        storeQty={storeQty}
        setStoreQty={setStoreQty}
        act={act}
        onMarket={() => setActivePage('market', 'trade')}
      />
      <div className="control-row">
        <label className="field">Turns<input className="form-control" type="number" min={1} max={clampedStreetTurns} value={streetTurns} onChange={e => setStreetTurns(Number(e.target.value))} /></label>
        <label className="field">Hoe Cut %<input className="form-control" type="number" min={10} max={80} value={hoeCut} onChange={e => setHoeCut(Number(e.target.value))} /></label>
        <Button className="btn btn-secondary" blocked={firstReason(
          busy && BUSY,
          hoeCut < 10 && 'A cut under 10% is not worth their while. Ten is the floor.',
          hoeCut > 80 && 'Anything over 80% and you are working for them. Eighty is the ceiling.',
          hoeCut === dashboard.hoeCutPercent && `The cut is already ${dashboard.hoeCutPercent}%.`,
        )} onClick={() => void act(() => api.setHoeCut(hoeCut))}>Save Cut</Button>
        <Button className="btn btn-primary" blocked={firstReason(
          busy && BUSY,
          !!pendingOutgoingAttack && 'Your crew is out on a job. Nobody is left to work a shift.',
          maxStreetTurns < 1 && 'Your storage cannot supply even a 1-turn street shift for this crew.',
          streetTurns < 1 && 'Set the shift to at least one turn.',
          streetTurns > dashboard.turns && `A ${streetTurns}-turn shift costs more turns than you have. You have ${dashboard.turns}.`,
          streetTurns > maxStreetTurns && `Your storage can supply this crew for ${number.format(maxStreetTurns)} turn${maxStreetTurns === 1 ? '' : 's'} at most.`,
        )} onClick={() => void act(() => api.workStreet(streetTurns, autoBuySupplies, district || undefined))}>{pendingOutgoingAttack ? 'Crew Out' : `Work ${streetTurns} Turn${streetTurns === 1 ? '' : 's'}`}</Button>
        <button className="btn btn-secondary" type="button" disabled={busy || maxStreetTurns < 1} onClick={() => setStreetTurns(clampedStreetTurns)}>Max</button>
      </div>
      <label className={`d-flex align-items-start gap-2 mt-3 border rounded px-3 py-2 ${autoBuySupplies ? 'border-primary bg-body-tertiary' : 'bg-body-tertiary'}`}>
        <input className="form-check-input flex-shrink-0 mt-1" type="checkbox" checked={autoBuySupplies} onChange={event => setAutoBuySupplies(event.target.checked)} />
        <span className="d-grid gap-1">
          <strong className="text-body">Auto-buy upkeep before working</strong>
          <small className={`small ${autoBuySupplies ? 'text-primary' : 'text-body-secondary'}`}>{restockLabel(restock, dashboard.cash)}</small>
        </span>
      </label>
      <div className="d-flex flex-wrap gap-2 mt-3">
        <span className="border rounded-pill bg-body-tertiary text-body-tertiary px-2 py-1 small">1 pimp manages 10 hoes</span>
        <span className="border rounded-pill bg-body-tertiary text-body-tertiary px-2 py-1 small">Condoms support hoes</span>
        <span className="border rounded-pill bg-body-tertiary text-body-tertiary px-2 py-1 small">Beer + weapons support thugs</span>
      </div>
    </section>

    <BankPanel dashboard={dashboard} busy={busy} bankAmount={bankAmount} setBankAmount={setBankAmount} act={act} />

    <section className="card p-3">
      <div className="panel-title"><h2>Activity</h2><span>Last 12 actions</span></div>
      <ActivityList entries={dashboard.recentActivity} />
    </section>
  </div>
}

const CREW_TABS = ['roster', 'hideout', 'production'] as const

/**
 * Everything the crew is made of, in three tabs.
 *
 * The hideout and the craft queue used to live under Business, which is where they had been put on the
 * grounds that both cost money. Almost nothing else about them belonged there. What a room does is set
 * how many hoes can be fed, how many thugs there is a bed for and how much of a shift can be supplied -
 * every one of those a number this page already prints, one tab away from the room that decides it -
 * and the craft queue is the bench arming those thugs. Business is where things are bought and sold;
 * this is where they are kept, housed and made.
 */
function CrewPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('crew', CREW_TABS, 'roster')
  return <div className="d-grid gap-3">
    <SectionTabs
      label="Crew sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'roster', label: 'Crew' },
        { key: 'hideout', label: 'Hideout' },
        { key: 'production', label: 'Craft Queue' },
      ]}
    />
    {tab === 'roster' && <CrewCorePage {...ctx} />}
    {tab === 'hideout' && <HideoutPage {...ctx} />}
    {tab === 'production' && <ProductionPage {...ctx} />}
  </div>
}

function CrewCorePage(ctx: PageContext) {
  const { dashboard, busy, crewQty, totalCrew, weaponCoverage, managementCapacity, setCrewQty, act } = ctx
  const combatCrew = dashboard.combatCrew
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start">
    {/* First on the page, and only when there is one. A cell has a clock on it and nothing else here
        does, so it goes above the crew it is holding rather than under them. */}
    <ArrestPanel dashboard={dashboard} busy={busy} act={act} />
    <ShrinePanel busy={busy} act={act} />
    <section className="card p-3 gcol-full" data-area="crew">
      <div className="panel-title"><h2>Your Crew</h2><span>{number.format(totalCrew)} total</span></div>
      <StorageSupplyNotice dashboard={dashboard} />
      <div className="d-grid gtc-1 gtc-md-3 gap-2">
        <CrewCard name="Pimps" count={dashboard.pimps} cap={dashboard.hideout.maxPimps} desc={`Manage up to ${number.format(managementCapacity)} hoes.`} />
        <CrewCard name="Hoes" count={dashboard.hoes} cap={dashboard.hideout.maxHoes} desc={`${dashboard.hoeHappiness.toFixed(0)}% morale / ${dashboard.hoeCutPercent}% cut`} tone={moraleTone(dashboard.hoeHappiness)} trend={<MoraleArrow trend={dashboard.moraleTrend} crew="hoe" />} />
        <CrewCard name="Thugs" count={dashboard.thugs} cap={dashboard.hideout.maxThugs} desc={`${dashboard.thugHappiness.toFixed(0)}% morale / ${weaponCoverage.toFixed(0)}% armed`} tone={moraleTone(dashboard.thugHappiness)} trend={<MoraleArrow trend={dashboard.moraleTrend} crew="thug" />} />
      </div>
      <div className="d-grid gtc-1 gtc-md-5 gap-2 mt-3">
        <AdminMetric label="Free pimps" value={number.format(combatCrew.availablePimps)} />
        <AdminMetric label="Free thugs" value={number.format(combatCrew.availableThugs)} />
        <AdminMetric label="Free weapons" value={number.format(combatCrew.availableWeapons)} />
        <AdminMetric label="Committed" value={`${number.format(combatCrew.committedPimps)} P / ${number.format(combatCrew.committedThugs)} T / ${number.format(combatCrew.committedWeapons)} W`} />
        <AdminMetric label="Attack slots" value={`${combatCrew.activeAttackMissions}/${combatCrew.maxActiveAttackMissions}`} />
      </div>
    </section>

    <section className="card p-3 gcol-full" data-area="crew-hiring">
      <div className="panel-title"><h2>Crew Management</h2><span>Hire + fire</span></div>
      <div className="d-grid">
        <CrewManageRow
          label="Pimps"
          owned={dashboard.pimps}
          quantity={crewQty.pimps}
          hireCost={dashboard.crewReport.hirePimpCost}
          cash={dashboard.cash}
          busy={busy}
          fireBlocked={dashboard.pimps - crewQty.pimps < 1 && 'Somebody has to run the house. You cannot let your last pimp go.'}
          onQuantity={quantity => setCrewQty(value => ({ ...value, pimps: quantity }))}
          onHire={() => void act(() => api.hireCrew('pimps', crewQty.pimps))}
          onFire={() => void act(() => api.fireCrew('pimps', crewQty.pimps))}
          note={`${number.format(managementCapacity)} hoe management capacity`}
        />
        <CrewManageRow
          label="Hoes"
          owned={dashboard.hoes}
          quantity={crewQty.hoes}
          hireCost={dashboard.crewReport.hireHoeCost}
          cash={dashboard.cash}
          busy={busy}
          hireBlocked={dashboard.hoeHappiness < dashboard.crewReport.minHoeMoraleToHire
            && `Nobody new signs on to an unhappy house. Morale is ${dashboard.hoeHappiness.toFixed(0)}% and hiring wants ${dashboard.crewReport.minHoeMoraleToHire.toFixed(0)}%.`}
          fireBlocked={dashboard.hoes < crewQty.hoes && `You are letting ${number.format(crewQty.hoes)} go and you have ${number.format(dashboard.hoes)}.`}
          onQuantity={quantity => setCrewQty(value => ({ ...value, hoes: quantity }))}
          onHire={() => void act(() => api.hireCrew('hoes', crewQty.hoes))}
          onFire={() => void act(() => api.fireCrew('hoes', crewQty.hoes))}
          note={`${dashboard.hoeHappiness.toFixed(0)}% morale, ${dashboard.crewReport.minHoeMoraleToHire.toFixed(0)}% needed to hire`}
          firePenalty={dashboard.crewReport.fireHoeMoralePenalty}
          maxFirePenalty={dashboard.crewReport.maxFireMoralePenalty}
          trims={[
            { label: `what your pimps manage (${number.format(managementCapacity)})`, cut: dashboard.hoes - managementCapacity },
            { label: `what your store supplies (${number.format(dashboard.crewReport.hoesStorageCanSupply)})`, cut: dashboard.hoes - dashboard.crewReport.hoesStorageCanSupply }
          ]}
        />
        <CrewManageRow
          label="Thugs"
          owned={dashboard.thugs}
          quantity={crewQty.thugs}
          hireCost={dashboard.crewReport.hireThugCost}
          cash={dashboard.cash}
          busy={busy}
          hireBlocked={dashboard.thugHappiness < dashboard.crewReport.minThugMoraleToHire
            && `Nobody new signs on to an unhappy house. Morale is ${dashboard.thugHappiness.toFixed(0)}% and hiring wants ${dashboard.crewReport.minThugMoraleToHire.toFixed(0)}%.`}
          fireBlocked={dashboard.thugs < crewQty.thugs && `You are letting ${number.format(crewQty.thugs)} go and you have ${number.format(dashboard.thugs)}.`}
          onQuantity={quantity => setCrewQty(value => ({ ...value, thugs: quantity }))}
          onHire={() => void act(() => api.hireCrew('thugs', crewQty.thugs))}
          onFire={() => void act(() => api.fireCrew('thugs', crewQty.thugs))}
          note={`${dashboard.crewReport.armedThugs}/${dashboard.thugs} armed`}
          firePenalty={dashboard.crewReport.fireThugMoralePenalty}
          maxFirePenalty={dashboard.crewReport.maxFireMoralePenalty}
          trims={[
            { label: `what your store supplies (${number.format(dashboard.crewReport.thugsStorageCanSupply)})`, cut: dashboard.thugs - dashboard.crewReport.thugsStorageCanSupply }
          ]}
        />
      </div>
    </section>

    <PimpRosterPanel dashboard={dashboard} />
  </div>
}

function HideoutPage(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const hideout = dashboard.hideout
  const workshop = hideout.stations?.find(station => station.key === 'workshop')
  const damage = hideout.damage ?? []
  const repairing = hideout.repair ?? null
  const broken = (room: BreakableRoom) => damage.find(x => x.room === room) ?? null
  const repair = (room: BreakableRoom) => void act(() => api.repairHideout(room))

  // The panel keeps its own second hand, for the reason the tier panel gives: the app-wide one stops
  // once turns are maxed, which is exactly when somebody sitting on a full bank is waiting out a
  // repair with nothing else to spend it on.
  const [, setTick] = useState(0)
  useEffect(() => {
    if (!repairing) return
    const timer = window.setInterval(() => setTick(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [repairing?.completesAtUtc])

  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    {/* Above the capacity bars on purpose. A house with three dark rooms has one decision in it and
        this is it, and a player who has to scroll past their storage graph to find out why the mules
        will not leave has been told last. Absent entirely when nothing is broken, rather than an
        empty panel headed "Damage" that everybody learns to stop reading. */}
    {damage.length > 0 && <section className="card p-3 gcol-full border-danger" data-area="damage">
      <div className="panel-title">
        <h2>What is broken</h2>
        <span>{damage.length === 1 ? 'One room' : `${number.format(damage.length)} rooms`} down</span>
      </div>
      <p>
        A raid leaves the place standing and the rooms in it not working. Each one does nothing at all
        until it is paid for, and the crew can only be in one room at a time - so the order is a
        decision, and it is yours.
      </p>
      <div className="d-grid gap-2">
        {damage.map(room => <div key={room.room} className="d-flex flex-wrap align-items-center justify-content-between gap-2 border rounded p-2">
          <div className="min-w-0">
            <strong className="text-capitalize">{room.name}</strong>
            <div className="small text-body-secondary">
              Level {room.level}, down since {new Date(room.wreckedAtUtc).toLocaleString()}. While it is,{' '}
              {room.stops}.
            </div>
          </div>
          <Button className="btn btn-danger btn-sm" blocked={firstReason(
            busy && BUSY,
            repairing?.room === room.room && `The crew are in there now. Working again in ${timeUntil(repairing.completesAtUtc)}.`,
            !!repairing && repairing.room !== room.room && `Your crew are in the ${repairing.name} for another ${timeUntil(repairing.completesAtUtc)}.`,
            dashboard.cash + dashboard.bankCash < room.repairCost && `That costs ${money.format(room.repairCost)} and you have ${money.format(dashboard.cash + dashboard.bankCash)} between cash and the bank.`,
          )} onClick={() => repair(room.room)}>
            {repairing?.room === room.room ? 'Being fixed' : `Repair ${money.format(room.repairCost)}`}
          </Button>
        </div>)}
      </div>
    </section>}

    <section className="card p-3 gcol-full" data-area="capacity">
      <div className="panel-title"><h2>Storage and Capacity</h2><span>{hideout.tierName} / tier {hideout.tier}</span></div>
      <p>Everything you can hold is decided here. Crew the place has no room for walks away, goods the store cannot take are left in the street, and cash the safe cannot hold goes to the bank.</p>
      <div className="tnum d-grid gtc-1 gtc-sm-2 gtc-md-3 gap-2 mt-3">
        <CapacityBar label="Pimps" used={dashboard.pimps} cap={hideout.maxPimps} />
        <CapacityBar label="Hoes" used={dashboard.hoes} cap={hideout.maxHoes} />
        <CapacityBar label="Thugs" used={dashboard.thugs} cap={hideout.maxThugs} />
        <CapacityBar label="Garage" used={dashboard.rides} cap={hideout.maxRides} />
        <CapacityBar label="Cash on hand" used={dashboard.cash} cap={hideout.maxCash} money />
        <CapacityBar label="Condoms" used={dashboard.condoms} cap={hideout.maxCondoms} />
        <CapacityBar label="Beer" used={dashboard.beer} cap={hideout.maxBeer} />
        <CapacityBar label="Weapons" used={dashboard.weapons} cap={hideout.maxWeapons} />
        <CapacityBar label="Weed" used={dashboard.weed} cap={hideout.maxWeed} />
        <CapacityBar label="Coke" used={dashboard.coke} cap={hideout.maxCoke} />
        <CapacityBar label="Moonshine" used={dashboard.moonshine} cap={hideout.maxMoonshine} />
        <CapacityBar label="Cut" used={dashboard.cut} cap={hideout.maxCut} />
        <CapacityBar label="Medicine" used={dashboard.medicine} cap={hideout.maxMedicine} />
        <CapacityBar label="Poison" used={dashboard.poison} cap={hideout.maxPoison} />
      </div>
    </section>

    <HideoutTierPanel dashboard={dashboard} busy={busy} act={act} />

    <section className="card p-3 gcol-full" data-area="rooms">
      <div className="panel-title"><h2>Rooms</h2><span>Paid from the bank first</span></div>
      <div className="d-grid gap-2 mt-3">
        <RoomRow
          name="Storage Room"
          level={hideout.storageLevel}
          detail={`Holds ${number.format(hideout.maxCondoms)} condoms, ${number.format(hideout.maxBeer)} beer, ${number.format(hideout.maxWeapons)} weapons, ${number.format(hideout.maxWeed)} weed, ${number.format(hideout.maxCoke)} coke`}
          upgrade={hideout.storageUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('storage'))}
        />
        <RoomRow
          name="Safe"
          level={hideout.safeLevel}
          detail={`Holds ${money.format(hideout.maxCash)} cash on hand`}
          upgrade={hideout.safeUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('safe'))}
        />
        <RoomRow
          name="Weed Lab"
          level={hideout.weedLabLevel}
          detail={hideout.weedLabLevel === 0
            ? 'Not built. Grows weed on its own while you are out, and stretches a shift further when you are in.'
            : `Active +${hideout.weedLabYieldBonusPercent}% per production turn, and ${number.format(hideout.weedLabPassivePerHour)} weed an hour on its own.`}
          upgrade={hideout.weedLabUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('weedlab'))}
          damage={broken('weedlab')}
          repairing={repairing}
          onRepair={() => repair('weedlab')}
        >
          <LabSwitches
            product="weed"
            level={hideout.weedLabLevel}
            running={hideout.weedLabRunning}
            autoSell={hideout.weedLabAutoSell}
            minSellLevel={hideout.minLabLevelForAutoSell}
            busy={busy}
            act={act}
          />
        </RoomRow>
        <RoomRow
          name="Coke Lab"
          level={hideout.cokeLabLevel}
          detail={hideout.cokeLabLevel === 0
            ? 'Not built. Cooks coke on its own while you are out, and stretches a shift further when you are in.'
            : `Active +${hideout.cokeLabYieldBonusPercent}% per production turn, and ${number.format(hideout.cokeLabPassivePerHour)} coke an hour on its own.`}
          upgrade={hideout.cokeLabUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('cokelab'))}
          damage={broken('cokelab')}
          repairing={repairing}
          onRepair={() => repair('cokelab')}
        >
          <LabSwitches
            product="coke"
            level={hideout.cokeLabLevel}
            running={hideout.cokeLabRunning}
            autoSell={hideout.cokeLabAutoSell}
            minSellLevel={hideout.minLabLevelForAutoSell}
            busy={busy}
            act={act}
          />
        </RoomRow>
        {workshop && <RoomRow
          name="Workshop"
          level={workshop.level}
          detail={workshop.level === 0
            ? 'Not built. Unlocks crafting for guns, moonshine, cut, medicine and poison.'
            : `Crafts run at ${number.format(workshop.perTurn)} unit${workshop.perTurn === 1 ? '' : 's'} a turn before each recipe's own rate.`}
          upgrade={workshop.upgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('workshop'))}
          damage={broken('workshop')}
          repairing={repairing}
          onRepair={() => repair('workshop')}
        />}
        {workshop && <WorkshopUnlockGrid dashboard={dashboard} />}
        <RoomRow
          name="Lookout"
          level={hideout.lookoutLevel}
          detail={hideout.lookoutLevel === 0
            ? 'Not built. Someone on the street watching for the law, so a raid is less likely to land.'
            : `Cuts the odds of a raid by ${hideout.bustRiskReductionPercent}%`}
          upgrade={hideout.lookoutUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('lookout'))}
          damage={broken('lookout')}
          repairing={repairing}
          onRepair={() => repair('lookout')}
        />
        <RoomRow
          name="Intelligence Centre"
          level={hideout.intelligenceLevel}
          detail={hideout.intelligenceLevel === 0
            ? 'Not built. Makes nothing. Lets you run mules out of town, and knows the routes they take.'
            : `${hideout.concurrentRunCap} mule run(s) out at once, on routes you already know`}
          upgrade={hideout.intelligenceUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('intelligence'))}
          damage={broken('intelligence')}
          repairing={repairing}
          onRepair={() => repair('intelligence')}
        />
      </div>
      {(hideout.weedLabLevel > 0 || hideout.cokeLabLevel > 0) && <p className="text-body-tertiary small mt-3">
        Labs keep running while you are away, up to {hideout.maxOfflineProductionHours} hours of work at a time,
        and stop at whatever your storage room holds. What they make is contraband, so a full lab and a
        full store draw heat whether you are here or not.
      </p>}
    </section>

    <HideoutMoralePanel dashboard={dashboard} busy={busy} act={act} />
  </div>
}

/**
 * Mule runs: crew sent to another town to buy cheap and carry home.
 *
 * Built around the one number that decides whether a run is worth making - what a unit costs there
 * against what it fetches here - because every other figure on the page is a consequence of it.
 */
function MulePage(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const [board, setBoard] = useState<MuleBoard | null>(null)
  const [quote, setQuote] = useState<MuleQuote | null>(null)
  const [error, setError] = useState('')
  const [city, setCity] = useState('')
  const [good, setGood] = useState('weed')
  const [hoes, setHoes] = useState(3)
  const [cash, setCash] = useState(10000)
  const [pimpId, setPimpId] = useState<number | null>(null)

  const load = async () => {
    try {
      const next = await api.mules()
      setBoard(next)
      setError('')
      if (!city && next.destinations.length > 0) setCity(next.destinations[0].city)
      if (pimpId === null) setPimpId(next.pimps.find(p => !p.isAway)?.id ?? null)
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.city, dashboard.turns, dashboard.hoes])

  // Re-quoted whenever the shape of the run changes, since every figure on the ticket moves with it.
  useEffect(() => {
    if (!city) return
    let stale = false
    void (async () => {
      try {
        const next = await api.muleQuote(city, good, hoes, cash)
        if (!stale) setQuote(next)
      } catch { if (!stale) setQuote(null) }
    })()
    return () => { stale = true }
  }, [city, good, hoes, cash])

  if (!board) return <div className="d-grid gtc-1 gap-3 align-items-start"><section className="card p-3 gcol-full">
    <div className="panel-title" data-area="mules"><h2>Mules</h2><span>Loading</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
  </section></div>

  const free = board.pimps.filter(p => !p.isAway)
  const out = board.runs.filter(r => r.status !== 'Done')
  const home = board.runs.filter(r => r.status === 'Done')
  const spread = quote ? quote.homePrice - quote.unitPriceThere : 0
  const unspendable = quote ? quote.cashSent - quote.unitsAffordable * quote.unitPriceThere : 0
  const supplyBlocked = quote
    ? dashboard.condoms < quote.condomsNeeded || dashboard.beer + dashboard.moonshine < quote.beerNeeded
    : false

  const send = async () => {
    if (!pimpId) return
    await act(() => api.launchMule(city, good, hoes, cash, pimpId))
    await load()
  }

  return <div className="d-grid gtc-1 gap-3 align-items-start">
    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>Mule Runs</h2>
        <span>{board.runsOut} of {board.concurrentRunCap} out</span>
      </div>
      {error && <div className="alert alert-danger"><span>{error}</span></div>}
      <p>
        Send a pimp and hoes to another town to buy cheap and carry it home. Going yourself costs the
        distance in turns each way and leaves you standing in the wrong town. A run costs a fraction of
        that in turns, but it takes real time, the crew earn nothing while they are gone, and you pay
        their fares and keep before anybody leaves.
      </p>
      {board.concurrentRunCap === 0 && <div className="alert alert-danger">
        {/* Two different reasons for the same zero, and they want two different sentences: one is
            something to go and buy, the other is something already paid for that somebody kicked in.
            Telling a raided player to build the centre they are looking at is the game losing track. */}
        <span>{dashboard.hideout.damage?.some(room => room.room === 'intelligence')
          ? 'Your intelligence centre is wrecked, so nobody is briefing routes and no runs leave. Repair it under Business / Hideout.'
          : 'You need an intelligence centre before you can run mules. Build one under Business / Hideout.'}</span>
      </div>}
    </section>

    {board.concurrentRunCap > 0 && <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Plan a run</h2><span>Prices are what they cost there</span></div>
      <div className="d-grid gtc-fill-150 gap-2 mb-3">
        <label className="field small">Town<select className="form-select" value={city} onChange={e => setCity(e.target.value)}>
          {board.destinations.map(d => <option key={d.city} value={d.city}>
            {d.city} - {d.flightMinutes}m each way, {d.risk.toLowerCase()} risk
          </option>)}
        </select></label>
        <label className="field">Good<select className="form-select" value={good} onChange={e => setGood(e.target.value)}>
          <option value="weed">Weed</option>
          <option value="coke">Coke</option>
        </select></label>
        <label className="field">Hoes<input className="form-control"
          type="number"
          min={1}
          max={Math.min(board.maxHoesPerRun, Math.max(1, board.hoesAvailable))}
          value={hoes}
          onChange={e => setHoes(Number(e.target.value))}
        /></label>
        <label className="field">Cash to send<input className="form-control"
          type="number"
          min={0}
          step={1000}
          value={cash}
          onChange={e => setCash(Number(e.target.value))}
        /></label>
        <label className="field">Led by<select className="form-select" value={pimpId ?? ''} onChange={e => setPimpId(Number(e.target.value))}>
          {free.length === 0 && <option value="">No pimp free</option>}
          {free.map(p => <option key={p.id} value={p.id}>{p.name} - {p.loyalty}% loyal</option>)}
        </select></label>
      </div>

      {quote && <div className="border rounded bg-body-secondary p-3">
        <div className="tnum d-grid gtc-fill-130 gap-2 mb-3">
          <MuleFigure label="Buys there" value={`${money.format(quote.unitPriceThere)} each`} />
          <MuleFigure label="Sells here" value={`${money.format(quote.homePrice)} each`} tone={spread > 0 ? 'good' : 'bad'} />
          <MuleFigure label="They can carry" value={`${number.format(quote.capacity)} ${quote.good}`} />
          <MuleFigure label="Your money buys" value={`${number.format(quote.unitsAffordable)} ${quote.good}`} />
          <MuleFigure label="Turns" value={number.format(quote.turns)} />
          <MuleFigure label="Round trip" value={`${quote.tripMinutes} min`} />
          <MuleFigure label="Fares and keep" value={money.format(quote.fare + quote.upkeep)} />
          <MuleFigure label="Supply keep" value={muleSupplyLabel(quote)} tone={supplyBlocked ? 'bad' : undefined} />
          <MuleFigure label="Spent on goods" value={money.format(quote.projectedSpend)} />
          <MuleFigure
            label="Profit if clean"
            value={money.format(quote.projectedProfit)}
            tone={quote.projectedProfit > 0 ? 'good' : 'bad'}
          />
          <MuleFigure label="Caught" value={`${quote.bustChancePercent}%`} tone={quote.bustChancePercent >= 25 ? 'bad' : undefined} />
          <MuleFigure label="He runs" value={`${quote.defectChancePercent}%`} tone={quote.defectChancePercent > 0 ? 'bad' : undefined} />
        </div>
        {/* The spread alone does not decide it: fares and keep are paid whether or not the run pays. */}
        <p className={`mb-2 ${quote.projectedProfit > 0 ? 'text-success-emphasis' : 'text-danger-emphasis'}`}>
          {spread <= 0
            ? `${quote.good} is no cheaper in ${quote.destinationCity} than it is here. There is nothing to make on this route.`
            : quote.projectedProfit <= 0
              ? `A clean run still loses ${money.format(-quote.projectedProfit)}. The ${money.format(quote.fare + quote.upkeep)} in fares and keep is more than ${number.format(quote.unitsAffordable)} ${quote.good} makes at ${money.format(spread)} a unit. Send more hoes, or find a wider spread.`
              : `Clean, this run comes home ${money.format(quote.projectedProfit)} up: ${number.format(quote.unitsAffordable)} ${quote.good} worth ${money.format(quote.projectedGross)}, less ${money.format(quote.fare + quote.upkeep + quote.projectedSpend)} spent getting it.`}
        </p>
        {unspendable > 0 && <p className="text-body-tertiary small mt-3">
          {money.format(unspendable)} of what you send cannot be spent: {quote.hoes} hoe(s) only carry {number.format(quote.capacity)}.
          It comes home with them, unless they are stopped, in which case it is taken too.
        </p>}
        {supplyBlocked && <p className="text-danger-emphasis small mt-3">
          Needs {muleSupplyNeedLabel(quote)} for {quote.supplyTurns} upkeep turn{quote.supplyTurns === 1 ? '' : 's'}.
          You have {number.format(dashboard.condoms)} condoms and {number.format(dashboard.beer + dashboard.moonshine)} beer or moonshine.
        </p>}
        <Button
          className="btn btn-primary"
          blocked={firstReason(
            busy && BUSY,
            !pimpId && 'Nobody is free to lead the run. Every pimp you have is already away.',
            board.runsOut >= board.concurrentRunCap && `You already have ${board.runsOut} run${board.runsOut === 1 ? '' : 's'} out, which is all your hideout can keep track of.`,
            hoes > board.hoesAvailable && `You are sending ${hoes} and only ${board.hoesAvailable} ${board.hoesAvailable === 1 ? 'is' : 'are'} free to go.`,
            supplyBlocked && `They will not travel unsupplied. The run needs ${muleSupplyNeedLabel(quote)}.`,
          )}
          onClick={() => void send()}
        >
          Send {quote.hoes} hoe(s) to {quote.destinationCity}
        </Button>
      </div>}
    </section>}

    {out.length > 0 && <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>In the air</h2><span>{out.length} out</span></div>
      <div className="d-grid gap-2 mt-3">
        {out.map(run => <div className="room-row" key={run.id}>
          <div className="room-copy">
            <strong>{run.pimpName} to {run.destinationCity}</strong>
            <span>
              {run.hoes} hoe(s) carrying up to {number.format(run.capacity)} {run.good}, {money.format(run.cashSent)} to buy with
            </span>
            <small>
              {run.status === 'Outbound' ? 'On the way out' : 'On the way back'} - {run.bustChancePercent}% caught, {run.defectChancePercent}% he runs
            </small>
          </div>
          <em>{run.secondsRemaining > 0 ? `${Math.ceil(run.secondsRemaining / 60)}m` : 'Landing'}</em>
        </div>)}
      </div>
    </section>}

    {home.length > 0 && <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Recently home</h2><span>Last 12 hours</span></div>
      <div className="d-grid gap-2 mt-3">
        {home.map(run => <div className={`room-row ${run.outcome === 'Delivered' ? '' : 'border-start-thick border-start-danger'}`} key={run.id}>
          <div className="room-copy">
            <strong>{run.pimpName} from {run.destinationCity}</strong>
            <span>{run.summary}</span>
          </div>
          <em>{run.outcome}</em>
        </div>)}
      </div>
    </section>}
  </div>
}

function MuleFigure({ label, value, tone }: { label: string, value: string, tone?: 'good' | 'bad' }) {
  const value_tone = tone === 'good' ? 'text-success' : tone === 'bad' ? 'text-danger' : 'text-body'
  return <div className="d-grid gap-1 min-w-0">
    <span className="eyebrow">{label}</span>
    <strong className={`fs-6 text-truncate ${value_tone}`}>{value}</strong>
  </div>
}

function muleSupplyLabel(quote: MuleQuote) {
  const beer = quote.moonshineUsed > 0
    ? `${number.format(quote.beerUsed)} beer + ${number.format(quote.moonshineUsed)} moonshine`
    : `${number.format(quote.beerNeeded)} beer`
  return `${number.format(quote.condomsNeeded)} condoms / ${beer}`
}

function muleSupplyNeedLabel(quote: MuleQuote) {
  return `${number.format(quote.condomsNeeded)} condoms and ${number.format(quote.beerNeeded)} beer or moonshine`
}

function CapacityBar({ label, used, cap, money: asMoney = false }: { label: string, used: number, cap: number, money?: boolean }) {
  const percent = cap <= 0 ? 0 : Math.min(100, (used / cap) * 100)
  const over = used > cap
  const format = (value: number) => asMoney ? money.format(value) : number.format(value)
  // A room filling up is a warning and a room overflowing is a problem, so the
  // bar says which by colour rather than only by length.
  const edge = over ? 'border-danger' : percent >= 90 ? 'border-warning' : ''
  const fill = over ? 'bg-danger' : percent >= 90 ? 'bg-warning' : ''
  return <div className={`d-grid gap-1 border rounded bg-body-tertiary px-3 py-2 ${edge}`}>
    <div className="d-flex justify-content-between align-items-baseline gap-2">
      <span className="eyebrow">{label}</span>
      <strong className="text-body">{format(used)} / {format(cap)}</strong>
    </div>
    {/*
      Bootstrap's progress bar, which is exactly this: a track, a fill, and a value the assistive
      layer can read out. The width still comes from an inline style, because it is data.
    */}
    <div className="progress" role="progressbar" aria-label={`${label} capacity`} aria-valuenow={Math.round(percent)} aria-valuemin={0} aria-valuemax={100}>
      <div className={`progress-bar ${fill}`} style={{ width: `${Math.max(2, percent)}%` }} />
    </div>
    {over && <small className="text-danger">More than the room holds. Nothing is lost, but nothing more comes in until it goes down.</small>}
  </div>
}

function WorkshopUnlockGrid({ dashboard }: { dashboard: Dashboard }) {
  const [open, setOpen] = useState(false)
  const stations = dashboard.hideout.stations ?? []
  const workshop = stations.find(station => station.key === 'workshop')
  if (!workshop) return null

  const currentLevel = workshop.level
  const craftRows = [
    ...(dashboard.hideout.production ?? [])
      .map(product => ({
        key: product.key,
        label: product.name,
        requirement: product.requiredWorkshopLevel,
        cost: product.costPerWork,
      })),
    ...dashboard.weaponRack
      .filter(tier => tier.forgeCost !== null)
      .map(tier => ({
        key: tier.key,
        label: tier.label,
        requirement: tier.minWorkshopLevel ?? 1,
        cost: tier.forgeCost ?? 0,
      })),
    ...stations
      .filter(station => station.key !== 'workshop')
      .map(station => ({
        key: station.key,
        label: station.name,
        requirement: station.requiredWorkshopLevel,
        cost: station.costPerUnit,
      })),
  ].sort((a, b) => a.requirement - b.requirement || a.label.localeCompare(b.label))
  const unlocked = craftRows.filter(craft => currentLevel >= craft.requirement).length
  const collapseId = 'workshop-unlocks'

  return <div className="workshop-unlocks border-start border-primary-subtle ps-3 py-1">
    <button
      className="btn btn-sm btn-outline-secondary d-inline-flex align-items-center gap-2"
      type="button"
      aria-expanded={open}
      aria-controls={collapseId}
      onClick={() => setOpen(value => !value)}
    >
      <i className={`bi ${open ? 'bi-chevron-up' : 'bi-chevron-down'}`} aria-hidden="true" />
      <span>Workshop unlocks</span>
      <span className="badge text-bg-secondary">{unlocked}/{craftRows.length}</span>
    </button>
    <div id={collapseId} className={`collapse ${open ? 'show' : ''}`}>
      <div className="d-grid gtc-fill-180 gap-2 mt-2">
        {craftRows.map(craft => {
          const craftUnlocked = currentLevel >= craft.requirement
          return <div className={`border rounded bg-body-tertiary p-2 ${craftUnlocked ? 'border-success' : ''}`} key={craft.key}>
            <div className="d-flex justify-content-between align-items-baseline gap-2">
              <strong className="text-body">{craft.label}</strong>
              <span className={`eyebrow ${craftUnlocked ? 'text-success-emphasis' : 'text-warning-emphasis'}`}>
                {craftUnlocked ? 'Unlocked' : `Level ${craft.requirement}`}
              </span>
            </div>
            <small className="text-body-tertiary">Materials {money.format(craft.cost)} each</small>
          </div>
        })}
      </div>
    </div>
  </div>
}

/**
 * The craft bench. Each craft is shown next to the price it exists to beat, because a recipe whose
 * output costs more than the thing it replaces has no reason to be used.
 */
function WorkshopCraftPanel({ dashboard, busy, act, sellQty, setSellQty }: {
  dashboard: Dashboard
  busy: boolean
  act: PageContext['act']
  sellQty: Record<'weed' | 'coke', number>
  setSellQty: React.Dispatch<React.SetStateAction<Record<'weed' | 'coke', number>>>
}) {
  const [turns, setTurns] = useState<Record<string, number>>({})
  const [productionWork, setProductionWork] = useState<Record<'weed' | 'coke', number>>({ weed: 5, coke: 5 })
  const stations = dashboard.hideout.stations ?? []
  const production = dashboard.hideout.production ?? []
  if (stations.length === 0 && production.length === 0) return null

  const activeCraft = dashboard.hideout.workshopCraft ?? null
  const craftMinutes = Math.max(1, dashboard.hideout.craftMinutesPerWork ?? 1)
  const workshop = stations.find(x => x.key === 'workshop')
  const workshopLevel = workshop?.level ?? 0
  const gunRate = Math.max(1, workshop?.perTurn ?? 0)
  const crafts = [
    ...dashboard.weaponRack
      .filter(tier => tier.forgeCost !== null)
      .map(tier => ({
        key: tier.key,
        name: tier.label,
        good: tier.key,
        level: workshopLevel >= (tier.minWorkshopLevel ?? 1) ? workshopLevel : 0,
        perTurn: gunRate,
        costPerUnit: tier.forgeCost ?? 0,
        comparePrice: tier.price,
        compareLabel: `store ${tier.label.toLowerCase()}`,
        heatPerUnit: 0,
        requiredWorkshopLevel: tier.minWorkshopLevel ?? 1,
        weapon: tier.key,
      })),
    ...stations
      .filter(station => station.key !== 'workshop')
      .map(station => ({ ...station, weapon: undefined })),
  ].sort((a, b) => a.requiredWorkshopLevel - b.requiredWorkshopLevel || a.name.localeCompare(b.name))

  return <section className="card p-3 gcol-full">
    <div className="panel-title" data-area="craft-queue"><h2>Craft Queue</h2><span>Workbench crafts</span></div>
    <p>
      Weed, coke, guns and back-room goods all run through the same queue. Turns and cash
      are paid up front, and the finished batch lands in storage when the timer clears.
    </p>
    {activeCraft && <div className="alert alert-primary d-flex flex-wrap justify-content-between align-items-center gap-2 mt-3 mb-0">
      <div>
        <strong className="d-block">Crafting {number.format(activeCraft.quantity)} {activeCraft.label.toLowerCase()}</strong>
        <span className="small">Spent {number.format(activeCraft.workUnits)} turn{activeCraft.workUnits === 1 ? '' : 's'} and {money.format(activeCraft.totalCost)}.</span>
      </div>
      <b className="text-nowrap">Ready in {timeUntil(activeCraft.completesAtUtc)}</b>
    </div>}
    <div className="d-grid gap-2 mt-3">
      {production.map(station => {
        const key = station.key
        const workUnits = productionWork[key] ?? 5
        const minQuantity = station.minPerWork * workUnits
        const maxQuantity = station.maxPerWork * workUnits
        const quantityLabel = minQuantity === maxQuantity
          ? number.format(minQuantity)
          : `${number.format(minQuantity)}-${number.format(maxQuantity)}`
        const totalCost = station.costPerWork * workUnits
        const held = key === 'weed' ? dashboard.weed : dashboard.coke
        const sellPrice = key === 'coke' ? dashboard.cokeSellPriceAtPurity : station.sellPrice
        const saleQty = sellQty[key]
        const built = workshopLevel >= station.requiredWorkshopLevel
        // Read in the order the player would ask it, so the one sentence they get back is the first
        // thing standing in the way rather than the last.
        const whyNot = firstReason(
          busy && BUSY,
          !!activeCraft && `The bench is busy making ${activeCraft.label.toLowerCase()}, ready in ${timeUntil(activeCraft.completesAtUtc)}.`,
          workUnits < 1 && 'Set the batch to at least one work unit.',
          workUnits > dashboard.maxActionTurns && `You can spend ${dashboard.maxActionTurns} turns at a time at most.`,
          workUnits > dashboard.turns && `That batch wants ${workUnits} turns and you have ${dashboard.turns}.`,
          totalCost > dashboard.cash && `Materials come to ${money.format(totalCost)} and you are carrying ${money.format(dashboard.cash)}.`,
        )
        return <div className="room-row border-start-thick border-start-danger" key={station.key}>
          <div className="room-copy">
            <strong className="text-body">{station.name}<small className="ms-1 eyebrow text-danger"> contraband</small></strong>
            <span className="text-body-secondary small">
              {built
                ? `${number.format(station.minPerWork)}-${number.format(station.maxPerWork)} ${station.key} per work unit at ${money.format(station.costPerWork)} each, selling here for ${money.format(sellPrice)}. ${station.sellLabel}.`
                : `Needs a level ${station.requiredWorkshopLevel} workshop. Queues ${station.key} production for the ${station.sellLabel.toLowerCase()}.`}
            </span>
            {built && <small className="text-body-tertiary">
              Current batch: {quantityLabel} for {money.format(totalCost)} and {number.format(workUnits)} turn{workUnits === 1 ? '' : 's'}, ready in {formatCraftMinutes(workUnits * craftMinutes)}.
              {station.labBonusPercent > 0 && ` Lab bonus adds ${station.labBonusPercent}%.`}
            </small>}
            {built && <small className="text-warning-emphasis small">
              Each one held adds {station.heatPerUnit} heat. Make and sell rather than stockpile.
            </small>}
          </div>
          <em className="eyebrow fst-normal small">{built ? `${number.format(held)} held` : `Needs L${station.requiredWorkshopLevel}`}</em>
          <div className="d-flex flex-wrap align-items-end gap-1 mt-1">
            {built && <>
              <label className="field">Work<input className="form-control"
                type="number"
                min={1}
                max={dashboard.maxActionTurns}
                value={workUnits}
                onChange={e => setProductionWork(v => ({ ...v, [key]: Number(e.target.value) }))}
              /></label>
              <Button
                className="btn btn-primary btn-sm"
                blocked={whyNot}
                onClick={() => void act(() => api.produce(key, workUnits))}
              >
                Queue {quantityLabel}
              </Button>
            </>}
            <label className="field">Sell Qty<input className="form-control"
              type="number"
              min={1}
              max={Math.max(1, held)}
              value={saleQty}
              onChange={e => setSellQty(v => ({ ...v, [key]: Number(e.target.value) }))}
            /></label>
            <Button
              className="btn btn-secondary btn-sm"
              blocked={firstReason(
                busy && BUSY,
                saleQty < 1 && 'Set how much to sell first.',
                saleQty > held && `You are selling ${number.format(saleQty)} and you hold ${number.format(held)}.`,
              )}
              onClick={() => void act(() => api.sellProduct(key, saleQty))}
            >
              Sell
            </Button>
          </div>
        </div>
      })}
      {crafts.map(station => {
        const runTurns = turns[station.key] ?? 5
        const built = station.level > 0
        const quantity = station.perTurn * runTurns
        const totalCost = station.costPerUnit * quantity
        const whyNot = firstReason(
          busy && BUSY,
          !!activeCraft && `The bench is busy making ${activeCraft.label.toLowerCase()}, ready in ${timeUntil(activeCraft.completesAtUtc)}.`,
          runTurns < 1 && 'Set the run to at least one work unit.',
          runTurns > dashboard.maxActionTurns && `You can spend ${dashboard.maxActionTurns} turns at a time at most.`,
          runTurns > dashboard.turns && `That run wants ${runTurns} turns and you have ${dashboard.turns}.`,
          totalCost > dashboard.cash && `Materials come to ${money.format(totalCost)} and you are carrying ${money.format(dashboard.cash)}.`,
        )
        return <div
          className={`room-row ${station.heatPerUnit > 0 ? 'border-start-thick border-start-danger' : ''}`}
          key={station.key}
        >
          <div className="room-copy">
            <strong className="text-body">{station.name}{station.heatPerUnit > 0 && <small className="ms-1 eyebrow text-danger"> contraband</small>}</strong>
            <span className="text-body-secondary small">
              {built
                ? `${number.format(station.perTurn)} ${station.good} per work unit at ${money.format(station.costPerUnit)} each, against ${money.format(station.comparePrice)} for ${station.compareLabel}`
                : `Needs a level ${station.requiredWorkshopLevel} workshop. Makes ${station.good} for less than ${money.format(station.comparePrice)}, the price of ${station.compareLabel}.`}
            </span>
            {built && <small className="text-body-tertiary">
              Current batch: {number.format(quantity)} for {money.format(totalCost)} and {number.format(runTurns)} turn{runTurns === 1 ? '' : 's'}, ready in {formatCraftMinutes(runTurns * craftMinutes)}.
            </small>}
            {station.heatPerUnit > 0 && built && <small className="text-warning-emphasis small">
              Each one held adds {station.heatPerUnit} heat. Make and sell rather than stockpile.
            </small>}
          </div>
          <em className="eyebrow fst-normal small">{built ? `Level ${station.level}` : `Needs L${station.requiredWorkshopLevel}`}</em>
          <div className="d-flex flex-wrap align-items-end gap-1 mt-1">
            {built && <>
              <label className="field">Work<input className="form-control"
                type="number"
                min={1}
                max={dashboard.maxActionTurns}
                value={runTurns}
                onChange={e => setTurns(v => ({ ...v, [station.key]: Number(e.target.value) }))}
              /></label>
              <Button
                className="btn btn-primary btn-sm"
                blocked={whyNot}
                onClick={() => void act(() => api.forge(runTurns, station.weapon ? 'workshop' : station.key, station.weapon as WeaponTierKey | undefined))}
              >
                Queue {number.format(quantity)}
              </Button>
            </>}
          </div>
        </div>
      })}
    </div>

    <CutCokePanel dashboard={dashboard} busy={busy} act={act} />
  </section>
}

/**
 * Stepping on the coke you already hold.
 *
 * Sits under the stations rather than inside the mix house row because it is not a station: the mix
 * house makes cut, and this spends it. The coke worth stretching is usually coke that was never
 * produced here at all - off a plane, off the board, out of a lab overnight.
 */
function CutCokePanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const hideout = dashboard.hideout
  // Cut comes off the workshop bench now rather than a mix house of its own, so what gates this is how
  // deep the bench is. The server reports the recipe as a row of its own with a level of zero until the
  // shop can reach it, which is exactly the condition to hide the panel on.
  const mix = hideout.stations?.find(s => s.key === 'cut')
  const [turns, setTurns] = useState(5)
  if (!mix || mix.level === 0) return null

  const perTurn = 10 * mix.level
  const room = Math.max(0, hideout.maxCoke - dashboard.coke)
  // Every limit at once, the same way the server bounds it, so the button never promises a batch the
  // rules will refuse.
  const batch = Math.min(turns * perTurn, dashboard.cut, dashboard.coke, room)
  const turnsNeeded = Math.max(1, Math.ceil(batch / perTurn))
  // Mirrors the server: blending is a weighted average, and price follows the square root of purity.
  const purity = dashboard.cokePurityPercent / 100
  const blended = dashboard.coke + batch <= 0 ? 1 : (dashboard.coke * purity) / (dashboard.coke + batch)
  const afterPurity = Math.round(blended * 100)
  const list = dashboard.cokeSellPrice
  const afterPrice = Math.max(1, Math.round(list * Math.sqrt(blended)))
  const nowValue = dashboard.coke * dashboard.cokeSellPriceAtPurity
  const afterValue = (dashboard.coke + batch) * afterPrice
  const blocked = dashboard.cut <= 0
    ? 'You have no cut to work with.'
    : dashboard.coke <= 0
      ? 'You have no coke to step on.'
      : room <= 0
        ? 'Your store is full of coke already.'
        : null

  return <div className="mt-3 border border-start-thick border-start-danger rounded p-3 bg-body-secondary">
    <div className="d-flex flex-wrap justify-content-between align-items-baseline gap-2">
      <strong>Step on it</strong>
      <span>
        {number.format(dashboard.cut)} cut, {number.format(dashboard.coke)} coke at {dashboard.cokePurityPercent}% pure,
        room for {number.format(room)} more
      </span>
    </div>
    <p>
      One unit of cut makes one unit of coke, on any coke you hold however it got here. What it costs
      is strength: the pile grows and weakens together, and buyers pay for what is actually in it. A
      stretch still pays, but each one pays less than the last, and a bigger pile of coke draws more
      notice than anything else you can hold.
    </p>
    {/* What the batch does, only where there is a batch. Why there is not is on the button. */}
    {!blocked && <p className="text-body-tertiary small mt-3">
      {number.format(batch)} coke from {number.format(batch)} cut, in {turnsNeeded} turn{turnsNeeded === 1 ? '' : 's'}.
      {' '}Purity {dashboard.cokePurityPercent}% to {afterPurity}%, so a unit drops from{' '}
      {money.format(dashboard.cokeSellPriceAtPurity)} to about {money.format(afterPrice)}.
      {batch < turns * perTurn && ' That is everything available.'}
    </p>}
    <p className={`mb-2 ${afterValue > nowValue ? 'text-success-emphasis' : 'text-danger-emphasis'}`}>
      {afterValue > nowValue
        ? `Worth ${money.format(afterValue - nowValue)} more in total: ${number.format(dashboard.coke + batch)} weaker units beat ${number.format(dashboard.coke)} clean ones, for now.`
        : 'This pile is already stretched too thin. More filler is worth less than the room it takes.'}
    </p>
    <div className="territory-actions">
      <label className="field">Turns<input className="form-control"
        type="number"
        min={1}
        max={dashboard.maxActionTurns}
        value={turns}
        onChange={e => setTurns(Number(e.target.value))}
      /></label>
      <Button
        className="btn btn-primary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          blocked,
          batch <= 0 && 'There is nothing to stretch at this size.',
          turnsNeeded > dashboard.turns && `That batch wants ${turnsNeeded} turn${turnsNeeded === 1 ? '' : 's'} and you have ${dashboard.turns}.`,
        )}
        onClick={() => void act(() => api.cutCoke(turns))}
      >
        Cut {number.format(batch)} coke
      </Button>
    </div>
  </div>
}

function HideoutTierPanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const hideout = dashboard.hideout
  const building = hideout.building
  const next = hideout.nextTier
  // Cash and bank together, matching what the server charges. Checking cash on hand alone greyed the
  // button out for exactly the players who could afford it, since a tier costs more than any safe below
  // it holds and the rest of their money is necessarily in the bank.
  const canAffordTier = !next || dashboard.cash + dashboard.bankCash >= next.cost

  // The panel keeps its own second hand. The app-wide one stops once turns are maxed, which would
  // otherwise freeze the countdown for exactly the players most likely to be building something.
  const [, setNow] = useState(0)
  useEffect(() => {
    if (!building) return
    const timer = window.setInterval(() => setNow(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [building?.completesAtUtc])

  // Hours of being somewhere else, which is what a turn bank actually buys. Read off the rate this
  // player earns at rather than the base one, so a new player is told the truth about their own clock.
  const hoursAway = (turns: number) => dashboard.turnsPerTick > 0
    ? Math.round(turns / dashboard.turnsPerTick * dashboard.turnTickMinutes / 60)
    : 0

  return <section className="card p-3 gcol-full">
    <div className="panel-title">
      <h2>The Building</h2>
      {/* The one number that used to be missing. A hideout counted for nothing on the board, so the
          biggest purchase in the game read as money burned; saying what it is worth is how a player
          can tell that an upgrade cost them nothing but the cash. */}
      <span>Worth {money.format(hideout.value)} on the board</span>
    </div>
    {building
      ? <div className="d-grid gap-1 mt-3 border border-warning rounded px-3 py-3">
        <strong>Building the {building.name}</strong>
        <span>Ready in {timeUntil(building.completesAtUtc)}. Your crew caps stay where they are until it lands.</span>
      </div>
      : next
        ? <>
          {/* The building raises the ceiling; the store decides how much of it you can actually use.
              Saying a move "raises your crew caps" was the same promise the hideout page used to make
              and could not keep - a player who buys a Warehouse for the hoes and finds the number has
              not moved has been sold something by their own game. */}
          <p>
            Moving up to the <strong>{next.name}</strong> raises the ceiling on your crew to{' '}
            {number.format(next.maxPimps)} pimps, {number.format(next.maxHoes)} hoes and{' '}
            {number.format(next.maxThugs)} thugs, and unlocks the rooms your current building is too
            small to hold. Your crew reaches that ceiling as the store grows into it: a room only ever
            supplies what it can feed for a full shift, and that is the number the caps show.
          </p>
          {/* The half of the purchase nothing else on the page mentions. A bigger building does not
              pay turns any faster - nothing does - it holds more of the ones you are already owed, so
              being away from the game for a night stops throwing them away. */}
          {next.maxTurns > dashboard.maxTurns && <p>
            It also holds <strong>{number.format(next.maxTurns)} turns</strong> against your{' '}
            {number.format(dashboard.maxTurns)}: {hoursAway(next.maxTurns)} hours away from this screen
            before the bank fills and stops, rather than {hoursAway(dashboard.maxTurns)}. Turns come
            back at the same rate they always did. What changes is how many of them are still there
            when you get back.
          </p>}
          <div className="room-row">
            <div className="room-copy">
              <strong>{next.name}</strong>
              <span>
                {money.format(next.cost)} and {next.turns} turns. Takes {next.buildMinutes} minutes to build.
                Paid from the bank first, then cash on hand.
              </span>
            </div>
            <em>Tier {next.level}</em>
            <Button
              className="btn btn-primary"
              blocked={firstReason(
                busy && BUSY,
                !canAffordTier && `The ${next.name} costs ${money.format(next.cost)} and you have ${money.format(dashboard.cash + dashboard.bankCash)} between cash and the bank.`,
                dashboard.turns < next.turns && `Starting the build costs ${next.turns} turns and you have ${dashboard.turns}.`,
              )}
              onClick={() => void act(() => api.upgradeHideout('tier'))}
            >
              {!canAffordTier ? 'You cannot cover it' : dashboard.turns < next.turns ? `${next.turns} turns and you have ${dashboard.turns}` : 'Start building'}
            </Button>
          </div>
        </>
        : <p>The {hideout.tierName} is the biggest building there is. Nothing left to move up to.</p>}
  </section>
}

// funds, not cash on hand: the server pays for a room from the bank first, because the safe is one of
// the things being bought and several rooms cost more than the safe below them holds.
/**
 * One room, and the two things that can be done to it.
 *
 * A wrecked room takes the row over rather than getting a badge on the side of it. That is the point:
 * the level a player paid for is still there and still worthless, and a row that reads "Level 4" with
 * a small red word next to it is a row somebody scrolls past on the way to the upgrade they meant to
 * buy. While it is down the row says what has stopped, what it costs to undo, and nothing else - the
 * upgrade button is not an option here, because the server will not sell a level on top of a wreck.
 */
/**
 * The two things you can tell a lab: whether to run, and what to do with what it makes.
 *
 * Production stopped being free the day holding stock started drawing the law and a raid started
 * taking half of it. There are nights when the right move is to stop making the stuff - you are
 * Hunted, the pile is why, and a lab quietly topping it up every hour is working against you.
 *
 * Selling is the other answer to the same problem: cash draws no attention at all and can be banked
 * out of a raider's reach, which product never can. It costs the spread, since it takes the local
 * price the hour it is made rather than whatever it would fetch somewhere worth carrying it to.
 */
function LabSwitches({ product, level, running, autoSell, minSellLevel, busy, act }: {
  product: 'weed' | 'coke'
  level: number
  running: boolean
  autoSell: boolean
  minSellLevel: number
  busy: boolean
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
}) {
  if (level <= 0) return null
  const canSell = level >= minSellLevel

  return <div className="d-flex flex-wrap align-items-center gap-2 mt-1">
    <Button
      className={`btn btn-sm ${running ? 'btn-secondary' : 'btn-primary'}`}
      blocked={busy && BUSY}
      onClick={() => void act(() => api.setLab(product, !running, autoSell))}
    >{running ? 'Switch off' : 'Switch on'}</Button>
    <Button
      className={`btn btn-sm ${autoSell ? 'btn-primary' : 'btn-secondary'}`}
      blocked={firstReason(
        busy && BUSY,
        !running && 'A lab that is switched off has nothing to sell.',
        !canSell && `Selling its own output needs level ${minSellLevel}. This one is level ${level}.`,
      )}
      onClick={() => void act(() => api.setLab(product, running, !autoSell))}
    >{autoSell ? 'Selling output' : 'Sell output'}</Button>
    <small className="text-body-tertiary">
      {!running
        ? 'Off. It makes nothing, and the hours it is off are gone rather than owed.'
        : autoSell
          ? 'Sold at this town’s price as it is made, so nothing sits drawing heat.'
          : canSell
            ? 'Shelved, where it draws the law and a raid can take it.'
            : `Shelved. Level ${minSellLevel} can sell it instead.`}
    </small>
  </div>
}

function RoomRow({ name, level, detail, upgrade, funds, busy, onUpgrade, damage, repairing, onRepair, children }: {
  name: string
  level: number
  detail: string
  upgrade?: HideoutRoomUpgrade | null
  funds: number
  busy: boolean
  onUpgrade: () => void
  damage?: HideoutDamage | null
  repairing?: HideoutRepair | null
  onRepair?: () => void
  /** Controls belonging to this room, under its copy. Only the labs have any. */
  children?: React.ReactNode
}) {
  const tierLocked = upgrade?.tierLocked ?? false
  const workshopLocked = upgrade?.workshopLocked ?? false
  const locked = tierLocked || workshopLocked
  const underway = damage != null && repairing?.room === damage.room
  const elsewhere = damage != null && repairing != null && repairing.room !== damage.room

  return <div className={`room-row${damage ? ' room-row-wrecked' : ''}`}>
    <div className="room-copy">
      <strong>{name}</strong>
      {damage
        ? <span className="text-danger">Wrecked, so {damage.stops}.</span>
        : <span>{detail}</span>}
      {damage && <small className="text-body-secondary">
        {underway
          ? `The crew are in there. Working again in ${timeUntil(repairing!.completesAtUtc)}.`
          : `Putting it back costs ${money.format(damage.repairCost)} and takes ${damage.repairMinutes} minute(s).`}
      </small>}
      {/* What the upgrade actually returns. The later levels are meant to be a poor deal - somewhere
          for money to go once there is nothing left to buy - and saying so is the difference between
          a trophy and a room that quietly took a fortune while looking like an investment. */}
      {!damage && !locked && upgrade?.paybackDays != null && <small className={upgrade.paybackDays > 30 ? 'text-primary' : 'text-body-secondary'}>
        {upgrade.paybackDays > 30
          ? `Pays for itself in ${upgrade.paybackDays} days. A trophy more than an investment.`
          : `Pays for itself in ${upgrade.paybackDays} days.`}
      </small>}
      {/* Withheld while the room is down, because a switch on a wrecked lab is a control over
          nothing and the only move that room has is the repair button opposite. */}
      {!damage && children}
    </div>
    {/* The damage row carries the level off the deeds, which is the one a wrecked room has to show.
        Several rows read their level off what the room can currently do, and that is zero while it is
        down - so without this the workshop would report "Level 0, down" to somebody who paid for four. */}
    <em className={damage ? 'text-danger' : undefined}>
      {damage ? `Level ${damage.level}, down` : level === 0 ? 'Not built' : `Level ${level}`}
    </em>
    {damage
      ? <Button className="btn btn-danger" blocked={firstReason(
        busy && BUSY,
        underway && `The crew are already in the ${damage.name}. They are out in ${timeUntil(repairing!.completesAtUtc)}.`,
        elsewhere && `Your crew are in the ${repairing!.name} until ${timeUntil(repairing!.completesAtUtc)} from now. They can only be in one room at a time.`,
        funds < damage.repairCost && `Putting it back costs ${money.format(damage.repairCost)} and you have ${money.format(funds)} between cash and the bank.`,
      )} onClick={() => onRepair?.()}>
        {underway ? 'Being fixed' : `Repair ${money.format(damage.repairCost)}`}
      </Button>
      : <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !upgrade && `The ${name.toLowerCase()} is at its highest level. There is nothing left to buy here.`,
        tierLocked && workshopLocked && `Level ${upgrade!.level} wants the ${upgrade!.requiredTierName} or better and a level ${upgrade!.requiredWorkshopLevel} workshop.`,
        tierLocked && `Level ${upgrade!.level} wants the ${upgrade!.requiredTierName} or better. Move the building up first.`,
        workshopLocked && `Level ${upgrade!.level} wants a level ${upgrade!.requiredWorkshopLevel} workshop first.`,
        !!upgrade && funds < upgrade.cost && `That level costs ${money.format(upgrade.cost)} and you have ${money.format(funds)} between cash and the bank.`,
      )} onClick={onUpgrade}>
        {!upgrade ? 'Maxed' : locked ? 'Locked' : `Upgrade ${money.format(upgrade.cost)}`}
      </Button>}
  </div>
}

/**
 * The map. Ground is held with thugs who count as away from home, so what this page really shows is
 * how much of your crew is standing somewhere else.
 */
function TerritoryPage(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const [board, setBoard] = useState<TerritoryBoard | null>(null)
  const [error, setError] = useState('')
  const [thugs, setThugs] = useState<Record<number, number>>({})
  const [pimpFor, setPimpFor] = useState<Record<number, number | null>>({})

  const load = async () => {
    try { setBoard(await api.territories()); setError('') }
    catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.thugs, dashboard.turns])

  const run = async (fn: () => Promise<unknown>) => {
    await act(fn)
    await load()
  }

  if (!board) return <div className="d-grid gtc-1 gap-3 align-items-start"><section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Territory</h2><span>Loading</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
  </section></div>

  const effects = board.effects
  const anyEffect = effects.streetIncomePercent || effects.productionYieldPercent || effects.moraleRecoveryPercent || effects.lootPercent || board.allianceCityControl
  const force = (id: number) => thugs[id] ?? board.minimumGarrison
  const chosen = (id: number) => pimpFor[id] ?? null
  // Only pimps who are actually free. Anyone out commanding a raid, or already running other ground,
  // cannot take a second posting, and the server refuses it anyway.
  const freePimps = (id: number) => dashboard.crew.filter(p => !p.isCommanding
    && !board.territories.some(t => t.id !== id && t.heldByYou && t.garrisonPimpName === p.name))

  return <div className="d-grid gtc-1 gap-3 align-items-start">
    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>{board.city}</h2>
        <span>{board.held} of {board.holdingCap} held</span>
      </div>
      <p>
        This is {board.city}, and it is the only map you fight over.
        Holding ground takes {board.minimumGarrison} thugs standing on it, and they are not at home while they do.
        Each piece holds up to {board.maxGarrisonThugs}, and a raid can send up to {board.maxRaidThugs}.
        You have <strong>{number.format(board.freeThugs)}</strong> free of {number.format(dashboard.thugs)}.
        Claiming empty ground costs {board.claimTurnCost} turns; taking it off somebody costs a raid and one of your two lanes.
      </p>
      {anyEffect
        ? <div className="d-flex flex-wrap gap-2 mt-3">
          {effects.streetIncomePercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.streetIncomePercent}% street income</span>}
          {effects.productionYieldPercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.productionYieldPercent}% production</span>}
          {effects.moraleRecoveryPercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.moraleRecoveryPercent}% morale recovery</span>}
          {effects.lootPercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.lootPercent}% haul</span>}
          {board.allianceCityControl && <span className="badge rounded-pill text-bg-primary">+{board.allianceCityControl.bonusThugs} alliance thugs</span>}
        </div>
        : <p className="text-body-tertiary small mt-3">You hold no ground yet, so nothing out there is working for you.</p>}
      {error && <div className="alert alert-danger"><span>{error}</span></div>}
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>Working Ground Up</h2>
        <span>{board.developmentLadder.length} levels</span>
      </div>
      {/* Shown whole rather than a rung at a time. This is the one climb in the game measured in
          months, and a months-long climb whose shape nobody can see is not a goal, it is a surprise. */}
      <p>
        Money goes into the ground itself, not into your building, and it stays with the ground. Every
        level raises what a piece is worth and how hard the crew standing on it fights for it. It also
        makes the piece worth taking: whoever beats you off it keeps <strong>half</strong> of what you
        put in, rounded down, and never more than their own building could have built. Walk away from a
        piece and all of it is gone.
      </p>
      <div className="d-grid gtc-fill-268 gap-2 mt-3">
        {board.developmentLadder.map(rung => <div
          className={`d-grid gap-1 align-content-start border rounded p-3 ${rung.reachable ? 'bg-body-tertiary' : 'bg-body-secondary opacity-75'}`}
          key={rung.level}
        >
          <div className="d-flex justify-content-between align-items-baseline gap-2">
            <strong className="text-body">{rung.name}</strong>
            <em className="eyebrow fst-normal">Level {rung.level}</em>
          </div>
          <span className="text-warning-emphasis small">+{rung.effectPercent}% on what the ground does</span>
          <span className="text-success-emphasis small">+{rung.defencePercent}% to the garrison holding it</span>
          <span className="text-body-secondary small">
            {money.format(rung.cost)} and {rung.turns} turns, {rung.buildMinutes >= 60 ? `${Math.round(rung.buildMinutes / 60)} hour(s)` : `${rung.buildMinutes} minutes`} of work.
          </span>
          {!rung.reachable && <small className="text-body-tertiary small">Needs the {rung.requiredTierName}.</small>}
        </div>)}
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The Map</h2><span>{board.territories.length} pieces in {board.city}</span></div>
      <div className="d-grid gtc-fill-268 gap-2 mt-3">
        {board.territories.map(t => <div
          className={`d-grid gap-1 align-content-start border rounded bg-body-tertiary p-3 border-start-thick ${t.heldByYou ? 'border-start-success' : t.holderId ? 'border-start-danger' : 'border-start-warning'}`}
          key={t.id}
        >
          <div className="d-flex justify-content-between align-items-baseline gap-2">
            <strong className="text-body">{t.name}</strong>
            <em className="eyebrow fst-normal">{t.typeLabel}</em>
          </div>
          <span className="text-warning-emphasis small">{t.effect}</span>
          <span className="text-body-secondary small">
            {t.heldByYou ? `Yours, ${number.format(t.garrisonThugs)} thug(s) on it`
              : t.holderId ? `${t.holderName} holds it with ${number.format(t.garrisonThugs)} thug(s)`
                : 'Nobody holds this'}
            {' / '}{t.city}
          </span>
          {t.garrisonPimpName && <span className="text-success-emphasis small">
            Run by {t.garrisonPimpName}{t.garrisonBonusPercent > 0 ? ` (+${t.garrisonBonusPercent}% defence)` : ''}
          </span>}
          {/* Shown on anybody's ground. What a rival has put into a corner is exactly the thing that
              decides whether it is worth crossing town for, and hiding it would leave every raid a
              guess about the only number that matters. */}
          {t.developmentLevel > 0 && <span className="text-info-emphasis small">
            {t.developmentName} ground{t.developmentDefencePercent > 0 ? `, +${t.developmentDefencePercent}% to whoever holds it` : ''}
          </span>}
          {t.developing && <small className="text-warning small">
            Work under way: {t.developing.name} in {timeUntil(t.developing.completesAtUtc)}
          </small>}
          {t.isProtected && t.protectedUntilUtc && <small className="text-body-tertiary small">Settled for {timeUntil(t.protectedUntilUtc)}</small>}
          {t.blockedReason && <small className="text-body-tertiary small">{t.blockedReason}</small>}
          <div className="territory-actions d-flex flex-wrap align-items-end gap-1 mt-1">
            <label className="field">Thugs<input className="form-control"
              type="number"
              min={t.heldByYou ? 0 : board.minimumGarrison}
              max={t.canRaid ? board.maxRaidThugs : board.maxGarrisonThugs}
              value={force(t.id)}
              onChange={e => setThugs(v => ({ ...v, [t.id]: Number(e.target.value) }))}
            /></label>
            {(t.heldByYou || t.canClaim) && <label className="field territory-pimp w-100">Run by<select className="form-select w-100"
              value={chosen(t.id) ?? ''}
              onChange={e => setPimpFor(v => ({ ...v, [t.id]: e.target.value ? Number(e.target.value) : null }))}
            >
              <option value="">Nobody</option>
              {freePimps(t.id).map(p => <option key={p.id} value={p.id}>
                {p.name} ({p.specialty}{p.specialty === 'Enforcer' ? ` +${p.bonusPercent}%` : ''})
              </option>)}
            </select></label>}
            {t.heldByYou && <>
              <Button className="btn btn-secondary btn-sm" blocked={firstReason(
                busy && BUSY,
                force(t.id) > board.maxGarrisonThugs && `A corner takes ${board.maxGarrisonThugs} thugs at most and you have set ${force(t.id)}.`,
              )} onClick={() => void run(() => api.setGarrison(t.id, force(t.id), chosen(t.id)))}>Set garrison</Button>
              <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => void run(() => api.setGarrison(t.id, 0, null))}>Give up</Button>
            </>}
            {/* Priced, gated and timed on the button itself. A greyed control with no reason on it is
                the thing players come back to ask about, and this one is greyed for four different
                reasons. */}
            {t.heldByYou && !t.developing && t.nextDevelopment && <Button
              className="btn btn-primary btn-sm w-100"
              blocked={firstReason(
                busy && BUSY,
                t.nextDevelopment.tierLocked && `Working ${t.name} up to ${t.nextDevelopment.name} wants the ${t.nextDevelopment.requiredTierName} behind you first.`,
                dashboard.cash + dashboard.bankCash < t.nextDevelopment.cost && `The work costs ${money.format(t.nextDevelopment.cost)} and you have ${money.format(dashboard.cash + dashboard.bankCash)} between cash and the bank.`,
                dashboard.turns < t.nextDevelopment.turns && `The work costs ${t.nextDevelopment.turns} turns and you have ${dashboard.turns}.`,
              )}
              title={`${t.nextDevelopment.effectNow}% now, ${t.nextDevelopment.effectAfter}% once it lands`}
              onClick={() => void run(() => api.developTerritory(t.id))}
            >
              {t.nextDevelopment.tierLocked
                ? `${t.nextDevelopment.name} needs the ${t.nextDevelopment.requiredTierName}`
                : dashboard.cash + dashboard.bankCash < t.nextDevelopment.cost
                  ? `${t.nextDevelopment.name}: ${money.format(t.nextDevelopment.cost)}`
                  : dashboard.turns < t.nextDevelopment.turns
                    ? `${t.nextDevelopment.turns} turns and you have ${dashboard.turns}`
                    : `Work up to ${t.nextDevelopment.name} (${money.format(t.nextDevelopment.cost)})`}
            </Button>}
            {t.canClaim && <Button className="btn btn-primary btn-sm" blocked={firstReason(
              busy && BUSY,
              force(t.id) > board.maxGarrisonThugs && `A corner takes ${board.maxGarrisonThugs} thugs at most and you have set ${force(t.id)}.`,
            )} onClick={() => void run(() => api.claimTerritory(t.id, force(t.id), chosen(t.id)))}>Claim</Button>}
            {t.canRaid && <Button className="btn btn-primary btn-sm" blocked={firstReason(
              busy && BUSY,
              force(t.id) > board.maxRaidThugs && `You can send ${board.maxRaidThugs} thugs on a raid at most and you have set ${force(t.id)}.`,
            )} onClick={() => void run(() => api.raidTerritory(t.id, force(t.id), force(t.id)))}>Raid it</Button>}
          </div>
        </div>)}
      </div>
    </section>
  </div>
}

/**
 * The player-to-player board. It works because turns are scarcer than cash: somebody with turns and no
 * money makes weapons, somebody with money and no turns buys them rather than spending the turns.
 */
function TradingPanel(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const [board, setBoard] = useState<MarketBoard | null>(null)
  const [error, setError] = useState('')
  // Empty until the board says what it sells.
  //
  // This used to start at 'weapons', a key that stopped existing the day guns split into tiers - the
  // board serves pistols, shotguns, smgs and rifles now. Nothing complained: a select whose value
  // matches no option shows the first one, so the panel looked fine and was not. `good` was undefined,
  // the price never seeded off the reference, the button sat disabled saying "List for $0", and the
  // line telling you what the game pays simply did not render. The only way to use the panel was to
  // change the dropdown to something and back.
  //
  // Naming a key here at all was the mistake, so this names none and takes what the board offers.
  const [item, setItem] = useState('')
  const [qty, setQty] = useState(1)
  const [price, setPrice] = useState(0)
  const [buyQty, setBuyQty] = useState<Record<number, number>>({})

  const load = async () => {
    try { setBoard(await api.market()); setError('') }
    catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.weapons, dashboard.weed, dashboard.coke, dashboard.condoms, dashboard.beer])

  const run = async (fn: () => Promise<unknown>) => { await act(fn); await load() }
  const good = board?.goods.find(g => g.item === item)
  // Whatever the board leads with, preferring something the player actually holds - a panel that opens
  // on a good you have none of is a panel that opens disabled.
  useEffect(() => {
    if (!board || good) return
    const opening = board.goods.find(g => g.held > 0) ?? board.goods[0]
    if (opening) { setItem(opening.item); setPrice(opening.referencePrice) }
  }, [board, good])
  // Seeded from what the game itself pays, so the first listing is not a guess in the dark.
  useEffect(() => { if (good && price === 0) setPrice(good.referencePrice) }, [good?.item])

  if (!board) return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Player Market</h2><span>Loading</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
  </section>

  return <>
    <section className="card p-3 gcol-full" data-area="flea">
      <div className="panel-title">
        <h2>Player Market</h2>
        <span>{board.houseCutPercent}% to the house / {board.yourOpenListings} of {board.maxListingsPerPlayer} listings</span>
      </div>
      {error && <div className="alert alert-danger"><span>{error}</span></div>}
      {/*
        Two columns, because this card spans the page and nothing in it was using the width. Paragraphs
        are capped at 68ch for legibility and the form fields are fixed widths, so a full-width card
        held a 590px paragraph above a 600px row of controls with half the card empty to the right of
        both. Side by side they fill it, and the explanation sits next to the thing it explains.
      */}
      <div className="d-grid gtc-1 gtc-lg-2 gap-3 align-items-start">
        <div className="d-grid gap-2 align-content-start">
          <p className="mb-0">
            Sell to other players instead of the game. Stock leaves your storage the moment you list it
            and comes back if you pull the listing. What the game pays is shown for reference, not as a
            limit.
          </p>
          {good && <p className="text-body-tertiary small mb-0">
            The game pays {money.format(good.referencePrice)} for {good.label.toLowerCase()}.
            {good.bestPrice ? ` Cheapest on the board right now is ${money.format(good.bestPrice)}.` : ' Nothing listed yet.'}
            {' '}You hold {number.format(good.held)} with room for {number.format(good.room)} more.
          </p>}
        </div>
        <div className="control-row">
          {/* Grows, because "Moonshine (1,240 held)" does not fit the 132px a number field wants. */}
          <label className="field grow">Good<select className="form-select" value={item} onChange={e => { setItem(e.target.value); setPrice(board.goods.find(g => g.item === e.target.value)?.referencePrice ?? 0) }}>
            {board.goods.map(g => <option key={g.item} value={g.item}>{g.label} ({number.format(g.held)} held)</option>)}
          </select></label>
          <label className="field">Quantity<input className="form-control" type="number" min={1} max={good?.held ?? 1} value={qty} onChange={e => setQty(Number(e.target.value))} /></label>
          <label className="field">Price each<input className="form-control" type="number" min={1} value={price} onChange={e => setPrice(Number(e.target.value))} /></label>
          <Button
            className="btn btn-primary btn-sm"
            blocked={firstReason(
              busy && BUSY,
              !good && 'Pick something to sell first.',
              qty < 1 && 'List at least one.',
              !!good && qty > good.held && `You are listing ${number.format(qty)} and you hold ${number.format(good.held)}.`,
              price < 1 && 'Nobody buys anything for nothing. Put a price on it.',
            )}
            onClick={() => void run(() => api.listOnMarket(item, qty, price))}
          >
            List for {money.format(qty * price)}
          </Button>
        </div>
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>On the Board</h2><span>{board.listings.length} listings</span></div>
      {board.listings.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nothing for sale. Be the first.</p>}
      {board.listings.length > 0 && <div className="table-responsive mt-3"><table className="table table-sm table-hover align-middle game-table">
        <thead><tr><th>Good</th><th>Left</th><th>Each</th><th>vs game</th><th>Seller</th><th /></tr></thead>
        <tbody>
          {board.listings.map(l => <tr key={l.id} className={l.yours ? 'text-body-tertiary fst-italic' : ''}>
            <td>{l.itemLabel}</td>
            <td>{number.format(l.quantity)} of {number.format(l.originalQuantity)}</td>
            <td>{money.format(l.pricePerUnit)}</td>
            <td>{l.referencePrice > 0 ? `${Math.round((l.pricePerUnit / l.referencePrice - 1) * 100)}%` : '-'}</td>
            <td>{l.yours ? 'You' : l.sellerName}</td>
            <td className="d-flex gap-1">
              {l.yours
                ? <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => void run(() => api.cancelListing(l.id))}>Pull it</Button>
                : <>
                  <input className="form-control"
                    type="number"
                    min={1}
                    max={l.quantity}
                    value={buyQty[l.id] ?? l.quantity}
                    onChange={e => setBuyQty(v => ({ ...v, [l.id]: Number(e.target.value) }))}
                  />
                  <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => void run(() => api.buyOnMarket(l.id, buyQty[l.id] ?? l.quantity))}>Buy</Button>
                </>}
            </td>
          </tr>)}
        </tbody>
      </table></div>}
    </section>
  </>
}

/**
 * A tab that survives a reload.
 *
 * Ordinary useState with the address bar underneath it: the same pair back, so every call site keeps
 * reading like the useState it replaced. The page it belongs to is passed in rather than read back,
 * because a tab is only ever meaningful under one page - 'hideout' means nothing on the Account page,
 * and a tab restored under the wrong one would be a tab nobody could see to close.
 *
 * Anything the hash asks for that this page does not have falls back silently. The address bar is
 * typed into, shared, and left over from an older build, so it is a request rather than an
 * instruction, and a stale link should open the page rather than an error.
 */
function useRouteTab<T extends string>(page: AppPage, allowed: readonly T[], fallback: T): [T, (next: T) => void] {
  const [tab, setTab] = useState<T>(() => {
    const asked = routeTab(page) as T
    return allowed.includes(asked) ? asked : fallback
  })

  // Written from an effect rather than from the click, so that a tab arrived at any other way - the
  // Fix this button on the account warning, a page opened straight onto its default - is written down
  // too. A player who cannot see how the address bar got there can still reload onto it.
  useEffect(() => { writeRoute(page, tab) }, [page, tab])

  /*
    And read back, for the tab somebody else asked for.

    Arriving from another page needs none of this: the strip unmounts with the page it was on and the
    new one reads the address as it mounts. This is the other half - a link that names a tab on the page
    already open, where nothing remounts and the initial read has long since happened. Without it,
    "upgrade the storage room" on the Crew page would move nothing, because the destination is the tab
    next door.

    Guarded on the page and the list, so one strip cannot be moved by an address meant for another, and
    a tab this page has no branch for is ignored rather than opening a blank. The allowed list is a
    module constant at every call site, so it is deliberately not a dependency: adding it would rebuild
    the subscription on a value that never changes.
  */
  useEffect(() => onRouteChange((written, asked) => {
    if (written === page && (allowed as readonly string[]).includes(asked)) setTab(asked as T)
  }), [page])

  return [tab, setTab]
}

// Generic over the tab keys so the strip and the state that answers it cannot drift: a key listed
// here that the page has no branch for is now a compile error rather than a tab that opens nothing.
// NoInfer keeps the list from widening T back to string - the keys are decided by the state, and the
// strip only draws them.
function SectionTabs<T extends string>({ label, tabs, active, onActive }: {
  label: string
  tabs: { key: NoInfer<T>, label: string }[]
  active: T
  onActive: (key: T) => void
}) {
  return <nav className="nav nav-pills gap-2" aria-label={label}>
    {tabs.map(tab => <button
      className={`nav-link ${active === tab.key ? 'active' : ''}`}
      type="button"
      key={tab.key}
      aria-current={active === tab.key ? 'page' : undefined}
      onClick={() => onActive(tab.key)}
    >
      {tab.label}
    </button>)}
  </nav>
}

const MARKET_TABS = ['trade', 'flea', 'routes'] as const

/**
 * Buying and selling, in three places that are not the same place.
 *
 * Shop is the town's trader: one person, fixed prices, always open, and the standing you have with them.
 * Flea is everybody else, at whatever they feel like asking. Runs is sending crew to buy somewhere the
 * price is better. They were one page and read as one counter, which flattered none of them - the flea
 * market looked like more shelves, and it buried the trader under a page of listings.
 *
 * The hideout and the craft queue were here too and are on Crew now. Both cost money, which was the
 * whole of what they had in common with a shop. See CrewPage.
 */
function MarketPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('market', MARKET_TABS, 'trade')
  return <div className="d-grid gap-3">
    <SectionTabs
      label="Business sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'trade', label: 'Shop' },
        { key: 'flea', label: 'Flea' },
        { key: 'routes', label: 'Runs' },
      ]}
    />
    {tab === 'trade' && <MarketCorePage {...ctx} />}
    {tab === 'flea' && <FleaPage {...ctx} />}
    {tab === 'routes' && <MulePage {...ctx} />}
  </div>
}

/**
 * The flea market: what other players are selling, and putting your own stock up beside it.
 *
 * Its own tab because it is a different counter to the shop's. The Shop tab is one trader with fixed
 * prices who is always there; this is everybody else, at whatever they feel like asking, and whether
 * there is anything worth having on it depends entirely on who has been listing lately. Sat under the
 * shop it read as more of the shop, which is the one thing it is not - and it pushed the trader, the
 * standing and the wanted board down a page that was already the longest in the game.
 */
function FleaPage(ctx: PageContext) {
  return <div className="d-grid gtc-1 gtc-xl-split-92 gap-3 align-items-start">
    <TradingPanel {...ctx} />
  </div>
}

type StoreTopOffLine = {
  key: string
  name: string
  quantity: number
  cost: number
  storageLimited: boolean
}

function storeTopOffPlan(dashboard: Dashboard): { lines: StoreTopOffLine[], cost: number, needed: number, storageLimited: boolean, blocked?: string } {
  const items = new Map(dashboard.store.map(item => [item.key, item]))
  const lines: StoreTopOffLine[] = []
  let cash = dashboard.cash
  let needed = 0
  let storageLimited = false

  const add = (key: string, target: number, heldForNeed: number, storageRoom: number) => {
    const short = Math.max(0, Math.ceil(target) - heldForNeed)
    if (short <= 0) return
    needed += short

    const item = items.get(key)
    if (!item || item.locked) return

    const room = Math.max(0, storageRoom)
    if (room < short) storageLimited = true
    const available = typeof item.available === 'number' ? Math.max(0, item.available) : 10_000
    const affordable = item.price <= 0 ? 0 : Math.floor(cash / item.price)
    const quantity = Math.min(short, room, available, affordable, 10_000)
    if (quantity <= 0) return

    const cost = quantity * item.price
    cash -= cost
    lines.push({ key, name: item.name, quantity, cost, storageLimited: quantity < short && quantity === room })
  }

  add('condoms', dashboard.crewReport.condomsNeededPerHour * 24, dashboard.condoms, dashboard.hideout.maxCondoms - dashboard.condoms)
  add('beer', dashboard.crewReport.beerNeededPerHour * 24, dashboard.beer + dashboard.moonshine, dashboard.hideout.maxBeer - dashboard.beer)

  const cost = lines.reduce((sum, line) => sum + line.cost, 0)
  const blocked = lines.length > 0
    ? undefined
    : needed <= 0
      ? 'You already have enough condoms and beer for the next 24 hours.'
      : dashboard.cash <= 0
        ? 'You need cash on hand to top off at the counter.'
        : storageLimited
          ? 'Your storage room is full before it can hold 24 hours of upkeep.'
          : 'The counter cannot fill any more of your current needs.'

  return { lines, cost, needed, storageLimited, blocked }
}

async function buyStoreTopOff(lines: StoreTopOffLine[]): Promise<ActionResult> {
  const results: ActionResult[] = []
  for (const line of lines)
    results.push(await api.buyStoreItem(line.key, line.quantity))

  const bought = lines.map(line => `${number.format(line.quantity)} ${line.name.toLowerCase()}`).join(', ')
  const cost = results.reduce((sum, result) => sum + numberFromBreakdown(result, 'total'), 0)
    || lines.reduce((sum, line) => sum + line.cost, 0)
  const rep = results.reduce((sum, result) => sum + numberFromBreakdown(result, 'repEarned'), 0)
  const capped = lines.some(line => line.storageLimited)
  return {
    summary: `Topped off ${bought} for ${money.format(cost)}.${capped ? ' Storage capped the rest.' : ''}${rep > 0 ? ` Counter rep rose by ${number.format(rep)}.` : ''}`,
    turnsRemaining: results.at(-1)?.turnsRemaining ?? 0,
  }
}

function numberFromBreakdown(result: ActionResult, key: string): number {
  const value = result.breakdown?.[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

function MarketCorePage(ctx: PageContext) {
  const { dashboard, busy, bankAmount, storeQty, setBankAmount, setStoreQty, act } = ctx
  const topOff = storeTopOffPlan(dashboard)
  return <div className="d-grid gtc-1 gtc-xl-split-92 gap-3 align-items-start">
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Inventory</h2><span>{dashboard.city} prices, travel on Overview</span></div>
      <div className="tnum d-grid gtc-1 gtc-sm-2 gtc-md-5 gap-2 mt-3">
        <InventoryCard name="Condoms" count={dashboard.condoms} note="Hoe upkeep" />
        <InventoryCard name="Beer" count={dashboard.beer} note="Thug upkeep" />
        {/* One card a gun. A single "weapons" number would hide the only thing that matters about
            them, which is what a crew carrying them is worth in a fight. */}
        {dashboard.weaponRack.map(tier => <InventoryCard
          key={tier.key}
          name={tier.label}
          count={tier.held}
          note={tier.firepower <= 1 ? "Covers a thug" : `${tier.firepower}x a pistol`}
        />)}
        <InventoryCard name="Medicine" count={dashboard.medicine} note="Treats an infestation" />
        <InventoryCard name="Poison" count={dashboard.poison} note="Throws one" />
        <InventoryCard name="Rides" count={dashboard.rides} note={`${dashboard.rides}/${dashboard.hideout.maxRides} garage`} />
        <InventoryCard name="Weed" count={dashboard.weed} note={`${money.format(dashboard.weedSellPrice)} ${dashboard.currentMarket.weed.toLowerCase()}`} />
        {/* Quotes what this pile actually fetches, not the list price, since cut coke is not coke. */}
        <InventoryCard
          name="Coke"
          count={dashboard.coke}
          note={dashboard.cokePurityPercent >= 100
            ? `${money.format(dashboard.cokeSellPrice)} ${dashboard.currentMarket.coke.toLowerCase()}`
            : `${money.format(dashboard.cokeSellPriceAtPurity)} at ${dashboard.cokePurityPercent}% pure`}
        />
      </div>
    </section>

    <TraderBoardPanel dashboard={dashboard} busy={busy} act={act} />
    <StoreStandingPanel dashboard={dashboard} busy={busy} act={act} />

    <section className="card p-3 gcol-full" data-area="store">
      {/* The counter is somebody's, and says so. "Street Store" was a sign on a building nobody
          worked in - which is a strange thing for the one room every player opens every day. */}
      <div className="panel-title">
        <h2>{dashboard.storeRep.trader.name}'s Counter</h2>
        <div className="d-flex flex-wrap align-items-center justify-content-end gap-2">
          <span className="text-body-secondary small">Cash on hand only{dashboard.storeRep.discountPercent > 0 ? `, ${dashboard.storeRep.discountPercent}% off as ${dashboard.storeRep.levelName}` : ''}</span>
          <Button
            className="btn btn-secondary btn-sm"
            blocked={firstReason(busy && BUSY, topOff.blocked)}
            onClick={() => void act(() => buyStoreTopOff(topOff.lines))}
          >Top off 24h{topOff.cost > 0 ? ` ${money.format(topOff.cost)}` : ''}</Button>
        </div>
      </div>
      <div className="d-grid gtc-1 gtc-xl-3 gap-2 mt-3">
        {dashboard.store.map(item => {
          const qty = storeQty[item.key] ?? 1
          /*
            A locked row is shown rather than hidden. What the shop sells and what you can be sold are
            two different questions, and a rack that quietly grows extra rows as you climb never tells
            anybody there was a ladder at all - which is the whole thing worth knowing about it.
          */
          return <div className={`store-row tnum d-grid gtc-1 gap-3 align-content-between border rounded p-3 ${item.locked ? 'bg-body-secondary opacity-75' : 'bg-body-tertiary'}`} key={item.key}>
            <div className="min-w-0 d-grid align-content-start gap-2">
              <div className="d-flex flex-wrap align-items-center gap-2">
                <strong className="text-body fs-5">{item.name}</strong>
                <span className="eyebrow border rounded-pill text-info-emphasis px-2 py-1">{item.category}</span>
                {/* What is left, where it is worth knowing. A thin shelf is a thing to act on and a
                    full one is noise, so only the last of a line says so. */}
                {typeof item.available === 'number' && item.available > 0 && item.available <= 12 &&
                  <span className="eyebrow border rounded-pill text-warning-emphasis border-warning px-2 py-1">
                    {number.format(item.available)} left
                  </span>}
                {item.minRepLevel > 1 && <span className={`eyebrow border rounded-pill px-2 py-1 ${item.locked ? 'text-warning-emphasis border-warning' : 'text-success-emphasis border-success'}`}>
                  {item.minRepLevelName}
                </span>}
              </div>
              <p className="m-0 text-body-secondary">{item.description}</p>
            </div>
            <div className="d-grid gtc-2 gap-2 align-items-end border rounded bg-body-tertiary p-2">
              <div className="d-grid gap-1">
                <span className="eyebrow">Unit</span>
                <strong className="text-primary fs-6">{money.format(item.price)}</strong>
                {/* The sticker, struck through, only where standing has actually moved it. */}
                {item.listPrice > item.price && <s className="text-body-tertiary small">{money.format(item.listPrice)}</s>}
              </div>
              {/* Capped at what is actually on the counter, so the number in the box is always a
                  number the shop can fill. The refusal behind it is real either way. */}
              <label className="field small">Qty<input className="form-control" aria-label={`${item.name} quantity`} type="number" min={1} max={item.available ?? 10000} value={qty} disabled={item.locked} onChange={e => setStoreQty(v => ({ ...v, [item.key]: Number(e.target.value) }))} /></label>
              <div className="d-grid gap-1">
                <span className="eyebrow">Total</span>
                <strong className="text-primary fs-6">{money.format(qty * item.price)}</strong>
              </div>
              <Button
                className="btn btn-primary btn-sm w-100"
                blocked={firstReason(
                  busy && BUSY,
                  // Three different reasons a row can be shut and they are not interchangeable: a rung
                  // you have not reached, a line this trader has never carried, and a line they are out
                  // of. Only the first is something to go and earn, which is why the trader's own
                  // sentence is used rather than one written here.
                  item.locked && (item.lockedReason ?? `The shop is not selling you ${item.name.toLowerCase()} yet.`),
                  qty < 1 && 'Buy at least one.',
                  dashboard.cash < qty * item.price && `That comes to ${money.format(qty * item.price)} and you are carrying ${money.format(dashboard.cash)}.`,
                )}
                onClick={() => void act(() => api.buyStoreItem(item.key, qty))}
              >{item.locked ? 'Locked' : 'Buy'}</Button>
              {/* Rides are the only store item with a resale price, so the sell button only exists here. */}
              {item.key === 'rides' && <Button
                className="btn btn-secondary btn-sm w-100"
                blocked={firstReason(
                  busy && BUSY,
                  qty < 1 && 'Sell at least one.',
                  dashboard.rides < qty && `You are selling ${number.format(qty)} and you have ${number.format(dashboard.rides)}.`,
                )}
                onClick={() => void act(() => api.sellStoreItem(item.key, qty))}
              >Sell</Button>}
            </div>
          </div>
        })}
      </div>
    </section>

    <BankPanel dashboard={dashboard} busy={busy} bankAmount={bankAmount} setBankAmount={setBankAmount} act={act} className="market-bank" wide />
  </div>
}

/**
 * Standing at the counter: where you are, what it is worth, and what money buys of it.
 *
 * The panel leads with the rung you are on rather than the points behind it, because the points are
 * only ever a means: nobody is saving up 15,000 rep, they are saving up for rifles.
 *
 * It shows the rung underfoot and the next one, and no further. Laying the whole ladder out was the
 * first thing tried and it read as a spoiler - five cards naming every gun and every price in the game
 * to somebody who has not bought a shotgun yet, which is a strange way to open a shop. What a player
 * needs here is what they are and what is next; a locked row on the shelf above still names the rung
 * that opens it, so nothing anybody is actually reaching for has gone quiet.
 *
 * Investments sit under it rather than in the goods grid above. They are the one thing at this counter
 * that hands over nothing at all, and a row that takes $250,000 and gives back no object belongs
 * nowhere near the row that sells beer.
 */
/**
 * The dealer's board: the three jobs you are being told about, and what asking for others costs.
 *
 * One panel where there were two. The trader's own wanted board and the town's contracts were separate
 * lists with separate headings, separate paragraphs and separate refill clocks, stacked one above the
 * other inside the same card and both headed with the same person's name - up to six open jobs at once,
 * asking a player to learn a distinction the fiction never made. Every job comes through the dealer;
 * some are theirs, some are somebody else's. The row says which.
 *
 * The book behind it is deep - sixteen to eighteen going in a town - and what you get is a hand of
 * three out of it, kept, so a job is still there when you come back with what you went away to make.
 * The first slot is always the dealer's own and the second always a buyer's, so an evening whose whole
 * question is what to do with a workshop never opens on a board with nothing to say to it.
 *
 * Loaded on its own rather than off the dashboard, because reading the board is what tops the book up:
 * hanging it off the dashboard would post jobs in every town in the game for a player who never goes
 * near a shop.
 */
function TraderBoardPanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const [board, setBoard] = useState<TraderJobBoard | null>(null)
  // Which slots are ticked for the next ask. Held here rather than derived, because choosing is the
  // whole interaction: the cost is per slot, so one, two and three are three different decisions.
  const [asking, setAsking] = useState<number[]>([])

  const load = async () => {
    try { setBoard(await api.jobs()) }
    catch { setBoard(null) }
  }
  // Re-read on the things that change what a row can do: the town you are in, and the stock you hold.
  useEffect(() => { void load() }, [
    dashboard.city, dashboard.weapons, dashboard.weed, dashboard.coke,
    dashboard.moonshine, dashboard.cut, dashboard.medicine, dashboard.poison,
  ])

  const trader = board?.trader ?? dashboard.storeRep.trader
  const fill = async (id: number) => {
    await act(() => api.fillJob(id))
    await load()
  }
  const reroll = async () => {
    if (asking.length === 0) return
    await act(() => api.rerollJobs(asking))
    setAsking([])
    await load()
  }

  const jobs = board?.jobs ?? []
  const rr = board?.reroll
  // What the ticked slots actually come to. The steps escalate, so this is the sum of the next N rather
  // than N times the next one - and quoting the wrong number here would be quoting a price nobody pays.
  const cost = (() => {
    if (!rr || asking.length === 0) return { cash: 0, rep: 0 }
    if (asking.length >= jobs.length) return { cash: rr.allCash, rep: rr.allRep }
    if (asking.length === 1) return { cash: rr.nextCash, rep: rr.nextRep }
    // Two of three: the page cannot see the middle step, so it says the part it knows and lets the
    // server be the authority. Understating would be worse than saying "from".
    return { cash: rr.nextCash, rep: rr.nextRep, from: true as const }
  })()
  // A job with goods already in it is pinned: the stock is gone and the premium is not paid until the
  // last unit, so swapping it out would take an unfinished job off somebody who has paid into it.
  const pinned = jobs.filter(job => job.yours).map(job => job.slot)
  const overRep = rr ? cost.rep > rr.spendableRep : false
  const short = dashboard.cash < cost.cash

  return <section className="card p-3 gcol-full" data-area="jobs">
    <div className="panel-title">
      <h2>{trader.name}</h2>
      <span>{trader.pitch}</span>
    </div>
    {/* Their line, then the line meant for you. The second one changes when your standing does, which
        is the cheapest way there is to tell somebody a rung moved. */}
    <p className="m-0 mt-2 text-body-secondary fst-italic">&ldquo;{trader.patter}&rdquo;</p>
    <p className="m-0 mt-2 text-body">{dashboard.storeRep.trader.greeting}</p>

    <p className="m-0 mt-3 text-body-secondary">
      Three jobs at a time, out of the {board ? number.format(board.openInTown) : 'many'} going in
      {' '}{board?.city ?? dashboard.city}. Every one of them is theirs - a gap on their own shelf, a
      favour for somebody, a promise they came up short on, or another town's counter they said they
      would cover. They all pay over the going rate, and the premium lands whole when the last of it
      goes in, so stopping half way costs you nothing but the finish.
    </p>

    {jobs.length === 0 && board !== null && <p className="m-0 mt-3 text-body-tertiary">
      Nothing going in {board.city} right now. The town posts more every few minutes.
    </p>}

    <div className="d-grid gap-2 mt-3">
      {jobs.map(job => {
        const hours = Math.floor(job.minutesRemaining / 60)
        const left = hours >= 1 ? `${hours}h left` : `${job.minutesRemaining}m left`
        const started = job.delivered > 0
        const finishes = job.canDeliverNow >= job.remaining && job.canDeliverNow > 0
        const ticked = asking.includes(job.slot)
        return <div className={`room-row ${job.blockedReason ? '' : 'border-start-thick border-start-success'}`} key={job.id}>
          <div className="room-copy">
            <strong>
              {number.format(job.quantity)} {job.goodLabel.toLowerCase()}
              {/* Why, rather than who. Every job is the dealer's; what differs is what they want it
                  for, and that is the sentence that was missing when half the rows were headed with
                  the name of a place the player had never dealt with. */}
              <span className="badge text-bg-secondary">{job.reason}</span>
              <span className="badge text-bg-secondary">+{number.format(job.rep)} rep</span>
              {started && <span className="badge text-bg-warning">Started</span>}
            </strong>
            <span>
              {money.format(job.pricePerUnit)} each, against {money.format(job.referencePricePerUnit)}
              {job.kind === 'Supply' ? ' on their shelf.' : ' over the counter.'}
              {job.minimumPurityPercent ? ` At least ${job.minimumPurityPercent}% pure.` : ''}
              {/* The bench is half the point of the board, so a row says outright whether yours can
                  make this - an order for SMGs is a different proposition at workshop 4 than at none. */}
              {job.workshopLevelNeeded
                ? ` Your workshop cannot make these yet - that needs level ${job.workshopLevelNeeded}.`
                : job.canForge ? ' Your workshop makes these.' : ''}
            </span>
            <small>
              {started
                ? `${number.format(job.delivered)} in, ${number.format(job.remaining)} to go - ${money.format(job.completionBonus)} and ${number.format(job.rep)} rep land when it is finished`
                : `${money.format(job.payout)} the lot, ${money.format(job.completionBonus)} more than selling it flat`}
              {' - '}{left}
            </small>
            {started && <div
              className="progress contract-progress mt-1"
              role="progressbar"
              aria-label="Job filled"
              aria-valuenow={Math.round((job.delivered / job.quantity) * 100)}
              aria-valuemin={0}
              aria-valuemax={100}
            >
              <div className="progress-bar" style={{ width: `${Math.round((job.delivered / job.quantity) * 100)}%` }} />
            </div>}
          </div>
          <em>{number.format(job.held)} held</em>
          <div className="d-grid gap-1">
            <Button
              className="btn btn-primary btn-sm"
              blocked={firstReason(
                busy && BUSY,
                job.blockedReason,
                job.canDeliverNow <= 0 && `You are holding ${number.format(job.held)} ${job.goodLabel.toLowerCase()} and the job wants ${number.format(job.remaining)}.`,
              )}
              onClick={() => void fill(job.id)}
            >
              {job.canDeliverNow <= 0 ? 'Nothing to hand over'
                : finishes ? 'Finish it' : `Run ${number.format(job.canDeliverNow)}`}
            </Button>
            {/* The tick that decides what the ask below covers. Absent on a job with goods in it,
                because that one cannot be swapped at any price and a disabled box invites the question. */}
            {!job.yours && <label className="eyebrow d-flex align-items-center gap-1 justify-self-end">
              <input
                className="form-check-input m-0"
                type="checkbox"
                checked={ticked}
                disabled={busy}
                onChange={() => setAsking(slots => ticked ? slots.filter(x => x !== job.slot) : [...slots, job.slot])}
              />
              Ask again
            </label>}
          </div>
        </div>
      })}
    </div>

    {rr && jobs.length > 0 && <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 border-top mt-3 pt-3">
      <div className="d-grid gap-1">
        <strong className="text-body">Ask what else is going</strong>
        <span className="text-body-secondary small">
          {rr.usedThisCycle === 0
            ? 'The first one is on them.'
            : rr.freeAgainAtUtc
              ? `${number.format(rr.usedThisCycle)} asked this cycle. The free one is back in ${timeUntil(rr.freeAgainAtUtc)}.`
              : 'The first one is on them.'}
          {pinned.length > 0 && ' A job you have goods in stays where it is.'}
        </span>
      </div>
      <div className="d-flex flex-wrap align-items-center gap-2">
        <span className="tnum text-body-secondary small">
          {asking.length === 0
            ? `Next one costs ${cost.cash > 0 || rr.nextRep > 0 ? `${money.format(rr.nextCash)} and ${number.format(rr.nextRep)} rep` : 'nothing'}`
            : `${'from' in cost ? 'From ' : ''}${money.format(cost.cash)} and ${number.format(cost.rep)} rep`}
        </span>
        <Button
          className="btn btn-secondary btn-sm"
          type="button"
          blocked={firstReason(
            busy && BUSY,
            asking.length === 0 && 'Tick the jobs you want swapped out first.',
            short && `Asking again costs ${money.format(cost.cash)} and you are carrying ${money.format(dashboard.cash)}.`,
            overRep && rr ? `Asking again costs ${number.format(cost.rep)} rep and you only have ${number.format(rr.spendableRep)} to spend without dropping a rung.` : false,
          )}
          onClick={() => void reroll()}
        >
          {overRep ? 'That would cost you the rung'
            : short ? 'Not enough cash'
            : asking.length === 0 ? 'Pick one'
            : `Ask about ${asking.length === 1 ? 'it' : number.format(asking.length)}`}
        </Button>
      </div>
    </div>}
  </section>
}

function StoreStandingPanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const rep = dashboard.storeRep
  const waiting = rep.investmentReadySeconds > 0 && rep.investmentReadyAtUtc
  return <section className="card p-3 gcol-full" data-area="standing">
    <div className="panel-title">
      <h2>Standing with {rep.trader.name}</h2>
      <span>{number.format(rep.rep)} rep, {money.format(rep.dollarsPerRep)} of trade a point</span>
    </div>

    <div className="tnum d-grid gap-2 mt-3">
      <div className="d-flex flex-wrap justify-content-between align-items-baseline gap-2">
        <strong className="text-body fs-5">{rep.levelName}</strong>
        <span className="text-body-secondary small">
          {rep.nextLevelName
            ? `${number.format(rep.repToNextLevel)} rep to ${rep.nextLevelName}`
            : 'Top of the ladder. Nothing here is closed to you.'}
        </span>
      </div>
      <div className="progress" role="progressbar" aria-label="Store standing" aria-valuenow={rep.progressPercent} aria-valuemin={0} aria-valuemax={100}>
        <div className="progress-bar bg-primary" style={{ width: `${Math.max(2, rep.progressPercent)}%` }} />
      </div>
      <p className="m-0 text-body-secondary small">
        Every dollar over the counter counts, so the beer and condoms you already buy are building this.
        {rep.discountPercent > 0 && ` Standing here takes ${rep.discountPercent}% off every price in the shop.`}
      </p>
    </div>

    <div className="panel-title mt-3">
      <h3 className="fs-6 m-0">Investment</h3>
      <span>{waiting ? `The counter takes another in ${timeUntil(rep.investmentReadyAtUtc!)}` : 'The counter will take one now'}</span>
    </div>
    <div className="tnum d-grid gtc-1 gtc-xl-3 gap-2 mt-2">
      {rep.investments.map(investment => {
        const short = dashboard.cash < investment.cost
        return <div className={`d-grid gap-2 align-content-between border rounded p-3 ${investment.locked ? 'bg-body-secondary opacity-75' : 'bg-body-tertiary'}`} key={investment.key}>
          <div className="d-grid gap-1">
            <div className="d-flex flex-wrap justify-content-between align-items-baseline gap-2">
              <strong className="text-body">{investment.name}</strong>
              <span className="eyebrow text-primary">+{number.format(investment.rep)} rep</span>
            </div>
            <p className="m-0 text-body-secondary small">{investment.description}</p>
            <span className="eyebrow">{money.format(investment.cost)} / shuts the counter {investment.cooldownHours}h</span>
          </div>
          <Button
            className="btn btn-primary btn-sm w-100"
            blocked={firstReason(
              busy && BUSY,
              investment.locked && (investment.lockedReason ?? `The counter takes this from ${investment.minLevelName} and up.`),
              short && `That costs ${money.format(investment.cost)} and you are carrying ${money.format(dashboard.cash)}.`,
              !!waiting && `The counter took one already. It will take another in ${timeUntil(rep.investmentReadyAtUtc!)}.`,
            )}
            onClick={() => void act(() => api.investInStore(investment.key))}
          >
            {investment.locked ? investment.minLevelName : waiting ? timeUntil(rep.investmentReadyAtUtc!) : short ? 'Short' : `Pay ${money.format(investment.cost)}`}
          </Button>
        </div>
      })}
    </div>
  </section>
}

function ProductionPage(ctx: PageContext) {
  return <div className="d-grid gap-3">
    <WorkshopCraftPanel
      dashboard={ctx.dashboard}
      busy={ctx.busy}
      act={ctx.act}
      sellQty={ctx.sellQty}
      setSellQty={ctx.setSellQty}
    />
  </div>
}

function TravelPanel({ markets, turns, travel, busy, act }: { markets: CityMarket[], turns: number, travel: TravelStatus, busy: boolean, act: PageContext['act'] }) {
  const here = markets.find(x => x.current)
  // The city you stand in leads the list: every other row's price is read as a move against it, so
  // the baseline has to sit where the eye starts rather than wherever the alphabet dropped it.
  const ordered = here ? [here, ...markets.filter(x => !x.current)] : markets
  return <section className="card p-3 travel-panel">
    {/* The stake is on the title line because it is the number that decides whether to bank first. */}
    <div className="panel-title"><h2>Travel</h2><span>{turns.toLocaleString()} turns, carrying {money.format(travel.carriedValue)}</span></div>
    {travel.carriedValue > 0 && <p className="travel-note text-body-tertiary small">A stop takes {travel.seizureMinPercent}–{travel.seizureMaxPercent}% of what you carry.</p>}
    <div className="tnum d-grid gap-1">
      {/* Column headings, so the prices are not made to label themselves in every row. */}
      <div className="city-market head">
        <span className="eyebrow">City</span><span className="eyebrow">Weed</span><span className="eyebrow">Coke</span><span className="eyebrow">Trip</span>
      </div>
      {ordered.map(city => {
        const shortfall = city.travelTurns - turns
        return <div className={`city-market border rounded px-3 py-2 ${city.current ? 'current border-primary' : 'bg-body-tertiary'}`} key={city.city}>
          <div className="min-w-0 d-grid gap-1">
            <strong className="text-body">{city.city}</strong>
            <span className={`eyebrow ${city.current ? 'text-primary' : ''}`}>{city.current ? 'Current city' : riskLine(city, travel)}</span>
          </div>
          <CityPrice price={city.weedSellPrice} base={here?.weedSellPrice} showDelta={!city.current} />
          <CityPrice price={city.cokeSellPrice} base={here?.cokeSellPrice} showDelta={!city.current} />
          <div className="city-market-go d-grid justify-items-end gap-1">
            {city.current
              ? <span className="eyebrow text-primary">You are here</span>
              : <>
                <Button
                  className="btn btn-secondary btn-sm"
                  blocked={firstReason(
                    busy && BUSY,
                    travel.blockedReason,
                    shortfall > 0 && `${city.city} is ${city.travelTurns} turns away and you have ${turns}.`,
                  )}
                  onClick={() => void act(() => api.travel(city.city))}
                >
                  Travel
                </Button>
                <small className={`small text-end ${shortfall > 0 ? 'text-danger' : 'text-body-tertiary'}`}>
                  {shortfall > 0 ? `need ${shortfall} more` : `${city.travelTurns} turns`}
                </small>
              </>}
          </div>
        </div>
      })}
    </div>
  </section>
}

/// Chance and severity on one line. A break-even share only reads as a number while it sits inside
/// the seizure range; outside it the honest answer is a phrase, because "break-even 8%" on a run
/// where every possible stop already costs more than staying tells the player nothing they can use.
function riskLine(city: CityMarket, travel: TravelStatus) {
  const bust = `${city.bustChancePercent}% bust`
  const breakEven = city.breakEvenSeizurePercent
  if (breakEven === null) return bust
  if (breakEven <= 0) return `${bust}, pays less here`
  if (breakEven <= travel.seizureMinPercent) return `${bust}, any stop costs the trip`
  if (breakEven >= travel.seizureMaxPercent) return `${bust}, no stop can cost the trip`
  return `${bust}, break-even ${breakEven}%`
}

/// The price leads and the change against your own city sits under it: the number a player acts on
/// is the difference, and making them hold your city's price in their head to find it is the work
/// the panel should be doing.
function CityPrice({ price, base, showDelta }: { price: number, base: number | undefined, showDelta: boolean }) {
  const delta = base === undefined ? 0 : price - base
  return <div className="d-grid justify-items-end text-end">
    <b className="text-body fs-6 fw-bold lh-1">{money.format(price)}</b>
    {showDelta && base !== undefined && (delta === 0
      ? <small className="text-body-tertiary small">same</small>
      : <small className={`small ${delta > 0 ? 'text-success' : 'text-danger'}`}>{delta > 0 ? '+' : '−'}{money.format(Math.abs(delta))}</small>)}
  </div>
}

const COMBAT_TABS = ['targets', 'ground', 'missions'] as const

function CombatPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('recon', COMBAT_TABS, 'targets')
  return <div className="d-grid gap-3">
    <SectionTabs
      label="Raids and map sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'targets', label: 'Raids' },
        { key: 'ground', label: 'Map' },
        { key: 'missions', label: 'Missions' },
      ]}
    />
    {tab === 'targets' && <ReconPage {...ctx} />}
    {tab === 'ground' && <TerritoryPage {...ctx} />}
    {tab === 'missions' && <CombatActivityPage {...ctx} />}
  </div>
}

function ReconPage(ctx: PageContext) {
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    <TitleBoardPanel currentPlayerId={ctx.dashboard.playerId} />
    <TargetReconPanel
      targets={ctx.targets}
      selectedTarget={ctx.selectedTarget}
      query={ctx.targetQuery}
      busy={ctx.busy}
      currentPlayerId={ctx.dashboard.playerId}
      combatMissions={ctx.combatMissions}
      dashboard={ctx.dashboard}
      attackCrew={ctx.attackCrew}
      setAttackCrew={ctx.setAttackCrew}
      commanderId={ctx.commanderId}
      setCommanderId={ctx.setCommanderId}
      attackMethod={ctx.attackMethod}
      setAttackMethod={ctx.setAttackMethod}
      poachCoke={ctx.poachCoke}
      setPoachCoke={ctx.setPoachCoke}
      borrowedThugs={ctx.borrowedThugs}
      setBorrowedThugs={ctx.setBorrowedThugs}
      onQuery={ctx.setTargetQuery}
      onSearch={ctx.searchTargets}
      onInspect={ctx.inspectTarget}
      onAttack={ctx.attackTarget}
    />
  </div>
}

function CombatActivityPage(ctx: PageContext) {
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start">
    <CombatMissionsPanel ctx={ctx} />
    <CombatHistoryPanel entries={ctx.combatLogs} currentPlayerId={ctx.dashboard.playerId} />
    {/* Fifty rows of ladder, last on the page with nothing beside it. */}
    <section className="card p-3 gcol-full">
      <StandingsPanel dashboard={ctx.dashboard} leaders={ctx.leaders} cityLeaders={ctx.cityLeaders} limit={50} />
    </section>
  </div>
}

function CombatMissionsPanel({ ctx }: { ctx: PageContext }) {
  const active = ctx.combatMissions.filter(mission => mission.status !== 'Complete')
  const completed = ctx.combatMissions.filter(mission => mission.status === 'Complete').slice(0, 8)
  const crew = ctx.dashboard.combatCrew
  return <>
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Active Missions</h2><span>{active.length} active</span></div>
      <div className="tnum d-grid gtc-1 gtc-md-4 gap-2 mb-3">
        <AdminMetric label="Available pimps" value={number.format(crew.availablePimps)} />
        <AdminMetric label="Available thugs" value={number.format(crew.availableThugs)} />
        <AdminMetric label="Available weapons" value={number.format(crew.availableWeapons)} />
        <AdminMetric label="Active missions" value={`${crew.activeAttackMissions}/${crew.maxActiveAttackMissions}`} />
      </div>
      <div className="d-grid gap-2">
        {active.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No active missions.</p>}
        {active.map(mission => <MissionCard mission={mission} currentPlayerId={ctx.dashboard.playerId} busy={ctx.busy} onCancel={ctx.cancelMission} key={mission.id} />)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Recent Results</h2><span>Completed</span></div>
      <div className="d-grid gap-2">
        {completed.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No completed missions yet.</p>}
        {completed.map(mission => <MissionCard mission={mission} currentPlayerId={ctx.dashboard.playerId} compact key={mission.id} />)}
      </div>
    </section>
  </>
}

function MissionCard({ mission, currentPlayerId, compact = false, busy = false, onCancel }: { mission: CombatMission, currentPlayerId: string, compact?: boolean, busy?: boolean, onCancel?: (missionId: number) => void }) {
  // Finished missions fold away: their round-by-round log is history, not something to scroll past.
  const [expanded, setExpanded] = useState(false)
  const showEvents = !compact || expanded
  const attacking = mission.attackerId === currentPlayerId
  const nextAt = nextMissionTime(mission)
  const commander = mission.commanderName ?? 'A pimp'
  const title = attacking ? `${commander} -> ${mission.defenderName}` : `${mission.attackerName} attacking you`
  const canCancel = attacking && !compact && mission.canCancel && onCancel
  return <div className={`d-grid gap-2 border rounded-2 p-3 ${mission.status === 'Fighting' ? 'border-warning' : mission.status === 'Returning' ? 'border-success' : ''}`}>
    <div className="d-flex flex-column flex-sm-row justify-content-between align-items-start align-items-sm-baseline gap-1 gap-sm-3">
      <div className="min-w-0 d-grid gap-1">
        <strong className="text-body text-truncate">{title}</strong>
        <span className="eyebrow">{mission.status} / {mission.outcome}</span>
      </div>
      {compact
        ? <button
            className="btn btn-secondary btn-sm flex-shrink-0 rounded-pill px-3 py-1 small fw-bold"
            type="button"
            aria-expanded={expanded}
            onClick={() => setExpanded(value => !value)}
          >
            {expanded ? 'Hide log' : `${mission.events.length} update${mission.events.length === 1 ? '' : 's'}`}
          </button>
        : <b className="text-primary text-nowrap">{mission.status === 'Complete' ? 'Done' : timeUntil(nextAt)}</b>}
    </div>
    {!compact && <div className="tnum d-grid gtc-1 gtc-md-4 gap-2">
      <AdminMetric label="Commander" value={mission.commanderBonusPercent > 0 ? `${commander} +${mission.commanderBonusPercent}%` : commander} />
      <AdminMetric label="Remaining" value={`${mission.remainingAttackers} T / ${mission.remainingWeapons} W`} />
      <AdminMetric label="Round" value={`${mission.currentRound}/${mission.maxRounds}`} />
      <AdminMetric label="Morale" value={`${mission.attackerMorale.toFixed(0)} / ${mission.defenderMorale.toFixed(0)}`} />
      {mission.lootMultiplierPercent < 100 && <AdminMetric label="Haul" value={`${mission.lootMultiplierPercent}% (repeat target)`} />}
    </div>}
    <p className="m-0 text-body-secondary">{mission.summary}</p>
    {canCancel && <div className="d-flex flex-column flex-sm-row justify-content-between align-items-start align-items-sm-center gap-2 border border-primary rounded bg-body-tertiary p-2">
      <span className="text-primary small">Call the crew back now for {money.format(mission.cancelCashCost)} cash on hand.</span>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => {
          if (window.confirm(`Cancel this attack for ${money.format(mission.cancelCashCost)}?`))
            onCancel(mission.id)
        }}
      >Cancel Mission</Button>
    </div>}
    {showEvents && <div className="d-grid border-top pt-2">
      {mission.events.length === 0 && <small className="text-body-tertiary small">No updates yet.</small>}
      {mission.events.map(event => <div className="mission-event d-grid gap-1 column-gap-2 py-2" key={event.id}>
        <strong className="text-primary small">{event.kind}{event.round > 0 ? ` ${event.round}` : ''}</strong>
        <span className="text-body-tertiary small text-end">{new Date(event.createdAtUtc).toLocaleTimeString()}</span>
        <p className="gcol-full m-0">{event.summary}</p>
      </div>)}
    </div>}
  </div>
}

const ADMIN_TABS = ['overview', 'players', 'keys', 'ai', 'config', 'titles', 'updates', 'liveops', 'audit'] as const
type AdminTab = typeof ADMIN_TABS[number]

const ADMIN_TAB_META: Record<AdminTab, { label: string, kicker: string }> = {
  overview: { label: 'Overview', kicker: 'Totals and distribution' },
  players: { label: 'Players', kicker: 'Search and enforcement' },
  keys: { label: 'Keys', kicker: 'Mint and revoke' },
  ai: { label: 'AI Rivals', kicker: 'Seed, run, automate' },
  config: { label: 'Tuning', kicker: 'Runtime values' },
  titles: { label: 'Titles', kicker: 'Create earned names' },
  updates: { label: 'Updates', kicker: 'Patch notes and events' },
  liveops: { label: 'Live Ops', kicker: 'Maintenance and banners' },
  audit: { label: 'Audit', kicker: 'Who changed what' }
}

/**
 * One tab at a time rather than six stacked panels. The Admin Control Center used to sit at the bottom
 * holding whatever had no other home: headline totals, a read-only economy dump, and the AI controls.
 * Those are three different jobs, so they now live with the things they belong to.
 */
function AdminPage(ctx: PageContext & { overview: AdminOverview }) {
  const [tab, setTab] = useRouteTab('admin', ADMIN_TABS, 'overview')
  /*
    One column, said once.

    This read `gtc-1 gtc-md-2 ... gtc-md-1` - somebody wanting a single column and appending gtc-md-1
    to force it. Utilities are generated from a map in value order, so .gtc-md-2 is written to the
    stylesheet after .gtc-md-1; both carry !important and the same specificity, so the later one wins
    whatever order the class attribute lists them in. The override never did anything, on any of the
    five elements that had it.

    Here it showed: the tab strip and every panel under it sat in the first of two columns, 619px of a
    1278px page, with the second column empty and the six tabs folded into three columns of two rows
    with their descriptions wrapping.
  */
  return <div className="d-grid gtc-1 gap-3 align-items-start">
    <nav className="d-grid gtc-fill-150 gap-1 border rounded p-1">
      {ADMIN_TABS.map(name => <button
        key={name}
        type="button"
        className={`admin-tab btn d-grid gap-1 text-start px-3 py-2 ${tab === name ? 'active' : ''}`}
        aria-current={tab === name ? 'page' : undefined}
        onClick={() => setTab(name)}
      >
        <strong>{ADMIN_TAB_META[name].label}</strong>
        {/* Inherits the button's colour so it stays legible once the tab fills in. */}
        <span className="small opacity-75">{ADMIN_TAB_META[name].kicker}</span>
      </button>)}
    </nav>
    {tab === 'overview' && <AdminOverviewTab overview={ctx.overview} busy={ctx.busy} />}
    {tab === 'players' && <AdminPlayersPanel busy={ctx.busy} onChanged={() => void ctx.act(async () => undefined)} />}
    {tab === 'keys' && <AdminKeysPanel busy={ctx.busy} />}
    {tab === 'ai' && <AdminAiTab ctx={ctx} />}
    {tab === 'config' && <><AdminConfigPanel busy={ctx.busy} /><AdminEconomyReadout overview={ctx.overview} /></>}
    {tab === 'titles' && <AdminTitlesPanel busy={ctx.busy} />}
    {tab === 'updates' && <AdminUpdatesPanel busy={ctx.busy} />}
    {tab === 'liveops' && <AdminLiveOpsPanel busy={ctx.busy} />}
    {tab === 'audit' && <AdminAuditPanel />}
  </div>
}

function AdminOverviewTab({ overview, busy }: { overview: AdminOverview, busy: boolean }) {
  return <>
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The World</h2><span>As of {new Date(overview.generatedAtUtc).toLocaleTimeString()}</span></div>
      <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
        <AdminMetric label="Accounts" value={number.format(overview.totalAccounts)} />
        <AdminMetric label="Admins" value={number.format(overview.adminAccounts)} />
        <AdminMetric label="AI rivals" value={number.format(overview.botAccounts)} />
        <AdminMetric label="AI auto" value={overview.botAutomation.enabled ? 'On' : 'Off'} />
        <AdminMetric label="Players" value={number.format(overview.totalPlayers)} />
        <AdminMetric label="Liquid cash" value={money.format(overview.totalLiquidCash)} />
        <AdminMetric label="Net worth" value={money.format(overview.totalNetWorth)} />
        <AdminMetric label="Turns banked" value={number.format(overview.totalTurnsBanked)} />
        <AdminMetric label="Avg hoe morale" value={`${overview.averageHoeMorale.toFixed(0)}%`} />
        <AdminMetric label="Avg thug morale" value={`${overview.averageThugMorale.toFixed(0)}%`} />
      </div>
    </section>
    <AdminOversightPanel busy={busy} />
  </>
}

function AdminEconomyReadout({ overview }: { overview: AdminOverview }) {
  const game = overview.economy
  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>In Effect Now</h2><span>Read-only summary</span></div>
    <div className="mt-3 border-top">
      <StatusRow label="Turns" value={`+${game.turnsPerTick} / ${game.turnTickMinutes}m, cap ${game.maxTurns}`} />
      <StatusRow label="Action limit" value={`${game.maxActionTurns} turns`} />
      <StatusRow label="Store prices" value={`Condom ${money.format(game.condomPrice)}, beer ${money.format(game.beerPrice)}, weapon ${money.format(game.weaponPrice)}`} />
      <StatusRow label="Product prices" value={`Weed ${money.format(game.weedSellPrice)}, coke ${money.format(game.cokeSellPrice)}`} />
      <StatusRow label="Crew hire costs" value={`P ${money.format(game.crew.hirePimpCost)} / H ${money.format(game.crew.hireHoeCost)} / T ${money.format(game.crew.hireThugCost)}`} />
      <StatusRow label="Recruit odds" value={`P ${percent(game.streetAction.pimpRecruitChance)} / H ${percent(game.streetAction.hoeRecruitChance)} / T ${percent(game.streetAction.thugRecruitChance)}`} />
      <StatusRow label="Production" value={`Weed ${money.format(game.production.weed.costPerTurn)} ${game.production.weed.unitsMin}-${game.production.weed.unitsMax}, coke ${money.format(game.production.coke.costPerTurn)} ${game.production.coke.unitsMin}-${game.production.coke.unitsMax}`} />
      <StatusRow label="Morale rules" value={`${game.morale.hoesManagedPerPimp} hoes/pimp, desertion below ${game.morale.desertionThreshold}%`} />
      <StatusRow label="Combat" value={`${game.combat.attackTurnCost} turns, ${game.combat.attackTravelSecondsMin}-${game.combat.attackTravelSecondsMax}s travel, ${game.combat.attackCooldownMinutes}m cooldown`} />
    </div>
  </section>
}

function AdminKeysPanel({ busy }: { busy: boolean }) {
  const [keys, setKeys] = useState<AdminBetaKey[]>([])
  const [total, setTotal] = useState(0)
  const [query, setQuery] = useState('')
  const [label, setLabel] = useState('')
  const [count, setCount] = useState(10)
  const [maxUses, setMaxUses] = useState(1)
  const [reason, setReason] = useState('')
  const [minted, setMinted] = useState<AdminBetaKey[]>([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async (nextQuery = query) => {
    try {
      const board = await adminApi.betaKeys(nextQuery.trim())
      setKeys(board.keys)
      setTotal(board.total)
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load('') }, [])

  const mint = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const created = await adminApi.mintBetaKeys({
        count,
        label: label.trim() || null,
        maxUses,
        reason: reason.trim() || null,
      })
      setMinted(created.keys)
      setMessage(`Minted ${number.format(created.keys.length)} beta key${created.keys.length === 1 ? '' : 's'}.`)
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const revoke = async (key: AdminBetaKey) => {
    if (!window.confirm(`Revoke ${key.displayCode}?`)) return
    setWorking(true); setError(''); setMessage('')
    try {
      const updated = await adminApi.revokeBetaKey(key.id, reason.trim() || undefined)
      setKeys(current => current.map(item => item.id === updated.id ? updated : item))
      setMessage(`${updated.displayCode} revoked.`)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const copy = async (value: string, said: string) => {
    try {
      await copyToClipboard(value)
      setMessage(said)
    } catch { setError('Could not copy to the clipboard.') }
  }

  const mintedBlock = minted.map(key => key.displayCode).join('\n')

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Beta Keys</h2><span>{total > keys.length ? `${keys.length} of ${total}` : `${keys.length}`}</span></div>
    {(error || message) && <div className="d-grid gap-2 mb-3">
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
    </div>}

    <div className="d-grid gtc-1 gtc-xl-2 gap-3 align-items-start">
      <form className="d-grid gap-3 border rounded bg-body-secondary p-3" onSubmit={mint}>
        <div className="panel-title mb-0"><h3 className="h5 mb-0">Mint</h3><span>Admin pool</span></div>
        <div className="d-grid gtc-1 gtc-md-3 gap-3">
          <label className="field">
            Count
            <input
              className="form-control"
              type="number"
              min={1}
              max={500}
              value={count}
              onChange={event => setCount(Math.max(1, Math.min(500, Number(event.target.value) || 1)))}
            />
          </label>
          <label className="field">
            Uses
            <input
              className="form-control"
              type="number"
              min={1}
              max={1000}
              value={maxUses}
              onChange={event => setMaxUses(Math.max(1, Math.min(1000, Number(event.target.value) || 1)))}
            />
          </label>
        </div>
        <label className="field">
          Label
          <input
            className="form-control"
            maxLength={120}
            value={label}
            placeholder="Optional batch label"
            onChange={event => setLabel(event.target.value)}
          />
        </label>
        <label className="field">
          Audit reason
          <input
            className="form-control"
            value={reason}
            placeholder="Optional"
            onChange={event => setReason(event.target.value)}
          />
        </label>
        <Button className="btn btn-primary" blocked={firstReason(busy && WORKING, working && 'The keys are being minted now.')}>
          {working ? 'Working...' : 'Mint Keys'}
        </Button>
      </form>

      <div className="d-grid gap-3">
        <form className="d-flex flex-wrap gap-2" onSubmit={event => { event.preventDefault(); void load(query) }}>
          <input
            className="form-control flex-fill"
            value={query}
            placeholder="Search code, label, player, username"
            onChange={event => setQuery(event.target.value)}
          />
          <Button className="btn btn-secondary" type="submit" blocked={working && WORKING}>Search</Button>
          <button className="btn btn-link text-body-secondary" type="button" onClick={() => { setQuery(''); void load('') }}>
            Clear
          </button>
        </form>
        {minted.length > 0 && <div className="border rounded bg-body-secondary p-3">
          <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
            <strong>Fresh keys</strong>
            <button
              className="btn btn-outline-primary btn-sm"
              type="button"
              onClick={() => void copy(mintedBlock, 'Fresh keys copied.')}
            >Copy Block</button>
          </div>
          <pre className="tnum mb-0 small">{mintedBlock}</pre>
        </div>}
      </div>
    </div>

    <div className="table-responsive mt-3">
      <table className="table table-sm align-middle mb-0">
        <thead>
          <tr>
            <th>Key</th>
            <th>Status</th>
            <th>Uses</th>
            <th>Chain</th>
            <th>Dates</th>
            <th className="text-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {keys.length === 0 && <tr><td colSpan={6} className="text-body-tertiary">No beta keys found.</td></tr>}
          {keys.map(key => {
            const issuedTo = key.issuedToPlayerName ?? key.issuedToUsername ?? 'Admin pool'
            const redeemedBy = key.redeemedByPlayerName ?? key.redeemedByUsername ?? 'Not redeemed'
            return <tr key={key.id}>
              <td className="tnum">
                <strong>{key.displayCode}</strong>
                {key.label && <small className="d-block text-body-tertiary text-truncate">{key.label}</small>}
              </td>
              <td><span className={`badge ${betaKeyStatusClass(key.status)}`}>{key.status}</span></td>
              <td className="tnum">{key.uses} / {key.maxUses}<small className="d-block text-body-tertiary">{key.usesLeft} left</small></td>
              <td className="small">
                <strong>{issuedTo}</strong>
                <span className="d-block text-body-tertiary">to {redeemedBy}</span>
              </td>
              <td className="small">
                <span className="d-block">Made {compactDateTime(key.createdAtUtc)}</span>
                <span className="d-block text-body-tertiary">Redeemed {compactDateTime(key.redeemedAtUtc)}</span>
              </td>
              <td className="text-end">
                <div className="btn-group btn-group-sm">
                  <button className="btn btn-outline-secondary" type="button" onClick={() => void copy(key.displayCode, 'Key copied.')}>
                    Copy
                  </button>
                  <Button className="btn btn-outline-danger" type="button" blocked={firstReason(
                    working && WORKING,
                    key.status === 'Revoked' && 'This key is already revoked.',
                  )} onClick={() => void revoke(key)}>
                    Revoke
                  </Button>
                </div>
              </td>
            </tr>
          })}
        </tbody>
      </table>
    </div>
  </section>
}

function AdminTitlesPanel({ busy }: { busy: boolean }) {
  const [titles, setTitles] = useState<AdminCustomTitle[]>([])
  const [criteria, setCriteria] = useState<CustomTitleCriteria[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [draft, setDraft] = useState<AdminCustomTitleDraft>(() => emptyCustomTitleDraft())
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [working, setWorking] = useState(false)
  const selected = titles.find(title => title.id === selectedId) ?? null
  const selectedCriteria = criteria.find(x => x.key === draft.criteria) ?? criteria[0]

  const load = async () => {
    try {
      const board = await opsApi.customTitles()
      setTitles(board.titles)
      setCriteria(board.criteria)
      setDraft(current => current.criteria ? current : { ...current, criteria: board.criteria[0]?.key ?? 'net-worth-at-least' })
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const edit = (title: AdminCustomTitle) => {
    setSelectedId(title.id)
    setDraft({
      key: title.key,
      title: title.title,
      detail: title.detail,
      criteria: title.criteria,
      threshold: title.threshold,
      textValue: title.textValue ?? '',
      isActive: title.isActive,
      reason: '',
    })
    setMessage('')
    setError('')
  }

  const reset = () => {
    setSelectedId(null)
    setDraft(emptyCustomTitleDraft(criteria[0]?.key))
    setMessage('')
    setError('')
  }

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const body = {
        ...draft,
        key: draft.key?.trim() || null,
        title: draft.title?.trim() || null,
        detail: draft.detail?.trim() || null,
        textValue: draft.textValue?.trim() || null,
        threshold: selectedCriteria?.needsThreshold ? Number(draft.threshold ?? 0) : 0,
        reason: draft.reason?.trim() || null,
      }
      const saved = selected
        ? await opsApi.updateCustomTitle(selected.id, body)
        : await opsApi.createCustomTitle(body)
      setSelectedId(saved.id)
      setDraft({
        key: saved.key,
        title: saved.title,
        detail: saved.detail,
        criteria: saved.criteria,
        threshold: saved.threshold,
        textValue: saved.textValue ?? '',
        isActive: saved.isActive,
        reason: '',
      })
      setMessage(selected ? 'Title saved.' : 'Title created.')
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const locked = busy || working
  return <div className="d-grid gtc-1 gtc-xl-split-60 gap-3 align-items-start gcol-full">
    <section className="card p-3">
      <div className="panel-title"><h2>Custom Titles</h2><span>{titles.length} defined</span></div>
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
      <div className="d-flex flex-wrap gap-2 mb-3">
        <button className="btn btn-primary btn-sm" type="button" onClick={reset}>New title</button>
        <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void load()}>Refresh</Button>
      </div>
      <div className="d-grid gap-1">
        {titles.length === 0 && <p className="text-body-tertiary small mb-0">No custom titles yet.</p>}
        {titles.map(title => <button
          className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${selectedId === title.id ? 'active border-primary' : ''}`}
          type="button"
          key={title.id}
          onClick={() => edit(title)}
        >
          <span className="d-flex flex-wrap gap-2 align-items-center min-w-0">
            <strong className="text-truncate">{title.title}</strong>
            <span className="badge rounded-pill text-bg-secondary">{title.key}</span>
            <span className={`badge rounded-pill ${title.isActive ? 'text-bg-success' : 'text-bg-light border'}`}>{title.isActive ? 'Active' : 'Paused'}</span>
          </span>
          <small className="text-body-tertiary text-truncate">{title.criteria}{title.threshold > 0 ? ` ${number.format(title.threshold)}` : ''}{title.textValue ? ` ${title.textValue}` : ''}</small>
        </button>)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>{selected ? 'Edit Title' : 'New Title'}</h2><span>{draft.key || 'achievement'}</span></div>
      <form className="d-grid gap-3" onSubmit={save}>
        <div className="d-grid gtc-1 gtc-md-2 gap-3">
          <label className="field">
            Key
            <input className="form-control" maxLength={32} value={draft.key ?? ''} onChange={event => setDraft({ ...draft, key: event.target.value })} placeholder="millionaire" required />
          </label>
          <label className="field">
            Title
            <input className="form-control" maxLength={64} value={draft.title ?? ''} onChange={event => setDraft({ ...draft, title: event.target.value })} placeholder="Millionaire" required />
          </label>
        </div>
        <label className="field">
          Detail
          <input className="form-control" maxLength={240} value={draft.detail ?? ''} onChange={event => setDraft({ ...draft, detail: event.target.value })} placeholder="Reached $1,000,000 net worth." />
        </label>
        <div className="d-grid gtc-1 gtc-md-2 gap-3">
          <label className="field">
            Earned by
            <select className="form-select" value={draft.criteria ?? criteria[0]?.key ?? ''} onChange={event => setDraft({ ...draft, criteria: event.target.value })}>
              {criteria.map(option => <option key={option.key} value={option.key}>{option.label}</option>)}
            </select>
          </label>
          {selectedCriteria?.needsThreshold
            ? <label className="field">
              Threshold
              <input className="form-control" type="number" min={1} step={1} value={draft.threshold ?? 0} onChange={event => setDraft({ ...draft, threshold: Number(event.target.value) })} />
            </label>
            : selectedCriteria?.needsText
            ? <label className="field">
              Name
              <input className="form-control" maxLength={64} value={draft.textValue ?? ''} onChange={event => setDraft({ ...draft, textValue: event.target.value })} placeholder={draft.criteria === 'city-is' ? 'Chicago' : 'The Eastside Table'} />
            </label>
            : <div className="d-flex align-items-end"><small className="text-body-tertiary">No extra value needed.</small></div>}
        </div>
        <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
          <input className="form-check-input" type="checkbox" checked={draft.isActive ?? true} onChange={event => setDraft({ ...draft, isActive: event.target.checked })} />
          <span>Active</span>
        </label>
        <label className="field">
          Audit reason
          <input className="form-control" value={draft.reason ?? ''} onChange={event => setDraft({ ...draft, reason: event.target.value })} placeholder="Added a new milestone title" />
        </label>
        <div className="d-flex flex-wrap gap-2">
          <Button className="btn btn-primary" blocked={locked && WORKING}>{locked ? 'Working...' : selected ? 'Save Title' : 'Create Title'}</Button>
          {selected && <Button className="btn btn-secondary" type="button" blocked={locked && WORKING} onClick={reset}>Clear Form</Button>}
        </div>
      </form>
    </section>
  </div>
}

function AdminUpdatesPanel({ busy }: { busy: boolean }) {
  const [posts, setPosts] = useState<AdminGameAnnouncement[]>([])
  const [delivery, setDelivery] = useState<AnnouncementDeliverySettings | null>(null)
  const [discord, setDiscord] = useState<DiscordIntegrationSettings | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [draft, setDraft] = useState<AdminGameAnnouncementDraft>(() => emptyAnnouncementDraft())
  const [includeArchived, setIncludeArchived] = useState(false)
  const [discordWebhookUrl, setDiscordWebhookUrl] = useState('')
  const [discordUsername, setDiscordUsername] = useState('')
  const [discordBotToken, setDiscordBotToken] = useState('')
  const [discordPublicKey, setDiscordPublicKey] = useState('')
  const [discordApplicationId, setDiscordApplicationId] = useState('')
  const [discordGuildId, setDiscordGuildId] = useState('')
  const [discordLinkedRoleId, setDiscordLinkedRoleId] = useState('')
  const [discordTopTenRoleId, setDiscordTopTenRoleId] = useState('')
  const [discordCrewBossRoleId, setDiscordCrewBossRoleId] = useState('')
  const [discordCityRoleMap, setDiscordCityRoleMap] = useState('')
  const [discordCrewRoleMap, setDiscordCrewRoleMap] = useState('')
  const [discordCrewChannelMap, setDiscordCrewChannelMap] = useState('')
  const [discordTitleRoleMap, setDiscordTitleRoleMap] = useState('')
  const [discordConsole, setDiscordConsole] = useState<string[]>([])
  const [reason, setReason] = useState('')
  const [deliveryReason, setDeliveryReason] = useState('')
  const [discordReason, setDiscordReason] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [working, setWorking] = useState(false)
  const selected = posts.find(post => post.id === selectedId) ?? null
  const discordInviteUrl = discordBotInviteUrl(discord, discordApplicationId, discordGuildId)

  const load = async () => {
    try { setPosts(await opsApi.updates(includeArchived)) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [includeArchived])

  const loadDelivery = async () => {
    try {
      const next = await opsApi.updateDelivery()
      setDelivery(next)
      setDiscordUsername(next.discordUsername)
    } catch (e) {
      setError((e as Error).message)
    }
  }
  useEffect(() => { void loadDelivery() }, [])

  const applyDiscordSettings = (next: DiscordIntegrationSettings) => {
    setDiscord(next)
    setDiscordApplicationId(next.applicationId ?? '')
    setDiscordGuildId(next.guildId ?? '')
    setDiscordLinkedRoleId(next.linkedRoleId ?? '')
    setDiscordTopTenRoleId(next.topTenRoleId ?? '')
    setDiscordCrewBossRoleId(next.crewBossRoleId ?? '')
    setDiscordCityRoleMap(next.cityRoleMap ?? '')
    setDiscordCrewRoleMap(next.crewRoleMap ?? '')
    setDiscordCrewChannelMap(next.crewChannelMap ?? '')
    setDiscordTitleRoleMap(next.titleRoleMap ?? '')
  }

  const logDiscord = (line: string, issues: string[] = []) => {
    const stamp = new Date().toLocaleTimeString()
    setDiscordConsole(previous => [`${stamp} ${line}`, ...issues.map(issue => `${stamp} ! ${issue}`), ...previous].slice(0, 10))
  }

  const loadDiscord = async () => {
    try { applyDiscordSettings(await opsApi.discordIntegration()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void loadDiscord() }, [])

  const edit = (post: AdminGameAnnouncement) => {
    setSelectedId(post.id)
    setDraft(draftFromAnnouncement(post))
    setReason('')
    setMessage('')
    setError('')
  }

  const reset = () => {
    setSelectedId(null)
    setDraft(emptyAnnouncementDraft())
    setReason('')
    setMessage('')
    setError('')
  }

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const body = announcementPayload(draft, reason)
      const saved = selected
        ? await opsApi.updatePost(selected.id, body)
        : await opsApi.createUpdate(body)
      setSelectedId(saved.id)
      setDraft(draftFromAnnouncement(saved))
      setReason('')
      setMessage(saved.isDraft ? 'Draft saved.' : selected ? 'Update saved.' : 'Update published.')
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const archive = async (archived: boolean) => {
    if (!selected) return
    setWorking(true); setError(''); setMessage('')
    try {
      const saved = await opsApi.archiveUpdate(selected.id, archived, reason)
      setReason('')
      setMessage(archived ? 'Update archived.' : 'Update restored.')
      await load()
      setSelectedId(saved.id)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const saveDelivery = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setUpdateDelivery({
        discordWebhookUrl: discordWebhookUrl.trim() || null,
        discordUsername: discordUsername.trim() || null,
        reason: deliveryReason.trim() || null,
      })
      setDelivery(next)
      setDiscordWebhookUrl('')
      setDiscordUsername(next.discordUsername)
      setDeliveryReason('')
      setMessage('Discord announcement settings saved.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const clearDeliveryWebhook = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setUpdateDelivery({
        clearDiscordWebhook: true,
        discordUsername: discordUsername.trim() || null,
        reason: deliveryReason.trim() || null,
      })
      setDelivery(next)
      setDiscordWebhookUrl('')
      setDiscordUsername(next.discordUsername)
      setDeliveryReason('')
      setMessage('Saved webhook cleared.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const saveDiscord = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setDiscordIntegration({
        botToken: discordBotToken.trim() || null,
        publicKey: discordPublicKey.trim() || null,
        applicationId: discordApplicationId.trim() || null,
        guildId: discordGuildId.trim() || null,
        linkedRoleId: discordLinkedRoleId.trim() || null,
        topTenRoleId: discordTopTenRoleId.trim() || null,
        crewBossRoleId: discordCrewBossRoleId.trim() || null,
        cityRoleMap: discordCityRoleMap,
        crewRoleMap: discordCrewRoleMap,
        crewChannelMap: discordCrewChannelMap,
        titleRoleMap: discordTitleRoleMap,
        reason: discordReason.trim() || null,
      })
      applyDiscordSettings(next)
      setDiscordBotToken('')
      setDiscordPublicKey('')
      setDiscordReason('')
      logDiscord('Saved Discord bot settings.')
      setMessage('Discord integration settings saved.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const clearDiscordSecret = async (kind: 'token' | 'key') => {
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setDiscordIntegration({
        clearBotToken: kind === 'token',
        clearPublicKey: kind === 'key',
        reason: discordReason.trim() || null,
      })
      applyDiscordSettings(next)
      if (kind === 'token') setDiscordBotToken('')
      if (kind === 'key') setDiscordPublicKey('')
      setDiscordReason('')
      logDiscord(kind === 'token' ? 'Cleared the saved bot token.' : 'Cleared the saved public key.')
      setMessage(kind === 'token' ? 'Discord bot token cleared.' : 'Discord public key cleared.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const registerDiscordCommands = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await opsApi.registerDiscordCommands()
      logDiscord(`Registered ${result.registered} slash command${result.registered === 1 ? '' : 's'}.`)
      setMessage(`Registered ${result.registered} slash command${result.registered === 1 ? '' : 's'} in Discord.`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const syncDiscordRoles = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result: DiscordRoleSyncResult = await opsApi.syncDiscordRoles()
      const tail = result.errors.length > 0 ? ` ${result.errors.length} issue${result.errors.length === 1 ? '' : 's'} reported.` : ''
      logDiscord(`Synced roles for ${result.syncedPlayers}/${result.linkedPlayers} linked members: +${result.rolesAdded} / -${result.rolesRemoved}.`, result.errors)
      setMessage(`Synced ${result.syncedPlayers} linked member${result.syncedPlayers === 1 ? '' : 's'}: +${result.rolesAdded} / -${result.rolesRemoved}.${tail}`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const ensureDiscordRoles = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await opsApi.ensureDiscordRoles()
      const tail = result.errors.length > 0 ? ` ${result.errors.length} role${result.errors.length === 1 ? '' : 's'} could not be created.` : ''
      logDiscord(`Role maps ready: ${result.cityRoles} city, ${result.crewRoles} crew, ${result.titleRoles} title. Created ${result.createdRoles}, reused ${result.reusedRoles}.`, result.errors)
      setMessage(`Role maps ready: ${result.cityRoles} city, ${result.crewRoles} crew, ${result.titleRoles} title. Created ${result.createdRoles}, reused ${result.reusedRoles}.${tail}`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const syncDiscordCrewChannels = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result: DiscordCrewChannelSyncResult = await opsApi.syncDiscordCrewChannels()
      const tail = result.errors.length > 0 ? ` ${result.errors.length} issue${result.errors.length === 1 ? '' : 's'} reported.` : ''
      logDiscord(`Crew channels synced: ${result.channels}/${result.crews} mapped. Created ${result.createdChannels}, reused ${result.reusedChannels}, updated ${result.updatedChannels}.`, result.errors)
      setMessage(`Crew channels synced: ${result.channels}/${result.crews} mapped. Created ${result.createdChannels}, reused ${result.reusedChannels}, updated ${result.updatedChannels}.${tail}`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const locked = busy || working
  return <div className="d-grid gtc-1 gtc-xl-split-80 gap-3 align-items-start gcol-full">
    {/*
      Two columns, and the left one is a stack rather than two cells of the same grid.

      The list, the webhook and the editor were three children of a two-column grid, so they laid out
      row by row: the list beside the webhook, and the editor underneath on its own. With the rows
      sized to their tallest cell, that left the short list sitting at the top of a row as tall as the
      webhook form, and several hundred pixels of nothing under it before the editor began.

      The list and the editor belong together anyway - you pick an update in one and edit it in the
      other - and the webhook is a setting that happens to live on this page.
    */}
    <div className="d-grid gap-3 align-content-start">
      <section className="card p-3">
        <div className="panel-title">
          <h2>Updates</h2>
          <span>{posts.length} shown</span>
        </div>
        {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
        {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
        <div className="d-flex flex-wrap gap-2 mb-3">
          <button className="btn btn-primary btn-sm" type="button" onClick={reset}>New update</button>
          <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
            <input className="form-check-input" type="checkbox" checked={includeArchived} onChange={event => setIncludeArchived(event.target.checked)} />
            <span className="small">Include archived</span>
          </label>
        </div>
        <div className="d-grid gap-1">
          {posts.length === 0 && <p className="text-body-tertiary small mb-0">No updates posted yet.</p>}
          {posts.map(post => <button
            className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${selectedId === post.id ? 'active border-primary' : ''}`}
            type="button"
            key={post.id}
            onClick={() => edit(post)}
          >
            <span className="d-flex flex-wrap gap-2 align-items-center min-w-0">
              <strong className="text-truncate">{post.title}</strong>
              <span className={`badge rounded-pill ${updateCategoryClass(post.category)}`}>{post.category}</span>
              <span className={`badge rounded-pill ${updateSeverityClass(post.severity)}`}>{post.severity}</span>
              {post.version && <span className="badge rounded-pill text-bg-secondary">{post.version}</span>}
              {post.isPinned && <span className="badge rounded-pill text-bg-primary">Pinned</span>}
              {post.showOnce && <span className="badge rounded-pill text-bg-warning">Login</span>}
              {post.isDraft && <span className="badge rounded-pill text-bg-light border">Draft</span>}
              {!post.isDraft && !post.archivedAtUtc && <span className="badge rounded-pill text-bg-success">Live</span>}
              {post.sendToDiscord && <span className={`badge rounded-pill ${post.discordSentAtUtc ? 'text-bg-info' : 'text-bg-light border'}`}>Discord</span>}
              {post.archivedAtUtc && <span className="badge rounded-pill text-bg-secondary">Archived</span>}
            </span>
            <small className="text-body-tertiary text-truncate">
              {post.isDraft ? 'Draft publish time ' : 'Published '}
              {new Date(post.publishedAtUtc).toLocaleString()}
            </small>
          </button>)}
        </div>
      </section>

      <section className="card p-3">
        <div className="panel-title"><h2>{selected ? 'Edit Update' : 'New Update'}</h2><span>{draft.version || draft.category}</span></div>
        <form className="d-grid gap-3" onSubmit={save}>
          <div className="d-grid gtc-1 gtc-md-2 gap-2">
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input
                className="form-check-input"
                type="checkbox"
                checked={!draft.isDraft}
                onChange={event => setDraft({ ...draft, isDraft: !event.target.checked })}
              />
              <span>{draft.isDraft ? 'Save as draft' : 'Publish to players'}</span>
            </label>
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input className="form-check-input" type="checkbox" checked={Boolean(draft.isPinned)} onChange={event => setDraft({ ...draft, isPinned: event.target.checked })} />
              <span>Pin in Street Wire</span>
            </label>
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input className="form-check-input" type="checkbox" checked={Boolean(draft.showOnce)} onChange={event => setDraft({ ...draft, showOnce: event.target.checked })} />
              <span>Show once on login</span>
            </label>
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input className="form-check-input" type="checkbox" checked={Boolean(draft.sendToDiscord)} onChange={event => setDraft({ ...draft, sendToDiscord: event.target.checked })} />
              <span>Send to Discord</span>
            </label>
          </div>
          <label className="field">
            Title
            <input className="form-control" maxLength={96} value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} required />
          </label>
          <label className="field">
            Body
            <textarea className="form-control" rows={7} maxLength={4000} value={draft.body} onChange={event => setDraft({ ...draft, body: event.target.value })} required />
          </label>
          <div className="d-grid gtc-1 gtc-md-3 gap-3">
            <label className="field">
              Category
              <select className="form-select" value={draft.category} onChange={event => setDraft({ ...draft, category: event.target.value as GameAnnouncement['category'] })}>
                {updateCategories.map(category =>
                  <option key={category} value={category}>{category}</option>)}
              </select>
            </label>
            <label className="field">
              Severity
              <select className="form-select" value={draft.severity} onChange={event => setDraft({ ...draft, severity: event.target.value as GameAnnouncement['severity'] })}>
                {updateSeverities.map(severity =>
                  <option key={severity} value={severity}>{severity}</option>)}
              </select>
            </label>
            <label className="field">
              Version
              <input className="form-control" maxLength={32} value={draft.version ?? ''} onChange={event => setDraft({ ...draft, version: event.target.value })} placeholder={__APP_VERSION__} />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Starts at
              <input className="form-control" type="datetime-local" value={draft.publishedAtUtc ?? ''} onChange={event => setDraft({ ...draft, publishedAtUtc: event.target.value || null })} />
            </label>
            <label className="field">
              Ends at
              <input className="form-control" type="datetime-local" value={draft.expiresAtUtc ?? ''} onChange={event => setDraft({ ...draft, expiresAtUtc: event.target.value || null })} />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Added
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.added ?? ''} onChange={event => setDraft({ ...draft, added: event.target.value })} />
            </label>
            <label className="field">
              Changed
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.changed ?? ''} onChange={event => setDraft({ ...draft, changed: event.target.value })} />
            </label>
            <label className="field">
              Fixed
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.fixed ?? ''} onChange={event => setDraft({ ...draft, fixed: event.target.value })} />
            </label>
            <label className="field">
              Known issues
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.knownIssues ?? ''} onChange={event => setDraft({ ...draft, knownIssues: event.target.value })} />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Action label
              <input className="form-control" maxLength={40} value={draft.actionLabel ?? ''} onChange={event => setDraft({ ...draft, actionLabel: event.target.value })} placeholder="Optional" />
            </label>
            <label className="field">
              Action URL
              <input className="form-control" maxLength={240} value={draft.actionUrl ?? ''} onChange={event => setDraft({ ...draft, actionUrl: event.target.value })} placeholder="/account" />
            </label>
          </div>
          <label className="field">
            Audit reason
            <input className="form-control" value={reason} onChange={event => setReason(event.target.value)} placeholder="Why this is being posted or changed" />
          </label>
          <div className="d-flex flex-wrap gap-2">
            <Button className="btn btn-primary" blocked={locked && WORKING}>
              {locked ? 'Working...' : draft.isDraft ? 'Save Draft' : selected ? 'Save and Publish' : 'Publish Update'}
            </Button>
            {selected && <Button className="btn btn-secondary" type="button" blocked={locked && WORKING} onClick={reset}>Clear Form</Button>}
            {selected && <Button
              className="btn btn-outline-danger"
              type="button"
              blocked={locked && WORKING}
              onClick={() => void archive(!selected.archivedAtUtc)}
            >{selected.archivedAtUtc ? 'Restore' : 'Archive'}</Button>}
          </div>
        </form>
      </section>
    </div>

    <div className="d-grid gap-3 align-content-start">
      <section className="card p-3">
        <div className="panel-title">
          <h2>Discord Webhook</h2>
          <span>{delivery?.discordConfigured ? delivery.discordUsesStoredWebhook ? 'Saved in admin' : 'From config' : 'Not set'}</span>
        </div>
        <form className="d-grid gap-3" onSubmit={saveDelivery}>
          <div className="d-flex flex-wrap gap-2">
            <span className={`badge rounded-pill ${delivery?.discordConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>
              {delivery?.discordConfigured ? 'Discord broadcast on' : 'Discord broadcast off'}
            </span>
            {delivery?.discordWebhookHost && <span className="badge rounded-pill text-bg-light border">{delivery.discordWebhookHost}</span>}
          </div>
          <label className="field">
            New webhook URL
            <input
              className="form-control"
              type="password"
              value={discordWebhookUrl}
              onChange={event => setDiscordWebhookUrl(event.target.value)}
              placeholder={delivery?.discordConfigured ? 'Paste a replacement webhook' : 'https://discord.com/api/webhooks/...'}
              autoComplete="off"
            />
            <small className="form-text">Saved URLs are not shown again. Leave blank to keep the current webhook.</small>
          </label>
          <label className="field">
            Webhook name
            <input className="form-control" maxLength={80} value={discordUsername} onChange={event => setDiscordUsername(event.target.value)} placeholder="Street Empire" />
          </label>
          <label className="field">
            Audit reason
            <input className="form-control" value={deliveryReason} onChange={event => setDeliveryReason(event.target.value)} placeholder="Moved announcements to #updates" />
          </label>
          <div className="d-flex flex-wrap gap-2">
            <Button className="btn btn-primary btn-sm" blocked={locked && WORKING}>{locked ? 'Working...' : 'Save Webhook Settings'}</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void loadDelivery()}>Refresh</Button>
            <Button className="btn btn-outline-danger btn-sm" type="button" blocked={firstReason(
              locked && WORKING,
              !delivery?.discordUsesStoredWebhook && 'There is no saved webhook to clear.',
            )} onClick={() => void clearDeliveryWebhook()}>
              Clear saved webhook
            </Button>
          </div>
          {delivery && <small className="text-body-tertiary">
            Last changed {new Date(delivery.updatedAtUtc).toLocaleString()}{delivery.updatedBy ? ` by ${delivery.updatedBy}` : ''}.
          </small>}
        </form>
      </section>

      <section className="card p-3">
        <div className="panel-title">
          <h2>Discord Bot</h2>
          <span>{discord?.gatewayConnected ? 'Online' : discord?.botConfigured ? 'Starting' : 'Needs setup'}</span>
        </div>
        <form className="d-grid gap-3" onSubmit={saveDiscord}>
          <div className="d-flex flex-wrap gap-2">
            <span className={`badge rounded-pill ${discord?.botConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.botConfigured ? 'Bot configured' : 'No bot'}</span>
            <span className={`badge rounded-pill ${discord?.gatewayConnected ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.gatewayConnected ? 'Gateway online' : 'Gateway offline'}</span>
            <span className={`badge rounded-pill ${discord?.slashCommandsConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.slashCommandsConfigured ? 'Slash ready' : 'Slash off'}</span>
            <span className={`badge rounded-pill ${discord?.roleSyncConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.roleSyncConfigured ? 'Role sync ready' : 'Roles off'}</span>
            {discord?.usesStoredBotToken && <span className="badge rounded-pill text-bg-light border">Token saved</span>}
            {discord?.publicKeyConfigured && <span className="badge rounded-pill text-bg-light border">Public key saved</span>}
          </div>
          {discord?.gatewayError && <small className="text-body-tertiary">{discord.gatewayError}</small>}
          <label className="field">
            Interaction endpoint
            <input className="form-control" readOnly value={`${window.location.origin}/api/discord/interactions`} />
          </label>
          <div className="d-flex flex-wrap gap-2">
            {discordInviteUrl
              ? <a className="btn btn-outline-primary btn-sm" href={discordInviteUrl} target="_blank" rel="noreferrer">
                  Add bot to Discord
                </a>
              : <Button className="btn btn-outline-secondary btn-sm" type="button" blocked="Fill in the application ID and server ID below and save, and the invite link appears here.">Add bot to Discord</Button>}
          </div>
          <label className="field">
            Bot token
            <input className="form-control" type="password" value={discordBotToken} onChange={event => setDiscordBotToken(event.target.value)} placeholder={discord?.botConfigured ? 'Paste a replacement token' : 'Discord bot token'} autoComplete="off" />
            <small className="form-text">Saved tokens are not shown again. Leave blank to keep the current one.</small>
          </label>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Application ID
              <input className="form-control" value={discordApplicationId} onChange={event => setDiscordApplicationId(event.target.value)} placeholder="123456789012345678" />
            </label>
            <label className="field">
              Guild ID
              <input className="form-control" value={discordGuildId} onChange={event => setDiscordGuildId(event.target.value)} placeholder="123456789012345678" />
            </label>
          </div>
          <label className="field">
            Public key
            <input className="form-control" type="password" value={discordPublicKey} onChange={event => setDiscordPublicKey(event.target.value)} placeholder={discord?.publicKeyConfigured ? 'Paste a replacement public key' : '64-character application public key'} autoComplete="off" />
          </label>
          <div className="d-grid gtc-1 gtc-md-3 gap-3">
            <label className="field">
              Linked role
              <input className="form-control" value={discordLinkedRoleId} onChange={event => setDiscordLinkedRoleId(event.target.value)} placeholder="Role ID" />
            </label>
            <label className="field">
              Top ten role
              <input className="form-control" value={discordTopTenRoleId} onChange={event => setDiscordTopTenRoleId(event.target.value)} placeholder="Role ID" />
            </label>
            <label className="field">
              Crew boss role
              <input className="form-control" value={discordCrewBossRoleId} onChange={event => setDiscordCrewBossRoleId(event.target.value)} placeholder="Role ID" />
            </label>
          </div>
          <label className="field">
            City roles
            <textarea className="form-control" rows={5} value={discordCityRoleMap} onChange={event => setDiscordCityRoleMap(event.target.value)} placeholder={'Chicago=123456789012345678\nMiami=234567890123456789'} />
          </label>
          <label className="field">
            Crew roles
            <textarea className="form-control" rows={5} value={discordCrewRoleMap} onChange={event => setDiscordCrewRoleMap(event.target.value)} placeholder={'The Eastside Table=123456789012345678\nThe Southside Table=234567890123456789'} />
          </label>
          <label className="field">
            Crew channels
            <textarea className="form-control" rows={5} value={discordCrewChannelMap} onChange={event => setDiscordCrewChannelMap(event.target.value)} placeholder={'The Eastside Table=123456789012345678\nThe Southside Table=234567890123456789'} />
            <small className="form-text">Run crew channel sync to let the bot create and fill this map.</small>
          </label>
          <label className="field">
            Title roles
            <textarea className="form-control" rows={5} value={discordTitleRoleMap} onChange={event => setDiscordTitleRoleMap(event.target.value)} placeholder={'killer=123456789012345678\nwheelman=234567890123456789\ndiscord-connected=345678901234567890'} />
          </label>
          <label className="field">
            Audit reason
            <input className="form-control" value={discordReason} onChange={event => setDiscordReason(event.target.value)} placeholder="Added Discord role sync" />
          </label>
          <div className="d-flex flex-wrap gap-2">
            <Button className="btn btn-primary btn-sm" blocked={locked && WORKING}>{locked ? 'Working...' : 'Save Bot Settings'}</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void registerDiscordCommands()}>Register slash commands</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void ensureDiscordRoles()}>Create role maps</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void syncDiscordCrewChannels()}>Sync crew channels</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void syncDiscordRoles()}>Sync roles now</Button>
            <Button className="btn btn-outline-danger btn-sm" type="button" blocked={firstReason(
              locked && WORKING,
              !discord?.usesStoredBotToken && 'There is no saved bot token to clear.',
            )} onClick={() => void clearDiscordSecret('token')}>Clear token</Button>
            <Button className="btn btn-outline-danger btn-sm" type="button" blocked={firstReason(
              locked && WORKING,
              !discord?.publicKeyConfigured && 'There is no saved public key to clear.',
            )} onClick={() => void clearDiscordSecret('key')}>Clear key</Button>
          </div>
          <div className="border rounded bg-body-secondary p-2 d-grid gap-1">
            <strong className="small">Discord Console</strong>
            {discordConsole.length === 0
              ? <small className="text-body-tertiary">No bot actions have run in this browser session.</small>
              : discordConsole.map((line, index) => <small className="font-monospace text-body-tertiary" key={`${line}-${index}`}>{line}</small>)}
          </div>
          {discord && <small className="text-body-tertiary">
            Commands {discord.commandsRegisteredAtUtc ? new Date(discord.commandsRegisteredAtUtc).toLocaleString() : 'not registered'}.
            {' '}Crew channels {discord.crewChannelsSyncedAtUtc ? new Date(discord.crewChannelsSyncedAtUtc).toLocaleString() : 'not synced'}.
            {' '}Roles {discord.rolesSyncedAtUtc ? new Date(discord.rolesSyncedAtUtc).toLocaleString() : 'not synced'}.
            {' '}Gateway {discord.gatewayHeartbeatAtUtc ? `heartbeat ${new Date(discord.gatewayHeartbeatAtUtc).toLocaleString()}` : 'no heartbeat yet'}.
          </small>}
        </form>
      </section>
    </div>
  </div>
}

function emptyCustomTitleDraft(criteria = 'net-worth-at-least'): AdminCustomTitleDraft {
  return {
    key: '',
    title: '',
    detail: '',
    criteria,
    threshold: 1,
    textValue: '',
    isActive: true,
    reason: '',
  }
}

function emptyAnnouncementDraft(): AdminGameAnnouncementDraft {
  return {
    title: '',
    body: '',
    category: 'Info',
    severity: 'Info',
    version: __APP_VERSION__,
    actionLabel: '',
    actionUrl: '',
    isDraft: true,
    isPinned: false,
    showOnce: false,
    sendToDiscord: false,
    publishedAtUtc: '',
    expiresAtUtc: '',
    added: '',
    changed: '',
    fixed: '',
    knownIssues: '',
  }
}

function discordBotInviteUrl(settings: DiscordIntegrationSettings | null, applicationId: string, guildId: string) {
  const clientId = (applicationId.trim() || settings?.applicationId || '').trim()
  if (!/^\d+$/.test(clientId)) return null

  const params = new URLSearchParams({
    client_id: clientId,
    scope: 'bot applications.commands',
    permissions: '268435472',
  })
  const guild = (guildId.trim() || settings?.guildId || '').trim()
  if (/^\d+$/.test(guild)) {
    params.set('guild_id', guild)
    params.set('disable_guild_select', 'true')
  }
  return `https://discord.com/oauth2/authorize?${params.toString()}`
}

function draftFromAnnouncement(post: AdminGameAnnouncement): AdminGameAnnouncementDraft {
  return {
    title: post.title,
    body: post.body,
    category: post.category,
    severity: post.severity,
    version: post.version ?? '',
    actionLabel: post.actionLabel ?? '',
    actionUrl: post.actionUrl ?? '',
    isDraft: post.isDraft,
    isPinned: post.isPinned,
    showOnce: post.showOnce,
    sendToDiscord: post.sendToDiscord,
    publishedAtUtc: toLocalDateTimeInput(post.publishedAtUtc),
    expiresAtUtc: post.expiresAtUtc ? toLocalDateTimeInput(post.expiresAtUtc) : '',
    added: post.added ?? '',
    changed: post.changed ?? '',
    fixed: post.fixed ?? '',
    knownIssues: post.knownIssues ?? '',
  }
}

function announcementPayload(draft: AdminGameAnnouncementDraft, reason: string): AdminGameAnnouncementDraft {
  return {
    title: draft.title.trim(),
    body: draft.body.trim(),
    category: draft.category,
    severity: draft.severity,
    version: draft.version?.trim() || null,
    actionLabel: draft.actionLabel?.trim() || null,
    actionUrl: draft.actionUrl?.trim() || null,
    isDraft: draft.isDraft ?? false,
    isPinned: draft.isPinned ?? false,
    showOnce: draft.showOnce ?? false,
    sendToDiscord: draft.sendToDiscord ?? false,
    publishedAtUtc: draft.publishedAtUtc ? new Date(draft.publishedAtUtc).toISOString() : null,
    expiresAtUtc: draft.expiresAtUtc ? new Date(draft.expiresAtUtc).toISOString() : null,
    added: draft.added?.trim() || null,
    changed: draft.changed?.trim() || null,
    fixed: draft.fixed?.trim() || null,
    knownIssues: draft.knownIssues?.trim() || null,
    reason: reason.trim() || null,
  }
}

function toLocalDateTimeInput(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}

function AdminLiveOpsPanel({ busy }: { busy: boolean }) {
  const [ops, setOps] = useState<LiveOps | null>(null)
  const [announcement, setAnnouncement] = useState('')
  const [maintenanceMessage, setMaintenanceMessage] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async () => {
    try {
      const next = await opsApi.liveOps()
      setOps(next)
      setAnnouncement(next.announcement ?? '')
      setMaintenanceMessage(next.maintenanceMessage ?? '')
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const apply = async (body: Parameters<typeof opsApi.setLiveOps>[0]) => {
    setWorking(true); setError('')
    try {
      const next = await opsApi.setLiveOps(body)
      setOps(next)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const locked = busy || working
  return <section className={`card p-3 gcol-full ${ops?.maintenanceMode ? 'border-warning' : ''}`}>
    <div className="panel-title">
      <h2>Live Operations</h2>
      <span>{ops?.maintenanceMode ? 'Maintenance is ON' : 'Game is open'}</span>
    </div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    <p>Maintenance blocks every gameplay action for players while leaving reads and admin access open, so you can verify a deploy before letting anyone back in.</p>
    <div className="control-row">
      <Button
        className={ops?.maintenanceMode ? 'btn btn-primary btn-sm' : 'btn btn-secondary btn-sm'}
        blocked={locked && WORKING}
        onClick={() => void apply({ maintenanceMode: !ops?.maintenanceMode })}
      >
        {ops?.maintenanceMode ? 'End maintenance' : 'Start maintenance'}
      </Button>
      <label className="field">Maintenance notice<input className="form-control" value={maintenanceMessage} onChange={e => setMaintenanceMessage(e.target.value)} placeholder="Back in 10 minutes" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
        onClick={() => void apply({ maintenanceMessage })}>Save notice</Button>
    </div>
    <div className="control-row">
      <label className="grow">Announcement banner<input className="form-control" value={announcement} onChange={e => setAnnouncement(e.target.value)} placeholder="Shown to every player" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
        onClick={() => void apply({ announcement })}>Save banner</Button>
      <Button className="btn btn-secondary btn-sm" blocked={firstReason(
        locked && WORKING,
        !ops?.announcement && 'There is no banner up to clear.',
      )} onClick={() => void apply({ announcement: '' })}>Clear</Button>
    </div>
    {ops && <small className="d-block mt-2 text-body-tertiary small">Last changed {new Date(ops.updatedAtUtc).toLocaleString()}{ops.updatedBy ? ` by ${ops.updatedBy}` : ''}.</small>}
  </section>
}

/**
 * Live tuning. Values here take effect on the next request, without a restart, because the services
 * read configuration per scope. Table-shaped settings (storage levels, lab tiers) stay in appsettings.
 */
function AdminConfigPanel({ busy }: { busy: boolean }) {
  const [config, setConfig] = useState<AdminConfig | null>(null)
  const [filter, setFilter] = useState('')
  const [edits, setEdits] = useState<Record<string, string>>({})
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [working, setWorking] = useState(false)
  const [showAll, setShowAll] = useState(false)

  const load = async () => {
    try { setConfig(await configApi.get()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const run = async (label: string, fn: () => Promise<ActionResult>) => {
    if (reason.trim().length < 3) {
      setError('Give a reason first. Tuning changes are audited.')
      return
    }
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await fn()
      setMessage(`${label}: ${result.summary}`)
      setEdits({})
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  if (!config) return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Tuning</h2><span>Live config</span></div>
    {error ? <div className="alert alert-danger"><span>{error}</span></div> : <p className="text-body-tertiary small mt-3 mb-0">Loading.</p>}
  </section>

  const needle = filter.trim().toLowerCase()
  const matches = config.settings.filter(entry =>
    (!showAll ? entry.isOverridden || needle.length > 0 : true)
    && (needle.length === 0 || entry.path.toLowerCase().includes(needle)))
  const locked = busy || working

  return <section className="card p-3 gcol-full">
    <div className="panel-title">
      <h2>Tuning</h2>
      <span>{config.overrideCount} override{config.overrideCount === 1 ? '' : 's'} live</span>
    </div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {message && <div className="alert alert-success"><span>{message}</span></div>}
    <p>Changes apply on the next request, no restart. Overrides are stored in the database and layered over appsettings, so clearing one falls back to the shipped value. Table-shaped settings like storage levels are not editable here.</p>

    <label className="field">Reason (recorded in the audit trail)
      <input className="form-control" value={reason} onChange={e => setReason(e.target.value)} placeholder="Why are you retuning this?" />
    </label>

    <div className="control-row">
      <label className="grow">Filter<input className="form-control" value={filter} onChange={e => setFilter(e.target.value)} placeholder="combat, morale, price..." /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING} onClick={() => setShowAll(value => !value)}>
        {showAll ? 'Show overrides only' : `Show all ${config.settings.length}`}
      </Button>
    </div>

    <div className="d-grid gap-1 mt-3 config-list">
      {matches.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">
        {showAll ? 'Nothing matches that filter.' : 'No overrides yet. Filter or show all to change something.'}
      </p>}
      {matches.map(entry => <ConfigRow
        key={entry.path}
        entry={entry}
        draft={edits[entry.path] ?? entry.effectiveValue}
        locked={locked}
        onDraft={value => setEdits(current => ({ ...current, [entry.path]: value }))}
        onSave={() => void run('Set', () => configApi.set(entry.path, edits[entry.path] ?? entry.effectiveValue, reason))}
        onClear={() => void run('Cleared', () => configApi.clear(entry.path, reason))}
      />)}
    </div>
  </section>
}

function ConfigRow({ entry, draft, locked, onDraft, onSave, onClear }: {
  entry: AdminConfigEntry
  draft: string
  locked: boolean
  onDraft: (value: string) => void
  onSave: () => void
  onClear: () => void
}) {
  const dirty = draft.trim() !== entry.effectiveValue.trim()
  return <div className={`config-row d-grid gap-2 align-items-center border-top py-2 ${entry.isOverridden ? 'border-primary' : ''}`}>
    <div className="config-copy d-grid gap-1 min-w-0">
      <strong>{entry.path}</strong>
      <span>{entry.type}{entry.isOverridden ? ' / overridden' : ' / from appsettings'}</span>
    </div>
    <input className="form-control" value={draft} onChange={e => onDraft(e.target.value)} />
    <Button className="btn btn-primary btn-sm" blocked={firstReason(
      locked && WORKING,
      !dirty && 'Nothing has been changed here.',
    )} onClick={onSave}>Save</Button>
    <Button className="btn btn-secondary btn-sm" blocked={firstReason(
      locked && WORKING,
      !entry.isOverridden && 'This one is still the value from appsettings. There is no override to reset.',
    )} onClick={onClear}>Reset</Button>
  </div>
}

function AdminOversightPanel({ busy }: { busy: boolean }) {
  const [data, setData] = useState<AdminOversight | null>(null)
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async () => {
    try { setData(await opsApi.oversight()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const resolve = async (missionId: number) => {
    setWorking(true); setError('')
    try { await opsApi.forceResolve(missionId); await load() }
    catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  if (!data) return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Oversight</h2><span>Economy and combat</span></div>
    {error ? <div className="alert alert-danger"><span>{error}</span></div> : <p className="text-body-tertiary small mt-3 mb-0">Loading.</p>}
  </section>

  const overdue = data.activeMissions.filter(mission => mission.isOverdue)
  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Oversight</h2><span>Economy and combat</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
      <AdminMetric label="Median net worth" value={money.format(data.medianNetWorth)} />
      <AdminMetric label="Richest" value={money.format(data.topNetWorth)} />
      <AdminMetric label="Concentration" value={`${data.giniPercent.toFixed(1)}% Gini`} />
      <AdminMetric label="Active missions" value={number.format(data.activeMissions.length)} />
      <AdminMetric label="Stuck missions" value={number.format(overdue.length)} />
    </div>

    <div className="control-block">
      <strong>Wealth spread</strong>
      <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
        {data.wealthBands.map(band => <AdminMetric key={band.label} label={band.label} value={`${number.format(band.players)} / ${money.format(band.totalNetWorth)}`} />)}
      </div>
      <small>Gini runs 0 (everyone equal) to 100 (one player holds everything).</small>
    </div>

    <div className="control-block">
      <strong>Fastest movers, last 24h</strong>
      <div className="d-grid gap-1">
        {data.fastestMovers.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No logged activity in the last day.</p>}
        {data.fastestMovers.map(mover => <div className="audit-row d-grid gap-1 border-top py-2" key={mover.playerId}>
          <div>
            <strong>{mover.name}{mover.isBot ? ' (AI)' : ''}</strong>
            <span>{money.format(mover.cashGained24h)} in {number.format(mover.actionsLast24h)} actions</span>
          </div>
          <p>Net worth {money.format(mover.netWorth)}</p>
        </div>)}
      </div>
      <small>Approximated from logged cash and bank deltas; the game keeps no net worth history to diff.</small>
    </div>

    <div className="control-block">
      <strong>In-flight missions</strong>
      <div className="d-grid gap-1">
        {data.activeMissions.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nothing in flight.</p>}
        {data.activeMissions.map(mission => <div className={`audit-row d-grid gap-1 border-top py-2 ${mission.isOverdue ? 'border-primary' : ''}`} key={mission.missionId}>
          <div>
            <strong>{mission.status}{mission.isOverdue ? ' / STUCK' : ''}</strong>
            <span>round {mission.currentRound}/{mission.maxRounds}</span>
          </div>
          <p>{mission.commanderName ?? 'A pimp'} ({mission.attackerName}) vs {mission.defenderName}</p>
          <div className="control-row">
            <em>{mission.nextEventAtUtc ? `next ${new Date(mission.nextEventAtUtc).toLocaleTimeString()}` : 'no timer'}</em>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(busy && WORKING, working && WORKING)}
              onClick={() => void resolve(mission.missionId)}>Force resolve</Button>
          </div>
        </div>)}
      </div>
    </div>

    <div className="control-block">
      <strong>AI health</strong>
      <div className="d-grid gap-1">
        {data.bots.map(bot => <div className="audit-row d-grid gap-1 border-top py-2" key={bot.playerId}>
          <div>
            <strong>{bot.name}</strong>
            <span>{bot.personality}</span>
          </div>
          <p>{money.format(bot.netWorth)} / {botPresence(bot)}</p>
        </div>)}
      </div>
    </div>
  </section>
}

/**
 * Player administration. Owns its own state and talks to the admin API directly rather than threading
 * a dozen fields through PageContext, matching how AdminPanel already handles its local controls.
 */
function AdminPlayersPanel({ busy, onChanged }: { busy: boolean, onChanged: () => void }) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<AdminPlayerSummary[]>([])
  const [detail, setDetail] = useState<AdminPlayerDetail | null>(null)
  const [reason, setReason] = useState('')
  const [resource, setResource] = useState('cash')
  const [delta, setDelta] = useState(10000)
  const [renameTo, setRenameTo] = useState('')
  const [suspendHours, setSuspendHours] = useState(24)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const search = async (event?: FormEvent<HTMLFormElement>) => {
    event?.preventDefault()
    setError('')
    try {
      setResults(await adminApi.searchPlayers(query))
    } catch (e) { setError((e as Error).message) }
  }

  const open = async (playerId: string) => {
    setError(''); setMessage('')
    try {
      const next = await adminApi.playerDetail(playerId)
      setDetail(next)
      setRenameTo(next.summary.name)
    } catch (e) { setError((e as Error).message) }
  }

  // Every mutation needs a reason: it is what makes the audit trail worth having.
  const run = async (label: string, fn: () => Promise<ActionResult>, requireReason = true) => {
    if (requireReason && reason.trim().length < 3) {
      setError('Give a reason first. It goes in the audit trail.')
      return
    }
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await fn()
      setMessage(`${label}: ${result.summary}`)
      if (detail) await open(detail.summary.playerId)
      await search()
      onChanged()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  useEffect(() => { void search() }, [])
  const locked = busy || working
  const target = detail?.summary

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Players</h2><span>Find and fix</span></div>
    <form className="d-grid gtc-1 gtc-md-1-auto gap-2 align-items-end mb-3" onSubmit={search}>
      <label className="field">Search<input className="form-control" value={query} onChange={e => setQuery(e.target.value)} placeholder="Player, username, or city" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}>Search</Button>
    </form>

    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {message && <div className="alert alert-success"><span>{message}</span></div>}

    <div className="d-grid gtc-1 gtc-lg-split-280 gap-3 mt-3">
      <div className="admin-player-list d-grid gap-1 align-content-start overflow-y-auto">
        {results.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No players matched.</p>}
        {results.map(player => <Button
          className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${target?.playerId === player.playerId ? 'active border-primary' : ''}`}
          key={player.playerId}
          type="button"
          blocked={locked && WORKING}
          onClick={() => void open(player.playerId)}
        >
          <strong>{player.name}</strong>
          <small>
            {player.username}{player.isBot ? ' / AI' : ''}{player.isAdmin ? ' / admin' : ''}
            {/* Marked in the list, so a search that matched on identity shows why without a click. */}
            {player.discordUsername && ` / ${player.discordUsername}`}
            {player.emailVerified && ' / ✉'}
          </small>
          <em>{enforcementLabel(player)}</em>
          <b>{money.format(player.netWorth)}</b>
        </Button>)}
      </div>

      {detail && target && <div className="d-grid gap-3 align-content-start border rounded bg-body-tertiary p-3">
        <div className="d-flex justify-content-between align-items-start gap-3">
          <div className="d-grid gap-1">
            <strong className="text-body fs-6">{target.name}</strong>
            <span className="text-body-secondary small">{target.username} / {target.city}</span>
          </div>
          <b className={`badge ${target.isBanned ? 'text-bg-danger' : 'text-bg-success'}`}>{enforcementLabel(target)}</b>
        </div>
        <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
          <AdminMetric label="Net worth" value={money.format(target.netWorth)} />
          <AdminMetric label="Cash" value={money.format(target.cash)} />
          <AdminMetric label="Bank" value={money.format(target.bankCash)} />
          <AdminMetric label="Turns" value={number.format(target.turns)} />
          <AdminMetric label="Crew" value={`${target.pimps} P / ${target.hoes} H / ${target.thugs} T`} />
          <AdminMetric label="Morale" value={`${detail.hoeHappiness.toFixed(0)}% / ${detail.thugHappiness.toFixed(0)}%`} />
          <AdminMetric label="Hideout" value={`${detail.hideout.tierName} S${detail.hideout.storageLevel}/V${detail.hideout.safeLevel}`} />
          <AdminMetric label="Joined" value={new Date(target.createdAtUtc).toLocaleDateString()} />
        </div>

        {/*
          Who this account actually is, rather than what it owns.

          A moderator handling a returning ban evader is asking one question - is this the same person -
          and the panel could not previously answer it at all. A username is the first thing somebody
          changes on the way to a second account; a Discord snowflake is the last, which is why it is
          shown as well as the handle and why both are searchable.
        */}
        <div className="tnum d-grid gtc-1 gtc-md-3 gap-2">
          <AdminMetric
            label="Email"
            value={target.email ?? '—'}
            sub={target.email ? (target.emailVerified ? 'Confirmed' : 'Not confirmed') : 'None set'}
          />
          <AdminMetric label="Discord" value={target.discordUsername ?? '—'} sub={target.discordUsername ? 'Connected' : 'Not connected'} />
          <AdminMetric label="Discord ID" value={target.discordUserId ?? '—'} sub="Survives a rename" />
        </div>

        <label className="field">Reason (recorded in the audit trail)
          <input className="form-control" value={reason} onChange={e => setReason(e.target.value)} placeholder="Why are you doing this?" />
        </label>

        <div className="control-block">
          <strong>Quick grants</strong>
          <div className="d-grid gtc-1 gtc-md-4 gap-2">
            {adjustPresets.map(preset => <Button
              className="btn btn-secondary btn-sm"
              key={preset.label}
              blocked={locked && WORKING}
              onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, preset.resource, preset.delta, reason))}
            >{preset.label}</Button>)}
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Morale set', () => adminApi.setMorale(target.playerId, 100, reason))}>Morale 100%</Button>
          </div>
        </div>

        <div className="control-block">
          <strong>Adjust a resource</strong>
          <div className="control-row">
            <label className="field">Resource<select className="form-select" value={resource} onChange={e => setResource(e.target.value)}>
              {detail.adjustableResources.map(key => <option key={key} value={key}>{key}</option>)}
            </select></label>
            <label className="field">Change<input className="form-control" type="number" value={delta} onChange={e => setDelta(Number(e.target.value))} /></label>
            <Button className="btn btn-primary btn-sm" blocked={firstReason(
              locked && WORKING,
              delta === 0 && 'A change of zero does nothing. Set an amount first.',
            )} onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, resource, delta, reason))}>
              Apply
            </Button>
          </div>
          <small>Negative values take resources away. Nothing drops below zero.</small>
        </div>

        <div className="control-block">
          <strong>Account</strong>
          <div className="control-row">
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Banned', () => adminApi.enforcement(target.playerId, 'ban', null, reason))}>
              Ban
            </Button>
            <label className="field">Suspend hours<input className="form-control" type="number" min={1} value={suspendHours} onChange={e => setSuspendHours(Number(e.target.value))} /></label>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(
              locked && WORKING,
              suspendHours < 1 && 'A suspension has to run for at least an hour.',
            )} onClick={() => void run('Suspended', () => adminApi.enforcement(
                target.playerId,
                'suspend',
                new Date(Date.now() + suspendHours * 3600_000).toISOString(),
                reason))}>
              Suspend
            </Button>
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Cleared', () => adminApi.enforcement(target.playerId, 'clear', null, reason))}>
              Lift
            </Button>
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Logged out', () => adminApi.forceLogout(target.playerId, reason))}>
              Force logout
            </Button>
          </div>
        </div>

        <div className="control-block">
          <strong>Identity and rights</strong>
          <div className="control-row">
            <label className="field">Name<input className="form-control" value={renameTo} onChange={e => setRenameTo(e.target.value)} minLength={3} maxLength={32} /></label>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(
              locked && WORKING,
              renameTo.trim() === target.name && `They are already called ${target.name}.`,
            )} onClick={() => void run('Renamed', () => adminApi.rename(target.playerId, renameTo, reason))}>
              Rename
            </Button>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(
              locked && WORKING,
              target.isBot && 'A rival run by the game cannot be given admin rights.',
            )} onClick={() => void run('Rights changed', () => adminApi.setAdminRights(target.playerId, !target.isAdmin, reason))}>
              {target.isAdmin ? 'Revoke admin' : 'Grant admin'}
            </Button>
          </div>
        </div>

        <div className="control-block">
          <strong>Recent activity</strong>
          <ActivityList entries={detail.recentActivity.slice(0, 6)} />
        </div>

        {detail.auditTrail.length > 0 && <div className="control-block">
          <strong>Admin history for this player</strong>
          <AuditList entries={detail.auditTrail} />
        </div>}
      </div>}
    </div>
  </section>
}

function AdminAuditPanel() {
  const [entries, setEntries] = useState<AdminAuditEntry[]>([])
  const [error, setError] = useState('')
  useEffect(() => {
    void (async () => {
      try { setEntries(await adminApi.audit()) } catch (e) { setError((e as Error).message) }
    })()
  }, [])

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Audit Trail</h2><span>Every admin action</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {entries.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No admin actions recorded yet.</p>}
    <AuditList entries={entries.slice(0, 30)} />
  </section>
}

function AuditList({ entries }: { entries: AdminAuditEntry[] }) {
  return <div className="d-grid gap-1">
    {entries.map(entry => <div className="audit-row d-grid gap-1 border-top py-2" key={entry.id}>
      <div>
        <strong>{entry.action}</strong>
        <span>{entry.actorUsername}{entry.targetName ? ` -> ${entry.targetName}` : ''}</span>
      </div>
      <p>{entry.summary}</p>
      {entry.reason && <small>"{entry.reason}"</small>}
      <em>{new Date(entry.createdAtUtc).toLocaleString()}</em>
    </div>)}
  </div>
}

function enforcementLabel(player: AdminPlayerSummary) {
  if (player.isBanned) return 'Banned'
  if (player.suspendedUntilUtc && new Date(player.suspendedUntilUtc) > new Date()) return 'Suspended'
  return 'Active'
}

/**
 * Defence alerts. Opening the panel marks everything read by moving the server-side watermark, then
 * refreshes so the badge clears. The count itself rides on the dashboard, so the bell costs no extra
 * request until it is opened.
 */
// Shown once on arrival and only when something actually happened. A popup that says the world stood
// still while you were out is an interruption with nothing behind it.
function CatchUpDialog({ news, onClose }: { news: CatchUp, onClose: () => void }) {
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const away = news.awayMinutes < 60
    ? `${news.awayMinutes} minute${news.awayMinutes === 1 ? '' : 's'}`
    : `${Math.floor(news.awayMinutes / 60)} hour${Math.floor(news.awayMinutes / 60) === 1 ? '' : 's'}`

  /*
    Bootstrap's modal markup, with React holding it open rather than Bootstrap's JS. The classes are
    the real ones — .modal-dialog centres and sizes it, .modal-content paints it, .modal-backdrop
    dims behind — so it inherits the component's layout and z-index without a second implementation
    of either. What is deliberately not used is the Modal plugin: it wants to own show and hide, and
    this dialog's visibility is a piece of React state that the rest of the app reads.
  */
  return <>
    <div className="modal-backdrop show" />
    <div className="modal d-block" role="dialog" aria-modal="true" aria-label="While you were away" onClick={onClose}>
      <div className="modal-dialog modal-dialog-centered" onClick={event => event.stopPropagation()}>
        <div className="modal-content p-3">
          <div className="d-grid gap-1 mb-3">
            <h2 className="m-0 text-body fs-5">While you were away</h2>
            <span className="text-body-tertiary small">{away} since you last looked in</span>
          </div>
          <div className="d-grid gap-2 mb-4">
            {news.items.map((item, index) => <div
              className={`d-grid gap-1 border rounded bg-body-tertiary p-3 border-start-thick ${item.tone === 'good' ? 'border-start-success' : item.tone === 'bad' ? 'border-start-danger' : 'border-start-secondary'}`}
              key={`${item.kind}-${index}`}
            >
              <strong className="text-body">{item.headline}</strong>
              <span className="text-body-secondary small lh-sm">{item.detail}</span>
            </div>)}
          </div>
          <button className="btn btn-primary w-100" onClick={onClose}>Back to work</button>
        </div>
      </div>
    </div>
  </>
}

function AlertBell({ unread, onRead }: { unread: number, onRead: () => void }) {
  const [open, setOpen] = useState(false)
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [error, setError] = useState('')

  const toggle = async () => {
    if (open) {
      setOpen(false)
      return
    }
    setOpen(true)
    setError('')
    try {
      const loaded = await api.alerts()
      setAlerts(loaded.alerts)
      if (loaded.unreadCount > 0) {
        await api.markAlertsSeen()
        onRead()
      }
    } catch (e) { setError((e as Error).message) }
  }

  return <div className="alert-bell position-relative d-flex align-items-stretch">
    <button
      className={`btn position-relative d-flex align-items-center px-3 ${unread > 0 ? 'btn-outline-warning' : 'btn-outline-secondary'}`}
      type="button"
      onClick={() => void toggle()}
      aria-expanded={open}
      title={unread > 0 ? `${unread} unread alert${unread === 1 ? '' : 's'}` : 'Alerts'}
    >
      {/* A filled bell when something is waiting, so the state reads without the count. */}
      <i className={`bi ${unread > 0 ? 'bi-bell-fill' : 'bi-bell'} fs-5`} aria-hidden="true" />
      <span className="visually-hidden">Alerts</span>
      {unread > 0 && <b className="badge rounded-pill bg-danger">
        {unread > 99 ? '99+' : unread}
        <span className="visually-hidden"> unread</span>
      </b>}
    </button>
    {open && <div className="alert-panel position-absolute d-grid gap-1 border rounded bg-body-secondary p-3 overflow-y-auto">
      <div className="d-flex justify-content-between align-items-center gap-2">
        <strong className="text-body">Alerts</strong>
        <button className="btn-close" type="button" aria-label="Close alerts" onClick={() => setOpen(false)} />
      </div>
      {error && <p className="text-body-tertiary small mt-3 mb-0">{error}</p>}
      {!error && alerts.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nothing has happened to you yet.</p>}
      {alerts.map(alert => <div className={`d-grid gap-1 border rounded p-2 border-start-thick ${alertClass(alert)}`} key={alert.id}>
        <strong className="text-body">{alert.headline}</strong>
        <span className="small">{alert.detail}</span>
        <small className="text-body-tertiary small">{new Date(alert.createdAtUtc).toLocaleString()}</small>
      </div>)}
    </div>}
  </div>
}

/**
 * What a rival is doing right now. Idle minutes stopped meaning anything on their own once rivals
 * played in sessions: one quiet for four hours is asleep, not stuck, and the admin needs to be able
 * to tell those apart at a glance.
 */
function botPresence(bot: AdminBotHealth) {
  if (bot.isInSession) return `playing, ${number.format(bot.sessionActionsLeft)} left`
  if (!bot.nextSessionAtUtc) return 'due to play'
  const minutes = Math.round((new Date(bot.nextSessionAtUtc).getTime() - Date.now()) / 60000)
  if (minutes <= 0) return 'due to play'
  return minutes < 90 ? `back in ${minutes}m` : `back in ${Math.round(minutes / 60)}h`
}

// Only a rival that is meant to be playing and is not counts as stale, so a sleeper is not flagged.
function rivalRowClass(bot: AdminBotHealth) {
  if (bot.isPaused) return 'paused'
  return bot.isInSession && bot.minutesIdle > 30 ? 'stale' : ''
}

function alertClass(alert: Alert) {
  // The stripe says what happened, the fill says whether you have seen it.
  const base = alert.tone === 'bad' ? 'border-start-danger' : 'border-start-success'
  return alert.isUnread ? `${base} bg-body-secondary` : base
}

function StatusStrip({ dashboard, nextTurn }: { dashboard: Dashboard, nextTurn: string }) {
  return <section className="status-strip tnum d-grid gap-2 mb-3" data-area="status">
    <Stat label="Cash" value={money.format(dashboard.cash)} />
    <Stat label="Bank" value={money.format(dashboard.bankCash)} />
    <Stat label="Net Worth" value={money.format(dashboard.netWorth)} />
    <Stat label="Turns" value={`${dashboard.turns} / ${dashboard.maxTurns}`} sub={nextTurn === 'MAX' ? 'Turn bank full' : `+${dashboard.turnsPerTick} in ${nextTurn}`} />
    <Stat
      label="Upkeep"
      value={hourlyUpkeepShort(dashboard)}
      sub="Per hour"
      tone={hourlyUpkeepWarn(dashboard) ? 'border-warning' : undefined}
      title={hourlyUpkeepLabel(dashboard)}
    />
    <Stat
      label="Heat"
      value={dashboard.hideout.heatLabel}
      sub={dashboard.hideout.heatDetail}
      tone={`heat-${dashboard.hideout.heatLabel.toLowerCase()}`}
      title={dashboard.hideout.heatNote}
    />
    <Stat label="Rank" value={`#${dashboard.rank}`} />
    <Stat label="City" value={dashboard.city} />
  </section>
}

function hourlyUpkeepLabel(dashboard: Dashboard) {
  const report = dashboard.crewReport
  return `${number.format(report.condomsNeededPerHour)} condoms / ${number.format(report.beerNeededPerHour)} beer / ${number.format(report.drugsNeededPerHour)} drugs`
}

function hourlyUpkeepShort(dashboard: Dashboard) {
  const report = dashboard.crewReport
  return `${number.format(report.condomsNeededPerHour)}C / ${number.format(report.beerNeededPerHour)}B / ${number.format(report.drugsNeededPerHour)}D`
}

function hourlyUpkeepWarn(dashboard: Dashboard) {
  const report = dashboard.crewReport
  return dashboard.condoms < report.condomsNeededPerHour
    || dashboard.beer + dashboard.moonshine < report.beerNeededPerHour
    || dashboard.weed + dashboard.coke < report.drugsNeededPerHour
}

function heatAmount(value: number) {
  if (value > 0 && value < 0.1) return '<0.1'
  return value.toFixed(value < 10 && value % 1 !== 0 ? 1 : 0)
}

function crewHeatLabel(report: CrewReport) {
  return `P ${heatAmount(report.pimpHeat)} / H ${heatAmount(report.hoeHeat)} / T ${heatAmount(report.thugHeat)}`
}

function selectedDistrict(dashboard: Dashboard, district: string) {
  return dashboard.districts.find(entry => entry.key === district)
    ?? dashboard.districts.find(entry => entry.isDefault)
    ?? dashboard.districts[0]
}

function streetHeatFor(dashboard: Dashboard, turns: number, district: string) {
  return (selectedDistrict(dashboard, district)?.heatPerTurn ?? 0) * streetTurnCount(dashboard, turns)
}

function streetTurnLimit(dashboard: Dashboard) {
  return Math.max(0, Math.min(dashboard.turns, dashboard.crewReport.suppliedStreetActionTurns))
}

function streetTurnCount(dashboard: Dashboard, turns: number) {
  const limit = streetTurnLimit(dashboard)
  if (limit < 1) return 0
  return Math.max(1, Math.min(turns, limit))
}

/** Upkeep a street action of this length burns, scaled from the server's max-action figures. */
function upkeepFor(dashboard: Dashboard, turns: number) {
  const planned = streetTurnCount(dashboard, turns)
  const scale = (maxActionNeed: number) => Math.ceil(maxActionNeed * planned / dashboard.maxActionTurns)
  return {
    planned,
    condoms: scale(dashboard.crewReport.condomsNeededForMaxStreetAction),
    beer: scale(dashboard.crewReport.beerNeededForMaxStreetAction),
  }
}

/**
 * Mirrors the server's auto-buy: shortfall, limited by storage room and by cash. Only an estimate for
 * display; the API does the real arithmetic and is the authority on what gets spent.
 */
function restockEstimate(dashboard: Dashboard, turns: number) {
  const upkeep = upkeepFor(dashboard, turns)
  const price = (key: string) => dashboard.store.find(item => item.key === key)?.price ?? 0
  const wanted = (need: number, held: number, cap: number) => Math.max(0, Math.min(need - held, cap - held))
  const condoms = wanted(upkeep.condoms, dashboard.condoms, dashboard.hideout.maxCondoms)
  const beer = wanted(upkeep.beer, dashboard.beer, dashboard.hideout.maxBeer)
  const cost = condoms * price('condoms') + beer * price('beer')
  return { condoms, beer, cost }
}

function restockLabel(restock: { condoms: number, beer: number, cost: number }, cash: number) {
  if (restock.condoms === 0 && restock.beer === 0)
    return 'Nothing to buy. The crew is already carrying what this needs.'
  const parts: string[] = []
  if (restock.condoms > 0) parts.push(`${number.format(restock.condoms)} condoms`)
  if (restock.beer > 0) parts.push(`${number.format(restock.beer)} beer`)
  const short = cash < restock.cost ? ' Your cash covers only part of it, so it buys what it can.' : ''
  return `Buys ${parts.join(' and ')} for about ${money.format(restock.cost)}.${short}`
}

function StreetSupplyPanel({ dashboard, busy, streetTurns, storeQty, setStoreQty, act, onMarket }: {
  dashboard: Dashboard
  busy: boolean
  streetTurns: number
  storeQty: Record<string, number>
  setStoreQty: React.Dispatch<React.SetStateAction<Record<string, number>>>
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  onMarket: () => void
}) {
  const upkeep = upkeepFor(dashboard, streetTurns)
  const plannedTurns = upkeep.planned
  const turnLabel = `${plannedTurns} turn${plannedTurns === 1 ? '' : 's'}`
  const catalog = new Map(dashboard.store.map(item => [item.key, item] as const))
  const hideout = dashboard.hideout
  const supplies: {
    key: string
    owned: number
    cap: number
    needed: number
    basis: string
    /** Held stock of something else that covers the same need. Not buyable here, so it never reaches the qty box. */
    covered?: number
    coveredLabel?: string
  }[] = [
    { key: 'condoms', owned: dashboard.condoms, cap: hideout.maxCondoms, needed: upkeep.condoms, basis: `to work ${turnLabel}` },
    // Thugs drink the still dry once the bought beer is gone, so moonshine already distilled covers
    // part of the need. Counted here and nowhere near the buy control, which stays about beer: the
    // counter does not sell moonshine, and the beer shelf is the only room a purchase can go into.
    { key: 'beer', owned: dashboard.beer, cap: hideout.maxBeer, needed: upkeep.beer, basis: `to work ${turnLabel}`, covered: dashboard.moonshine, coveredLabel: 'moonshine' },
    // Guns are permanent cover rather than something a shift burns through, so the number to reach is
    // the crew rather than the turns. Pistols specifically, because the counter stopped selling a thing
    // called "weapons" the day guns split into tiers: this row asked for a key that no longer existed
    // and the filter below quietly dropped it, so the panel has been two rows ever since.
    //
    // What is held is the whole rack, not the pistols on it - any gun covers a thug, and the shelf is
    // shared - but the pistol is what you buy to close the gap, being the cheapest thing that counts.
    { key: cheapestWeapon, owned: dashboard.weapons, cap: hideout.maxWeapons, needed: dashboard.thugs, basis: 'to arm every thug' },
  ].filter(supply => catalog.has(supply.key))
  // A row whose key the counter does not stock is a bug, not an empty state. Named here so the next
  // one is a message in the console rather than a row nobody notices is gone.
  if (supplies.length < 3 && import.meta.env.DEV)
    console.warn('Supplies panel dropped a row: the store has no', [
      'condoms', 'beer', cheapestWeapon,
    ].filter(key => !catalog.has(key)).join(', '))
  if (supplies.length === 0) return null

  return <div className="tnum d-grid gap-2 my-3 border rounded bg-body-secondary p-3" data-area="supplies">
    <div className="d-flex flex-column flex-md-row justify-content-between align-items-stretch align-items-md-center gap-3">
      <div className="d-grid gap-1">
        <strong className="text-body">Supplies</strong>
        <span className="eyebrow">Checked against {turnLabel}</span>
      </div>
      <button className="btn btn-primary" type="button" onClick={onMarket}>Open Business</button>
    </div>
    <div className="d-grid gtc-1 gtc-sm-2 gtc-md-3 gap-2">
      {supplies.map(supply => {
        const item = catalog.get(supply.key)!
        const covered = supply.covered ?? 0
        const short = Math.max(0, supply.needed - supply.owned - covered)
        // The storage room refuses buys that do not fit, so never offer more than the room left.
        const room = Math.max(0, supply.cap - supply.owned)
        const qty = Math.min(storeQty[supply.key] ?? Math.max(1, short), Math.max(1, room))
        const total = qty * item.price
        return <div className={`d-grid gtc-1-auto gap-2 align-content-start border rounded p-3 ${short > 0 ? 'border-warning' : 'bg-body-tertiary'}`} key={supply.key}>
          <div className="d-grid gap-1">
            <strong className="text-body">{item.name}</strong>
            <span className={`small ${short > 0 ? 'text-primary' : 'text-body-secondary'}`}>
              {number.format(supply.owned)} on hand
              {covered > 0 && ` + ${number.format(covered)} ${supply.coveredLabel ?? ''}`}
              {' / '}{number.format(supply.needed)} {supply.basis} / {number.format(supply.cap)} storage
            </span>
          </div>
          <em className={`eyebrow fst-normal align-self-start justify-self-end text-nowrap ${short > 0 ? 'text-primary' : 'text-body-tertiary'}`}>
            {room === 0 ? 'Storage full' : short > 0 ? `${number.format(short)} short` : 'Covered'}
          </em>
          <label className="field gcol-full small">Qty<input className="form-control" aria-label={`${item.name} quantity`} type="number" min={1} max={Math.max(1, room)} value={qty} onChange={event => setStoreQty(value => ({ ...value, [supply.key]: Number(event.target.value) }))} /></label>
          <Button
            className="btn btn-primary gcol-full w-100 min-w-0"
            blocked={firstReason(
              busy && BUSY,
              room === 0 && `Your store already holds ${number.format(supply.cap)} ${item.name.toLowerCase()}, which is all it has room for.`,
              qty < 1 && 'Buy at least one.',
              qty > room && `There is room for ${number.format(room)} more and you are buying ${number.format(qty)}.`,
              dashboard.cash < total && `That comes to ${money.format(total)} and you are carrying ${money.format(dashboard.cash)}.`,
            )}
            onClick={() => void act(() => api.buyStoreItem(supply.key, qty))}
          >
            {room === 0 ? 'Storage Full' : `Buy ${money.format(total)}`}
          </Button>
        </div>
      })}
    </div>
    <div className="d-flex flex-column flex-sm-row justify-content-between align-items-start align-items-sm-baseline gap-1 gap-sm-3 border-top pt-2">
      <span className="text-body-tertiary small">Street work also turns up product.</span>
      <small className="text-body-secondary small">Carrying {number.format(dashboard.weed)} weed / {number.format(dashboard.coke)} coke</small>
    </div>
  </div>
}

/**
 * What to do next, ranked by the server against the state the player is actually in.
 *
 * This used to be four fixed rows - turns, crew pressure, supplies, combat posture - that read the
 * same on day one and day one hundred and never named a move. A new player finished their whole
 * first session having clicked one button five times, with the best purchase available to them
 * sitting unmentioned in a room they had no reason to open.
 */
function NextMovePanel({ dashboard, onPage }: { dashboard: Dashboard, onPage: GoTo }) {
  const moves = dashboard.guidance?.moves ?? []
  if (moves.length === 0) return null

  return <section className="card p-3">
    <div className="panel-title" data-area="next-moves"><h2>Next Moves</h2><span>Worth doing now</span></div>
    <div className="d-grid gap-2">
      {moves.map(move => <button
        className={`w-100 d-grid gap-1 text-start border rounded p-3 ${move.urgent ? 'border-warning bg-body-tertiary' : 'bg-body-secondary'}`}
        type="button"
        key={move.label}
        onClick={() => goToFlow(onPage, move.page)}
      >
        {/* Advice carries a price, so the cost sits with the label rather than buried in the reason. */}
        <strong className={move.urgent ? 'text-primary' : 'text-body'}>
          {move.label}{move.cost > 0 && <b className="float-end text-primary fw-bold">{money.format(move.cost)}</b>}
        </strong>
        <span className="text-body-secondary lh-sm">{move.why}</span>
      </button>)}
    </div>
  </section>
}

/**
 * The opening ladder, and the only place the game explains itself.
 *
 * Hidden once it is finished rather than kept forever: a checklist a veteran still has to scroll past
 * is clutter, and the whole point of it is to stop being needed.
 */
function OpeningLadderPanel({ dashboard, onPage }: {
  dashboard: Dashboard
  onPage: GoTo
}) {
  const guidance = dashboard.guidance
  if (!guidance || guidance.objectivesDone >= guidance.objectivesTotal) return null
  // The next unfinished rung, plus what has been done, so progress is visible without listing it all.
  const next = guidance.objectives.find(o => !o.done)

  return <section className="card p-3" data-area="ladder">
    <div className="panel-title">
      <h2>Getting Started</h2>
      <div className="d-flex align-items-center gap-2">
        <span>{guidance.objectivesDone} of {guidance.objectivesTotal}</span>
      </div>
    </div>
    <div className="d-grid gap-1">
      {guidance.objectives.map(step => <button
        className={`ladder-row d-grid gap-2 align-items-start text-start border rounded-2 p-2 ${step.done ? 'done' : step === next ? 'next bg-body-secondary text-body' : 'border-0 bg-transparent'}`}
        type="button"
        key={step.label}
        onClick={() => goToFlow(onPage, step.page)}
      >
        <em className="fst-normal text-success-emphasis">{step.done ? '✓' : ''}</em>
        <div>
          <strong className={` ${step === next ? 'fw-bold' : 'fw-normal'}`}>{step.label}</strong>
          {step === next && <span className="d-block mt-1 text-body-secondary small">{step.why}</span>}
        </div>
      </button>)}
    </div>
  </section>
}

function PimpRosterPanel({ dashboard }: { dashboard: Dashboard }) {
  const crew = dashboard.crew
  const fallen = dashboard.fallenCrew
  return <section className="card p-3 gcol-full">
    <div className="panel-title" data-area="pimps"><h2>Your Pimps</h2><span>{crew.length}/{dashboard.hideout.maxPimps} on the payroll</span></div>
    <p>Pimps are the only crew you know by name. One of them commands each attack, and loyalty slides when the operation is miserable or a mission goes badly.</p>
    <div className="d-grid gap-2 mt-3">
      {crew.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No pimps left. Hire one before you can run the streets or attack.</p>}
      {crew.map(pimp => <div
        className={`pimp-row d-grid gap-3 align-items-center border rounded px-3 py-2 ${pimp.isCommanding ? 'border-primary bg-body-tertiary' : 'bg-body-tertiary'}`}
        key={pimp.id}
      >
        <div className="d-grid gap-1">
          <strong className="text-body">
            {pimp.name} <b className={`badge ${pimp.specialty === 'Enforcer' ? 'text-bg-warning' : 'text-bg-success'} ms-1`}>{pimp.specialty} +{pimp.bonusPercent}%</b>
          </strong>
          <span className="text-body-secondary small">{pimp.specialty === 'Enforcer' ? 'Sharpens any attack they lead, and the house whenever they are in it' : 'Lifts street income while home'}</span>
          <span className="text-body-secondary small">{pimp.missionsLed === 0 ? 'No missions led yet' : `${number.format(pimp.missionsLed)} mission${pimp.missionsLed === 1 ? '' : 's'} led / ${number.format(pimp.victories)} won`}</span>
        </div>
        <em className={`eyebrow fst-normal ${pimp.isCommanding ? 'text-primary' : 'text-body-tertiary'}`}>{pimp.isCommanding ? 'Out commanding' : 'At the house'}</em>
        <div className="d-grid gap-1 justify-items-end">
          <span className="eyebrow text-body-tertiary">Loyalty</span>
          <strong className={`fs-5 ${moraleTone(pimp.loyalty) === 'danger' ? 'text-danger-emphasis' : moraleTone(pimp.loyalty) === 'warn' ? 'text-primary' : 'text-success-emphasis'}`}>
            {pimp.loyalty.toFixed(0)}%
          </strong>
        </div>
      </div>)}
    </div>
    {fallen.length > 0 && <div className="d-grid gap-2 mt-3 border-top pt-3">
      <strong className="eyebrow">Gone</strong>
      <div className="d-flex flex-wrap gap-2">
        {fallen.map(pimp => <div className="d-grid gap-1 border rounded bg-body-tertiary p-2 pimp-gone" key={pimp.id}>
          <b className="text-body-secondary">{pimp.name}</b>
          <span className="text-warning-emphasis small">{pimp.lostReason}</span>
          <small className="text-body-tertiary small">{pimp.lostAtUtc ? new Date(pimp.lostAtUtc).toLocaleDateString() : ''}</small>
        </div>)}
      </div>
    </div>}
  </section>
}

/** The rack in one line: what is on it, best guns first, or how bare it is. */
function rackSummary(rack: WeaponTier[]) {
  const carried = rack.filter(tier => tier.held > 0).slice().reverse()
  if (carried.length === 0) return 'Unarmed'
  return carried.map(tier => `${number.format(tier.held)} ${tier.label.toLowerCase()}`).join(', ')
}

/**
 * Coverage first, then what the guns are worth. A crew can be fully covered and still be carrying
 * nothing but pistols, and that difference is the whole reason the tiers exist.
 */
function weaponSummary(dashboard: Dashboard) {
  const best = dashboard.weaponRack.filter(tier => tier.held > 0).slice().reverse()[0]
  const covered = `${number.format(dashboard.weapons)}/${number.format(dashboard.thugs)}`
  return best && best.firepower > 1 ? `${covered} (${best.label.toLowerCase()})` : covered
}

/**
 * Where to work the shift.
 *
 * Every district states what it is for outright rather than leaving it to be discovered. The source
 * game had these five and its own guide never worked out whether they differed at all - which is what
 * happens when a choice is offered without being explained.
 */
function DistrictPicker({ districts, selected, onSelect }: {
  districts: StreetDistrict[]
  selected: string
  onSelect: (district: string) => void
}) {
  if (districts.length === 0) return null
  const active = selected || districts.find(x => x.isDefault)?.key || districts[0].key

  return <div className="d-grid gtc-fill-140 gap-2 mb-3 mt-3">
    {districts.map(entry => <button
      className={`tile d-grid gap-1 text-start border rounded p-2 ${entry.key === active ? 'active border-primary' : 'bg-body-tertiary'}`}
      key={entry.key}
      type="button"
      title={entry.blurb}
      onClick={() => onSelect(entry.key)}
    >
      <strong className="text-body">{entry.name}</strong>
      <small className="text-body-tertiary small">{districtEdge(entry)}</small>
    </button>)}
  </div>
}

/**
 * A district in one line. The full numbers are useful for tuning, but too loud in the picker; this
 * keeps the visible choice to the role and the heat feel.
 */
function districtEdge(district: StreetDistrict) {
  const roles: Record<string, string> = {
    casino: 'Cash focus',
    winos: 'Thug recruits',
    lowrent: 'Balanced',
    nightclub: 'Pimp network',
    ghetto: 'Street finds',
  }
  const claims: { label: string, value: number }[] = [
    { label: 'Cash focus', value: district.grossPercent },
    { label: 'Hoe recruits', value: district.hoeRecruitPercent },
    { label: 'Thug recruits', value: district.thugRecruitPercent },
    { label: 'Pimp network', value: district.pimpRecruitPercent },
    { label: 'Street finds', value: district.findPercent },
  ]
  const best = claims.reduce((a, b) => (b.value > a.value ? b : a))
  const worst = claims.reduce((a, b) => (b.value < a.value ? b : a))
  const role = roles[district.key] ?? (best.value <= 100 && worst.value >= 100 ? 'Balanced' : best.label)

  const heat = district.heatPercent >= 150
    ? 'Hot'
    : district.heatPercent > 100
      ? 'Warm'
      : district.heatPercent <= 60
        ? 'Quiet'
        : ''
  return heat && role !== 'Balanced' ? `${role} / ${heat}` : role
}

function moraleTone(value: number) {
  if (value < 30) return 'danger'
  if (value < 60) return 'warn'
  return 'good'
}

function HideoutMoralePanel({ dashboard, busy, act }: {
  dashboard: Dashboard
  busy: boolean
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
}) {
  const report = dashboard.crewReport
  const moraleFull = dashboard.hoeHappiness >= 100 && dashboard.thugHappiness >= 100

  /*
    Why the button is grey, in the button.

    Both of these were `disabled` and silent, which is the one thing this game says it does not do to
    a player - and the state they most often land in is the one that hides the answer best: buy the
    building, and the price comes out of the bank and then out of your pocket, so the crew needs
    steadying on the day there is nothing in hand to steady them with. A player in that position sees
    two dead buttons and no way at all to find out that the money is the problem, or that the money
    being in the bank is why it is a problem.

    The order is the order the server checks in, so the reason shown is the reason it would refuse
    with, and cash names where it has to be.
  */
  const restReason = moraleFull
    ? 'Your crew are already steady.'
    : dashboard.turns < report.hqRestTurnCost
      ? `Needs ${report.hqRestTurnCost} turns and you have ${dashboard.turns}.`
      : dashboard.cash < report.hqRestCashCost
        ? `Needs ${money.format(report.hqRestCashCost)} in hand. Bank money does not count.`
        : null
  const partyReason = moraleFull
    ? 'Your crew are already steady.'
    : dashboard.turns < report.hqPartyTurnCost
      ? `Needs ${report.hqPartyTurnCost} turns and you have ${dashboard.turns}.`
      : dashboard.cash < report.hqPartyCashCost
        ? `Needs ${money.format(report.hqPartyCashCost)} in hand. Bank money does not count.`
        : dashboard.beer < report.hqPartyBeerCost
          ? `Needs ${report.hqPartyBeerCost} beer and you have ${dashboard.beer}.`
          : dashboard.weed < report.hqPartyWeedCost
            ? `Needs ${report.hqPartyWeedCost} weed and you have ${dashboard.weed}.`
            : null

  // The buttons carry these rather than printing them underneath. Said in both places at once, the
  // sentence appeared twice on screen the moment anybody hovered the thing it was about.
  const restBlocked = firstReason(busy && BUSY, restReason)
  const partyBlocked = firstReason(busy && BUSY, partyReason)

  return <section className="card p-3 gcol-full" data-area="recovery">
    <div className="panel-title"><h2>Recovery</h2><span>{dashboard.hideout.tierName} morale</span></div>
    <div className="d-grid gtc-1 gtc-md-split-90 gap-3 align-items-stretch">
      <div className="d-grid align-content-center gap-2 border rounded-2 bg-body-secondary p-3">
        <strong className="text-primary">Current hideout</strong>
        <p className="m-0">Your crew comes back here after street work and fights. Low morale heals slowly over time, or you can spend turns and supplies to steady them faster.</p>
      </div>
      <div className="d-grid gtc-1 gtc-md-2 gap-2">
        <Button className="btn btn-secondary btn-stacked" blocked={restBlocked} onClick={() => void act(() => api.recoverMorale('rest'))}>
          Rest Crew
          <span>{report.hqRestTurnCost} turns / {money.format(report.hqRestCashCost)} / +{report.hqRestMoraleGain.toFixed(0)}% to both</span>
        </Button>
        <Button className="btn btn-primary btn-stacked" blocked={partyBlocked} onClick={() => void act(() => api.recoverMorale('party'))}>
          Throw Party
          {/* The party's two gains were the one thing this panel never said, while the rest button
              beside it has always shown its own. A player whose thugs are the half that is suffering
              could not tell which of these two was the one aimed at them. */}
          <span>
            {report.hqPartyTurnCost} turns / {money.format(report.hqPartyCashCost)} / {report.hqPartyBeerCost} beer / {report.hqPartyWeedCost} weed
            {' / '}+{report.hqPartyHoeMoraleGain.toFixed(0)}% hoes, +{report.hqPartyThugMoraleGain.toFixed(0)}% thugs
          </span>
        </Button>
      </div>
    </div>
  </section>
}


/**
 * A name you can open.
 *
 * A button rather than a link, because it goes nowhere - it opens a dialog over whatever you were
 * looking at, and a link would promise an address that does not exist.
 *
 * The click is announced on the window rather than handed down through props. Names appear in a dozen
 * places that have nothing else in common - a leaderboard row, a chat line, a transfer record, the
 * news feed - and threading a callback from the top of the app through every one of them would be a
 * prop passed through components that have no other interest in it. The app is already using window
 * events for exactly this shape of thing: opening a conversation, and blocking somebody.
 */
/** What a row says when nobody has been sent to look. A dash, not a zero: a zero is a claim. */
const UNKNOWN = '—'

/** What a strike's advice says when the house it describes has not been looked at. */
const NOT_SCOUTED = 'You have not looked inside. Scout them to find out what is in there.'

/**
 * The rungs of the intelligence ladder, matching IntelLevels on the server.
 *
 * Two copies of a disclosure rule would be one that eventually shows something the server thought it
 * was hiding - so the server nulls the field and this only decides what to write in the gap. The one
 * place it needs the number is where a field is not nullable and the absence has to be inferred: the
 * rack is an empty list either way, and protection and the day's fighting are on the status object.
 */
const INTEL = { fightingWeight: 1, armoury: 2, stock: 3, morale: 4 }

function PlayerName({ playerId, children, className }: {
  playerId: string | null | undefined
  children: ReactNode
  className?: string
}) {
  // Anything the game said rather than a player has no id, and stays plain text.
  if (!playerId) return <>{children}</>

  return <button
    type="button"
    className={`btn btn-link p-0 border-0 align-baseline text-start lh-inherit ${className ?? ''}`}
    onClick={event => {
      // The row underneath is often clickable too. This is the more specific intent.
      event.stopPropagation()
      window.dispatchEvent(new CustomEvent('street-empire:profile', { detail: { playerId } }))
    }}
  >{children}</button>
}

/**
 * That dialog. Fetched when it opens rather than held ready, because it is the same call the combat
 * screen makes and the answer is only wanted when somebody asks for it.
 */
function PlayerProfileDialog({ playerId, currentPlayerId, onClose }: {
  playerId: string
  currentPlayerId: string
  onClose: () => void
}) {
  const [profile, setProfile] = useState<PlayerProfile | null>(null)
  const [error, setError] = useState('')
  // Bumped when something the card describes has changed under it, which is the only way a refetch
  // gets asked for while the same player stays open.
  const [reload, setReload] = useState(0)

  useEffect(() => {
    let stale = false
    setProfile(null); setError('')
    void (async () => {
      try {
        const found = await api.playerProfile(playerId)
        if (!stale) setProfile(found)
      } catch (e) { if (!stale) setError((e as Error).message) }
    })()
    // Guards the race where somebody opens two names quickly: the slower answer must not land last.
    return () => { stale = true }
  }, [playerId, reload])

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return <>
    <div className="modal-backdrop show" />
    <div className="modal d-block" role="dialog" aria-modal="true" aria-label="Player profile" onClick={onClose}>
      <div className="modal-dialog modal-dialog-centered modal-lg modal-dialog-scrollable" onClick={event => event.stopPropagation()}>
        <div className="modal-content p-3">
          {error
            ? <p className="text-danger mb-0">{error}</p>
            : !profile
              ? <p className="text-body-tertiary mb-0">Looking them up.</p>
              : <>
                <PlayerCardHeader profile={profile} isSelf={profile.playerId === currentPlayerId} />
                <PlayerCardStats
                  profile={profile}
                  isSelf={profile.playerId === currentPlayerId}
                  onScouted={() => setReload(n => n + 1)}
                />
              </>}
          <button className="btn btn-secondary mt-3" type="button" onClick={onClose}>Close</button>
        </div>
      </div>
    </div>
  </>
}

function ProfileBadgeStrip({ badges }: { badges: PlayerProfile['profileBadges'] }) {
  if (badges.length === 0) return null
  return <div className="d-flex flex-wrap gap-1 mt-1">
    {badges.map(badge => <span className="badge text-bg-secondary d-inline-flex align-items-center gap-1" title={badge.detail} key={badge.key}>
      {badge.key === 'discord-connected' && <i className="bi bi-discord" aria-hidden="true" />}
      {badge.label}
    </span>)}
  </div>
}

/**
 * One player, as everybody else sees them.
 *
 * Split in two rather than one component, because the combat screen puts the whole attack apparatus
 * between the two halves and the pop-up puts nothing there at all. Everything else about them - who
 * they are, what they hit for, what they have been doing - is the same card in both places, which is
 * the point: a name should open the same thing wherever it is clicked.
 */
function PlayerCardHeader({ profile, isSelf }: { profile: PlayerProfile, isSelf: boolean }) {
  return <>
      {profile.profileBanner !== 'None'
        && <div className={`profile-banner ${bannerClass(profile.profileBanner)} mb-3`} aria-hidden="true" />}
      <div className="d-flex justify-content-between align-items-start gap-3 mb-3">
        <div className="d-flex align-items-center gap-3 min-w-0">
          <PlayerAvatar name={profile.name} avatarUrl={profile.avatarUrl} size={56} />
          <div className="d-grid gap-1 min-w-0">
            <strong className={`${profileAccentClass(profile.profileAccent)} fs-5 text-truncate`}>{profile.name}</strong>
            <span className="eyebrow">
              {[profile.city, profile.profilePronouns, profile.profileLocation].filter(Boolean).join(' / ')}
              {profile.aiPersonality ? ` / ${profile.aiPersonality}` : profile.isBot ? ' / AI rival' : ''}
            </span>
            {/* How long somebody has been at this, which is context for the numbers beside it. */}
            <small className="d-block text-body-tertiary">
              Playing since {new Date(profile.joinedAtUtc).toLocaleDateString([], { month: 'long', year: 'numeric' })}
            </small>
            {profile.publicDiscordUsername && <small className="d-block text-primary">
              <i className="bi bi-discord me-1" aria-hidden="true" />
              {profile.publicDiscordUsername}
            </small>}
            <ProfileBadgeStrip badges={profile.profileBadges ?? []} />
            {profile.profileTagline && <small className={`d-block ${profileAccentClass(profile.profileAccent)}`}>{profile.profileTagline}</small>}
            {profile.titles.length > 0 && <small className="d-block mt-1 text-primary small">{profile.titles.join(' / ')}</small>}
          </div>
        </div>
        {/* The only place a conversation can start. Everywhere else in chat you are answering
            somebody; this is where you pick who to write to in the first place. */}
        <Button
          className="btn btn-secondary btn-sm"
          type="button"
          blocked={!profile.canMessage && (profile.messageBlockedReason ?? `${profile.name} is not taking messages.`)}
          title="Start a direct conversation"
          onClick={() => void (async () => {
            try {
              const { id } = await api.openDirect(profile.playerId)
              window.dispatchEvent(new CustomEvent('street-empire:conversation', { detail: { conversationId: id } }))
            } catch { /* the profile shows its own errors elsewhere */ }
          })()}
        >{profile.canMessage ? 'Message' : 'Closed'}</Button>
        {/* Silences them. Says so plainly, because a player who thinks this also keeps them from
            raiding the house will find out the hard way and blame the button. */}
        {!isSelf && <button
          className="btn btn-secondary btn-sm"
          type="button"
          title="Stops them writing to you and hides them from your rooms. It does not stop them attacking you."
          onClick={() => void (async () => {
            try {
              await api.block(profile.playerId)
              window.dispatchEvent(new CustomEvent('street-empire:blocked'))
            } catch { /* the profile shows its own errors elsewhere */ }
          })()}
        >Block</button>}
        <b className="text-primary fs-5">#{profile.rank}</b>
      </div>
      {isSelf && <p className="text-body-tertiary small">
        This is your own card, exactly as anybody who looks you up sees it - which is also what the
        privacy settings on your account page decide.
      </p>}
  </>
}

/**
 * Why the card is half blank, and the button that fixes it.
 *
 * Both numbers are shown - what the last look was worth and what a look would be worth now - because
 * neither means anything alone. "Level 1" is not an answer to "why can I not see their morale"; "level
 * 1, and your centre is level 3" is.
 */
function IntelBand({ profile, onScouted }: { profile: PlayerProfile, onScouted?: () => void }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const { level, yourCentreLevel, gatheredAtUtc, fresh, scoutTurnCost, freshHours } = profile.intel

  const scout = async () => {
    setBusy(true); setError('')
    try {
      await api.scoutPlayer(profile.playerId)
      onScouted?.()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const said = yourCentreLevel < 1
    ? 'You have no intelligence centre, so there is nobody to send. Build one at the hideout.'
    : !fresh && gatheredAtUtc
      ? `You looked on ${new Date(gatheredAtUtc).toLocaleDateString()} and it has gone cold. Intelligence keeps for ${freshHours} hours.`
      : !fresh
        ? 'Nobody has been inside. Everything below is guesswork until somebody has.'
        : level < yourCentreLevel
          ? `Scouted at level ${level}. Your centre is level ${yourCentreLevel} now - another look would bring back more.`
          : `Scouted at level ${level}, good for ${freshHours} hours.`

  return <div className={`d-flex flex-wrap align-items-center justify-content-between gap-2 border rounded p-2 mb-3 ${fresh ? 'bg-body-tertiary' : 'border-warning-subtle'}`}>
    <small className="min-w-0 text-body-secondary">{error || said}</small>
    {yourCentreLevel >= 1 && <Button
      className="btn btn-outline-primary btn-sm"
      type="button"
      blocked={busy && BUSY}
      onClick={() => void scout()}
    >{busy ? 'Looking...' : `Scout (${scoutTurnCost} turn${scoutTurnCost === 1 ? '' : 's'})`}</Button>}
  </div>
}

function PlayerCardStats({ profile, isSelf, onScouted }: { profile: PlayerProfile, isSelf: boolean, onScouted?: () => void }) {
  // Null all the way down when nobody has looked, which is what turns every row below into a dash
  // rather than a number somebody would act on.
  const fight = profile.combatReadiness
  return <>
    {!isSelf && <IntelBand profile={profile} onScouted={onScouted} />}
      <div className="tnum d-grid gtc-1 gtc-md-3 gap-2">
        <AdminMetric label="Net worth" value={money.format(profile.netWorth)} />
        <AdminMetric label="Cash" value={money.format(profile.cash)} />
        <AdminMetric label="Bank" value={money.format(profile.bankCash)} />
        <AdminMetric label="Attack" value={fight ? number.format(fight.attackPower) : UNKNOWN} />
        <AdminMetric label="Defence" value={fight ? number.format(fight.defensePower) : UNKNOWN} />
        <AdminMetric label="Risk" value={fight ? fight.riskBand : UNKNOWN} />
        <AdminMetric label="Combat" value={profile.combatStatus.eligibility} />
      </div>
      <div className="mt-3 border-top">
        {/* Crew sizes are on the leaderboard for the top fifty of every town, so hiding them here
            would hide nothing. Everything below them is a house's own business. */}
        <StatusRow label="Crew" value={`${profile.pimps} P / ${profile.hoes} H / ${profile.thugs} T`} />
        <StatusRow
          label="Weapons"
          value={fight ? `${fight.armedThugs}/${profile.thugs} armed` : UNKNOWN}
          warn={!!fight && fight.uncoveredThugs > 0}
        />
        {/* Coverage says how many are armed; the rack says how hard that is going to hit back. */}
        <StatusRow
          label={isSelf ? 'Your guns' : 'Their guns'}
          value={profile.intel.level >= INTEL.armoury ? rackSummary(profile.weaponRack) : UNKNOWN}
        />
        <StatusRow
          label="Firepower"
          value={fight && profile.intel.level >= INTEL.armoury ? `${fight.firepower} pistols` : UNKNOWN}
          warn={!!fight && profile.intel.level >= INTEL.armoury && fight.firepower > fight.armedThugs * 1.5}
        />
        <StatusRow
          label="Weapon coverage"
          value={fight ? `${fight.weaponCoveragePercent.toFixed(0)}%` : UNKNOWN}
          warn={!!fight && fight.weaponCoveragePercent < 75}
        />
        <StatusRow
          label="Protection"
          value={profile.intel.level >= INTEL.armoury ? combatProtectionText(profile.combatStatus) : UNKNOWN}
          warn={profile.intel.level >= INTEL.armoury && profile.combatStatus.isProtected}
        />
        <StatusRow
          label="24h combat"
          value={profile.intel.level >= INTEL.armoury
            ? `${profile.combatStatus.recentAttacksMade} attacks / ${profile.combatStatus.recentDefenses} defences`
            : UNKNOWN}
        />
        {/* Not gated. It is the reason an attack would be refused, and a refusal you cannot see the
            reason for is a bug rather than a secret. */}
        {profile.combatStatus.mismatchReason && <StatusRow label="Blocked" value={profile.combatStatus.mismatchReason} warn />}
        {/* What each strike is aimed at. A garage with cars in it and a house with no medicine are
            the reads that turn the menu into a decision rather than a list - which is exactly why they
            cost a scout. */}
        <StatusRow label="Rides" value={profile.rides === null ? UNKNOWN : profile.rides > 0 ? `${number.format(profile.rides)} parked` : 'None'} />
        <StatusRow label="Medicine" value={profile.medicine === null ? UNKNOWN : profile.medicine > 0 ? `${number.format(profile.medicine)} crate(s)` : 'None'} />
        <StatusRow
          label="Hoe morale"
          value={profile.hoeHappiness === null
            ? UNKNOWN
            : `${profile.hoeHappiness.toFixed(0)}%${profile.hoeHappiness >= 90 ? ' - paid too well to poach' : ''}`}
          warn={profile.hoeHappiness !== null && profile.hoeHappiness < 50}
        />
        <StatusRow
          label="Thug morale"
          value={profile.thugHappiness === null ? UNKNOWN : `${profile.thugHappiness.toFixed(0)}%`}
          warn={profile.thugHappiness !== null && profile.thugHappiness < 50}
        />
        <StatusRow
          label="Product"
          value={profile.weed === null || profile.coke === null
            ? UNKNOWN
            : `${number.format(profile.weed)} weed / ${number.format(profile.coke)} coke`}
        />
      </div>
      <div className="mt-3 border-top pt-3">
        <strong className="d-block mb-1 text-primary">Public Activity</strong>
        {/*
          Turned off and never done anything are different facts, and an empty list with no
          explanation reads as a broken profile rather than a choice. Saying which gives away nothing
          they did not choose to say.
        */}
        {profile.activityHidden
          ? <p className="text-body-tertiary small mt-3 mb-0">
            {isSelf
              ? 'You keep your recent activity private, so nobody else sees this list.'
              : 'They keep their recent activity private.'}
          </p>
          : profile.publicActivity.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No public activity yet.</p>}
        <ActivityList entries={profile.publicActivity} />
      </div>
  </>
}

function TargetReconPanel({ targets, selectedTarget, query, busy, currentPlayerId, combatMissions, dashboard, attackCrew, setAttackCrew, commanderId, setCommanderId, attackMethod, setAttackMethod, poachCoke, setPoachCoke, borrowedThugs, setBorrowedThugs, onQuery, onSearch, onInspect, onAttack }: {
  targets: PlayerTarget[]
  selectedTarget: PlayerProfile | null
  query: string
  busy: boolean
  currentPlayerId: string
  combatMissions: CombatMission[]
  dashboard: Dashboard
  attackCrew: { thugs: number, weapons: number }
  setAttackCrew: React.Dispatch<React.SetStateAction<{ thugs: number, weapons: number }>>
  commanderId: number | null
  setCommanderId: (id: number | null) => void
  attackMethod: AttackMethodKey
  setAttackMethod: (method: AttackMethodKey) => void
  poachCoke: number
  setPoachCoke: (coke: number) => void
  borrowedThugs: number
  setBorrowedThugs: (thugs: number) => void
  onQuery: (query: string) => void
  onSearch: (event: FormEvent<HTMLFormElement>) => void
  onInspect: (playerId: string) => void
  onAttack: (playerId: string) => void
}) {
  const profile = selectedTarget
  const activeOutgoingMissions = combatMissions.filter(mission => mission.attackerId === currentPlayerId && mission.status !== 'Complete')
  const activeAgainstProfile = profile
    ? activeOutgoingMissions.find(mission => mission.defenderId === profile.playerId)
    : undefined
  const crew = dashboard.combatCrew
  const freeCommanders = dashboard.crew.filter(pimp => !pimp.isCommanding)
  // Written as reasons rather than as one boolean, because "Assign available crew" was the whole of
  // what the page could say about six different ways of getting the party wrong.
  const raidBlocker = firstReason(
    crew.availablePimps < 1 && 'Every pimp you have is already out. A raid needs one to lead it.',
    attackCrew.thugs < 1 && 'Send at least one thug.',
    attackCrew.thugs > crew.availableThugs && `You are sending ${number.format(attackCrew.thugs)} thugs and ${number.format(crew.availableThugs)} are free to go.`,
    attackCrew.weapons < 0 && 'You cannot send a negative number of guns.',
    attackCrew.weapons > attackCrew.thugs && `${number.format(attackCrew.weapons)} guns and ${number.format(attackCrew.thugs)} thugs: there is nobody to carry the rest.`,
    attackCrew.weapons > crew.availableWeapons && `You are sending ${number.format(attackCrew.weapons)} guns and ${number.format(crew.availableWeapons)} are on the rack.`,
    crew.activeAttackMissions >= crew.maxActiveAttackMissions && `You have ${crew.activeAttackMissions} raids out already, which is all you can run at once.`,
  )
  const method = dashboard.attackMethods.find(x => x.key === attackMethod) ?? dashboard.attackMethods[0]
  // Worked out by the server against this exact pairing, so it is the same sentence the launch would
  // have thrown rather than a second opinion the page arrived at on its own.
  const strikeBlocker = method && profile ? profile.strikeBlockers?.[method.key] : undefined
  const isRaid = method?.key === 'raid'
  // A strike is gated by the method's own requirements, which the server has already worked out, plus
  // the turns it costs. A raid is gated by crew, which only it commits.
  const methodBlocker = firstReason(
    !method && 'Pick how you want to hit them first.',
    method?.blockedReason,
    !!method && dashboard.turns < method.turnCost && `${method.label} costs ${method.turnCost} turns and you have ${dashboard.turns}.`,
    isRaid && raidBlocker,
    // Nothing to hand out means nobody to tempt, so the run is refused before it costs the turns.
    method?.key === 'poach' && poachCoke <= 0 && 'Set how much coke to put on the table first.',
    method?.key === 'poach' && poachCoke > dashboard.coke && `You are spending ${number.format(poachCoke)} coke and you hold ${number.format(dashboard.coke)}.`,
  )
  const methodReady = methodBlocker === null
  // Everything above is about your own side. This adds theirs, and is only answerable once somebody is
  // actually being looked at.
  const attackBlocked = (target: PlayerProfile) => firstReason(
    busy && BUSY,
    isRaid && !!activeAgainstProfile && `Your crew is already out against ${target.name}. Next update in ${timeUntil(nextMissionTime(activeAgainstProfile))}.`,
    methodBlocker,
    // The method menu is built from your own crew and garage and has never seen who you are looking
    // at, so a strike with nothing to take on the other end sat under a live button and was only
    // refused once you had pressed it.
    strikeBlocker,
    !isRaid && target.combatStatus.isStrikeProtected && `${target.name} was just hit and is watching the street.`,
    !target.combatStatus.canAttackNow && attackStatusText(
      target.combatStatus,
      activeAgainstProfile,
      activeOutgoingMissions[0],
      methodReady),
  )
  return <div className="card p-3 gcol-full">
    <div className="panel-title" data-area="targets"><h2>Combat Targets</h2><span>Scout + launch</span></div>
    <form className="d-grid gtc-1 gtc-md-1-auto-auto gap-2 align-items-end mb-3" onSubmit={onSearch}>
      <label className="field">Search<input className="form-control" value={query} onChange={event => onQuery(event.target.value)} placeholder="Name or city" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY}>Search</Button>
      {/*
        Your own card, from the one screen where the numbers on it mean something. Attack and defence
        were readable for every player in the game except the one reading them, which made judging a
        target a matter of guessing at your own half of the comparison.

        The same endpoint and the same card - the server already knew what this was, answering with an
        eligibility of "Self" and every strike blocked, and nothing had ever asked it.
      */}
      <Button
        className="btn btn-outline-secondary btn-sm"
        type="button"
        blocked={busy && BUSY}
        onClick={() => onInspect(currentPlayerId)}
      >Your card</Button>
    </form>
    <div className="d-grid gtc-1 gtc-xl-split-80 gap-3 align-items-start">
      <div className="d-grid gap-2">
        {targets.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No targets found.</p>}
        {targets.map(target => <Button
          className={`target-row w-100 d-grid gap-1 column-gap-2 align-items-center text-start border rounded p-2 ${profile?.playerId === target.playerId ? 'active border-info' : 'bg-body-secondary'}`}
          key={target.playerId}
          type="button"
          blocked={busy && BUSY}
          onClick={() => onInspect(target.playerId)}
        >
          <span className="text-primary fw-bolder">#{target.rank}</span>
          <span className="d-flex align-items-center gap-2 min-w-0">
            <PlayerAvatar name={target.name} avatarUrl={target.avatarUrl} size={32} />
            <strong className={`min-w-0 text-truncate ${profileAccentClass(target.profileAccent)}`}>{target.name}</strong>
          </span>
          <small className="text-body-secondary small">{target.profileTagline || `${target.city}${target.aiPersonality ? ` / ${target.aiPersonality}` : target.isBot ? ' / AI' : ''}`}</small>
          <em className={`eyebrow fst-normal ${target.combatStatus.mismatchReason ? 'text-warning-emphasis' : ''}`}>{target.titles.length > 0 ? target.titles.join(', ') : `${target.combatStatus.eligibility} / ${target.combatReadiness.riskBand}`}{target.rides > 0 ? ` / ${target.rides} parked` : ''}</em>
          <b className="text-body">{money.format(target.netWorth)}</b>
        </Button>)}
      </div>
      {profile && (() => { const isSelf = profile.playerId === currentPlayerId; const blocked = attackBlocked(profile); return <div className="border rounded bg-body-secondary p-3">
        <PlayerCardHeader profile={profile} isSelf={isSelf} />
        {isSelf
          ? <p className="text-body-tertiary small">
            This is your own card, exactly as anybody who looks you up sees it - which is also what the
            privacy settings on your account page decide.
          </p>
          : <><AttackMethodPicker
          methods={dashboard.attackMethods}
          selected={attackMethod}
          turns={dashboard.turns}
          onSelect={setAttackMethod}
        />
        {/* A raid is the only method that commits crew, so it is the only one that asks for any. */}
        {isRaid && <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
          <StatusRow label="Available" value={`${crew.availablePimps} P / ${crew.availableThugs} T / ${crew.availableWeapons} W`} warn={crew.availablePimps < 1 || crew.availableThugs < 1} />
          <StatusRow label="Committed" value={`${crew.committedPimps} P / ${crew.committedThugs} T / ${crew.committedWeapons} W`} warn={crew.committedThugs > 0} />
          <div className="d-grid gtc-1 gtc-md-3 gap-2">
            <label className="field small">Commander
              <select className="form-select"
                value={commanderId ?? ''}
                onChange={event => setCommanderId(event.target.value === '' ? null : Number(event.target.value))}
              >
                <option value="">Best available</option>
                {freeCommanders.map(pimp => <option key={pimp.id} value={pimp.id}>
                  {pimp.name} - {pimp.specialty} +{pimp.bonusPercent}%
                </option>)}
              </select>
            </label>
            <label className="field">Thugs<input className="form-control" type="number" min={1} max={Math.max(1, crew.availableThugs)} value={attackCrew.thugs} onChange={e => setAttackCrew(value => ({ ...value, thugs: Number(e.target.value), weapons: Math.min(value.weapons, Number(e.target.value)) }))} /></label>
            <label className="field">Weapons<input className="form-control" type="number" min={0} max={Math.max(0, Math.min(crew.availableWeapons, attackCrew.thugs))} value={attackCrew.weapons} onChange={e => setAttackCrew(value => ({ ...value, weapons: Number(e.target.value) }))} /></label>
            {/* You may bring as many of the crew's as you brought of your own, so the cap moves with
                the party rather than sitting at a fixed number. */}
            {dashboard.alliance && dashboard.alliance.offensiveThugs > 0 && <label className="field">{dashboard.alliance.name}
              <input className="form-control"
                type="number"
                min={0}
                max={Math.min(dashboard.alliance.offensiveThugs, attackCrew.thugs)}
                value={Math.min(borrowedThugs, attackCrew.thugs)}
                onChange={event => setBorrowedThugs(Number(event.target.value))}
              />
            </label>}
          </div>
          <small className="d-block mt-1 text-body-tertiary small measure">{commanderNote(freeCommanders.find(x => x.id === commanderId) ?? null)}</small>
        </div>}
        {method && !isRaid && <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
          <p className="m-0 text-info-emphasis small">{method.description}</p>
          {method.key === 'poach' && <div className="d-grid gtc-1 gtc-md-3 gap-2">
            <label className="field small">Coke to spend<input className="form-control"
              type="number"
              min={1}
              max={Math.max(1, dashboard.coke)}
              value={poachCoke}
              onChange={event => setPoachCoke(Number(event.target.value))}
            /></label>
          </div>}
          <small className="d-block mt-1 text-body-tertiary small measure">{strikeNote(method, profile, dashboard, poachCoke)}</small>
        </div>}
        <div className="d-grid gtc-1 gtc-md-auto-1 gap-2 align-items-center mb-3 border rounded p-2">
          <Button
            className="btn btn-primary"
            type="button"
            blocked={blocked}
            onClick={() => onAttack(profile.playerId)}
          >
            {isRaid ? 'Send the Raid' : method?.label ?? 'Attack'}
          </Button>
          {/* What it costs, and only while it can actually be thrown. Why it cannot is on the button
              beside this, and printing both put the same sentence on screen twice. */}
          {!blocked && <span>{method && !isRaid
            ? strikeStatusText(method, dashboard, profile.combatStatus)
            : attackStatusText(
              profile.combatStatus,
              activeAgainstProfile,
              activeOutgoingMissions[0],
              methodReady)}</span>}
        </div>
        </>}
        <PlayerCardStats profile={profile} isSelf={isSelf} onScouted={() => onInspect(profile.playerId)} />
      </div> })()}
    </div>
  </div>
}

/**
 * The attack menu. Every entry arrives from the server already priced and already carrying the reason
 * it cannot be used, so this renders the list without knowing a single rule about any of them.
 */
function AttackMethodPicker({ methods, selected, turns, onSelect }: {
  methods: AttackMethod[]
  selected: AttackMethodKey
  turns: number
  onSelect: (method: AttackMethodKey) => void
}) {
  return <div className="d-grid gtc-2 gtc-md-fill-120 gap-2 mb-3">
    {methods.map(method => {
      // Blocked and unaffordable read differently on purpose: one is something to go and buy, the
      // other is something to go and wait for.
      const unaffordable = turns < method.turnCost
      return <button
        className={`tile d-grid gap-1 text-start border rounded p-2 ${method.key === selected ? 'active border-primary' : 'bg-body-tertiary'}`}
        key={method.key}
        type="button"
        title={method.description}
        onClick={() => onSelect(method.key)}
      >
        <strong className="text-body">{method.label}</strong>
        <small className={`small ${unaffordable ? 'text-warning-emphasis' : 'text-body-tertiary'}`}>{method.turnCost} turns</small>
        {method.blockedReason && <em className="fst-normal small text-warning-emphasis">{method.blockedReason}</em>}
      </button>
    })}
  </div>
}

/**
 * What this strike would actually achieve against this target. The server refuses the impossible, but a
 * refusal after the click is a worse experience than a sentence before it - and for poaching, the honest
 * answer is often "nothing, they are paid too well", which no error message would ever say.
 */
function strikeNote(method: AttackMethod, profile: PlayerProfile, dashboard: Dashboard, poachCoke: number) {
  switch (method.key) {
    case 'driveby': {
      const armed = profile.combatReadiness.armedThugs
      if (armed === 0) return 'Nobody armed on that street. A clean pass, and the car should come back.'
      // The guns are the half that decides whether the car comes back, so name them when they are
      // more than sidearms rather than leaving "better armed" for the player to infer.
      const heavy = profile.combatReadiness.firepower > armed
      return heavy
        ? `${armed} armed thug(s) on that street carrying ${rackSummary(profile.weaponRack).toLowerCase()}. That is what takes the car, not the number of them.`
        : `${armed} armed thug(s) on that street, carrying sidearms. Hard to catch anyone in the open, but the car should come back.`
    }
    case 'jack': {
      if (profile.rides === 0) return 'Nothing parked there to take.'
      // Both halves of the guard, because both stop you and they stop you differently: bodies are eyes
      // on the door, guns are what happens once you are seen.
      // The advice is intelligence too. Describing their garage to somebody who has not looked in it
      // would hand back through the narrative exactly what the card refuses to show.
      if (!profile.combatReadiness || profile.rides === null) return NOT_SCOUTED
      const armed = profile.combatReadiness.armedThugs
      const heavy = profile.combatReadiness.firepower > armed
      const guns = heavy ? rackSummary(profile.weaponRack).toLowerCase() : 'sidearms'
      return `${profile.rides} parked behind ${armed} armed thug(s) carrying ${guns}. Room for ${Math.max(0, dashboard.hideout.maxRides - dashboard.rides)} more in your garage.`
    }
    case 'infest': {
      if (profile.medicine === null) return NOT_SCOUTED
      const covered = profile.medicine * 3
      // Your own doses are half the arithmetic now: you reach as far as you brought poison for, so a
      // note that only described their medicine would be describing half the fight.
      const reach = dashboard.poison * 3
      if (dashboard.poison === 0) return 'You have no poison. The counter sells it, and a mix house makes it cheaper.'
      if (covered >= profile.hoes && profile.hoes > 0)
        return `Their ${profile.medicine} crate(s) cover the whole house. Nothing would be lost.`
      return `${profile.hoes} hoes behind ${profile.medicine} crate(s) of medicine. `
        + `Your ${dashboard.poison} dose(s) reach ${reach} of them, and whatever the medicine cannot treat is gone.`
    }
    case 'poach':
      if (profile.hoeHappiness === null) return NOT_SCOUTED
      return profile.hoeHappiness >= 90
        ? 'Their house is paid too well. Nobody is going anywhere at any price.'
        : `Their morale is ${profile.hoeHappiness.toFixed(0)}%, and yours is ${dashboard.cokePurityPercent}% pure. ${poachCoke > dashboard.coke ? 'You do not hold that much coke.' : 'The coke goes out whether or not anyone comes back with you.'}`
    default:
      return method.description
  }
}

/**
 * The shrine. The gods name a thing and a number; meeting it is answered.
 *
 * Fetched on its own rather than folded into the dashboard because it is a weekly errand, not a live
 * figure: nothing on this panel changes between page loads except when the player acts on it.
 */
/**
 * The cell.
 *
 * Draws nothing at all when nobody is being held, which is almost always - a panel that sat there
 * empty would be a standing reminder of a thing that is not happening, on the page a player opens to
 * look at the crew they still have.
 *
 * The clock is the whole reason this is urgent, so it runs live off the same ticker the bank window
 * uses, and the row re-reads itself when the window closes so a bond is never offered on somebody the
 * server has already written off.
 */
function ArrestPanel({ dashboard, busy, act }: {
  dashboard: Dashboard
  busy: boolean
  act: PageContext['act']
}) {
  const [board, setBoard] = useState<ArrestBoard | null>(null)
  const [error, setError] = useState('')

  const load = async () => {
    try {
      setBoard(await api.arrests())
      setError('')
    } catch (e) { setError((e as Error).message) }
  }
  // Re-read when the crew or the money moves, which covers both answering a cell and coming back from
  // a shift that filled one.
  useEffect(() => { void load() }, [dashboard.hoes, dashboard.thugs, dashboard.pimps, dashboard.cash])

  const held = board?.held ?? []
  // A window that has closed since the page was drawn is not a bond any more; the server has already
  // settled it and will refuse. Hidden rather than greyed out, because there is nothing left to decide.
  //
  // Filtered against a live reading rather than the ticker's last value: a render caused by anything
  // other than the tick - the parent refreshing, a sibling changing - would otherwise be judged on a
  // clock up to a second stale, and offer a bond the server has already written off. The ticker only
  // has to drive the redraw and stop itself once the last window is gone.
  const open = held.filter(x => new Date(x.bailDeadlineUtc).getTime() > Date.now())
  const ticking = useSecondsTicker(open.length > 0)

  if (open.length === 0) return null

  return <section className="card p-3 gcol-full border-warning" data-area="arrests">
    <div className="panel-title">
      <h2>In County</h2>
      <span>{number.format(open.reduce((n, x) => n + x.heads, 0))} held</span>
    </div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    <p className="mb-2">
      Bail draws on your bank first. Leaving them costs the morale of everybody still out, and a pimp
      with little left to lose talks to the law on the way in.
    </p>
    <div className="d-grid gap-2">
      {open.map(arrest => {
        const seconds = secondsUntil(arrest.bailDeadlineUtc, ticking)
        const who = [
          arrest.hoes > 0 ? `${number.format(arrest.hoes)} hoe${arrest.hoes === 1 ? '' : 's'}` : null,
          arrest.thugs > 0 ? `${number.format(arrest.thugs)} thug${arrest.thugs === 1 ? '' : 's'}` : null,
          arrest.pimpName,
        ].filter(Boolean).join(' and ')
        return <div className="d-grid gap-1 border rounded px-3 py-2" key={arrest.id}>
          <div className="d-flex justify-content-between align-items-center flex-wrap gap-2">
            <strong>{who}</strong>
            <span className={seconds < 3600 ? 'text-danger' : 'text-body-secondary'}>Released in {countdown(seconds)}</span>
          </div>
          <small className="text-body-tertiary">
            Swept up in {arrest.city}{arrest.district ? ` / ${arrest.district}` : ''} on a {arrest.chancePercent}% shift.
          </small>
          <div className="control-row">
            <Button
              className="btn btn-primary"
              blocked={firstReason(
                busy && BUSY,
                !arrest.canAffordBail && `Bail is ${money.format(arrest.bailAmount)} and you have ${money.format(board?.funds ?? 0)} between cash and the bank.`,
              )}
              onClick={() => void act(async () => { const r = await api.bailArrest(arrest.id); await load(); return r })}>
              Bail out ({money.format(arrest.bailAmount)})
            </Button>
            <Button
              className="btn btn-outline-secondary"
              blocked={busy && BUSY}
              title="They are gone, and the crew still out will notice."
              onClick={() => void act(async () => { const r = await api.abandonArrest(arrest.id); await load(); return r })}>
              Leave them
            </Button>
          </div>
        </div>
      })}
    </div>
  </section>
}

function ShrinePanel({ busy, act }: { busy: boolean, act: PageContext['act'] }) {
  const [board, setBoard] = useState<PrayerBoard | null>(null)
  const [offered, setOffered] = useState(0)

  const load = async () => {
    try {
      const next = await api.prayer()
      setBoard(next)
      // Default to exactly what was asked. Anything the player types over that is generosity, which is
      // the only decision the shrine actually offers them.
      setOffered(next.quantity)
    } catch {
      // The shrine is flavour. A player who cannot reach it should still get the rest of the page.
    }
  }

  useEffect(() => { void load() }, [])
  if (!board) return null

  const enough = board.held >= board.quantity
  const generous = offered >= board.generousQuantity
  // Spans, and splits inside. Alone in the first row of the crew page's grid, this left the best part
  // of 700px empty beside a paragraph and one number box. Side by side they fill the row, which is the
  // same shape the player market card ended up in for the same reason.
  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>The Pimp Gods</h2><span>Once a week</span></div>
    <div className="d-grid gtc-1 gtc-lg-2 gap-3 align-items-center">
    <p className="text-info-emphasis mb-0">
      They want <strong>{number.format(board.quantity)} {board.label}</strong> this week. You hold{' '}
      {number.format(board.held)}. What comes back is never money: they deal in what the law has on you,
      how the house feels, and whether your pimps still believe in you.
    </p>
    <div className="control-row">
      <label className="field">Offer<input className="form-control"
        type="number"
        min={board.quantity}
        value={offered}
        onChange={event => setOffered(Number(event.target.value))}
      /></label>
      <Button
        className="btn btn-primary"
        blocked={firstReason(
          busy && BUSY,
          !board.canPray && (board.blockedReason ?? (board.nextPrayerAtUtc
            ? `They have had their week. Come back in ${timeUntil(board.nextPrayerAtUtc)}.`
            : 'The shrine is closed to you right now.')),
          !enough && `They asked for ${number.format(board.quantity)} ${board.label} and you hold ${number.format(board.held)}.`,
          offered < board.quantity && `They asked for ${number.format(board.quantity)}. Anything less is an insult.`,
          offered > board.held && `You are offering ${number.format(offered)} and you hold ${number.format(board.held)}.`,
        )}
        onClick={() => void act(async () => {
          const result = await api.pray(offered)
          await load()
          return result
        })}
      >
        Make the offering
      </Button>
      <span className="text-body-tertiary small">
        {generous
          ? 'Twice what they asked. Generosity buys what meeting the ask does not.'
          : `${number.format(board.generousQuantity)} would count as generous.`}
      </span>
    </div>
    </div>
  </section>
}

/**
 * Who leads at what today. Half the categories are for things done to a player rather than by them,
 * which is the source game's own reading and the half that makes the board worth reading.
 */
function TitleBoardPanel({ currentPlayerId }: { currentPlayerId: string }) {
  const [titles, setTitles] = useState<PlayerTitle[]>([])

  useEffect(() => {
    let live = true
    void (async () => {
      try {
        const next = await api.titles()
        if (live) setTitles(next)
      } catch {
        // A board nobody has earned anything on is not worth an error message.
      }
    })()
    return () => { live = false }
  }, [])

  // Spans: the panel beside it on the recon page spans too, so this was sitting in a two-column grid
  // with the second column held open for nothing.
  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Today's Names</h2><span>Last 24 hours</span></div>
    {titles.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nobody has done enough today to be called anything.</p>}
    <div className="tnum d-grid gap-1">
      {titles.map(title => <div
        className={`title-row d-grid gap-1 column-gap-2 border rounded bg-body-tertiary p-2 ${title.playerId === currentPlayerId ? 'border-primary' : ''}`}
        key={title.key}
      >
        <strong className="text-primary">{title.title}</strong>
        <b className="text-body"><PlayerName playerId={title.playerId}>{title.playerName}</PlayerName></b>
        <small className="gcol-full text-body-tertiary small">{title.detail}</small>
      </div>)}
    </div>
  </section>
}

/**
 * Who you run with.
 *
 * The whole of what a crew buys is at the top of the page, because it is the only reason to be on it:
 * the people listed here cannot rob you and you cannot rob them. Everything else - the treasury, the
 * rate, the board - is bookkeeping around that one fact.
 */
function AlliancePage(ctx: PageContext) {
  const { busy, act } = ctx
  const [board, setBoard] = useState<AllianceBoard | null>(null)
  const [name, setName] = useState('')
  const [motto, setMotto] = useState('')

  const load = async () => {
    try {
      setBoard(await api.alliances())
    } catch {
      // The page is readable without the board; an error banner over an empty list says nothing.
    }
  }
  useEffect(() => { void load() }, [])

  const run = (fn: () => Promise<ActionResult>) => void act(async () => {
    const result = await fn()
    await load()
    return result
  })

  if (!board) return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start"><section className="card p-3"><p className="text-body-tertiary small mt-3 mb-0">Reading the board.</p></section></div>

  const yours = board.yours
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    {yours
      ? <section className="card p-3 gcol-full">
        <div className="panel-title"><h2>{yours.name}</h2><span>#{yours.rank} / {yours.members} of {yours.maxMembers}</span></div>
        {yours.motto && <p className="fst-italic text-primary mb-2">{yours.motto}</p>}
        <p>
          Nobody on this list can attack you and you cannot attack them, by any method. That is what the{' '}
          {yours.duesPercent}% off every shift is buying.
        </p>
        <div className="tnum d-grid gtc-1 gtc-md-3 gtc-xl-5 gap-2 mb-3">
          <AdminMetric label="Crew worth" value={money.format(yours.netWorth)} />
          <AdminMetric label="Treasury" value={money.format(board.treasury)} />
          <AdminMetric label="Dues" value={`${yours.duesPercent}%`} />
          <AdminMetric label="Pool" value={`${yours.offensiveThugs} off / ${yours.defensiveThugs} def`} />
          <AdminMetric label="City control" value={yours.cityControlThugs > 0 ? `+${yours.cityControlThugs}` : 'None'} />
          <AdminMetric label="You are" value={board.yourRank} />
        </div>

        <div className="d-grid gap-1 my-3">
          {board.members.map(member => <AllianceMemberRow
            key={member.playerId}
            member={member}
            board={board}
            busy={busy}
            onAct={run}
          />)}
        </div>

        <AllianceRequestsPanel board={board} busy={busy} onAct={run} />

        <AllianceAssistPanel board={board} ownPlayerId={ctx.dashboard.playerId} busy={busy} onAct={run} />

        <AllianceWarPanel board={board} busy={busy} />

        <AlliancePactsPanel board={board} busy={busy} onAct={run} />

        <AlliancePoolPanel board={board} crew={yours} busy={busy} onAct={run} />

        <AllianceTransfersPanel transfers={board.transfers} />

        {board.yourRank === 'Boss' && <AllianceSettingsPanel crew={yours} board={board} maxDues={board.maxDuesPercent} busy={busy} onSave={run} />}

        <div className="control-row">
          <Button className="btn btn-secondary" blocked={busy && BUSY} onClick={() => run(() => api.leaveAlliance())}>
            {yours.youFounded && yours.members > 1 ? 'Leave (throw everybody out first)' : 'Leave the crew'}
          </Button>
        </div>
      </section>
      : <section className="card p-3 gcol-full">
        <div className="panel-title"><h2>Start a Crew</h2><span>{money.format(board.foundingCost)}</span></div>
        {/*
          Three sentences rather than one, and one of them says what the money is for.

          "A crew is people who" works as a heading and grates as the opening of a paragraph - a
          singular subject with a plural after it. The rest ran on through two "and"s and finished on
          "a share into a shared pot", which repeats itself in five words and still leaves a player
          deciding whether to spend the founding fee with no idea what the pot does.

          The truce sentence is now word for word the one the in-crew panel already uses, since it is
          the same promise and there is no reason for the game to phrase it twice.
        */}
        <p>
          A crew is an agreement not to rob each other. Nobody in one can attack you and you cannot
          attack them, by any method. It costs a cut of every shift any of you works, and that fills a
          treasury the crew spends on thugs to send along on a raid or post at a member's house.
        </p>
        <div className="control-row">
          <label className="field">Name<input className="form-control" value={name} maxLength={32} onChange={event => setName(event.target.value)} /></label>
          <label className="field">Motto<input className="form-control" value={motto} maxLength={140} onChange={event => setMotto(event.target.value)} /></label>
          <Button
            className="btn btn-primary"
            blocked={firstReason(
              busy && BUSY,
              name.trim().length < 3 && 'A crew needs a name of at least three characters.',
            )}
            onClick={() => run(() => api.foundAlliance(name.trim(), motto.trim()))}
          >Found it</Button>
        </div>
      </section>}

    {/* Spans, because the alliance page has exactly two children and the other one spans too - the
        second column of this grid was being held open for something that never renders. */}
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The Board</h2><span>{board.board.length} crews</span></div>
      {board.board.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nobody is running with anybody yet.</p>}
      <div className="tnum d-grid gap-1 my-3">
        {board.board.map(crew => <div className={`alliance-row d-grid gap-2 align-items-center border rounded bg-body-tertiary p-2 ${crew.yours ? 'border-primary' : ''}`} key={crew.id}>
          <span>#{crew.rank}</span>
          {/*
            A stack, not two inline elements in a row. Both of these are inline and JSX eats the
            newline between them, so they rendered welded together - "The Eastside TableOpen to
            anyone". The name goes above the things attached to it, which is what the row's own
            comment in the stylesheet says a crew is.
          */}
          <div className="d-grid">
            <strong>{crew.name}</strong>
            <small className="text-body-secondary">
              {crew.doorLabel} / {crew.members} of {crew.maxMembers} / {crew.duesPercent}% dues
              {crew.cityControlThugs > 0 ? ` / +${crew.cityControlThugs} city thugs` : ''}
              {/* A record you cannot see from outside is not a reputation. */}
              {crew.warsWon + crew.warsLost > 0 ? ` / ${crew.warsWon}-${crew.warsLost} in wars` : ''}
            </small>
            {crew.atWarWith && <small className="text-danger-emphasis">At war with {crew.atWarWith}</small>}
          </div>
          <b>{money.format(crew.netWorth)}</b>
          {/*
            Every control in one cell, however many there turn out to be.

            They used to be siblings of the row itself, each taking a grid column of its own, and the
            row declares four. Three are spoken for by the rank, the name and the money, which left
            exactly one for the buttons - fine while there was only ever one, and there is only ever
            one for a crew you are not in. From inside a crew there are two: ally with them, or
            declare on them. The second had nowhere to go, so it fell into an implicit row and landed
            in the rank column, and "War costs $250,000" was rendered a character at a time down a
            42-pixel strip.

            A cell that holds them means the row's column count stops depending on how many buttons a
            particular crew happens to earn.
          */}
          <div className="alliance-row-actions d-flex flex-wrap align-items-center gap-2">
            {/* One door, one thing an outsider can do about it. Offering a button the crew has said it
                does not want is how a player learns a rule by being refused. */}
            {!yours && crew.members >= crew.maxMembers && <em>Full</em>}
            {!yours && crew.members < crew.maxMembers && crew.door === 'Open' && <Button
              className="btn btn-secondary btn-sm"
              blocked={busy && BUSY}
              onClick={() => run(() => api.joinAlliance(crew.id))}
            >Join</Button>}
            {!yours && crew.members < crew.maxMembers && crew.door === 'Application' && <Button
              className="btn btn-secondary btn-sm"
              blocked={busy && BUSY}
              onClick={() => run(() => api.applyToAlliance(crew.id))}
            >Ask</Button>}
            {!yours && crew.members < crew.maxMembers && crew.door === 'InviteOnly' && <em title={crew.doorDetail}>Invite only</em>}
            {yours && !crew.yours && !hasPactWith(board, crew.id) && <Button
              className="btn btn-secondary btn-sm"
              blocked={busy && BUSY}
              onClick={() => run(() => api.requestAlliancePact(crew.id))}
            >Ally</Button>}
            {/* Offered only where it could actually be pressed: your rank has to allow spending the
                treasury, neither crew can already be in a war, and you cannot declare on people you
                hold a truce with. Every one of those is refused by the server too - this is so nobody
                learns the rules by being told no. */}
            {yours && !crew.yours && board.warTerms.youCanDeclare && !hasPactWith(board, crew.id)
              && !board.war && !crew.atWarWith && <Button
                className="btn btn-outline-danger btn-sm text-nowrap"
                blocked={firstReason(
                  busy && BUSY,
                  board.treasury < board.warTerms.stake && `A war stakes ${money.format(board.warTerms.stake)} and the treasury holds ${money.format(board.treasury)}.`,
                )}
                title={`${money.format(board.warTerms.stake)} out of the treasury, ${board.warTerms.durationHours} hours, winner takes the stake and ${board.warTerms.tributePercent}% of the losing treasury.`}
                onClick={() => run(() => api.declareWar(crew.id))}
              >{board.treasury < board.warTerms.stake ? `War costs ${money.format(board.warTerms.stake)}` : 'Declare war'}</Button>}
          </div>
        </div>)}
      </div>
    </section>
  </div>
}

function hasPactWith(board: AllianceBoard, allianceId: number) {
  return board.pacts.some(pact =>
    pact.status !== 'Canceled'
    && pact.status !== 'Declined'
    && (pact.requestingAllianceId === allianceId || pact.targetAllianceId === allianceId))
}

/**
 * One name on the roster, with whatever this viewer is entitled to do about them.
 *
 * The buttons are drawn from the powers the server sent rather than from a guess about rank, so a crew
 * whose boss moved a line sees the change immediately and the client never has to know what the lines
 * currently are.
 */
function AllianceMemberRow({ member, board, busy, onAct }: {
  member: AllianceMember
  board: AllianceBoard
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const canExpel = board.powers.find(x => x.power === 'Expel')?.youHaveIt ?? false
  const isBoss = board.yourRank === 'Boss'
  const [item, setItem] = useState('cash')
  const [quantity, setQuantity] = useState(1)
  const [sendOpen, setSendOpen] = useState(false)
  // Promotable ranks stop below the top: handing the crew over is its own move because it is the one
  // that gives yours away.
  const promotable = board.ranks.filter(x => x !== 'Boss')

  return <div className={`alliance-member d-grid gap-2 align-items-center border rounded bg-body-tertiary p-2 ${member.isYou ? 'border-primary' : ''}`}>
    <div className="min-w-0">
      <strong className="d-block text-truncate"><PlayerName playerId={member.playerId}>{member.name}</PlayerName></strong>
      <small className="d-block text-body-secondary">{member.rankLabel}{member.isFounder ? ' / founded it' : ''} - {member.city} / {member.pimps}P {member.hoes}H {member.thugs}T{member.defenders > 0 ? ` / ${member.defenders} posted` : ''}</small>
    </div>
    <b className="tnum">{money.format(member.netWorth)}</b>
    {!member.isYou && <div className="alliance-member-actions d-flex flex-wrap align-items-center gap-1">
      {isBoss && <select className="form-select"
        value={member.rank === 'Boss' ? '' : member.rank}
        disabled={busy || member.rank === 'Boss'}
        onChange={event => onAct(() => api.setAllianceRank(member.playerId, event.target.value))}
      >
        {member.rank === 'Boss' && <option value="">Boss</option>}
        {promotable.map(rank => <option key={rank} value={rank}>{rank}</option>)}
      </select>}
      {isBoss && <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => onAct(() => api.handOverAlliance(member.playerId))}
      >Hand over</Button>}
      {canExpel && member.youOutrankThem && <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => onAct(() => api.expelMember(member.playerId))}
      >Throw out</Button>}
      <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => setSendOpen(value => !value)}
      >Send</Button>
    </div>}
    {!member.isYou && sendOpen && <div className="alliance-transfer-controls d-flex flex-wrap align-items-end gap-1">
      <label className="field mb-0">Send
        <select className="form-select" value={item} disabled={busy} onChange={event => setItem(event.target.value)}>
          {allianceSendItems.map(option => <option key={option.key} value={option.key}>{option.label}</option>)}
        </select>
      </label>
      <label className="field mb-0">Qty<input className="form-control" type="number" min={1} value={quantity} onChange={event => setQuantity(Number(event.target.value))} /></label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          quantity < 1 && 'Send at least one.',
        )}
        onClick={() => onAct(() => api.sendAllianceResource(member.playerId, item, quantity))}
      >Confirm</Button>
    </div>}
  </div>
}

const allianceSendItems = [
  { key: 'cash', label: 'Cash' },
  { key: 'thugs', label: 'Thugs' },
  { key: 'weed', label: 'Weed' },
  { key: 'coke', label: 'Coke' },
  { key: 'beer', label: 'Beer' },
  { key: 'medicine', label: 'Medicine' },
  { key: 'poison', label: 'Poison' },
  { key: 'moonshine', label: 'Moonshine' },
  { key: 'cut', label: 'Cut' },
  { key: 'pistols', label: 'Pistols' },
  { key: 'shotguns', label: 'Shotguns' },
  { key: 'smgs', label: 'SMGs' },
  { key: 'rifles', label: 'Rifles' },
]

/**
 * Who is waiting on somebody. Invitations to this player and applications to their crew sit in one
 * list, because from here they are the same thing: an ask with your name on the answer.
 */
function AllianceRequestsPanel({ board, busy, onAct }: {
  board: AllianceBoard
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const answerable = board.requests.filter(x => x.yoursToAnswer)
  // Asks the crew has sent and is still waiting to hear about. Nobody is waiting on you for these, but
  // without them a boss can never see who has been asked or take an ask back.
  const sent = board.requests.filter(x => !x.yoursToAnswer)
  if (answerable.length === 0 && sent.length === 0) return null

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    {sent.length > 0 && <>
      <strong className="d-block mb-1 text-primary small">Asked, waiting to hear</strong>
      {sent.map(ask => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={ask.id}>
        <div className="min-w-0">
          <strong className="d-block text-truncate">{ask.kind === 'Invitation' ? ask.playerName : ask.allianceName}</strong>
          <small className="d-block text-body-secondary">{ask.kind === 'Invitation' ? 'has not answered yet' : 'has not answered your application'}</small>
        </div>
        {ask.kind === 'Invitation'
          ? <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.withdrawAllianceRequest(ask.id))}>Take it back</Button>
          : <small className="text-body-secondary text-sm-end">Waiting on the crew</small>}
      </div>)}
    </>}
    {answerable.length > 0 && <strong className="d-block mb-1 text-primary small">Waiting on you</strong>}
    {answerable.map(ask => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={ask.id}>
      <div className="min-w-0">
        <strong className="d-block text-truncate">{ask.kind === 'Invitation' ? ask.allianceName : ask.playerName}</strong>
        <small className="d-block text-body-secondary">{ask.kind === 'Invitation' ? 'asked you to run with them' : 'is asking for a place'}{ask.note ? ` - "${ask.note}"` : ''}</small>
      </div>
      <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAllianceRequest(ask.id, true))}>Accept</Button>
      <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAllianceRequest(ask.id, false))}>Refuse</Button>
    </div>)}
  </div>
}

/**
 * The war, if there is one, and the record if there is not.
 *
 * A crew was a reason to exist and no reason to act - everything it carried was defensive, and two
 * crews could sit beside each other for a month with nothing to decide. This is the panel where that
 * stops being true, so it says the terms out loud whether or not a war is on: what the fights a crew
 * already fights are worth, what the clock is, and what changes hands at the end.
 */
function AllianceWarPanel({ board, busy }: { board: AllianceBoard, busy: boolean }) {
  const war = board.war
  const terms = board.warTerms

  // The panel keeps its own second hand, like the building does. The app-wide one stops once turns
  // are maxed, which would freeze a war clock for exactly the crews who have stopped earning to fight.
  const [, setNow] = useState(0)
  useEffect(() => {
    if (!war || war.settled) return
    const timer = window.setInterval(() => setNow(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [war?.id, war?.settled])

  if (!board.yours) return null

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-danger small">Wars</strong>
    {war
      ? <div className="d-grid gap-1 border rounded border-danger p-3">
        <div className="d-flex justify-content-between align-items-baseline gap-2">
          <strong>Against {war.opponentName}</strong>
          <em className="eyebrow fst-normal">{timeUntil(war.endsAtUtc)} left</em>
        </div>
        <div className="tnum d-grid gtc-1 gtc-md-3 gap-2 my-2">
          <AdminMetric label="You" value={`${war.yourScore}`} />
          <AdminMetric label="Them" value={`${war.theirScore}`} />
          <AdminMetric label="On the table" value={money.format(war.stake)} />
        </div>
        <span className="text-body-secondary small">
          {war.youDeclared
            ? `${war.declaredByName} declared it, and the stake is yours until somebody wins it.`
            : `${war.opponentName} declared it. The stake is theirs, and it is yours if you beat them.`}
          {' '}A raid won is {terms.pointsForRaidWon}, a raid turned away is {terms.pointsForDefenceHeld},
          and taking ground is {terms.pointsForGroundTaken}. It takes {terms.minScoreToWin} to win
          anything at all, and the winner takes the stake plus {terms.tributePercent}% of the losing
          treasury.
        </span>
        <small className="text-body-tertiary">
          Nothing about a war lifts a protection. The wealth floor, the ratio, the shield on somebody
          who has just been hit and the falling haul on a repeat all still apply - so this is a reason
          to fight, not a licence.
        </small>
      </div>
      : <p className="text-body-secondary small mb-0">
        No war on. Declaring costs the treasury {money.format(terms.stake)} and runs {terms.durationHours} hours;
        the winner takes that back plus {terms.tributePercent}% of the losing crew's treasury, up to{' '}
        {money.format(terms.maxTribute)}. {terms.youCanDeclare
          ? 'Pick a crew off the board below.'
          : 'Somebody who can spend the treasury has to call it.'}
      </p>}
    {board.warHistory.length > 0 && <div className="d-grid gap-1 mt-2">
      {board.warHistory.map(past => <div key={past.id} className="d-flex justify-content-between align-items-baseline gap-2 border-top py-1">
        <small className={past.youWon === true ? 'text-success-emphasis' : past.youWon === false ? 'text-danger-emphasis' : 'text-body-secondary'}>
          {past.youWon === true ? 'Won' : past.youWon === false ? 'Lost' : 'Drew'} against {past.opponentName}
        </small>
        <small className="text-body-tertiary tnum">{past.yourScore}-{past.theirScore}</small>
      </div>)}
    </div>}
    {busy && <small className="text-body-tertiary">Working.</small>}
  </div>
}

function AlliancePactsPanel({ board, busy, onAct }: {
  board: AllianceBoard
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  if (!board.yours || board.pacts.length === 0) return null
  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Allied crews</strong>
    {board.pacts.map(pact => <AlliancePactRow key={pact.id} pact={pact} ownAllianceId={board.yours?.id ?? 0} busy={busy} onAct={onAct} />)}
  </div>
}

function AlliancePactRow({ pact, ownAllianceId, busy, onAct }: {
  pact: AlliancePact
  ownAllianceId: number
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const other = pact.requestingAllianceId === ownAllianceId ? pact.targetAllianceName : pact.requestingAllianceName
  return <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2">
    <div className="min-w-0">
      <strong className="d-block text-truncate">{other}</strong>
      <small className="d-block text-body-secondary">{pact.status === 'Active' ? 'active pact' : 'waiting on an answer'}</small>
    </div>
    {pact.yoursToAnswer
      ? <>
        <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAlliancePact(pact.id, true))}>Accept</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAlliancePact(pact.id, false))}>Refuse</Button>
      </>
      : <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.cancelAlliancePact(pact.id))}>
        {pact.status === 'Active' ? 'Break pact' : 'Take it back'}
      </Button>}
  </div>
}

function AllianceAssistPanel({ board, ownPlayerId, busy, onAct }: {
  board: AllianceBoard
  /** Passed through to the rows: only whoever sent help is offered the button to take it back. */
  ownPlayerId: string
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  if (!board.yours) return null
  /*
    An `and`, not an `or`.

    This read `status === 'Open' || missionStatus !== 'Complete'`, and since nothing ever closed a call,
    an unanswered one on a fight that finished last week passed the first clause and stayed on the page
    for good - offering to send help to a raid long over, and answering "that fight is no longer taking
    help" to anybody who tried. The server closes them now, and this stops showing the closed ones.
  */
  const calls = board.assistCalls.filter(call => call.status !== 'Closed' && call.missionStatus !== 'Complete')
  if (calls.length === 0) return null

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Assist calls</strong>
    {calls.map(call => <AllianceAssistRow key={call.id} call={call} ownAllianceId={board.yours?.id ?? 0} ownPlayerId={ownPlayerId} busy={busy} onAct={onAct} />)}
  </div>
}

function AllianceAssistRow({ call, ownAllianceId, ownPlayerId, busy, onAct }: {
  call: AllianceAssistCall
  ownAllianceId: number
  /** Taking help back is personal: it goes to whoever sent it, not to whoever is looking at the page. */
  ownPlayerId: string
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const canAnswer = call.status === 'Open' && call.allyAllianceId === ownAllianceId && call.missionStatus !== 'Complete'
  // Only the person who sent it, and only once the fight it was sent to has finished.
  const canRecall = call.status === 'Answered'
    && call.missionStatus === 'Complete'
    && call.respondedByPlayerId === ownPlayerId
  const [thugs, setThugs] = useState(0)
  const [pistols, setPistols] = useState(0)
  const [shotguns, setShotguns] = useState(0)
  const [smgs, setSmgs] = useState(0)
  const [rifles, setRifles] = useState(0)
  const sentWeapons = call.pistolsSent + call.shotgunsSent + call.smgsSent + call.riflesSent

  return <div className="d-grid gap-2 border-top py-2">
    <div className="d-flex flex-wrap justify-content-between gap-2">
      <div>
        <strong>{call.defenderName} vs {call.attackerName}</strong>
        <small>{call.defenderAllianceName} called {call.allyAllianceName} / {call.missionStatus}</small>
      </div>
      {call.status !== 'Open' && <em>{call.thugsSent} thugs / {sentWeapons} guns sent</em>}
    </div>
    {canRecall && <div className="d-flex flex-wrap align-items-center gap-2">
      <span className="text-body-tertiary small">
        The fight is over. What you sent still counts as theirs until you take it back, and whatever did
        not survive it is gone.
      </span>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => onAct(() => api.recallAllianceAssist(call.id))}
      >Take back what is left</Button>
    </div>}
    {canAnswer && <div className="d-grid gtc-2 gtc-md-fill-120 gap-2">
      <label className="field">Thugs<input className="form-control" type="number" min={0} value={thugs} onChange={event => setThugs(Number(event.target.value))} /></label>
      <label className="field">Pistols<input className="form-control" type="number" min={0} value={pistols} onChange={event => setPistols(Number(event.target.value))} /></label>
      <label className="field">Shotguns<input className="form-control" type="number" min={0} value={shotguns} onChange={event => setShotguns(Number(event.target.value))} /></label>
      <label className="field">SMGs<input className="form-control" type="number" min={0} value={smgs} onChange={event => setSmgs(Number(event.target.value))} /></label>
      <label className="field">Rifles<input className="form-control" type="number" min={0} value={rifles} onChange={event => setRifles(Number(event.target.value))} /></label>
      <Button
        className="btn btn-primary btn-sm align-self-end"
        blocked={firstReason(
          busy && BUSY,
          thugs + pistols + shotguns + smgs + rifles < 1 && 'Put something in the boxes above. Help with nothing in it is not help.',
        )}
        onClick={() => onAct(() => api.answerAllianceAssist(call.id, thugs, pistols, shotguns, smgs, rifles))}
      >Send help</Button>
    </div>}
  </div>
}

function AllianceTransfersPanel({ transfers }: { transfers: AllianceTransfer[] }) {
  if (transfers.length === 0) return null
  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Recent sends</strong>
    {transfers.slice(0, 6).map(transfer => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={transfer.id}>
      <div>
        <strong>
          <PlayerName playerId={transfer.fromPlayerId}>{transfer.fromPlayerName}</PlayerName>
          {' to '}
          <PlayerName playerId={transfer.toPlayerId}>{transfer.toPlayerName}</PlayerName>
        </strong>
        <small>{transfer.quantity.toLocaleString()} {transfer.label.toLowerCase()}</small>
      </div>
      <em>{new Date(transfer.createdAtUtc).toLocaleString()}</em>
    </div>)}
  </div>
}

/**
 * The shared pool: what the crew has bought, and what this member may borrow of it.
 *
 * The borrow limit is stated on the panel rather than discovered by being refused, because it is the
 * rule that makes the pool interesting - you can bring as many as you brought yourself, so the crew
 * doubles you rather than replacing you.
 */
function AlliancePoolPanel({ board, crew, busy, onAct }: {
  board: AllianceBoard
  crew: AllianceSummary
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const [buy, setBuy] = useState(1)
  const [post, setPost] = useState(1)
  const room = Math.max(0, board.borrowLimit - board.yourDefenders)
  const cities = crew.controlledCities.map(city => `${city.city} +${city.bonusThugs}`).join(' / ')

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <StatusRow label="Pool" value={`${crew.offensiveThugs} offensive / ${crew.defensiveThugs} defensive`} />
    {crew.cityControlThugs > 0 && <StatusRow label="City control" value={`+${crew.cityControlThugs} thugs (${cities})`} />}
    <StatusRow
      label="You may borrow"
      value={board.borrowLimit === 0 ? 'Nothing until you have thugs of your own' : `${board.borrowLimit} (${board.yourDefenders} standing here)`}
      warn={board.borrowLimit === 0}
    />

    {crew.youFounded && <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Buy<input className="form-control" type="number" min={1} value={buy} onChange={event => setBuy(Number(event.target.value))} /></label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          buy < 1 && 'Buy at least one.',
          board.treasury < board.offensiveThugCost * buy && `${number.format(buy)} offensive thugs cost ${money.format(board.offensiveThugCost * buy)} and the treasury holds ${money.format(board.treasury)}.`,
        )}
        onClick={() => onAct(() => api.buyAllianceThugs('offensive', buy))}
      >Offensive {money.format(board.offensiveThugCost * buy)}</Button>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          buy < 1 && 'Buy at least one.',
          board.treasury < board.defensiveThugCost * buy && `${number.format(buy)} defensive thugs cost ${money.format(board.defensiveThugCost * buy)} and the treasury holds ${money.format(board.treasury)}.`,
        )}
        onClick={() => onAct(() => api.buyAllianceThugs('defensive', buy))}
      >Defensive {money.format(board.defensiveThugCost * buy)}</Button>
    </div>}

    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Defenders<input className="form-control" type="number" min={1} value={post} onChange={event => setPost(Number(event.target.value))} /></label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          post < 1 && 'Post at least one.',
          post > room && (board.borrowLimit === 0
            ? 'You can borrow nothing until you have thugs of your own.'
            : `You may borrow ${number.format(board.borrowLimit)} and ${number.format(board.yourDefenders)} of them already stand at your place.`),
          crew.defensiveThugs < post && `The pool has ${number.format(crew.defensiveThugs)} defensive thugs in it and you are posting ${number.format(post)}.`,
        )}
        onClick={() => onAct(() => api.postDefenders(post))}
      >Post to your place</Button>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          post < 1 && 'Send back at least one.',
          board.yourDefenders < post && `You have ${number.format(board.yourDefenders)} of the crew's thugs standing here and you are sending back ${number.format(post)}.`,
        )}
        onClick={() => onAct(() => api.postDefenders(-post))}
      >Send back</Button>
    </div>
    <small className="d-block mt-1 text-body-tertiary small measure">
      Offensive thugs ride along on a raid and defensive ones stand at your place. Both die like anybody
      else, and what dies is gone from the pool for good.
    </small>
  </div>
}

/**
 * The boss's authority: the rate, the door, the sign on it, and where every other line is drawn.
 *
 * The thresholds sit here rather than beside the powers they gate because they are one decision - how
 * much of this crew do I run personally - and a boss changing their mind should not have to make it
 * five times in five places.
 */
function AllianceSettingsPanel({ crew, board, maxDues, busy, onSave }: {
  crew: AllianceSummary
  board: AllianceBoard
  maxDues: number
  busy: boolean
  onSave: (fn: () => Promise<ActionResult>) => void
}) {
  const [crewName, setCrewName] = useState(crew.name)
  const [dues, setDues] = useState(crew.duesPercent)
  const [door, setDoor] = useState<AllianceDoorKey>(crew.door)
  useEffect(() => {
    setCrewName(crew.name)
    setDues(crew.duesPercent)
    setDoor(crew.door)
  }, [crew.id, crew.name, crew.duesPercent, crew.door])
  const nameTicker = useSecondsTicker(!!crew.nameChangeReadyAtUtc)
  const nameCooldownSeconds = secondsUntil(crew.nameChangeReadyAtUtc, nameTicker)

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Who may do what</strong>
    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Crew name
        <input className="form-control" maxLength={32} value={crewName} onChange={event => setCrewName(event.target.value)} />
        <small className="form-text">
          {nameCooldownSeconds > 0
            ? `You can change it again in ${timeUntil(crew.nameChangeReadyAtUtc!)}.`
            : 'Shown on the crew board, rosters, wars, and season tables.'}
        </small>
      </label>
      <Button
        className="btn btn-secondary btn-sm align-self-end"
        blocked={firstReason(
          busy && BUSY,
          crewName.trim().length < 3 && 'A crew name needs at least three characters.',
          crewName.trim().length > 32 && 'A crew name must be 32 characters or less.',
          crewName.trim() === crew.name && 'That is already the crew name.',
          nameCooldownSeconds > 0 && `You can change the crew name again in ${timeUntil(crew.nameChangeReadyAtUtc!)}.`,
        )}
        onClick={() => onSave(() => api.updateAlliance({ name: crewName.trim() }))}
      >Rename</Button>
    </div>
    <div className="alliance-powers d-grid gap-2">
      {board.powers.map(power => <label className="d-grid gap-1 small" key={power.power}>
        <span>{power.label}</span>
        <select className="form-select"
          value={power.minRank}
          disabled={busy}
          onChange={event => onSave(() => api.updateAlliance({ powers: { [power.power]: event.target.value } }))}
        >
          {board.ranks.map(rank => <option key={rank} value={rank}>{rank} and up</option>)}
        </select>
      </label>)}
    </div>
    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Dues %<input className="form-control" type="number" min={0} max={maxDues} value={dues} onChange={event => setDues(Number(event.target.value))} /></label>
      <label className="field">Door
        <select className="form-select" value={door} disabled={busy} onChange={event => setDoor(event.target.value as AllianceDoorKey)}>
          {board.doors.map(option => <option key={option.door} value={option.door}>{option.label}</option>)}
        </select>
      </label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          dues < 0 && 'Dues cannot be negative. The crew pays you, not the other way about.',
          dues > maxDues && `Dues top out at ${maxDues}%.`,
        )}
        onClick={() => onSave(() => api.updateAlliance({ duesPercent: dues, door }))}
      >Save</Button>
    </div>
    <small className="d-block mt-1 text-body-tertiary small measure">
      Dues come off the gross of every member's shift, beside the hoe cut. The ceiling is {maxDues}%.{' '}
      {board.doors.find(x => x.door === door)?.detail}
    </small>
  </div>
}

function BankPanel({ dashboard, busy, bankAmount, setBankAmount, act, className, wide }: {
  dashboard: Dashboard
  busy: boolean
  bankAmount: number
  setBankAmount: (amount: number) => void
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  className?: string
  /**
   * Whether this one is standing on its own.
   *
   * The same panel appears twice: beside the activity list on the street page, where half a row is
   * exactly right, and at the foot of the business page, where nothing sits next to it and the row
   * held 727px open for nothing. Told which it is, rather than guessing from a width it cannot see.
   */
  wide?: boolean
}) {
  /*
    Free while the player is still at the counter from a trip they have already paid for.

    The window closes on the clock rather than on a refetch, so this is decided here against a live
    reading rather than by the field merely being present. The server only sends it while it is still
    standing, but the dashboard it arrived on is cached between refreshes: trusting the field alone
    left the panel offering free moves, and hiding the fare from the buttons, for as long as nothing
    else happened to refresh the page.

    The second hand runs only while there is something to count, and stops itself the moment the
    window is gone - at which point this flips back to the charged case on its own.
  */
  const freeUntil = dashboard.bankTripFreeUntilUtc
  const free = !!freeUntil && new Date(freeUntil).getTime() > Date.now()
  const now = useSecondsTicker(free)
  const secondsFree = secondsUntil(freeUntil, now)
  const fare = free ? 0 : dashboard.bankTripTurnCost
  const cannotAfford = dashboard.turns < fare

  /*
    What the safe has room for, which is a different limit from what the bank is holding.

    The server refuses a withdrawal that would not fit rather than clamping it, so offering the whole
    balance here would be offering a button that cannot work - and now that the trip is charged before
    the money moves, finding that out costs turns. The bank page is also the one place a player meets
    their safe as a wall rather than as a line on the hideout screen, so it is worth naming.
  */
  const safeRoom = Math.max(0, dashboard.hideout.maxCash - dashboard.cash)

  const fareLabel = fare === 0 ? '' : ` (${fare} ${fare === 1 ? 'turn' : 'turns'})`
  // The charge is a live setting an admin can turn off, and a panel announcing "0 turns a trip" would
  // be reporting a rule that is not running. With it off this reads exactly as it did before.
  const charged = dashboard.bankTripTurnCost > 0

  return <section className={`card p-3 ${wide ? 'gcol-full' : ''} ${className ?? ''}`}>
    <div className="panel-title" data-area="bank"><h2>Bank</h2><span>{!charged ? 'Cash handling' : free ? 'Still at the counter' : `${dashboard.bankTripTurnCost} turns a trip`}</span></div>
    <div className={wide ? 'd-grid gtc-1 gtc-lg-2 gap-3 align-items-center' : ''}>
      <div className={wide ? 'mb-0' : ''}>
        <p className="mb-1">Banked cash still counts toward net worth. Combat can steal cash on hand, but bank cash stays protected.</p>
        <p className="text-body-tertiary small mb-0">
          {!charged
            ? <>Your safe has room for {money.format(safeRoom)} more cash on hand.</>
            : free
              ? <>You are still at the bank, so moves are free for another {countdown(secondsFree)}.</>
              : <>A trip costs {dashboard.bankTripTurnCost} {dashboard.bankTripTurnCost === 1 ? 'turn' : 'turns'}, and everything you move while you are there is on the same trip. Your safe has room for {money.format(safeRoom)} more.</>}
        </p>
      </div>
      <div className="control-row">
        <label className="field">Amount<input className="form-control" type="number" min={1} value={bankAmount} onChange={e => setBankAmount(Number(e.target.value))} /></label>
        <Button
          className="btn btn-secondary"
          blocked={firstReason(
            busy && BUSY,
            cannotAfford && `A trip to the bank costs ${fare} turns and you have ${dashboard.turns}.`,
            bankAmount < 1 && 'Bank at least a dollar.',
            bankAmount > dashboard.cash && `You are banking ${money.format(bankAmount)} and you are carrying ${money.format(dashboard.cash)}.`,
          )}
          onClick={() => void act(() => api.deposit(bankAmount))}>Deposit{fareLabel}</Button>
        <Button
          className="btn btn-secondary"
          blocked={firstReason(
            busy && BUSY,
            cannotAfford && `A trip to the bank costs ${fare} turns and you have ${dashboard.turns}.`,
            bankAmount < 1 && 'Draw out at least a dollar.',
            bankAmount > dashboard.bankCash && `You are drawing ${money.format(bankAmount)} and the bank holds ${money.format(dashboard.bankCash)}.`,
            bankAmount > safeRoom && `Your safe only has room for ${money.format(safeRoom)} more.`,
          )}
          onClick={() => void act(() => api.withdraw(bankAmount))}>Withdraw{fareLabel}</Button>
      </div>
    </div>
  </section>
}

function CombatHistoryPanel({ entries, currentPlayerId }: { entries: CombatLog[], currentPlayerId: string }) {
  return <section className="card p-3">
    <div className="panel-title"><h2>Combat History</h2><span>Last {entries.length}</span></div>
    <div className="combat-history d-grid overflow-y-auto">
      {entries.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No fights yet.</p>}
      {entries.map(entry => {
        const attacking = entry.attackerId === currentPlayerId
        const pending = entry.outcome === 'Pending'
        return <div className={`${attacking ? 'combat-entry attack' : 'combat-entry defense'}${pending ? ' pending' : ''}`} key={entry.id}>
          <div className="combat-entry-meta"><strong>{entry.methodLabel} / {entry.outcome}</strong><span>{new Date(entry.createdAtUtc).toLocaleString()}</span></div>
          <p>{entry.summary}</p>
          {/* Power is a raid's story. A strike never rolls one, so quoting 0-0 for one would be noise. */}
          <small>{entry.attackerName} vs {entry.defenderName} / {pending && entry.resolvesAtUtc
            ? `ETA ${timeUntil(entry.resolvesAtUtc)}`
            : entry.method === 'raid'
              ? `${entry.attackerPower}-${entry.defenderPower} power`
              : attacking ? 'Your strike' : 'Struck you'}</small>
        </div>
      })}
    </div>
  </section>
}

/**
 * Everything about the AI rivals in one place: how many exist, running them by hand, and the
 * automatic loop. The loop's timing is editable here because it was previously fixed at startup from
 * appsettings, so tuning it meant a restart, and the on/off switch lived only in memory so a restart
 * silently reverted it.
 */
/**
 * Tells one rival exactly what to do next, so a scenario can be set up rather than waited for.
 * Everything runs through the same services a real player's action does, so a refusal here is a real
 * game rule refusing, which is often the thing being tested.
 */
function BotDirectivePanel({ bot, targets, selfId, selfName, busy, onRun }: {
  bot: AdminBotHealth
  targets: AdminBotHealth[]
  selfId: string
  selfName: string
  busy: boolean
  onRun: (directive: BotDirective) => void
}) {
  const [action, setAction] = useState('street')
  const [turns, setTurns] = useState(10)
  const [quantity, setQuantity] = useState(10)
  const [amount, setAmount] = useState(10000)
  const [product, setProduct] = useState('weed')
  const [item, setItem] = useState('condoms')
  const [role, setRole] = useState('hoes')
  const [strategy, setStrategy] = useState('rest')
  const [room, setRoom] = useState('storage')
  const [defenderId, setDefenderId] = useState(selfId)

  const directive = (): BotDirective => {
    switch (action) {
      case 'street': return { action, turns }
      case 'produce': return { action, product, turns }
      case 'sell': return { action, product, quantity }
      case 'buy': return { action, item, quantity }
      case 'hire': case 'fire': return { action, role, quantity }
      case 'deposit': case 'withdraw': return { action, amount }
      case 'recover': return { action, strategy }
      case 'upgrade': return { action, room }
      case 'attack': return { action, defenderId, thugs: quantity, weapons: quantity }
      default: return { action }
    }
  }

  return <div className="mt-3 border rounded bg-body-tertiary p-3">
    <div className="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-baseline gap-1 gap-md-3">
      <strong>Direct {bot.name}</strong>
      <span className="eyebrow">Runs through the real rules, so a refusal is the game refusing</span>
    </div>
    <div className="control-row mt-2">
      <label className="field">Action<select className="form-select" value={action} onChange={e => setAction(e.target.value)}>
        <option value="street">Work the streets</option>
        <option value="produce">Produce</option>
        <option value="sell">Sell product</option>
        <option value="buy">Buy supplies</option>
        <option value="hire">Hire crew</option>
        <option value="fire">Fire crew</option>
        <option value="deposit">Deposit</option>
        <option value="withdraw">Withdraw</option>
        <option value="recover">Recover morale</option>
        <option value="upgrade">Upgrade hideout</option>
        <option value="attack">Attack someone</option>
      </select></label>

      {(action === 'street' || action === 'produce') &&
        <label className="field">Turns<input className="form-control" type="number" min={1} max={20} value={turns} onChange={e => setTurns(Number(e.target.value))} /></label>}
      {(action === 'produce' || action === 'sell') &&
        <label className="field">Product<select className="form-select" value={product} onChange={e => setProduct(e.target.value)}>
          <option value="weed">Weed</option><option value="coke">Coke</option>
        </select></label>}
      {action === 'buy' &&
        <label className="field">Item<select className="form-select" value={item} onChange={e => setItem(e.target.value)}>
          {/* The store sells guns by tier, so this offers them by tier. "weapons" was refused. */}
          <option value="condoms">Condoms</option><option value="beer">Beer</option><option value="medicine">Medicine</option>
          <option value="pistols">Pistols</option><option value="shotguns">Shotguns</option>
          <option value="smgs">SMGs</option><option value="rifles">Rifles</option>
        </select></label>}
      {(action === 'hire' || action === 'fire') &&
        <label className="field">Role<select className="form-select" value={role} onChange={e => setRole(e.target.value)}>
          <option value="pimps">Pimps</option><option value="hoes">Hoes</option><option value="thugs">Thugs</option>
        </select></label>}
      {(action === 'sell' || action === 'buy' || action === 'hire' || action === 'fire') &&
        <label className="field">Quantity<input className="form-control" type="number" min={1} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>}
      {(action === 'deposit' || action === 'withdraw') &&
        <label className="field">Amount<input className="form-control" type="number" min={1} step={1000} value={amount} onChange={e => setAmount(Number(e.target.value))} /></label>}
      {action === 'recover' &&
        <label className="field">Strategy<select className="form-select" value={strategy} onChange={e => setStrategy(e.target.value)}>
          <option value="rest">Rest</option><option value="party">Party</option>
        </select></label>}
      {action === 'upgrade' &&
        <label className="field">Room<select className="form-select" value={room} onChange={e => setRoom(e.target.value)}>
          <option value="tier">Building tier</option><option value="storage">Storage</option>
          <option value="safe">Safe</option><option value="weedlab">Weed lab</option><option value="cokelab">Coke lab</option>
        </select></label>}
      {action === 'attack' && <>
        <label className="field">Target<select className="form-select" value={defenderId} onChange={e => setDefenderId(e.target.value)}>
          <option value={selfId}>{selfName} (you)</option>
          {targets.map(t => <option key={t.playerId} value={t.playerId}>{t.name}</option>)}
        </select></label>
        <label className="field">Thugs<input className="form-control" type="number" min={1} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>
      </>}

      <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => onRun(directive())}>Do it</Button>
    </div>
  </div>
}

function AdminAiTab({ ctx }: { ctx: PageContext & { overview: AdminOverview } }) {
  const { overview, busy, seedBots, runBots, setBotAutomation } = ctx
  const auto = overview.botAutomation
  const [seedCount, setSeedCount] = useState(10)
  const [runRounds, setRunRounds] = useState(1)
  const [tickSeconds, setTickSeconds] = useState(auto.tickSeconds)
  const [roundsPerTick, setRoundsPerTick] = useState(auto.roundsPerTick)
  const [roster, setRoster] = useState<AdminBotHealth[]>([])
  const [rosterError, setRosterError] = useState('')
  const [working, setWorking] = useState<string | null>(null)
  const [directing, setDirecting] = useState<string | null>(null)

  // Re-reads the roster rather than patching it locally, so an action's real effect on net worth and
  // idle time shows up instead of just the flag that was toggled.
  const rivalAction = async (playerId: string, run: () => Promise<unknown>) => {
    setWorking(playerId); setRosterError('')
    try {
      await run()
      setRoster((await opsApi.oversight()).bots)
    } catch (e) { setRosterError((e as Error).message) }
    finally { setWorking(null) }
  }

  // Follow the server whenever it reports different timings, so an edit made elsewhere does not leave
  // stale numbers sitting in the inputs.
  useEffect(() => { setTickSeconds(auto.tickSeconds); setRoundsPerTick(auto.roundsPerTick) }, [auto.tickSeconds, auto.roundsPerTick])
  useEffect(() => {
    opsApi.oversight()
      .then((data: AdminOversight) => setRoster(data.bots))
      .catch((e: unknown) => setRosterError((e as Error).message))
  }, [overview.generatedAtUtc])

  const timingChanged = tickSeconds !== auto.tickSeconds || roundsPerTick !== auto.roundsPerTick
  const timingValid = tickSeconds >= auto.minTickSeconds && tickSeconds <= auto.maxTickSeconds
    && roundsPerTick >= auto.minRoundsPerTick && roundsPerTick <= auto.maxRoundsPerTick
  const atDefaults = auto.tickSeconds === auto.defaultTickSeconds && auto.roundsPerTick === auto.defaultRoundsPerTick

  return <>
    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>Automatic AI</h2>
        <span>{auto.enabled ? `On, ${auto.roundsPerTick} round(s) every ${auto.tickSeconds}s` : 'Off'}</span>
      </div>
      <p>
        Rivals act on their own on this loop. The setting is saved, so it survives a restart, and the
        timing takes effect on the next tick without one.
      </p>
      <div className="control-row">
        <Button
          className={auto.enabled ? 'btn btn-secondary btn-sm' : 'btn btn-primary btn-sm'}
          blocked={firstReason(
            busy && WORKING,
            overview.botAccounts < 1 && 'There are no rivals for the loop to run. Seed some below first.',
          )}
          onClick={() => setBotAutomation(!auto.enabled)}
        >
          {auto.enabled ? 'Turn off' : 'Turn on'}
        </Button>
        <label className="field">Tick seconds<input className="form-control"
          type="number"
          min={auto.minTickSeconds}
          max={auto.maxTickSeconds}
          value={tickSeconds}
          onChange={e => setTickSeconds(Number(e.target.value))}
        /></label>
        <label className="field">Rounds per tick<input className="form-control"
          type="number"
          min={auto.minRoundsPerTick}
          max={auto.maxRoundsPerTick}
          value={roundsPerTick}
          onChange={e => setRoundsPerTick(Number(e.target.value))}
        /></label>
        <Button
          className="btn btn-secondary btn-sm"
          blocked={firstReason(
            busy && WORKING,
            !timingChanged && 'The timing is already what is saved.',
            !timingValid && `Tick has to be ${auto.minTickSeconds}-${auto.maxTickSeconds}s and rounds ${auto.minRoundsPerTick}-${auto.maxRoundsPerTick}.`,
          )}
          onClick={() => setBotAutomation(auto.enabled, { tickSeconds, roundsPerTick })}
        >
          Save timing
        </Button>
        <Button
          className="btn btn-secondary btn-sm"
          blocked={firstReason(
            busy && WORKING,
            atDefaults && 'The timing is already at the defaults.',
          )}
          onClick={() => setBotAutomation(auto.enabled, { resetTiming: true })}
        >
          Reset to {auto.defaultTickSeconds}s / {auto.defaultRoundsPerTick}
        </Button>
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Seed and Run</h2><span>{number.format(overview.botAccounts)} rivals exist</span></div>
      <div className="control-row">
        <label className="field">Seed count<input className="form-control" type="number" min={1} max={15} value={seedCount} onChange={e => setSeedCount(Number(e.target.value))} /></label>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setSeedCount(5)}>5</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setSeedCount(10)}>10</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setSeedCount(15)}>15</Button>
        <Button className="btn btn-primary btn-sm" blocked={firstReason(
          busy && WORKING,
          (seedCount < 1 || seedCount > 15) && 'Seed between 1 and 15 rivals at a time.',
        )} onClick={() => seedBots(seedCount)}>Seed rivals</Button>
      </div>
      <div className="control-row">
        <label className="field">Rounds<input className="form-control" type="number" min={1} max={10} value={runRounds} onChange={e => setRunRounds(Number(e.target.value))} /></label>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setRunRounds(1)}>1</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setRunRounds(3)}>3</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setRunRounds(10)}>10</Button>
        <Button className="btn btn-primary btn-sm" blocked={firstReason(
          busy && WORKING,
          overview.botAccounts < 1 && 'There are no rivals to run. Seed some first.',
          (runRounds < 1 || runRounds > 10) && 'Run between 1 and 10 rounds at a time.',
        )} onClick={() => runBots(runRounds)}>Run now</Button>
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The Rivals</h2><span>Personality and playing habits</span></div>
      {rosterError && <div className="alert alert-danger"><span>{rosterError}</span></div>}
      {roster.length === 0 && !rosterError && <p className="text-body-tertiary small mt-3 mb-0">No AI rivals yet.</p>}
      {roster.length > 0 && <div className="table-responsive mt-3"><table className="table table-sm table-hover align-middle game-table">
        <thead><tr><th>Name</th><th>Personality</th><th>Net worth</th><th>Idle</th><th>Habits</th><th>State</th><th /></tr></thead>
        <tbody>
          {roster.map(bot => <tr key={bot.playerId} className={rivalRowClass(bot)}>
            <td>{bot.name}</td>
            <td>{bot.personality}</td>
            <td>{money.format(bot.netWorth)}</td>
            <td>{bot.lastActionAtUtc ? `${number.format(bot.minutesIdle)}m` : 'never acted'}</td>
            <td>{bot.habits}</td>
            <td>{bot.isPaused ? 'Paused' : botPresence(bot)}</td>
            <td className="d-flex gap-1">
              <Button
                className="btn btn-secondary btn-sm"
                blocked={working === bot.playerId && `${bot.name} is mid-action. Wait for it to land.`}
                onClick={() => void rivalAction(bot.playerId, () => opsApi.setBotPaused(bot.playerId, !bot.isPaused))}
              >
                {bot.isPaused ? 'Resume' : 'Pause'}
              </Button>
              <Button
                className="btn btn-secondary btn-sm"
                blocked={firstReason(
                  working === bot.playerId && `${bot.name} is mid-action. Wait for it to land.`,
                  bot.isPaused && `${bot.name} is paused. Resume them first.`,
                )}
                title="Act now, ignoring the cooldown"
                onClick={() => void rivalAction(bot.playerId, () => opsApi.actNow(bot.playerId))}
              >
                Act now
              </Button>
              <Button
                className="btn btn-secondary btn-sm"
                blocked={working === bot.playerId && `${bot.name} is mid-action. Wait for it to land.`}
                onClick={() => setDirecting(id => id === bot.playerId ? null : bot.playerId)}
              >
                {directing === bot.playerId ? 'Close' : 'Direct'}
              </Button>
            </td>
          </tr>)}
        </tbody>
      </table></div>}
      {directing && <BotDirectivePanel
        bot={roster.find(x => x.playerId === directing)!}
        targets={roster.filter(x => x.playerId !== directing)}
        selfId={ctx.dashboard.playerId}
        selfName={ctx.dashboard.name}
        busy={working === directing}
        onRun={directive => void rivalAction(directing, () => opsApi.directBot(directing, directive))}
      />}
    </section>
  </>
}

function MiniInventory({ dashboard }: { dashboard: Dashboard }) {
  return <div className="tnum d-grid">
    <StatusRow label="Condoms" value={number.format(dashboard.condoms)} />
    <StatusRow label="Beer" value={number.format(dashboard.beer)} />
    <StatusRow label="Weapons" value={weaponSummary(dashboard)} warn={dashboard.weapons < dashboard.thugs} />
    <StatusRow label="Medicine" value={number.format(dashboard.medicine)} warn={dashboard.hoes > 0 && dashboard.medicine === 0} />
    <StatusRow label="Rides" value={number.format(dashboard.rides)} />
    <StatusRow label="Weed" value={number.format(dashboard.weed)} />
    <StatusRow label="Coke" value={number.format(dashboard.coke)} />
  </div>
}

/**
 * Two ladders behind one toggle, home first.
 *
 * Eight towns on a single global board means most players never appear on it and never will, so the
 * town they chose is the only place their standing is legible. The global board still exists, because
 * being seventieth in the world is worth knowing once you are first at home.
 */
function StandingsPanel({ dashboard, leaders, cityLeaders, limit }: {
  dashboard: Dashboard
  leaders: LeaderboardEntry[]
  cityLeaders: LeaderboardEntry[]
  limit: number
}) {
  const [scope, setScope] = useState<'city' | 'world'>('city')
  const rows = scope === 'city' ? cityLeaders : leaders
  const standing = scope === 'city'
    ? `#${dashboard.cityRank} of ${number.format(dashboard.cityPlayers)} in ${dashboard.city}`
    : `#${dashboard.rank} in the world`

  return <>
    <div className="panel-title">
      <h2>Standings</h2>
      <span>{standing}</span>
    </div>
    {/*
      Home board or the whole world. Bootstrap's button group, which is what a pair of controls where
      exactly one is chosen already is.
    */}
    <div className="btn-group w-100 mb-2" role="group" aria-label="Standings scope">
      <button
        type="button"
        className={`btn btn-sm ${scope === 'city' ? 'scope-on' : 'btn-secondary text-body-secondary'}`}
        aria-pressed={scope === 'city'}
        onClick={() => setScope('city')}
      >{dashboard.city}</button>
      <button
        type="button"
        className={`btn btn-sm ${scope === 'world' ? 'scope-on' : 'btn-secondary text-body-secondary'}`}
        aria-pressed={scope === 'world'}
        onClick={() => setScope('world')}
      >Everywhere</button>
    </div>
    {rows.length === 0
      ? <p className="text-body-tertiary small mt-3 mb-0">Nobody else has set up in {dashboard.city} yet. That makes you first.</p>
      : <Leaderboard leaders={rows.slice(0, limit)} currentPlayerId={dashboard.playerId} />}
  </>
}

function Leaderboard({ leaders, currentPlayerId }: { leaders: LeaderboardEntry[], currentPlayerId: string }) {
  return <div className="leaderboard tnum d-grid overflow-y-auto">
    {leaders.map(l => <div
      className={`leader d-grid gap-2 p-2 border-top ${l.playerId === currentPlayerId ? 'bg-success-subtle' : ''}`}
      key={l.rank}
    >
      <span className="text-body-secondary">#{l.rank}</span>
      <span className="d-flex align-items-center gap-2 min-w-0">
        <PlayerAvatar name={l.playerName} avatarUrl={l.avatarUrl} size={30} />
        <span className="d-grid min-w-0">
          <strong className="min-w-0 text-truncate"><PlayerName playerId={l.playerId}>{l.playerName}</PlayerName></strong>
          {l.profileTagline && <small className="text-body-tertiary text-truncate">{l.profileTagline}</small>}
        </span>
      </span>
      <span className="text-body-secondary">{money.format(l.netWorth)}</span>
    </div>)}
  </div>
}

function ActivityList({ entries }: { entries: { id: number, action: string, createdAtUtc: string, summary: string }[] }) {
  return <div className="d-grid">
    {entries.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No activity yet.</p>}
    {entries.map(a => <div className="feed-item py-3 border-top" key={a.id}>
      <div className="d-flex flex-column flex-sm-row justify-content-between gap-1 gap-sm-2">
        <strong className="text-primary">{a.action}</strong>
        <span className="text-body-tertiary small text-sm-end">{new Date(a.createdAtUtc).toLocaleString()}</span>
      </div>
      <p className="mt-1 mb-0">{a.summary}</p>
    </div>)}
  </div>
}

/** `sub` is for a tile whose number needs a word under it - confirmed, connected, none set. */
function AdminMetric({ label, value, sub }: { label: string, value: string, sub?: string }) {
  return <div className="d-grid gap-1 border rounded bg-body-secondary px-3 py-2">
    <span className="eyebrow">{label}</span>
    <strong className="min-w-0 fs-5 text-break">{value}</strong>
    {sub && <small className="small text-body-tertiary">{sub}</small>}
  </div>
}

// The stripe down the side of a headline, keyed by what the story is about. Held
// as a map rather than as five CSS classes, because the colour is Bootstrap's and
// the only decision left is which of its six a given kind of news belongs to.
const HEADLINE_EDGE: Record<string, string> = {
  leader: 'border-start-warning',
  robbery: 'border-start-danger',
  score: 'border-start-success',
  arrival: 'border-start-info',
  ground: 'border-start-primary',
}

// What a line of world news is, in one word, and the colour it is said in.
const NEWS_TONE: Record<WorldNewsEntry['category'], string> = {
  combat: 'text-danger',
  build: 'text-warning',
  arrival: 'text-info',
  crew: 'text-success',
  money: 'text-primary',
  ground: 'text-primary',
  casino: 'text-warning',
}

const NEWS_LABELS: Record<WorldNewsEntry['category'], string> = {
  combat: 'Fight',
  build: 'Built',
  arrival: 'Arrival',
  crew: 'Crew',
  money: 'Money',
  ground: 'Ground',
  casino: 'Casino'
}

function WorldNewsPanel({ news, currentPlayerId }: { news: WorldNews, currentPlayerId: string }) {
  const entries = news.feed.slice(0, 8)
  return <div className="card p-3 gcol-full">
    <div className="panel-title"><h2>World News</h2><span>What is worth knowing</span></div>
    {news.headlines.length > 0 && <div className="d-grid gtc-fill-210 gap-2 mt-3 mb-1">
      {news.headlines.map(headline => <div
        className={`d-grid gtc-1 gap-1 border rounded-1 bg-body-tertiary px-3 py-2 border-start-thick ${HEADLINE_EDGE[headline.kind] ?? 'border-start-secondary'}`}
        key={headline.kind}
      >
        <strong className="text-break">{headline.title}</strong>
        <span className="text-body-secondary small">{headline.detail}</span>
      </div>)}
    </div>}
    <div className="world-news d-grid overflow-y-auto">
      {entries.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nothing worth reporting yet. Small moves stay off the page.</p>}
      {entries.map(entry => <div
        className={`feed-item py-2 border-top ${entry.playerId === currentPlayerId ? 'mine' : ''}`}
        key={entry.id}
      >
        <div className="d-flex flex-column flex-sm-row justify-content-between gap-1 gap-sm-2">
          <strong className={`small fw-bold ${NEWS_TONE[entry.category] ?? 'text-body-secondary'}`}>{NEWS_LABELS[entry.category] ?? entry.action}</strong>
          <span className="text-body-tertiary small text-sm-end">{new Date(entry.createdAtUtc).toLocaleString()}</span>
        </div>
        <p className="my-1">{entry.summary}</p>
        <small className="text-body-tertiary small"><PlayerName playerId={entry.playerId}>{entry.playerName}</PlayerName> / {entry.city}{entry.turnsSpent > 0 ? ` / ${entry.turnsSpent} turn${entry.turnsSpent === 1 ? '' : 's'}` : ''}</small>
      </div>)}
    </div>
  </div>
}

function percent(value: number) {
  return `${(value * 100).toFixed(value < 0.1 ? 1 : 0)}%`
}

function combatProtectionText(status: { isProtected: boolean, protectionUntilUtc?: string | null }) {
  if (!status.isProtected || !status.protectionUntilUtc) return 'None'
  return `Until ${new Date(status.protectionUntilUtc).toLocaleString()}`
}

function attackStatusText(status: { canAttackNow: boolean, eligibility: string, attackTurnCost: number, attackCooldownUntilUtc?: string | null, mismatchReason?: string | null }, missionAgainstTarget?: CombatMission, activeMission?: CombatMission, attackReady = true) {
  if (missionAgainstTarget) return `Mission active, next update in ${timeUntil(nextMissionTime(missionAgainstTarget))}`
  // A mismatch is a hard block, so say why before anything else the player could act on.
  if (status.mismatchReason) return status.mismatchReason
  if (!attackReady) return 'Assign available crew'
  if (activeMission) return `Crew already out, next update in ${timeUntil(nextMissionTime(activeMission))}`
  if (status.canAttackNow) return `${status.attackTurnCost} turns to attack`
  if (status.attackCooldownUntilUtc && status.eligibility === 'Cooldown') return `Cooldown until ${new Date(status.attackCooldownUntilUtc).toLocaleString()}`
  return status.eligibility
}

/**
 * The status line for a strike. Its own function rather than a flag on attackStatusText, which quotes
 * the raid's turn cost and knows about lanes and missions: a strike has neither, and telling somebody a
 * six-turn drive-by costs ten turns is worse than saying nothing.
 */
function strikeStatusText(method: AttackMethod, dashboard: Dashboard, status: { canAttackNow: boolean, eligibility: string, mismatchReason?: string | null }) {
  if (status.mismatchReason) return status.mismatchReason
  if (dashboard.turns < method.turnCost) return `${method.turnCost} turns needed, you have ${dashboard.turns}`
  if (status.canAttackNow) return `${method.turnCost} turns, settles on the spot`
  return status.eligibility
}

function commanderNote(pimp: Pimp | null) {
  if (!pimp) return 'Server fields your strongest enforcer'
  return pimp.specialty === 'Enforcer'
    ? `+${pimp.bonusPercent}% attack power while leading`
    : `Hustler: no combat bonus, and their +${pimp.bonusPercent}% street income stays home`
}

function nextMissionTime(mission: CombatMission) {
  return mission.status === 'Traveling'
    ? mission.arrivesAtUtc
    : mission.status === 'Fighting'
      ? mission.nextRoundAtUtc ?? mission.arrivesAtUtc
      : mission.status === 'Returning'
        ? mission.returnsAtUtc ?? mission.arrivesAtUtc
        : mission.completedAtUtc ?? mission.returnsAtUtc ?? mission.arrivesAtUtc
}

function timeUntil(value: string) {
  const seconds = Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000))
  const minutes = Math.floor(seconds / 60)
  const remainder = seconds % 60
  // Hideout builds run for hours, where a bare minute count stops being readable.
  if (minutes >= 60) return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m`
  return minutes <= 0 ? `${seconds}s` : `${minutes}m ${String(remainder).padStart(2, '0')}s`
}

/**
 * A countdown long enough to be a season.
 *
 * timeUntil tops out at hours, which is right for everything it was written for - a build, a mission,
 * a shift, all of which finish inside a day. A season runs for a month, and "719h 04m" is not a number
 * anybody reads as a date. Days first here, and the minutes only once the days have gone.
 */
function timeLeft(value: string) {
  const seconds = Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000))
  const days = Math.floor(seconds / 86_400)
  return days > 0 ? `${days}d ${Math.floor((seconds % 86_400) / 3600)}h` : timeUntil(value)
}

function formatCraftMinutes(minutes: number) {
  if (minutes >= 60) return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m`
  return `${Math.max(1, minutes)}m`
}

function formatBreakdownKey(key: string) {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, value => value.toUpperCase())
}

function formatBreakdownValue(key: string, value: unknown) {
  if (typeof value === 'number') {
    const moneyKey = /gross|payout|profit|cost|price|total|amount/i.test(key)
    if (moneyKey) return money.format(value)
    return Number.isInteger(value) ? number.format(value) : value.toFixed(2)
  }
  return String(value)
}

const ACCOUNT_TABS = ['profile', 'display', 'signin', 'invites', 'privacy', 'alerts', 'security'] as const
type AccountTab = typeof ACCOUNT_TABS[number]

const ACCOUNT_TAB_META: Record<AccountTab, { label: string, kicker: string }> = {
  profile: { label: 'Profile', kicker: 'Who you are here' },
  display: { label: 'Display', kicker: 'How this device shows it' },
  signin: { label: 'Account', kicker: 'Name and sign-in' },
  invites: { label: 'Invites', kicker: 'Beta keys you hold' },
  privacy: { label: 'Privacy', kicker: 'Discord and messages' },
  alerts: { label: 'Alerts', kicker: 'Email and sync' },
  security: { label: 'Security', kicker: 'Sessions and last doors' },
}

const PROFILE_ACCENTS: Account['profileAccent'][] = ['Gold', 'Teal', 'Rose', 'Steel']

function bannerClass(banner: ProfileBanner) {
  return banner === 'None' ? 'border' : `profile-banner-${banner.toLowerCase()}`
}

/**
 * How this device shows the game, which is not a fact about the account: a phone and a monitor want
 * different densities, and reduced motion belongs to the machine that is doing the moving. Kept in
 * localStorage for that reason - see preferences.ts.
 */
function AccountDisplayPanel() {
  const [preferences, setPreferences] = useState<Preferences>(loadPreferences)

  const change = (next: Preferences) => {
    setPreferences(next)
    savePreferences(next)
    applyPreferences(next)
  }

  const systemReduced = systemPrefersReducedMotion()

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title"><h2>Display</h2><span>This device only</span></div>
    <p className="text-body-secondary">
      Kept on this device rather than on your account, because the answers are usually different on a
      phone and on a monitor. Signing in somewhere else starts from that machine's own settings.
    </p>

    <div className="d-grid gap-3">
      <label className="form-check form-switch d-flex align-items-start gap-2 m-0">
        <input
          className="form-check-input flex-shrink-0"
          type="checkbox"
          role="switch"
          checked={preferences.compact}
          onChange={event => change({ ...preferences, compact: event.target.checked })}
        />
        <span className="min-w-0">
          <strong className="d-block">Compact</strong>
          <small className="text-body-tertiary">
            Tighter rows and padding on the long lists - the leaderboard, the feed, the market. Buttons
            and the tab bar keep their size, since a smaller target is a harder one to hit.
          </small>
        </span>
      </label>

      <label className="form-check form-switch d-flex align-items-start gap-2 m-0">
        <input
          className="form-check-input flex-shrink-0"
          type="checkbox"
          role="switch"
          checked={preferences.reduceMotion ?? systemReduced}
          onChange={event => change({ ...preferences, reduceMotion: event.target.checked })}
        />
        <span className="min-w-0">
          <strong className="d-block">Reduce animations</strong>
          <small className="text-body-tertiary">
            {preferences.reduceMotion === null
              ? `Following this device, which currently asks for ${systemReduced ? 'reduced' : 'full'} motion.`
              : 'Set here, ignoring what this device asks for.'}
          </small>
        </span>
      </label>

      {preferences.reduceMotion !== null && <div>
        <button
          className="btn btn-link p-0 text-body-secondary"
          type="button"
          onClick={() => change({ ...preferences, reduceMotion: null })}
        >Follow this device instead</button>
      </div>}
    </div>

    <hr className="my-3" />
    <p className="text-body-tertiary small mb-0">
      The game is dark and only dark for now. A light theme is not a switch here: every panel, input and
      table colour is compiled into the stylesheet as a dark value, so light means authoring a second
      palette rather than flipping one.
    </p>
  </section>
}

function AccountInvitesPanel({ busy }: { busy: boolean }) {
  const [keys, setKeys] = useState<AccountInviteKey[]>([])
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)
  const available = keys.filter(key => key.status === 'Available' && key.usesLeft > 0)

  const load = async () => {
    setLoading(true); setError('')
    try {
      const board = await api.invites()
      setKeys(board.keys)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }
  useEffect(() => { void load() }, [])

  const copy = async (value: string, said: string) => {
    try {
      await copyToClipboard(value)
      setMessage(said)
    } catch { setError('Could not copy to the clipboard.') }
  }

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title">
      <h2>Invites</h2>
      <span>{loading ? 'Reading' : `${available.length} available`}</span>
    </div>
    {(error || message) && <div className="d-grid gap-2 mb-3">
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
    </div>}

    <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
      <div className="tnum d-grid gtc-3 gap-2">
        <AdminMetric label="Total" value={number.format(keys.length)} />
        <AdminMetric label="Available" value={number.format(available.length)} />
        <AdminMetric label="Used" value={number.format(keys.filter(key => key.status === 'Used').length)} />
      </div>
      <Button
        className="btn btn-outline-primary"
        type="button"
        blocked={firstReason(
          busy && BUSY,
          available.length === 0 && 'You have no unused invites left to copy.',
        )}
        onClick={() => void copy(available.map(key => key.displayCode).join('\n'), 'Available invites copied.')}
      >Copy Available</Button>
    </div>

    <div className="table-responsive">
      <table className="table table-sm align-middle mb-0">
        <thead>
          <tr>
            <th>Key</th>
            <th>Status</th>
            <th>Uses</th>
            <th>Redeemed by</th>
            <th>Dates</th>
            <th className="text-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {!loading && keys.length === 0 && <tr>
            <td colSpan={6} className="text-body-tertiary">No invites have been issued to this account.</td>
          </tr>}
          {loading && <tr><td colSpan={6} className="text-body-tertiary">Reading your invites.</td></tr>}
          {keys.map(key => <tr key={key.id}>
            <td className="tnum">
              <strong>{key.displayCode}</strong>
              {key.label && <small className="d-block text-body-tertiary text-truncate">{key.label}</small>}
            </td>
            <td><span className={`badge ${betaKeyStatusClass(key.status)}`}>{key.status}</span></td>
            <td className="tnum">{key.uses} / {key.maxUses}<small className="d-block text-body-tertiary">{key.usesLeft} left</small></td>
            <td className="small">{key.redeemedByPlayerName ?? 'Not redeemed'}</td>
            <td className="small">
              <span className="d-block">Made {compactDateTime(key.createdAtUtc)}</span>
              <span className="d-block text-body-tertiary">Redeemed {compactDateTime(key.redeemedAtUtc)}</span>
            </td>
            <td className="text-end">
              <button className="btn btn-outline-secondary btn-sm" type="button" onClick={() => void copy(key.displayCode, 'Invite copied.')}>
                Copy
              </button>
            </td>
          </tr>)}
        </tbody>
      </table>
    </div>
  </section>
}

function profileAccentClass(accent: Account['profileAccent'] | PlayerTarget['profileAccent']) {
  return accent === 'Teal'
    ? 'text-info'
    : accent === 'Rose'
      ? 'text-danger'
      : accent === 'Steel'
        ? 'text-body-secondary'
        : 'text-primary'
}

/**
 * A clock that ticks while something is counting down, and stops when nothing is.
 *
 * The verification panel has three deadlines running at once - the code expiring, the resend
 * cooldown, and neither - and a component that re-renders once a second forever to show a countdown
 * that is not there is a component quietly burning a laptop battery on a settings page.
 */
function useSecondsTicker(active: boolean) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [active])
  return now
}

/** Whole seconds between now and a deadline, floored at zero. */
function secondsUntil(iso: string | null | undefined, now: number) {
  if (!iso) return 0
  return Math.max(0, Math.ceil((new Date(iso).getTime() - now) / 1000))
}

function countdown(seconds: number) {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${String(s).padStart(2, '0')}`
}

/**
 * The account tab.
 *
 * Everything a player owns hangs off one account, and until recently the only thing holding that
 * account was a username and a password chosen on the day they signed up, with no way to change
 * either and nowhere to look at them. This is that place.
 *
 * The rule the whole tab is arranged around is that at least one way in has to stay open. A player
 * who removes their password and then disconnects Discord owns an empire nobody can reach, so the
 * page says which is the last one standing and the server refuses the change regardless of what the
 * page says - this is the explanation, not the enforcement.
 *
 * It keeps its own state rather than going through the shared act(), because none of it is a game
 * action: nothing here spends a turn, moves a number, or belongs in the activity log, and running it
 * through the dashboard refresh would only throw away the one sentence worth reading.
 */
function AccountPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('account', ACCOUNT_TABS, 'profile')
  const [account, setAccount] = useState<Account | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [email, setEmail] = useState('')

  const load = async () => {
    try {
      const loaded = await api.account()
      setAccount(loaded)
      setEmail(loaded.email ?? '')
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  /** Every control on the tab does the same three things, so they say so once. */
  const run = async (fn: () => Promise<Account | void>, said: string, form?: HTMLFormElement) => {
    setBusy(true); setError(''); setNotice('')
    const previousPlayerName = account?.playerName
    try {
      const updated = await fn()
      if (updated) {
        setAccount(updated)
        setEmail(updated.email ?? '')
        if (previousPlayerName && updated.playerName !== previousPlayerName) await ctx.refresh()
      }
      setNotice(said)
      // Passwords typed into a form have no business surviving the submit that used them.
      form?.querySelectorAll('input[type=password]').forEach(input => { (input as HTMLInputElement).value = '' })
    } catch (e) {
      setError((e as Error).message)
      // A refused attempt still burned one, and the count only comes back on a fresh read.
      await load()
    }
    finally { setBusy(false) }
  }

  if (!account) return <div className="d-grid gap-3">
    <section className="card p-3"><p className="text-body-tertiary small mb-0">Reading your account.</p></section>
    {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
  </div>

  // Only what the panels take. Spreading the whole component state would let a panel quietly start
  // depending on something it has no business touching.
  const panel: AccountPanel = { account, busy, run, fail: setError }

  /*
    Accounts made before signing up required one of the two exist, and nothing was ever going to tell
    them. They keep working - it is a rule about signing up, not about carrying on playing - but an
    account with no way back is one forgotten password from being gone, and the owner should hear that
    from the page rather than from the day it happens.
  */
  const strandable = waysBackIn(account).length === 0

  return <div className="d-grid gtc-1 gap-3 align-items-start">
    <nav className="d-grid gtc-fill-150 gap-1 border rounded p-1">
      {ACCOUNT_TABS.map(name => <button
        key={name}
        type="button"
        className={`admin-tab btn d-grid gap-1 text-start px-3 py-2 ${tab === name ? 'active' : ''}`}
        aria-current={tab === name ? 'page' : undefined}
        onClick={() => setTab(name)}
      >
        <strong>{ACCOUNT_TAB_META[name].label}</strong>
        <span className="small opacity-75">{ACCOUNT_TAB_META[name].kicker}</span>
      </button>)}
    </nav>

    {(error || notice) && <div className="d-grid gap-2">
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {notice && <DismissibleMessage className="alert alert-success" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
    </div>}

    {/* Not dismissible, and on every tab. It is true until it is fixed, and hiding it would be doing
        the player a favour they did not ask for. */}
    {strandable && <div className="alert alert-warning d-flex flex-wrap align-items-center justify-content-between gap-3 mb-0">
      <span>
        <strong>There is no way back into this account.</strong> Forget your password and it is gone -
        confirm an email address or connect Discord, and there is a way back.
      </span>
      {tab !== 'signin' && <button className="btn btn-warning flex-shrink-0" type="button" onClick={() => setTab('signin')}>
        Fix this
      </button>}
    </div>}

    <div className="account-grid d-grid gtc-1 gtc-xl-2 gap-3 align-items-start min-w-0">
      {tab === 'profile' && <AccountProfilePanel {...panel} dashboard={ctx.dashboard} onTab={setTab} />}
      {tab === 'signin' && <>
        <AccountNamePanel {...panel} />
        <AccountEmailPanel {...panel} email={email} setEmail={setEmail} />
        <AccountPasswordPanel {...panel} />
        <AccountDiscordPanel {...panel} />
      </>}
      {tab === 'invites' && <AccountInvitesPanel busy={busy} />}
      {tab === 'display' && <>
        <AccountDisplayPanel />
        <AccountWalkthroughPanel onTour={ctx.openTour} />
      </>}
      {tab === 'privacy' && <AccountPrivacyPanel {...panel} />}
      {tab === 'alerts' && <AccountAlertsPanel {...panel} />}
      {tab === 'security' && <AccountSecurityPanel {...panel} onTab={setTab} />}
    </div>
  </div>
}

/**
 * A door back into the walkthrough.
 *
 * It used to live on the Getting Started panel, which is the one place it was certain to be useless:
 * that panel is on the Overview, it is aimed at somebody in their first week, and it disappears once
 * the opening ladder is done. The player who actually wants this is the one who came back after a
 * month and cannot remember what banking was for - and by then the button had gone.
 *
 * Settings, because that is where somebody looks for a thing they half remember switching off.
 */
function AccountWalkthroughPanel({ onTour }: { onTour: () => void }) {
  return <section className="card p-3">
    <div className="panel-title"><h2>Walkthrough</h2><span>The opening four moves</span></div>
    <p className="text-body-secondary mt-3 mb-0">
      The short tour a new account gets: pricing a shift before working it, working one, banking what
      it paid, and buying what the next one burns. It runs once when the account is made. Nothing here
      is spent by looking at it again.
    </p>
    <div className="d-flex mt-3">
      <button className="btn btn-primary" type="button" onClick={onTour}>Run it again</button>
    </div>
  </section>
}

/** What the panels below all take. Bundled because every one of them takes all of it. */
type AccountPanel = {
  account: Account
  busy: boolean
  run: (fn: () => Promise<Account | void>, said: string, form?: HTMLFormElement) => Promise<void>
  /** For a refusal the page can make on its own, without troubling the server about it. */
  fail: (message: string) => void
}

function AccountNamePanel({ account, busy, run }: AccountPanel) {
  const [playerName, setPlayerName] = useState(account.playerName)
  useEffect(() => { setPlayerName(account.playerName) }, [account.playerName])
  const nameTicker = useSecondsTicker(!!account.playerNameChangeReadyAtUtc)
  const nameCooldownSeconds = secondsUntil(account.playerNameChangeReadyAtUtc, nameTicker)

  const savePlayerName = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(() => api.setPlayerName(playerName.trim()), 'Player name changed.')
  }

  return <section className="card p-3">
    <div className="panel-title"><h2>Player Name</h2><span>{nameCooldownSeconds > 0 ? timeUntil(account.playerNameChangeReadyAtUtc!) : 'Ready'}</span></div>
    <p>
      This is the name other players see on ladders, news, profiles, chat, crew rosters, wars, and season
      tables. Your username stays private and still handles sign-in.
    </p>
    <form className="d-grid gap-3" onSubmit={savePlayerName}>
      <label className="field">
        Display name
        <input
          className="form-control"
          maxLength={32}
          value={playerName}
          onChange={event => setPlayerName(event.target.value)}
        />
        <small className="form-text">
          {nameCooldownSeconds > 0
            ? `You can change it again in ${timeUntil(account.playerNameChangeReadyAtUtc!)}.`
            : 'Names must be 3-32 characters.'}
        </small>
      </label>
      <Button
        className="btn btn-primary"
        blocked={firstReason(
          busy && BUSY,
          playerName.trim().length < 3 && 'Player name must be at least three characters.',
          playerName.trim().length > 32 && 'Player name must be 32 characters or less.',
          playerName.trim() === account.playerName && 'That is already your player name.',
          nameCooldownSeconds > 0 && `You can change your player name again in ${timeUntil(account.playerNameChangeReadyAtUtc!)}.`,
        )}
      >
        {busy ? 'Working...' : 'Change Name'}
      </Button>
    </form>
  </section>
}

function AccountProfilePanel({ account, dashboard, busy, run, fail, onTab }: AccountPanel & { dashboard: Dashboard, onTab: (tab: AccountTab) => void }) {
  // Two names, and they are not the same thing, which is worth saying plainly on the page where both
  // appear: one is how you sign in and nobody else sees it, the other is what the whole city calls you.
  const open = waysIn(account)
  const [tagline, setTagline] = useState(account.profileTagline ?? '')
  const [pronouns, setPronouns] = useState(account.profilePronouns ?? '')
  const [location, setLocation] = useState(account.profileLocation ?? '')
  const [accent, setAccent] = useState<Account['profileAccent']>(account.profileAccent)
  const [banner, setBanner] = useState<ProfileBanner>(account.profileBanner)
  const [featured, setFeatured] = useState(account.featuredTitle ?? '')
  // What the picker may offer is what they hold today, which is a live answer rather than part of the
  // account - see the endpoint. Empty for almost everybody, which is what makes a title worth having.
  const [held, setHeld] = useState<PlayerTitle[]>([])
  useEffect(() => { void (async () => { try { setHeld(await api.myTitles()) } catch { /* the picker just stays empty */ } })() }, [])
  useEffect(() => {
    setTagline(account.profileTagline ?? '')
    setPronouns(account.profilePronouns ?? '')
    setLocation(account.profileLocation ?? '')
    setAccent(account.profileAccent)
    setBanner(account.profileBanner)
    setFeatured(account.featuredTitle ?? '')
  }, [account.profileTagline, account.profilePronouns, account.profileLocation, account.profileAccent,
      account.profileBanner, account.featuredTitle])

  const saveProfile = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(() => api.setProfile(tagline.trim(), pronouns.trim(), location.trim(), accent, banner, featured), 'Profile saved.')
  }

  const uploadAvatar = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const file = (new FormData(form).get('avatar') as File | null)
    if (!(file instanceof File) || file.size === 0) { fail('Choose an image file first.'); return }
    if (file.size > 1_000_000) { fail('Avatar image must be 1 MB or smaller.'); return }
    void run(() => api.uploadCustomAvatar(file), 'Custom avatar uploaded.', form)
  }

  return <>
    <section className="card p-3 gcol-xl-full">
      <div className="d-flex flex-wrap align-items-center gap-3 mb-3">
        <AccountAvatar account={account} size={72} />
        <div className="min-w-0 flex-fill">
          <div className="panel-title mb-0"><h2>{account.playerName}</h2><span>{dashboard.city} / Rank #{dashboard.rank}</span></div>
          <small className="text-body-tertiary">
            {account.avatarSource === 'Discord' ? 'Using Discord avatar' : account.avatarSource === 'Custom' ? 'Using custom avatar' : 'Using default avatar'}
          </small>
          {account.profileTagline && <p className={`mb-0 mt-1 ${profileAccentClass(account.profileAccent)} text-truncate`}>{account.profileTagline}</p>}
          {(account.profilePronouns || account.profileLocation) && <small className="d-block text-body-tertiary text-truncate">
            {[account.profilePronouns, account.profileLocation].filter(Boolean).join(' / ')}
          </small>}
          <ProfileBadgeStrip badges={account.profileBadges ?? []} />
        </div>
      </div>
      <div className="tnum d-grid gtc-1 gtc-md-4 gap-2 mb-3">
        <AdminMetric label="Player name" value={account.playerName} />
        <AdminMetric label="Username" value={account.username} />
        <AdminMetric label="Ways in" value={`${open.length} of 2`} />
        <AdminMetric label="Since" value={new Date(account.createdAtUtc).toLocaleDateString()} />
      </div>
      <p className="mb-0">
        Your <strong className="text-primary">player name</strong> is what the city sees - the ladder, the
        news, the wanted list. Your <strong className="text-primary">username</strong> is only ever how you
        sign in, and nobody else is shown it.
      </p>
      <div className="d-grid gtc-1 gtc-lg-2 gap-3 mt-3">
        <form className="d-grid gap-3 border rounded bg-body-secondary p-3" onSubmit={saveProfile}>
          <label className="field">
            Tagline
            <input
              className="form-control"
              maxLength={140}
              value={tagline}
              placeholder="One line the city sees"
              onChange={event => setTagline(event.target.value)}
            />
            <small className="form-text">{Math.max(0, 140 - tagline.length)} characters left.</small>
          </label>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Pronouns
              <input
                className="form-control"
                maxLength={64}
                value={pronouns}
                placeholder="Optional"
                onChange={event => setPronouns(event.target.value)}
              />
            </label>
            <label className="field">
              Profile location
              <input
                className="form-control"
                maxLength={64}
                value={location}
                placeholder="Optional"
                onChange={event => setLocation(event.target.value)}
              />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Accent
              <select
                className="form-select"
                value={accent}
                onChange={event => setAccent(event.target.value as Account['profileAccent'])}
              >
                {PROFILE_ACCENTS.map(option => <option value={option} key={option}>{option}</option>)}
              </select>
            </label>
            <label className="field">
              Banner
              <select
                className="form-select"
                value={banner}
                onChange={event => setBanner(event.target.value as ProfileBanner)}
              >
                {profileBanners.map(option => <option value={option.key} key={option.key}>{option.label}</option>)}
              </select>
              <small className="form-text">Behind your name when somebody opens your profile.</small>
            </label>
          </div>
          {/*
            Titles are worked out fresh from the day's fighting, so this offers what you hold now and
            remembers the choice either way - one taken from you this afternoon is one you may hold
            again tomorrow, and forgetting it every time would make this a setting nobody could keep.
          */}
          <label className="field">
            Lead with
            <select
              className="form-select"
              value={featured}
              onChange={event => setFeatured(event.target.value)}
            >
              <option value="">Whatever I hold</option>
              {held.map(title => <option value={title.key} key={title.key}>{title.title}</option>)}
              {/* Their choice, still selectable, even on a day they have lost it. */}
              {featured !== '' && !held.some(x => x.key === featured)
                && <option value={featured}>{featured} (not held today)</option>}
            </select>
            <small className="form-text">
              {held.length === 0
                ? 'You hold no titles today. They are won by the day’s fighting, and most days nobody holds one.'
                : 'Shown first on your card, ahead of the rest.'}
            </small>
          </label>

          {/* Shown rather than described. A named gradient means nothing until you see it. */}
          <div className={`profile-banner ${bannerClass(banner)} d-flex align-items-end p-2`}>
            <strong className={`${profileAccentClass(accent)} text-truncate`}>{account.playerName}</strong>
          </div>
          <Button
            className="btn btn-primary"
            blocked={firstReason(
              busy && BUSY,
              tagline.trim() === (account.profileTagline ?? '')
                && pronouns.trim() === (account.profilePronouns ?? '')
                && location.trim() === (account.profileLocation ?? '')
                && accent === account.profileAccent
                && banner === account.profileBanner
                && featured === (account.featuredTitle ?? '')
                && 'Nothing on the card has been changed.',
            )}
          >
            {busy ? 'Working...' : 'Save Profile'}
          </Button>
        </form>

        <form className="avatar-form d-grid gap-3 border rounded bg-body-secondary p-3" onSubmit={uploadAvatar}>
          <div className="d-flex align-items-center gap-3 min-w-0">
            <PlayerAvatar name={account.playerName} username={account.username} avatarUrl={account.customAvatarUrl} size={56} />
            <div className="min-w-0">
              <span className="eyebrow d-block">Uploaded avatar</span>
              <strong className="d-block text-truncate">{account.customAvatarUrl ? 'Ready' : 'None uploaded'}</strong>
            </div>
          </div>
          <label className="field">
            Custom avatar
            <input className="form-control" name="avatar" type="file" accept="image/png,image/jpeg,image/gif,image/webp" />
            <small className="form-text">PNG, JPG, GIF, or WebP. 1 MB max.</small>
          </label>
          <div className="avatar-actions d-flex flex-wrap gap-2">
            <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Upload and Use'}</Button>
            <Button
              className="btn btn-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                !account.customAvatarUrl && 'You have not uploaded a picture yet.',
                account.avatarSource === 'Custom' && 'Your uploaded picture is the one already in use.',
              )}
              onClick={() => void run(() => api.setAvatarSource('Custom'), 'Custom avatar selected.')}
            >Use Custom</Button>
            <Button
              className="btn btn-outline-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                account.avatarSource === 'None' && 'You are already on the default picture.',
              )}
              onClick={() => void run(() => api.setAvatarSource('None'), 'Default avatar selected.')}
            >Use Default</Button>
            <Button
              className="btn btn-outline-danger"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                !account.customAvatarUrl && 'There is no uploaded picture to remove.',
              )}
              onClick={() => void run(() => api.deleteCustomAvatar(), 'Custom avatar removed.')}
            >Remove Custom</Button>
          </div>
        </form>
      </div>
    </section>

    {/*
      Two panels rather than one, because a name and a way in are not the same kind of thing and putting
      them in one list of "ways in" says something false. An email address is a second name for the
      password door - it opens nothing on its own, and the day the password goes it is worth nothing.
      A player reading a tile that said otherwise might close the only door they had.
    */}
    <section className="card p-3">
      <div className="panel-title"><h2>Ways In</h2><span>{open.length} of 2</span></div>
      <p>
        Two things can actually let you in, and you need to keep at least one. The game will not let you
        close the last one.
      </p>
      <div className="d-grid gtc-1 gtc-md-2 gap-2">
        <WayInTile label="Password" open={account.hasPassword} detail={account.hasPassword ? 'Set' : 'Never set'} />
        <WayInTile label="Discord" open={account.discordConnected} detail={account.discordUsername ?? 'Not connected'} />
      </div>
      <button className="btn btn-secondary mt-3" type="button" onClick={() => onTab('signin')}>Manage sign-in</button>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Names You Can Type</h2><span>{signInNames(account).length} of 2</span></div>
      <p>
        Either of these goes in the box on the sign-in screen, with your password. They are names, not
        keys - neither of them opens anything without the password beside it.
      </p>
      <div className="d-grid gtc-1 gtc-md-2 gap-2">
        <WayInTile label="Username" open detail={account.username} />
        <WayInTile
          label="Email"
          open={account.emailVerified}
          detail={account.email
            ? account.emailVerified ? account.email : `${account.email} - not confirmed`
            : 'None set'}
        />
      </div>
    </section>
  </>
}

function AccountAvatar({ account, size = 56 }: { account: Account, size?: number }) {
  return <PlayerAvatar name={account.playerName} username={account.username} avatarUrl={account.avatarUrl} size={size} />
}

function PlayerAvatar({ name, username = name, avatarUrl, size = 36 }: { name: string, username?: string, avatarUrl?: string | null, size?: number }) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map(part => part[0]?.toUpperCase())
    .join('') || username.slice(0, 2).toUpperCase()
  const style = {
    width: size,
    height: size,
    borderRadius: '50%',
  }

  return avatarUrl
    ? <img
      src={avatarUrl}
      alt=""
      className="border border-primary object-fit-cover flex-shrink-0"
      style={style}
      referrerPolicy="no-referrer"
    />
    : <div
      className="d-inline-grid place-items-center border border-primary bg-body-secondary text-primary fw-bold flex-shrink-0 tnum"
      style={{ ...style, fontSize: Math.max(16, Math.floor(size * 0.34)) }}
      aria-hidden="true"
    >{initials}</div>
}

/**
 * The things that would actually let somebody in.
 *
 * An email address is deliberately not one of them. It is a second name for the password door, so
 * counting it here would tell a player with a password and an address that they have two ways in and
 * can safely drop one - and dropping the password takes the address with it.
 */
function waysIn(account: Account) {
  return [
    account.hasPassword && 'password',
    account.discordConnected && 'discord',
  ].filter(Boolean)
}

/** The names the sign-in box will accept. Only a confirmed address is one. */
function signInNames(account: Account) {
  return ['username', account.emailVerified && 'email'].filter(Boolean)
}

/**
 * The things that could get somebody back in, which is a different list from the things that let them
 * in. A password is a way in and is not a way back: forget it and there is nothing left to prove the
 * account was ever yours. Only a confirmed address and a Discord answer this one.
 */
function waysBackIn(account: Account) {
  return [
    account.emailVerified && 'email',
    account.discordConnected && 'discord',
  ].filter(Boolean)
}

function WayInTile({ label, open, detail }: { label: string, open: boolean, detail: string }) {
  return <div className={`stat d-grid gap-1 border rounded bg-body-secondary p-3 ${open ? 'border-primary' : ''}`}>
    <span className="eyebrow">{label}</span>
    <strong className={`min-w-0 fs-6 lh-1 text-truncate ${open ? 'text-primary' : 'text-body-tertiary'}`}>
      {open ? 'Open' : 'Closed'}
    </strong>
    <small className="small text-truncate" title={detail}>{detail}</small>
  </div>
}

function AccountEmailPanel({ account, busy, run, email, setEmail }: AccountPanel & { email: string, setEmail: (value: string) => void }) {
  const pending = account.verification
  const now = useSecondsTicker(pending !== null)
  const expiresIn = secondsUntil(pending?.expiresAtUtc, now)
  const resendIn = secondsUntil(pending?.resendableAtUtc, now)
  const emailChanged = (account.email ?? '') !== email.trim()
  // Emptying the box is a removal, and a removal is only allowed while something else could still get
  // them back in. Changing it to a different address is always fine - one is still there to confirm.
  const removingLastWayBack = email.trim().length === 0 && account.email !== null && !account.discordConnected

  const saveEmail = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const current = new FormData(form).get('currentPassword')
    void run(
      () => api.setEmail(email.trim(), String(current ?? '')),
      email.trim() ? 'Email saved. Confirm it to sign in with it.' : 'Email removed.',
      form)
  }

  const confirm = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    void run(() => api.confirmEmail(String(new FormData(form).get('code') ?? '')), 'Address confirmed.', form)
      .then(() => { form.reset() })
  }

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Email</h2>
      <span className={account.emailVerified ? 'text-primary' : ''}>
        {!account.email ? 'None' : account.emailVerified ? 'Confirmed' : 'Not confirmed'}
      </span>
    </div>
    <p>
      A second name to sign in under, with the same password. It only becomes a way in once you have
      confirmed it, so an address typed by somebody who cannot read the mail opens nothing.
    </p>

    {/*
      Said out loud rather than hidden. Mail written to a server log is exactly right on a laptop and
      exactly wrong anywhere else, and a player who never gets a code deserves to know which it is.
    */}
    {!account.emailDelivers && <div className="alert alert-warning">
      No email provider is configured on this server, so codes are written to the server log instead of
      being sent. Fine for development; nobody will receive anything.
    </div>}

    {account.email && !account.emailVerified && <div className="border border-primary rounded p-3 mb-3 d-grid gap-3">
      <div>
        <span className="eyebrow d-block">Confirm this address</span>
        <p className="mb-0 mt-1">
          {pending
            ? <>A six-digit code went to <strong className="text-primary">{pending.sentTo}</strong>.
              It is good for another <strong className="tnum">{countdown(expiresIn)}</strong>, and you have{' '}
              <strong className="tnum">{pending.attemptsRemaining}</strong> {pending.attemptsRemaining === 1 ? 'try' : 'tries'} left.</>
            : <>Nothing is waiting. Ask for a code and it will arrive at{' '}
              <strong className="text-primary">{account.email}</strong>.</>}
        </p>
      </div>

      {pending && expiresIn > 0 && <form className="d-flex flex-wrap align-items-end gap-2" onSubmit={confirm}>
        <label className="field flex-fill min-w-0">
          Code
          {/*
            One box rather than six. Six boxes look the part and then fight the player over pasting,
            backspacing and autofill, all to save typing that nobody was struggling with.
          */}
          <input
            className="form-control tnum fs-4 text-center"
            style={{ letterSpacing: '.4em' }}
            name="code"
            inputMode="numeric"
            autoComplete="one-time-code"
            pattern="[0-9]*"
            maxLength={6}
            placeholder="000000"
            required
          />
        </label>
        <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Confirm'}</Button>
      </form>}

      <Button
        className="btn btn-secondary"
        type="button"
        blocked={firstReason(
          busy && BUSY,
          resendIn > 0 && `A code went out already. You can ask for another in ${countdown(resendIn)}.`,
        )}
        onClick={() => void run(() => api.sendEmailCode(), 'A new code is on its way.')}
      >
        {resendIn > 0
          ? `Send another in ${countdown(resendIn)}`
          : pending ? 'Send a new code' : 'Send a code'}
      </Button>
    </div>}

    {account.emailVerified && account.emailVerifiedAtUtc && <p className="text-body-tertiary small">
      Confirmed on {new Date(account.emailVerifiedAtUtc).toLocaleDateString()}.
    </p>}

    <form className="d-grid gap-3" onSubmit={saveEmail}>
      <label className="field">
        Address
        <input
          className="form-control"
          type="email"
          maxLength={254}
          value={email}
          placeholder="nobody@example.com"
          onChange={event => setEmail(event.target.value)}
        />
        <small className="form-text">Empty removes it. Changing it starts the confirmation again.</small>
      </label>
      {account.hasPassword && <label className="field">
        Current password
        <input className="form-control" name="currentPassword" type="password" autoComplete="current-password" required />
        <small className="form-text">Changing where a sign-in can come from costs the password.</small>
      </label>}
      {/*
        Removing the last way back in is refused by the server whatever this button says, so the button
        says it first. A refusal a player could have seen coming is a worse refusal than one that
        explains itself before they click.
      */}
      <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !emailChanged && 'That is the address already on the account.',
        removingLastWayBack && 'This address is the only way back into your account if you forget your password. Connect Discord on this page and you can remove it.',
      )}>
        {busy ? 'Working...' : email.trim() ? 'Save Email' : 'Remove Email'}
      </Button>
    </form>
  </section>
}

function AccountPasswordPanel({ account, busy, run, fail }: AccountPanel) {
  const savePassword = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const next = String(data.get('newPassword') ?? '')
    // Caught here rather than sent: the server has no way to know what was typed in the second box,
    // and a round trip to be told something the page already knew is a round trip wasted.
    if (next !== String(data.get('confirmPassword') ?? '')) { fail('The two new passwords do not match.'); return }
    void run(
      () => api.setPassword(String(data.get('currentPassword') ?? ''), next),
      account.hasPassword ? 'Password changed. Every other session has been signed out.' : 'Password set.',
      form)
  }

  return <section className="card p-3">
    <div className="panel-title"><h2>Password</h2><span>{account.hasPassword ? 'Set' : 'None'}</span></div>
    <p>
      {account.hasPassword
        ? 'Changing it signs out every other session on this account, and keeps this one.'
        : 'You signed up through Discord and have never set one. Set a password and you can sign in with your username as well.'}
    </p>
    <form className="d-grid gap-3" onSubmit={savePassword}>
      {account.hasPassword && <label className="field">
        Current password
        <input className="form-control" name="currentPassword" type="password" autoComplete="current-password" required />
      </label>}
      <label className="field">
        New password
        <input className="form-control" name="newPassword" type="password" autoComplete="new-password" minLength={8} required />
        <small className="form-text">Eight characters at the very least.</small>
      </label>
      <label className="field">
        New password again
        <input className="form-control" name="confirmPassword" type="password" autoComplete="new-password" minLength={8} required />
      </label>
      <Button className="btn btn-primary" blocked={busy && BUSY}>
        {busy ? 'Working...' : account.hasPassword ? 'Change Password' : 'Set Password'}
      </Button>
    </form>
  </section>
}

function AccountDiscordPanel({ account, busy, run }: AccountPanel) {
  // Disconnecting takes away a way in, which is the same kind of act as changing the address, so it
  // costs the same thing. An account with no password has nothing to prove with and is not asked.
  const [password, setPassword] = useState('')
  // Two separate reasons the connection might be stuck, and they are not the same reason - one is
  // about getting in at all, the other about getting back in after forgetting the password.
  const discordIsTheOnlyWayIn = account.discordConnected && !account.hasPassword
  const discordIsTheOnlyWayBackIn = account.discordConnected && !account.emailVerified

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Discord</h2><span>{account.discordConnected ? 'Connected' : 'Not connected'}</span>
    </div>
    {account.discordConnected
      ? <>
        <p>
          Connected to <strong className="text-primary">{account.discordUsername}</strong>
          {account.discordLinkedAtUtc && <> since {new Date(account.discordLinkedAtUtc).toLocaleDateString()}</>}.
          That Discord account signs straight in, on any browser, without a password.
        </p>
        {account.discordLinkRewardClaimedAtUtc && <div className="alert alert-success">
          Link reward claimed: $10,000, 25 condoms, 25 beer, and the Discord Connected title.
        </div>}
        {!account.discordLinkRewardClaimedAtUtc && <div className="alert alert-primary d-flex flex-wrap align-items-center justify-content-between gap-2">
          <span>Claim your first-link reward: $10,000, 25 condoms, 25 beer, and the Discord Connected title.</span>
          <Button
            className="btn btn-primary btn-sm"
            type="button"
            blocked={busy && BUSY}
            onClick={() => void run(() => api.claimDiscordLinkReward(), 'Discord link reward claimed.')}
          >
            {busy ? 'Working...' : 'Claim reward'}
          </Button>
        </div>}
        <div className="border rounded bg-body-secondary p-3 mb-3 d-grid gap-3">
          <div className="d-flex align-items-center gap-3 min-w-0">
            {account.discordAvatarUrl
              ? <img
                src={account.discordAvatarUrl}
                alt=""
                className="border border-primary object-fit-cover flex-shrink-0"
                style={{ width: 56, height: 56, borderRadius: '50%' }}
                referrerPolicy="no-referrer"
              />
              : <AccountAvatar account={account} />}
            <div className="min-w-0">
              <span className="eyebrow d-block">Avatar</span>
              <strong className="d-block text-truncate">
                {account.avatarSource === 'Discord'
                  ? 'Synced from Discord'
                  : account.avatarSource === 'Custom' ? 'Using custom avatar' : 'Default'}
              </strong>
              <small className="text-body-tertiary">
                {account.discordAvatarUrl ? 'Refresh after changing it on Discord.' : 'No custom Discord avatar found.'}
              </small>
            </div>
          </div>
          <div className="avatar-actions d-flex flex-wrap gap-2">
            <Button
              className="btn btn-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                !account.discordAvatarUrl && 'Discord has no custom picture for you to use.',
                account.avatarSource === 'Discord' && 'Your Discord picture is the one already in use.',
              )}
              onClick={() => void run(() => api.setAvatarSource('Discord'), 'Discord avatar selected.')}
            >
              Use Discord avatar
            </Button>
            <Button
              className="btn btn-outline-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                account.avatarSource === 'None' && 'You are already on the default picture.',
              )}
              onClick={() => void run(() => api.setAvatarSource('None'), 'Default avatar selected.')}
            >
              Use default
            </Button>
            <a className="btn btn-outline-secondary d-inline-flex align-items-center gap-2" href={discordStartUrl()}>
              <i className="bi bi-arrow-repeat" aria-hidden="true" />
              Refresh from Discord
            </a>
          </div>
        </div>
        {discordIsTheOnlyWayIn
          ? <div className="alert alert-warning mb-0">
            This is the only way into your empire. Set a password before disconnecting it.
          </div>
          : discordIsTheOnlyWayBackIn
          ? <div className="alert alert-warning mb-0">
            This is the only way back into your empire if you forget your password. Confirm an email
            address before disconnecting it.
          </div>
          : <div className="d-grid gap-2">
            {account.hasPassword && <label className="field">
              Current password
              <input
                className="form-control"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={event => setPassword(event.target.value)}
              />
              <small className="form-text">Taking away a way in costs the password, as changing your address does.</small>
            </label>}
            <Button
              className="btn btn-outline-danger"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                account.hasPassword && password.length === 0 && 'Type your password above. Taking away a way in costs it.',
              )}
              onClick={() => void run(
                async () => { const a = await api.disconnectDiscord(password); setPassword(''); return a },
                'Discord disconnected.')}
            >Disconnect Discord</Button>
          </div>}
      </>
      : account.discordConfigured
        ? <>
          <p>
            Connect one and it becomes a way in: one button on the sign-in screen, no password typed.
            You keep your username and password either way.
          </p>
          <p className="text-body-tertiary small">
            First link pays $10,000, 25 condoms, 25 beer, and unlocks the Discord Connected title.
          </p>
          {/*
            A link, not a button. Connecting is the same round trip through Discord that signing in is,
            and the only difference is that this one starts with a session already in hand - which is
            what tells the callback to attach rather than to sign somebody in.
          */}
          <a className="btn btn-secondary d-inline-flex align-items-center justify-content-center gap-2" href={discordStartUrl()}>
            <i className="bi bi-discord" aria-hidden="true" />
            Connect Discord
          </a>
        </>
        : <p className="mb-0 text-body-tertiary">
          This server has no Discord credentials set, so there is nothing to connect to yet.
        </p>}
  </section>
}

function AccountPrivacyPanel({ account, busy, run }: AccountPanel) {
  const [showDiscord, setShowDiscord] = useState(account.showDiscordOnProfile)
  const [dmPolicy, setDmPolicy] = useState<Account['directMessagePolicy']>(account.directMessagePolicy)
  const [showActivity, setShowActivity] = useState(account.showActivityOnProfile)
  useEffect(() => {
    setShowDiscord(account.showDiscordOnProfile)
    setDmPolicy(account.directMessagePolicy)
    setShowActivity(account.showActivityOnProfile)
  }, [account.showDiscordOnProfile, account.directMessagePolicy, account.showActivityOnProfile])

  const changed = showDiscord !== account.showDiscordOnProfile
    || dmPolicy !== account.directMessagePolicy
    || showActivity !== account.showActivityOnProfile
  const save = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(() => api.setPrivacy(showDiscord, dmPolicy, showActivity), 'Privacy saved.')
  }

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title"><h2>Privacy</h2><span>{
      dmPolicy === 'Everyone' ? 'Open'
        : dmPolicy === 'Alliance' ? 'Crew only'
          : dmPolicy === 'AllianceAndPacts' ? 'Crew and allies'
            : 'Closed'
    }</span></div>
    <form className="d-grid gap-3" onSubmit={save}>
      <label className={`form-check form-switch border rounded bg-body-secondary p-3 ps-5 ${!account.discordConnected ? 'text-body-tertiary' : ''}`}>
        <input
          className="form-check-input"
          type="checkbox"
          checked={showDiscord}
          disabled={!account.discordConnected}
          onChange={event => setShowDiscord(event.target.checked)}
        />
        <strong className="d-block">Show Discord on public profile</strong>
        <small className="form-text">
          {account.discordConnected
            ? account.discordUsername ?? 'Connected Discord'
            : 'Connect Discord before showing it publicly.'}
        </small>
      </label>
      <label className="field">
        Direct messages
        <select
          className="form-select"
          value={dmPolicy}
          onChange={event => setDmPolicy(event.target.value as Account['directMessagePolicy'])}
        >
          <option value="Everyone">Everyone</option>
          <option value="AllianceAndPacts">My crew and our allies</option>
          <option value="Alliance">My crew only</option>
          <option value="Nobody">Nobody</option>
        </select>
        <small className="form-text">
          Allies are crews yours has a standing pact with. Existing blocks still win over this setting.
        </small>
      </label>
      {/*
        The one genuinely private thing on a profile, and the reason this is a switch rather than a
        blanket setting. Your city and your numbers are on the leaderboard whatever you choose here -
        this is the eight-action list with timestamps and takings, which is available nowhere else.
      */}
      <label className="form-check form-switch border rounded bg-body-secondary p-3 ps-5">
        <input
          className="form-check-input"
          type="checkbox"
          checked={showActivity}
          onChange={event => setShowActivity(event.target.checked)}
        />
        <strong className="d-block">Show recent activity on my profile</strong>
        <small className="form-text">
          The last eight things you did, with times and takings, to anybody who opens your profile. Your
          city and your worth are on the leaderboard either way; this is the part that is not.
        </small>
      </label>
      <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !changed && 'Nothing here has been changed.',
      )}>{busy ? 'Working...' : 'Save Privacy'}</Button>
    </form>
  </section>
}

function AccountAlertsPanel({ account, busy, run }: AccountPanel) {
  const [syncDiscord, setSyncDiscord] = useState(account.syncDiscordAvatar)
  const [security, setSecurity] = useState(account.emailSecurityNotices)
  const [combat, setCombat] = useState(account.emailCombatNotices)
  const [alliance, setAlliance] = useState(account.emailAllianceNotices)
  const [discordSecurity, setDiscordSecurity] = useState(account.discordSecurityNotices)
  const [discordCombat, setDiscordCombat] = useState(account.discordCombatNotices)
  const [discordCrew, setDiscordCrew] = useState(account.discordCrewNotices)
  const [discordMarket, setDiscordMarket] = useState(account.discordMarketNotices)
  const [bellCombat, setBellCombat] = useState(account.noticeCombat)
  const [bellCrew, setBellCrew] = useState(account.noticeCrew)
  const [bellMarket, setBellMarket] = useState(account.noticeMarket)
  useEffect(() => {
    setSyncDiscord(account.syncDiscordAvatar)
    setSecurity(account.emailSecurityNotices)
    setCombat(account.emailCombatNotices)
    setAlliance(account.emailAllianceNotices)
    setDiscordSecurity(account.discordSecurityNotices)
    setDiscordCombat(account.discordCombatNotices)
    setDiscordCrew(account.discordCrewNotices)
    setDiscordMarket(account.discordMarketNotices)
    setBellCombat(account.noticeCombat)
    setBellCrew(account.noticeCrew)
    setBellMarket(account.noticeMarket)
  }, [account.syncDiscordAvatar, account.emailSecurityNotices, account.emailCombatNotices, account.emailAllianceNotices,
      account.discordSecurityNotices, account.discordCombatNotices, account.discordCrewNotices,
      account.discordMarketNotices, account.noticeCombat, account.noticeCrew, account.noticeMarket])

  const changed = syncDiscord !== account.syncDiscordAvatar
    || security !== account.emailSecurityNotices
    || combat !== account.emailCombatNotices
    || alliance !== account.emailAllianceNotices
    || discordSecurity !== account.discordSecurityNotices
    || discordCombat !== account.discordCombatNotices
    || discordCrew !== account.discordCrewNotices
    || discordMarket !== account.discordMarketNotices
    || bellCombat !== account.noticeCombat
    || bellCrew !== account.noticeCrew
    || bellMarket !== account.noticeMarket

  const save = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(
      () => api.setNotificationPreferences(
        syncDiscord,
        security,
        combat,
        alliance,
        discordSecurity,
        discordCombat,
        discordCrew,
        discordMarket,
        bellCombat,
        bellCrew,
        bellMarket),
      'Alert settings saved.')
  }

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title"><h2>Alerts</h2><span>{security || combat || alliance || discordSecurity || discordCombat || discordCrew || discordMarket ? 'On' : 'Quiet'}</span></div>
    <form className="d-grid gap-3" onSubmit={save}>
      <label className={`form-check form-switch border rounded bg-body-secondary p-3 ps-5 ${!account.discordConnected ? 'text-body-tertiary' : ''}`}>
        <input
          className="form-check-input"
          type="checkbox"
          checked={syncDiscord}
          disabled={!account.discordConnected}
          onChange={event => setSyncDiscord(event.target.checked)}
        />
        <strong className="d-block">Prefer Discord avatar after refresh</strong>
        <small className="form-text">
          {account.discordConnected
            ? 'When Discord refreshes and has an avatar, it becomes your selected account avatar.'
            : 'Connect Discord before turning this on.'}
        </small>
      </label>
      {/*
        Refreshing is a trip back through Discord rather than a call the server can make on its own,
        because no Discord token is kept here - only the account id it handed over. That is the more
        private arrangement of the two and this is the cost of it: one click, and Discord asks nothing
        again if you are still signed in there.

        The handle has always refreshed itself on every Discord sign-in. What was missing was any
        record of when, which is the half this reports.
      */}
      {account.discordConnected && <div className="border rounded bg-body-secondary p-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
        <div className="min-w-0">
          <strong className="d-block text-truncate">
            <i className="bi bi-discord me-1" aria-hidden="true" />
            {account.discordUsername ?? 'Connected'}
          </strong>
          <small className="text-body-tertiary">
            {account.discordSyncedAtUtc
              ? `Last checked ${new Date(account.discordSyncedAtUtc).toLocaleString()}.`
              : 'Not checked since this was added - refresh to pull your current handle and avatar.'}
          </small>
        </div>
        <a className="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-2" href={discordStartUrl()}>
          <i className="bi bi-arrow-clockwise" aria-hidden="true" />
          Refresh from Discord
        </a>
      </div>}
      <div>
        <span className="eyebrow d-block mb-2">By email</span>
      </div>
      <div className="d-grid gtc-1 gtc-md-3 gap-2">
        <NoticeToggle
          label="Security"
          detail="Password, Discord, sessions, and account access."
          checked={security}
          onChange={setSecurity}
        />
        <NoticeToggle
          label="Combat"
          detail="Future fight and defence email alerts."
          checked={combat}
          onChange={setCombat}
        />
        <NoticeToggle
          label="Crew"
          detail="Future crew requests, pacts, and transfers."
          checked={alliance}
          onChange={setAlliance}
        />
      </div>

      <div>
        <span className="eyebrow d-block mb-2">By Discord DM</span>
        <div className="d-grid gtc-1 gtc-md-4 gap-2">
          <NoticeToggle
            label="Security"
            detail={account.discordConnected ? 'Password, Discord, sessions, and account access.' : 'Connect Discord before turning this on.'}
            checked={discordSecurity}
            disabled={!account.discordConnected}
            onChange={setDiscordSecurity}
          />
          <NoticeToggle
            label="Combat"
            detail={account.discordConnected ? 'Raids on your house, and ground won or lost.' : 'Connect Discord before turning this on.'}
            checked={discordCombat}
            disabled={!account.discordConnected}
            onChange={setDiscordCombat}
          />
          <NoticeToggle
            label="Crew"
            detail={account.discordConnected ? 'Allies calling for help or crew business that needs eyes.' : 'Connect Discord before turning this on.'}
            checked={discordCrew}
            disabled={!account.discordConnected}
            onChange={setDiscordCrew}
          />
          <NoticeToggle
            label="Market"
            detail={account.discordConnected ? 'Somebody buying what you put up for sale.' : 'Connect Discord before turning this on.'}
            checked={discordMarket}
            disabled={!account.discordConnected}
            onChange={setDiscordMarket}
          />
        </div>
      </div>

      {/*
        A different channel, not a duplicate of the three above. Somebody who wants no mail at all still
        wants the bell, and somebody who wants mail about a raid does not necessarily want it about a
        sale - so these are their own columns rather than one set of switches governing both.

        Turning one off takes it out of the unread count as well as the list: a badge over something you
        asked not to be told about is the notification you switched off.
      */}
      <div>
        <span className="eyebrow d-block mb-2">In the game, on the bell</span>
        <div className="d-grid gtc-1 gtc-md-3 gap-2">
          <NoticeToggle
            label="Combat"
            detail="Raids on your house, and ground won or lost."
            checked={bellCombat}
            onChange={setBellCombat}
          />
          <NoticeToggle
            label="Crew"
            detail="Allies calling for help while they are being raided."
            checked={bellCrew}
            onChange={setBellCrew}
          />
          <NoticeToggle
            label="Market"
            detail="Somebody buying what you put up for sale."
            checked={bellMarket}
            onChange={setBellMarket}
          />
        </div>
        <small className="form-text d-block mt-2">
          Your labs, builds and mule runs always ring. They are your own machinery reporting in, and
          there is nowhere else they are said.
        </small>
      </div>

      <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !changed && 'Nothing here has been changed.',
      )}>{busy ? 'Working...' : 'Save Alerts'}</Button>
    </form>
  </section>
}

function NoticeToggle({ label, detail, checked, disabled, onChange }: {
  label: string
  detail: string
  checked: boolean
  disabled?: boolean
  onChange: (value: boolean) => void
}) {
  return <label className={`form-check form-switch border rounded bg-body-secondary p-3 ps-5 ${checked ? 'border-primary' : ''}`}>
    <input
      className="form-check-input"
      type="checkbox"
      checked={checked}
      disabled={disabled}
      onChange={event => onChange(event.target.checked)}
    />
    <strong className="d-block">{label}</strong>
    <small className="form-text">{detail}</small>
  </label>
}

/**
 * Where you are signed in, and the ability to end one of them.
 *
 * The list is loaded here rather than arriving with the account, because it is the one thing on this
 * page that changes without anybody touching it - a session moves every few minutes as somebody plays -
 * and folding it into the account payload would make every other panel refetch it for nothing.
 */
function SessionsCard({ account, busy, run }: { account: Account, busy: boolean, run: AccountPanel['run'] }) {
  const [sessions, setSessions] = useState<PlayerSession[] | null>(null)
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    try { setSessions(await api.sessions()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  // Only an account with a password can be asked for one. A Discord-only account has nothing to prove
  // with and is already proving itself with the cookie, which is the same exemption the password form
  // makes rather than a hole opened here.
  const needsPassword = account.hasPassword

  const revokeOne = async (session: PlayerSession) => {
    setError('')
    try {
      await api.revokeSession(session.id, password)
      setPassword('')
      await load()
    } catch (e) { setError((e as Error).message) }
  }

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Sessions</h2>
      <span>{sessions === null ? 'Reading' : `${sessions.length} signed in`}</span>
    </div>
    <p>
      A sign-in lasts a fortnight and renews itself while you play, which is convenient right up until
      you leave yourself signed in on a machine you no longer have.
    </p>

    {needsPassword && <label className="field mb-3">
      Current password
      <input
        className="form-control"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={event => setPassword(event.target.value)}
      />
      <small className="form-text">
        Ending a session is how somebody who has taken one would lock you out of your own account, so it
        costs the password - which a stolen cookie does not carry.
      </small>
    </label>}

    {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}

    {sessions !== null && <div className="d-grid gap-2 mb-3">
      {sessions.length === 0 && <p className="text-body-tertiary small mb-0">
        Nothing recorded yet. Sessions from before this was added are not listed - they still work, and
        signing out everywhere ends them.
      </p>}
      {sessions.map(session => <div
        key={session.id}
        className={`session-row border rounded p-2 d-grid gap-2 align-items-center ${session.isCurrent ? 'border-primary' : 'bg-body-secondary'}`}
      >
        <div className="min-w-0">
          <strong className="d-block text-truncate">
            {session.isCurrent ? 'This device' : session.ipAddress ?? 'Unknown address'}
          </strong>
          <small className="session-user-agent d-block text-body-tertiary">{session.userAgent ?? 'Unknown browser'}</small>
          <small className="d-block text-body-tertiary">
            Last seen {new Date(session.lastSeenAtUtc).toLocaleString()}
            {session.isCurrent ? '' : ` / signed in ${new Date(session.createdAtUtc).toLocaleDateString()}`}
          </small>
        </div>
        <Button
          className="btn btn-outline-danger btn-sm"
          type="button"
          blocked={busy && BUSY}
          onClick={() => void revokeOne(session)}
        >{session.isCurrent ? 'Sign out here' : 'End it'}</Button>
      </div>)}
    </div>}

    <Button
      className="btn btn-outline-danger"
      type="button"
      blocked={busy && BUSY}
      onClick={() => void run(
        async () => { const a = await api.revokeSessions(password); setPassword(''); await load(); return a },
        'Every other session has been signed out.')}
    >{busy ? 'Working...' : 'Sign out everywhere else'}</Button>
  </section>
}

/**
 * Ten single-use ways back in, shown once.
 *
 * Once is not a limitation to work around - it is the reason these are safe to have. What the server
 * keeps is a hash, exactly as it does for a password, so there is no endpoint that could say them again
 * and no column that hands somebody with database access a way into every account in the game.
 */
function RecoveryCodesCard({ account, busy }: { account: Account, busy: boolean }) {
  const [remaining, setRemaining] = useState<number | null>(null)
  const [password, setPassword] = useState('')
  const [codes, setCodes] = useState<string[] | null>(null)
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async () => {
    try { setRemaining((await api.recoveryCodesLeft()).remaining) } catch { /* the count is not the point */ }
  }
  useEffect(() => { void load() }, [])

  const issue = async () => {
    setWorking(true); setError('')
    try {
      setCodes((await api.issueRecoveryCodes(password)).codes)
      setPassword('')
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Recovery codes</h2>
      <span>{remaining === null ? 'Reading' : remaining === 0 ? 'None made' : `${remaining} left`}</span>
    </div>
    <p>
      Ten one-time codes. Any of them gets you back in without an email and without Discord, which is the
      case neither of the other two doors can answer - a lost mailbox, or a Discord account you no longer
      have. Each one works once.
    </p>
    <p className="text-body-tertiary small">
      They do not replace your email or your Discord: you still cannot remove your last way back in. A
      sheet of paper is the thing most easily lost, so it is a spare set of keys rather than the door.
    </p>

    {codes
      ? <>
        <div className="alert alert-warning">
          Written down now or not at all. They are stored hashed, exactly as your password is, so this is
          the only time they can be shown.
        </div>
        <pre className="border rounded bg-body-tertiary p-3 mb-3 tnum">{codes.join('\n')}</pre>
        <button
          className="btn btn-secondary"
          type="button"
          onClick={() => void navigator.clipboard?.writeText(codes.join('\n'))}
        >Copy them</button>
        <button className="btn btn-link text-body-secondary" type="button" onClick={() => setCodes(null)}>
          I have written them down
        </button>
      </>
      : <div className="d-grid gap-3">
        {account.hasPassword && <label className="field">
          Current password
          <input
            className="form-control"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
          />
        </label>}
        {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
        <div>
          <Button
            className="btn btn-outline-primary"
            type="button"
            blocked={firstReason(
              busy && BUSY,
              working && 'Your codes are being made now.',
              account.hasPassword && password.length === 0 && 'Type your password above first.',
            )}
            onClick={() => void issue()}
          >{working ? 'Working...' : remaining ? 'Make a new set' : 'Make my codes'}</Button>
        </div>
        {remaining !== null && remaining > 0 && <small className="text-body-tertiary">
          Making a new set voids the old one, so any sheet you already have stops working.
        </small>}
      </div>}
  </section>
}

function AccountSecurityPanel({ account, busy, run, onTab }: AccountPanel & { onTab: (tab: AccountTab) => void }) {
  const open = waysIn(account)
  const back = waysBackIn(account)
  const enoughOfBoth = open.length > 1 && back.length > 1
  return <>
    <SessionsCard account={account} busy={busy} run={run} />
    <RecoveryCodesCard account={account} busy={busy} />

    {/*
      Two counters rather than one, because the panel used to answer one question and imply the other.
      It said "you can close either one and still get back in", which stopped being true the day a way
      *in* and a way *back in* came apart - closing Discord with no confirmed address leaves a player
      signed in and unrecoverable, which is exactly the state the counts exist to show.
    */}
    <section className="card p-3">
      <div className="panel-title">
        <h2>The Last Door</h2><span>{open.length} in / {back.length} back</span>
      </div>
      <p>
        Two different questions, and the pair above answers both. <strong className="text-primary">In</strong> is
        what signs you in: a password, or a connected Discord. <strong className="text-primary">Back</strong> is
        what could still prove the account was yours once the password is gone: a confirmed email
        address, or that same Discord.
      </p>
      <p>
        A password answers the first and never the second - forget it and it proves nothing - which is
        why the two are rarely the same number.
      </p>
      <p className={enoughOfBoth ? 'mb-0' : ''}>
        {enoughOfBoth
          ? 'You have a spare of each, so nothing here is load-bearing. Close any one of them and you can still get in, and still get back.'
          : 'The game refuses to let you close a last one of either. That is a poor substitute for having a spare of each.'}
      </p>
      {!enoughOfBoth && <button className="btn btn-primary" type="button" onClick={() => onTab('signin')}>
        Add another
      </button>}
    </section>
  </>
}

function DismissibleMessage({ className, children, onClose }: { className: string, children: ReactNode, onClose: () => void }) {
  return <div className={`${className} d-flex align-items-center justify-content-between gap-3`}>
    <span>{children}</span>
    <button className="btn-close" type="button" aria-label="Close notification" onClick={onClose} />
  </div>
}

function Stat({ label, value, sub, tone, title }: { label: string, value: string, sub?: string, tone?: string, title?: string }) {
  return <div className={`stat d-grid gap-1 border rounded bg-body-secondary p-3 ${tone ?? ''}`} title={title}>
    <span className="eyebrow">{label}</span>
    <strong className="min-w-0 fs-5 lh-1 text-truncate">{value}</strong>
    {sub && <small className="small text-truncate">{sub}</small>}
  </div>
}

// A full storage room is a harder limit than what you currently hold: past it there is nothing to buy,
// and every full-length shift runs a shortage until the room itself is bigger. Warning only, never
// blocking, since a crew built for fighting does not have to be supplyable for street work.
function StorageSupplyNotice({ dashboard }: { dashboard: Dashboard }) {
  const report = dashboard.crewReport
  const hoesOver = dashboard.hoes > report.hoesStorageCanSupply
  const thugsOver = dashboard.thugs > report.thugsStorageCanSupply
  if (!hoesOver && !thugsOver) return null

  const over: string[] = []
  if (hoesOver) over.push(`${number.format(report.hoesStorageCanSupply)} of your ${number.format(dashboard.hoes)} hoes`)
  if (thugsOver) over.push(`${number.format(report.thugsStorageCanSupply)} of your ${number.format(dashboard.thugs)} thugs`)

  return <div className="d-grid gap-1 mt-3 border rounded border-start-thick border-start-danger px-3 py-3">
    <strong className="text-danger">Your storage room cannot supply this crew</strong>
    <span className="text-body-secondary small lh-sm">
      Even completely full, a level {dashboard.hideout.storageLevel} room carries {over.join(' and ')} through a
      full-length street action. Every shift past that runs a shortage and morale falls.
      {report.storageLevelToSupplyCrew
        ? ` A level ${report.storageLevelToSupplyCrew} storage room would cover them.`
        : ' No storage room in the game is big enough for a crew this size.'}
      {/* The answer that costs nothing: a crew too big for a full shift is usually fine on a shorter one. */}
      {report.suppliedStreetActionTurns > 0
        ? ` Or work ${number.format(report.suppliedStreetActionTurns)} turns at a time instead of ${number.format(dashboard.maxActionTurns)}, which this room does supply.`
        : ' Until then even a single turn runs short.'}
    </span>
  </div>
}

function CrewCard({ name, count, desc, tone, cap, trend }: { name: string, count: number, desc: string, tone?: string, cap?: number, trend?: ReactNode }) {
  const edge = tone === 'good' ? 'border-success' : tone === 'warn' ? 'border-warning' : tone === 'danger' ? 'border-danger' : ''
  return <div className={`border rounded bg-body-secondary p-3 ${edge}`}>
    <span className="text-body-secondary">{name}</span>
    <strong className="d-block fs-3 my-1 text-primary">
      {number.format(count)}{cap !== undefined && <small className="text-body-secondary fw-bold"> / {number.format(cap)}</small>}
    </strong>
    <p className="text-body-secondary m-0">{desc}{trend}</p>
  </div>
}


function InventoryCard({ name, count, note }: { name: string, count: number, note: string }) {
  return <div className="inventory-card d-grid gap-1 align-content-center border rounded bg-body-secondary p-3">
    <span className="eyebrow">{name}</span>
    <strong className="fs-3 text-primary lh-1">{number.format(count)}</strong>
    <small className="text-body-tertiary">{note}</small>
  </div>
}

// hireBlocked and fireBlocked are sentences rather than flags, because the two callers stop these
// buttons for different reasons - morale on one side, the last pimp on the other - and only the caller
// knows which. A boolean here could only ever be answered with a shrug.
function CrewManageRow({ label, owned, quantity, hireCost, cash, busy, hireBlocked, fireBlocked, onQuantity, onHire, onFire, note, trims = [], firePenalty = 0, maxFirePenalty = 0 }: {
  label: string
  owned: number
  quantity: number
  hireCost: number
  cash: number
  busy: boolean
  hireBlocked?: Blocked
  fireBlocked?: Blocked
  onQuantity: (quantity: number) => void
  onHire: () => void
  onFire: () => void
  note: string
  /** Sizes worth cutting down to, so the player is not left doing the arithmetic themselves. */
  trims?: { label: string, cut: number }[]
  firePenalty?: number
  maxFirePenalty?: number
}) {
  const totalCost = quantity * hireCost
  // What letting this many go actually costs. The button used to give no hint until after it landed,
  // and firing a dozen is a severe hit.
  const moraleCost = Math.min(maxFirePenalty || Infinity, quantity * firePenalty)
  const worthTrimming = trims.filter(t => t.cut > 0 && t.cut <= owned)

  return <div className="crew-manage-row d-grid gap-2 align-items-center py-3 border-top">
    <div className="d-grid gap-1">
      <strong>{label}</strong>
      <span className="text-body-secondary">{number.format(owned)} owned | {money.format(hireCost)} each | {note}</span>
      {worthTrimming.length > 0 && <span className="d-flex flex-wrap gap-1 column-gap-3 mt-1 small text-body-tertiary">
        {worthTrimming.map(trim => <Button
          type="button"
          key={trim.label}
          className="btn btn-link"
          blocked={busy && BUSY}
          onClick={() => onQuantity(trim.cut)}
        >
          let {number.format(trim.cut)} go to {trim.label}
        </Button>)}
      </span>}
      {firePenalty > 0 && quantity > 0 && <span className="d-flex flex-wrap gap-1 column-gap-3 mt-1 small text-body-tertiary">
        Firing {number.format(quantity)} costs {moraleCost.toFixed(0)}% morale{moraleCost >= (maxFirePenalty || Infinity) ? ', the most a single cut can' : ''}.
      </span>}
    </div>
    <input className="form-control" aria-label={`${label} quantity`} type="number" min={1} max={1000} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <Button className="btn btn-primary btn-sm" blocked={firstReason(
      busy && BUSY,
      quantity < 1 && 'Take on at least one.',
      hireBlocked,
      cash < totalCost && `${number.format(quantity)} ${label.toLowerCase()} cost ${money.format(totalCost)} and you are carrying ${money.format(cash)}.`,
    )} onClick={onHire}>Hire</Button>
    <Button className="btn btn-secondary btn-sm" blocked={firstReason(
      busy && BUSY,
      quantity < 1 && 'Let at least one go.',
      fireBlocked,
    )} onClick={onFire}>Fire</Button>
  </div>
}

function SellRow({ name, owned, price, quantity, onQuantity, onSell, blocked }: {
  name: string
  owned: number
  price: number
  quantity: number
  onQuantity: (quantity: number) => void
  onSell: () => void
  blocked?: Blocked
}) {
  return <div className="sell-row d-grid gap-2 align-items-center border-top pt-2">
    <div className="d-grid gap-1">
      <strong>{name}</strong>
      <span className="text-body-secondary">{number.format(owned)} owned | {money.format(price)} each</span>
    </div>
    <input className="form-control" type="number" min={1} max={Math.max(1, owned)} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <Button className="btn btn-secondary btn-sm" blocked={firstReason(
      blocked,
      quantity < 1 && 'Sell at least one.',
      quantity > owned && `You are selling ${number.format(quantity)} and you have ${number.format(owned)}.`,
    )} onClick={onSell}>Sell</Button>
  </div>
}

function StatusRow({ label, value, warn, trend }: { label: string, value: string, warn?: boolean, trend?: ReactNode }) {
  return <div className="status-row d-flex justify-content-between gap-3 py-2 border-top">
    <span className="text-body-secondary">{label}</span>
    <strong className={`text-end text-break ${warn ? 'text-primary' : 'text-body'}`}>{value}{trend}</strong>
  </div>
}

const MORALE_ARROWS: Record<MoraleDirection, string> = { up: '▲', down: '▼', steady: '–', unknown: '' }

// An arrow is a claim about the past, so it only appears when the server actually has a baseline to
// compare against. No recent activity reports nothing rather than a flat line nobody earned.
function MoraleArrow({ trend, crew }: { trend: MoraleTrend, crew: 'hoe' | 'thug' }) {
  const direction = crew === 'hoe' ? trend.hoeDirection : trend.thugDirection
  const delta = crew === 'hoe' ? trend.hoeDelta : trend.thugDelta
  if (direction === 'unknown') return null

  const sign = delta !== null && delta !== undefined && delta > 0 ? '+' : ''
  const title = direction === 'steady'
    ? 'Steady since your last action'
    : `${sign}${delta?.toFixed(1)} since your last action`
  const tone = direction === 'up' ? 'text-success' : direction === 'down' ? 'text-danger' : 'text-body-tertiary'
  return <em className={`fst-normal ms-1 small ${tone}`} title={title}>{MORALE_ARROWS[direction]}</em>
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
