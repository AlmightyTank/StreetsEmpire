# Street Empire 0.1.1

A playable browser-game foundation inspired by the turn-based economy and crew-management loop of classic browser crime/empire games.

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

PvP, travel, organizations, player-to-player markets, and territory are intentionally not part of 0.1.1.

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

### 1. Start PostgreSQL

From the repository root:

```powershell
docker compose up -d
```

### 2. Create the database migration

For a brand-new database:

```powershell
cd Server\StreetEmpire.Api
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
```

If you already created a 0.1.0 database and still have its EF migration files, create an upgrade migration instead:

```powershell
dotnet ef migrations add Economy_0_1_1
dotnet ef database update
```

Because the 0.1.1 player schema replaces the old Workers/Enforcers/Supplies fields, this is still an early-development schema. If there is no save data you care about, deleting the development database and generating a fresh `InitialCreate` migration is the simplest path.

### 3. Run the API

```powershell
dotnet run --urls http://localhost:5080
```

Health check:

```text
http://localhost:5080/api/health
```

It should report version `0.1.1`.

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

## API added in 0.1.1

```text
POST /api/game/street
POST /api/game/production
POST /api/game/product/sell
GET  /api/game/store
POST /api/game/store/buy
POST /api/game/bank/deposit
POST /api/game/bank/withdraw
PUT  /api/game/crew/settings
```

`POST /api/game/scout` is retained as a temporary compatibility alias for the new street action.

## Important server rule

The browser never determines money earned, product produced, recruiting results, morale, desertion, turn costs, prices, or net worth. The client submits the player's intended action; the ASP.NET API validates and resolves it.

That rule becomes especially important once PvP and a player market are introduced.

## Proposed 0.1.x path

- **0.1.2 — Economy tuning:** configurable street/recruit/production tables, stronger balance controls, and better action breakdowns.
- **0.1.3 — Crew depth:** hiring/firing controls, deeper happiness requirements, and crew expense reporting.
- **0.1.4 — Game administration:** admin configuration and economy controls.
- **0.1.5 — World activity:** notifications and a global activity/news feed.
- **0.2.0 — War:** player search, attack/defense strength, combat, theft, losses, protection windows, and attack logs.
