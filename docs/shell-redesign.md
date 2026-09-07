# The shell, and what it costs to look at

A read of the client as it stands, and a proposal for the shell, the page list and the words on it.
Nothing here is a rule; it is an argument, written so the parts of it that are wrong can be found and
dropped without taking the rest with them.

Line references are against `09d233a`. Pixel figures are computed from the stylesheet and the markup
rather than measured on a handset, so they are close rather than exact — the shape of the problem
does not depend on the last twenty pixels.

## The map today

| Page | Route id | Sections | Reached on a phone by |
| --- | --- | --- | --- |
| Overview | `overview` | none — twelve stacked panels | tab bar |
| Street | `street` | none — shift, bank, activity | tab bar |
| Crew | `crew` | Crew · Hideout · Craft Queue | tab bar |
| Business | `market` | Shop · Flea · Runs | tab bar |
| Raids & Map | `recon` | Raids · Map · Missions | More sheet |
| Alliance | `alliance` | none — nine stacked panels | More sheet |
| Casino | `casino` | Slots · Blackjack · Roulette, not routed | More sheet |
| Seasons | `seasons` | This Season · Finished · Your Record | More sheet |
| Updates | `updates` | none | More sheet |
| Account | `account` | seven tabs | More sheet |
| Admin | `admin` | nine tabs | More sheet |

Thirty-six leaf screens. The four with a permanent slot are the opening loop, which is the right
answer on day one and the wrong one by week two: raiding and the alliance, the two things a settled
player opens the game for, are both two taps deep behind a word that means nothing.

## What a phone pays

On a 390x812 screen, per page: 16px of padding, a 66px page header, 298px of status strip, 44px of
section tabs, a 61px chat dock and the 53px tab bar. The first card started 473px down, and the strip
of screen between it and the dock was 225px.

Measured against `3650873`, before any of the work below. After Phase 1 the same card starts at 151px
and the playable strip is 608px - see the phase list at the end.

**M1 — The status strip is the largest thing on every page, and it is not the page.**
Eight tiles, two columns, four rows. `_components.scss:381` records that it once ate 415px and that a
compression pass cut it to about two thirds, which still leaves ~300px of readout above content on
all eleven pages whether the player is using any of it or not. Cash, turns and heat gate every
decision; bank, net worth, upkeep, rank and city are things you look up.
*Fix:* one 40px sticky line — `$1.24M · 87/120 turns +12 in 4:31 · Hunted` — tapping to expand into
the full eight as a sheet, and gaining a second warning line only while the player is away from the
hideout. Chrome that appears because the state calls for it rather than standing there permanently.

**M2 — The page title repeats what the lit tab already says.** `main.tsx:1977`. Two labels for one
fact, on the screen with the least room to say anything twice. The player plate beside it is already
hidden on phones for exactly this reason. *Fix:* clip the heading below `md` — clipped rather than
removed, since it is the page's only `h1` — and move the alert bell, which is a control rather than a
label, into the new status bar.

The heading and its kicker are ~66px, but the row does not shrink by that much: the bell is 44px tall
and holds it open. Clipping the heading buys about 30px on its own, measured; the remaining ~36px
arrives in Phase 1, when the bell moves into the status bar and the header row goes entirely. The
first draft of this document credited M2 with the whole 70px, which was the two moves counted as one.

**M3 — Section tabs scroll away, on the longest pages in the game.** Hideout runs four panels and a
room list, Runs five, Map five. Changing tab means scrolling back to the top first. *Fix:* pin the
strip under the status bar.

**M4 — Nothing pins the one button the page exists for.** Street's Work button sits after a district
picker, three metric tiles, a storage notice and the supply panel. *Fix:* a sticky action bar above
the tab bar carrying the page's verb and its live price — `Work 20 turns · 20T · $0` — the same
button, with the same blocked reason, always in reach.

**M5 — Chat and navigation are fighting over the same fifty pixels.** The dock pins itself above the
tab bar and publishes `--chat-dock-height` so every page can pad itself out of the way. That is a
well-built workaround for a component in the wrong place: the log gets 22vh, and the page pays for it
whether chat is open or not. *Fix:* a message icon with an unread badge in the status bar, opening
full screen. The height variable and the padding arithmetic go away.

**M6 — Overview buries its most useful panel fifth.** Twelve panels, and the one that answers "what
should I do now" is five deep; much of the rest is a second copy of something with a home elsewhere.
*Fix:* alerts, Next Moves, the ladder, then everything else collapsed. Anything not actionable right
now opens shut on a phone — most of Hideout's room list, most of Alliance, the fifty-row ladder.

Two smaller things: upkeep reads `4C / 12B / 3D`, a code decoded on a screen with room to spell it;
and the numbers should be said in words.

## What a desktop wastes

The wide layout is the phone layout with a rail bolted on: same single scroll column, same chrome in
the same order, 1,540px spent making panels wider rather than showing more of the game at once.

**D1 — Between 768px and 1200px the rail is eleven two-letter codes.** `OV ST CR BZ CA RM SN UP AL AC
AD` is not an icon set, it is a cipher, and it lands on the 1024–1280px laptop. Meanwhile
`bootstrap-icons` is a dependency, its whole font is imported at `main.tsx:51`, and five glyphs are
used anywhere in the client. *Fix:* spend the icon font — icon and label where the rail fits both,
icon and the existing hover title where it does not.

**D2 — The status strip scrolls sideways on tablets, which is the defect the phone pass fixed.**
Above `md` it is `repeat(8, minmax(138px, 1fr))` with `overflow-x: auto`, needing ~1,160px; at a
768px viewport the content column is ~634px. *Fix:* `repeat(auto-fit, minmax(138px, 1fr))`, and drop
the horizontal scroll.

**D3 — Nothing on a desktop is persistent except navigation.** The turn clock, alerts, Next Moves and
chat all live inside the scrolling page. *Fix:* three columns above `xl` — rail, page, context — with
the context column sticky. That absorbs M5, and half of what Overview duplicates stops needing to
exist.

**D4 — Seven two-column splits, so nothing lands twice in the same place.** `split-11`, `-108`,
`-135`, `-92`, `-90`, `-80`, `-280`. *Fix:* two grammars — "work beside reference" and "full width" —
and every page picks one.

**D5 — Three tab patterns, and one forgets where you were.** `SectionTabs` pills on four pages, a
150px card grid on Account and Admin, and plain `useState` buttons in the casino — so a reload drops
you from the blackjack table back to the slots, on the page where reloading is most tempting, which
is the complaint `route.ts` exists to answer. *Fix:* one strip everywhere, and route the casino game
through `useRouteTab`.

**D6 — Panels are copied between pages rather than linked.** Bank on Street and again on Business ·
Shop; standings on Overview at eight rows and on Raids · Missions at fifty; inventory twice. *Fix:*
one home each and a link from the other. Bank belongs on Street, where the walkthrough already
teaches it; the full ladder belongs on Seasons, which is the record.

**D7 — Thirty-six destinations and no way to type where you are going.** Every page, tab and panel
already has a stable name: the hash carries `#/crew/hideout`, `flowTarget()` maps a name onto page,
tab and panel, and panels carry `data-area` anchors. A palette is mostly wiring what exists.

## Words that slip

**N1 — "Crew" means two things.** It is the payroll, and it is the alliance: the Alliance page's own
button reads *Start a Crew*, and the README calls alliances crews. The shared thug pool makes both
readings plausible in the same sentence. *Fix:* crew is the people on your payroll; the group is the
Alliance, in copy as well as in code.

**N2 — Two pages answer to a name they do not carry.** *Business* routes as `market`, *Raids & Map*
as `recon`. *Fix:* label and id agree — Trade and War — with the old ids redirecting in `route.ts`.

**N3 — A page named after people is mostly rooms and a workbench.** The reasoning for putting the
hideout and the bench under Crew is sound; the label promises one of the three. *Fix:* Empire.

## Five destinations, the same five on both screens

The deeper problem is that the game keeps two maps of itself: four-plus-More on a phone, eleven in a
rail on a desktop. A player who learns one does not know the other.

| | |
| --- | --- |
| **Streets** | alerts · next moves · shift · supplies · bank |
| **Empire** | crew · hideout · bench |
| **Trade** | shop · flea · runs · travel |
| **War** | targets · map · missions · alliance |

Everything else — Casino, Seasons, Updates, Account, Admin, Logout — stays behind More, which stays
in the bottom bar. Travel joins Trade because a trip is a purchase priced in turns and fare. Alliance
joins War because wars, pacts, assist calls and the thug pool are combat, and it gives those nine
panels the tabs they have always needed.

### Revised after measuring: a phone tab bar holds five labels

The first draft of this section moved the overflow to a menu in the top bar and kept five slots for
the game. Both halves of that turn out not to fit. Measured against the shipped shell at 430, 390,
360 and 320px:

- **A third icon in the status bar overflows the page.** The row already carries three figures and two
  handles; one more pushes it 6px past a 390px screen and 34px past a 320px one. The top right is also
  the hardest corner of a phone to reach, which is the worst place for the only route to six
  destinations when the bar it came from is the easiest.
- **Six labelled slots do not fit either.** At 390px "Streets" and "Empire" ellipsize; dropping the
  label to 12px rescues 390 and still fails at 360.

Five slots, one of which must be More, leaves four for the game. So the recommendation is to fold
**Home into Streets** — alerts, Next Moves and the ladder become the top of the page a session starts
on — and the bar reads **Streets · Empire · Trade · War · More**. That keeps the names, keeps More
under a thumb, and still gets War out of the sheet, which is what this section is for.

A bar that reorders itself under the player's thumb is worse than one that never moves, so: badges,
not shuffling. War carries a dot when a strike is inbound or an assist is called; Home carries the
unread count.

### Renames

| Today | Proposed | Why |
| --- | --- | --- |
| Overview | Home | Where you land, not a summary of anything |
| Street | Streets | Plural to match the verb |
| Crew (`crew`) | Empire (`empire`) | Crew, hideout and bench; the label promised one |
| Business (`market`) | Trade (`trade`) | Label and id agree |
| Raids & Map (`recon`) | War (`war`) | One word fits a tab bar; the id stops lying |
| Alliance | War · Alliance | It is combat |
| Craft Queue | Empire · Bench | The code already calls it the bench |
| Travel, on Overview | Trade · Travel | A trip is a purchase |

The renames are cheaper than they look: `pageMeta` and `flowTarget` are already the only two places
that know what a page is called and where a section lives, and a rule test binds `flowTarget` to the
server's `GuidancePages`. Add an id redirect in `route.ts` so shared links survive.

## What to do first

**Phase 0 — repairs. Done.** No design decisions in any of them.

- D2 — the strip is `repeat(auto-fit, minmax(138px, 1fr))` and the `overflow-x` is gone. Measured at a
  768px viewport: the strip's scroll width and client width are both 634px, so there is nothing to
  scroll to, and the eight tiles lay out in two rows. One row again by 1440px.
- D5 — the casino game goes through `useRouteTab`, so `#/casino/blackjack` survives a reload. The
  three rooms are one list now, which is also the set the address bar will accept.
- M3 — the section strip is `position: sticky` below `md`, in the bottom bar's colours. Measured: it
  holds at the top of the viewport through a 700px scroll, and the page gains no horizontal overflow
  from the negative margin that takes it out to the padding edge.
- M2 — the heading is clipped below `md` and the bell moves to the end of the row. Header measured
  66px before, 44px after, plus the row's own bottom margin halved - and zero once Phase 1 moved the
  bell out of it entirely.

**Phase 1 — the phone shell. Done.**

- M1 — three figures pinned in one 58px line, the other five a tap below them, and the town joining
  them only while it disagrees with the hideout's (95px then). The bell moves in, which empties the
  page header on a phone and takes it to zero.
- M4 — the page's one verb appears along the bottom once the real button has left the screen, and is
  not there at all while it has not. Street and the slots have one; the shop, the bench and the map
  are lists of small actions with no single dominant verb, and were left alone.
- M5 — chat is a sheet on a phone: nothing on screen minimised, the whole screen open, and the log
  takes 664px where it had 179. `--chat-dock-height` is gone and the page's bottom padding clears one
  bar rather than two.
- M6 — Next Moves and the opening ladder lead the home page instead of coming fifth and seventh.
  Readiness and Inventory arrive shut on a phone.

Measured, at 390x812: the first card moved from 473px down to 151px, and the playable strip from
225px to 608px - 2.7x. The estimate in the first draft of this document was 244px to 582px, so the
shape was right and the result is a little better than the arithmetic promised.

**Still to do below.**

**Phase 2 — the map.** Five destinations with More among them, the sheet behind it grouped under
headings, Alliance into War, the renames with redirects, and N1 through the copy.

**Phase 3 — the wide shell.** D3 (the context column), D1 (icons in the rail), D4 (two grammars),
D7 (the palette), D6 (de-duplication).

## What not to break

- **Blocked, not disabled.** A button that will not go carries the sentence explaining why, and the
  reason has to be written before the button can be switched off. Every new control keeps this — the
  sticky action bar especially.
- **The address bar remembers.** Page and tab live in the hash and survive a reload without turning
  Back into a walk through thirty tabs. New state worth keeping goes through `useRouteTab`.
- **One place knows where things live.** `flowTarget` maps a name to a page, a tab and a panel, and a
  rule test binds it to `GuidancePages`. Every move above goes through that map and nowhere else.
- **44px on a coarse pointer, and the safe areas are paid for.** The tab bar clears the home
  indicator, `viewport-fit=cover` is deliberate, and the compact density preference never shrinks a
  touch target.
