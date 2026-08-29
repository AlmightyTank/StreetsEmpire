/*
  Where you were, kept in the address bar.

  Refreshing used to put you back on the Overview no matter what you had open, which is worst exactly
  where reloading is most tempting: a stuck panel, a stale number, an admin tab you have been editing
  in. The page and its tab are the two things worth surviving a reload, so they live in the hash:

    #/market/hideout

  The hash rather than the path because nothing here is a real route - there is no server side to it,
  and a path would need the host to serve index.html for every address the app might invent. The hash
  costs nothing and works the same on a static host.

  replaceState rather than pushState throughout. Restoring on refresh is one thing; turning Back into
  a walk through the last thirty tabs you glanced at, so that leaving the game takes thirty presses,
  is another. Every write here edits the current entry, so Back still means what it did before.
*/

/** The hash as its parts. `#/market/hideout` is `['market', 'hideout']`; anything empty is dropped. */
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
}
