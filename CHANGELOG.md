# Changelog

## 0.2.5 (in progress)

### Fixed
- The raw action breakdown is admin-only. Every action popped a table of internal keys and unrounded
  figures at every player - "Item Key / condoms", "Unit Price / $10" - which is a debugging aid and
  reads like one. Players keep the summary sentence, which is written for them.

### Added
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
