# Street Empire

A playable browser-game foundation inspired by the turn-based economy and crew-management loop of
classic browser crime/empire games. An ASP.NET Core API owns every rule; a React client draws it.

Release notes for every version live in [CHANGELOG.md](CHANGELOG.md).

## What is in it

- Turn-based street work, production, and a fixed-price store, all resolved on the server.
- Crew of pimps, hoes and thugs, with separate morale, supply upkeep, and weapon coverage.
- A hideout with four tiers and seven upgradeable rooms — storage, safe, weed and coke labs, workshop,
  lookout and intelligence centre — that caps how big an empire can get.
- Eight towns with their own prices, risk and ground, travel by timed flight, and mule runs.
- Player-versus-player combat: raids, drive-bys, jackings, infestations and poaches, with shields,
  cooldowns and anti-farm protection.
- Forty-eight pieces of territory, six per town, garrisoned and developed over five levels.
- Crews: ranks, dues, a treasury, a shared thug pool, pacts, assist calls and wars.
- A player market, town contracts, and named traders whose stock and standing differ by town.
- A casino district with slots, blackjack and roulette.
- Seasons: a world reset, on a switch, that keeps the account and the record.
- AI rivals that play the same economy on their own schedules and personalities.
- Accounts with email or Discord sign-in, verification codes, password reset and recovery codes.
- An admin panel: player search and adjustment, audit trail, oversight dashboards, runtime tuning,
  maintenance mode and AI rival controls.

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

On Windows, once PostgreSQL is running and client dependencies are installed, `.\start-dev.bat` from
the repository root opens the API and the Vite client in separate windows. The same steps by hand:

```powershell
# 1. Start PostgreSQL
docker compose up -d

# 2. Apply migrations (first run only; the app also migrates on startup)
cd Server\StreetEmpire.Api
dotnet tool install --global dotnet-ef
dotnet ef database update

# 3. Run the API
dotnet run --urls http://localhost:5080

# 4. Run the client, in another terminal
cd Client
npm install
npm run dev
```

The API is at <http://localhost:5080> and the client at <http://localhost:5173>.
`GET /api/health` reports the version from `VERSION` and the commit the image was built from.

## Verification

```powershell
dotnet build StreetEmpire.sln
dotnet run --project Tests\StreetEmpire.Tests\StreetEmpire.Tests.csproj
cd Client
npm run build
```

## Configuration

### `.env`

Nothing secret is committed. Copy the template and fill in what you need:

```bash
cp .env.example .env
```

`.env` is gitignored; `.env.example` is the committed copy, and a test fails the build if any key in
it ever carries a value. Key names are the `appsettings.json` paths with `__` (two underscores)
wherever the JSON nests — `Auth__Email__ApiKey` is `Auth:Email:ApiKey` — so anything in
`appsettings.json` can be overridden from `.env`, not only the secrets.

[`DotEnv`](Server/StreetEmpire.Api/Support/DotEnv.cs) reads the file into the process environment
before the builder runs, walking up from the working directory so one `.env` at the repository root
serves both `dotnet run` from the root and from inside the project. A value already set in the real
environment is never overwritten. The server logs which file it read and how many settings it left
alone.

### Discord sign-in

Optional; the button is hidden unless both values are set, because a half-configured door sends a
player to Discord and fails them on the way back. Make an application at
<https://discord.com/developers/applications>, add `http://localhost:5080/api/auth/discord/callback`
under OAuth2 > Redirects **and save it**, then:

```
Auth__Discord__ClientId=...
Auth__Discord__ClientSecret=...
```

Discord compares the redirect as a string — no trailing slash, no `https` for localhost. The server
prints the exact string it will send at startup; if `Invalid OAuth2 redirect_uri` comes back, compare
that line with the Discord dashboard. The redirect points at the API rather than the client because
Vite's dev port moves and a moving port cannot be registered.

In production, set `Auth__Discord__RedirectUri` and `Auth__Discord__ReturnUrl` to the real origins,
and put the client's origin in `Cors__AllowedOrigins__0`. Discord will not accept an `http://`
callback for anything but localhost.

The Discord bot (announcements, linked roles) is separate and equally optional:
`Discord__BotToken`, `Discord__ApplicationId`, `Discord__PublicKey`, `Discord__GuildId`.

### Email

Mail goes over [Resend](https://resend.com)'s HTTP API rather than an SMTP server of our own.

```
Auth__Email__ApiKey=re_...
Auth__Email__FromAddress=Street Empire <no-reply@yourdomain.example>
```

The from address has to be on a domain verified with Resend. Their sandbox sender,
`onboarding@resend.dev`, is the shipped default and only delivers to the Resend account's own owner.

**Without an API key, messages are written to the server log instead of sent** — right on a laptop, a
quiet disaster in production, so the server says which of the two it is doing at startup and warns
loudly outside development.

Codes are six digits, sealed by the data protection key ring rather than stored as sent, and tuned
under `Auth:Email`:

| | |
|---|---|
| Lifetime | 15 minutes |
| Wrong guesses | 5, then the code is burned |
| Between sends | 60 seconds, per address |
| Ceiling | `MaxCodesPerDay` (10), per address across verification and reset |
| Retention | `CodeRetentionDays` (7); spent and expired rows are swept daily |

`Auth__Email__SendSecurityNotices=false` turns off the notices sent on every change to a way in. It
exists for load testing against a real provider and nothing else.

### Game tuning

The server is authoritative, and the tuning numbers live in
`Server\StreetEmpire.Api\appsettings.json` under `Game`. Scalars are also editable at runtime from the
admin config panel, layered over the file and reversible to it.

| Section | What it covers |
|---|---|
| `MaxActionTurns` | The ceiling on a single action |
| `StreetAction` | Gross ranges, recruit chances, found-item tables, districts |
| `Production` | Product costs and unit ranges |
| `Morale` | Upkeep rates, management capacity, pressure penalties, desertion thresholds |
| `Crew` | Hire costs, morale hire requirements, firing penalties |
| `Combat` | Turn costs, travel timers, cooldowns, defender protection, randomness, loot and loss rates |
| `AntiFarm` | Net worth floor and ratio, loot decay, protection escalation, incoming attack cap |
| `Hideout` | Tiers, storage rooms, safes, labs, and the offline production ceiling |
| `Bank` | What a trip to the bank costs in turns, and how long paid moves stay free |
| `Arrests` | Sweep odds and scaling, bail cost, time to pay, and the cost of leaving people inside |
| `Strikes` | The four non-raid attacks: costs, odds, and the cross-country drive timers |
| `Mules` | Fares, capacity, keep, and the odds a run is stopped |
| `Carry` | What a player can take with them when they travel |
| `Casino` | Machines, reel weights, paytables, jackpot contribution, and comps |
| `WorldNews` | Feed size, window, and the thresholds that make an action newsworthy |
| `Seasons` | `Enabled`, `StartsAtUtc`, `LengthDays`, and the head start each placing pays |

Seasons deserve a word of warning: `Enabled` is what makes the clock actually roll the world, and
rolling deletes every empire in it. Set `StartsAtUtc` alongside it so the end date is one somebody
chose, and never enable it while the derived end date is already in the past — that rolls the world
on the very next request. Only `Enabled` and `LengthDays` can be changed from the admin panel without
a restart.

## Game reference

### Starting balance

| Resource | Starting value |
|---|---:|
| Cash on hand | $5,000 |
| Bank cash | $0 |
| Turns | 200 / 200 |
| Pimps | 1 |
| Hoes | 3 |
| Thugs | 1 |
| Condoms | 17 |
| Beer | 10 |
| Weapons | 1 pistol |
| Hoe payout | 30% |
| Hoe morale | 100% |
| Thug morale | 100% |

New players start in New York. Turns come back at +2 every 10 minutes against a bank the hideout tier
sets: 200 at the Trap House, then 300, 450 and 650.

### Net worth formula

Net worth is everything a raid could take — the *plunder* — plus the building.

```text
Plunder = Cash on Hand
        + Bank Cash
        + Safe Cash
        + Pimps × $1,000
        + Hoes  × $550
        + Thugs × $1,250
        + Rides × $15,000
        + both stashes: what is on the shelves, and what is carried

Stash   = Condoms   × $10
        + Beer      × $15
        + Moonshine × $15
        + Weed      × $30
        + Cut       × $30
        + Medicine  × $250
        + Poison    × $300
        + Guns, at what the shop charges for each tier
        + Coke × $120 × purity^0.5

Net Worth = Plunder + the hideout, at what it cost
```

Three deliberate choices in there. Product is valued below its fixed sale value, so inventory does not
inflate ranking exactly like liquid cash. A ride counts at what a chop shop would pay rather than the
sticker price, so buying a fleet is not a way to climb the board. And fights are weighed on plunder
rather than net worth, since a hideout is the one thing nobody can take.

## API surface

Around 180 routes under four prefixes. The full set is defined in `Server/StreetEmpire.Api`.

| Prefix | What lives there |
|---|---|
| `/api/auth` | Register, login, logout, password reset, Discord OAuth, cities, providers |
| `/api/account` | Profile, sign-in methods, sessions, recovery codes, notification and privacy settings |
| `/api/game` | Everything a player does: dashboard, street, production, store, bank, crew, hideout, combat, territories, alliances, market, mules, casino, chat, seasons, travel, alerts |
| `/api/admin` | Player search and adjustment, enforcement, audit, oversight, config, live-ops, bots, seasons, titles, updates |
| `/api/world` | The public world news feed |

`POST /api/game/scout` is retained as a temporary compatibility alias for
`POST /api/game/street`.

## Important server rule

The browser never determines money earned, product produced, recruiting results, morale, desertion,
turn costs, prices, or net worth. The client submits the player's intended action; the ASP.NET API
validates and resolves it.

## Deployment

Three containers — the app, Caddy in front of it, and a job that takes dumps — talking to a Postgres
installed on the VPS itself. The app image carries the built client, so one origin serves both.

```bash
git clone https://github.com/AlmightyTank/StreetsEmpire.git streetsempire && cd streetsempire
cp .env.example .env    # DOMAIN, POSTGRES_HOST, POSTGRES_PASSWORD, and the keys you have
./ops/deploy.sh
```

Set the database up first — see below — because there is nothing to connect to until it exists. After
that the app migrates on the way up, so there is no separate step and no `dotnet ef` on the box.

Things worth knowing:

- **Only Caddy faces the internet.** It terminates TLS, gets a Let's Encrypt certificate on first boot,
  renews it on its own, and redirects plain HTTP. The app publishes no host port; Postgres listens on
  loopback and the Docker bridge only.
- **Set `DOMAIN` to the bare hostname** — `streetsempire.dev`, no scheme, no slash — and point its DNS
  at the VPS before starting, since Caddy proves ownership over port 80. Everything else is built from
  it. Then register `https://$DOMAIN/api/auth/discord/callback` with the Discord application; the
  server prints the exact string at startup.
- **Point `www` at the machine too, or delete the www block from `ops/Caddyfile`.** A name Caddy holds
  no certificate for fails the handshake rather than 404ing.
- **The app trusts `X-Forwarded-*`.** Without it the session cookie loses its `Secure` flag behind TLS
  termination, and the sign-in rate limiter partitions every anonymous caller as the proxy. Safe only
  because nothing can reach the app except through Caddy, which is why the switch is off by default.
- **The data protection key ring is on a volume** (`DataProtection__KeyPath`). Unconfigured, it lives
  inside the container, so every redeploy would silently sign out every player and void every code in
  flight — with no error anywhere.

### Postgres on the host

Postgres runs on the VPS rather than as a container so it keeps running when Docker does not, is
patched by the same `apt` as everything else, and stores its data where you can reach it. Match the
major version to the `postgres:` tag on the backup service — `pg_dump` refuses to dump a server newer
than itself, and a drift there is a backup that stops silently.

```bash
sudo apt install -y postgresql-17
sudo -u postgres createuser --pwprompt street_empire
sudo -u postgres createdb --owner=street_empire street_empire
```

Then make it reachable from the containers and nothing else. In
`/etc/postgresql/17/main/postgresql.conf`:

```
listen_addresses = 'localhost,172.28.0.1'
```

and in `/etc/postgresql/17/main/pg_hba.conf`, above the local rules:

```
host    street_empire    street_empire    172.28.0.0/16    scram-sha-256
```

`172.28.0.1` is the VPS as a container sees it — the gateway of the compose network, which
`docker-compose.prod.yml` pins to that subnet so Docker cannot quietly move it. `sudo systemctl
restart postgresql`, then put the same address in `.env` as `POSTGRES_HOST`.

<details>
<summary>Moving an existing database off a Docker volume</summary>

Do this with the app stopped, and delete nothing until the new database has answered a real request.

```bash
# 1. Dump what is in the volume, from the container that is about to stop.
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_dump --username=street_empire --dbname=street_empire --format=custom \
  > /tmp/final.dump

# 2. Everything down. The dump is on the host now.
docker compose -f docker-compose.prod.yml down

# 3. Into the new one. --clean --if-exists makes it repeatable.
sudo -u postgres pg_restore --clean --if-exists --no-owner --role=street_empire \
  --dbname=street_empire /tmp/final.dump

# 4. Count something you recognise before trusting it.
sudo -u postgres psql -d street_empire -c 'select count(*) from "Accounts";'

# 5. Bring it up and sign in as a real player.
docker compose -f docker-compose.prod.yml up -d --build
```

Only then remove the old volume, and keep it for a week rather than a minute. There is no undo:

```bash
docker volume rm streetsempire_street-empire-data
```

</details>

### Backups

A third container dumps the database, checks it, and prunes old ones. It dumps once immediately on
startup, writes to a temporary name and renames on success, and reads every archive back with
`pg_restore --list` before keeping it. It passes `POSTGRES_PORT` explicitly, because on a host 5432 is
whatever answered first.

Dumps land in `./backups` on the host rather than in a Docker volume — a backup that only exists on the
machine it is backing up is not a backup of that machine. Copy them somewhere else. They are
gitignored, because each one is a complete copy of every account, address and hashed password.

Interval, retention and destination are `BACKUP_INTERVAL_HOURS`, `BACKUP_RETENTION_DAYS` and
`BACKUP_DIR` in `.env`.

### Restoring

The dumps are the custom format, so one table can go back without touching the rest.

```bash
# What is in it
docker compose -f docker-compose.prod.yml exec backup \
  pg_restore --list /backups/street_empire-20260827-140346.dump

# One table back, with the app stopped so nothing writes underneath it
docker compose -f docker-compose.prod.yml stop api
docker compose -f docker-compose.prod.yml exec backup \
  pg_restore --host="$POSTGRES_HOST" --port=5432 --username=street_empire --dbname=street_empire \
             --data-only --table=Accounts --disable-triggers \
             /backups/street_empire-20260827-140346.dump
docker compose -f docker-compose.prod.yml start api
```

No password flag: the container carries `PGPASSWORD`.

### Updating

```bash
./ops/deploy.sh
```

That pulls the checkout, waits for the image CI is building for that exact commit, pulls it the moment
it is published, restarts, and waits until the app answers before calling it done. New migrations
apply on the way up; the database and the key ring volume are not touched.

**The VPS builds nothing.** The publish job in `.github/workflows/ci.yml` has `needs: [server, tests,
client]`, so an image exists only for a commit that passed all three. It goes to GHCR under three tags:

| tag | what it is for |
| --- | --- |
| `latest` | the newest green build of main — what `--now` takes, and the fallback when nothing is pinned |
| the version | the number in `VERSION`, which is what a human says out loud |
| the commit sha | the only one that never moves, which is why a plain deploy asks for it by name |

Deploying by sha rather than `latest` is what stops a deploy run a minute after a push from succeeding
with the *previous* commit. It gives up after thirty minutes (`DEPLOY_WAIT_MINUTES`) and touches
nothing when it does.

```bash
./ops/deploy.sh --now                                     # whatever exists right now
./ops/deploy.sh 4f3a91c8d2e5b7a1f0c9d8e7b6a5f4e3d2c1b0a9  # roll back to a specific sha
```

A tag named by hand is never waited for. Pin `IMAGE_TAG` in `.env` to make it stick across deploys.

**One thing to do once, in GitHub.** A package published from a public repository still starts private
and the VPS has no credentials, so set the package's visibility to public under the repository's
Packages > Package settings.

### Bumping the version

```bash
echo 0.3.2 > VERSION
```

MSBuild reads that file into the assembly and Vite bakes it into the bundle, so `/api/health` and the
app's sidebar both follow without anybody typing the number again. Both halves fail quietly when
broken — MSBuild falls back to `0.0.0`, Vite leaves its token unreplaced — so a test reads `VERSION`,
checks it against the version in the assembly, and fails if any of the three files goes back to naming
a number itself.

`/api/health` reports the commit alongside it:

```json
{"status":"ok","version":"0.3.2","build":"0.3.2+8f63b72f0dba6dac2fdafc95f3d7dbeaa74ede3e"}
```

The commit is passed in as a build argument, because the build context carries `Server/` and not
`.git`. CI passes it; a build by hand leaves it off, which is honest.
