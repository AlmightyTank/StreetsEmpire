import React, { FormEvent, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { adminApi, api, cheapestWeapon, configApi, discordStartUrl, opsApi, RequestError } from './api'
import type { Account, AuthProviders, DiscordOutcome, DiscordSignUpTicket, BlockedList, ChatBoard, ChatChannelKey, ChatConversation, ChatConversationList, Person, ActionResult, AdminAuditEntry, Alert, AdminConfig, AdminConfigEntry, AdminOverview, AdminBotHealth, AdminOversight, AdminPlayerDetail, AdminPlayerSummary, AllianceBoard, AllianceBrief, AllianceDoorKey, AllianceMember, AlliancePower, AllianceRequest, AllianceSummary, AttackMethod, AttackMethodKey, PrayerBoard, PlayerTitle, StreetDistrict, WeaponTier, WeaponTierKey, CombatLog, CombatMission, Dashboard, HideoutRoom, HideoutRoomUpgrade, LeaderboardEntry, LiveOps, Pimp, BotDirective, MoraleDirection, MoraleTrend, MarketBoard, MuleBoard, MuleQuote, ContractBoard, PlayerProfile, PlayerTarget, TerritoryBoard, TravelStatus, WorldNews, WorldNewsEntry, CatchUp, CityMarket } from './api'
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

type AppPage = 'overview' | 'street' | 'crew' | 'market' | 'recon' | 'alliance' | 'account' | 'admin'

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
  crew: { label: 'Crew', short: 'CR', kicker: 'Morale and hiring' },
  market: { label: 'Business', short: 'BZ', kicker: 'Shop, rooms, craft, runs' },
  recon: { label: 'Raids & Map', short: 'RM', kicker: 'Targets and territory' },
  alliance: { label: 'Alliance', short: 'AL', kicker: 'Who you run with' },
  account: { label: 'Account', short: 'AC', kicker: 'How you get in' },
  admin: { label: 'Admin', short: 'AD', kicker: 'Control centre' },
}

function flowPage(page: string): AppPage {
  if (page === 'hideout' || page === 'mules') return 'market'
  if (page === 'territory') return 'recon'
  return page in pageMeta ? page as AppPage : 'overview'
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
const tourSeenKey = 'street-empire.walkthrough.seen'

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
  'already-connected': { text: 'This account already has a Discord connected. Disconnect that one first.', bad: true },
  cancelled: { text: 'Discord sign-in was cancelled.' },
  failed: { text: 'Discord could not finish signing you in. Try again.', bad: true },
  locked: { text: 'That account is banned or suspended.', bad: true },
  unavailable: { text: 'Discord sign-in is not set up on this server.', bad: true },
}

const tourSteps: { page: AppPage, target: string, title: string, body: string }[] = [
  {
    page: 'overview',
    target: 'status',
    title: 'Your numbers',
    body: 'Cash, turns, crew and heat. Turns are the real currency - almost everything worth doing spends '
      + 'them, and they come back slowly on their own. Heat is how much attention you have drawn; let it '
      + 'climb and the busts start.',
  },
  {
    page: 'overview',
    target: 'ladder',
    title: 'What to do next',
    body: 'The opening ladder, in order. Each rung says why it is worth doing, and clicking one takes you '
      + 'to the page where it happens. If you ever lose the thread, come back here.',
  },
  {
    page: 'street',
    target: 'street-action',
    title: 'Working the streets',
    body: 'Where turns become money. Your hoes earn, your thugs guard them, and you pick up new crew while '
      + 'you are out. It costs supplies - condoms and beer - so a shift you cannot supply pays less and '
      + 'sours the crew.',
  },
  {
    page: 'market',
    target: 'rooms',
    title: 'The hideout is the engine',
    body: 'Every room does one job: the store decides how big a crew you can feed, the labs make product '
      + 'while you are away, the safe keeps cash out of a raider\'s hands. Nothing you buy here is lost - '
      + 'a building counts towards your standing at every pound it cost.',
  },
  {
    page: 'market',
    target: 'market-trade',
    title: 'Buying and selling',
    body: 'Prices differ by town, so what is dear here is cheap somewhere else. This is also where you bank '
      + 'cash, build rooms, run mules, and handle production: money on hand is stolen in a raid and money in the bank is not, which is the cheapest '
      + 'insurance in the game.',
  },
  {
    page: 'recon',
    target: 'targets',
    title: 'Other people',
    body: 'You can look up any player and take what they have, and they can do the same to you. You are only '
      + 'matched against people worth robbing and able to fight back, so nobody can farm a newcomer - and '
      + 'your buildings are never part of what is on the table.',
  },
]

function Walkthrough({ active, stepIndex, onPage, onStep, onClose }: {
  active: boolean
  stepIndex: number
  onPage: (page: AppPage) => void
  onStep: (index: number) => void
  onClose: () => void
}) {
  const [rect, setRect] = useState<DOMRect | null>(null)
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
    onPage(step.page)
  }, [active, stepIndex])

  // Measure after the page has had a frame to render, and keep measuring while things move.
  useEffect(() => {
    if (!active || !step) return

    let frame = 0
    const measure = () => {
      const node = document.querySelector(`[data-tour="${step.target}"]`)
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
      <div className="d-flex gap-1">
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

      {channel === 'Direct' && <div className="d-flex align-items-center justify-content-between gap-2">
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
        : <div className="chat-log d-grid align-content-start gap-1 border rounded bg-body-tertiary p-2 overflow-y-auto" ref={log} onScroll={onScroll}>
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
              className={`chat-thread d-grid text-start border rounded bg-body-secondary p-2 ${row.unread > 0 ? 'border-primary' : ''}`}
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
              <strong className={`text-nowrap ${line.yours ? 'text-body' : 'text-primary'}`}>{line.author}</strong>
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
        <button className="btn btn-primary btn-sm" type="submit" disabled={busy || sending || over || draft.trim().length === 0}>Send</button>
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
      <button className="btn btn-primary btn-sm" type="button" disabled={busy || working || chosen.length === 0} onClick={() => void start()}>
        {chosen.length > 1 ? `Start group of ${chosen.length + 1}` : 'Open'}
      </button>
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
          <strong className={`text-nowrap ${line.yours ? 'text-body' : 'text-primary'}`}>{line.author}</strong>
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
        <button className="btn btn-primary btn-sm" type="submit" disabled={busy || sending || over || draft.trim().length === 0}>Send</button>
      </form>
    </>}
  </section>
}

function App() {
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
  const [activePage, setActivePage] = useState<AppPage>('overview')
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  // Fetched rather than hardcoded: the towns come from the territory map, so a city with no ground
  // could never be offered as somewhere to set up.
  const [cities, setCities] = useState<string[]>([])
  useEffect(() => { void api.cities().then(setCities).catch(() => setCities([])) }, [])
  // Which doors this server can actually open. A button for a provider with no credentials behind it
  // is a button that fails, so it is never drawn.
  const [providers, setProviders] = useState<AuthProviders>({ discord: false })
  useEffect(() => { void api.providers().then(setProviders).catch(() => setProviders({ discord: false })) }, [])
  // A Discord login that turned out to belong to nobody yet, waiting on a name and a town.
  const [discordTicket, setDiscordTicket] = useState<DiscordSignUpTicket | null>(null)
  /*
    Where the sign-in card is in the reset flow, if it is in it at all. Two steps rather than one
    screen, because the second needs a code that does not exist until the first has run - and the
    identifier is carried between them rather than re-typed, since it is the thing the server matches
    the code against.
  */
  const [resetStep, setResetStep] = useState<'off' | 'asking' | 'confirming'>('off')
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

  /**
   * Full reload after an action. `pollMissions` instead re-reads only what a running mission changes,
   * which keeps the 5-second poll from re-fetching the leaderboard, world news and target list.
   */
  const refresh = async () => {
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
      window.history.replaceState({}, '', window.location.pathname + (query ? `?${query}` : ''))
      if (outcome === 'sign-up') sessionStorage.setItem(discordPendingKey, '1')
      const said = discordOutcomes[outcome]
      if (said?.bad) setError(said.text)
      else if (said) setNotice(said.text)
      // Connecting is something you were doing on the account page, so that is where you come back to.
      if (outcome === 'connected' || outcome === 'already-connected') setActivePage('account')
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
    if (activePage === 'admin' && !adminOverview)
      setActivePage('overview')
  }, [activePage, adminOverview])
  useEffect(() => {
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
        await api.register(String(form.get('username')), String(form.get('password')), String(form.get('playerName')), String(form.get('city')), String(form.get('email') ?? ''))
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
        String(form.get('playerName')),
        String(form.get('city')),
        String(form.get('email') ?? ''))
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
      you are and has no opinion about what you want to be called or which town you are setting up in.
      So the sign-in card steps aside for a shorter form that asks only those two, and the identity
      behind it stays where it was put - in a signed cookie the browser cannot read or forge.
    */
    return <main className="auth-shell d-grid place-items-center p-4">
      <section className="auth-card card p-4">
        <div className="brand-mark d-grid place-items-center border border-primary text-primary fw-bolder mb-3">SE</div>
        <h1>Street Empire</h1>

        {resetStep !== 'off'
          ? <>
            <p className="text-body-secondary">
              {resetStep === 'asking'
                ? 'A code goes to the confirmed email address on the account. Without one there is no way back in - which is what confirming an address is for.'
                : 'Type the code from that email and pick a new password. Every other session on the account will be signed out.'}
            </p>
            {resetStep === 'asking'
              ? <form className="d-grid gap-3 mt-4" onSubmit={startReset}>
                <label className="field">
                  Username or Email
                  <input className="form-control" name="identifier" maxLength={254} required autoFocus />
                </label>
                {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
                <button className="btn btn-primary" disabled={busy}>{busy ? 'Working...' : 'Send Me a Code'}</button>
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
                <button className="btn btn-primary" disabled={busy}>{busy ? 'Working...' : 'Set My Password'}</button>
                <button className="btn btn-link text-body-secondary" type="button" onClick={() => setResetStep('asking')}>Send another code</button>
                <button className="btn btn-link text-body-secondary" type="button" onClick={leaveReset}>Back to signing in</button>
              </form>}
          </>
          : discordTicket
          ? <>
            <p className="text-body-secondary">
              Signed in as <strong className="text-primary">{discordTicket.discordUsername}</strong> on Discord.
              Two things left before you have an empire.
            </p>
            <form className="d-grid gap-3 mt-4" onSubmit={finishDiscordSignUp}>
              <label className="field">
                Username
                <input className="form-control" name="username" defaultValue={discordTicket.suggestedUsername} minLength={3} maxLength={32} required />
                <small className="form-text">What you would sign in as if you ever set a password.</small>
              </label>
              <label className="field">Player Name<input className="form-control" name="playerName" minLength={3} maxLength={32} required /></label>
              <label className="field">
                Town
                <select className="form-select" name="city" defaultValue={cities[0] ?? ''}>
                  {cities.map(city => <option key={city} value={city}>{city}</option>)}
                </select>
                <small className="form-text">Turf is contested inside a town rather than between them, so this is the ground you start out fighting for. Moving to another town later costs turns.</small>
              </label>
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
              <button className="btn btn-primary" disabled={busy}>{busy ? 'Working...' : 'Build My Empire'}</button>
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
              {/* One box either way. Which kind of name is in it gets decided by the @, server-side. */}
              <label className="field">
                {authMode === 'login' ? 'Username or Email' : 'Username'}
                <input className="form-control" name="username" minLength={3} maxLength={254} required />
              </label>
              {authMode === 'register' && <label className="field">Player Name<input className="form-control" name="playerName" minLength={3} maxLength={32} required /></label>}
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
              {authMode === 'register' && <label className="field">
                Town
                <select className="form-select" name="city" defaultValue={cities[0] ?? ''}>
                  {cities.map(city => <option key={city} value={city}>{city}</option>)}
                </select>
                <small className="form-text">Turf is contested inside a town rather than between them, so this is the ground you start out fighting for. Moving to another town later costs turns.</small>
              </label>}
              <label className="field">Password<input className="form-control" name="password" type="password" minLength={8} required /></label>
              {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
              {notice && <DismissibleMessage className="alert alert-success" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
              <button className="btn btn-primary" disabled={busy}>{busy ? 'Working...' : authMode === 'login' ? 'Enter the City' : 'Build My Empire'}</button>
              {/* Only on the login side. Offering it while somebody is creating an account is offering
                  to reset a password they have not chosen yet. */}
              {authMode === 'login' && <button
                className="btn btn-link text-body-secondary"
                type="button"
                onClick={() => { setResetStep('asking'); setError(''); setNotice('') }}
              >Forgotten your password?</button>}
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
  // first moment the targets exist to be pointed at.
  if (!tourOffered.current && dashboard && localStorage.getItem(tourSeenKey) === null) {
    tourOffered.current = true
    queueMicrotask(() => setTourStep(0))
  }

  return <main className="game-shell d-grid">
    {catchUp && <CatchUpDialog news={catchUp} onClose={() => setCatchUp(null)} />}
    <ChatWindows dashboard={dashboard} busy={busy} />
    <Walkthrough
      active={tourStep !== null}
      stepIndex={tourStep ?? 0}
      onPage={setActivePage}
      onStep={setTourStep}
      onClose={() => { setTourStep(null); localStorage.setItem(tourSeenKey, '1') }}
    />
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
        <small className="text-body-tertiary">0.2.6</small>
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
  setActivePage: (page: AppPage) => void
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
    case 'recon': return <CombatPage {...ctx} />
    case 'alliance': return <AlliancePage {...ctx} />
    case 'account': return <AccountPage {...ctx} />
    case 'admin': return ctx.adminOverview
      ? <AdminPage {...ctx} overview={ctx.adminOverview} />
      : <OverviewPage {...ctx} />
    default: return <OverviewPage {...ctx} />
  }
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
          <button className="btn btn-secondary" onClick={() => setActivePage('recon')}>Raids & Map</button>
        </div>
      </section>

      <NextMovePanel dashboard={dashboard} onPage={setActivePage} />
      <OpeningLadderPanel dashboard={dashboard} onPage={setActivePage} onTour={ctx.openTour} />

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
        <StatusRow label="Condoms for a full shift" value={`${dashboard.condoms}/${dashboard.crewReport.condomsNeededForMaxStreetAction}`} warn={dashboard.condoms < dashboard.crewReport.condomsNeededForMaxStreetAction} />
        <StatusRow label="Beer for a full shift" value={`${dashboard.beer}/${dashboard.crewReport.beerNeededForMaxStreetAction}`} warn={dashboard.beer < dashboard.crewReport.beerNeededForMaxStreetAction} />
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

    <WorldNewsPanel news={worldNews} currentPlayer={dashboard.name} />
  </div>
}

function StreetPage(ctx: PageContext) {
  const { dashboard, combatMissions, busy, streetTurns, autoBuySupplies, hoeCut, bankAmount, storeQty, district, setActivePage, setStreetTurns, setAutoBuySupplies, setHoeCut, setBankAmount, setStoreQty, setDistrict, act } = ctx
  const pendingOutgoingAttack = combatMissions.find(mission => mission.attackerId === dashboard.playerId && mission.status !== 'Complete')
  const restock = restockEstimate(dashboard, streetTurns)
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    <section className="card p-3 gcol-full" data-tour="street-action">
      <div className="panel-title"><h2>Work the Streets</h2><span>Income + recruiting</span></div>
      <p>Your hoes earn, and their cut comes off the top before anything reaches your pocket. A shift also turns up new crew and whatever is lying about.</p>
      {pendingOutgoingAttack && <div className="d-flex justify-content-between align-items-center gap-3 border border-primary rounded bg-body-tertiary px-3 py-2 mt-3">
        <strong className="text-primary">Crew is out</strong>
        <span className="text-body-secondary text-end">Street work unlocks after the next mission update in {timeUntil(nextMissionTime(pendingOutgoingAttack))}.</span>
      </div>}
      <DistrictPicker districts={dashboard.districts} selected={district} onSelect={setDistrict} />
      <StorageSupplyNotice dashboard={dashboard} />
      <StreetSupplyPanel
        dashboard={dashboard}
        busy={busy}
        streetTurns={streetTurns}
        storeQty={storeQty}
        setStoreQty={setStoreQty}
        act={act}
        onMarket={() => setActivePage('market')}
      />
      <div className="control-row">
        <label className="field">Turns<input className="form-control" type="number" min={1} max={dashboard.maxActionTurns} value={streetTurns} onChange={e => setStreetTurns(Number(e.target.value))} /></label>
        <label className="field">Hoe Cut %<input className="form-control" type="number" min={10} max={80} value={hoeCut} onChange={e => setHoeCut(Number(e.target.value))} /></label>
        <button className="btn btn-secondary" disabled={busy || hoeCut < 10 || hoeCut > 80 || hoeCut === dashboard.hoeCutPercent} onClick={() => void act(() => api.setHoeCut(hoeCut))}>Save Cut</button>
        <button className="btn btn-primary" disabled={busy || !!pendingOutgoingAttack || streetTurns < 1 || streetTurns > dashboard.turns || streetTurns > dashboard.maxActionTurns} onClick={() => void act(() => api.workStreet(streetTurns, autoBuySupplies, district || undefined))}>{pendingOutgoingAttack ? 'Crew Out' : `Work ${streetTurns} Turn${streetTurns === 1 ? '' : 's'}`}</button>
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

function CrewPage(ctx: PageContext) {
  const { dashboard, busy, crewQty, totalCrew, weaponCoverage, managementCapacity, setCrewQty, act } = ctx
  const combatCrew = dashboard.combatCrew
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start">
    <ShrinePanel busy={busy} act={act} />
    <section className="card p-3 gcol-full">
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

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Crew Management</h2><span>Hire + fire</span></div>
      <div className="d-grid">
        <CrewManageRow
          label="Pimps"
          owned={dashboard.pimps}
          quantity={crewQty.pimps}
          hireCost={dashboard.crewReport.hirePimpCost}
          cash={dashboard.cash}
          busy={busy}
          canFire={dashboard.pimps - crewQty.pimps >= 1}
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
          canHire={dashboard.hoeHappiness >= dashboard.crewReport.minHoeMoraleToHire}
          canFire={dashboard.hoes >= crewQty.hoes}
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
          canHire={dashboard.thugHappiness >= dashboard.crewReport.minThugMoraleToHire}
          canFire={dashboard.thugs >= crewQty.thugs}
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
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    <section className="card p-3 gcol-full">
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

    <section className="card p-3 gcol-full" data-tour="rooms">
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
        />
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
        />
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

  if (!board) return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-md-1"><section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Mules</h2><span>Loading</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
  </section></div>

  const free = board.pimps.filter(p => !p.isAway)
  const out = board.runs.filter(r => r.status !== 'Done')
  const home = board.runs.filter(r => r.status === 'Done')
  const spread = quote ? quote.homePrice - quote.unitPriceThere : 0
  const unspendable = quote ? quote.cashSent - quote.unitsAffordable * quote.unitPriceThere : 0

  const send = async () => {
    if (!pimpId) return
    await act(() => api.launchMule(city, good, hoes, cash, pimpId))
    await load()
  }

  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-md-1">
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
        <span>You need an intelligence centre before you can run mules. Build one under Business / Hideout.</span>
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
        <button
          className="btn btn-primary"
          disabled={busy || !pimpId || board.runsOut >= board.concurrentRunCap || hoes > board.hoesAvailable}
          onClick={() => void send()}
        >
          Send {quote.hoes} hoe(s) to {quote.destinationCity}
        </button>
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
    <div className="panel-title"><h2>Craft Queue</h2><span>Workbench crafts</span></div>
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
        const canStart = built
          && !activeCraft
          && !busy
          && workUnits >= 1
          && workUnits <= dashboard.maxActionTurns
          && workUnits <= dashboard.turns
          && totalCost <= dashboard.cash
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
              <button
                className="btn btn-primary btn-sm"
                disabled={!canStart}
                onClick={() => void act(() => api.produce(key, workUnits))}
              >
                Queue {quantityLabel}
              </button>
            </>}
            <label className="field">Sell Qty<input className="form-control"
              type="number"
              min={1}
              max={Math.max(1, held)}
              value={saleQty}
              onChange={e => setSellQty(v => ({ ...v, [key]: Number(e.target.value) }))}
            /></label>
            <button
              className="btn btn-secondary btn-sm"
              disabled={busy || saleQty < 1 || saleQty > held}
              onClick={() => void act(() => api.sellProduct(key, saleQty))}
            >
              Sell
            </button>
          </div>
        </div>
      })}
      {crafts.map(station => {
        const runTurns = turns[station.key] ?? 5
        const built = station.level > 0
        const quantity = station.perTurn * runTurns
        const totalCost = station.costPerUnit * quantity
        const canStart = built
          && !activeCraft
          && !busy
          && runTurns >= 1
          && runTurns <= dashboard.maxActionTurns
          && runTurns <= dashboard.turns
          && totalCost <= dashboard.cash
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
              <button
                className="btn btn-primary btn-sm"
                disabled={!canStart}
                onClick={() => void act(() => api.forge(runTurns, station.weapon ? 'workshop' : station.key, station.weapon as WeaponTierKey | undefined))}
              >
                Queue {number.format(quantity)}
              </button>
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
    {blocked
      ? <p className="text-body-tertiary small mt-3">{blocked}</p>
      : <p className="text-body-tertiary small mt-3">
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
      <button
        className="btn btn-primary btn-sm"
        disabled={busy || blocked !== null || batch <= 0 || turnsNeeded > dashboard.turns}
        onClick={() => void act(() => api.cutCoke(turns))}
      >
        Cut {number.format(batch)} coke
      </button>
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
          <div className="room-row">
            <div className="room-copy">
              <strong>{next.name}</strong>
              <span>
                {money.format(next.cost)} and {next.turns} turns. Takes {next.buildMinutes} minutes to build.
                Paid from the bank first, then cash on hand.
              </span>
            </div>
            <em>Tier {next.level}</em>
            <button
              className="btn btn-primary"
              disabled={busy || !canAffordTier || dashboard.turns < next.turns}
              onClick={() => void act(() => api.upgradeHideout('tier'))}
            >
              {!canAffordTier ? 'You cannot cover it' : dashboard.turns < next.turns ? `${next.turns} turns and you have ${dashboard.turns}` : 'Start building'}
            </button>
          </div>
        </>
        : <p>The {hideout.tierName} is the biggest building there is. Nothing left to move up to.</p>}
  </section>
}

// funds, not cash on hand: the server pays for a room from the bank first, because the safe is one of
// the things being bought and several rooms cost more than the safe below them holds.
function RoomRow({ name, level, detail, upgrade, funds, busy, onUpgrade }: {
  name: string
  level: number
  detail: string
  upgrade?: HideoutRoomUpgrade | null
  funds: number
  busy: boolean
  onUpgrade: () => void
}) {
  const tierLocked = upgrade?.tierLocked ?? false
  const workshopLocked = upgrade?.workshopLocked ?? false
  const locked = tierLocked || workshopLocked
  return <div className="room-row">
    <div className="room-copy">
      <strong>{name}</strong>
      <span>{detail}</span>
      {tierLocked && workshopLocked
        ? <small>Level {upgrade!.level} needs the {upgrade!.requiredTierName} or better, and will be needing a level {upgrade!.requiredWorkshopLevel} workshop.</small>
        : <>
          {tierLocked && <small>Level {upgrade!.level} needs the {upgrade!.requiredTierName} or better.</small>}
          {workshopLocked && <small>Level {upgrade!.level} needs a level {upgrade!.requiredWorkshopLevel} workshop.</small>}
        </>}
      {/* What the upgrade actually returns. The later levels are meant to be a poor deal - somewhere
          for money to go once there is nothing left to buy - and saying so is the difference between
          a trophy and a room that quietly took a fortune while looking like an investment. */}
      {!locked && upgrade?.paybackDays != null && <small className={upgrade.paybackDays > 30 ? 'text-primary' : 'text-body-secondary'}>
        {upgrade.paybackDays > 30
          ? `Pays for itself in ${upgrade.paybackDays} days. A trophy more than an investment.`
          : `Pays for itself in ${upgrade.paybackDays} days.`}
      </small>}
    </div>
    <em>{level === 0 ? 'Not built' : `Level ${level}`}</em>
    <button className="btn btn-primary" disabled={busy || !upgrade || locked || funds < upgrade.cost} onClick={onUpgrade}>
      {!upgrade ? 'Maxed' : locked ? 'Locked' : `Upgrade ${money.format(upgrade.cost)}`}
    </button>
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

  if (!board) return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-md-1"><section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Territory</h2><span>Loading</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
  </section></div>

  const effects = board.effects
  const anyEffect = effects.streetIncomePercent || effects.productionYieldPercent || effects.moraleRecoveryPercent || effects.lootPercent
  const force = (id: number) => thugs[id] ?? board.minimumGarrison
  const chosen = (id: number) => pimpFor[id] ?? null
  // Only pimps who are actually free. Anyone out commanding a raid, or already running other ground,
  // cannot take a second posting, and the server refuses it anyway.
  const freePimps = (id: number) => dashboard.crew.filter(p => !p.isCommanding
    && !board.territories.some(t => t.id !== id && t.heldByYou && t.garrisonPimpName === p.name))

  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-md-1">
    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>{board.city}</h2>
        <span>{board.held} of {board.holdingCap} held</span>
      </div>
      <p>
        This is {board.city}, and it is the only map you fight over.
        Holding ground takes {board.minimumGarrison} thugs standing on it, and they are not at home while they do.
        You have <strong>{number.format(board.freeThugs)}</strong> free of {number.format(dashboard.thugs)}.
        Claiming empty ground costs {board.claimTurnCost} turns; taking it off somebody costs a raid and one of your two lanes.
      </p>
      {anyEffect
        ? <div className="d-flex flex-wrap gap-2 mt-3">
          {effects.streetIncomePercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.streetIncomePercent}% street income</span>}
          {effects.productionYieldPercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.productionYieldPercent}% production</span>}
          {effects.moraleRecoveryPercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.moraleRecoveryPercent}% morale recovery</span>}
          {effects.lootPercent > 0 && <span className="badge rounded-pill text-bg-secondary">+{effects.lootPercent}% haul</span>}
        </div>
        : <p className="text-body-tertiary small mt-3">You hold no ground yet, so nothing out there is working for you.</p>}
      {error && <div className="alert alert-danger"><span>{error}</span></div>}
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
          {t.isProtected && t.protectedUntilUtc && <small className="text-body-tertiary small">Settled for {timeUntil(t.protectedUntilUtc)}</small>}
          {t.blockedReason && <small className="text-body-tertiary small">{t.blockedReason}</small>}
          <div className="territory-actions d-flex flex-wrap align-items-end gap-1 mt-1">
            <label className="field">Thugs<input className="form-control"
              type="number"
              min={t.heldByYou ? 0 : board.minimumGarrison}
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
              <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => void run(() => api.setGarrison(t.id, force(t.id), chosen(t.id)))}>Set garrison</button>
              <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => void run(() => api.setGarrison(t.id, 0, null))}>Give up</button>
            </>}
            {t.canClaim && <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => void run(() => api.claimTerritory(t.id, force(t.id), chosen(t.id)))}>Claim</button>}
            {t.canRaid && <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => void run(() => api.raidTerritory(t.id, force(t.id), force(t.id)))}>Raid it</button>}
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
    <section className="card p-3 gcol-full" data-tour="market-trade">
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
          <button
            className="btn btn-primary btn-sm"
            disabled={busy || !good || qty < 1 || qty > (good?.held ?? 0) || price < 1}
            onClick={() => void run(() => api.listOnMarket(item, qty, price))}
          >
            List for {money.format(qty * price)}
          </button>
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
                ? <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => void run(() => api.cancelListing(l.id))}>Pull it</button>
                : <>
                  <input className="form-control"
                    type="number"
                    min={1}
                    max={l.quantity}
                    value={buyQty[l.id] ?? l.quantity}
                    onChange={e => setBuyQty(v => ({ ...v, [l.id]: Number(e.target.value) }))}
                  />
                  <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => void run(() => api.buyOnMarket(l.id, buyQty[l.id] ?? l.quantity))}>Buy</button>
                </>}
            </td>
          </tr>)}
        </tbody>
      </table></div>}
    </section>
  </>
}

function SectionTabs({ label, tabs, active, onActive }: {
  label: string
  tabs: { key: string, label: string }[]
  active: string
  onActive: (key: string) => void
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

function MarketPage(ctx: PageContext) {
  const [tab, setTab] = useState('trade')
  return <div className="d-grid gap-3">
    <SectionTabs
      label="Business sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'trade', label: 'Shop' },
        { key: 'hideout', label: 'Hideout' },
        { key: 'production', label: 'Craft Queue' },
        { key: 'routes', label: 'Runs' },
      ]}
    />
    {tab === 'trade' && <MarketCorePage {...ctx} />}
    {tab === 'hideout' && <HideoutPage {...ctx} />}
    {tab === 'production' && <ProductionPage {...ctx} />}
    {tab === 'routes' && <MulePage {...ctx} />}
  </div>
}

function MarketCorePage(ctx: PageContext) {
  const { dashboard, busy, bankAmount, storeQty, setBankAmount, setStoreQty, act } = ctx
  return <div className="d-grid gtc-1 gtc-xl-split-92 gap-3 align-items-start">
    <ContractsPanel dashboard={dashboard} busy={busy} act={act} />
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

    <TradingPanel {...ctx} />

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Street Store</h2><span>Cash on hand only</span></div>
      <div className="d-grid gtc-1 gtc-xl-3 gap-2 mt-3">
        {dashboard.store.map(item => {
          const qty = storeQty[item.key] ?? 1
          return <div className="store-row tnum d-grid gtc-1 gap-3 align-content-between border rounded bg-body-tertiary p-3" key={item.key}>
            <div className="min-w-0 d-grid align-content-start gap-2">
              <div className="d-flex flex-wrap align-items-center gap-2">
                <strong className="text-body fs-5">{item.name}</strong>
                <span className="eyebrow border rounded-pill text-info-emphasis px-2 py-1">{item.category}</span>
              </div>
              <p className="m-0 text-body-secondary">{item.description}</p>
            </div>
            <div className="d-grid gtc-2 gap-2 align-items-end border rounded bg-body-tertiary p-2">
              <div className="d-grid gap-1">
                <span className="eyebrow">Unit</span>
                <strong className="text-primary fs-6">{money.format(item.price)}</strong>
              </div>
              <label className="field small">Qty<input className="form-control" aria-label={`${item.name} quantity`} type="number" min={1} max={10000} value={qty} onChange={e => setStoreQty(v => ({ ...v, [item.key]: Number(e.target.value) }))} /></label>
              <div className="d-grid gap-1">
                <span className="eyebrow">Total</span>
                <strong className="text-primary fs-6">{money.format(qty * item.price)}</strong>
              </div>
              <button className="btn btn-primary btn-sm w-100" disabled={busy || qty < 1 || dashboard.cash < qty * item.price} onClick={() => void act(() => api.buyStoreItem(item.key, qty))}>Buy</button>
              {/* Rides are the only store item with a resale price, so the sell button only exists here. */}
              {item.key === 'rides' && <button
                className="btn btn-secondary btn-sm w-100"
                disabled={busy || qty < 1 || dashboard.rides < qty}
                onClick={() => void act(() => api.sellStoreItem(item.key, qty))}
              >Sell</button>}
            </div>
          </div>
        })}
      </div>
    </section>

    <BankPanel dashboard={dashboard} busy={busy} bankAmount={bankAmount} setBankAmount={setBankAmount} act={act} className="market-bank" wide />
  </div>
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
    {travel.blockedReason !== null && <p className="travel-note border-start-thick border-start-danger ps-2 text-danger-emphasis">{travel.blockedReason}</p>}
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
                <button
                  className="btn btn-secondary btn-sm"
                  disabled={busy || travel.blockedReason !== null || shortfall > 0}
                  onClick={() => void act(() => api.travel(city.city))}
                >
                  Travel
                </button>
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

function CombatPage(ctx: PageContext) {
  const [tab, setTab] = useState('targets')
  return <div className="d-grid gap-3">
    <SectionTabs
      label="Raids and map sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'targets', label: 'Raids' },
        { key: 'ground', label: 'Map' },
      ]}
    />
    {tab === 'targets' && <ReconPage {...ctx} />}
    {tab === 'ground' && <TerritoryPage {...ctx} />}
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
      <button
        className="btn btn-secondary btn-sm"
        disabled={busy}
        onClick={() => {
          if (window.confirm(`Cancel this attack for ${money.format(mission.cancelCashCost)}?`))
            onCancel(mission.id)
        }}
      >Cancel Mission</button>
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

const ADMIN_TABS = ['overview', 'players', 'ai', 'config', 'liveops', 'audit'] as const
type AdminTab = typeof ADMIN_TABS[number]

const ADMIN_TAB_META: Record<AdminTab, { label: string, kicker: string }> = {
  overview: { label: 'Overview', kicker: 'Totals and distribution' },
  players: { label: 'Players', kicker: 'Search and enforcement' },
  ai: { label: 'AI Rivals', kicker: 'Seed, run, automate' },
  config: { label: 'Tuning', kicker: 'Runtime values' },
  liveops: { label: 'Live Ops', kicker: 'Maintenance and banners' },
  audit: { label: 'Audit', kicker: 'Who changed what' }
}

/**
 * One tab at a time rather than six stacked panels. The Admin Control Center used to sit at the bottom
 * holding whatever had no other home: headline totals, a read-only economy dump, and the AI controls.
 * Those are three different jobs, so they now live with the things they belong to.
 */
function AdminPage(ctx: PageContext & { overview: AdminOverview }) {
  const [tab, setTab] = useState<AdminTab>('overview')
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-md-1">
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
    {tab === 'ai' && <AdminAiTab ctx={ctx} />}
    {tab === 'config' && <><AdminConfigPanel busy={ctx.busy} /><AdminEconomyReadout overview={ctx.overview} /></>}
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
      <button
        className={ops?.maintenanceMode ? 'btn btn-primary btn-sm' : 'btn btn-secondary btn-sm'}
        disabled={locked}
        onClick={() => void apply({ maintenanceMode: !ops?.maintenanceMode })}
      >
        {ops?.maintenanceMode ? 'End maintenance' : 'Start maintenance'}
      </button>
      <label className="field">Maintenance notice<input className="form-control" value={maintenanceMessage} onChange={e => setMaintenanceMessage(e.target.value)} placeholder="Back in 10 minutes" /></label>
      <button className="btn btn-secondary btn-sm" disabled={locked}
        onClick={() => void apply({ maintenanceMessage })}>Save notice</button>
    </div>
    <div className="control-row">
      <label className="grow">Announcement banner<input className="form-control" value={announcement} onChange={e => setAnnouncement(e.target.value)} placeholder="Shown to every player" /></label>
      <button className="btn btn-secondary btn-sm" disabled={locked}
        onClick={() => void apply({ announcement })}>Save banner</button>
      <button className="btn btn-secondary btn-sm" disabled={locked || !ops?.announcement}
        onClick={() => void apply({ announcement: '' })}>Clear</button>
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
      <button className="btn btn-secondary btn-sm" disabled={locked} onClick={() => setShowAll(value => !value)}>
        {showAll ? 'Show overrides only' : `Show all ${config.settings.length}`}
      </button>
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
    <button className="btn btn-primary btn-sm" disabled={locked || !dirty} onClick={onSave}>Save</button>
    <button className="btn btn-secondary btn-sm" disabled={locked || !entry.isOverridden} onClick={onClear}>Reset</button>
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
            <button className="btn btn-secondary btn-sm" disabled={busy || working}
              onClick={() => void resolve(mission.missionId)}>Force resolve</button>
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
      <button className="btn btn-secondary btn-sm" disabled={locked}>Search</button>
    </form>

    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {message && <div className="alert alert-success"><span>{message}</span></div>}

    <div className="d-grid gtc-1 gtc-lg-split-280 gap-3 mt-3">
      <div className="admin-player-list d-grid gap-1 align-content-start overflow-y-auto">
        {results.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No players matched.</p>}
        {results.map(player => <button
          className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${target?.playerId === player.playerId ? 'active border-primary' : ''}`}
          key={player.playerId}
          type="button"
          disabled={locked}
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
        </button>)}
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
            {adjustPresets.map(preset => <button
              className="btn btn-secondary btn-sm"
              key={preset.label}
              disabled={locked}
              onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, preset.resource, preset.delta, reason))}
            >{preset.label}</button>)}
            <button className="btn btn-secondary btn-sm" disabled={locked}
              onClick={() => void run('Morale set', () => adminApi.setMorale(target.playerId, 100, reason))}>Morale 100%</button>
          </div>
        </div>

        <div className="control-block">
          <strong>Adjust a resource</strong>
          <div className="control-row">
            <label className="field">Resource<select className="form-select" value={resource} onChange={e => setResource(e.target.value)}>
              {detail.adjustableResources.map(key => <option key={key} value={key}>{key}</option>)}
            </select></label>
            <label className="field">Change<input className="form-control" type="number" value={delta} onChange={e => setDelta(Number(e.target.value))} /></label>
            <button className="btn btn-primary btn-sm" disabled={locked || delta === 0}
              onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, resource, delta, reason))}>
              Apply
            </button>
          </div>
          <small>Negative values take resources away. Nothing drops below zero.</small>
        </div>

        <div className="control-block">
          <strong>Account</strong>
          <div className="control-row">
            <button className="btn btn-secondary btn-sm" disabled={locked}
              onClick={() => void run('Banned', () => adminApi.enforcement(target.playerId, 'ban', null, reason))}>
              Ban
            </button>
            <label className="field">Suspend hours<input className="form-control" type="number" min={1} value={suspendHours} onChange={e => setSuspendHours(Number(e.target.value))} /></label>
            <button className="btn btn-secondary btn-sm" disabled={locked || suspendHours < 1}
              onClick={() => void run('Suspended', () => adminApi.enforcement(
                target.playerId,
                'suspend',
                new Date(Date.now() + suspendHours * 3600_000).toISOString(),
                reason))}>
              Suspend
            </button>
            <button className="btn btn-secondary btn-sm" disabled={locked}
              onClick={() => void run('Cleared', () => adminApi.enforcement(target.playerId, 'clear', null, reason))}>
              Lift
            </button>
            <button className="btn btn-secondary btn-sm" disabled={locked}
              onClick={() => void run('Logged out', () => adminApi.forceLogout(target.playerId, reason))}>
              Force logout
            </button>
          </div>
        </div>

        <div className="control-block">
          <strong>Identity and rights</strong>
          <div className="control-row">
            <label className="field">Name<input className="form-control" value={renameTo} onChange={e => setRenameTo(e.target.value)} minLength={3} maxLength={32} /></label>
            <button className="btn btn-secondary btn-sm" disabled={locked || renameTo.trim() === target.name}
              onClick={() => void run('Renamed', () => adminApi.rename(target.playerId, renameTo, reason))}>
              Rename
            </button>
            <button className="btn btn-secondary btn-sm" disabled={locked || target.isBot}
              onClick={() => void run('Rights changed', () => adminApi.setAdminRights(target.playerId, !target.isAdmin, reason))}>
              {target.isAdmin ? 'Revoke admin' : 'Grant admin'}
            </button>
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
  return <section className="status-strip tnum d-grid gap-2 mb-3" data-tour="status">
    <Stat label="Cash" value={money.format(dashboard.cash)} />
    <Stat label="Bank" value={money.format(dashboard.bankCash)} />
    <Stat label="Net Worth" value={money.format(dashboard.netWorth)} />
    <Stat label="Turns" value={`${dashboard.turns} / ${dashboard.maxTurns}`} sub={nextTurn === 'MAX' ? 'Turn bank full' : `+${dashboard.turnsPerTick} in ${nextTurn}`} />
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

/** Upkeep a street action of this length burns, scaled from the server's max-action figures. */
function upkeepFor(dashboard: Dashboard, turns: number) {
  const planned = Math.max(1, Math.min(turns, dashboard.maxActionTurns))
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
  const supplies = [
    { key: 'condoms', owned: dashboard.condoms, cap: hideout.maxCondoms, needed: upkeep.condoms, basis: `to work ${turnLabel}` },
    { key: 'beer', owned: dashboard.beer, cap: hideout.maxBeer, needed: upkeep.beer, basis: `to work ${turnLabel}` },
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

  return <div className="tnum d-grid gap-2 my-3 border rounded bg-body-secondary p-3">
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
        const short = Math.max(0, supply.needed - supply.owned)
        // The storage room refuses buys that do not fit, so never offer more than the room left.
        const room = Math.max(0, supply.cap - supply.owned)
        const qty = Math.min(storeQty[supply.key] ?? Math.max(1, short), Math.max(1, room))
        const total = qty * item.price
        return <div className={`d-grid gtc-1-auto gap-2 align-content-start border rounded p-3 ${short > 0 ? 'border-warning' : 'bg-body-tertiary'}`} key={supply.key}>
          <div className="d-grid gap-1">
            <strong className="text-body">{item.name}</strong>
            <span className={`small ${short > 0 ? 'text-primary' : 'text-body-secondary'}`}>{number.format(supply.owned)} on hand / {number.format(supply.needed)} {supply.basis} / {number.format(supply.cap)} storage</span>
          </div>
          <em className={`eyebrow fst-normal align-self-start justify-self-end text-nowrap ${short > 0 ? 'text-primary' : 'text-body-tertiary'}`}>
            {room === 0 ? 'Storage full' : short > 0 ? `${number.format(short)} short` : 'Covered'}
          </em>
          <label className="field gcol-full small">Qty<input className="form-control" aria-label={`${item.name} quantity`} type="number" min={1} max={Math.max(1, room)} value={qty} onChange={event => setStoreQty(value => ({ ...value, [supply.key]: Number(event.target.value) }))} /></label>
          <button
            className="btn btn-primary gcol-full w-100 min-w-0"
            disabled={busy || qty < 1 || room === 0 || qty > room || dashboard.cash < total}
            onClick={() => void act(() => api.buyStoreItem(supply.key, qty))}
          >
            {room === 0 ? 'Storage Full' : `Buy ${money.format(total)}`}
          </button>
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
function NextMovePanel({ dashboard, onPage }: { dashboard: Dashboard, onPage: (page: AppPage) => void }) {
  const moves = dashboard.guidance?.moves ?? []
  if (moves.length === 0) return null

  return <section className="card p-3">
    <div className="panel-title"><h2>Next Moves</h2><span>Worth doing now</span></div>
    <div className="d-grid gap-2">
      {moves.map(move => <button
        className={`w-100 d-grid gap-1 text-start border rounded p-3 ${move.urgent ? 'border-warning bg-body-tertiary' : 'bg-body-secondary'}`}
        type="button"
        key={move.label}
        onClick={() => onPage(flowPage(move.page))}
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
function OpeningLadderPanel({ dashboard, onPage, onTour }: {
  dashboard: Dashboard
  onPage: (page: AppPage) => void
  onTour: () => void
}) {
  const guidance = dashboard.guidance
  if (!guidance || guidance.objectivesDone >= guidance.objectivesTotal) return null
  // The next unfinished rung, plus what has been done, so progress is visible without listing it all.
  const next = guidance.objectives.find(o => !o.done)

  return <section className="card p-3" data-tour="ladder">
    <div className="panel-title">
      <h2>Getting Started</h2>
      <div className="d-flex align-items-center gap-2">
        <span>{guidance.objectivesDone} of {guidance.objectivesTotal}</span>
        {/* The walkthrough shows itself once. Anybody who skipped it, or who has come back after a
            month away, needs a door back in that is not clearing their browser storage. */}
        <button className="btn btn-secondary btn-sm" type="button" onClick={onTour}>Show me around</button>
      </div>
    </div>
    <div className="d-grid gap-1">
      {guidance.objectives.map(step => <button
        className={`ladder-row d-grid gap-2 align-items-start text-start border rounded-2 p-2 ${step.done ? 'done' : step === next ? 'next bg-body-secondary text-body' : 'border-0 bg-transparent'}`}
        type="button"
        key={step.label}
        onClick={() => onPage(flowPage(step.page))}
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
    <div className="panel-title"><h2>Your Pimps</h2><span>{crew.length}/{dashboard.hideout.maxPimps} on the payroll</span></div>
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
  const canRest = !busy
    && !moraleFull
    && dashboard.turns >= report.hqRestTurnCost
    && dashboard.cash >= report.hqRestCashCost
  const canParty = !busy
    && !moraleFull
    && dashboard.turns >= report.hqPartyTurnCost
    && dashboard.cash >= report.hqPartyCashCost
    && dashboard.beer >= report.hqPartyBeerCost
    && dashboard.weed >= report.hqPartyWeedCost

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Recovery</h2><span>{dashboard.hideout.tierName} morale</span></div>
    <div className="d-grid gtc-1 gtc-md-split-90 gap-3 align-items-stretch">
      <div className="d-grid align-content-center gap-2 border rounded-2 bg-body-secondary p-3">
        <strong className="text-primary">Current hideout</strong>
        <p className="m-0">Your crew comes back here after street work and fights. Low morale heals slowly over time, or you can spend turns and supplies to steady them faster.</p>
      </div>
      <div className="d-grid gtc-1 gtc-md-2 gap-2">
        <button className="btn btn-secondary btn-stacked" disabled={!canRest} onClick={() => void act(() => api.recoverMorale('rest'))}>
          Rest Crew
          <span>{report.hqRestTurnCost} turns / {money.format(report.hqRestCashCost)} / +{report.hqRestMoraleGain.toFixed(0)}%</span>
        </button>
        <button className="btn btn-primary btn-stacked" disabled={!canParty} onClick={() => void act(() => api.recoverMorale('party'))}>
          Throw Party
          <span>{report.hqPartyTurnCost} turns / {money.format(report.hqPartyCashCost)} / {report.hqPartyBeerCost} beer / {report.hqPartyWeedCost} weed</span>
        </button>
      </div>
    </div>
  </section>
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
  const raidReady = crew.availablePimps >= 1
    && attackCrew.thugs >= 1
    && attackCrew.weapons >= 0
    && attackCrew.weapons <= attackCrew.thugs
    && attackCrew.thugs <= crew.availableThugs
    && attackCrew.weapons <= crew.availableWeapons
    && crew.activeAttackMissions < crew.maxActiveAttackMissions
  const method = dashboard.attackMethods.find(x => x.key === attackMethod) ?? dashboard.attackMethods[0]
  // Worked out by the server against this exact pairing, so it is the same sentence the launch would
  // have thrown rather than a second opinion the page arrived at on its own.
  const strikeBlocker = method && profile ? profile.strikeBlockers?.[method.key] : undefined
  const isRaid = method?.key === 'raid'
  // A strike is gated by the method's own requirements, which the server has already worked out, plus
  // the turns it costs. A raid is gated by crew, which only it commits.
  const methodReady = !!method
    && !method.blockedReason
    && dashboard.turns >= method.turnCost
    && (!isRaid || raidReady)
    // Nothing to hand out means nobody to tempt, so the run is refused before it costs the turns.
    && (method.key !== 'poach' || (poachCoke > 0 && poachCoke <= dashboard.coke))
  return <div className="card p-3 gcol-full">
    <div className="panel-title" data-tour="targets"><h2>Combat Targets</h2><span>Scout + launch</span></div>
    <form className="d-grid gtc-1 gtc-md-1-auto gap-2 align-items-end mb-3" onSubmit={onSearch}>
      <label className="field">Search<input className="form-control" value={query} onChange={event => onQuery(event.target.value)} placeholder="Name or city" /></label>
      <button className="btn btn-secondary btn-sm" disabled={busy}>Search</button>
    </form>
    <div className="d-grid gtc-1 gtc-xl-split-80 gap-3 align-items-start">
      <div className="d-grid gap-2">
        {targets.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No targets found.</p>}
        {targets.map(target => <button
          className={`target-row w-100 d-grid gap-1 column-gap-2 align-items-center text-start border rounded p-2 ${profile?.playerId === target.playerId ? 'active border-info' : 'bg-body-secondary'}`}
          key={target.playerId}
          type="button"
          disabled={busy}
          onClick={() => onInspect(target.playerId)}
        >
          <span className="text-primary fw-bolder">#{target.rank}</span>
          <strong className="min-w-0 text-truncate">{target.name}</strong>
          <small className="text-body-secondary small">{target.city}{target.aiPersonality ? ` / ${target.aiPersonality}` : target.isBot ? ' / AI' : ''}</small>
          <em className={`eyebrow fst-normal ${target.combatStatus.mismatchReason ? 'text-warning-emphasis' : ''}`}>{target.titles.length > 0 ? target.titles.join(', ') : `${target.combatStatus.eligibility} / ${target.combatReadiness.riskBand}`}{target.rides > 0 ? ` / ${target.rides} parked` : ''}</em>
          <b className="text-body">{money.format(target.netWorth)}</b>
        </button>)}
      </div>
      {profile && <div className="border rounded bg-body-secondary p-3">
        <div className="d-flex justify-content-between align-items-baseline gap-3 mb-3">
          <div className="d-grid gap-1">
            <strong className="text-body fs-5">{profile.name}</strong>
            <span className="eyebrow">{profile.city}{profile.aiPersonality ? ` / ${profile.aiPersonality}` : profile.isBot ? ' / AI rival' : ''}</span>
            {profile.titles.length > 0 && <small className="d-block mt-1 text-primary small">{profile.titles.join(' / ')}</small>}
          </div>
          {/* The only place a conversation can start. Everywhere else in chat you are answering
              somebody; this is where you pick who to write to in the first place. */}
          <button
            className="btn btn-secondary btn-sm"
            type="button"
            onClick={() => void (async () => {
              try {
                const { id } = await api.openDirect(profile.playerId)
                window.dispatchEvent(new CustomEvent('street-empire:conversation', { detail: { conversationId: id } }))
              } catch { /* the profile shows its own errors elsewhere */ }
            })()}
          >Message</button>
          {/* Silences them. Says so plainly, because a player who thinks this also keeps them from
              raiding the house will find out the hard way and blame the button. */}
          <button
            className="btn btn-secondary btn-sm"
            type="button"
            title="Stops them writing to you and hides them from your rooms. It does not stop them attacking you."
            onClick={() => void (async () => {
              try {
                await api.block(profile.playerId)
                window.dispatchEvent(new CustomEvent('street-empire:blocked'))
              } catch { /* the profile shows its own errors elsewhere */ }
            })()}
          >Block</button>
          <b className="text-primary fs-5">#{profile.rank}</b>
        </div>
        <AttackMethodPicker
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
          <button
            className="btn btn-primary"
            type="button"
            disabled={busy
              || (isRaid && !!activeAgainstProfile)
              || !methodReady
              // Their half of the rule. The method menu is built from your own crew and garage and has
              // never seen who you are looking at, so a strike with nothing to take on the other end
              // sat under a live button and was only refused once you had pressed it.
              || !!strikeBlocker
              || !profile.combatStatus.canAttackNow
              || (!isRaid && profile.combatStatus.isStrikeProtected)}
            onClick={() => onAttack(profile.playerId)}
          >
            {isRaid ? 'Send the Raid' : method?.label ?? 'Attack'}
          </button>
          <span>{method?.blockedReason
            ?? strikeBlocker
            ?? (!isRaid && profile.combatStatus.isStrikeProtected
              ? `${profile.name} was just hit and is watching the street.`
              : method && !isRaid
                ? strikeStatusText(method, dashboard, profile.combatStatus)
                : attackStatusText(
                  profile.combatStatus,
                  activeAgainstProfile,
                  activeOutgoingMissions[0],
                  methodReady))}</span>
        </div>
        <div className="tnum d-grid gtc-1 gtc-md-3 gap-2">
          <AdminMetric label="Net worth" value={money.format(profile.netWorth)} />
          <AdminMetric label="Cash" value={money.format(profile.cash)} />
          <AdminMetric label="Bank" value={money.format(profile.bankCash)} />
          <AdminMetric label="Attack" value={number.format(profile.combatReadiness.attackPower)} />
          <AdminMetric label="Defence" value={number.format(profile.combatReadiness.defensePower)} />
          <AdminMetric label="Risk" value={profile.combatReadiness.riskBand} />
          <AdminMetric label="Combat" value={profile.combatStatus.eligibility} />
        </div>
        <div className="mt-3 border-top">
          <StatusRow label="Crew" value={`${profile.pimps} P / ${profile.hoes} H / ${profile.thugs} T`} />
          <StatusRow label="Weapons" value={`${profile.combatReadiness.armedThugs}/${profile.thugs} armed`} warn={profile.combatReadiness.uncoveredThugs > 0} />
          {/* Coverage says how many are armed; the rack says how hard that is going to hit back. */}
          <StatusRow label="Their guns" value={rackSummary(profile.weaponRack)} />
          <StatusRow
            label="Firepower"
            value={`${profile.combatReadiness.firepower} pistols`}
            warn={profile.combatReadiness.firepower > profile.combatReadiness.armedThugs * 1.5}
          />
          <StatusRow label="Weapon coverage" value={`${profile.combatReadiness.weaponCoveragePercent.toFixed(0)}%`} warn={profile.combatReadiness.weaponCoveragePercent < 75} />
          <StatusRow label="Protection" value={combatProtectionText(profile.combatStatus)} warn={profile.combatStatus.isProtected} />
          <StatusRow label="24h combat" value={`${profile.combatStatus.recentAttacksMade} attacks / ${profile.combatStatus.recentDefenses} defences`} />
          {profile.combatStatus.mismatchReason && <StatusRow label="Blocked" value={profile.combatStatus.mismatchReason} warn />}
          {/* What each strike is aimed at. A garage with cars in it and a house with no medicine are
              the reads that turn the menu into a decision rather than a list. */}
          <StatusRow label="Rides" value={profile.rides > 0 ? `${number.format(profile.rides)} parked` : 'None'} />
          <StatusRow label="Medicine" value={profile.medicine > 0 ? `${number.format(profile.medicine)} crate(s)` : 'None'} />
          <StatusRow
            label="Hoe morale"
            value={`${profile.hoeHappiness.toFixed(0)}%${profile.hoeHappiness >= 90 ? ' - paid too well to poach' : ''}`}
            warn={profile.hoeHappiness < 50}
          />
          <StatusRow label="Thug morale" value={`${profile.thugHappiness.toFixed(0)}%`} warn={profile.thugHappiness < 50} />
          <StatusRow label="Product" value={`${number.format(profile.weed)} weed / ${number.format(profile.coke)} coke`} />
        </div>
        <div className="mt-3 border-top pt-3">
          <strong className="d-block mb-1 text-primary">Public Activity</strong>
          {profile.publicActivity.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No public activity yet.</p>}
          <ActivityList entries={profile.publicActivity} />
        </div>
      </div>}
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
        title={method.blockedReason ?? method.description}
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
      const armed = profile.combatReadiness.armedThugs
      const heavy = profile.combatReadiness.firepower > armed
      const guns = heavy ? rackSummary(profile.weaponRack).toLowerCase() : 'sidearms'
      return `${profile.rides} parked behind ${armed} armed thug(s) carrying ${guns}. Room for ${Math.max(0, dashboard.hideout.maxRides - dashboard.rides)} more in your garage.`
    }
    case 'infest': {
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
      <button
        className="btn btn-primary"
        disabled={busy || !board.canPray || !enough || offered < board.quantity || offered > board.held}
        onClick={() => void act(async () => {
          const result = await api.pray(offered)
          await load()
          return result
        })}
      >
        Make the offering
      </button>
      <span className="text-body-tertiary small">
        {board.blockedReason
          ?? (generous
            ? `Twice what they asked. Generosity buys what meeting the ask does not.`
            : `${number.format(board.generousQuantity)} would count as generous.`)}
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
        <b className="text-body">{title.playerName}</b>
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
        <div className="tnum d-grid gtc-1 gtc-md-4 gap-2 mb-3">
          <AdminMetric label="Crew worth" value={money.format(yours.netWorth)} />
          <AdminMetric label="Treasury" value={money.format(board.treasury)} />
          <AdminMetric label="Dues" value={`${yours.duesPercent}%`} />
          <AdminMetric label="Pool" value={`${yours.offensiveThugs} off / ${yours.defensiveThugs} def`} />
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

        <AlliancePoolPanel board={board} crew={yours} busy={busy} onAct={run} />

        {board.yourRank === 'Boss' && <AllianceSettingsPanel crew={yours} board={board} maxDues={board.maxDuesPercent} busy={busy} onSave={run} />}

        <div className="control-row">
          <button className="btn btn-secondary" disabled={busy} onClick={() => run(() => api.leaveAlliance())}>
            {yours.youFounded && yours.members > 1 ? 'Leave (throw everybody out first)' : 'Leave the crew'}
          </button>
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
          <button
            className="btn btn-primary"
            disabled={busy || name.trim().length < 3}
            onClick={() => run(() => api.foundAlliance(name.trim(), motto.trim()))}
          >Found it</button>
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
            <small className="text-body-secondary">{crew.doorLabel} / {crew.members} of {crew.maxMembers} / {crew.duesPercent}% dues</small>
          </div>
          <b>{money.format(crew.netWorth)}</b>
          {/* One door, one thing an outsider can do about it. Offering a button the crew has said it
              does not want is how a player learns a rule by being refused. */}
          {!yours && crew.members >= crew.maxMembers && <em>Full</em>}
          {!yours && crew.members < crew.maxMembers && crew.door === 'Open' && <button
            className="btn btn-secondary btn-sm"
            disabled={busy}
            onClick={() => run(() => api.joinAlliance(crew.id))}
          >Join</button>}
          {!yours && crew.members < crew.maxMembers && crew.door === 'Application' && <button
            className="btn btn-secondary btn-sm"
            disabled={busy}
            onClick={() => run(() => api.applyToAlliance(crew.id))}
          >Ask</button>}
          {!yours && crew.members < crew.maxMembers && crew.door === 'InviteOnly' && <em title={crew.doorDetail}>Invite only</em>}
        </div>)}
      </div>
    </section>
  </div>
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
  // Promotable ranks stop below the top: handing the crew over is its own move because it is the one
  // that gives yours away.
  const promotable = board.ranks.filter(x => x !== 'Boss')

  return <div className={`alliance-member d-grid gap-2 align-items-center border rounded bg-body-tertiary p-2 ${member.isYou ? 'border-primary' : ''}`}>
    <div>
      <strong>{member.name}</strong>
      <small>{member.rankLabel}{member.isFounder ? ' / founded it' : ''} - {member.city} / {member.pimps}P {member.hoes}H {member.thugs}T{member.defenders > 0 ? ` / ${member.defenders} posted` : ''}</small>
    </div>
    <b>{money.format(member.netWorth)}</b>
    {!member.isYou && <div className="d-flex align-items-center gap-1">
      {isBoss && <select className="form-select"
        value={member.rank === 'Boss' ? '' : member.rank}
        disabled={busy || member.rank === 'Boss'}
        onChange={event => onAct(() => api.setAllianceRank(member.playerId, event.target.value))}
      >
        {member.rank === 'Boss' && <option value="">Boss</option>}
        {promotable.map(rank => <option key={rank} value={rank}>{rank}</option>)}
      </select>}
      {isBoss && <button
        className="btn btn-secondary btn-sm"
        disabled={busy}
        onClick={() => onAct(() => api.handOverAlliance(member.playerId))}
      >Hand over</button>}
      {canExpel && member.youOutrankThem && <button
        className="btn btn-secondary btn-sm"
        disabled={busy}
        onClick={() => onAct(() => api.expelMember(member.playerId))}
      >Throw out</button>}
    </div>}
  </div>
}

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
        <div>
          <strong>{ask.kind === 'Invitation' ? ask.playerName : ask.allianceName}</strong>
          <small>{ask.kind === 'Invitation' ? 'has not answered yet' : 'has not answered your application'}</small>
        </div>
        {ask.kind === 'Invitation'
          ? <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => onAct(() => api.withdrawAllianceRequest(ask.id))}>Take it back</button>
          : <em>Waiting on somebody who can open the door</em>}
        <span />
      </div>)}
    </>}
    {answerable.length > 0 && <strong className="d-block mb-1 text-primary small">Waiting on you</strong>}
    {answerable.map(ask => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={ask.id}>
      <div>
        <strong>{ask.kind === 'Invitation' ? ask.allianceName : ask.playerName}</strong>
        <small>{ask.kind === 'Invitation' ? 'asked you to run with them' : 'is asking for a place'}{ask.note ? ` - "${ask.note}"` : ''}</small>
      </div>
      <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => onAct(() => api.answerAllianceRequest(ask.id, true))}>Accept</button>
      <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => onAct(() => api.answerAllianceRequest(ask.id, false))}>Refuse</button>
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

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <StatusRow label="Pool" value={`${crew.offensiveThugs} offensive / ${crew.defensiveThugs} defensive`} />
    <StatusRow
      label="You may borrow"
      value={board.borrowLimit === 0 ? 'Nothing until you have thugs of your own' : `${board.borrowLimit} (${board.yourDefenders} standing here)`}
      warn={board.borrowLimit === 0}
    />

    {crew.youFounded && <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Buy<input className="form-control" type="number" min={1} value={buy} onChange={event => setBuy(Number(event.target.value))} /></label>
      <button
        className="btn btn-secondary btn-sm"
        disabled={busy || buy < 1 || board.treasury < board.offensiveThugCost * buy}
        onClick={() => onAct(() => api.buyAllianceThugs('offensive', buy))}
      >Offensive {money.format(board.offensiveThugCost * buy)}</button>
      <button
        className="btn btn-secondary btn-sm"
        disabled={busy || buy < 1 || board.treasury < board.defensiveThugCost * buy}
        onClick={() => onAct(() => api.buyAllianceThugs('defensive', buy))}
      >Defensive {money.format(board.defensiveThugCost * buy)}</button>
    </div>}

    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Defenders<input className="form-control" type="number" min={1} value={post} onChange={event => setPost(Number(event.target.value))} /></label>
      <button
        className="btn btn-secondary btn-sm"
        disabled={busy || post < 1 || post > room || crew.defensiveThugs < post}
        onClick={() => onAct(() => api.postDefenders(post))}
      >Post to your place</button>
      <button
        className="btn btn-secondary btn-sm"
        disabled={busy || post < 1 || board.yourDefenders < post}
        onClick={() => onAct(() => api.postDefenders(-post))}
      >Send back</button>
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
  const [dues, setDues] = useState(crew.duesPercent)
  const [door, setDoor] = useState<AllianceDoorKey>(crew.door)

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Who may do what</strong>
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
      <button
        className="btn btn-secondary btn-sm"
        disabled={busy || dues < 0 || dues > maxDues}
        onClick={() => onSave(() => api.updateAlliance({ duesPercent: dues, door }))}
      >Save</button>
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
  return <section className={`card p-3 ${wide ? 'gcol-full' : ''} ${className ?? ''}`}>
    <div className="panel-title"><h2>Bank</h2><span>Cash handling</span></div>
    <div className={wide ? 'd-grid gtc-1 gtc-lg-2 gap-3 align-items-center' : ''}>
      <p className={wide ? 'mb-0' : ''}>Banked cash still counts toward net worth. Combat can steal cash on hand, but bank cash stays protected.</p>
      <div className="control-row">
        <label className="field">Amount<input className="form-control" type="number" min={1} value={bankAmount} onChange={e => setBankAmount(Number(e.target.value))} /></label>
        <button className="btn btn-secondary" disabled={busy || bankAmount < 1 || bankAmount > dashboard.cash} onClick={() => void act(() => api.deposit(bankAmount))}>Deposit</button>
        <button className="btn btn-secondary" disabled={busy || bankAmount < 1 || bankAmount > dashboard.bankCash} onClick={() => void act(() => api.withdraw(bankAmount))}>Withdraw</button>
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
          <div><strong>{entry.methodLabel} / {entry.outcome}</strong><span>{new Date(entry.createdAtUtc).toLocaleString()}</span></div>
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

      <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => onRun(directive())}>Do it</button>
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
        <button
          className={auto.enabled ? 'btn btn-secondary btn-sm' : 'btn btn-primary btn-sm'}
          disabled={busy || overview.botAccounts < 1}
          onClick={() => setBotAutomation(!auto.enabled)}
        >
          {auto.enabled ? 'Turn off' : 'Turn on'}
        </button>
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
        <button
          className="btn btn-secondary btn-sm"
          disabled={busy || !timingChanged || !timingValid}
          onClick={() => setBotAutomation(auto.enabled, { tickSeconds, roundsPerTick })}
        >
          Save timing
        </button>
        <button
          className="btn btn-secondary btn-sm"
          disabled={busy || atDefaults}
          onClick={() => setBotAutomation(auto.enabled, { resetTiming: true })}
        >
          Reset to {auto.defaultTickSeconds}s / {auto.defaultRoundsPerTick}
        </button>
      </div>
      {!timingValid && <p className="text-body-tertiary small mt-3">
        Tick must be {auto.minTickSeconds}-{auto.maxTickSeconds}s and rounds {auto.minRoundsPerTick}-{auto.maxRoundsPerTick}.
      </p>}
      {overview.botAccounts < 1 && <p className="text-body-tertiary small mt-3">Seed some rivals below before turning the loop on.</p>}
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Seed and Run</h2><span>{number.format(overview.botAccounts)} rivals exist</span></div>
      <div className="control-row">
        <label className="field">Seed count<input className="form-control" type="number" min={1} max={15} value={seedCount} onChange={e => setSeedCount(Number(e.target.value))} /></label>
        <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => setSeedCount(5)}>5</button>
        <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => setSeedCount(10)}>10</button>
        <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => setSeedCount(15)}>15</button>
        <button className="btn btn-primary btn-sm" disabled={busy || seedCount < 1 || seedCount > 15} onClick={() => seedBots(seedCount)}>Seed rivals</button>
      </div>
      <div className="control-row">
        <label className="field">Rounds<input className="form-control" type="number" min={1} max={10} value={runRounds} onChange={e => setRunRounds(Number(e.target.value))} /></label>
        <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => setRunRounds(1)}>1</button>
        <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => setRunRounds(3)}>3</button>
        <button className="btn btn-secondary btn-sm" disabled={busy} onClick={() => setRunRounds(10)}>10</button>
        <button className="btn btn-primary btn-sm" disabled={busy || overview.botAccounts < 1 || runRounds < 1 || runRounds > 10} onClick={() => runBots(runRounds)}>Run now</button>
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
              <button
                className="btn btn-secondary btn-sm"
                disabled={working === bot.playerId}
                onClick={() => void rivalAction(bot.playerId, () => opsApi.setBotPaused(bot.playerId, !bot.isPaused))}
              >
                {bot.isPaused ? 'Resume' : 'Pause'}
              </button>
              <button
                className="btn btn-secondary btn-sm"
                disabled={working === bot.playerId || bot.isPaused}
                title={bot.isPaused ? 'Resume them first' : 'Act now, ignoring the cooldown'}
                onClick={() => void rivalAction(bot.playerId, () => opsApi.actNow(bot.playerId))}
              >
                Act now
              </button>
              <button
                className="btn btn-secondary btn-sm"
                disabled={working === bot.playerId}
                onClick={() => setDirecting(id => id === bot.playerId ? null : bot.playerId)}
              >
                {directing === bot.playerId ? 'Close' : 'Direct'}
              </button>
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
      : <Leaderboard leaders={rows.slice(0, limit)} currentPlayer={dashboard.name} />}
  </>
}

function Leaderboard({ leaders, currentPlayer }: { leaders: LeaderboardEntry[], currentPlayer: string }) {
  return <div className="leaderboard tnum d-grid overflow-y-auto">
    {leaders.map(l => <div
      className={`leader d-grid gap-2 p-2 border-top ${l.playerName === currentPlayer ? 'bg-success-subtle' : ''}`}
      key={l.rank}
    >
      <span className="text-body-secondary">#{l.rank}</span>
      <strong className="min-w-0 text-truncate">{l.playerName}</strong>
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
}

const NEWS_LABELS: Record<WorldNewsEntry['category'], string> = {
  combat: 'Fight',
  build: 'Built',
  arrival: 'Arrival',
  crew: 'Crew',
  money: 'Money'
}

function WorldNewsPanel({ news, currentPlayer }: { news: WorldNews, currentPlayer: string }) {
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
        className={`feed-item py-2 border-top ${entry.playerName === currentPlayer ? 'mine' : ''}`}
        key={entry.id}
      >
        <div className="d-flex flex-column flex-sm-row justify-content-between gap-1 gap-sm-2">
          <strong className={`small fw-bold ${NEWS_TONE[entry.category] ?? 'text-body-secondary'}`}>{NEWS_LABELS[entry.category] ?? entry.action}</strong>
          <span className="text-body-tertiary small text-sm-end">{new Date(entry.createdAtUtc).toLocaleString()}</span>
        </div>
        <p className="my-1">{entry.summary}</p>
        <small className="text-body-tertiary small">{entry.playerName} / {entry.city}{entry.turnsSpent > 0 ? ` / ${entry.turnsSpent} turn${entry.turnsSpent === 1 ? '' : 's'}` : ''}</small>
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

const ACCOUNT_TABS = ['profile', 'signin', 'security'] as const
type AccountTab = typeof ACCOUNT_TABS[number]

const ACCOUNT_TAB_META: Record<AccountTab, { label: string, kicker: string }> = {
  profile: { label: 'Profile', kicker: 'Who you are here' },
  signin: { label: 'Sign-in', kicker: 'Email, password, Discord' },
  security: { label: 'Security', kicker: 'Sessions and last doors' },
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
  const [tab, setTab] = useState<AccountTab>('profile')
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
    try {
      const updated = await fn()
      if (updated) { setAccount(updated); setEmail(updated.email ?? '') }
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

    <div className="d-grid gtc-1 gtc-xl-2 gap-3 align-items-start">
      {tab === 'profile' && <AccountProfilePanel account={account} dashboard={ctx.dashboard} onTab={setTab} />}
      {tab === 'signin' && <>
        <AccountEmailPanel {...panel} email={email} setEmail={setEmail} />
        <AccountPasswordPanel {...panel} />
        <AccountDiscordPanel {...panel} />
      </>}
      {tab === 'security' && <AccountSecurityPanel {...panel} onTab={setTab} />}
    </div>
  </div>
}

/** What the panels below all take. Bundled because every one of them takes all of it. */
type AccountPanel = {
  account: Account
  busy: boolean
  run: (fn: () => Promise<Account | void>, said: string, form?: HTMLFormElement) => Promise<void>
  /** For a refusal the page can make on its own, without troubling the server about it. */
  fail: (message: string) => void
}

function AccountProfilePanel({ account, dashboard, onTab }: { account: Account, dashboard: Dashboard, onTab: (tab: AccountTab) => void }) {
  // Two names, and they are not the same thing, which is worth saying plainly on the page where both
  // appear: one is how you sign in and nobody else sees it, the other is what the whole city calls you.
  const open = waysIn(account)
  return <>
    <section className="card p-3 gcol-xl-full">
      <div className="panel-title"><h2>{account.playerName}</h2><span>{dashboard.city} / Rank #{dashboard.rank}</span></div>
      <div className="tnum d-grid gtc-1 gtc-md-4 gap-2 mb-3">
        <AdminMetric label="Player name" value={account.playerName} />
        <AdminMetric label="Username" value={account.username} />
        <AdminMetric label="Ways in" value={`${open.length} of 2`} />
        <AdminMetric label="Since" value={new Date(account.createdAtUtc).toLocaleDateString()} />
      </div>
      <p className="mb-0">
        Your <strong className="text-primary">player name</strong> is what the city sees - the ladder, the
        news, the wanted list. Your <strong className="text-primary">username</strong> is only ever how you
        sign in, and nobody else is shown it. Neither of them changes.
      </p>
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
        <button className="btn btn-primary" disabled={busy}>{busy ? 'Working...' : 'Confirm'}</button>
      </form>}

      <button
        className="btn btn-secondary"
        type="button"
        disabled={busy || resendIn > 0}
        onClick={() => void run(() => api.sendEmailCode(), 'A new code is on its way.')}
      >
        {resendIn > 0
          ? `Send another in ${countdown(resendIn)}`
          : pending ? 'Send a new code' : 'Send a code'}
      </button>
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
      {removingLastWayBack && <div className="alert alert-warning mb-0">
        This address is the only way back into your account if you forget your password. Connect Discord
        on this page and you can remove it.
      </div>}
      <button className="btn btn-primary" disabled={busy || !emailChanged || removingLastWayBack}>
        {busy ? 'Working...' : email.trim() ? 'Save Email' : 'Remove Email'}
      </button>
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
      <button className="btn btn-primary" disabled={busy}>
        {busy ? 'Working...' : account.hasPassword ? 'Change Password' : 'Set Password'}
      </button>
    </form>
  </section>
}

function AccountDiscordPanel({ account, busy, run }: AccountPanel) {
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
        {discordIsTheOnlyWayIn
          ? <div className="alert alert-warning mb-0">
            This is the only way into your empire. Set a password before disconnecting it.
          </div>
          : discordIsTheOnlyWayBackIn
          ? <div className="alert alert-warning mb-0">
            This is the only way back into your empire if you forget your password. Confirm an email
            address before disconnecting it.
          </div>
          : <button
            className="btn btn-outline-danger"
            type="button"
            disabled={busy}
            onClick={() => void run(() => api.disconnectDiscord(), 'Discord disconnected.')}
          >Disconnect Discord</button>}
      </>
      : account.discordConfigured
        ? <>
          <p>
            Connect one and it becomes a way in: one button on the sign-in screen, no password typed.
            You keep your username and password either way.
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

function AccountSecurityPanel({ account, busy, run, onTab }: AccountPanel & { onTab: (tab: AccountTab) => void }) {
  const open = waysIn(account)
  const back = waysBackIn(account)
  const enoughOfBoth = open.length > 1 && back.length > 1
  return <>
    <section className="card p-3">
      <div className="panel-title"><h2>Sessions</h2><span>Signed in for 14 days</span></div>
      <p>
        A sign-in lasts a fortnight and renews itself while you play, which is convenient right up until
        you leave yourself signed in on a machine you no longer have. This ends every session but this
        one, everywhere, immediately.
      </p>
      <button
        className="btn btn-outline-danger"
        type="button"
        disabled={busy}
        onClick={() => void run(() => api.revokeSessions(), 'Every other session has been signed out.')}
      >{busy ? 'Working...' : 'Sign out everywhere else'}</button>
    </section>

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

/**
 * The people in town who want things.
 *
 * The game had one buyer before this - the city itself, fixed price, any amount, any hour - which is
 * a price list rather than a market. An order has a shape: an amount, a deadline, sometimes a
 * condition, which is what makes producing a decision rather than a routine.
 */
function ContractsPanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const [board, setBoard] = useState<ContractBoard | null>(null)
  const [error, setError] = useState('')

  const load = async () => {
    try { setBoard(await api.contracts()); setError('') }
    catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.city, dashboard.weed, dashboard.coke, dashboard.weapons, dashboard.moonshine])

  if (!board || board.contracts.length === 0) return null

  const fill = async (id: number, quantity?: number) => {
    await act(() => api.fillContract(id, quantity))
    await load()
  }

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Wanted in {board.city}</h2><span>Buyers with a deadline</span></div>
    <p>
      These pay over the counter price, but they want a set amount by a set time, and some of them care
      what it is cut with. Selling flat is always there; this is what makes it worth choosing what to make.
    </p>
    <div className="d-grid gap-2 mt-3">
      {board.contracts.map(c => {
        const hours = Math.floor(c.minutesRemaining / 60)
        const left = hours >= 1 ? `${hours}h left` : `${c.minutesRemaining}m left`
        const started = c.delivered > 0
        const finishes = c.canDeliverNow >= c.remaining && c.canDeliverNow > 0
        return <div className={`room-row ${c.blockedReason ? '' : 'border-start-thick border-start-success'}`} key={c.id}>
          <div className="room-copy">
            <strong>{c.buyer}{c.yours && <span className="badge text-bg-primary">Yours</span>}</strong>
            <span>
              Wants {number.format(c.quantity)} {c.good}
              {c.minimumPurityPercent ? `, at least ${c.minimumPurityPercent}% pure` : ''}
              {' '}at {money.format(c.pricePerUnit)} each, against {money.format(c.listPricePerUnit)} over the counter.
            </span>
            <small>
              {/* What a delivery pays now, and what is still waiting on the last of it - the premium
                  never splits, so it is worth naming separately from the running rate. */}
              {started
                ? `${number.format(c.delivered)} in, ${number.format(c.remaining)} to go - ${money.format(c.completionBonus)} lands when it is finished`
                : `${money.format(c.payout)} the lot, ${money.format(c.completionBonus)} more than selling it flat`}
              {' - '}{left}
              {c.blockedReason ? ` - ${c.blockedReason}` : ''}
            </small>
            {started && <div
              className="progress contract-progress mt-1"
              role="progressbar"
              aria-label="Order filled"
              aria-valuenow={Math.round((c.delivered / c.quantity) * 100)}
              aria-valuemin={0}
              aria-valuemax={100}
            >
              <div className="progress-bar" style={{ width: `${Math.round((c.delivered / c.quantity) * 100)}%` }} />
            </div>}
          </div>
          <em>{number.format(c.held)} held</em>
          <div className="d-flex flex-wrap align-items-end gap-1 mt-1">
            <button
              className="btn btn-primary btn-sm"
              disabled={busy || c.blockedReason !== null || c.canDeliverNow <= 0}
              onClick={() => void fill(c.id)}
            >
              {/* One button that says what it will actually do, rather than an amount box the player
                  has to work out for themselves. Handing over everything that fits is the move in
                  almost every case, because the room is the constraint the order is fighting. */}
              {finishes ? 'Finish it' : `Run ${number.format(c.canDeliverNow)}`}
            </button>
          </div>
        </div>
      })}
    </div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
  </section>
}

function InventoryCard({ name, count, note }: { name: string, count: number, note: string }) {
  return <div className="inventory-card d-grid gap-1 align-content-center border rounded bg-body-secondary p-3">
    <span className="eyebrow">{name}</span>
    <strong className="fs-3 text-primary lh-1">{number.format(count)}</strong>
    <small className="text-body-tertiary">{note}</small>
  </div>
}

function CrewManageRow({ label, owned, quantity, hireCost, cash, busy, canHire = true, canFire, onQuantity, onHire, onFire, note, trims = [], firePenalty = 0, maxFirePenalty = 0 }: {
  label: string
  owned: number
  quantity: number
  hireCost: number
  cash: number
  busy: boolean
  canHire?: boolean
  canFire: boolean
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
        {worthTrimming.map(trim => <button
          type="button"
          key={trim.label}
          className="btn btn-link"
          disabled={busy}
          onClick={() => onQuantity(trim.cut)}
        >
          let {number.format(trim.cut)} go to {trim.label}
        </button>)}
      </span>}
      {firePenalty > 0 && quantity > 0 && <span className="d-flex flex-wrap gap-1 column-gap-3 mt-1 small text-body-tertiary">
        Firing {number.format(quantity)} costs {moraleCost.toFixed(0)}% morale{moraleCost >= (maxFirePenalty || Infinity) ? ', the most a single cut can' : ''}.
      </span>}
    </div>
    <input className="form-control" aria-label={`${label} quantity`} type="number" min={1} max={1000} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <button className="btn btn-primary btn-sm" disabled={busy || quantity < 1 || !canHire || cash < totalCost} onClick={onHire}>Hire</button>
    <button className="btn btn-secondary btn-sm" disabled={busy || quantity < 1 || !canFire} onClick={onFire}>Fire</button>
  </div>
}

function SellRow({ name, owned, price, quantity, onQuantity, onSell, disabled }: {
  name: string
  owned: number
  price: number
  quantity: number
  onQuantity: (quantity: number) => void
  onSell: () => void
  disabled: boolean
}) {
  return <div className="sell-row d-grid gap-2 align-items-center border-top pt-2">
    <div className="d-grid gap-1">
      <strong>{name}</strong>
      <span className="text-body-secondary">{number.format(owned)} owned | {money.format(price)} each</span>
    </div>
    <input className="form-control" type="number" min={1} max={Math.max(1, owned)} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <button className="btn btn-secondary btn-sm" disabled={disabled || quantity < 1 || quantity > owned} onClick={onSell}>Sell</button>
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
