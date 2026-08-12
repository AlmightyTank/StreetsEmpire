# Street Empire 0.2.1

A playable browser-game foundation inspired by the turn-based economy and crew-management loop of classic browser crime/empire games.

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

Travel, organizations, player-to-player markets, and territory are intentionally not part of 0.2.0.

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

It should report version `0.2.1`.

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
| Condoms | 25 |
| Beer | 12 |
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

The server remains authoritative, and 0.2.1 keeps the core tuning numbers in `Server\StreetEmpire.Api\appsettings.json` under `Game`.

The configurable tables now include:

- `MaxActionTurns`
- `StreetAction` gross ranges, recruit chances, and found-item tables
- `Production` product costs and unit ranges
- `Morale` upkeep rates, management capacity, pressure penalties, and desertion thresholds
- `Crew` hire costs, morale hire requirements, and firing penalties
- `Combat` turn costs, travel timers, cooldowns, defender protection, power randomness, loot rates, and loss rates

## Verification

```powershell
dotnet build StreetEmpire.sln
dotnet run --project Tests\StreetEmpire.Tests\StreetEmpire.Tests.csproj
cd Client
npm run build
```

## API surface

```text
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
GET  /api/world/news
GET  /api/game/targets
GET  /api/game/players/{playerId}/profile
GET  /api/game/combat/logs
GET  /api/game/combat/missions
POST /api/game/combat/attack
GET  /api/admin/overview
POST /api/admin/cheats
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
- **0.2.2 - Next:** combat balance pass, AI attack behavior, anti-farm protections, and better defender alerts.
