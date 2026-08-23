# Changelog

## 0.2.5 (in progress)

### Fixed
- **Pistols are on the street's quick-buy, where a row for guns had silently gone missing.** The supplies
  panel asked the counter for a key called "weapons", which stopped existing the day guns split into
  tiers, and the filter that guards against a missing key dropped the row without a word. It has been
  two rows ever since. The row buys pistols now - the cheapest thing that covers a thug - and the panel
  says so in the console rather than quietly shrinking if a key ever goes missing again.
- **Moonshine and cut counted for nothing.** Both take a shelf, cost money and turns to make, and draw
  heat, and neither moved your standing by a penny - so brewing a still-full of moonshine put you down
  the board by whatever the materials cost. The same trap the hideout was in, in a different currency.
  They are priced off what the game already says they are worth when it prices a contract: moonshine
  stands in for beer, and cut is a quarter of coke.
- A test now walks every good a player can hold and insists that holding some of it is worth something,
  so the next good added is covered the day it is added rather than whenever somebody notices their net
  worth going the wrong way.

- **The client copy went through line by line too**, all 355 strings of it. Most of it was already in
  the game's voice; seventeen were not. The offenders were accountancy and systems register in the
  places that explain the game to a newcomer - "Your hoes generate gross income", "Your hideout sets
  every hard limit you operate under", "Turn cash-on-hand into inventory", "Over capacity" - which is
  exactly the copy a new player reads first and the last place the game should sound like a ledger.
- The game was also speaking two dialects on the same screen: the server said defence and Intelligence
  Centre while the client said Defense, defenses, Command center and stabilize. It is one dialect now.
- Two lab descriptions said "Raises weed production turns", which describes an implementation rather
  than a thing that happens, and the tier button said "Not enough turns" where it could say how many.

- **Poison could not actually be bought.** It went on sale with no case behind it in either of the two
  switches a purchase passes through: the first refused with "Store item is not implemented", a
  developer's note shown to a player, and once that was fixed the second quietly filed the doses on the
  weapon rack, because its default arm assumed anything it did not recognise was a gun. Both are fixed,
  the default now refuses rather than guessing, and a test walks the whole shop buying one of each so
  the counter and the shelf cannot disagree again.
- **The refusals sound like the game now.** Twenty-seven of the hundred and seventy-five things the game
  says when it turns a player down were written in form-validation register - "Quantity must be between
  1 and 10,000", "You do not have enough coke" - while the rest of the game was saying things like
  "Nothing parked there to take" and "Pick on someone your own size". The rule ones became imperatives
  and the shortage ones now name the number the player actually has, which the game already did when it
  refused a poach: "You only hold 12 coke."

- The walkthrough put its card above or below whatever it was pointing at, measured against a guess of
  200px for its own height. A tall target left no room for either, so the step explaining the opening
  ladder - nine rungs, and the tallest thing on the page - pushed its own card off the top of the
  screen. The card measures itself now and goes beside the highlight wherever there is a column free,
  which is the placement that works for every shape of target. Where nothing fits at all it sits at the
  foot of the screen rather than the head, so the panel's own heading stays readable.
- **A strike says no before the click rather than after it.** The menu of methods is built from the
  attacker alone - your thugs, your garage, your coke - and never sees who you are looking at, so the
  target's half of every rule had nowhere to be said. A player could sit reading "nothing parked there
  to take" underneath a live button offering to take it, and learn the rule only by spending the click.
  Jacking an empty garage, infesting a house with no hoes and poaching one with nobody in it are all
  answered up front now, in the same sentence the launch would have thrown - one function answers both,
  because a rule written twice is a rule that will disagree with itself.
- A player's profile weighed the viewer's net worth against the target's plunder, so the anti-farm gate
  gave a different answer on a profile than it did on the target list beside it. Both read what can be
  taken now. Introduced when the two sums were split apart, by a positional argument that went on
  compiling once the parameter beneath it had changed meaning.
- **"Run a production shift" sent you to the street, where there is no production.** It had pointed
  there for as long as the ladder had existed, so a new player following the game's own instructions
  arrived at a page with nothing on it to do. It points at the hideout now, where the lab the previous
  rung just told them to build actually is - which also makes the two one page instead of two, and
  takes the opening ladder from six tab changes down to five.
- **The storage ladder the server actually runs on was still the old one.** The rebuilt ladder went into
  the code defaults, and appsettings.json goes on winning wherever it carries a value, so the running
  game had the new rule capping crews at the old room sizes: a starting player was held to ten hoes
  rather than the twenty-five intended, and worse than the fifty they had before any of this began. The
  tests were green throughout, because every one of them builds its options from the defaults.
- A defender told they had been infested read "Somebody put something through your house", which names
  neither what was done nor what it was done to. It is the phrasing the attack menu uses, but there a
  sentence about medicine and who it can treat follows immediately and makes the euphemism land; alone
  at the top of an alert it says nothing. It says poisoned now, and so does the menu that lends it the
  words. It was also the only headline in that family with no test behind it, which is how it got out.
- The raw action breakdown is admin-only. Every action popped a table of internal keys and unrounded
  figures at every player - "Item Key / condoms", "Unit Price / $10" - which is a debugging aid and
  reads like one. Players keep the summary sentence, which is written for them.

### Changed
- **The game takes weeks now.** A player spending every turn finished everything in fourteen days; the
  same player now takes thirty-six, and one who logs in once a day takes fifty. Simulated rather than
  guessed at - the model reinvests the way a real player does, because a steady-state sum says a hundred
  days while compounding says fourteen, and only one of those is what happens.
- The curve is graduated rather than multiplied flat. The first rungs barely move - a second storage
  room goes from 15,000 to 22,000 - and the last ones carry the weight, up to seven times. Slow to
  finish rather than slow to start, so a first session still buys something.
- Crew hire costs are untouched. Raising them moved the finish line by a day and a half while making the
  early game meaner, which is all cost and no effect.
- Closing that curve opened a hole the ladder test caught immediately: past a certain point a Trap House
  had nothing to save for between 210,000 and the Warehouse. A lab level moved to fill it.

- **Ten rooms became eight.** The workshop, the still and the mix house were the same room wearing three
  signs - turns and materials in, one good out - and two of them dead-ended at the second building with
  two levels each, maxed in an afternoon and never thought about again. There is one bench now: guns,
  moonshine, cut and poison all come off it, the level buys how fast it works and how far up the list
  it reaches, and what a thing costs to make belongs to the thing rather than the room. Which is what
  the guns had been saying since they were given a forge cost of their own.
- Nobody loses what they built. The migration folds a still and a mix house into the workshop before it
  drops them, taking the best of the three, so a player who had bought two small rooms and no workshop
  comes out the other side with a bench.
- **A lab upgrade says what it actually returns.** The later levels buy about half the output per pound
  that the level before them did - payback slides from five days to forty-six - which is a fine thing
  for them to be and a bad thing to be quiet about. They are a sink for money with nowhere else to go,
  so the room now says how many days it takes to pay for itself, and says outright when it is a trophy
  rather than an investment.

- **A hideout counts towards net worth, at every pound it cost.** The building was the one thing a
  player owned that counted for nothing: cash, crew, guns, product and even the beer were on the books
  while up to 13.4 million pounds of building was invisible. The largest investment in the game made
  your standing worse the moment you made it, and a player who put a Penthouse over their head dropped
  down the board for doing it.
- Valued at cost, which makes an upgrade neutral: cash becomes a building of the same worth and the
  board does not move. Buying rooms is not a way up the leaderboard, and it is no longer a way down it.
- A tier being built counts from the moment it is paid for, rather than after it lands. Otherwise a
  player drops down the board for the length of the build and climbs back afterwards.
- **Fights are weighed on what could actually be carried off, which is net worth without the building.**
  The anti-farm rules exist to stop the strong robbing the weak, and a hideout is the one thing nobody
  can take. Counting it would rule a well-built player out of fights they can plainly afford and drag
  in heavyweights who would arrive to find nothing worth the trip. Rivals pick targets the same way.
- Two other places kept measuring what a player can lay hands on rather than what they are worth, for
  the same reason in different currencies. The beginner's turn boost tapers off at 250,000, and on net
  worth it would have expired the day somebody bought a 200,000 building - charging a new player their
  starter help as a fee for the upgrade the game had just recommended. The shrine caps its demand at
  half a shelf, so counting a building would have pushed every established hideout against that cap at
  once and had the gods ask the same of a millionaire and of a man who just bought a roof.
- The hideout page says what the building is worth on the board, because a number that had always been
  zero changing quietly is how a player concludes the game is lying to them.
- Three rooms - the still, the mixing room and the intelligence room - were missed on the first pass at
  valuing a hideout, so there is now a test that reflects over every price list in the config and
  insists a maxed hideout is worth every pound the game can charge for one.

- **A crew is capped by whichever runs out first: the room the building has for them, or the supplies
  the store can put behind them.** A Trap House offered space for fifty hoes while the room behind it
  held four turns of condoms for them, so the game invited a player into a shortfall and then charged
  them morale for it every shift. That is a punishment for believing the hideout page.
- **The storage ladder is the crew ladder now**, because the store is what decides how big a crew can
  be. It used to open on a room supplying a fifth of an action at the caps above it - a room you had
  outgrown before you understood what it was for. It opens at a working crew of 25 hoes and 12 thugs
  and climbs, a rung at a time, to the biggest house in the game.
- Nothing above the top building supplies a bigger crew, because no building holds one. What the last
  upgrade buys is room for product, which is the only thing left to want.
- **Pimps are deliberately not on that list.** Nothing supplies a pimp - they eat no condoms and drink
  no beer - so the building is the only thing that can run out of room for one.
- A refusal names whichever cap is actually the limit. Blaming the building for a ceiling the storage
  room is setting sends a player off to buy a bigger house, which will not move the number by one.
- Moving up a tier no longer claims to raise your crew. It raises the ceiling; the store is what walks
  you up to it. A player who buys a Warehouse for the hoes and finds the number unmoved has been sold
  something by their own game.
- Existing hideouts keep every level they had and every one of them got roomier, since the ladder
  shifted up rather than down. No crew anywhere is over its new cap.

- **Orders can be filled a bit at a time.** They run to sixty units and a first storage room holds five
  weapons or ten of coke, so insisting on the whole amount in one movement made most of the board
  unfillable for exactly the players it was meant to give something to aim at - a new player could not
  take a single weapons order at any size.
- **Deliveries pay the town's ordinary rate as they happen; the premium arrives whole at the end.** That
  is what stops instalments becoming free money: stopping half way leaves a player exactly where selling
  the same goods flat would have, so a part-filled order costs nothing but the chance at the premium,
  and the only way to earn the premium is to finish. Because it is never split, two trips pay precisely
  what one would have.
- **The first delivery claims the order.** Without that, two people part-fill the same one and whoever
  worked hardest and arrived last simply wastes the goods. A claim is not forever - the deadline frees
  an order nobody finishes - and rivals respect it the same way players do.
- Purity is re-checked on every delivery rather than only at the start, because it is a property of the
  pile rather than of the units leaving it: a buyer who took a strong first instalment has not agreed
  to a weak second one.
- Rivals work orders in instalments too, so they go after ones they can start rather than only ones
  they could finish in a single movement.
- Asking to hand over more than is held is refused rather than quietly reduced, on the grounds that a
  player who typed a number meant it. Handing over more than the buyer still wants is refused the same
  way, and the button offers the amount that will actually go.

- **The interface works on a phone.** The layout already collapsed to one column at 760px, but the
  navigation, the status numbers and every panel added since that breakpoint was last revisited did
  not, so the parts that mattered most were the parts that had drifted furthest.
- **Navigation moved to a bottom tab bar.** The side rail used to fold down into a strip of two-letter
  codes that scrolled sideways: three of the nine destinations sat off the right-hand edge with nothing
  to say they were there, and the six you could see were abbreviations you had to learn. Four named
  destinations now hold a permanent slot with the rest a tap away behind More, which is also where a
  thumb can actually reach. More carries the name of wherever you are when you are somewhere it holds,
  so the bar never shows a page you cannot find yourself on.
- **Your name and your alerts sit to the right of the page title** rather than stacked underneath it,
  where they had been costing three rows of height before any of the game appeared - on the screen with
  the least height to spare. The header is 52px now. To fit on one line the name plate gives up its
  border and padding, and the title takes the squeeze rather than the right-hand side: a truncated page
  name is still legible, a truncated player name is not.
- Moving the bell inboard broke the alert panel that hangs off it, which had been anchored to the
  button's right edge back when that edge was the screen's. It is anchored to the header now, so it
  spans the content width and lines up with the rest of the page rather than depending on how long
  somebody's name happens to be.
- **The status strip no longer hides your money.** Seven cards in a horizontal scroller put cash and
  heat - the two numbers every decision in the game is weighed against - past the right edge, behind a
  gesture nothing advertised. They wrap into two columns and all seven are on screen at once.
- **The alliance panels were the only ones that actually broke**, having been built after the mobile
  breakpoint was last touched: member rows kept their desktop columns and crushed a name and town into
  35 pixels. Rows built around a name now give the name the width and put the controls beneath it.
- **Every control clears 44 pixels on a touch screen**, including the compact ones. Compact exists to
  fit another row on screen, which is a good trade for a mouse and a bad one for a thumb - a row you
  can see is no help if the button in it takes two goes to hit. Desktop density is untouched, because
  the rule only applies where the pointer is actually coarse.
- The page reaches under a notch and pays it back with safe-area padding, so the tab bar clears the
  home indicator rather than sitting beneath it, and heights measure the viewport that is actually
  visible rather than the taller one a phone's address bar claims.
- **The palette has names.** It had grown to 175 loose hex literals, which meant there was no design to
  change - only 175 separate decisions to find and re-make by hand. The eleven carrying most of the
  weight are now named for the job they do rather than the colour they are, and 212 uses point at them.

- **The typeface is real now.** The stylesheet had asked for Inter since the beginning and nothing ever
  loaded it - no `@font-face`, no link, no package - so every player had been reading the fallback, and
  the two weights above bold were rendering as plain bold because a system font has nothing between.
  Inter is self-hosted rather than pulled from a font CDN: one dependency, and no third-party request
  on page load.
- **A type scale of eight steps, down from 33 hand-picked sizes.** Fifteen of those sat between .72rem
  and .88rem - steps of about a fifth of a pixel, which no eye resolves - so they read as one muddled
  size rather than as hierarchy. Two sizes that cannot be told apart should be the same size.
- **Three weights, down from six.** 900 and 950 were both in use against a font that had neither. The
  variable Inter does have them, which would have made the difference visible for the first time and
  immediately far too much.
- Naming `b` and `strong` stopped a bug the new font would have exposed: the browser's own stylesheet
  gives them `bolder`, which is relative, so one nested inside a parent already at 700 computed to 900.
  That was invisible while the fallback had nothing heavier than bold to give.
- **Spacing snapped to a 4px rhythm**, replacing 19 gap values and 51 padding values that included 3px,
  5px, 9px and 13px. Off-grid spacing is not wrong so much as arbitrary: no two panels agreed on what a
  small gap was, and the eye reads that disagreement as untidiness it cannot name. Ties round tighter
  rather than looser, so nothing grew into the overflow the mobile pass had just removed.
- **Figures hold still.** Every number in the game changes while you watch it, and proportional digits
  are different widths, so a figure re-flowed its own row each time it ticked. Tabular figures are one
  width: columns stop shuffling and numbers line up on their digits the way a ledger does.
- Body copy is capped at 68 characters. A line stops being comfortable past about 75 because the eye
  loses the return sweep, and the wide desktop panels were running paragraphs to 1,400px.

### Added
- **Chat is a window in the corner rather than a panel on a page.** It started on the overview, which
  meant a conversation ended the moment you went to work the streets - the one screen you are least
  likely to be sitting on when somebody says something to you. It docks now: a bar bottom-right that
  expands, collapses to its own header, closes to a small launcher, and remembers which of those it was
  across page changes and reloads.
- Minimised it still listens, at a slower interval, and carries a count of what other people have said
  since you last looked. Your own lines coming back are not news, so they do not count. Closed it asks
  the server nothing at all.
- The room is a tab inside the window rather than the window itself, which is the shape a direct
  message will want: another tab rather than another panel.
- On a phone it spans the width and sits above the tab bar rather than under it, because there is no
  room beside anything at 375px.

- **Chat, in three rooms**: the whole board, the town you are standing in, and your crew. Each is a
  different room rather than the same room with a filter on it, and a message belongs to exactly one of
  them for good - a thing said to your own crew was said on the understanding that it stayed there.
- The scope is written onto the line rather than read off its author afterwards, so a Detroit message
  stays a Detroit message once its author has moved to Miami, and a crew message stays with that crew
  once its author has walked out. Reading the author's current state would quietly rewrite history
  every time somebody travelled.
- **An unknown channel falls to Global**, which is the opposite of how every other unknown value in the
  game is treated and is deliberate. A door that cannot be read is shut, because handing somebody a
  crew by accident is the worse mistake. A line is the other way round: one that lands somewhere more
  public than intended can be seen and answered for, while one that quietly reaches a crew it was not
  meant for cannot be taken back. Every private room has to be asked for by name, and a test holds it.
- Bans and suspensions need no work here: they are enforced at the door, so an account that cannot hold
  a session cannot say anything either.
- Three seconds between messages, read off the table rather than held in memory, so it survives a
  restart and cannot be sidestepped with a second tab. Lines are swept after a fortnight, from the read
  path, because a table nobody is reading does not need tidying.
- Polled every eight seconds and only while the tab is in front. A socket is the right answer for a chat
  that has to feel instant; this one sits beside a game whose turns arrive every ten minutes.

- **Two new things on the bench, both closing gaps rather than adding systems.**
- **Medicine**, and it arrives a level before the poison it answers. Poison went onto the bench without
  it, which quietly made attacking cheaper than defending: a player with the deeper shop could buy an
  infestation at a third of the counter price while the house it landed on paid full price for the
  cure. Defence comes first now - you can look after your own place a level before you can go after
  anybody else's - and a test holds that line for any pair where one thing exists to beat another.
- **Condoms by the case.** Beer had moonshine undercutting it since the still existed; the hoes' half of
  upkeep is the larger recurring cost in the game and had nothing. Not manufactured - nobody is making
  these in a back room - but bought wholesale, which is the same saving in the player's hands.
- Rides stay off the bench deliberately. A car you can build whenever you want one is a car nobody
  minds losing, and the jacking strike is only worth throwing because it takes something that stings.
- Poison had no reference price, so the workshop had been quoting it against a counter price of zero.

- **Infesting a house costs poison, which you have to buy or make.** It was the only strike that took
  nothing to throw: a drive-by risks the car, a jacking needs a thug and somewhere to park what it
  takes, a poach spends coke a head, and poisoning somebody's crew was free - which made it the obvious
  opening move against anybody, at any time, for no reason.
- **A dose reaches three hoes, exactly as a crate of medicine treats three.** The defender's own problem
  handed back in reverse: covering a big house costs real money at either end, and turning up short only
  buys you the hoes your doses could reach. A part-used dose is a used dose, or one would cover a house
  forever by never quite finishing.
- Bought at the counter beside the medicine that answers it, or made cheaper in the mix house, which is
  the chemicals room and was already turning out cut. It takes a shelf like anything else, counts
  towards net worth at what it cost, and is not loot - a raid cannot carry off medicine either.

- **A walkthrough that shows you the game rather than describing it.** Six steps, one thing lit at a
  time with everything else dimmed, and a sentence saying what it is and what it is for. It drives the
  pages itself, because half of what a newcomer has to learn is which tab a thing lives on and being
  taken there is the lesson. It offers itself once and then stays out of the way; "Show me around" sits
  with the opening ladder for anybody who skipped it or has come back after a month.
- **The suite reads the settings the server actually ships.** Half the config lived somewhere no test
  had ever looked. It now loads appsettings.json from the server project - never the copy the build
  drops beside the test binary, which is how a stale file convinced this suite the ladder had been
  updated when it had not - and runs the same invariants against it: every crew supplyable, every rung
  reachable, and every value in step with the default it is meant to be restating. Compared over all ten
  room lists by reflection rather than the one that happened to break, because the next drift will be in
  a different list.
- **A test that is written but never listed does not fail; it simply does not run**, and the suite
  reports green while the thing it guards goes uncovered. That happened twice in one sitting, and both
  times the only symptom was a total that did not go up by one. There is now a test that reads this
  suite's own source and refuses to pass while a test body sits unregistered.

- Alliances: the crew, ranks and permissions, the treasury, and the shared thug pool it pays for.
- **Four ranks** - Soldier, Enforcer, Underboss, Boss - rather than the seven or eight a clan system in
  a game with thousands of players carries, for the same reason a crew holds six and not twenty: ranks
  only mean anything when there are enough people for the gaps to matter.
- **The boss sets a minimum rank for each power**, which is the part of a rank system that actually gets
  used. Ranks on their own are decoration; what makes two crews with identical ranks run completely
  differently is where their boss drew the lines. Five powers are configurable - opening the door,
  throwing people out, spending the treasury, taking thugs on a raid, posting defenders at home - and
  the settings, promotions and handover stay with the boss.
- **You can only act on somebody below you.** Strictly below, never equal: two Underbosses able to throw
  each other out is not a chain of command, it is a fight the crew loses either way.
- **The door is one setting with three states**, set by the boss: open to anyone, by application, or
  invitation only. It replaces a boolean that had two paths always open underneath it - the old shape
  said "open or not" and quietly accepted applications either way, so a crew that had shut its door was
  still fielding requests it had no way to stop.
- The three states are the three things an outsider can do on their own initiative, which is the only
  axis a door has: walk in, ask, or wait to be asked. Each one turns the other two away by name rather
  than silently, so asking an open crew is told to walk in. Invitations sit deliberately off that axis -
  they are the crew reaching out rather than somebody arriving, they work in every state, and a crew
  that could not invite while set to invitation-only would be a contradiction.
- Invitations and applications are one table read from opposite ends, because they are the same row with
  different people waiting on it, and every road in ends at the same place - joining at the bottom, so
  no route can accidentally hand out a rank.
- Both are re-checked at the moment they are accepted rather than trusted from when they were sent:
  weeks can pass, and in that time a crew fills up or the player joins somebody else. Accepting one
  clears every other ask that player had outstanding, in both directions.
- A boss can see and take back the invitations their crew has sent, not only the ones waiting on them.
  Leaving that out is how a crew ends up with invitations outstanding to people who quit months ago.
- Promotion stops below the top. Handing the crew on is its own move precisely because it is the one
  that gives yours away, and a promotion that could reach Boss would let a crew acquire two by accident.
- **A crew is people who have agreed not to rob each other**, and that is enforced rather than asked
  for. The source game left the interesting half of this to the message board - "don't form super
  alliances, it's against the rules" - which is a rule that only works while somebody is reading. Here
  members cannot attack each other by any method: raid, drive-by, jacking, infestation, poaching, or a
  raid on their ground. The check lives in the launch rules, so every route into a fight runs into it -
  a player's, a rival's brain, and the admin's directive alike.
- **Dues** are a founder-set share of every member's shift, taken off the gross beside the hoe cut
  because it is the same kind of thing and reads in the same sentence. Off the gross rather than off
  what is left, so a house paying 40% and dues of 20% gives up 60% of a shift and not 52% - compounding
  would make the second rate quietly mean something different depending on the first. Between them the
  two can never take more than the shift actually made.
- **Crew ranks** are the sum of what the members are worth, off the same net worth expression the
  individual leaderboard ranks by, so a crew's standing and its members' can never tell two stories.
- Six members rather than the source game's twenty. That was a game with thousands of players signing up
  every month; this world is two dozen rivals and a handful of people, where twenty would not be an
  alliance but everybody against nobody.
- Rivals already run with each other. Crews seed themselves into an existing world on first read, the
  way ground does, formed around towns because that is the alliance a world would actually make. A share
  of rivals is always left unaligned, because a board where everybody has agreed not to fight has
  nothing left in it.
- **The shared thug pool.** The founder buys offensive and defensive thugs out of the treasury at the
  source game's $15,000. Offensive ones ride along on a member's raid; defensive ones are posted to a
  member's house and stand in it until released or killed. Both fight as an armed thug apiece, both die,
  and what dies is gone from the pool for good.
- The pool is finite and that is the whole of its interest. Thugs committed to a raid leave the pool for
  as long as the raid is out, so what you take tonight your ally does not have; survivors come back when
  the crew comes home, and calling a raid off sends them back too.
- **A member may field at most as many borrowed thugs as they brought of their own.** This is the rule
  that keeps the pool from breaking the game. Alliance thugs ignore the hideout's thug cap, which is the
  constraint every combat number is measured against - without a limit a Trap House with a rich crew
  behind it could field a Penthouse army and the whole ladder would stop meaning anything. Tied to the
  member's own crew, the pool amplifies instead of substituting: your tier still sets your ceiling and
  the crew only doubles it. Posting defenders runs under the same rule, so an empty house cannot be made
  a fortress with borrowed men.
- Losses fall across the whole line in proportion. Borrowed thugs dying first would empty a pool in one
  raid; the member's own dying first would make borrowing a way of using other people's men as armour.
- Two things from the guide left out on purpose. The **99/99 trick** is the guide documenting an exploit
  - a price tier you game by stockpiling to $10M and buying in one burst - and reproducing it would mean
  deliberately building the bug. **Alliance chat** is a different kind of project with no gameplay in it.

- Named scouting districts, and a reason to pick between them. The source game had five and its own
  guide admits it never found a difference between any of them - "I've yet to find a significant
  difference", and the FAQ answer to which is best is a shrug. Five names on a dropdown that all do the
  same thing is a wasted click, so each one here changes what a shift is actually for.
- The **Casino District** pays 45% more and hires almost nobody, and the law is watching all of it.
  The **Wino Slums** pay badly and are full of men who will take any work going - the place to go for
  thugs, and the quietest street in the game. The **Nightclub District** is where hoes and the pimps who
  manage them turn up. The **Urban Ghetto** is where product changes hands, and where the law knows it.
- The **Low Rent District** is the neutral one, at exactly the base numbers, and the default. It is also
  the district the source game's own guide-writer said they preferred, which turns out to have been a
  reasonable call. A player who never touches the picker works precisely the shift they always did.
- Every district is best at something and costs something - in what it gives up, or in how much notice
  it draws - and a test fails if any of them is better at something and worse at nothing.
- Each tile says what it is for, written from the numbers rather than from a stored sentence, so
  retuning a district retunes what it says about itself.
- Rivals pick a district from what they are short of rather than from a fixed preference: a crew builder
  with no thugs goes to the slums today whatever it usually does. Personality only breaks the tie.

- The shrine, and the names the day hands out. The last two things the source game had that this did not.
- **Praying to the pimp gods**, once a week. The source game made this a slot machine: burn whatever you
  like, roll, maybe something happens. That is a lever rather than a decision, so here the gods say what
  they want - a specific good, a specific number - and meeting it is answered every time. Only which
  blessing lands is uncertain, and even that is narrowed to the ones that would actually help you.
- What they ask for is worked out from the player and the week rather than stored, the same trick the
  rival personalities use, so the ask holds all week without a row to keep or a job to run. It is sized
  against net worth, banded to two significant figures so ordinary earning does not move it, and capped
  at half a storage shelf - a value share in a cheap good is otherwise hundreds of bottles of moonshine
  that no room in the game could hold.
- Nothing the shrine gives back is money. Every blessing is something money cannot buy at all: notice
  the law has already taken, the mood of the house, a pimp's faith in you. Turns are the one rationed
  behind giving twice what was asked, because they are the only thing that touches the rate the whole
  game runs at. A player who prays every week for a year is no richer for it.
- **Daily titles**: seven names, held by whoever leads a category over the last day. Read out of the
  fights that actually happened rather than kept as counters - the source game held eight running totals
  and a button to wipe them, and a tally you can clear is not a record of anything.
- Half the titles are for things done to you, which is the source game's own reading and the half that
  makes the board worth looking at: Silver Tongue and Picked Clean are the same number counted from
  opposite ends. A board of nothing but winners says only who is winning, which the leaderboard already
  says.
- Titles show on a player's row and profile, so the target list says who somebody is before you open
  them, and a floor under each category stops a quiet day handing out names for one of anything.

- Weapon tiers. One generic weapon could only ever answer "is this thug armed", which made arming a
  crew a purchase rather than a decision. There are four guns now - pistols, shotguns, SMGs and rifles -
  and the point of them is that a weapon does two jobs which come apart.
- Any gun covers a thug for morale. A thug with a pistol is exactly as content as a thug with a rifle,
  so covering a big crew cheaply is a real strategy. What a gun changes is the fight: firepower is
  measured in pistols, and a crew picks up the best guns on the rack rather than a sample of them.
- That turns the hideout's thug cap into the binding constraint. More bodies is the efficient way to
  get stronger right up until there is nowhere to put another one, and past that the only thing left to
  buy is better guns - which are steeply worse value per point of firepower, and priced to be.
- Source prices throughout: $250, $1,250, $2,500 and $5,500. Every weapon that already existed became a
  pistol, so **nobody's fighting strength moved at all** - a pistol is worth exactly what the single
  weapon was. What moved is paper value: a rack halves, uniformly, across every player and rival alike.
- The workshop makes what its level has unlocked: pistols and shotguns from the start, SMGs at level 2,
  and never rifles - the one gun nobody makes in a back room, which is what stops the workshop from
  eventually replacing the shop. Its per-weapon cost moved onto the guns, so a level now buys throughput
  and reach rather than a discount on a single thing.
- All four trade separately on the player market and can be asked for by name in a contract. A board
  that listed a rack of pistols and a rack of rifles both as "weapons" would price them as if they were
  the same offer.
- Losses and storage overflow take the cheapest guns first, always. The alternative - a lost fight
  destroying your rifles before your pistols - would make owning good ones a liability.
- A raid carries a specific mix, recorded when it leaves, so losing five weapons takes the right five
  off the right shelves at home. Two raids at once cannot arm themselves from the same rifles.
- Rivals arm for coverage first and trade up with spare cash, so a rich rival's house is genuinely
  harder to break than a poor one of the same size.
- A jacking reads both halves of the guard on the garage. Bodies are eyes on the door - however lightly
  armed, more of them means more chance somebody is looking at the one you came in through - and guns
  are what happens once you are seen. Six riflemen shut a garage that six pistols would only make risky.
  Only the firepower above one pistol each counts in the second term, so the two never describe the same
  thug twice and an all-pistol guard has precisely the odds it had before guns had tiers.
- Guns out on a raid are not guarding the garage either, so striking somebody mid-raid is the opening it
  ought to be: a raiding party takes exactly the guns that would otherwise have stopped you.
- A drive-by reads the same two halves, and weights them the other way round. Whether the pass finds
  anybody leans on bodies - a crowded street is one where somebody sees you coming and everybody is
  behind a wall before you arrive - while whether the car comes back leans on guns, because a pistol
  rarely stops a moving car and a rifle very often does. Against the same six guards, swapping their
  pistols for rifles takes the hit chance from 78% to 64% and the odds of losing the car from 11% to
  24%. It is also what gives the drive-by a ceiling: past a certain quality of guard it costs more cars
  than it is worth, whoever is driving.

- The rest of the attack menu. One attack verb made every holding one undifferentiated pile of loot: a
  garage of cars and a hundred hoes were numbers feeding the same defence roll, and no decision a
  defender made about either of them mattered. There are five ways to move on somebody now, and four of
  them are aimed at exactly one thing.
- A **raid** is unchanged: ten turns, an attack lane, travel, rounds, and whatever the crew can carry.
  The four **strikes** are the opposite in every respect - four to eight turns, settled on the spot, no
  lane, no crew committed, and each answered by something different.
- A **drive-by** needs a low-rider. It kills thugs and dents their morale, takes nothing at all, and the
  better armed the street the likelier it is you lose the car. It is how a player who cannot yet win a
  raid makes one winnable.
- **Jacking** takes their rides. Its odds are almost entirely the defender's own doing: a garage behind
  a full armed crew is close to untouchable, and one behind nobody is a car park with the keys in.
- **Infesting** their hoes is the only attack in the game answered by a purchase. Medicine treats who it
  can and the rest are lost, which is what makes a crate on a shelf - costing money, doing nothing -
  worth owning.
- **Poaching** buys their hoes away with coke, and is the reason the payout slider is a decision rather
  than a dial nobody touches. A fully happy house cannot be poached at any price; a squeezed one can be
  emptied. Stepped-on product tempts fewer people, through the same purity multiplier the market prices
  by, and the coke goes out whether or not anybody comes back with it.
- Low-riders and medicine, the two things the menu needed. Rides are held by the building rather than
  the storage room - a garage of two at the Trap House, fifteen at the Penthouse - and the chop shop is
  the one counter in the game that buys as well as sells, at $15,000 against a $25,000 sticker. Net worth
  counts a ride at what the shop would pay, not what it cost, or buying one would be a free climb.
- Two shields on two clocks. A raid's protection covers everything, because walking in behind somebody
  else's victory is the dogpile it exists to stop; a strike sets only its own twenty-minute shield, or a
  four-turn drive-by could buy its victim an hour of immunity from the raid that was actually coming.
- Rivals use all five. Each personality has its own appetite for a cheap shot and its own order of
  preference, which is what gives them a signature in the news: the Hard Charger shoots up streets, the
  Banker quietly drives off with cars, the Crew Builder poaches. They reach for a strike when they cannot
  afford an operation, and they restock medicine once somebody has actually been infesting them - a field
  that never bought any would make the infestation a one-way ratchet forever.
- The defence alert says which of the five hit you. "Broke through your defence" is true of a raid and
  absurd of a drive-by, and a defender told only that they lost has no idea whether to buy medicine, move
  the cars, or pay the house better. Hoes were missing from the loss list even before this, which
  understated every raid that took any.
- AI rivals fill contracts too, so the board is a race rather than a menu. No dice roll and no
  personality dial: an order pays over the counter for stock a rival is already holding, so every one
  of them takes it, and what decides who gets there first is whose hours fall when.
- A town posts orders at a pace instead of refilling on demand. Live testing showed rivals stripping
  23 of 24 boards, which was fine on its own - the board topped up when a player looked - but it meant
  anybody could fill an order, look again for a fresh one and repeat until their stock ran out. That
  would have made the counter price never worth taking and quietly raised the value of every sale in
  the game. Now filling one means waiting for the next, and a rival taking one actually takes it.

- Contracts: people in a town who want a set amount of something by a deadline and pay over the
  counter for it. The game had exactly one buyer before this - the city itself, fixed price, any
  amount, any hour - which is a price list rather than a market and made producing a routine.
- The buyer is a real place on that town's map, and what a town asks for follows what it values, so
  Las Vegas leans on coke and Detroit on weed without either ever ruling the other out.
- Some coke buyers set a purity floor and pay extra for strength. Sometimes rather than always: a
  floor on every order would make stretching pointless rather than the trade it is meant to be.
- Every refusal is a real one, against the same stock the rest of the game moves - not enough held,
  cut too thin, or the buyer is in another town - and an order is filled once and then gone.
- Boards are topped up when somebody looks at a town, the way ground is seeded, so a town nobody
  visits costs nothing and no timer can drift.

- A town's risk now reaches the daily loop instead of only the road in. It used to decide whether a
  run was stopped at the door and nothing at all about living somewhere, so two players running
  identical operations in Detroit and New York stood in identical danger.
- The same stash and the same shift draw more notice in a watchful town: 60 coke and 40 weed reads
  17.5 heat in Detroit, 25 in Atlanta and 35 in New York. Earned heat is banked points and is not
  rescaled by moving, or changing town would rewrite a player's history rather than change what
  happens next.
- The heat note names the town, so a player who moves and watches the number jump knows it was the
  place rather than something they did.
- It pairs with what a town pays. The towns that watch hardest are the ones that sell dearest, so the
  trade is legible rather than a penalty for living in the wrong place.

- Per-city leaderboards. Eight towns on one global board means most players never appear on it and
  never will, so the town they chose is now the place their standing is actually legible. Standings
  opens on your own city and toggles to everywhere, and the dashboard carries both ranks.
- Both ranks come from the same definition of who outranks whom, narrowed to a town, so the two can
  never disagree - and a city board reads 1..n for that town rather than showing global positions.
- Nine more rivals, three in each new town. A city with nobody in it has an empty leaderboard and
  nobody to fight, so putting a town on the map without names in it only looks like a choice at
  sign-up. A test now fails if any town has fewer than three.
- Three more towns - Las Vegas, Atlanta and Houston - bringing the map to eight, each with its own
  ground, prices, risk and distance rather than being reskins of the same place.
- Houston takes coke off the water and is the second place it is cheap; Las Vegas is where it is spent,
  pricing it dearest alongside Miami; Atlanta is a distribution town, close to everything with cheap
  weed. That widens the best coke route on the map to $112 a unit.
- Every town still carries all four kinds of ground. A player picks their town at sign-up knowing
  nothing about any of them, so a town missing an effect entirely would punish a blind choice for as
  long as they stayed there: the character is in the mix, never in leaving a gap.
- New ground seeds itself into an existing world by name the first time the territory page is opened,
  so the three towns arrive without a migration.

### Added
- AI rivals hold grudges. They already fought each other, but picked whoever was richest every time
  and forgot being robbed the moment it happened, so nothing between two of them ever became a story
  and the world read as weather rather than as people.
- A grudge never makes a rival reckless. The win margin and the anti-farm rules still decide what they
  will take on: it only settles which of the fights they were already willing to have they pick.
- How hard it is taken follows from character. A Hard Charger weights a score at nearly its own worth
  and remembers for three days; a Banker treats a robbery as a cost of doing business and has
  forgotten by the morning.
- Grudges are read from the fights that actually happened rather than kept as a score, so one is
  exactly as old as the last punch and nothing has to be pruned or migrated.
- A feud headline in world news, one-sided or mutual, so a quarrel between two rivals is something the
  player can watch rather than something happening out of sight.
- Next Moves is advice now rather than a status readout. It ranks what is actually worth doing against
  the state you are in, names what each move costs, and says why it is worth it. The old panel showed
  the same four rows on day one and day one hundred and never once named a move.
- A Getting Started ladder covering the verbs the game never introduced: work the streets, bank what
  you make, build the weed lab, run production, sell it, arm your thugs, hire a second pimp, deepen
  the store, and reach the Warehouse. It hides itself once finished.
- The opening bank is the full 200 turns rather than half of it, so a first sitting is ten shifts
  instead of five - long enough to buy the first lab and still have turns left to watch it work.
- Turns come back faster while a player is small, tapering from three times the rate at the start to
  the normal rate by a quarter of a million net worth. A flat twelve an hour meant a new player who
  spent their bank waited most of a day to play again, at exactly the point they had least reason to
  come back. An established empire is untouched.
- Crew rows suggest what to cut down to instead of leaving the player to work it out. Firing in bulk
  already worked - the quantity box takes up to a thousand - but nothing told you the number, so the
  hoes row now offers "let 14 go to what your pimps manage" and "let 24 go to what your store
  supplies", filling in the box rather than firing on the spot.
- And it quotes the morale cost before the button is pressed, including when a cut is large enough to
  hit the ceiling. Firing fourteen hoes costs 21% morale, which the button previously gave no hint of
  until after it had landed.
- The storage supply warning offers the answer that costs nothing as well as the one that costs money.
  Outgrowing a room has two fixes - buy a bigger one, or work a shorter shift - and it only ever named
  the first. It now names the longest shift the room actually supplies, which for eleven hoes in a
  level 1 room is eighteen turns rather than twenty.
- A Lookout room, and the end of the first tier's dead zone. Everything a Trap House could buy landed
  between $10,000 and $75,000, and then nothing until $150,000: a session and a half of earning with
  nothing to want. The lookout sits at $100,000 and cuts the odds of a raid landing by a quarter.
- It is also the only new verb in the tier after the workshop, and the first answer to heat that is
  not selling everything and waiting. It never removes the risk, or holding contraband would be free.
- A test now walks the tier's whole ladder and fails if any two rungs are more than two sessions of
  earning apart, so a future re-pricing cannot quietly reopen the hole.
- The status strip reports the rate a player actually earns at rather than the base one, and the
  advice panel says plainly that the help exists and will fade.
- Both guidance panels are read from the world rather than stored, so a rung cannot drift out of step with the empire
  it describes, and no migration was needed for either.

### Changed

## 0.2.4

### Changed
- Coke now has a purity, and cutting is a trade instead of a printer. Stretching a pile made a unit of
  filler into a unit of product at full price, which made the mix house a cheaper and faster source of
  coke than producing coke was, with nothing to stop it: about $300 a turn against $220 for the real
  thing, and no ceiling.
- Purity is a weighted average of everything in the room, so filler drags the whole pile down. The
  sale price follows the square root of it, which is the only shape that works: fall proportionally
  and stretching gains nothing, put a floor under it and total value climbs with unit count forever.
- Every way coke arrives now blends rather than counts on - produced, found, stolen, bought off the
  board, flown in by a mule, or stretched with filler - and listings carry the purity they were
  escrowed at, so the board cannot be used to launder weak product into clean.
- Net worth values coke by strength too, in the database as well as in memory, so the ladder is not
  fooled by bulk.
- Producing coke is now roughly three times better per turn than making cut and stretching, which is
  the way round it should have been all along.
- Cut is spent by a step of its own now instead of vanishing into coke production. One unit of cut
  makes one unit of coke, on any coke you hold however it got there, at a speed the mix house level
  sets. Production no longer touches cut at all.
- The old arrangement was wrong twice: a player saving cut for a batch watched it disappear into a
  production run they had not connected it to, and cut could never reach coke off a plane, off the
  board, or out of a lab, which is most of the coke worth stretching.
- A batch stops at whichever limit binds first and says which one it was, rather than leaving a player
  guessing whether to buy cut, sell coke, or build a bigger room.
- AI rivals step on their coke too, before selling, so the mix house does not pile up cut they never
  turn into anything.
- Heat rose far too fast, because neither half of it was sized against anything the game actually
  ships. Working the streets earned half a point a turn, so a full 200-turn bank spent in one sitting
  earned 100 heat and took a player holding nothing at all from Quiet to Hunted, with decay of three
  an hour unable to keep up. Coke drew a point a unit, so simply filling a Warehouse store put you at
  85 and Hunted for using the room you had bought.
- Street work is now 0.15 a turn, so a whole bank is about 30 and a night of laying low clears it, and
  the per-unit weights are cut to roughly a third. A full Warehouse store of coke reads Noticed, a
  fully stocked Warehouse reads Watched, and only a maxed Penthouse store of everything reaches
  Hunted on stock alone.
- AI rivals play in sittings instead of on a metronome. They used to take exactly one action every
  fifteen to fifty minutes, evenly, around the clock, forever, which is nothing like what a player
  does. A player is away for hours while turns bank up, then sits down and spends the lot, then goes
  again.
- Each rival keeps its own hours, drawn from its seed, so the world has a rush hour and a quiet
  stretch instead of a flat hum. A fifth of them keep no hours at all, so the board is never dead for
  someone playing at an odd time.
- How often a rival plays comes from its personality: the eager ones sit down five or six times a day,
  the patient ones two or three. This is read off the pacing the personalities already carried rather
  than a second dial that could disagree with the first.
- A sitting ends when the turn bank runs dry, which is what a real one ends on, with an action count
  and a wall clock as backstops. Rivals now bank turns while away and spend them in a burst.
- Rivals hesitate: a quarter of the time they do nothing on a given beat, so a sitting is not a
  machine gun of evenly spaced actions.
- The admin's rivals table shows habits and what each one is doing now - playing with so many actions
  left, or back in so long - because idle minutes stopped meaning anything on their own once rivals
  slept. One quiet for four hours is asleep, not stuck.

### Added
- Mule runs, first slice: the model, the intelligence centre that gates them, and the launch that
  prices and freezes one. Send a pimp and hoes to another town to buy cheap and carry it home.
- An intelligence centre hideout station. Unlike every other room it makes nothing: it decides how
  many runs can be in the air at once, and takes a share off a route's risk for knowing it. Without
  one there are no runs at all.
- A run costs fewer turns than travelling yourself, which pays the distance each way, but it takes
  real time, locks up crew who earn nothing while they are gone, and is paid for in cash before
  anybody leaves: fares both ways, and their keep for the whole trip, charged up front.
- Flights take real time. At six minutes a turn of distance, the shipped map runs twelve to thirty-six
  minutes a leg, so a run is a decent chunk of an evening rather than a teleport.
- Mules buy at the destination's price, not at yours, which is the entire reason to send them.
- Everything an outcome will depend on is frozen at launch: capacity, cash, the pimp's loyalty, and
  the odds faced. A pimp whose loyalty slips mid-flight does not change a run already in the air.
- Runs settle three ways. Delivered brings the load home along with cash they never spent; seized
  takes a share of the load and the unspent cash with it, because it was in the room when the door
  came in, and the heat lands on whoever sent them; defected is the pimp keeping the money, the goods
  and the crew, and coming off the payroll.
- Runs settle on the clock, so they land whether or not anyone is watching, and reach the player
  through the alert bell and the catch-up digest.
- Cargo that will not fit in the storage room is dumped rather than overfilling it, and the notice
  says how much and why. Silently dropping a third of a load a player had already paid for read as
  the price being wrong rather than the room being full.
- AI rivals run mules. They build the intelligence centre themselves, pick a route by what it clears
  after fares rather than by the widest spread, and since rivals sit in different towns, what is worth
  running differs per rival without any of them being told so.
- How keenly a rival runs mules follows from what it is for: the Product Runner moves goods for a
  living and does it most, the Banker wants the money where it can see it, and the Hard Charger would
  rather have a fight than wait for a plane.
- A rival never sends its whole roster or its whole purse, buys only what it has somewhere to put, and
  counts cargo already in the air so two runs are not sized against the same empty shelf.

- A Mules page: pick a town, a good, how many hoes and how much money, and see the whole ticket before
  committing. What it costs there against what it fetches here, what they can carry, the fares, the
  turns, the round trip, and the odds of being caught or walked out on.
- The ticket quotes profit rather than gross, because the spread alone does not decide a run: the
  fares are paid whether or not it pays. A losing route says so and says why.
- An Intelligence Centre row on the hideout page, so the room that gates mule running can be built.
- Mule tuning was wrong on first contact. A head cost more to fly than a hoe could carry margin for,
  so every route in the game lost money. Carrying doubled and fares cut to a third, which makes short
  hops thin, bad routes clearly negative, and long runs into a wide spread worth the risk.
- Travel is a flight for the player too. A town's distance is time as well as turns, and while it is
  running you are in the air and cannot act. Travel used to be instant, which made distance a pure
  turn cost: you were somewhere else the moment you decided to be.

### Added
- A player-to-player market: one global board, escrowed stock, partial fills, a house cut, and payouts
  into the seller's bank.
- Moonshine and cut, made by a still and a mix house. Moonshine substitutes for the beer thugs drink;
  cut stretches coke one for one.
- Heat. Everything the player does is illegal, so the question is not whether they are breaking the
  law but how loudly: contraband draws notice while it is held, weighted per good, and working the
  streets draws notice on top of that. Under the floor nobody is looking, however long you sit there.
- A raid, rolled per hour above the floor, taking half of every pile and a fine capped at cash on
  hand. It clears the heat that drew it, and reaches the player as an alert and in the catch-up
  digest.
- Heat cools on its own, so lying low works. It runs on its own clock rather than the turn clock, so
  twelve short visits still add up to an hour, and a fortnight away costs no more than a night.
- Heat sits in the status strip on every page, next to turns, reading Quiet, Noticed, Watched or
  Hunted and tinting with the band. It is a live risk, so it belongs with the numbers a player carries
  between pages rather than in a panel on one of them. Each station says how much notice a unit of its
  output draws, replacing a legal/illegal badge that told the player nothing.
- AI rivals buy from and sell on the market.
- The still and the mix house need a Warehouse or better, enforced when making as well as when
  building.
- A Production section on the hideout page for the workshop, still and mix house, each shown next to
  the price it exists to beat.
- A workshop hideout station that makes weapons from turns and materials below the store price, so the
  board has a good with real demand and room to undercut.
- Territory: six pieces of ground per town, held by garrisoning thugs, who count as away from home
  while they hold it, capped per hideout tier.
- Four ground types, each a percentage on an activity the player still spends turns on: Corner for
  street income, Docks for production yield, Club for passive morale recovery, Stash House for raid
  haul.
- Empty ground is claimed with turns and a garrison; held ground is taken with a raid that uses an
  attack lane and fights the garrison rather than the holder's house.
- A Territory page showing your town's map, who holds what, and why a piece cannot be acted on.
- Players choose their town at sign-up. Registration ignored the field before, so everyone ended up in
  New York whatever they picked.
- A pimp can be posted to run each piece of ground, adding their bonus to its defence if they are an
  Enforcer. Posted pimps count as away from home for every other purpose.
- AI rivals post their best free Enforcer to ground they claim, and otherwise claim and raid ground, judging a garrison by the holder's morale and committing the same
  share of crew they would send on a raid.
- Ground changing hands reaches the catch-up digest, the alert bell, and world news.
- A raid you beat off tells the holder what it cost the garrison, in both the digest and the bell.
- A world news headline for whoever runs the most ground.
- Each town prices weed and coke on its own band, so a pile is worth different money depending on
  where you are standing with it.
- Travel between towns. How far a town is and how dangerous it is are separate numbers, so a short
  run into a bad town is a real choice rather than the same fact stated twice.
- A run can be stopped on the way in, taking a fifth to three fifths of the cash and product carried.
  The bank is never touched, which is what makes banking before a run worth doing. A stopped run
  still arrives: the turns are already spent, and turning the player back as well would be two
  punishments for one roll. A load too small to be worth searching is left alone.
- A Travel panel on the overview reading every town's prices as a change against the town you are in,
  the trip cost against the turns in hand, and the share a stop would have to take before the run
  stops paying for itself. That share is priced against the load actually being carried, so the same
  map reads differently for a coke run and a weed run.
- Travel is refused while an attack is out or while you hold ground, and the panel says which before
  a button is pressed rather than leaving the player to find out by pressing one.

### Changed
- One shared definition of which log rows are notifications rather than actions, with the activity
  list using its derived negation. It had been written out separately in three queries, so a new kind
  landed in both places or neither.
- The territory map is per town. You see and contest your own city's ground and nowhere else, and
  every town carries all four types so nowhere is short of an effect.
- Anti-farm's wealth rules and house protection do not apply to fights over ground, which carries its
  own settling period after changing hands.
- The player clock resolves morale recovery bonuses itself rather than asking every caller to pass
  them, so recovery still happens in one place.
- Product sells at the price of the town it is sold in, rather than one street price for the whole
  game.
- A listing's price band on the player market is judged against the seller's town, so the guard
  against a fat-fingered price moves with the local market instead of a single global number.
- Ground pays out only in the town you are standing in.

## 0.2.3

### Added
- Hideout tiers above the Trap House: the Warehouse, Nightclub, and Penthouse, each raising crew caps
  and unlocking deeper rooms.
- Tier builds cost cash and turns up front and take time to finish, with the old caps holding until the
  build lands.
- Storage, safe, and lab levels beyond what the Trap House can hold, each gated on the tier it needs.
- Weed and coke labs produce passively, bounded by the storage room and by a 12 hour offline ceiling.
- AI rivals invest in their hideout: safe, storage, tier, and labs, each gated on that room already
  being the constraint.
- A curated world news feed with headlines for who leads, the biggest take, the best score, and new
  arrivals.
- Individual AI rivals can be paused, told to act immediately, or directed through a chosen action:
  street work, production, trade, crew, banking, morale, hideout upgrades, or an attack on a named
  target. Directed actions go through the same services a player's do, so the rules still apply.
- The alert bell carries non-combat notices as well as raids: passive lab output and a building
  finishing, which are things done to a player rather than by them.
- The admin panel is split into tabs, with the catch-all Admin Control Center dissolved into them and
  a dedicated AI Rivals tab covering seeding, manual runs, the automatic loop's timing, and a roster.
- A catch-up summary on arrival covering attacks taken, passive lab output, finished buildings, a
  filled turn meter, live protection, the rank you moved to, and who changed places with you. Shown
  once, and only when there is something to say.
- A standings history, sampled for every player on a timer, so rank comparisons between two past
  moments are possible at all. Pruned to two weeks.
- A warning on the crew and street pages when a completely full storage room still cannot supply the
  crew through a full-length action, naming the storage level that would. Warning only: a crew built
  for fighting does not have to be supplyable for street work.
- Morale trend arrows on the overview and crew pages, measured from your most recent action, with the
  exact movement on hover and no arrow at all when there is nothing recent to compare against.

### Changed
- World news now reports fights, buildings, arrivals, and only the money and crew moves large enough to
  matter, instead of every action anyone took.
- Turn accrual, hideout builds, and lab output are settled together by one player clock rather than a
  turn refresh repeated across seven endpoints.
- Every hideout upgrade, room as well as tier, is paid from the bank first and cash on hand second.

### Fixed
- The new storage caps and station tables were only added to the code defaults, which appsettings
  overrides, so both goods had a cap of zero and could never be made.
- A raid on your ground was counted as an attack on your house as well, so the arrival summary
  reported one fight twice and described a fight over a corner as a break-in.
- Which log rows are notifications is decided by the action rather than how the sentence ends. The
  suffix match broke as soon as a second kind of ground notice existed.
- Passive lab output and finished builds appeared in the activity list, which is a record of what the
  player did, so a payout they had no hand in read as an action they took. They are alerts now.
- The world news leader headline was styled by a bare "leader" class that collided with the
  leaderboard row's, squeezing the title into a 44px column and cutting it to three characters.
- Automatic AI lived only in memory, so every restart silently reverted an admin's decision to the
  appsettings default. It is persisted now, and its tick and rounds no longer need a restart to change.
- The morale arrow measured net change across a three hour window, so it kept pointing down for hours
  after a crash was over while morale visibly climbed. It reads from the most recent action instead,
  and the steady band narrowed from a full point to a quarter, which a crew recovering 0.7 an action
  had been falling inside.
- Running short on condoms or beer was charged per missing unit, so the penalty grew with the crew
  while the morale a shift earns did not. A crew of 59 needing 99 condoms with a level 3 storage room
  holding 84 lost about 29 morale a shift and walked out within four, despite auto-buy reporting a
  successful restock. The cost is now the share of upkeep missed: the same shortfall costs about 4,
  and going out wholly unsupplied still costs 45.
- The crew morale panel and its rest and party messages were hardcoded to the Trap House, so a player
  who had moved up was still being told about a building they left behind.
- Seeded rivals were given the deepest storage room and safe in the table, which after tiers existed
  meant a Trap House holding a Penthouse-sized safe.
- Hideout upgrades priced above the safe that holds them could never be paid for, since earnings over
  the safe are swept into the bank. A level 3 safe cost $120,000 against a level 2 safe holding
  $100,000, which stranded every room gated behind it, and a level 3 coke lab has been unbuyable at
  $150,000 against the same $100,000 since 0.2.2.
- The hideout page greyed out the tier button for players whose money was in the bank, which after the
  charge moved to the bank was everyone who could actually afford it.

## 0.2.2

### Added
- Hideout capacity: the Trap House tier caps crew, a storage room caps goods, and a safe caps cash on hand.
- Upgradeable storage and safe, plus turn-fed weed and coke labs that raise production yield.
- Named pimps with Enforcer and Hustler specialties, loyalty, and a record of missions led.
- Player-chosen mission commanders, with the commander's specialty bonus frozen onto the mission at launch.
- Pimp mortality: killed commanding a defeat, killed defending a broken house, or walking out at low loyalty.
- Optional auto-buy of street upkeep, bounded by storage room and cash on hand.
- Admin panel: player search and detail, signed resource adjustments, ban, suspend, force-logout, rename, and admin rights.
- Admin audit trail recording actor, target, before and after values, and a reason.
- Oversight dashboards: wealth distribution and concentration, fastest movers, in-flight missions with stuck ones flagged, and AI idle times.
- Maintenance mode and site-wide announcements, both persisted.
- Runtime editing of 127 scalar tuning values, layered over appsettings and reversible to it.
- Anti-farm protections: a net worth floor and ratio on who may be attacked, decaying loot for repeat
  victories, protection that widens with each hit taken, and a cap on simultaneous incoming attacks.
- AI rivals now launch attacks, choosing the richest target they should beat and committing a share of
  crew that matches their personality.
- Defender alerts with an unread count, written from the defender's point of view.

### Changed
- Attack cooldowns are per lane rather than per player, so two attacks can run at once; cancelling refunds the lane.
- Ranking is computed by the database instead of loading every player into memory.
- Combat polling dropped from 26 queries over 6 requests per tick to 9 over 2.
- Condom upkeep and storage now line up: each storage level supplies 4, 10, then 20 turns at the crew caps.
- Starting supplies fit a level 1 storage room, so a new player is never over capacity.
- Program.cs split into endpoint groups, response mappers, and support classes.
- Combat strength is one configurable formula rather than four hardcoded copies that could disagree.
- Rebalanced combat: an attacker needs roughly 10-20% more armed crew instead of 36-80%, and round
  resolution is configurable. Previously a fully built defender needed 34 attacking thugs against a
  crew cap of 25, so they could not be beaten at all.
- The drawn-round band narrowed from 10% to 6%, so a modest edge produces a result instead of six
  drawn rounds and no loot.

### Removed
- `/api/admin/cheats`, which could only add resources, only to the acting admin, and left no audit record. Its quick grants now work on any player through the audited adjust endpoint.

### Fixed
- Admin endpoints returned 302 to an HTML page instead of 403 for a non-admin.
- Hideout tuning tables were bound twice, so edits to appsettings had no effect.
- Bots could not restock once their supply targets exceeded storage capacity.
- Target search was case-sensitive after moving the filter into the database.
- The attacker cooldown was not enforced on the live mission path at all.
- Simultaneous attackers bypassed defender protection entirely, since protection is only set once a
  mission finishes.

## 0.2.1

### Added
- Live combat mission schema with `CombatMissions` and `CombatMissionEvents`.
- Assigned-crew attack launches for pimps, thugs, and weapons.
- Combined Combat page with target scouting, active missions, round updates, morale, remaining attackers, and recent results.
- Combat mission resolver for travel, fight rounds, return travel, and final history logs.
- Combat crew availability in dashboard responses.

### Changed
- Attacks now use available/committed crew instead of one global pending attack.
- Players can run multiple attack missions if they have enough free pimps and crew.
- Defenders use home crew while their outgoing crews are away.
- Health check, browser UI, package metadata, and README now report 0.2.1.

## 0.2.0

### Added
- Player attack endpoint with turn cost, attacker cooldown, defender protection, delayed mission resolution, loot, and combat losses.
- Pending combat timing columns and resolver for attacks that finish after their travel timer.
- Server-side mission lock preventing street work and stacked attacks while an outgoing attack is pending.
- Combat resolution service using crew, weapons, pimps, morale, and configurable randomness.
- Recon attack button for inspected targets.
- Combat History panel showing recent attacks and defenses.
- Combat History pending-state display with ETA refresh.
- Street page mission-lock notice while the crew is out attacking.
- Backend combat rule tests for self-attacks, protected targets, loot, turn spending, and log creation.

### Changed
- Health check, browser UI, package metadata, and README now report 0.2.0.
- Combat loot tuning now allows a configured 0% loot rate to steal nothing.

## 0.1.12

### Added
- Combat schema migration with player protection timestamps and `CombatLogs`.
- Read-only combat status contracts for dashboard, target recon, and player profiles.
- Authenticated `/api/game/combat/logs` endpoint for future attack history.
- Browser combat protection and eligibility hints in Overview and Target Recon.

### Changed
- Health check, browser UI, and package metadata now report 0.1.12.

## 0.1.11

### Added
- Authenticated target-recon endpoint with search by player name or city.
- Public player profile endpoint with rank, net worth, visible economy, recent public activity, and combat-readiness hints.
- Browser Target Recon panel for searching and inspecting future combat targets.
- Stable random AI brains that make rivals manage resources, spend cash, ignore morale, build crew, run product, or bank differently.
- Browser app-shell redesign with page navigation for Overview, Street, Crew, Market, Recon, World, and Admin.

### Changed
- Leaderboard and target recon now share the same server-side rank calculation.
- Target recon now shows AI personality labels for bot rivals.
- Health check, browser UI, and package metadata now report 0.1.11.

## 0.1.10

### Added
- AI crew-morale management decisions for hoe cut, supplies, weapons, and management capacity.

### Changed
- AI rivals pause expansion and street work when morale or crew coverage needs recovery.
- Health check, browser UI, and package metadata now report 0.1.10.

## 0.1.9

### Added
- Admin Control Center button for turning automatic AI on or off at runtime.
- Admin automation status in the overview payload.

### Changed
- Automatic AI now starts disabled by default and waits for an admin toggle.
- World News excludes store purchases so global activity stays focused on meaningful empire movement.
- Health check, browser UI, and package metadata now report 0.1.9.

## 0.1.8

### Added
- Configurable hosted service for automatic AI bot progression.
- `Bots` configuration section for enabling automation, setting tick interval, and controlling rounds per tick.

### Changed
- Automatic bot ticks run one simulation round and rely on per-bot cooldowns so actions are staggered over minutes.
- Health check, browser UI, and package metadata now report 0.1.8.

## 0.1.7

### Added
- Admin-only AI progression endpoint that runs bot economy rounds.
- Bot simulation service that makes AI rivals buy supplies, hire crew, work streets, produce product, sell inventory, and bank cash through the same economy rules players use.
- Admin Control Center controls for running AI progression rounds.

### Changed
- AI rivals now pace turn spending more like players by keeping a turn reserve, making smaller buys/hires, and running at most one major turn-spending action per round.
- AI rival action logs use real action timestamps while per-bot cooldowns decide whether automatic bots are due to act.
- Health check, browser UI, and package metadata now report 0.1.7.

## 0.1.6

### Added
- Account-level AI player flag.
- Admin-only AI rival seeding endpoint for pre-0.2.0 combat testing.
- Admin Control Center AI rival seeding controls.

### Changed
- Bot accounts cannot log in and are counted separately in the admin overview.
- Legacy 0.1.0 economy columns, including old happiness fields, are removed after their values are copied into the 0.1.1+ schema.
- Health check, browser UI, and package metadata now report 0.1.6.

## 0.1.5

### Added
- Authenticated global world-news endpoint backed by action logs.
- Browser World News panel showing recent public activity across players.

### Changed
- Health check, browser UI, and package metadata now report 0.1.5.

## 0.1.4

### Added
- Persistent account-level admin flag.
- First registered account is promoted to admin automatically.
- Migration that promotes the oldest existing account to admin for development databases.
- Admin-only `/api/admin/overview` endpoint with account/player totals, cash totals, net worth totals, morale averages, and active economy configuration.
- Admin-only cheat endpoint for audited balance testing grants.
- Browser Admin Control Center panel for admin accounts.
- Admin Control Center cheats for cash, turns, crew, inventory, product, and morale.

### Changed
- Health check, browser UI, and package metadata now report 0.1.4.

## 0.1.3

### Added
- Direct crew hiring and firing for pimps, hoes, and thugs.
- Configurable crew hire costs, morale hiring requirements, firing penalties, and transaction limits.
- Dashboard crew report with management capacity, armed-thug coverage, max-action supply needs, and projected supply reserve cost.
- Browser Crew Management panel.

### Changed
- Health check, browser UI, and package metadata now report 0.1.3.
- Hoes and thugs require minimum morale before additional crew can be hired.

## 0.1.2

### Added
- Configurable street income, recruit, found-item, production, action-limit, and morale tuning tables.
- Structured action-result breakdowns for street work, production, sales, store buys, banking, and crew settings.
- Browser display for compact server-calculated action breakdowns.
- Lightweight backend rule-check runner.

### Changed
- Health check, browser UI, and package metadata now report 0.1.2.
- Browser action buttons now respect the server-provided max action turns.
- Registration, login, product, and store input validation now handles missing string fields safely.

### Fixed
- Initial EF migration now creates and drops the 0.1.x schema instead of only updating the model snapshot.

## 0.1.1

### Added
- Pimps, hoes, and thugs as separate crew roles.
- Separate hoe and thug morale.
- Configurable hoe payout percentage (10-80%).
- Pimp management capacity (10 hoes per pimp).
- Condoms, beer, weapons, weed, and coke inventory.
- Weapon coverage pressure for thugs.
- Cash-on-hand and bank balances.
- Deposit and withdrawal actions.
- Weed and coke production.
- Fixed-price product selling for early balancing.
- Generic street-store catalog and buy endpoint.
- Richer action-log deltas for all new resources.
- Empire-status panel in the browser UI.

### Changed
- Replaced the 0.1.0 Workers/Enforcers/Supplies economy.
- Reworked scouting into the `Work the Streets` action.
- Net worth now includes banked cash, crew roles, store inventory, and product.
- Leaderboard now ranks against the 0.1.1 net-worth formula.
- Dashboard version updated to 0.1.1.

### Compatibility
- `/api/game/scout` remains as a temporary alias for `/api/game/street`.
