# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## 0.3.2 (in progress)

## 0.3.0

### Added

**Casino District**

- Four slot machines, each with its own symbols, reel weights and paytable. A five-by-three grid
  with nine paylines, bought one at a time.
- A progressive jackpot per machine, fed by a percentage of every stake and paid for three Vaults
  with all lanes bought — roughly one spin in twelve thousand. A drop reaches the world feed on its
  own account, whatever it is worth.
- Comps, rated at a hundredth of the stake on every pull whether it lands or not and held in dollars.
- Blackjack in a pit of two tables: hit, stand, double and split against a dealer who draws to a rule.
- Roulette on two wheels, with inside and outside bets.

### Changed

**Casino presentation**

- Reels stop one at a time, and the machine holds a near-miss reel for an extra beat.
- Wins count up rather than print, and light only the cells that actually matched.
- Paid pulls can award free spins.
- The ledger shows the grid instead of spelling out fifteen symbol names in one column.
- A pull costs a turn, so the casino competes with everything else for time.
- Store standing is earned per ticket (a full ticket at a machine's top stake is worth five) rather
  than per dollar.
- Machine tiles advertise a prize somebody could actually be paid.

**The map, travel and combat**

- **Travel moves the player, not the empire.** A hideout now has a town of its own. The building,
  shelves, safe, crew and fleet stay where they were built; only what a player carries flies with them.
  Carrying capacity is a person's worth.
- **The safe is a place you walk to.** It is separate from cash on hand, costs no turns and no trip,
  can only be opened in the town it stands in, and is what a raid on the house gets at.
- **Ground stays held when you leave town.** It pays out only in its own town, its garrison stays off
  your roster, and it is still raidable. From out of town the only thing you can do to a piece is
  abandon it. It counts against your holding cap wherever it is.
- **Strikes can cross the country, as drives.** A neighbour is still instant; another town is twelve
  to thirty-six minutes each way, paid in turns and a fare. The target gets that long as warning.
  A strike resolves against the house it finds rather than the one it left, and a wasted trip returns
  the crew with their turns and fare kept. Loud work (drive-bys, jackings) loses odds at distance;
  infestations and poaches do not. Hauls have to be driven home, at the town's risk plus the length
  of the road.
- **The lookout watches the road.** It still shaves police odds, and now buys notice without detail —
  four to fifteen minutes of warning by level, and nothing about who or what is coming.
- The hideout reads its own town for everything it does on its own, and heat is charged where the
  crime happened rather than where the criminal lives.
- Raid odds double per point of heat and top out at a coin flip, so a Hunted house is tonight's
  problem rather than one to outrun.
- A police sweep leaves a file open on the house instead of cooling it.
- Standing crew upkeep is half what working costs, so a night away is no longer priced as a night
  on the corner.

### Fixed

- Season rows are matched by player id rather than display name, so a rename or a case change no
  longer loses somebody their own row.
- A street shift is gated on drink that can actually be poured — moonshine is not sold at the
  counter, so an empty still no longer authorises shifts nobody can supply.

## 0.2.7

### Added

**Raid damage and repairs**

- A won raid leaves a room broken behind it, on a house and never on a corner. Everything a raid took
  used to grow back by morning; damaged rooms now stay down until they are paid for.
- Repairs cost a third of the room's build price, take an evening, and run one at a time.
- Nothing is built on top of a wreck, and a raid cannot un-buy an upgrade: the level stays on the
  deeds and the repair hands back exactly what was taken.
- The store and the safe cannot be broken, on purpose.
- Rivals are raided on the same rules and repair on the same bill, oldest damage first.
- The returning-player digest, the alert bell and Next Moves all lead with what is broken.

**Seasons**

- The world can start over. One rule throughout: the empire goes and the person stays. The account,
  sign-in, player name, town, crew membership and every season result recorded survive a roll; cash,
  crew, building, stock, ground, clocks, shields and the crew treasury do not.
- Where everybody finished is written down before anything is deleted, for every player rather than
  only the top.
- Finishing well pays opening cash in the next season only — $50,000, $25,000, $10,000 for champion,
  top three and top ten. It never stacks.
- Off by default (`Game:Seasons:Enabled`), so a live world is not deleted by a date committed months
  earlier. `POST /api/admin/season/roll` ends one by hand and makes the caller type the season's name.
- Seasons has a page of its own with three tabs — This Season, Finished, Your Record — served by
  `GET /api/game/seasons` and `GET /api/game/seasons/{number}`. A finished season's table is read from
  the record rather than recomputed, and carries your own line whether or not you placed in it.
- The countdown counts days; it had been reading `719h 04m`.

**Crew wars**

- Crews can declare war, with a clock, a score and a pot. Declaring costs the treasury $250,000 and
  runs 48 hours.
- Nothing new is scored: a raid won is three, a raid turned away is two, taking ground is five.
- The winner takes the stake back plus 15% of the losing treasury, capped at five million. It takes
  six points to win anything, and a level score sends the stake home.
- No war lifts any protection — the wealth floor, the ratio and the shields all stand.
- A truce must be ended in the open before a war can be declared.
- Crews carry a public war record, next to who they are currently fighting.

**Territory development**

- Ground can be worked up over five levels — $150,000, $600,000, $2.4M, $9M, $30M — each with turns,
  a build timer and a hideout tier behind it. Every level raises that ground type's own effect.
- The level belongs to the ground rather than to whoever holds it, which is what gives a raid a target
  that is not simply the biggest house in town.
- What survives being taken is half, rounded down, and never more than the winner's own building could
  have built. Walking away razes it.
- Development defends itself, so worked ground is not simply a target painted on its holder.
- Rivals work their ground up on the same ladder, breadth first and then depth.

**Arrests**

- The law takes people off the street. The choice is bail or leave them, and bail is deliberately
  dearer than hiring the same head again.
- Odds scale with the crew actually working and with the district. Rivals answer in character.
- Being in a cell is a third state for a pimp, distinct from being lost.

**Scouting, profiles and account security**

- A rival's numbers have to be scouted now rather than being free. What comes back depends on the
  intelligence centre's level: 1 is how hard they hit, 2 what they hit with, 3 what is worth taking,
  4 where they are soft. Unknown reads as a dash, never a zero, and the level is stamped at the moment
  of looking.
- Any player name is clickable — leaderboard, feed, chat, crew roster, transfers, titles board — and
  opens their card over whatever you were looking at. You can open your own.
- Recovery codes: ten one-time codes made behind the current password, shown once, stored hashed.
  Making a new set voids the old one and sends a notice.
- The account page lists where you are signed in and can end one session without ending the rest.
  Ending a session or disconnecting Discord costs the current password.
- You can choose which daily title leads on your profile.

**Privacy and notification settings**

- Direct messages can be opened to allied crews, not just your own.
- Recent activity on a profile can be turned off.
- The alert bell has per-category switches (combat, crew, market), separate from the email ones.
- A sale reaches the seller's bell, and an assist call now tells the crew it is asking.
- Per-device display preferences, preset profile banners, an account age on profiles, and a manual
  Discord refresh with a last-synced stamp.

### Changed

**Traders, contracts and standing**

- **The shop is a person.** Every town's counter is a named trader who greets you by your standing,
  and each is a different shop: Auntie Vasska in Chicago is the cheapest in the country at 84% and
  nearly the emptiest.
- **A counter is a shop with stock in it,** running on two clocks: what a trader sells turns over at
  midnight Central, and buying takes stock off the shelf until it is restocked. A trader's range is
  rolled daily from what they can get hold of, at three lines in four, and depth is held in money
  rather than count.
- Condoms, beer and a pistol are always on every shelf and never run out. Rifles exist in four towns
  out of eight, so the top of the standing ladder is something you travel for.
- Out of stock is what posts a job, and filling it puts the line back on sale. No more than two lines
  can be dark in a town at once.
- A closed shelf row says which of four reasons it is: a rung you have not reached, a line this trader
  never carries, one that did not come in today, or one they have sold out of.
- **The trader's board and the town's contracts are one board.** A book of sixteen to eighteen jobs
  per town, dealt as a remembered hand of three — one from the dealer, one from a town buyer, one
  open. You can ask what else is going, at a cost in standing, but never enough to cost a rung, and
  never on a job you have already put goods into.
- Rep per job went up about forty percent. Instalments follow the contract board's rule throughout:
  cash per delivery at the town rate, the premium and the standing whole at the end.
- **The store keeps a reputation, and the gun rack is behind it.** Five rungs — Nobody, Regular,
  Trusted, Connected, Made — gating shotguns, SMGs and rifles, at 300 / 3,000 / 15,000 / 50,000 rep
  ($30,000 / $300,000 / $1.5M / $5M of trade). Standing also takes 2/4/6/8% off every price.
- Rep is earned at a hundredth of a point per dollar traded, on a Wanted board of workshop goods the
  trader pays over shelf price for, and at an Investment counter that buys standing and nothing else.
- Everything above the pistol costs more, because the gate and the price are one decision; the pistol
  did not move. Forging got much cheaper against those prices. The workshop is deliberately not gated.
- Nobody already playing loses a gun they could buy yesterday — existing players' standing is read off
  the rack they already own.
- The player market has a tab of its own, Flea, alongside the town's Shop counter.
- New players start in New York rather than in whichever town the alphabet put first.

**Economy and pacing**

- **The building holds the turn bank, and the rate never moves.** Turns still come back at 288 a day;
  the bank is 300 at the Warehouse, 450 at the Nightclub, 650 at the Penthouse, and unchanged at the
  Trap House. Income per hour is untouched; the cost is heat.
- **The district picker is a choice rather than a gross ladder.** Five districts with five sets of
  multipliers; the neutral default is no longer strictly worse at every crew size, and districts that
  pay in crew pay for it out of the take.
- The street turns up more recruits for a bigger house, sized against the crew ladder and read off the
  crew the shift started with.
- Free recruitment is a trickle: base rates of 0.0024 / 0.024 / 0.008 a turn for pimps, hoes and thugs.
  At full reach a house finds half what every house used to; a small one, a fifth.
- Being short of pimps or guns is charged as a rate against the share of the crew in that state — 0.7
  and 1.0 a turn — rather than per head.
- **A trip to the bank costs turns,** charged on the visit rather than the amount or direction, with
  everything moved on one visit on one fare. Cash swept over the safe is still banked for nothing, and
  paying for a room is not a trip to the bank. Rivals price the walk against their own crew.
- A shift stops reporting the things that did not happen — no more "Found 0 condoms, 0 beer, 2 weed".

**Interface**

- Anything that tells you where to go now takes you there, down to the panel: next moves, the opening
  ladder, update action links and every step of the walkthrough.
- Signing up asks for one name instead of two, and a collision says "that name is already taken"
  rather than naming a column.

**Build, deploy and operations**

- The version is written down once, in `VERSION`, read by MSBuild and Vite. A test fails the build if
  any of the three files goes back to naming a number itself.
- `/api/health` reports the commit alongside the version.
- CI builds the image and the VPS pulls it, under three tags: `latest`, the version, and the commit
  sha. `ops/deploy.sh` waits for the build for that exact sha rather than racing it with `latest`.
- Postgres runs on the VPS rather than in the compose stack, with the compose network pinned to a
  fixed subnet so the containers and `pg_hba.conf` cannot disagree about an address.
- The backup job passes the port explicitly, since on a host 5432 is whatever answered first.
- Shell scripts are pinned to LF in the working tree, not only in the repository.
- The action log and the combat log have a retention, and the sweep never takes a fight that has not
  happened yet. Container logs are bounded, since a full disk stops Postgres writing.
- Last-seen moves at most once every five minutes rather than on every request.

### Fixed

- Rivals in a morale hole had been buying a good called "weapons" since guns got tiers.
- The two morale buttons went grey without saying why, and the party never said what it was worth.
- A finished building did not raise the ground you could run until something else happened.
- A full turn bank stopped the client asking the server anything at all.
- Starting work on your own ground reported itself in the bell as losing it.
- Thirty-eight player-facing strings formatted money with `:C0`, which asks the ambient culture what a
  currency looks like — caught by CI failing on Linux.
- `www` failed with a TLS error rather than redirecting, because Caddy held no certificate for it.

## 0.2.6

### Added

**Deployment**

- A Dockerfile and a production compose file: the app, Caddy, and a backup job. The built client ships
  inside the API image and is served from the same origin, so CORS has nothing to do, the session
  cookie is plainly first-party, and Discord has exactly one callback address.
- Caddy terminates TLS, gets a Let's Encrypt certificate on first boot, renews it itself, and redirects
  plain HTTP.
- The app trusts `X-Forwarded-*` when told it is behind a proxy — without it the session cookie loses
  its `Secure` flag and the sign-in rate limiter partitions every anonymous caller as the proxy. The
  known-proxy list is cleared rather than enumerated, since a Docker bridge address is not knowable
  in advance.
- The data protection key ring is kept on a volume. Unconfigured, every redeploy would have silently
  signed out every player and voided every code in flight.
- The database is backed up by a third container on a schedule, from the same `postgres` image the
  server runs, with dumps landing on the host rather than in a Docker volume. The restore was tested
  rather than assumed.

**Accounts**

- **Two more ways in, and a page to manage all three.** An email address is a second name to sign in
  under; Discord signs a player straight in and can make an account from scratch. The account page is
  a full tab, split into Profile, Sign-in and Security.
- Signing up needs either an email address or Discord, and it cannot be undone afterwards: removing an
  address is refused unless Discord is connected, and disconnecting Discord unless there is a confirmed
  address. An account can never end up with no way in, and the page says which door is the last one
  standing.
- Signing up through Discord also asks for an optional email, since such an account has no password.
- **Email verification by six-digit code.** An unconfirmed address cannot be signed in with. Codes come
  from `RandomNumberGenerator`, are sealed by the data protection key ring rather than stored as sent,
  live fifteen minutes, allow five wrong guesses, and are rate-limited per address. Verification starts
  at sign-up and again on every address change.
- **A forgotten password can be reset.** The only unauthenticated flow in the game, so it never reveals
  whether an account exists: the same sentence for a real name, a real address, a typo and a fishing
  expedition, and a wrong code costs an attempt against the sign-in limiter. Success ends every other
  session. Reset and verification codes share a table and are told apart by a purpose column.
- Mail goes over Resend's HTTP API rather than an SMTP server of our own. With no provider key,
  messages are written to the server log, and the server says at startup which of those it is doing.
- **Every change to a way in emails the account** — password set or changed, Discord connected or
  disconnected, sessions ended, address changed or removed, and a Discord sign-in. An address change
  tells the address being left behind and names where the account went. A notice reports a change and
  never carries it. A test walks every value of the change enum and fails if one has no copy of its own.
- Changing a password ends every other session and keeps the one that changed it.
- The starting player is built in one place both sign-up doors call, with a test that they agree.
- The admin panel can see and search the email address, whether it was confirmed, the Discord handle
  and the Discord snowflake.
- Credentials come from a `.env` file at the repository root. `.env.example` is the committed template
  and a test fails the build if any key in it ever carries a value.
- Spent and expired codes are swept daily.

**Crews**

- Members of the same crew can send each other cash, thugs, or anything on the trade list, and help
  sent to an ally can be taken back.
- Crews can make pacts with other crews — requested, answered, cancellable from either side. A pact is
  a truce enforced at both places a fight can start.
- A crew under attack opens an assist call to every crew it has a pact with. Calls close themselves
  when the fight ends.
- Holding every piece of ground in a town gives a crew extra defending thugs there, set per city.
- Garrisons cap at 50 thugs, raids at 100, and the garrison bonus at 85% — with a test that a fully
  buffed garrison on its cap holds against a maximum raid.
- Transfers, pacts and assist calls have tests, the first in the suite to run against a database.
  The conservation ones were checked by breaking the code on purpose.
- A failing test now reports the inner exception rather than EF's outermost "see the inner exception".

### Changed

- An unconfirmed address can no longer be signed in with. It still holds the address against other
  accounts, so nobody can claim one twice.
- The account page counts two ways in — password and Discord — not three.
- A copy pass over everything this release touched, including a town picker that implied the choice
  was permanent and a register form that promised nothing would ever be sent to the address.

### Fixed

- A session opened in the same second as a password change or reset survived it.
- Changing your password signed you out of your own password change: the watermark and the cookie
  ticket are now both floored to the second.
- Changing your email address inside a minute of the last code sent no code at all, because the
  cooldown was measured per account rather than per address.
- Two people registering the same name at the same moment got a 500 with a stack trace in it.
- An unauthenticated caller could aim a message a minute at somebody else's inbox forever;
  `Auth__Email__MaxCodesPerDay` is now the ceiling.
- The player market opened on a good that does not exist, as did an admin quick-grant and a bot action
  dropdown. A test now checks every good the client names against the server's lists.
- Several layout fixes: five cards using half a page each, a crew name running into its own
  description, and a `gtc-*` override that never overrode anything.

### Security

- The Discord round trip carries a nonce in the signed state, also written to a cookie and compared on
  the way back, so a login somebody else finished cannot be replayed into your session.
- The return origin is named by the client and checked against the origins CORS already trusts, plus
  any localhost port in development.
- Changing the email address costs the current password.
- Verification codes are sealed, single-use, and counted against before they are compared.
- The verification email escapes the player name before putting it in the HTML body.

## 0.2.5

### Added

**Chat and messaging**

- Chat in three rooms — the whole board, your town, and your crew — as a window in the corner rather
  than a panel on a page. Minimised it still listens, at a slower interval, and carries an unread count.
- Scope is written onto the line, so a Detroit message stays a Detroit message once its author moves.
  An unknown channel falls to Global.
- Three seconds between messages, read off the table so a restart or a second tab cannot sidestep it.
  Polled every eight seconds, and only while the tab is in front.
- Direct messages as a fourth tab, then rebuilt as conversations: group messages, a people search, and
  up to three windows open at once, remembered across page changes and reloads.
- Membership is the whole security model — no query reaches a conversation the asker is not in, and
  reading one you are not in refuses rather than coming back empty. Unread is a real per-person
  watermark.
- Blocking, deliberately narrow: it silences somebody rather than shielding you from them, cuts both
  ways, and gives the same refusal in both directions so it never says who did the blocking.

**Crews (alliances)**

- Crews with four ranks — Soldier, Enforcer, Underboss, Boss — where the boss sets a minimum rank per
  power and you can only act on somebody strictly below you. Promotion stops below the top.
- One door setting with three states: open to anyone, by application, or invitation only. Invitations
  and applications are one table read from opposite ends, re-checked when accepted rather than trusted
  from when they were sent.
- A crew is people who have agreed not to rob each other, and that is enforced rather than asked for.
- Dues: a founder-set share of every member's shift, taken off the gross beside the hoe cut.
- Crew ranks are the sum of members' net worth, off the same expression the individual board ranks by.
- A shared thug pool bought out of the treasury, finite, where a member may field at most as many
  borrowed thugs as they brought of their own. Losses fall across the whole line in proportion.
- Six members rather than the source game's twenty.

**Attacks, districts and content**

- Four more attacks beside the raid: a drive-by (needs a low-rider), a jacking (takes their rides),
  an infestation (costs poison, reaches three hoes a dose), and a poach (buys their hoes away with
  coke). Two shields on two clocks; the defence alert says which of the five hit you.
- Weapon tiers: pistols, shotguns, SMGs and rifles at $250 / $1,250 / $2,500 / $5,500. Any gun covers
  a thug for morale. The workshop makes pistols and shotguns from the start and SMGs at level 2, but
  never rifles. Losses and overflow take the cheapest guns first, and a raid carries a recorded mix.
- Medicine and condoms by the case on the bench; poison bought at the counter or made in the mix house.
- Five named scouting districts, each best at something and costing something, with a test that fails
  if any is better at something and worse at nothing. Tiles are written from the numbers. Rivals pick
  from what they are short of.
- A shrine: pray to the pimp gods once a week, for something that is never money. The ask is worked out
  from the player and the week rather than stored.
- Seven daily titles, held by whoever leads a category over the last day, shown on rows and profiles,
  with a floor to stop a quiet day handing out names for one of anything.
- A walkthrough that shows the game rather than describing it: six steps, one thing lit at a time.
- A Getting Started ladder covering the verbs the game never introduced, and Next Moves rewritten as
  advice rather than a status readout. Both are read from the world rather than stored.
- Contracts: named buyers in a town who want a set amount by a deadline and pay over the counter, with
  purity floors on some coke orders. A town posts orders at a pace, and rivals fill them too.
- Per-city leaderboards, off the same definition of who outranks whom, narrowed to a town.
- Three more towns — Las Vegas, Atlanta and Houston — bringing the map to eight, with nine more rivals.
  New ground seeds itself by name the first time the territory page is opened.
- A town's risk reaches the daily loop, so the same stash draws more notice in a watchful town.
- AI rivals hold grudges, read from the fights that actually happened rather than kept as a score,
  with a feud headline in world news.
- A Lookout room, closing the first tier's dead zone, with a test that no two rungs of the ladder are
  more than two sessions of earning apart.
- New players get the full 200 turns, and turns come back up to three times faster below a quarter of
  a million net worth.
- The test suite reads the settings the server actually ships, after half the config turned out to
  live somewhere no test had looked.

### Changed

- **The game takes weeks now.** A player spending every turn finished everything in fourteen days; the
  same player now takes thirty-six, and one who logs in once a day takes fifty. The curve is graduated
  rather than multiplied flat, and crew hire costs are untouched.
- **Ten rooms became eight.** The workshop, still and mix house were the same room wearing three signs;
  nobody loses what they built.
- A lab upgrade says what it actually returns, since later levels buy about half the output per pound
  of the level before.
- **A hideout counts towards net worth,** at cost, so an upgrade is neutral on the board. Fights are
  weighed on net worth without the building, since a hideout is the one thing nobody can take.
- **A crew is capped by whichever runs out first:** the room the building has for them, or the supplies
  the store can put behind them. The storage ladder is the crew ladder, and a refusal names whichever
  cap actually binds. Pimps are deliberately not on that list. Existing hideouts all got roomier.
- Orders can be filled a bit at a time. Deliveries pay the town's ordinary rate as they happen and the
  premium arrives whole at the end; the first delivery claims the order, and purity is re-checked on
  every delivery.
- **The interface works on a phone.** Navigation moved to a bottom tab bar, the name and alert bell sit
  beside the page title, the status strip no longer hides cash and heat behind a scroller, the alliance
  panels were rebuilt, every control clears 44 pixels, and the page pays back a notch with safe-area
  padding.
- **A design system, replacing loose decisions.** 175 hex literals became a named palette; Inter is
  actually loaded; a type scale of eight steps replaces 33 hand-picked sizes; three weights replace
  six; spacing snaps to a 4px rhythm; figures use tabular digits; body copy caps at 68 characters.

### Fixed

- The list of your conversations threw every time it was asked for, and a window whose conversation
  had gone sat on "Loading" forever.
- Pistols were missing from the street's quick-buy, which still asked for a good called "weapons".
- Moonshine and cut counted for nothing towards net worth. A test now walks every good a player can
  hold and insists that holding some of it is worth something.
- Poison could not actually be bought, having gone on sale with no case behind it in either switch.
- A strike said no after the click rather than before it, because the menu was built from the attacker
  alone. A profile and the target list beside it gave different anti-farm answers.
- "Run a production shift" sent you to the street, where there is no production.
- The storage ladder the server actually ran on was still the old one, since appsettings wins over the
  code defaults.
- The client copy went through line by line, all 355 strings: twenty-seven refusals written in
  form-validation register, two dialects on one screen (defence/defense, Intelligence Centre/Command
  center), and a defender told "somebody put something through your house".
- The walkthrough guessed its own height at 200px when placing its card.
## 0.2.4

### Added

- **Territory.** Six pieces of ground per town, held by garrisoning thugs who count as away from home,
  capped per hideout tier. Four ground types, each a percentage on an activity the player still spends
  turns on: Corner for street income, Docks for production yield, Club for passive morale recovery,
  Stash House for raid haul. Empty ground is claimed with turns and a garrison; held ground is taken by
  a raid that fights the garrison rather than the holder's house. A pimp can be posted to run a piece
  and adds their bonus to its defence if they are an Enforcer.
- A Territory page showing your town's map, who holds what, and why a piece cannot be acted on. Ground
  changing hands reaches the digest, the alert bell and world news, and a raid you beat off tells the
  holder what it cost the garrison.
- **A player-to-player market:** one global board, escrowed stock, partial fills, a house cut, and
  payouts into the seller's bank. AI rivals buy and sell on it.
- **Travel between towns,** as a flight that takes real time and cannot be acted from. Players choose
  their town at sign-up. Each town prices weed and coke on its own band, and a run can be stopped on
  the way in for a fifth to three fifths of what is carried. A Travel panel reads every town's prices
  as a change against the one you are in.
- **Mule runs.** An intelligence centre gates them; a run costs fewer turns than travelling yourself
  but takes real time, locks up crew who earn nothing while away, and is paid for in cash up front.
  Mules buy at the destination's price. Everything an outcome depends on is frozen at launch, runs
  settle on the clock three ways, and cargo that will not fit is dumped with a notice saying why.
  A Mules page quotes profit rather than gross. Rivals run mules in character.
- **Heat.** A raid rolled per hour above the floor takes half of every pile and a fine capped at cash
  on hand. Heat cools on its own, and sits in the status strip reading Quiet, Noticed, Watched or
  Hunted.
- **Moonshine and cut,** made by a still and a mix house, both needing a Warehouse or better.
- **A workshop** that makes weapons from turns and materials below the store price, giving the market
  a good with real demand.

### Changed

- **Coke has a purity, and cutting is a trade instead of a printer.** Purity is a weighted average of
  everything in the room, every way coke arrives blends rather than counts on, and listings carry the
  purity they were escrowed at so the board cannot be used to launder strength. Net worth values coke
  by strength, in the database as well as in memory. Producing coke is now roughly three times better
  per turn than making cut and stretching. Cut is spent by a step of its own, and a batch stops at
  whichever limit binds first and says which.
- **Heat rose far too fast.** Street work is now 0.15 a turn — a whole bank is about 30, and a night
  of laying low clears it — with per-unit weights cut to roughly a third.
- **AI rivals play in sittings rather than on a metronome.** Each keeps its own hours drawn from its
  seed, so the world has a rush hour and a quiet stretch. How often a rival plays comes from its
  personality, a sitting ends when the turn bank runs dry, and rivals hesitate a quarter of the time.
  The admin rivals table shows habits and what each one is doing now.
- Product sells at the price of the town it is sold in, and a listing's price band is judged against
  the seller's town. Ground pays out only in the town you are standing in.
- Anti-farm's wealth rules and house protection do not apply to fights over ground, which carries its
  own settling period after changing hands.
- The player clock resolves morale recovery bonuses itself, so recovery still happens in one place.
- One shared definition of which log rows are notifications rather than actions.

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
