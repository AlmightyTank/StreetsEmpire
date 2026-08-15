# Changelog

## 0.2.4 (in progress)

### Changed
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
