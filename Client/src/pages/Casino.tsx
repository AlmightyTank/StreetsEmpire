import React, { useEffect, useMemo, useRef, useState } from 'react'
import { api } from '../api'
import type { BlackjackAction, BlackjackBoard, BlackjackRound, CasinoBoard, CasinoMachine,
  CasinoTransaction, ClaimedComp, CompReward, RouletteBoard, RouletteSpin, RouletteStake, SlotSpin,
  SlotWin } from '../api'
import { money, number, signedMoney, wait } from '../format'
import { AdminMetric, BUSY, Button, firstReason, type Blocked } from '../ui'
import type { PageContext } from '../pagecontext'

/*
  The floor: slots, roulette and the blackjack pit, and the reels they are drawn on.

  Its own module because it is the largest thing in the game that most people never open, and because
  it is genuinely separate - the only things it borrows are the ones every page borrows. Loaded when
  somebody walks in rather than shipped to everybody who loads the sign-in page.
*/

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
 * How much longer a reel hangs when the ones already down have left a run alive.
 *
 * This is the whole reason a slot machine stops its reels in order. Three of a kind already showing
 * with two reels to go means the next one decides whether this is a small win or a large one, and a
 * machine that dropped it on the same beat as every other reel would be throwing that away.
 */
const slotAnticipateMs = 900

/**
 * What the next reel has to be worth before the machine holds for it, counted in whole tickets.
 *
 * Run length alone is the wrong test. Three of a kind is live on 36% of nine-lane spins - a machine
 * pausing on a third of them is not pausing - and most of those are three of the commonest face,
 * where a fourth adds very little. What matters is whether the reel still turning could pay
 * something: this holds when landing one more would return at least four times the whole stake,
 * which is true of almost any four-of-a-kind going to five and only of the better faces going to four.
 */
const slotAnticipateTickets = 4

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
    ? { hold: 120, gap: 60, ramp: 0, anticipate: 0 }
    : { hold: slotSpinDurationMs, gap: slotReelStopMs, ramp: slotReelStopRampMs, anticipate: slotAnticipateMs }
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

/** The grid written out, for a reader that cannot see the picture of it. */
function slotGridText(symbols: string[], columns: number) {
  return Array.from({ length: Math.ceil(symbols.length / columns) }, (_, row) =>
    symbols.slice(row * columns, row * columns + columns).join(' / ')).join(' | ')
}

/**
 * A pull as it landed, small enough to sit in a table cell.
 *
 * This column used to be the fifteen symbol names written out - "Cash Stack / Gold Chain / Pistol /
 * Low-Rider / Crew Crown | ..." - which was two hundred characters of a row that has seven other
 * columns, and unreadable at any width. Fifteen faces are a picture, so it is drawn as one, with the
 * cells that actually paid lit the way they are on the machine.
 *
 * Columns come off the row rather than off the floor's current shape, because a row written before
 * the floor widened holds nine symbols and is still in the ledger.
 */
function SlotMiniGrid({ symbols, wins }: { symbols: string[], wins: SlotWin[] }) {
  if (symbols.length === 0) return <span className="text-body-tertiary">-</span>

  const columns = symbols.length % slotColumns === 0 ? slotColumns : 3
  const lit = new Set(wins.flatMap(win => win.cells))
  return <span
    className="slot-mini"
    style={{ gridTemplateColumns: `repeat(${columns}, auto)` }}
    role="img"
    aria-label={slotGridText(symbols, columns)}
  >
    {symbols.map((symbol, cell) =>
      <span className={lit.has(cell) ? 'is-lit' : undefined} key={cell} title={symbol}>
        <SlotGlyph symbol={symbol} />
      </span>)}
  </span>
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
/**
 * Counts a payout up from nothing instead of printing it.
 *
 * A machine that states the number has finished with you; one that counts it out is still paying. The
 * count is only worth anything if it takes longer for more money, so the length scales with how many
 * times the stake came back - a double is over almost at once, a thirty-to-one hangs about - on a
 * square root, so a thousand-to-one is not a thousand times slower.
 *
 * Eased out rather than linear, so it slows into the figure instead of stopping dead on it.
 */
function useCountUp(target: number, stake: number, token: number) {
  const [value, setValue] = useState(target)

  useEffect(() => {
    // Somebody who has asked the game to stop moving gets the number, not the performance.
    const reduced = document.documentElement.getAttribute('data-motion') === 'reduced'
    if (reduced || target <= 0) {
      setValue(target)
      return
    }

    const ratio = stake > 0 ? target / stake : 1
    const duration = Math.min(2600, 420 + Math.sqrt(Math.max(1, ratio)) * 340)
    let frame = 0
    const started = window.performance.now()
    const step = (now: number) => {
      const through = Math.min(1, (now - started) / duration)
      setValue(Math.round(target * (1 - Math.pow(1 - through, 3))))
      if (through < 1) frame = window.requestAnimationFrame(step)
    }

    setValue(0)
    frame = window.requestAnimationFrame(step)
    return () => window.cancelAnimationFrame(frame)
    // The token is the spin's own id: two spins that pay the same amount are still two spins, and the
    // second one has to count out as well as the first.
  }, [target, stake, token])

  return value
}

function slotPaylinePoints(cells: number[]) {
  return cells.map(cell => `${cell % slotColumns},${Math.floor(cell / slotColumns)}`).join(' ')
}


export function CasinoPage(ctx: PageContext) {
  const { dashboard, busy, refresh, refreshDashboard, act } = ctx
  const [board, setBoard] = useState<CasinoBoard | null>(null)
  const [activeKey, setActiveKey] = useState('')
  const [bet, setBet] = useState(10)
  const [paylines, setPaylines] = useState(1)
  const [lastSpin, setLastSpin] = useState<SlotSpin | null>(null)
  // How many columns have come to rest, left to right. Reels that all stop together read as a
  // picture appearing rather than as a machine landing, and the column that has not stopped yet is
  // the only reason to keep watching - so this is a count rather than a flag.
  const [stoppedColumns, setStoppedColumns] = useState(slotColumns)
  // Set while the reels still turning are the ones a live run is waiting on.
  const [anticipating, setAnticipating] = useState(false)
  const spinning = stoppedColumns < slotColumns
  const [compNote, setCompNote] = useState('')
  const [game, setGame] = useState<CasinoGame>('slots')
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

  // Held back until the reels are down: the receipt is the verdict, and the count is the receipt
  // being read out. While the machine is still turning there is nothing to read.
  const settledSpin = spinning ? null : lastSpin
  const countedPayout = useCountUp(
    settledSpin?.transaction.payoutAmount ?? 0,
    settledSpin?.transaction.betAmount ?? 0,
    settledSpin?.transaction.id ?? 0)
  // The net is derived from the counted payout rather than animated separately, so the two figures
  // cannot disagree on the way past and both land together.
  const countedNet = settledSpin
    ? countedPayout - (settledSpin.transaction.isFreeSpin ? 0 : settledSpin.transaction.betAmount)
    : 0

  const runSpin = async () => {
    if (!active || spinning) return
    const started = window.performance.now()
    let spin: SlotSpin | null = null
    setStoppedColumns(0)
    // Nothing is re-read here. The reels are still turning and the balance is the answer.
    await act(async () => {
      spin = await api.spinSlots(active.key, clampedBet, lineCount)
      return spin
    }, 'none')
    // Asserted because the assignment happens inside the callback handed to act, which TypeScript's
    // flow analysis does not follow - without this it still reads the variable as its initialiser.
    const settled = spin as SlotSpin | null
    if (!settled) {
      // The cage refused it. Put the reels back rather than leaving them turning on a spin that
      // never happened.
      setStoppedColumns(slotColumns)
      setAnticipating(false)
      return
    }

    // The result is known now, and the board takes it immediately: a column that has stopped has to
    // be showing what it actually landed on. What is still held back is everything that reads as the
    // verdict - the winning lines, the lit cells, the receipt - all of which wait on the last reel.
    setLastSpin(settled)
    // The whole floor comes back with the spin. It has to: the pot on every machine moved, and the one
    // that was just taken has gone back to its seed with somebody's name against it.
    setBoard(settled.board)

    // Whether the reels already down have left a run that the next one could still extend. Read off
    // the grid that has landed rather than off the result, because that is all the player can see.
    const grid = slotGridSymbols(activeFaces, settled.transaction.symbols)
    // Off the board that came back with the spin rather than the one in state: this runs before the
    // render that would have narrowed the other one, and they say the same thing anyway.
    const lanes = settled.board.paylines.slice(0, settled.transaction.paylines)
    // What each face pays for a run of a given length, off the machine's own card.
    const pays = new Map(active.paytable.map(pay => [pay.label, [0, 0, pay.pair, pay.triple, pay.quad, pay.quint]]))
    const ticket = settled.transaction.betAmount
    const perLane = ticket / Math.max(1, settled.transaction.paylines)

    // Is any lane still live, and is the reel it is waiting on worth holding for? A run that broke
    // short of the reels already down is decided and there is nothing left to wait on.
    const worthHolding = (columns: number) => lanes.some(lane => {
      let run = 1
      while (run < columns && grid[lane.cells[run]] === grid[lane.cells[0]]) run++
      if (run !== columns || columns >= slotColumns) return false
      const next = pays.get(grid[lane.cells[0]])?.[columns + 1] ?? 0
      return next * perLane >= ticket * slotAnticipateTickets
    })

    const timing = slotReelTiming()
    const spun = window.performance.now() - started
    if (spun < timing.hold) await wait(timing.hold - spun)
    for (let column = 1; column <= slotColumns; column++) {
      setStoppedColumns(column)
      if (column >= slotColumns) break

      const held = timing.anticipate > 0 && worthHolding(column)
      setAnticipating(held)
      await wait(timing.gap + (column - 1) * timing.ramp + (held ? timing.anticipate : 0))
    }
    setAnticipating(false)
    // A jackpot is the one spin that changes something outside this room - it goes out on the wire -
    // so that alone is worth the whole screen. Every other spin only moved the money.
    await (settled.transaction.jackpot ? refresh() : refreshDashboard())
  }

  const claimComp = async (rewardKey: string) => {
    let claimed: ClaimedComp | null = null
    await act(async () => {
      const result = await api.claimComp(rewardKey)
      claimed = result
      return result
    }, 'dashboard')
    // Same reason the spin path asserts: the assignment happens inside the callback handed to act,
    // which TypeScript's flow analysis does not follow.
    const settled = claimed as ClaimedComp | null
    if (!settled) return
    setCompNote(settled.summary)
    setBoard(settled.board)
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
  // A lane that pays on two of a kind won across two cells, not across five. Lighting the whole lane
  // for it - which is what the board used to do - says a five of a kind landed.
  const wins: SlotWin[] = !spinning && lastSpin ? lastSpin.transaction.wins : []
  const winningCells = new Set(wins.flatMap(win => win.cells))
  const verdict = lastSpin ? spinVerdict(lastSpin.transaction) : null
  // Richest first off the paytable, so an idle reel shows the room's own faces.
  const activeFaces = active.paytable.map(pay => pay.label)

  if (game !== 'slots') return <div className="d-grid gap-3">
    <CasinoGames game={game} onPick={setGame} />
    {game === 'roulette' ? <RoulettePanel {...ctx} /> : <BlackjackPanel {...ctx} />}
  </div>

  return <div className="d-grid gtc-1 gtc-xl-split-135 gap-3 align-items-start">
    <div className="gcol-full"><CasinoGames game={game} onPick={setGame} /></div>
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
            className={`slot-reel d-grid border rounded bg-body-tertiary ${turning ? 'is-spinning' : ''} ${turning && anticipating ? 'is-anticipating' : ''} ${winningCells.has(index) ? 'is-winning' : ''}`}
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
        {wins.length > 0 && <svg className="slot-payline-overlay" viewBox={`0 0 ${slotColumns - 1} ${slotRows - 1}`} preserveAspectRatio="none" aria-hidden="true">
          {wins.map(win => <polyline className="slot-payline-hit" points={slotPaylinePoints(win.cells)} key={win.paylineIndex} />)}
        </svg>}
      </div>
      {wins.length > 0 && <ul className="list-unstyled d-grid gap-1 mb-3">
        {wins.map(win => <li className="d-flex justify-content-between gap-3 align-items-baseline small" key={win.paylineIndex}>
          <span>
            <SlotGlyph symbol={win.symbol} className="slot-pay-glyph" />
            <strong>{win.run}</strong> {win.symbol}
            <span className="text-body-tertiary"> on {win.paylineName.toLowerCase()}</span>
          </span>
          <span className="tnum text-success">{money.format(win.payout)}</span>
        </li>)}
      </ul>}
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
        {/*
          Nine buttons from the small breakpoint up, and a stepper below it. Nine lanes in a row on a
          phone came out 35px wide - under a fingertip - and laying them out three by three worked but
          spent a third of the machine on a number pad. The stepper is one line, and the lane names
          underneath already say what the choice bought, so nothing is lost by not showing all nine.
        */}
        <div className="btn-group w-100 d-none d-sm-flex" role="group" aria-label="Paylines">
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
        <div className="lane-stepper d-sm-none" role="group" aria-label="Paylines">
          <button
            className="btn btn-secondary"
            type="button"
            disabled={busy || spinning || lineCount <= 1}
            onClick={() => setPaylines(Math.max(1, lineCount - 1))}
            aria-label="One lane fewer"
          >
            <i className="bi bi-chevron-left" aria-hidden="true" />
          </button>
          {/* Announced rather than silent, because the number is the only thing either button changes. */}
          <strong aria-live="polite">{lineCount} of {lineLimit} lane{lineLimit === 1 ? '' : 's'}</strong>
          <button
            className="btn btn-secondary"
            type="button"
            disabled={busy || spinning || lineCount >= lineLimit}
            onClick={() => setPaylines(Math.min(lineLimit, lineCount + 1))}
            aria-label="One lane more"
          >
            <i className="bi bi-chevron-right" aria-hidden="true" />
          </button>
        </div>
        <small className="text-body-tertiary">
          {board.paylines.slice(0, lineCount).map(line => line.name).join(', ')}
        </small>
      </div>
      <div className="control-row bet-row">
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
          <span className={`tnum ${verdict.tone}`}>{signedMoney(countedNet)}</span>
        </div>
        <small className="text-body-tertiary">
          {lastSpin.wasFreeSpin ? 'On the house' : `Bet ${money.format(lastSpin.transaction.betAmount)}`}. Won {money.format(countedPayout)}.
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
      <div className="panel-title"><h2>Casino Stats</h2><span>{number.format(board.stats.plays)} plays</span></div>
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
      <div className="panel-title"><h2>Slots Ledger</h2><span>Recent pulls</span></div>
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
                  <td><SlotMiniGrid symbols={entry.symbols} wins={entry.wins} /></td>
                  <td>{entry.wins.length}/{entry.paylines}</td>
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

/**
 * The order the numbers actually sit in around a wheel, which is not the order they sit in on the
 * cloth and is not derivable from anything.
 *
 * Both sequences are the real ones. They are built so that colours alternate, high and low alternate,
 * and each half of the wheel carries a balanced spread - a wheel numbered one to thirty-six in order
 * would let a player cover half the outcomes with one arc of it.
 *
 * Cosmetic, strictly: the server draws a pocket uniformly and does not care where it sits. But a wheel
 * that showed the numbers in the wrong places would be a picture of a different game.
 */
const wheelOrders: Record<number, string[]> = {
  1: ['0', '32', '15', '19', '4', '21', '2', '25', '17', '34', '6', '27', '13', '36', '11', '30', '8', '23',
      '10', '5', '24', '16', '33', '1', '20', '14', '31', '9', '22', '18', '29', '7', '28', '12', '35', '3', '26'],
  2: ['0', '28', '9', '26', '30', '11', '7', '20', '32', '17', '5', '22', '34', '15', '3', '24', '36', '13', '1',
      '00', '27', '10', '25', '29', '12', '8', '19', '31', '18', '6', '21', '33', '16', '4', '23', '35', '14', '2'],
}

/** How long the ball is in the air. Longer than a reel drop, because a wheel is slower than a reel. */
const rouletteSpinMs = 4200

/** A point on the rim, measured clockwise from twelve where the pointer sits. */
function wheelPoint(degrees: number, radius: number) {
  const radians = degrees * Math.PI / 180
  return [100 + radius * Math.sin(radians), 100 - radius * Math.cos(radians)] as const
}

function wheelSector(from: number, to: number, outer: number, inner: number) {
  const [x0, y0] = wheelPoint(from, outer)
  const [x1, y1] = wheelPoint(to, outer)
  const [x2, y2] = wheelPoint(to, inner)
  const [x3, y3] = wheelPoint(from, inner)
  return `M${x0} ${y0} A${outer} ${outer} 0 0 1 ${x1} ${y1} L${x2} ${y2} A${inner} ${inner} 0 0 0 ${x3} ${y3} Z`
}

/**
 * The wheel itself.
 *
 * The disc turns and the pointer does not, so landing a pocket means rotating until that pocket's
 * middle is under twelve o'clock. Everything else - the whole turns, the easing - is just how long it
 * takes to get there.
 */
function RouletteWheel({ order, red, rotation, turning }: {
  order: string[]
  red: Set<number>
  rotation: number
  turning: boolean
}) {
  const step = 360 / order.length
  return <div className="roulette-wheel">
    <svg viewBox="0 0 200 200" role="img" aria-label={turning ? 'The wheel is turning' : 'The wheel is at rest'}>
      <circle cx="100" cy="100" r="97" className="roulette-wheel-rim" />
      <g
        style={{
          transform: `rotate(${rotation}deg)`,
          transformOrigin: '100px 100px',
          transition: turning ? `transform ${rouletteSpinMs}ms cubic-bezier(.16,.7,.18,1)` : 'none',
        }}
      >
        {order.map((pocket, index) => {
          const from = index * step
          const colour = pocket === '0' || pocket === '00' ? 'green' : red.has(Number(pocket)) ? 'red' : 'black'
          const [tx, ty] = wheelPoint(from + step / 2, 74)
          return <g key={pocket}>
            <path d={wheelSector(from, from + step, 92, 56)} className={`roulette-arc is-${colour}`} />
            <text
              x={tx}
              y={ty}
              className="roulette-arc-label"
              transform={`rotate(${from + step / 2} ${tx} ${ty})`}
            >{pocket}</text>
          </g>
        })}
      </g>
      <circle cx="100" cy="100" r="54" className="roulette-wheel-hub" />
      {/* The pointer, and the ball resting under it. Neither moves: the wheel comes to them. */}
      <path d="M100 4 L94 20 L106 20 Z" className="roulette-pointer" />
      <circle cx="100" cy="30" r="5" className="roulette-ball" />
    </svg>
  </div>
}

type CasinoGame = 'slots' | 'roulette' | 'blackjack'

const casinoGameNames: Record<CasinoGame, string> = {
  slots: 'Slots',
  roulette: 'Roulette',
  blackjack: 'Blackjack',
}

function CasinoGames({ game, onPick }: { game: CasinoGame, onPick: (game: CasinoGame) => void }) {
  return <div className="btn-group" role="group" aria-label="Casino games">
    {(['slots', 'roulette', 'blackjack'] as const).map(key =>
      <button
        className={`btn ${game === key ? 'btn-primary' : 'btn-secondary'}`}
        type="button"
        onClick={() => onPick(key)}
        key={key}
      >
        {casinoGameNames[key]}
      </button>)}
  </div>
}

const cardPips: Record<string, string> = { S: '\u2660', H: '\u2665', D: '\u2666', C: '\u2663' }

/**
 * A card off the shoe, drawn as a card.
 *
 * It deals itself in rather than appearing: the position in the hand sets a delay, so a hand arrives
 * left to right the way the reels come down and the wheel comes round. The dealer's second card turns
 * over instead, because that is the one card in the game whose arrival is the answer.
 */
function PlayingCard({ card, index = 0, flip = false, small = false }: {
  card: string
  index?: number
  flip?: boolean
  small?: boolean
}) {
  const suit = card[1]
  // A ten is written as one character in the shoe and two on a card.
  const rank = card[0] === 'T' ? '10' : card[0]
  const red = suit === 'H' || suit === 'D'
  return <span
    className={`playing-card ${red ? 'is-red' : 'is-black'} ${flip ? 'is-turning' : 'is-dealt'} ${small ? 'is-small' : ''}`}
    // Capped, so the fourth card of a hand does not sit waiting three quarters of a second.
    style={{ animationDelay: `${Math.min(index, 3) * 70}ms` }}
    aria-label={`${rank} of ${suit === 'S' ? 'spades' : suit === 'H' ? 'hearts' : suit === 'D' ? 'diamonds' : 'clubs'}`}
  >
    <span className="playing-card-rank">{rank}</span>
    <span className="playing-card-pip" aria-hidden="true">{cardPips[suit]}</span>
  </span>
}

/**
 * The pit.
 *
 * The only screen in the casino where the game waits on the player rather than the other way round, so
 * the whole of it is about which buttons are live: what the table will let you do with the hand in
 * front of you is the game.
 */
function BlackjackPanel(ctx: PageContext) {
  const { dashboard, busy, refreshDashboard, act } = ctx
  const [board, setBoard] = useState<BlackjackBoard | null>(null)
  const [tableKey, setTableKey] = useState('')
  const [bet, setBet] = useState(0)
  // The board only ever carries a round that is still live, so the moment one settles it stops being
  // there - and the hand would vanish at exactly the point the player wants to look at it. The last
  // round an action returned is kept here and shown until the next deal replaces it.
  const [finished, setFinished] = useState<BlackjackRound | null>(null)
  const [loadError, setLoadError] = useState('')

  useEffect(() => {
    let live = true
    void api.blackjack()
      .then(next => {
        if (!live) return
        setBoard(next)
        const open = next.tables.find(t => !t.locked) ?? next.tables[0]
        if (open) { setTableKey(open.key); setBet(open.minBet) }
        setLoadError('')
      })
      .catch(error => { if (live) setLoadError((error as Error).message) })
    return () => { live = false }
  }, [dashboard.playerId])

  const table = board?.tables.find(t => t.key === tableKey) ?? board?.tables.find(t => !t.locked) ?? board?.tables[0]
  const round = board?.round ?? finished

  // Counted out once the dealer is done, the same way a slot win is. Nothing to read while the round
  // is still live, so the target is nothing.
  const settledRound = round && !round.inPlay ? round : null
  const countedPayout = useCountUp(settledRound?.payout ?? 0, settledRound?.bet ?? 0, settledRound?.id ?? 0)
  const countedNet = settledRound ? countedPayout - settledRound.bet : 0

  const run = async (call: () => Promise<BlackjackAction>) => {
    let next: BlackjackAction | null = null
    // The move comes back with the board, the cash and the turns on it, so the only thing left to
    // re-read is the header. This is the fastest room in the game to click through.
    await act(async () => {
      const result = await call()
      next = result
      return result
    }, 'dashboard')
    const done = next as BlackjackAction | null
    if (done) {
      setBoard(done.board)
      // A live round comes back on the board; a settled one only ever comes back here.
      setFinished(done.round.inPlay ? null : done.round)
    }
  }

  if (loadError) return <section className="card p-3"><div className="panel-title"><h2>Blackjack</h2><span>Closed</span></div><p>{loadError}</p></section>
  if (!board || !table) return <section className="card p-3"><div className="panel-title"><h2>Blackjack</h2><span>Loading</span></div><p>The dealer is breaking a new shoe.</p></section>
  if (!board.enabled) return <section className="card p-3"><div className="panel-title"><h2>Blackjack</h2><span>Shut</span></div><p>The blackjack pit is shut.</p></section>

  const clamped = Math.min(Math.max(bet, table.minBet), table.maxBet)
  const dealBlocked = firstReason(
    busy && BUSY,
    round?.inPlay && 'Finish the hand in front of you.',
    table.locked && (table.lockedReason ?? 'That table is closed to you.'),
    dashboard.turns < board.handTurnCost && `A hand is ${board.handTurnCost} turn${board.handTurnCost === 1 ? '' : 's'} and you have ${dashboard.turns}.`,
    dashboard.cash < clamped && `You are carrying ${money.format(dashboard.cash)}.`,
  )

  // One word for how a hand ended, used for the round when it has only one hand in it.
  const verdictOf = (status: string) =>
    status === 'blackjack' ? 'Blackjack'
    : status === 'bust' ? 'Bust'
    : status === 'dealer_bust' ? 'Dealer bust'
    : status === 'won' ? 'You take it'
    : status === 'push' ? 'Push'
    : status === 'split' ? 'Split decision'
    : status === 'surrendered' ? 'Given up'
    : status === 'stood' ? 'Waiting on the dealer'
    : 'The house takes it'

  return <div className="d-grid gap-3">
    <section className="card p-3">
      <div className="panel-title"><h2>Blackjack</h2><span>{dashboard.city}</span></div>
      <p className="text-body-secondary mb-0">
        The only game here you can play badly, and so the only one worth learning. The dealer
        {board.dealerHitsSoft17 ? ' hits' : ' stands on'} a soft seventeen, a natural pays
        {' '}{board.blackjackPaysNumerator} to {board.blackjackPaysDenominator}, you may split up to
        {' '}{board.maxSplits} time{board.maxSplits === 1 ? '' : 's'} a round, and the shoe is
        shuffled every hand so there is nothing to count.
        {board.insuranceEnabled && ' Insurance is offered whenever the dealer shows an ace, and taking it is the worst bet in the building.'}
      </p>
      <div className="d-grid gtc-fill-220 gap-2 mt-3">
        {board.tables.map(t => <button
          className={`tile d-grid gap-1 text-start border rounded p-2 ${t.key === table.key ? 'active border-primary' : 'bg-body-tertiary'} ${t.locked ? 'opacity-75' : ''}`}
          type="button"
          disabled={busy || !!round?.inPlay}
          onClick={() => { setTableKey(t.key); setBet(t.minBet) }}
          key={t.key}
        >
          <strong className="text-body">{t.name}</strong>
          <small className="text-body-tertiary small">{money.format(t.minBet)}-{money.format(t.maxBet)}</small>
          {t.locked
            ? <small className="text-warning small">{t.lockedReason}</small>
            : <small className="text-primary small">{t.blurb}</small>}
        </button>)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>{table.name}</h2><span>{money.format(table.minBet)} min</span></div>

      {round
        ? <div className="d-grid gap-3">
            <div className="blackjack-felt d-grid gap-3 rounded p-3">
              <div className="d-grid gap-1">
                <small className="text-body-tertiary">
                  Dealer{round.inPlay ? ' shows' : ''} <strong>{round.dealerBest}</strong>{round.inPlay && ' and one down'}
                </small>
                <div className="d-flex flex-wrap gap-1 align-items-center">
                  {round.dealerCards.map((card, i) =>
                    // The hole card is the one that turns over, and it only ever mounts at the reveal.
                    <PlayingCard card={card} index={i} flip={!round.inPlay && i === 1} key={`${card}-${i}`} />)}
                  {round.inPlay && <span className="playing-card is-down" aria-label="Face down" />}
                </div>
              </div>

            {round.awaitingInsurance && <div className="border border-warning rounded p-3 d-grid gap-2">
              <div>
                <strong>The dealer is showing an ace.</strong>
                <div className="text-body-tertiary small">
                  Insurance costs {money.format(round.insuranceCost)} and pays 2 to 1 if the card underneath is
                  worth ten. Four ranks in thirteen are, so this is a bet the house wants you to take.
                </div>
              </div>
              <div className="control-row">
                <Button
                  className="btn btn-secondary"
                  blocked={firstReason(busy && BUSY, !round.canInsure && `Insurance is ${money.format(round.insuranceCost)} and you are carrying ${money.format(dashboard.cash)}.`)}
                  onClick={() => void run(() => api.blackjackMove('insure'))}
                >Insure {money.format(round.insuranceCost)}</Button>
                <Button className="btn btn-primary" blocked={busy && BUSY} onClick={() => void run(() => api.blackjackMove('decline'))}>
                  No insurance
                </Button>
              </div>
            </div>}

            {/* One hand usually, several after a split, and the one being played is ringed. */}
            <div className="d-grid gap-2">
              {round.hands.map(hand => <div
                className={`d-grid gap-1 rounded p-2 ${hand.isActive ? 'border border-primary' : 'border border-transparent'}`}
                key={hand.index}
              >
                <small className="text-body-tertiary d-flex justify-content-between gap-2">
                  <span>
                    {round.hands.length > 1 && <strong>Hand {hand.index + 1}. </strong>}
                    <strong>{hand.best}</strong>{hand.soft && ' soft'} for {money.format(hand.bet)}
                  </span>
                  {!round.inPlay && <span className={hand.netResult > 0 ? 'text-success' : 'text-body-secondary'}>
                    {verdictOf(hand.status)} {signedMoney(hand.netResult)}
                  </span>}
                </small>
                <div className="d-flex flex-wrap gap-1">
                  {hand.cards.map((card, i) => <PlayingCard card={card} index={i} key={`${card}-${i}`} />)}
                </div>

                {hand.isActive && !round.awaitingInsurance && <div className="control-row mt-1">
                  <Button className="btn btn-primary" blocked={busy && BUSY} onClick={() => void run(() => api.blackjackMove('hit'))}>Hit</Button>
                  <Button className="btn btn-secondary" blocked={busy && BUSY} onClick={() => void run(() => api.blackjackMove('stand'))}>Stand</Button>
                  <Button
                    className="btn btn-secondary"
                    blocked={firstReason(busy && BUSY, !hand.canDouble && 'Doubling is for the first two cards, and needs the stake again.')}
                    onClick={() => void run(() => api.blackjackMove('double'))}
                  >Double {money.format(hand.bet)}</Button>
                  {hand.canSplit && <Button
                    className="btn btn-secondary"
                    blocked={busy && BUSY}
                    onClick={() => void run(() => api.blackjackMove('split'))}
                  >Split {money.format(hand.bet)}</Button>}
                  {hand.canSurrender && <Button
                    className="btn btn-outline-secondary"
                    blocked={busy && BUSY}
                    onClick={() => void run(() => api.blackjackMove('surrender'))}
                  >Surrender for {money.format(Math.floor(hand.bet / 2))}</Button>}
                </div>}
              </div>)}
              </div>
            </div>

            {!round.inPlay && <div className={`border rounded p-3 ${round.netResult > 0 ? 'border-success' : 'border-secondary'}`}>
              <div className="d-flex justify-content-between gap-3 align-items-baseline">
                <strong>{round.hands.length > 1 ? `${round.hands.length} hands` : verdictOf(round.status)}</strong>
                <span className={`tnum ${round.netResult > 0 ? 'text-success' : 'text-body-secondary'}`}>{signedMoney(countedNet)}</span>
              </div>
              <small className="text-body-tertiary d-block">
                Dealer {round.dealerBest} against {round.hands.map(h => h.best).join(', ')}. {money.format(round.bet)} down, {money.format(countedPayout)} back.
              </small>
              {round.insuranceBet > 0 && <small className="text-body-tertiary d-block">
                Insurance {money.format(round.insuranceBet)}
                {round.insurancePayout > 0 ? ` paid ${money.format(round.insurancePayout)}.` : ' went down with it.'}
              </small>}
            </div>}
          </div>
        : <p className="text-body-tertiary">Nothing dealt. Put a bet up and the dealer will take it.</p>}

      {!round?.inPlay && <div className="control-row mt-3">
        <label className="field">Bet
          <input
            className="form-control"
            type="number"
            min={table.minBet}
            max={table.maxBet}
            step={table.minBet}
            value={bet}
            onChange={event => setBet(Number(event.target.value))}
          />
        </label>
        <button className="btn btn-secondary" type="button" disabled={busy} onClick={() => setBet(table.minBet)}>Min</button>
        <button className="btn btn-secondary" type="button" disabled={busy} onClick={() => setBet(Math.min(table.maxBet, dashboard.cash))}>Max</button>
        <Button className="btn btn-primary" blocked={dealBlocked} onClick={() => void run(() => api.blackjackDeal(table.key, clamped))}>
          Deal {money.format(clamped)}{board.handTurnCost > 0 && ` / ${board.handTurnCost}t`}
        </Button>
      </div>}
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Pit Ledger</h2><span>Recent hands</span></div>
      {board.recent.length === 0
        ? <p className="text-body-tertiary mb-0">No hands yet.</p>
        : <div className="table-responsive">
            <table className="table table-sm game-table align-middle mb-0">
              <thead><tr><th>Table</th><th>You</th><th>Dealer</th><th>Result</th><th>Bet</th><th>Net</th><th>When</th></tr></thead>
              <tbody>
                {board.recent.map(row => <tr key={row.id}>
                  <td>{row.tableName}</td>
                  <td><span className="d-grid gap-1">{row.hands.map((h, hi) => <span className="d-inline-flex gap-1 align-items-center" key={hi}>
                    {h.cards.map((c, i) => <PlayingCard card={c} small key={i} />)}
                    <span className="text-body-tertiary ms-1">{h.best}</span>
                  </span>)}</span></td>
                  <td><span className="d-inline-flex gap-1 align-items-center">{row.dealerCards.map((c, i) => <PlayingCard card={c} small key={i} />)}<span className="text-body-tertiary ms-1">{row.dealerBest}</span></span></td>
                  <td>{row.status.replace('_', ' ')}</td>
                  <td>{money.format(row.bet)}</td>
                  <td className={row.netResult >= 0 ? 'text-success' : 'text-danger'}>{signedMoney(row.netResult)}</td>
                  <td>{new Date(row.settledAtUtc).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</td>
                </tr>)}
              </tbody>
            </table>
          </div>}
    </section>
  </div>
}

/**
 * The cloth.
 *
 * Bets are held here and sent together, because that is what a spin of a real wheel is: everything on
 * the table settles against one pocket. Sending them one at a time would be a different game with the
 * same name and much better odds.
 */
function RoulettePanel(ctx: PageContext) {
  const { dashboard, busy, refreshDashboard, act } = ctx
  const [board, setBoard] = useState<RouletteBoard | null>(null)
  const [tableKey, setTableKey] = useState('')
  const [chip, setChip] = useState(0)
  const [stakes, setStakes] = useState<RouletteStake[]>([])
  const [last, setLast] = useState<RouletteSpin | null>(null)
  const [rolling, setRolling] = useState(false)
  // Where the disc is pointing, in degrees. It only ever grows, so each spin carries on from wherever
  // the last one stopped rather than snapping back to nothing first.
  const [rotation, setRotation] = useState(0)
  const [loadError, setLoadError] = useState('')

  useEffect(() => {
    let live = true
    void api.roulette()
      .then(next => {
        if (!live) return
        setBoard(next)
        const open = next.tables.find(t => !t.locked) ?? next.tables[0]
        if (open) { setTableKey(open.key); setChip(open.minBet) }
        setLoadError('')
      })
      .catch(error => { if (live) setLoadError((error as Error).message) })
    return () => { live = false }
  }, [dashboard.playerId])

  const table = board?.tables.find(t => t.key === tableKey) ?? board?.tables.find(t => !t.locked) ?? board?.tables[0]
  const staked = stakes.reduce((total, bet) => total + bet.amount, 0)
  const red = new Set(board?.redPockets ?? [])

  // One entry per spot on the cloth: clicking the same spot again stacks another chip on it, which is
  // how chips go down in a casino and keeps the bet count under the croupier's limit.
  const place = (kind: string, value: string | null) => {
    if (!table) return
    setStakes(current => {
      const at = current.findIndex(bet => bet.kind === kind && (bet.value ?? '') === (value ?? ''))
      if (at < 0) return [...current, { kind, value, amount: chip }]
      const raised = Math.min(table.maxBet, current[at].amount + chip)
      return current.map((bet, i) => i === at ? { ...bet, amount: raised } : bet)
    })
  }

  const lift = (kind: string, value: string | null) =>
    setStakes(current => current.filter(bet => !(bet.kind === kind && (bet.value ?? '') === (value ?? ''))))

  const amountOn = (kind: string, value: string | null) =>
    stakes.find(bet => bet.kind === kind && (bet.value ?? '') === (value ?? ''))?.amount ?? 0

  const roll = async () => {
    if (!table || rolling || stakes.length === 0) return
    let spin: RouletteSpin | null = null
    setRolling(true)
    await act(async () => {
      const result = await api.spinRoulette(table.key, stakes)
      spin = result
      return result
    })

    const settled = spin as RouletteSpin | null
    if (!settled) {
      setRolling(false)
      return
    }

    // Turn the disc until the pocket that came up is under the pointer. Six whole turns on top of
    // that, so it reads as a wheel being spun rather than as a dial being set.
    const order = wheelOrders[table.zeroes >= 2 ? 2 : 1]
    const landed = Math.max(0, order.indexOf(settled.pocket))
    const step = 360 / order.length
    const facing = -(landed * step + step / 2)
    const reduced = document.documentElement.getAttribute('data-motion') === 'reduced'
    setRotation(current => {
      const turns = reduced ? 0 : 360 * 6
      // Whatever it takes to get from where the disc is now to where it has to end up, going forwards.
      const shortfall = ((facing - current) % 360 + 360) % 360
      return current + turns + shortfall
    })

    if (!reduced) await wait(rouletteSpinMs)
    setRolling(false)
    setLast(settled)
    setBoard(settled.board)
    setStakes([])
    await refreshDashboard()
  }

  if (loadError) return <section className="card p-3"><div className="panel-title"><h2>Roulette</h2><span>Closed</span></div><p>{loadError}</p></section>
  if (!board || !table) return <section className="card p-3"><div className="panel-title"><h2>Roulette</h2><span>Loading</span></div><p>The croupier is counting the float.</p></section>
  if (!board.enabled) return <section className="card p-3"><div className="panel-title"><h2>Roulette</h2><span>Covered</span></div><p>The wheel is covered for the night.</p></section>

  const blocked = firstReason(
    rolling && 'The ball is still going.',
    busy && BUSY,
    table.locked && (table.lockedReason ?? 'That table is closed to you.'),
    stakes.length === 0 && 'Put something on the cloth first.',
    stakes.length > board.maxBetsPerSpin && `The croupier will take ${board.maxBetsPerSpin} bets on one spin.`,
    dashboard.turns < board.spinTurnCost && `A spin is ${board.spinTurnCost} turn${board.spinTurnCost === 1 ? '' : 's'} and you have ${dashboard.turns}.`,
    dashboard.cash < staked && `That is ${money.format(staked)} on the cloth and you are carrying ${money.format(dashboard.cash)}.`,
  )

  const pockets = Array.from({ length: 36 }, (_, i) => String(i + 1))
  const zeroes = table.zeroes >= 2 ? ['0', '00'] : ['0']
  const outside = board.betKinds.filter(k => k.key !== 'straight')
  const chips = [table.minBet, table.minBet * 5, table.minBet * 20, table.minBet * 100].filter(v => v <= table.maxBet)

  return <div className="d-grid gap-3">
    <section className="card p-3">
      <div className="panel-title"><h2>Roulette</h2><span>{dashboard.city}</span></div>
      <p className="text-body-secondary mb-0">
        Every bet on a cloth is paid as though the zeroes were not on the wheel, so the zeroes are the
        entire house edge and every bet carries the same one. Picking between a number and a colour is
        picking how hard you want the swing, not how good the odds are.
      </p>
      <div className="d-grid gtc-fill-220 gap-2 mt-3">
        {board.tables.map(t => <button
          className={`tile d-grid gap-1 text-start border rounded p-2 ${t.key === table.key ? 'active border-primary' : 'bg-body-tertiary'} ${t.locked ? 'opacity-75' : ''}`}
          type="button"
          disabled={busy || rolling}
          onClick={() => { setTableKey(t.key); setStakes([]); setChip(t.minBet) }}
          key={t.key}
        >
          <strong className="text-body">{t.name}</strong>
          <small className="text-body-tertiary small">{money.format(t.minBet)}-{money.format(t.maxBet)} / {t.pockets} pockets</small>
          <small className="text-body-tertiary small">Returns {t.returnPercent}% / {t.zeroes === 1 ? 'single zero' : 'double zero'}</small>
          {t.locked
            ? <small className="text-warning small">{t.lockedReason}</small>
            : <small className="text-primary small">{t.blurb}</small>}
        </button>)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>{table.name}</h2><span>{money.format(table.minBet)} min</span></div>

      <div className="d-flex flex-wrap gap-3 align-items-center justify-content-center mb-3">
        <RouletteWheel
          order={wheelOrders[table.zeroes >= 2 ? 2 : 1]}
          red={red}
          rotation={rotation}
          turning={rolling}
        />
      </div>

      {last && !rolling && <div className={`roulette-result d-flex align-items-center gap-3 border rounded p-3 mb-3 ${last.spin.netResult > 0 ? 'border-success' : 'border-secondary'}`}>
        <span className={`roulette-pocket is-${last.colour}`}>{last.pocket}</span>
        <div className="d-grid">
          <strong>{last.spin.netResult > 0 ? `Paid ${money.format(last.spin.payoutAmount)}` : 'The house takes it'}</strong>
          <small className="text-body-tertiary">
            {last.spin.bets.filter(b => b.payout > 0).length} of {last.spin.bets.length} bets came in
            {last.turnsSpent > 0 && `, ${last.turnsSpent} turn${last.turnsSpent === 1 ? '' : 's'}`}
            {last.repEarned > 0 && `, +${number.format(last.repEarned)} rep`}.
          </small>
        </div>
        <span className={`tnum ms-auto ${last.spin.netResult >= 0 ? 'text-success' : 'text-body-secondary'}`}>
          {signedMoney(last.spin.netResult)}
        </span>
      </div>}

      <div className="control-row mb-3">
        <span className="text-body-secondary">Chip</span>
        <div className="btn-group" role="group" aria-label="Chip">
          {chips.map(value => <button
            className={`btn btn-sm ${chip === value ? 'btn-primary' : 'btn-secondary'}`}
            type="button"
            disabled={busy || rolling}
            onClick={() => setChip(value)}
            key={value}
          >{money.format(value)}</button>)}
        </div>
      </div>

      <div className="roulette-cloth d-grid gap-1 mb-3">
        {zeroes.map(pocket => <button
          className={`roulette-spot is-green ${amountOn('straight', pocket) > 0 ? 'is-backed' : ''}`}
          style={{ gridColumn: `span ${6 / zeroes.length}` }}
          type="button"
          disabled={busy || rolling}
          onClick={() => place('straight', pocket)}
          onContextMenu={event => { event.preventDefault(); lift('straight', pocket) }}
          key={pocket}
        >
          {pocket}
          {amountOn('straight', pocket) > 0 && <span className="roulette-chip">{money.format(amountOn('straight', pocket))}</span>}
        </button>)}
        {pockets.map(pocket => <button
          className={`roulette-spot ${red.has(Number(pocket)) ? 'is-red' : 'is-black'} ${amountOn('straight', pocket) > 0 ? 'is-backed' : ''}`}
          type="button"
          disabled={busy || rolling}
          onClick={() => place('straight', pocket)}
          onContextMenu={event => { event.preventDefault(); lift('straight', pocket) }}
          key={pocket}
        >
          {pocket}
          {amountOn('straight', pocket) > 0 && <span className="roulette-chip">{money.format(amountOn('straight', pocket))}</span>}
        </button>)}
      </div>

      <div className="d-grid gtc-fill-180 gap-2 mb-3">
        {outside.flatMap(kind => (kind.takesNumber ? ['1', '2', '3'] : [null]).map(value => {
          const on = amountOn(kind.key, value)
          return <button
            className={`btn btn-sm text-start ${on > 0 ? 'btn-primary' : 'btn-secondary'}`}
            type="button"
            disabled={busy || rolling}
            onClick={() => place(kind.key, value)}
            onContextMenu={event => { event.preventDefault(); lift(kind.key, value) }}
            title={kind.blurb}
            key={`${kind.key}-${value ?? 'x'}`}
          >
            {value ? `${kind.name} ${value}` : kind.name}
            <span className="text-body-tertiary"> {kind.odds}:1</span>
            {on > 0 && <strong className="float-end">{money.format(on)}</strong>}
          </button>
        }))}
      </div>

      <div className="control-row">
        <span className="text-body-secondary">
          {stakes.length === 0
            ? 'Nothing on the cloth.'
            : `${stakes.length} bet${stakes.length === 1 ? '' : 's'}, ${money.format(staked)} down.`}
        </span>
        <button className="btn btn-secondary" type="button" disabled={busy || rolling || stakes.length === 0} onClick={() => setStakes([])}>Clear</button>
        <Button className="btn btn-primary" blocked={blocked} onClick={() => void roll()}>
          Spin {money.format(staked)}{board.spinTurnCost > 0 && ` / ${board.spinTurnCost}t`}
        </Button>
      </div>
      <small className="text-body-tertiary d-block mt-2">Click a spot to back it, again to add another chip, right-click to take it off.</small>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Wheel Ledger</h2><span>Recent spins</span></div>
      {board.recent.length === 0
        ? <p className="text-body-tertiary mb-0">No spins yet.</p>
        : <div className="table-responsive">
            <table className="table table-sm game-table align-middle mb-0">
              <thead><tr><th>Table</th><th>Pocket</th><th>Bets</th><th>Staked</th><th>Payout</th><th>Net</th><th>When</th></tr></thead>
              <tbody>
                {board.recent.map(row => <tr key={row.id}>
                  <td>{row.tableName}</td>
                  <td><span className={`roulette-pocket is-small is-${row.colour}`}>{row.pocket}</span></td>
                  <td title={row.bets.map(b => `${b.label} ${money.format(b.amount)}`).join(', ')}>
                    {row.bets.filter(b => b.payout > 0).length}/{row.bets.length}
                  </td>
                  <td>{money.format(row.staked)}</td>
                  <td>{money.format(row.payoutAmount)}</td>
                  <td className={row.netResult >= 0 ? 'text-success' : 'text-danger'}>{signedMoney(row.netResult)}</td>
                  <td>{new Date(row.createdAtUtc).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}</td>
                </tr>)}
              </tbody>
            </table>
          </div>}
    </section>
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
