/*
  Where you were, kept in the address bar.

  Refreshing used to put you back on the Overview no matter what you had open, which is worst exactly
  where reloading is most tempting: a stuck panel, a stale number, an admin tab you have been editing
  in. The page and its tab are the two things worth surviving a reload, so they live in the hash:

    #/crew/hideout

  The hash rather than the path because nothing here is a real route - there is no server side to it,
  and a path would need the host to serve index.html for every address the app might invent. The hash
  costs nothing and works the same on a static host.

  replaceState rather than pushState throughout. Restoring on refresh is one thing; turning Back into
  a walk through the last thirty tabs you glanced at, so that leaving the game takes thirty presses,
  is another. Every write here edits the current entry, so Back still means what it did before.
*/

/** The hash as its parts. `#/crew/hideout` is `['crew', 'hideout']`; anything empty is dropped. */
function parts(): string[] {
  return window.location.hash.replace(/^#\/?/, '').split('/').filter(Boolean)
}

/** Which page the address bar currently claims, or '' before anything has been written. */
export function routePage(): string {
  return parts()[0] ?? ''
}

/** The tab the address bar holds for `page`, or '' when it is describing some other page. */
export function routeTab(page: string): string {
  const [written, tab] = parts()
  return written === page ? tab ?? '' : ''
}

/**
 * Points the address bar at a page, and at a tab within it when there is one.
 *
 * Naming the page again when writing a tab is what keeps the two in step. Page changes are written
 * from the click that causes them and tabs from an effect after the new page has mounted, and effects
 * run children first - so a tab that wrote only its own half would be writing it under whichever page
 * the bar still named. Writing the pair together means whoever writes last is right.
 */
export function writeRoute(page: string, tab?: string): void {
  const hash = `#/${tab ? `${page}/${tab}` : page}`
  if (hash === window.location.hash) return
  window.history.replaceState(window.history.state, '', window.location.pathname + window.location.search + hash)
  for (const listener of [...listeners]) listener(page, tab ?? '')
}

type RouteListener = (page: string, tab: string) => void
const listeners = new Set<RouteListener>()

/**
 * Told when the address changes, for the parts of the app that have to follow it rather than write it.
 *
 * The tab strips read the address once, when they mount, which answers a reload and nothing else. It
 * stopped being enough when navigation started naming a tab: sending somebody from the guidance list to
 * the room they need is a page change and a remount when they are somewhere else, and nothing at all
 * when they are already on that page looking at a different tab. The second case is the common one -
 * the panel that says "upgrade the storage room" sits on a page whose neighbour is the storage room -
 * and it would have quietly done nothing.
 *
 * replaceState fires no event of its own, which is the whole reason this exists. It is announced here
 * instead, after the write and only when the address actually moved, so a strip re-writing the tab it
 * is already on cannot start a loop.
 */
export function onRouteChange(listener: RouteListener): () => void {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}
