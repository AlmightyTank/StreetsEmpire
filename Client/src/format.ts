/*
  The small shared vocabulary: how this game writes a number, a date, and a pause.

  These lived at the top of main.tsx, which is where everything lived. They are here because they are
  the one group with no opinion about any page - every screen formats money the same way, and a helper
  that half the app needs is the wrong reason for that half to import the other half.
*/

/** Whole dollars, the way every price and balance in the game is written. */
export const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })

export const number = new Intl.NumberFormat('en-US')

/**
 * Money in the width a phone's status bar has for it.
 *
 * Exact for as long as the figure still fits, and shortened only once it does not: an empire worth
 * twelve million reads perfectly well as $12.9M, and the two digits lost are two nobody was reading.
 * A hundred thousand is the line because below it the exact number is still being spent - supplies
 * are bought in hundreds, and "$1.2K" is a worse answer than "$1,240" to somebody deciding whether
 * they can afford a shift.
 */
const compactMoney = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  notation: 'compact',
  maximumFractionDigits: 1,
})

export function tightMoney(value: number) {
  return Math.abs(value) < 100_000 ? money.format(value) : compactMoney.format(value)
}

/** A delta, where the sign is the point: what a move cost you or paid you. */
export function signedMoney(value: number) {
  return `${value >= 0 ? '+' : '-'}${money.format(Math.abs(value))}`
}

export function compactDateTime(iso: string | null | undefined) {
  return iso ? new Date(iso).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' }) : 'Never'
}

/** Trimmed to fit, with an ellipsis standing in for what was cut. */
export function clampText(value: string, max: number) {
  return value.length <= max ? value : `${value.slice(0, Math.max(0, max - 1)).trimEnd()}...`
}

/**
 * A pause, awaited.
 *
 * Used where a sequence has to be watched rather than merely completed - reels landing one at a time,
 * a win counting up - which is the one case where doing it all at once is the wrong answer.
 */
export function wait(ms: number) {
  return new Promise(resolve => window.setTimeout(resolve, ms))
}
