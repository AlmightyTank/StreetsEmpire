# Street Empire 0.2.4

A playable browser-game foundation inspired by the turn-based economy and crew-management loop of classic browser crime/empire games.

## What changed in 0.2.4

0.2.4 is in progress. Territory is in; player-to-player markets and organizations are still to come.

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
- Losing ground reaches the player: a line in the arrival summary, an entry in the alert bell, and the
  raider's own row in world news. The loser's notice is written to them in the second person and is
  deliberately kept out of the public feed, where the raider's row already reports the same event.

### Still to come in 0.2.4

- Player-to-player markets and organizations.

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

Travel is intentionally not part of 0.2.4.

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

It should report version `0.2.4`.

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
