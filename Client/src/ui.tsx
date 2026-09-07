import React, { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import * as bootstrap from 'bootstrap'
import type { Account, AccountInviteKey, AdminBetaKey, GameAnnouncement, PlayerProfile,
  PlayerTarget, PlayerTitle, ProfileBanner } from './api'
import { onRouteChange, routeTab, writeRoute } from './route'
import { compactDateTime, signedMoney } from './format'
import type { AppPage } from './pagecontext'

/*
  The controls every page shares, and the idea behind all of them.

  Pulled out of main.tsx so that a page can be loaded on its own without dragging the whole app in
  behind it. Nothing here knows what page it is on, which is what makes it safe for all of them.
*/

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
export type Blocked = string | false | null | undefined

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
export function firstReason(...reasons: Blocked[]): string | null {
  return reasons.find((reason): reason is string => typeof reason === 'string' && reason !== '') ?? null
}

// The reason almost every button in the game can give, because almost every one of them waits on the
// same in-flight request.
export const BUSY = 'Hold on - your last move is still going through.'


// The same thing said behind the admin desk, where the buttons act on the game rather than play it.
export const WORKING = 'Hold on - the last request is still going through.'

export function Button({ blocked, className, title, onClick, children, ...rest }: {
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

/**
 * One figure with its name over it. Small enough to have lived wherever it was first needed, shared
 * enough now that a page loaded on its own would otherwise have to bring the admin desk with it.
 */
export function AdminMetric({ label, value, sub }: { label: string, value: string, sub?: string }) {
  return <div className="d-grid gap-1 border rounded bg-body-secondary px-3 py-2">
    <span className="eyebrow">{label}</span>
    <strong className="min-w-0 fs-5 text-break">{value}</strong>
    {sub && <small className="small text-body-tertiary">{sub}</small>}
  </div>
}

export function DismissibleMessage({ className, children, onClose }: { className: string, children: ReactNode, onClose: () => void }) {
  return <div className={`${className} d-flex align-items-center justify-content-between gap-3`}>
    <span>{children}</span>
    <button className="btn-close" type="button" aria-label="Close notification" onClick={onClose} />
  </div>
}

export function StatusRow({ label, value, warn, trend }: { label: string, value: string, warn?: boolean, trend?: ReactNode }) {
  return <div className="status-row d-flex justify-content-between gap-3 py-2 border-top">
    <span className="text-body-secondary">{label}</span>
    <strong className={`text-end text-break ${warn ? 'text-primary' : 'text-body'}`}>{value}{trend}</strong>
  </div>
}

export function ActivityList({ entries }: { entries: { id: number, action: string, createdAtUtc: string, summary: string }[] }) {
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

/** Three states and no fourth: a key is waiting, spent, or taken back. It never goes off on its own. */
export function betaKeyStatusClass(status: AccountInviteKey['status'] | AdminBetaKey['status']) {
  return status === 'Available'
    ? 'text-bg-success'
    : status === 'Used'
      ? 'text-bg-primary'
      : 'text-bg-secondary'
}

export async function copyToClipboard(value: string) {
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

export function percent(value: number) {
  return `${(value * 100).toFixed(value < 0.1 ? 1 : 0)}%`
}

export const updateCategories: GameAnnouncement['category'][] = ['Info', 'Patch', 'Balance', 'Event', 'Maintenance']

export const updateSeverities: GameAnnouncement['severity'][] = ['Info', 'Warning', 'Event', 'Maintenance']

export function updateCategoryClass(category: GameAnnouncement['category']) {
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

export function updateSeverityClass(severity: GameAnnouncement['severity']) {
  return severity === 'Warning'
    ? 'text-bg-warning'
    : severity === 'Event'
      ? 'text-bg-success'
      : severity === 'Maintenance'
        ? 'text-bg-danger'
        : 'text-bg-light border'
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
export function useRouteTab<T extends string>(page: AppPage, allowed: readonly T[], fallback: T): [T, (next: T) => void] {
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

export function PlayerAvatar({ name, username = name, avatarUrl, size = 36 }: { name: string, username?: string, avatarUrl?: string | null, size?: number }) {
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

export function bannerClass(banner: ProfileBanner) {
  return banner === 'None' ? 'border' : `profile-banner-${banner.toLowerCase()}`
}

export function profileAccentClass(accent: Account['profileAccent'] | PlayerTarget['profileAccent']) {
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
export function useSecondsTicker(active: boolean) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [active])
  return now
}

/** Whole seconds between now and a deadline, floored at zero. */
export function secondsUntil(iso: string | null | undefined, now: number) {
  if (!iso) return 0
  return Math.max(0, Math.ceil((new Date(iso).getTime() - now) / 1000))
}

export function countdown(seconds: number) {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${String(s).padStart(2, '0')}`
}

export function ProfileBadgeStrip({ badges }: { badges: PlayerProfile['profileBadges'] }) {
  if (badges.length === 0) return null
  return <div className="d-flex flex-wrap gap-1 mt-1">
    {badges.map(badge => <span className="badge text-bg-secondary d-inline-flex align-items-center gap-1" title={badge.detail} key={badge.key}>
      {badge.key === 'discord-connected' && <i className="bi bi-discord" aria-hidden="true" />}
      {badge.label}
    </span>)}
  </div>
}

export function timeUntil(value: string) {
  const seconds = Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000))
  const minutes = Math.floor(seconds / 60)
  const remainder = seconds % 60
  // Hideout builds run for hours, where a bare minute count stops being readable.
  if (minutes >= 60) return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m`
  return minutes <= 0 ? `${seconds}s` : `${minutes}m ${String(remainder).padStart(2, '0')}s`
}

/**
 * A second hand, so a countdown moves while somebody is looking at it rather than only when something
 * else happens to redraw the page.
 */
export function useSecondHand(active: boolean) {
  const [, setTick] = useState(0)
  useEffect(() => {
    if (!active) return
    const timer = window.setInterval(() => setTick(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [active])
}

export function PlayerName({ playerId, children, className }: {
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

// strip only draws them.
//
// The strip pins itself to the top of the screen on a phone, which is what the
// section-tabs class is for. The pages under it are the longest in the game -
// the hideout is four panels and a room list, the runs page five - and a strip
// that scrolls away means changing tab starts with scrolling back up to find the
// tabs. A flick on a desktop; a journey on a phone.
export function SectionTabs<T extends string>({ label, tabs, active, onActive }: {
  label: string
  tabs: { key: NoInfer<T>, label: string }[]
  active: T
  onActive: (key: T) => void
}) {
  return <nav className="section-tabs nav nav-pills gap-2" aria-label={label}>
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

/**
 * A countdown long enough to be a season.
 *
 * timeUntil tops out at hours, which is right for everything it was written for - a build, a mission,
 * a shift, all of which finish inside a day. A season runs for a month, and "719h 04m" is not a number
 * anybody reads as a date. Days first here, and the minutes only once the days have gone.
 */
export function timeLeft(value: string) {
  const seconds = Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000))
  const days = Math.floor(seconds / 86_400)
  return days > 0 ? `${days}d ${Math.floor((seconds % 86_400) / 3600)}h` : timeUntil(value)
}
