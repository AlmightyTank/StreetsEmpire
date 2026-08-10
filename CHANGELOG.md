# Changelog

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
