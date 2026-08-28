/**
 * Display preferences, kept on the device rather than on the account.
 *
 * That is a decision rather than a shortcut. Whether this screen wants tighter
 * rows is a fact about this screen - a phone and a monitor want different
 * answers, and one account reaches the game from both. Reduced motion is the
 * same: it belongs to the machine, and the machine already has an opinion in
 * `prefers-reduced-motion` that is worth starting from.
 *
 * Applied to <html> as attributes, which the stylesheet answers. The same
 * attributes are set by a small inline script in index.html before the bundle
 * loads, so a compact layout does not arrive one frame late as a visible jump.
 */

export type Preferences = {
  /** Tighter padding and gaps on the dense screens. */
  compact: boolean
  /** True or false to decide here; null to follow whatever the system says. */
  reduceMotion: boolean | null
}

export const defaultPreferences: Preferences = { compact: false, reduceMotion: null }

const KEY = 'se.preferences'

/**
 * Reading is wrapped because localStorage is not always there to read: a private
 * window, site data cleared, or a browser told to block storage all throw rather
 * than return nothing. A display preference is never worth an unusable page.
 */
export function loadPreferences(): Preferences {
  try {
    const raw = window.localStorage.getItem(KEY)
    if (!raw) return defaultPreferences
    const parsed = JSON.parse(raw) as Partial<Preferences>
    return {
      compact: parsed.compact === true,
      reduceMotion: parsed.reduceMotion === true ? true : parsed.reduceMotion === false ? false : null,
    }
  } catch {
    return defaultPreferences
  }
}

export function savePreferences(preferences: Preferences) {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(preferences))
  } catch {
    // Storage refused. The preference still applies for this visit, which is the
    // part the player asked for; it simply will not survive the tab.
  }
}

/** What the OS asks for, when nothing here has overridden it. */
export function systemPrefersReducedMotion(): boolean {
  return window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false
}

export function applyPreferences(preferences: Preferences) {
  const root = document.documentElement
  const reduced = preferences.reduceMotion ?? systemPrefersReducedMotion()

  if (preferences.compact) root.setAttribute('data-density', 'compact')
  else root.removeAttribute('data-density')

  if (reduced) root.setAttribute('data-motion', 'reduced')
  else root.removeAttribute('data-motion')
}

/**
 * Follows the OS while nothing here has overridden it. Somebody who turns the
 * system switch on mid-session should see the game stop moving without having to
 * reload it.
 */
export function watchSystemMotion(current: () => Preferences): () => void {
  const query = window.matchMedia?.('(prefers-reduced-motion: reduce)')
  if (!query) return () => {}
  const onChange = () => { if (current().reduceMotion === null) applyPreferences(current()) }
  query.addEventListener('change', onChange)
  return () => query.removeEventListener('change', onChange)
}
