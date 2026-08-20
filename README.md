# Street Empire 0.2.5

A playable browser-game foundation inspired by the turn-based economy and crew-management loop of classic browser crime/empire games.

## What changed in 0.2.5

0.2.5 is in progress, and is about the early game.

### An order you had no room to carry

Contracts asked for between fifteen and sixty units and would only take the whole amount in one
movement. A first storage room holds five weapons, ten of coke and twenty-five of weed - so a new
player could not fill a single weapons order at any size, and most coke orders stayed impossible until
the fourth storage upgrade. The board was aimed at players who could not reach it.

Goods go in a bit at a time now, and the buyer keeps a tally. What makes that more than a convenience
is where the money sits: **a delivery pays the town's ordinary rate the moment it is made, and the
premium arrives whole when the last unit goes in.** Stopping half way leaves you exactly where selling
those goods flat would have, so instalments are never free money - the premium is what finishing buys.
And because it is never split, two trips pay precisely what one trip would have.

The first delivery claims the order. Without that, two people part-fill the same one and whoever worked
hardest and arrived last has simply wasted the goods. The deadline still frees anything nobody
finishes, and rivals respect a claim the same way players do.

### The design was partly fictional

Two things the stylesheet claimed were not happening. It had asked for Inter since the beginning and
nothing had ever loaded it - no `@font-face`, no link, no package - so every player was reading Segoe UI
or SF and the design had never once been seen as written. It also carried weights of 900 and 950 against
a fallback font that had neither, so both rendered as ordinary bold. Inter is now self-hosted, which
costs one dependency and no third-party request, and the weights are three because a variable font
genuinely distinguishes them and six steps of bold is five too many.

Underneath that, 33 distinct font sizes - fifteen of them between .72rem and .88rem, differing by about
a fifth of a pixel. That is not hierarchy, it is noise wearing hierarchy's clothes: sizes the eye cannot
tell apart read as one muddled size. Eight steps replace them, far enough apart to mean something.
Spacing had the same problem in a different currency - 19 gap values and 51 padding values including
3px, 5px, 9px and 13px - now snapped to a 4px rhythm, rounding tighter rather than looser so nothing
grew back into the overflow the mobile pass had just cleared.

The change that matters most in play is the smallest to describe: figures are tabular. Every number here
moves while you watch it, and proportional digits are different widths, so each tick re-flowed the row
it sat in. They hold still now, and columns of them line up on their digits.

Loading a real font surfaced a bug that had been hiding behind the fallback. The browser's own
stylesheet gives `b` and `strong` a weight of `bolder`, which is relative rather than absolute, so one
nested inside a parent already at 700 computed to 900 - invisible while nothing heavier than bold
existed to render it, and a visible jump the moment something did.

### The phone was holding the game at arm's length

The layout already collapsed to a single column at 760px, so the game *fitted* on a phone. What it did
not do was let you play it with one hand, and the three worst parts were the three that matter most.

**Navigation.** The side rail folded down into a horizontal strip of two-letter codes. Three of the nine
destinations sat off the right-hand edge with nothing to indicate they existed, the six you could see
were abbreviations you had to learn, and the whole thing sat at the top of the screen where a thumb
reaches last. It is now a fixed bottom bar: four named destinations - Overview, Street, Crew, Market,
which is the loop the ladder itself teaches - with the remaining five behind More. Nothing hides off an
edge and every destination keeps its word. When you are on a page More holds, More wears that page's
name, so the bar never shows you a screen you cannot locate yourself on.

**The numbers.** The seven status cards were a sideways scroller, which put cash and heat past the right
edge behind a gesture nothing advertised. Those are the two figures every decision in the game is
weighed against, and they were the ones you had to go looking for. They wrap into two columns now.

**The newest panels were the broken ones.** Mobile was handled by naming each grid individually at a
breakpoint, which works right up until somebody adds a panel - so the alliance page, built after that
list was last revisited, kept its desktop columns and squeezed a member's name and town into 35 pixels.
Rows built around a name now give the name the room and put the controls underneath it.

Beyond that: every control clears 44 pixels wherever the pointer is coarse, including the compact ones,
because fitting one more row on screen is a good trade for a mouse and a bad one for a thumb. The page
reaches under a notch and pays it back with safe-area padding so the bar clears the home indicator, and
heights measure the viewport that is actually visible rather than the taller one an address bar claims.

The palette got names at the same time. It had reached 175 loose hex literals, which meant there was
nothing to change but 175 individual decisions; the eleven doing most of the work are now named for
their job, with 212 uses pointing at them.

### Ranks, and the lines a boss draws

- Four rungs: Soldier, Enforcer, Underboss, Boss. Fewer than a clan system in a game with thousands of
  players, because ranks only mean anything when there are enough people for the gaps to matter, and a
  crew here holds six.
- The boss sets **a minimum rank for each power** - open the door, throw people out, spend the treasury,
  take thugs on a raid, post defenders at home - and that is the part of a rank system that actually
  gets used. Ranks alone are words next to names; what makes two crews with the same ranks run
  completely differently is where their boss drew those five lines, and neither crew had to be
  programmed.
- **You can only act on somebody below you.** Strictly below, never equal, because two Underbosses able
  to throw each other out is not a chain of command.
- **The door is one setting with three states:** open to anyone, by application, or invitation only.
  Those are the three things an outsider can do on their own - walk in, ask, or wait to be asked - and
  each state turns the other two away by name rather than silently. Invitations sit off that axis and
  work in all three, because they are the crew reaching out rather than somebody arriving, and a crew
  that could not invite while set to invitation-only would be a contradiction.
- Invitations and applications are one table read from opposite ends, and every road in ends at the same
  place - joining at the bottom - so no route can hand out a rank by accident. Both are re-checked when
  they are accepted rather than trusted from when they were sent.
- Promotion stops below the top. Handing the crew on is its own move, because it is the one that gives
  yours away.

### A crew is people who have agreed not to rob each other

- That is the whole of what an alliance is, and everything else it carries follows from it. Members
  cannot attack each other by any method - raid, drive-by, jacking, infestation, poaching, or a raid on
  their ground - and the check lives in the launch rules rather than in an endpoint, so every route into
  a fight runs into it: a player's, a rival's brain, and the admin's directive alike.
- The source game left the interesting half of this to the honour system: "don't form super alliances,
  it's against the rules". A rule nobody enforces is not a rule, so here it is mechanical.
- **Dues** are a founder-set share of every member's shift, taken off the gross beside the hoe cut
  because it is the same kind of thing and reads in the same sentence. Off the gross rather than off
  what is left: a house paying 40% and dues of 20% gives up 60% of a shift, not 52%, and compounding
  would make the second rate mean something different depending on the first.
- **Crew rank** is the sum of what the members are worth, off the same expression the individual
  leaderboard ranks by, so the two boards can never disagree.
- Six members, not the source game's twenty. That was a game with thousands of players; this world is
  two dozen rivals, where twenty would not be an alliance but everybody against nobody. Rivals already
  run with each other - crews seed themselves into an existing world on first read, the way ground does
  - and a share of rivals always stays unaligned, because a board where everyone has agreed not to fight
  has nothing left in it.
- **The pool** is what the treasury buys: offensive thugs that ride along on a member's raid, defensive
  ones posted to a member's house. Both fight as an armed thug apiece and both die, and what dies is
  gone for good. Thugs on a raid leave the pool until it comes home, so what you take tonight your ally
  does not have - that shared, finite pot is the whole reason a crew is an arrangement between people
  rather than a switch you flip once.
- **A member may bring at most as many borrowed thugs as they brought of their own**, and that is the
  rule keeping the pool from breaking the game. Alliance thugs ignore the hideout's thug cap, which is
  what every combat number is balanced against; tied to your own crew, the pool amplifies rather than
  substitutes, so your tier still sets your ceiling and the crew only doubles it. Losses fall across the
  whole line in proportion, so borrowing is neither free nor a way of using other people as armour.

### Five districts that finally differ

- The source game had five places to scout and its own guide admits it never found a difference between
  any of them: "I've yet to find a significant difference", and the FAQ answer to which is best is a
  shrug. That is a choice offered without ever being explained, which is the same as no choice, so each
  district here changes what a shift is actually for and says so on the tile.
- The **Casino District** pays 45% more and hires almost nobody, with the law watching all of it. The
  **Wino Slums** pay badly and are full of men who will take any work - the place to go for thugs, and
  the quietest street in the game. The **Nightclub District** is where hoes and the pimps who manage
  them turn up. The **Urban Ghetto** is where product changes hands, and where the law knows it.
- **Low Rent** is the neutral one and the default, at exactly the base numbers, so nothing about the
  existing balance moved and a player who never touches the picker works the shift they always did. It
  is also the one the source game's guide-writer said they preferred, which turns out to have been a
  fair call.
- The trade is the same shape everywhere: what you go home with against how much notice you drew getting
  it. A test fails if any district is better at something and worse at nothing.

### The shrine, and the names the day hands out

- Praying to the pimp gods was a slot machine in the source game: burn whatever you like, roll, maybe
  something happens. That is a lever rather than a decision, and it would sit badly in a game where the
  lookout tells you what it takes off your odds and a mule quote tells you what a run will clear. So the
  gods say what they want - a specific good, a specific number - and meeting it is answered every time.
  What stays uncertain is only which blessing lands, and even that is narrowed to the ones that would
  actually do you some good.
- The ask is worked out from the player and the week rather than stored, so it holds all week with no
  row to keep. It scales with what you are worth, bands to two significant figures so ordinary earning
  does not move it under you, and never exceeds half a storage shelf - four percent of a mid empire in
  moonshine is hundreds of bottles, and no room in the game holds a tenth of that.
- Nothing it gives back is money. The blessings are notice the law has already taken, the mood of the
  house, and whether your pimps still believe in you - none of which has a price anywhere else. Turns
  are the one thing rationed behind giving twice what was asked, because they are the only blessing
  that touches the rate the whole game runs at.
- Seven **titles** turn over daily, held by whoever leads a category. They are read out of the fights
  that happened rather than kept as counters: the source game held eight running totals and a button to
  wipe them, and a tally you can clear is not a record of anything.
- Half of them are for things done to you. Silver Tongue and Picked Clean are the same number counted
  from opposite ends, and a board of nothing but winners would only say who is winning - which the
  leaderboard already says. Being publicly the man everybody is robbing is a different fact about the
  world, and a funnier one.

### A weapon was answering only one question

- One generic weapon could tell a fight exactly one thing: is this thug armed. That made arming a crew
  a purchase rather than a decision - you bought weapons until the number matched your thugs, and there
  was nothing else to think about. There are four guns now, and they exist because a weapon does two
  jobs which come apart.
- **Any gun covers a thug.** A thug with a pistol is exactly as content as a thug with a rifle, so
  covering a big crew with cheap guns is a real strategy rather than a mistake.
- **What a gun changes is the fight.** Firepower is measured in pistols - a shotgun is 1.4, an SMG 1.9,
  a rifle 2.5 - and a crew picks up the best guns on the rack rather than a sample of them. Guns beyond
  the crew carrying them are worth nothing at all, however much they cost.
- Which turns the hideout's thug cap into the binding constraint. Buying more bodies is the efficient
  way to get stronger, right until there is nowhere to put another one: better guns are steeply worse
  value per point of firepower and are priced to be. Past the cap they are the only thing left to buy.
- Prices are the source game's - $250, $1,250, $2,500, $5,500 - and every weapon that already existed
  became a pistol. So nobody's fighting strength moved by a single point, because a pistol is worth
  exactly what the one weapon was. What moved is paper value: a rack halves, on the same asset, in the
  same proportion, for everybody.

### What the rack reaches

- The workshop makes what its level has unlocked: pistols and shotguns from the start, SMGs at level
  two, and never rifles. The one gun nobody makes in a back room is what stops the workshop from
  eventually replacing the shop. Its per-weapon cost moved onto the guns themselves, so a level buys
  throughput and reach rather than a discount on one thing.
- All four trade separately on the board and can be named in a contract, because a rack of pistols and
  a rack of rifles are not the same offer and a market that called both "weapons" would price them as
  if they were.
- Losses and storage overflow take the cheapest guns first, every time. A lost fight that destroyed
  your rifles before your pistols would make owning good ones a liability.
- A raid records the mix it left with, so losing five weapons takes the right five off the right
  shelves at home - and two raids at once cannot arm themselves from the same rifles.
- A jacking reads both halves of the guard standing in the garage. Bodies are eyes on the door; guns are
  what happens once you are seen. Six riflemen shut a garage that six pistols would only make risky, and
  because only the firepower above a pistol each counts in the second term, a pistol guard is worth
  exactly what it was before tiers existed. Guns away on a raid are not guarding anything, which is what
  makes hitting somebody mid-raid the opening it should be.
- A drive-by reads the same two halves and weights them the other way round. Finding somebody in the
  open is mostly about how many were watching the road; losing the car is mostly about what they were
  holding. The same six guards with rifles instead of pistols take the hit chance from 78% to 64% and
  the odds of losing the car from 11% to 24% - which is what stops a player with one car and a full turn
  bank grinding any rival's crew down for free.

### One attack verb was making everything you own the same thing

- With only a raid, a player's holdings are one undifferentiated pile of loot. A garage of cars, a
  hundred hoes and a shelf of supplies were all just numbers feeding the same defence roll, and no
  decision a defender made about any of them changed what an attacker would do. There are five ways to
  move on somebody now, and four of them are aimed at exactly one thing.
- A **raid** is what it always was: ten turns, one of two attack lanes, travel, rounds, and whatever the
  crew can carry home. The four **strikes** are its opposite - four to eight turns, settled in one call,
  no lane, no crew committed, and each with a different answer.
- A **drive-by** needs a low-rider. It kills thugs and takes nothing at all, and the better armed the
  street the likelier you lose the car. It is how a player who cannot yet win a raid makes one winnable.
- **Jacking** takes their rides, on odds that are almost entirely the defender's own doing: a garage
  behind a full armed crew is close to untouchable, one behind nobody is a car park with the keys in.
- **Infesting** their hoes is the only attack answered by a purchase rather than by crew or morale.
  Medicine treats who it can and the rest are gone - which is what makes a crate sitting on a shelf,
  costing money and doing nothing, worth owning.
- **Poaching** buys their hoes away with coke. This is the one that makes the payout slider a decision:
  a fully happy house cannot be poached at any price, and a squeezed one can be emptied by a rival with
  a lab. Cut product tempts fewer people, and the coke goes out whether or not anybody comes back.

### The two things the menu needed

- Low-riders are held by the building rather than the storage room. A car is not stock - it is not
  consumed, it does not spoil, and there is nowhere to put a fleet but the hideout - so the garage runs
  from two at the Trap House to fifteen at the Penthouse. That is also what stops a rich player parking
  twenty rides behind a first-tier guard and treating the jacking as somebody else's problem.
- The chop shop is the only counter in the game that buys as well as sells: $25,000 to drive one out,
  $15,000 to sell it back. Net worth counts a ride at what the shop would pay rather than what it cost,
  or buying one would be a free climb up the board.
- Medicine is priced so that covering a house is a real cost that does nothing until somebody attacks,
  and the storage room that supplies a tier's crew through a full shift holds exactly enough to treat
  that whole crew once.

### Two shields, two clocks

- A raid's protection covers everything, strikes included, because walking in behind somebody else's
  victory to finish the job is the dogpile that protection exists to stop.
- A strike sets only its own twenty-minute shield. One column for both would let either loop lock the
  other out: a four-turn drive-by would buy its victim an hour of immunity from the raid that was
  actually coming.
- Rivals use all five, each with its own appetite for a cheap shot and its own order of preference, so
  they read differently in the news: the Hard Charger shoots up streets, the Banker quietly drives off
  with cars, the Crew Builder poaches. They also restock medicine once somebody has actually been
  infesting them - a field that never bought any would make the infestation a one-way ratchet forever.

### Telling a new player anything at all

- Played fresh, the first session was five clicks. A hundred starting turns at twenty a shift is five
  street actions, after which the turn bank is empty for eight hours. In that whole session a player
  saw one verb, never met production, a lab, the market or heat, and the best purchase available to
  them - a ten thousand dollar weed lab - went unmentioned in a room they had no reason to open.
- Next Moves used to be a status readout wearing an advice label: four fixed rows that read the same
  on day one and day one hundred. It ranks real moves now, priced, with a reason, ordered so that what
  is actively costing you money comes before what would merely pay.
- A Getting Started ladder covers the verbs nothing else introduces, and hides itself when finished.
  A checklist a veteran still scrolls past is clutter; the point of it is to stop being needed.
### The wall after five clicks

- The opening bank was half the cap, which read as a courtesy and played as a wall. It is the whole
  cap now: ten shifts instead of five, enough to buy the first lab and still have turns to use it.
- Turns come back faster while you are small and taper to the normal rate by a quarter of a million
  net worth, which is just past the Warehouse. A flat rate is a wall that falls hardest on the people
  least able to take it: twelve an hour meant a new player who spent their bank waited most of a day
  to play again, at the exact moment they had least reason to come back.
- It tapers rather than switching off at a line, so nothing lurches the day you cross a threshold, and
  an established empire earns exactly what it always did.
- The strip reports the rate you actually earn at, not the base one. Paying a boosted rate while
  displaying the flat one would make the countdown quietly wrong for every new player.

### The hole in the first tier

- Measured rather than assumed, the ladder turned out to be denser than it looked: eight things a Trap
  House can buy between $10,000 and $75,000. The problem was one gap after it, $75,000 to $150,000,
  which is a session and a half of earning with nothing to save for.
- The **lookout** fills it at $100,000. Someone on the street watching for the law, cutting the odds of
  a raid landing by a quarter. It never removes the risk, or holding would be free.
- It is also the only new verb in the tier after the workshop. Everything else a first-tier player can
  buy is a bigger version of something already owned, and the lookout is the first answer to heat that
  is not selling everything and waiting.
- A test walks the whole first-tier ladder and fails if any two rungs sit more than two sessions of
  earning apart, so a later re-pricing cannot quietly reopen the hole.

### Neither panel is stored

- Neither is stored. A lab built is a lab on the ladder, and a sale in the log is a sale: asking the
  world what happened cannot drift out of step with it the way a checklist column would.

## What changed in 0.2.4

Territory, player-to-player markets, mule runs, heat, travel between towns as timed flights, and
rivals who play in sittings rather than on a metronome. Organizations did not make it and carry
forward.

### Territory

- Every town has its own map of six pieces, and the territory page shows yours and nowhere else.
  Ground is contested inside a town: the other cities exist and rivals hold ground in them, but they
  are not yours to fight over.
- Each town carries all four types, so nowhere is starved of an effect, and the town list is derived
  from the map rather than kept beside it, so a city with no ground could never be offered.
- Players pick their town when they sign up. Registration used to ignore the field entirely, so every
  player defaulted to New York whatever they chose, which would have put everybody on one map and left
  the other four empty.
- You pick a pimp to run each piece of ground. Standing there is a posting, not a visit: they are away
  for as long as it lasts, so they do not sharpen the house, do not lift street income, cannot command
  a raid, and cannot run a second piece. Only an **Enforcer** helps hold ground, capped separately from
  the house bonus because the same percentage over a five thug garrison is worth far less than over a
  full roster. A beaten pimp does not stay on to run the ground for whoever took it.
- Holding ground takes thugs standing on it, and they are not at home while they do. That is the whole
  design in one line: attack, defend, or occupy, pick two. The hideout tier caps how many pieces you
  can run at once, from one to four.
- Each piece is a **Corner** (+15% street income), **Docks** (+20% production yield), **Club** (+50%
  passive morale recovery), or **Stash House** (+20% haul from raids). Every one is a percentage on an
  activity you still spend turns on, so ground amplifies play rather than paying out on its own. The
  labs already fill the idle-income role and needed two separate bounds to stay sane.
- Empty ground is claimed with turns and a garrison. Held ground takes a raid, which uses one of your
  two attack lanes, so taking ground competes with robbing a house.
- A raid for ground fights the garrison standing on it, not everyone back at the holder's house.
  Fighting the whole house would make ground untakeable: the garrison is a handful of thugs and the
  house is the rest of the roster.
- Anti-farm's wealth rules do not apply to ground, and a raid for it grants no house protection.
  Taking a corner is not robbing anyone, and gating it by wealth would let a weak player park on good
  ground permanently. Ground carries its own settling period after changing hands instead.

- AI rivals contest the map. Ground is checked before a house raid because both use an attack lane,
  and a rival that always robbed houses would never take any. They claim what is open before fighting
  for what is not, and only raid a garrison they should beat.
- Ground reaching the player: losing it, and holding it. A raid you beat off still costs the garrison,
  so it earns its own line rather than leaving a garrison that quietly shrank with no explanation.
  Both appear in the arrival summary and the alert bell, alongside the raider's own row in world news,
  and world news carries a headline for whoever runs the most ground.
- Losing ground reaches the player: a line in the arrival summary, an entry in the alert bell, and the
  raider's own row in world news. The loser's notice is written to them in the second person and is
  deliberately kept out of the public feed, where the raider's row already reports the same event.

### The market

- Players sell to each other on one global board. Stock leaves storage the moment it is listed and
  comes back if the listing is pulled: escrow rather than a promise, or the same fifty weapons could be
  listed twice or spent after somebody had bought them.
- Partial fills, because a listing only one player in the game can afford is a listing nobody buys. The
  house takes a cut of each sale, which is a money sink the game otherwise lacked.
- The seller is paid into the bank rather than cash on hand, which is capped by their safe and
  stealable. A sale that overflowed into nothing while they were offline would be a hole.
- Pulling a listing into a full room leaves the stock on the board rather than destroying it.
- A **workshop** makes weapons from turns and materials, under the store's price. This is what gives
  the board anything worth trading: every other good has a fixed price on both sides, so the tradeable
  spread over them is zero. Weapons are the one thing a player can make that everybody needs.

### Moonshine and cut

- Both the still and the mix house need the **Warehouse** or better. A Trap House is not somewhere you
  hide a still, and gating them there keeps the first tier about learning the game rather than running
  a lab. The gate is checked when making as well as when building, since buying is not the only way to
  end up with a station.
- A **still** brews moonshine, which thugs drink exactly like the shop's beer at well under the shop's
  price. The bought beer is always poured first, so a player is never quietly spending contraband while
  a legal barrel sits next to it.
- A **mix house** makes cut, which is worth nothing on its own and worth whatever the coke it becomes
  is worth. That is why it is priced off the local coke rather than carrying a band of its own: it
  follows the town automatically, because what it is worth is what it makes.
- **Stepping on it** spends that cut: one unit of cut makes one unit of coke, on any coke you hold.
  It used to be a silent bonus inside coke production, which was wrong twice over. A player saving cut
  for a batch watched it disappear into a production run they had not connected it to, and cut could
  never reach coke that arrived any other way - off a plane, off the board, out of a lab overnight,
  which is most of the coke worth stretching.
- What it costs is **strength**. Coke carries a purity, and cutting is a weighted average: the pile
  grows and weakens together. Without that the mix house was simply a cheaper, faster coke lab - a
  unit of filler became a unit of product at full price, about $300 a turn against $220 for producing
  the real thing, with no ceiling on it at all.
- The price follows the square root of purity, which is the only shape that works. Fall proportionally
  and a stretch gains you nothing, so nobody would ever do it. Put a floor under it and total value
  climbs with unit count forever, which is the printer wearing a different hat. A square root means a
  stretch pays, the next one pays less, and eventually the cut costs more than it makes.
- Purity is a property of the pile, so everything that adds coke blends into it: produced, found,
  stolen, bought, flown in, or stretched. Market listings carry the purity they were escrowed at, or
  the board would launder weak product into clean.
- What it also costs is turns, room, and notice. The mix house level sets how fast a batch goes, giving that
  room a second reason to exist, and a stretched pile is a hotter pile: coke draws more heat per unit
  than anything else you can hold.
- Moonshine is priced against the shop's beer, which is the same everywhere, so it does not move with
  the town the way weed, coke and cut do.
- AI rivals trade. They buy weapons off the board when it beats the shop, judged against the shop
  rather than against the other listings, and list what they cannot use.

### Heat

- Everything in this game is illegal, so an illegal flag on one room and not another said nothing.
  What differs is how much notice a thing draws, and that is what heat measures. Coke draws the most
  per unit, then moonshine, then weed, and cut the least, which is the reverse of where it is made.
- Working the streets adds heat by itself, with nothing held at all. The core loop is a crime; a system
  that only watched the stash would have said otherwise.
- Under the floor nobody is looking, however long you sit there. That floor is what makes a small
  operation safe and stops the game punishing a player for existing. Above it, every hour is a roll,
  and the chance climbs with the heat.
- Getting caught takes half of every pile and fines you, and the fine stops at cash on hand: a raid
  that could put a player into debt would be a nastier mechanic than losing the stash. It clears the
  heat that drew it, so one raid does not guarantee the next.
- Heat cools on its own, which is what makes lying low a real move rather than a figure of speech.
- Both halves are sized against the game's own scales rather than against nothing, which is what the
  first tuning got wrong: street work is measured against the turn bank, so spending the whole thing
  is a night's worth of notice that fades by morning, and the per-unit weights are measured against
  the storage rooms, so a full store is worth watching rather than an automatic death sentence. Under
  the first numbers an ordinary evening of work by someone holding nothing reached Hunted, and so did
  simply filling the room you had just paid for.
- It runs on its own clock rather than the turn clock. The turn clock is dragged forward every few
  minutes by anyone at the screen, so a player checking in often would never accumulate a whole hour
  and would never be raided or cool down. Whole hours are consumed and the remainder is left behind.
  A long absence is capped the same way offline lab work is, so a fortnight away costs no more than a
  night.
- Heat reads Quiet, Noticed, Watched or Hunted, because the number alone says nothing about whether
  tonight is the night. It lives in the status strip rather than on the hideout page: it is a live
  risk that changes what a player should do next, so it belongs with the numbers carried between
  pages. The strip scrolls on a narrow screen, so heat is placed ahead of rank and city, which are
  read rather than acted on.

### Mule runs

- Sending crew to another town to buy cheap and carry it home. It is the first thing in the game that
  costs crew and time rather than turns, which is what makes it a different decision rather than a
  cheaper version of one you already had.
- Travelling yourself pays the distance in turns each way and leaves you standing in the wrong town.
  A run costs a fraction of that in turns, but it takes real time, locks up a pimp and hoes who earn
  nothing while they are gone, and is paid for before anybody leaves: fares both ways and their keep
  for the trip. Neither option dominates, which is the point.
- The intelligence centre is the gate. Unlike every other room it makes nothing: it buys how many
  runs can be in the air at once and how much of a route is known before anybody goes. Without one
  there are no runs at all, which is what stops mule running from being free the moment you can
  afford a spare pimp.
- Mules buy at the destination's price, since that is the entire reason to send them. The load is
  capped by the hoes sent, so how greedy to be is the dial the player actually turns.
- Three ways it ends. Delivered brings the load and the unspent cash home. Seized takes a share of
  the load and all the cash they were still carrying, and the heat lands on you, because crew who are
  caught talk. Defected is a pimp far from home with your money deciding not to come back, which is
  what makes who you send a real question rather than picking whoever is spare.
- Runs settle on the clock rather than on a request, so they land whether or not you are watching,
  and find you through the alert bell and the catch-up digest.
- What will not fit in the storage room is dumped, and the notice says so. A run that quietly dropped
  a third of a load already paid for would read as the price being wrong rather than the room.

- The page quotes profit, not gross. The spread alone never decided a run, because the fares are paid
  whether or not it pays: the first tuning had a head costing more to fly than a hoe could carry
  margin for, so every route in the game lost money and the mechanic was dead on arrival. Short hops
  are now thin, bad routes are clearly negative, and a long run into a wide spread is worth the risk.
- Cash beyond what the crew can carry goods for is dead weight, and the page says so. It comes home
  untouched on a clean run and is taken on a bad one, so sending a fat purse is pure exposure.

- Rivals run mules too, or this would be a player-only edge and the leaderboard would stop meaning
  anything. They build the room themselves and judge a route on what it clears after fares. Because
  rivals live in different towns, they find different routes without being told to.
- A rival sends neither its whole roster nor its whole purse, buys only what it has room to store,
  and subtracts what is already in the air. The first pass did none of that: one rival spent thousands
  on coke and stored none of it, because the shelf was already full when the plane landed.

### Flights take time

- A town's distance is now time as well as turns, for the player as much as for a mule. Travel used
  to be instant, which made distance a pure turn cost: you were somewhere else the moment you decided
  to be.
- While the flight is running you are in the air and cannot act. That check lives in the services
  rather than at each endpoint: there are two dozen ways to act and only one set of places where
  acting happens, and a guard the endpoints have to remember is one that will eventually be forgotten.

### Rivals with a history

- They already fought each other. What they lacked was memory: a rival picked whoever was richest
  every single time and forgot being robbed the moment it happened, so nothing between two of them
  ever turned into a story and the world read as weather rather than as people.
- A grudge is read from the fights that actually happened, not kept as a score, so it is exactly as
  old as the last punch and there is nothing to prune. It also never makes a rival reckless: the win
  margin and the anti-farm rules still decide what they will take on, and a grudge only settles which
  of the fights they were already willing to have they actually pick.
- How hard it lands is character. The Hard Charger weights a score at nearly the target's own worth
  and carries it for three days; the Banker treats a robbery as a cost of doing business and has
  forgotten by the morning.
- World news carries the loudest quarrel, mutual ahead of one-sided, so it is something you can watch
  rather than something happening out of sight.

### How the rivals play

- A rival used to take one action every twenty-odd minutes, evenly, forever. Nothing about that is
  what a player does, and a world of them read as machinery rather than as opponents.
- Rivals now play in sittings. Between them they are gone, and turns bank up exactly as a real
  player's do; then they sit down and spend the lot. A sitting ends when the bank runs dry, with an
  action count and a wall clock only as backstops, because running out of turns is what really ends
  an evening.
- Each rival keeps its own hours, so the world has a rush hour and a quiet stretch. A fifth of them
  keep no hours at all: without those the board is dead for anyone who plays at an odd time, and the
  point of rivals is that the world moves whether you are there or not.
- How often a rival plays is read off its personality rather than a separate dial, using the same
  pacing the personalities already carried. Eager ones sit down five or six times a day, patient ones
  two or three.
- Rivals hesitate. A quarter of the beats in a sitting pass with nothing happening, which is reading
  the screen and changing your mind, and it is the difference between a person and a loop.
- Because rivals now sleep, idle time stopped being a health signal on its own: one quiet for four
  hours is asleep, not stuck. The admin's rivals table shows each one's habits and whether it is
  playing right now, which is what actually distinguishes the two.

### Carried out of 0.2.4

- Organizations. Everything else planned for it shipped.

## What changed in 0.2.3

0.2.3 gives the hideout somewhere to go, makes the labs work while you are logged out, and turns the
activity log into something worth reading.

### Hideout tiers

- Three tiers above the Trap House: the **Warehouse**, the **Nightclub**, and the **Penthouse**,
  ending at 22 pimps, 200 hoes, and 110 thugs. Each name matches what the tier buys you: a warehouse
  holds more, a club employs more, a penthouse puts you out of reach.
- A tier is paid for in cash and turns up front and then takes time to build, from 30 minutes to six
  hours. The old caps hold until it lands, so nobody buys a bigger crew mid-fight.
- Every upgrade is paid from the bank first, then cash on hand. The safe is one of the things being
  bought, so charging cash on hand would cap what a player can spend at the safe they already own, and
  several upgrades cost more than the safe one level below them holds. A rule test now walks the whole
  ladder and buys every tier and room level in order, so no level can be stranded again.
- Storage rooms, safes, and labs now run deeper than the Trap House can hold. Each one names the tier
  it needs, and each storage level holds exactly what a full-length street action consumes at the crew
  caps of the tier that unlocks it. A rule test pins that relationship down.
- AI rivals grow their base the same way. Without this they sat at the Trap House forever: rich enough
  to be worth raiding, capped too low to fight back, and eventually walled off by the anti-farm ratio,
  which would have left a maxed player with nobody to attack.

### Labs that work while you are away

- Weed and coke labs now produce on their own, between 1 and 16 units an hour depending on the lab and
  its level, on top of the bonus they give production turns.
- Output stops at the storage room rather than spilling, so time away can never destroy stock you
  already had, and it stops again after 12 hours, so the hideout is a reason to come back rather than a
  reason to stay gone.
- What the labs made while you were out is written into your activity, so it is still there whichever
  page you open first.

### Morale trend

- Hoe and thug morale carry an arrow showing which way they are moving, with the exact figure on hover.
- The baseline is the morale going into your most recent action, recorded before the action rather than
  after. Taken after, a row already contains the damage its own action did, so a player who crashed
  morale in one shift and looked straight away would be told it was steady. Measured across a fixed
  window instead, the arrow kept reporting a crash for hours after it was over, pointing down while
  morale climbed.
- With nothing recent to compare against the arrow is absent rather than flat. A steady arrow on a
  player who has not acted in hours would be a claim the server cannot support.

### While you were away

- Arriving raises a summary of what happened in your absence: who attacked and what they took, what
  the labs made, any building that finished, whether your turn meter has filled up and stopped, and
  whether you are still under protection.
- It appears once and only when something actually happened. A popup reporting that the world stood
  still is an interruption with nothing behind it.
- It also reports where you finished up: the rank you moved to, and who changed places with you in
  either direction.
- Rank is a comparison, so answering "who moved ahead of me" needs everyone's position at the same past
  instant. Standings are sampled for all players every 15 minutes, and the digest compares the sample
  nearest your last visit against the newest one. With no sample covering the absence it says nothing
  rather than guessing.
- Its read position is separate from the alert bell's. Reading the bell should not swallow the summary
  of an absence, and seeing that summary should not mark every attack as read.

### World news worth reading

- The feed was every action anyone took. With rivals acting on a timer that meant thirty rows of
  somebody buying condoms, and the one attack that mattered fell off the page within a minute.
- Fights, buildings, and arrivals are news whatever their size. Everything else has to move real money
  or real crew. Money is judged on cash and bank together, so moving your own money between two pockets
  no longer reads as a story.
- Above the feed are the standing facts: who runs the city, the biggest take of the last two days, the
  best single score, and anyone who just arrived.

## What changed in 0.2.2

0.2.2 gives the operation a base, gives the crew names, gives the game a real admin panel, and makes
combat something you can lose as well as win.

### Hideout

- Every player has a hideout. The tier caps crew, a storage room caps goods, and a safe caps cash on hand.
- The Trap House holds **6 pimps, 50 hoes, and 25 thugs**.
- The storage room upgrades through three levels, and level 3 is what makes a full-length street action supplyable at the crew caps.
- The safe upgrades from $50,000 to $100,000 of cash on hand. Bank cash stays uncapped and stays safe from theft.
- Weed and coke labs are turn-fed: they raise what each production turn yields rather than producing on their own.
- Earned income over the safe is swept into the bank, and goods over storage spill. Deliberate purchases are refused up front instead, so you never lose something you paid for.
- Stock a player already held is never taken away, so saves from before the caps drain down through upkeep instead of being confiscated.

### Named pimps

- Pimps are tracked individually with a name, a specialty, loyalty, and a record of missions led. Hoes and thugs stay as counts; they churn too fast to be worth naming.
- **Enforcers** sharpen the attack they command and the defence of the house while they are home. **Hustlers** lift street income while they are home. The two never apply at once.
- Exactly one pimp commands each attack, chosen by the player or fielded by the server.
- A pimp can die commanding a defeat, die defending a broken house, or walk out when loyalty bottoms out. Never the last one.

### Combat and economy

- Attack cooldowns are per lane: two lanes, each held for the cooldown window, so two attacks can run at once. Cancelling a mission refunds its lane.
- Street work can auto-buy the upkeep an action needs, bounded by both storage room and cash.
- Ranking moved into the database. The dashboard, leaderboard, targets, profiles, and admin overview no longer load every player to sort them.
- Combat polling dropped from 26 queries over 6 requests per tick to 9 over 2.

### Admin panel

- Search any player, open their full detail, and adjust any resource up or down. Every action is recorded in an audit trail with the actor, the target, before and after values, and a reason.
- Ban, suspend with an expiry, lift, force-logout, rename, and grant or revoke admin. All reversible, and a ban ends live sessions rather than waiting for the cookie to lapse.
- Oversight shows wealth distribution rather than bare totals, the fastest movers, every in-flight mission with stuck ones flagged, and AI idle times.
- Maintenance mode blocks gameplay while leaving reads and admin access open. Announcements post a site-wide banner.
- 161 scalar tuning values are editable at runtime without a restart, layered over `appsettings.json` and reversible to it.
- The panel is split into tabs: Overview, Players, AI Rivals, Tuning, Live Ops, and Audit. The old
  Admin Control Center held whatever had no other home, so its headline totals, its read-only economy
  dump, and its AI controls now sit with the things they belong to.
- The AI tab owns the rivals: seeding, running a batch by hand, the automatic loop, and a roster
  showing each rival's personality, net worth, and idle time. Rivals can be paused individually, which
  keeps them in the world as a fixed target while everyone else moves, or told to act immediately,
  which ignores the cooldown that paces the loop.
- A rival can also be directed: pick the action yourself rather than letting its brain choose. Work the
  streets, produce, buy, sell, hire, fire, bank, recover morale, upgrade a room, or attack a named
  target including yourself, which is the quickest way to put a fight in front of you. Every action
  runs through the same services a player's would, so the rules still apply and a refusal is the game
  refusing rather than a special admin path that behaves differently. Automatic AI is saved rather than held in
  memory, so it survives a restart, and its tick and rounds are editable without one.

### Combat refinement

- **Anti-farm protections.** A player under $25,000 net worth cannot be attacked at all, and nobody may
  hit a target worth less than a fifth of their own. Repeat victories against the same defender inside a
  day decay the haul 40% each time, down to a tenth, so farming becomes pointless rather than forbidden.
  Protection widens with every hit a defender has already taken, and at most two attacks may be in
  flight against one player at once.
- **AI rivals attack.** Bots pick the richest target they should still beat, skipping anyone protected,
  mismatched, or already swarmed. Aggression follows personality: Hard Chargers raid readily on thin
  odds, Bankers rarely and only with a clear edge.
- **Defender alerts.** A bell shows how many attacks you have not read, with each one written from the
  defender's side: what was taken, which crew died, and whether you held the house.
- **Combat balance.** Strength is one configurable formula instead of four hardcoded copies. Defence
  used to earn 24 per armed thug against attack's 20 and counted morale twice as heavily, which meant
  beating a fully built house needed 34 armed thugs when the crew cap is 25: it was unbeatable by
  arithmetic. An attacker now needs roughly 10-20% more armed crew, and cracking a maxed defender takes
  a top Enforcer commanding or catching their crew away.

## What changed in 0.2.1

0.2.1 turns attacks into live combat missions.

- Attacks now use assigned pimps, thugs, and weapons instead of always sending the whole crew.
- Each active mission requires at least one pimp in control.
- Up to two attack missions can run at once when enough free pimps and crew are available.
- Combat missions travel, fight round by round, and return home.
- The Combat page shows target scouting, launch controls, live mission status, morale, rounds, remaining attackers, and recent combat events.
- Crews committed to attacks are unavailable at home, so players can be attacked while their defense is weakened.
- Completed missions still write final combat logs and world activity.

## What changed in 0.2.0

0.2.0 turns combat on.

- Players can dispatch attacks against inspected targets from Target Recon.
- Attacks spend turns immediately, then resolve after a short server-side travel timer.
- Players cannot work the streets or dispatch another attack while an outgoing attack is pending.
- Attacks respect attacker cooldowns and give defenders a protection window.
- Combat compares attack and defense power from thugs, weapons, pimps, and morale.
- Victories can steal cash on hand, weed, and coke, while bank cash remains protected.
- Combat can cause crew and weapon losses for both sides.
- Attack dispatches and final results write world activity and detailed combat logs.
- The browser includes attack controls in Recon and a Combat History panel in World.

## What changed in 0.1.12

0.1.12 prepares the database and API surface for the 0.2.0 combat layer.

- Players now have combat protection and attack timestamp fields.
- The database has a `CombatLogs` table for future attack outcomes, theft, losses, power checks, and protection windows.
- Dashboard and Target Recon responses include read-only combat status.
- The browser shows protection/eligibility hints in Overview and Target Recon.
- `/api/game/combat/logs` is ready to return a player's combat history once attacks start writing records.

## What changed in 0.1.11

0.1.11 prepares the browser and API for the 0.2.0 combat layer.

- Players can search future combat targets by name or city.
- Public player profiles expose rank, net worth, visible resources, crew, weapon coverage, morale, and recent public activity.
- The browser includes a Target Recon panel with combat-readiness hints.
- AI rivals now receive a stable random brain, such as Resource Manager, Big Spender, Hard Charger, Product Runner, Crew Builder, Banker, or Balanced Operator.
- The browser UI now uses a full app shell with separate Overview, Street, Crew, Market, Recon, World, and Admin pages.

## What changed in 0.1.10

0.1.10 teaches AI rivals to manage crew morale.

- AI rivals raise hoe cut when hoe morale gets low and ease it back down when morale recovers.
- AI rivals prioritize pimps, condoms, beer, and weapons when crew morale or coverage is strained.
- AI rivals pause crew expansion and street work when morale needs recovery.

## What changed in 0.1.9

0.1.9 adds live admin control for automatic AI.

- The Admin Control Center shows whether automatic AI is on or off.
- Admins can turn automatic AI on or off without editing configuration.
- Automatic AI starts disabled by default through `Bots:Enabled`.

## What changed in 0.1.8

0.1.8 makes AI rivals progress automatically.

- The API hosts an automatic bot service that wakes on a configurable interval.
- Automatic ticks run one AI round and let per-bot cooldowns decide who is actually due to act.
- `Bots:Enabled`, `Bots:TickSeconds`, and `Bots:RoundsPerTick` control the automation.

## What changed in 0.1.7

0.1.7 gives AI rivals a progression loop instead of leaving them as static seeded accounts.

- Admins can call `/api/admin/bots/run` to advance AI rivals through economy rounds.
- AI rivals use the same server-side economy rules as players for store buys, hiring, street work, production, product sales, and banking.
- AI rivals pace themselves by keeping turns in reserve and running at most one small turn-spending action per round.
- AI rival activity uses real action timestamps in World News while per-bot cooldowns decide whether automatic bots are due to act.
- The browser Admin Control Center includes controls for running 1, 3, or 10 AI rounds.

## What changed in 0.1.6

0.1.6 adds AI rivals so 0.2.0 combat can be tested against populated leaderboards.

- Accounts now have a persistent AI-player flag.
- Bot accounts are disabled for login and counted separately in the admin overview.
- Admins can call `/api/admin/bots/seed` to create up to 15 seeded rivals with varied cities, cash, crews, morale, inventory, and turns.
- The browser Admin Control Center includes AI rival seeding controls.
- Development databases drop legacy 0.1.0 economy columns, including old happiness fields, after their values are copied into the current schema.

## What changed in 0.1.5

0.1.5 starts the world-activity layer.

- Players can call `/api/world/news` for a recent global activity feed.
- World News is built from server-side action logs and excludes admin cheat logs and store purchases.
- The browser includes a World News panel in the right rail.

## What changed in 0.1.4

0.1.4 starts the game-administration layer for economy oversight.

- Accounts now have a persistent admin flag.
- The first registered account becomes an admin automatically.
- Existing development databases promote the oldest account to admin through the 0.1.4 migration.
- Admins can call `/api/admin/overview` for account/player totals, cash totals, net-worth totals, morale averages, and the active economy configuration.
- Admins can call `/api/admin/cheats` for audited balance-testing grants.
- The browser shows an Admin Control Center panel only to admin accounts, including quick cheats for cash, turns, crew, inventory, product, and morale.

## What changed in 0.1.3

0.1.3 adds direct crew-management depth on top of the 0.1.2 tuning foundation.

- Players can hire or fire pimps, hoes, and thugs directly.
- Hire costs, morale hiring requirements, firing morale penalties, and max crew transaction size are configurable through `Game:Crew`.
- Hoes and thugs now require minimum morale before more can be hired.
- Firing crew applies configurable morale pressure.
- The dashboard reports management capacity, armed-thug coverage, max-action supply needs, and projected supply reserve cost.
- The browser includes a new Crew Management panel with hire/fire controls.

## What changed in 0.1.2

0.1.2 turns the 0.1.1 economy into a tunable foundation for balance work.

- Street income ranges, recruit odds, found-item tables, production costs, production yields, action turn limits, and morale pressures are now configurable through `Game` options.
- Action responses now include structured server-calculated breakdowns in addition to human-readable summaries.
- The browser uses the server-provided action turn limit and shows compact action breakdown metrics after resolved actions.
- Malformed auth/product/store inputs now fail with rule errors instead of null-reference crashes.
- The initial EF migration now creates the schema required by a fresh PostgreSQL database.
- A lightweight backend rule-check runner covers net worth, turn refresh, street action math, production math, and invalid product handling.

## What changed in 0.1.1

0.1.1 replaces the generic 0.1.0 `Workers / Enforcers / Supplies` model with the first real Street Empire economy.

### Crew

- **Pimps** manage the operation. One pimp currently supports up to 10 hoes without a morale penalty.
- **Hoes** generate the majority of street gross income.
- **Thugs** form the security crew and will later become the base of PvP attack/defense.
- Hoe and thug morale are tracked independently.
- Low morale can cause crew members to leave.
- Players can set the **hoe payout cut from 10% to 80%**.
- Higher cuts help hoe morale but reduce the player's share of street gross.

### Crew upkeep

- Hoes consume **condoms** while working the streets.
- Thugs consume **beer** while working the streets.
- Weapons are permanent inventory and provide **one weapon of coverage per thug**.
- Too many hoes for the current pimp management capacity lowers hoe morale.
- Too few weapons for the current thug count lowers thug morale.

### Money

- Money is now split into **cash on hand** and **bank cash**.
- Store purchases and production materials use cash on hand.
- Players can deposit or withdraw money without spending turns.
- Both cash pools count toward net worth.
- Banked cash establishes the protected-money foundation for the future PvP build.

### Product economy

Players can now produce:

- **Weed** — $25 production cost per turn, 3-6 units per turn, $40 fixed sell price in 0.1.1.
- **Coke** — $80 production cost per turn, 1-3 units per turn, $150 fixed sell price in 0.1.1.

Production spends turns and cash on hand. Product can then be sold back into cash on hand. Dynamic city/black-market pricing is intentionally reserved for later versions.

### Street store

The old generic supply purchase endpoint has been replaced by a reusable store catalog:

| Item | Price | Purpose |
|---|---:|---|
| Condoms | $10 | Hoe upkeep |
| Beer | $15 | Thug upkeep |
| Weapons | $500 | Permanent thug weapon coverage |

### Street action

Working the streets for 1-20 turns can now:

- Generate gross income from the current hoe/pimp crew.
- Pay the configured hoe cut before the player receives profit.
- Recruit pimps, hoes, and thugs.
- Find condoms, beer, weed, and coke.
- Consume crew upkeep.
- Apply management-capacity and weapon-coverage pressure.
- Raise or lower separate hoe/thug morale.
- Cause desertion when morale becomes dangerously low.

## Existing core systems

- Account registration/login with server-side password hashing.
- Unique player names.
- New York starting city.
- Lazy turn regeneration at **+2 turns every 10 minutes**.
- **200-turn cap**.
- Global top-50 net-worth leaderboard.
- Server-authoritative economy rules.
- Per-player economy/action history.
- Responsive React browser UI.

Travel arrived in 0.2.4, as timed flights you cannot act from.

## Stack

- ASP.NET Core / .NET 10
- Entity Framework Core 10
- PostgreSQL + Npgsql 10
- React + TypeScript + Vite

## Requirements

- .NET 10 SDK
- Node.js 22.12+ (Node.js 24 LTS is also fine)
- Docker Desktop (recommended) or a local PostgreSQL server

## Run locally

### Quick start on Windows

After PostgreSQL is running and client dependencies are installed, launch both dev servers from the repository root:

```powershell
.\start-dev.bat
```

The script opens the API and Vite client in separate command windows.

### 1. Start PostgreSQL

From the repository root:

```powershell
docker compose up -d
```

### 2. Apply the database migration

For a brand-new database:

```powershell
cd Server\StreetEmpire.Api
dotnet tool install --global dotnet-ef
dotnet ef database update
```

Because the 0.1.x player schema is still early-development, deleting the development database and applying the committed `InitialCreate` migration is the simplest path if there is no save data you care about.

### 3. Run the API

```powershell
dotnet run --urls http://localhost:5080
```

Health check:

```text
http://localhost:5080/api/health
```

It should report version `0.2.5`.

### 4. Run the browser client

Open another terminal:

```powershell
cd Client
npm install
npm run dev
```

Open:

```text
http://localhost:5173
```

## Starting balance

| Resource | Starting value |
|---|---:|
| Cash on hand | $5,000 |
| Bank cash | $0 |
| Turns | 100 / 200 |
| Pimps | 1 |
| Hoes | 3 |
| Thugs | 1 |
| Condoms | 17 |
| Beer | 10 |
| Weapons | 1 |
| Hoe payout | 30% |
| Hoe morale | 100% |
| Thug morale | 100% |

## Net worth formula

```text
Net Worth = Cash on Hand
          + Bank Cash
          + Pimps × $1,000
          + Hoes × $550
          + Thugs × $1,250
          + Condoms × $10
          + Beer × $15
          + Weapons × $500
          + Weed × $30
          + Coke × $120
```

The product net-worth value is deliberately below its fixed sale value so inventory does not inflate ranking exactly like liquid cash.

## Economy tuning

The server remains authoritative, and 0.2.4 keeps the core tuning numbers in `Server\StreetEmpire.Api\appsettings.json` under `Game`.

The configurable tables now include:

- `MaxActionTurns`
- `StreetAction` gross ranges, recruit chances, and found-item tables
- `Production` product costs and unit ranges
- `Morale` upkeep rates, management capacity, pressure penalties, and desertion thresholds
- `Crew` hire costs, morale hire requirements, and firing penalties
- `Combat` turn costs, travel timers, cooldowns, defender protection, power randomness, loot rates, and loss rates
- `AntiFarm` net worth floor and ratio, loot decay, protection escalation, and the incoming attack cap
- `Hideout` tiers, storage rooms, safes, labs, and the offline production ceiling
- `WorldNews` feed size, window, and the money and crew thresholds that make an action newsworthy

## Verification

```powershell
dotnet build StreetEmpire.sln
dotnet run --project Tests\StreetEmpire.Tests\StreetEmpire.Tests.csproj
cd Client
npm run build
```

## API surface

```text
GET  /api/game/dashboard
POST /api/game/street
POST /api/game/production
POST /api/game/product/sell
GET  /api/game/store
POST /api/game/store/buy
POST /api/game/bank/deposit
POST /api/game/bank/withdraw
PUT  /api/game/crew/settings
POST /api/game/crew/hire
POST /api/game/crew/fire
POST /api/game/hideout/upgrade
POST /api/game/hideout/recover
GET  /api/world/news
GET  /api/game/leaderboard
GET  /api/game/targets
GET  /api/game/players/{playerId}/profile
GET  /api/game/alerts
POST /api/game/alerts/seen
GET  /api/game/combat/logs
GET  /api/game/combat/missions
POST /api/game/combat/attack
POST /api/game/combat/missions/{missionId}/cancel
GET  /api/admin/overview
GET  /api/admin/oversight
GET  /api/admin/players
GET  /api/admin/players/{playerId}
POST /api/admin/players/{playerId}/adjust
POST /api/admin/players/{playerId}/enforcement
GET  /api/admin/audit
GET  /api/admin/config
PUT  /api/admin/config
PUT  /api/admin/live-ops
POST /api/admin/bots/seed
POST /api/admin/bots/run
```

`POST /api/game/scout` is retained as a temporary compatibility alias for the new street action.

## Important server rule

The browser never determines money earned, product produced, recruiting results, morale, desertion, turn costs, prices, or net worth. The client submits the player's intended action; the ASP.NET API validates and resolves it.

That rule becomes especially important once PvP and a player market are introduced.

## Proposed 0.1.x path

- **0.1.2 - Done:** economy tuning, configurable tables, stronger balance controls, and better action breakdowns.
- **0.1.3 - Done:** hiring/firing controls, deeper happiness requirements, and crew expense reporting.
- **0.1.4 - Done:** admin identity, admin-only economy overview, browser admin control center, and audited admin cheats.
- **0.1.5 - Done:** global action-log news feed and browser World News panel.
- **0.1.6 - Done:** seeded AI rivals for pre-combat leaderboard and 0.2.0 testing.
- **0.1.7 - Done:** AI rival progression rounds using the player economy.
- **0.1.8 - Done:** automatic AI rival progression with staggered per-bot cooldowns.
- **0.1.9 - Done:** admin runtime toggle for automatic AI.
- **0.1.10 - Done:** AI crew-morale management.
- **0.1.11 - Done:** target recon and public player profiles for combat prep.
- **0.1.12 - Done:** combat schema, protection status, and combat log contracts.
- **0.2.0 - Done:** player search, attack/defense strength, combat, theft, losses, protection windows, and attack logs.
- **0.2.1 - Done:** live combat missions, assigned crew, round events, combined Combat page, and committed-crew vulnerability.
- **0.2.2 - Done:** hideout capacity, named pimps, the admin panel, database-side ranking, anti-farm protections, AI attack behavior, defender alerts, and a combat balance pass.
- **0.2.3 - Done:** hideout tiers beyond the Trap House, passive lab production, and a curated world news feed.
- **0.2.4 - In progress:** player-to-player markets, organizations, and territory.
