# Changelog

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
- Morale trend arrows on the overview and crew pages, measured over a configurable window, with the
  exact movement on hover and no arrow at all when there is nothing recent to compare against.

### Changed
- World news now reports fights, buildings, arrivals, and only the money and crew moves large enough to
  matter, instead of every action anyone took.
- Turn accrual, hideout builds, and lab output are settled together by one player clock rather than a
  turn refresh repeated across seven endpoints.
- Every hideout upgrade, room as well as tier, is paid from the bank first and cash on hand second.

### Fixed
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
