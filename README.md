# Street Empire

A playable browser-game foundation inspired by the turn-based economy and crew-management loop of classic browser crime/empire games.

## What changed in 0.2.6

0.2.6 was about the account, and about the fact that there was nowhere to put the game.

An account was a username and a password chosen on the day somebody signed up, and that was the whole
of it for ever: no second way in, no way to change either, no way back if the password went, and
nowhere in the game to go and look. It now has three doors and a page that manages them. An email
address is a second name to sign in under and the thing an account is recovered by. Discord signs a
player straight in, and can make one from scratch. An address is proved by a six-digit code, which is
what makes a forgotten password survivable and what stops a stranger's address opening anything.

Two rules hold that together, and both are refused by the server rather than merely discouraged by the
page. Nobody can sign up without something an account can be recovered by, and nobody can take away
the last of those afterwards - not by removing an address, and not by disconnecting the Discord that
was covering for its absence. A password lets you in and can never get you back, which is why the two
questions are counted separately everywhere they are asked.

Every change to a way in now writes to the owner, and the one that matters most goes to the address
being left behind rather than the one arriving: moving the address is how somebody who has taken an
account keeps it.

The crew work that landed alongside it gave crews something to do besides not robbing each other -
handing each other goods, agreeing pacts with other crews, and calling those crews in when one of
their own is raided. Help sent that way genuinely leaves the sender, and can be asked back once the
fight is over, minus whatever did not survive it.

The rest of the release is that none of it was deployable. There is an image now with the client built
into it, Postgres beside it, Caddy in front terminating TLS and renewing its own certificates, a
backup that reads its dumps back before keeping them, and configuration out of a .env file rather than
out of something somebody committed.

Six faults were found by running things rather than reading them, and they are the honest measure of
the release: changing a password signed you out of your own password change; a session opened in the
same second as a reset survived it; two people registering at once got a stack trace; a stranger could
aim a message a minute at somebody else's inbox for ever; the player market opened on a good that
stopped existing when guns split into tiers, and so did an admin button and a bot dropdown; and assist
calls had no way to end, so the page kept offering help to fights that finished days ago.

The suite went from 133 tests to 168 and grew a database for the first time, because the three things
that move stock between players cannot be checked without one - and a missed subtraction there does
not throw, it quietly mints condoms.

### What a crew is for, beyond not being robbed

A crew was a truce and a treasury. Everything else about running with people - lending somebody a gun,
agreeing terms with another crew, turning up when one of yours is being hit - had nowhere to happen.

**Members can hand each other things.** Cash, thugs, or anything on the trade list. The checks are the
ones that stop a transfer inventing goods: the sender must have it *free* - thugs standing at home
rather than out on a raid, guns off the rack rather than in somebody's hands - and the receiver must
have room to put it. Every send is written down, because a shared treasury with an untraceable side
channel is not really shared.

**Crews can make pacts.** Requested, answered, cancellable from either side, one per pair. A pact is a
truce with the same teeth the in-crew one has: members of two allied crews cannot attack each other,
and the refusal lives at both places a fight can start - the strike endpoint and the raid mission -
rather than at one of them, which is how a rule ends up being half a rule.

**And a pact is worth something when it is tested.** Launching a raid on somebody in a crew opens an
assist call to every crew they have a pact with. An ally answers with thugs and guns, tier by tier,
and the force genuinely moves: it leaves the ally, it is checked against what they have standing free,
and it lands with the defender in time to fight. The window is the fight itself - travelling or
fighting, nothing later - because arriving after the shooting stops is not help.

### Taking the help back

What an ally sends genuinely leaves them, which is the only way the fight can count it - the raid reads
the defender's own numbers and knows nothing about where they came from. That made sending help a
one-way gift, which sat oddly next to the crew pool: borrowed pool thugs have always gone home when the
mission ends.

So once the fight is over, whoever sent help can take back what is still standing. Two caps, and they
exist for different reasons. **Never more than was sent**, or a recall would be a way to strip a crew
mate of thugs they always had. **Never more than the defender still has free**, which is the honest
half - some of what was sent will have died, and what died is not owed back by anybody. A recall that
gets nothing back is still a recall, and the game says so rather than pretending it failed.

It is something the sender does rather than something that happens, unlike the pool. Pool thugs are the
crew's; these are one player's, and a crew that wants to leave them where they are as a gift should be
able to.

A call nobody answers closes itself when the fight ends. Nothing was doing that before, so the page
went on offering to reinforce raids that had finished days earlier.

### Holding a whole town

A crew that holds every piece of ground in a city gets extra thugs defending any member attacked at
home there. Per-city in config, so a town can be worth more than the sum of its plots.

It deliberately does not apply to territory raids. Ground that made ground harder to take would
compound - the crew that got there first would be the crew that stays - so the reward is on the
houses instead, which is where a town being yours ought to be felt.

### Two ceilings that were not there

A garrison had no upper bound and neither did a raid. They are 50 and 100 now, with the garrison bonus
capped at 85%.

The number that matters is the relationship between them rather than either one. A test stands a fully
buffed garrison on its cap, sends a raid on its cap at it, and insists the garrison holds.

That is a design statement rather than an incidental fact: ground filled to the top and buffed to the
top is meant to be safe from one maximum raid, so that investing in a garrison buys something real. It
is also the fragile half of the pair - retune either ceiling, or the garrison bonus, and the two can
slide past each other without anything failing loudly. The test is what makes that slide loud.

### One door, and no way to change the lock

An account was a username and a password chosen on the day somebody signed up, and that was the whole
of it for ever. There was no second way in, no way to change the password, no way to correct a
username typed wrong on the first try, and nowhere in the game to go and look at any of it. The only
page that could see an account at all was the admin panel, looking at somebody else's.

There are three doors now, and a page that shows them side by side.

**Signing up needs one of the two.** The username-and-password door demands an email address; the
Discord door does not, because a Discord account is itself a way back in. The rule underneath is that
nobody may make an empire they have no means of recovering: a password on its own cannot be recovered
from, since forgetting it leaves nothing to prove ownership with.

It holds afterwards too, or it would not be a rule. Removing an address is refused unless Discord is
connected, and disconnecting Discord is refused unless there is a confirmed address - because closing
only one of those two moves the hole rather than filling it:

> sign up with an address and a password &rarr; connect Discord &rarr; remove the address, because
> Discord covers it &rarr; disconnect Discord, because the password covers it

and out the far end comes an account with a password and no way to recover it, one allowed step at a
time. Each step passed the rule it was checked against; no step was checked against this one.

Two different questions are being asked, which is why there are two methods on the account rather than
one. `HasAnotherWayIn` counts what lets somebody in - a password or a Discord. `HasAnotherWayBackIn`
counts what could prove the account was theirs after the password is gone - a **confirmed** address or
a Discord. A password answers the first and never the second, and an account can be perfectly reachable
and completely unrecoverable. That state is the one being outlawed.

Changing an address is always allowed: one is still there to confirm, and a code is already going to it.

Accounts that predate the rule keep working with neither. It is a rule about signing up and about not
undoing it, not a condition of continuing to play.

**An email address** is a second name to sign in under. The login box takes either kind and decides
which it has by the @ - an address can never be a username, so it is one lookup rather than two
guesses. Addresses are folded to lower case on the way in, because signing up as `Sam@example.com` and
coming back as `sam@example.com` is one person and a unique index compares bytes.

That paragraph used to carry a warning that the rest of this release spent itself undoing: nothing is
ever sent, no verification, no reset, so an address is a convenience and never a way back. All three
stopped being true further down this page. An address is confirmed by a code, it can be sent a reset,
and it is the thing that makes an account recoverable at all - which is why signing up now requires
either one of these or a Discord.

**Discord** signs a player straight in, on any browser, without a password. The round trip is the
ordinary OAuth one, and the same callback serves all three things it can turn out to be: an identity
that already belongs to somebody is a login, an identity that belongs to nobody while somebody is
signed in is the connect button on the account page, and an identity that belongs to nobody with
nobody signed in is a new player - who then has to answer the half Discord cannot, which is what they
want to be called and which town they are setting up in.

Signing up this way makes a whole account - the same starting cash, turns, crew and hideout the
register form deals - and it asks for one thing that form does not have to: an **optional email**. An
account made through Discord has no password, so Discord is the only way in and losing it loses the
empire. The one moment a player is already filling in a form is the cheapest moment to offer them a
second way back, and it is confirmed by a code like any other address. The form says plainly what
skipping it costs.

What is stored is Discord's snowflake, not the handle. A handle can be changed by its owner at any
time, and keying on one would hand somebody else's empire over on the next login. The handle is kept
too, refreshed on every trip through, purely so the settings page can say which Discord this is.

Only `identify` is asked for. Discord will hand over an email address for the asking, and the game has
no use for one it did not verify itself, so it does not ask.

Nothing about it is on by default. The button is drawn only when the server actually holds a client id
and a secret, because a door that is painted on is worse than no door. Half-configured counts as off:
an id with no secret would send a player to Discord and fail them on the way back. The shipped
`appsettings.json` carries the two harmless values and blanks for the two that matter, and a test
reads that file and fails if a secret ever lands in it.

**The account page** puts them together under three tabs - Profile, Sign-in, Security - and the rule it
is arranged around is that at least one way in has to stay open. A player who removes their password on Monday and disconnects Discord on
Tuesday owns an empire nobody can ever reach again. So the page says which door is the last one
standing, and the server refuses the change regardless of what the page says.

Changing a password ends every other session on the account and keeps the one that changed it, which
is the only version of that worth having - a password changed while whoever it was changed against is
still signed in has not changed anything. That took two goes to get right, and the first one signed
the player out of their own password change: the watermark is compared against the moment the session
cookie was issued, and a cookie ticket remembers whole seconds and throws the fraction away. An
unrounded watermark is therefore always a few hundred microseconds ahead of the cookie written in the
same breath. Both are floored to the second now, and a test says why.

The round trip through somebody else's site cannot keep anything in memory, so the two halves it needs
are signed blobs the browser carries and the server refuses on the way back unless the signature and
the clock both agree. The state carries a nonce that is also written to a cookie, so a login somebody
else finished cannot be replayed into your session. The sign-up ticket carries the Discord identity,
which means the finish-signing-up form claims nothing about who is filling it in - it sends a name and
a town, and the server already knows the rest.

The one thing the browser does get to name is where it wants to be put down afterwards, because in
development that is a port nobody could have written into a config file. An origin the caller names
and the server obeys is an open redirect, so it is checked against the origins CORS already trusts,
plus - in development only - any port on this machine.

### Proving the address

An `email_verified` flag that gates nothing is decoration, so this one gates something: **an
unconfirmed address cannot be signed in with.** The unique index still holds the address against every
other account from the moment it is typed, which stops two people claiming one - and it means somebody
who types an address they do not own has blocked it and gained nothing, because the door it would have
opened stays shut until they prove they can read the mail.

The proof is a **six-digit code**, typed into the account page. A link would have carried a long random
token and could safely have lived for a day; six digits is a million possibilities, and a million
possibilities held open for a day is a day to guess in. So the code is given neither time nor tries:

| | |
|---|---|
| Lifetime | 15 minutes |
| Wrong guesses | 5, then the code is burned |
| Between sends | 60 seconds, per address |
| Generated by | `RandomNumberGenerator`, uniform across all 10<sup>6</sup> |
| Stored as | sealed by the data protection key ring, never as it was sent |

All four are configurable under `Auth:Email`, and a test fails the build if the shipped ones drift
somewhere unsafe. The code is sealed rather than hashed because six digits falls to any hash in
seconds; sealing means a database read alone is not enough, since the key ring lives outside the
database.

Verification starts at sign-up (`SendOnSignUp`, on by default) and again whenever the address changes,
because those are the two moments a player is already thinking about the address they just typed.
Changing the address takes the tick with it - `PlayerAccount.SetEmail` moves both together, so the pair
cannot come apart - and a code proves control of *the address it was sent to*, so one still in flight
when the address changes is void rather than misapplied to the new one.

### Sending it: Resend, not SMTP

Mail goes over Resend's HTTP API. Running a mail server means owning deliverability - reverse DNS, SPF,
DKIM, DMARC, warming an IP, and a reputation lost faster than it is earned - and the reward for getting
any of it wrong is verification mail that lands in spam, which is the same as not sending it.

Without an API key, **the message is written to the server log instead of sent**, which makes the whole
flow clickable on a laptop with no account anywhere. The account page says which of the two is
happening rather than implying the mail went: a code sitting in a log is exactly right in development
and a quiet disaster in production.

```
Auth__Email__ApiKey=re_...
Auth__Email__FromAddress=Street Empire <no-reply@yourdomain.example>
```

The from address has to be on a domain verified with Resend. Their sandbox sender,
`onboarding@resend.dev`, is the shipped default and will only deliver to the Resend account's own
owner - fine for a first test, useless for players.

### A note on what was asked for

The request named [Better Auth](https://www.better-auth.com/), which is a TypeScript library. This
server is ASP.NET Core and the client is a static SPA with no Node backend, so adopting it would have
meant standing up a second service to own accounts and rebuilding the cookie auth, ban enforcement,
force-logout and Discord linking around it. What it *describes* is framework-agnostic, so that is what
was built: the `email_verified` flag, the expiring cryptographically random token mapped to a user, the
send-on-sign-up hook, and a transactional provider rather than a raw SMTP server. One runtime, one
schema.

The other deviation is the expiry window. 15-24 hours is the right span for a long random token in a
link; for six digits it is far too generous, so the code lives fifteen minutes and the window is
configurable for anybody who disagrees.

### Telling somebody their account changed

A password quietly changed by whoever is holding a borrowed session is invisible until the day the
real owner tries to sign in. A notice makes it visible in a minute, and that minute is the difference
between an account that can be saved and one that is gone. So every change to a way in now sends one:

| What changed | Where the notice goes |
|---|---|
| Password set for the first time | The confirmed address |
| Password changed | The confirmed address |
| Signed out everywhere else | The confirmed address |
| Discord connected | The confirmed address |
| Discord disconnected | The confirmed address, naming the handle |
| **Email address changed** | **The address being left behind**, naming where the account went |
| Email address removed | The address being left behind |

The bolded row is the one that matters most and the easiest to get backwards. Moving the address is
how somebody who has taken an account keeps it - the owner is cut off and never hears - so the notice
goes to where the account *used to* point, at the moment it stops pointing there. Telling only the new
address would be telling the thief. The new address is not told separately, because a verification code
is already on its way to it.

Three rules run through the copy:

- **A notice reports a change and never carries it.** No new password, no verification code, no token.
  A mailbox is not a secure channel, and a notice that leaked what it reported would be worse than
  none.
- **Only confirmed addresses are written to.** An unconfirmed one may belong to a stranger who was
  typed in by accident, and mailing them about somebody else's account is both a nuisance and a spam
  complaint against the sending domain - which would eventually stop the codes arriving for everybody.
  The cost is that an account with an unconfirmed address gets no notices, which is one more reason the
  page pushes to confirm.
- **A notice never fails the change it reports.** The account has already been altered and saved by the
  time one is attempted, so a provider being down cannot answer an error for a password that really did
  change. A send that throws is logged and swallowed, and a test holds that line.

A test walks every value of the `AccountChange` enum and fails if one has no copy of its own, so the
next event added cannot quietly ship as "Something on your account changed".

Turn them off with `Auth__Email__SendSecurityNotices=false`, which exists for load testing against a
real provider and for nothing else.

One notice is not about a setting at all: **a Discord account signing in**. A connected Discord signs
in without a password, so nothing else would ever tell somebody that another person is in their
account. It is noisier than the rest by design.

### When mail goes nowhere

With no provider key the message is written to the server log instead of sent. On a laptop that is the
point. Anywhere else it is a quiet disaster - nobody can confirm an address, nobody can reset a
password, and the only hint is one line on a settings page - so the server now says which of the three
situations it is in, at startup:

```
Email is on, sending as Street Empire <no-reply@yourdomain.example>.
No email provider is configured. Verification codes and account notices will be written to this log instead of sent.
NO EMAIL PROVIDER IS CONFIGURED and this is not a development environment. ...
```

The third is a warning rather than a refusal to start. A server with no mail is degraded, not broken:
existing players keep playing.

### Getting back in without the password

This is what confirming an address was always for. A confirmed address can be handed a reset code, and
that code sets a new password - so the address is now both a way to sign in and a way back in.

The flow is two legs, and the shape of both is decided by one fact: it is the only unauthenticated
flow in the game, so **every answer it gives is an answer to a stranger.**

- **It never says whether an account exists.** Starting a reset returns the same sentence, byte for
  byte, for a real username, a real address, a typo and a fishing expedition. Otherwise the form is a
  way to test whether somebody plays this game, and then a way to test which of a leaked address list
  does.
- **A missing account and a wrong code are the same refusal.** Both cost an attempt against the sign-in
  limiter, so the confirm leg cannot become the enumeration oracle the start leg refuses to be.
- **An unconfirmed address is not found at all.** Mailing a reset code to an address nobody proved would
  hand the account to whoever typed the address in.
- **Success ends every other session.** Whoever took the account is signed in right now; a new password
  that left them there would have changed nothing. The same watermark machinery a password change uses.

Codes for the two flows share a table and are told apart by a `Purpose` column. Without it, a code
mailed to confirm an address - which the mail correctly describes as harmless - could be typed into the
reset form and become a new password. The two mails say plainly which they are, and the reset one tells
a reader who did not ask for it that nothing has happened yet.

### What a reset still cannot do

An account with no confirmed address has no way back in, and that is not an oversight - there is
nothing to prove ownership *with*. The account page says so, and the security tab counts ways in as two
(password and Discord) rather than three, because an address is a second name for the password door
rather than a door of its own.

### The ceiling under the rate

Starting a password reset needs no account and no password, so the sixty-second cooldown only sets a
*rate* - and a rate with no ceiling is unbounded. Anybody who knew an address could aim a message a
minute at it for as long as they liked: sixty an hour, somebody else's inbox ruined, and a pile of spam
complaints against the one domain every code goes out from.

`Auth__Email__MaxCodesPerDay` (10) is the ceiling, counted per address across both flows. Far above what
a real person needs - a confirmation, a couple of resends, a forgotten password or two - and far below a
nuisance. It answers differently from the cooldown on purpose, because "wait a minute" is useless advice
to somebody who has used the day up.

### When two people want the same name

Every place that takes a name checks whether it is free and then saves, and those are two moments with a
gap between them. Two registrations in that gap both pass the check, and the second hits the unique
index - which is the thing that actually decides, and which used to arrive as an uncaught
`DbUpdateException`: a 500, with a stack trace in the body in development. Four simultaneous identical
registrations reproduced it every time.

The check stays, because it produces a decent message almost always. The index is now caught too, and
says which of the names was taken. Outside development anything still uncaught gets one sentence in the
same shape as every other error here, with the detail in the log where it belongs, rather than whatever
the framework felt like putting in the body.

### Keeping the table honest

Spent and expired codes are swept daily, five minutes after startup, once they are older than
`Auth__Email__CodeRetentionDays` (7 by default). They are worth a few days - "did a code actually go out
on Tuesday" is a real question when a player says they never got one - and worth nothing after that.

Age is the only test, deliberately, rather than "spent or expired": a row younger than the window is
left alone whatever state it is in, so the sweep can never race a flow that is using one.

### What a moderator can see

The admin panel could see a username and nothing else about who an account belongs to, which is the
one field somebody changes first on the way to a second account. It now shows the email address and
whether anybody proved it, the Discord handle, and the Discord snowflake - and searches all three.

The snowflake is shown next to the handle rather than instead of it because a handle is renamed in a
second and the snowflake is not. For the question a moderator is actually asking - is this the same
person who was banned last week - it is the only one of the two worth anything.

### Putting it on a server

Three containers - the app, Caddy in front of it, and a job that takes dumps - talking to a Postgres
installed on the VPS itself. The app image carries the built client inside it, so one origin serves both.

```bash
git clone https://github.com/AlmightyTank/StreetsEmpire.git streetsempire && cd streetsempire
cp .env.example .env    # fill in DOMAIN, POSTGRES_HOST, POSTGRES_PASSWORD, and the keys you have
./ops/deploy.sh
```

Set the database up first - see below - because there is nothing to connect to until it exists. After
that the app migrates on the way up, a fresh database taking all 58 migrations before the first request
is served, so there is no separate step and no `dotnet ef` on the box.

**One image, not two.** The client is a bundle of static files rather than a service, so it ships inside
the API image and is served from the same origin. That is worth more than the tidiness: CORS has
nothing to do, cookies are plainly first-party, and there is exactly one address to register with
Discord instead of one that moves. Requests under `/api` are the API's; a path that is neither an API
route nor a file on disk gets the client shell and is resolved in the browser. A mistyped API route
still answers 404 rather than a page of HTML, which is the difference between a five-minute bug and an
afternoon.

**Only Caddy faces the internet.** It terminates TLS, gets a Let's Encrypt certificate on first boot
and renews it on its own - no certbot, no cron, no renewal that quietly stops - and redirects plain
HTTP to HTTPS. The app publishes no host port at all, and Postgres is configured below to listen on
loopback and the Docker bridge only. A 5432 open to the internet is found by a scanner within the hour.

TLS here is not tidiness. The session cookie *is* the login - fourteen days, sliding - so anybody who
reads one in transit becomes that player without a password, and none of the account work in this
release can tell them apart from the real owner. Passwords and reset codes travel in request bodies
besides, which makes a six-digit code guarded by a fifteen-minute window pointless if it is readable on
the wire.

**The app trusts `X-Forwarded-*`, and has to.** Behind TLS termination it otherwise sees a plain HTTP
request from a container, and two things break silently: the session cookie is issued with
`SameAsRequest`, so it would lose its `Secure` flag while the browser is on HTTPS, and the sign-in rate
limiter partitions anonymous callers by address, which behind a proxy is the proxy for everybody -
turning ten attempts a minute *each* into ten a minute *for the whole game*. Both were confirmed fixed
by testing rather than by reading: the cookie comes back marked `secure`, and eleven attempts from one
address throttle while a twelfth from a different one still gets through.

The known-proxy list is cleared rather than enumerated, because the proxy's address on a Docker bridge
is not knowable in advance. That is safe only while nothing can reach the app except through Caddy,
which is exactly why it publishes no host port - and why the switch is off by default.

**The key ring is on a volume, and this is the part that is easy to miss.** Data protection seals
session cookies, verification codes, reset codes and half-finished Discord sign-ups. Unconfigured, its
keys live inside the container and go when the container does - so every redeploy would silently sign
out every player and quietly void every code in flight. Nothing errors; it simply stops working, once,
at deploy time. `DataProtection__KeyPath` points at a mounted volume, which was tested by replacing the
container and confirming an existing session still worked.

Set `DOMAIN` to the bare hostname - `streetsempire.dev`, no scheme and no slash - and point its DNS at
the VPS before starting, because Caddy proves ownership over port 80 to get each certificate. That one
value builds the rest: the certificates for the apex and for www, the origin the app calls its own, and
both Discord addresses. Then register `https://$DOMAIN/api/auth/discord/callback` with the Discord
application; the server prints the exact string it expects at startup.

**Point `www` at the machine too, or delete the www block from `ops/Caddyfile`.** Caddy only holds
certificates for names it is configured for, and a browser asking for a name it has none for does not
get a 404 - it gets a failed handshake, `ERR_SSL_VERSION_OR_CIPHER_MISMATCH`, which reads like a broken
server rather than a site that was never set up. www redirects to the apex rather than serving the game,
because two origins for one game means a session cookie set on one is not sent to the other.

Discord will not accept an `http://` callback for anything but localhost, so TLS is what makes Discord
sign-in possible at all rather than merely advisable.

### Postgres on the host

Postgres runs on the VPS rather than as a container, and the data was not the reason. It was a container
once, on a named volume, and that data was already permanent: a volume is a separate object with its own
lifetime, so it outlives every container that ever mounts it. That was demonstrated rather than assumed -
the container destroyed outright, the volume left alone, and the same eleven accounts counted afterwards.
Only `down -v` or an explicit `docker volume rm` destroys it.

What running it on the host buys is different. It keeps running when Docker does not, so `docker compose
down` at the wrong moment is no longer a decision about the database. It is upgraded and security-patched
by the same `apt` that patches everything else on the box. And its data sits in a directory you can look
at, back up and reason about without going through Docker to reach it.

**Install it, and make it reachable from the containers but from nothing else.** Match the major version
to the `postgres:` tag on the backup service - `pg_dump` refuses to dump a server newer than itself, so a
drift there is a backup that stops silently.

```bash
sudo apt install -y postgresql-17
sudo -u postgres createuser --pwprompt street_empire
sudo -u postgres createdb --owner=street_empire street_empire
```

Two files then decide who can reach it. In `/etc/postgresql/17/main/postgresql.conf`:

```
listen_addresses = 'localhost,172.28.0.1'
```

and in `/etc/postgresql/17/main/pg_hba.conf`, above the local rules:

```
host    street_empire    street_empire    172.28.0.0/16    scram-sha-256
```

`172.28.0.1` is the VPS as a container sees it - the gateway of the compose network, which
`docker-compose.prod.yml` pins to that subnet for exactly this reason. Docker would otherwise be free to
pick a different range whenever a network is recreated, and this file would be quietly wrong afterwards.
Pinning it means one line here naming one range, rather than `0.0.0.0/0` and a firewall rule carrying the
whole argument. `sudo systemctl restart postgresql` and it is listening.

The same address goes in `.env` as `POSTGRES_HOST`, which has no default in the compose file on purpose:
an address that appears in two files is an address that drifts, and the way you find out is a database
that stopped being reachable some time after a reboot.

**Moving an existing database off the volume.** Do this with the app stopped, and do not delete anything
until the new one has answered a real request.

```bash
# 1. A dump of what is in the volume, taken from the container that is about to stop.
docker compose -f docker-compose.prod.yml exec -T postgres \
  pg_dump --username=street_empire --dbname=street_empire --format=custom \
  > /tmp/final.dump

# 2. Everything down. The dump is on the host now, not in a container.
docker compose -f docker-compose.prod.yml down

# 3. Into the new one. --clean --if-exists makes it repeatable if step 4 sends you back here.
sudo -u postgres pg_restore --clean --if-exists --no-owner --role=street_empire \
  --dbname=street_empire /tmp/final.dump

# 4. Count something you recognise before trusting it.
sudo -u postgres psql -d street_empire -c 'select count(*) from "Accounts";'

# 5. Then bring it up against the host database and sign in as a real player.
docker compose -f docker-compose.prod.yml up -d --build
```

Only once you have signed in should you remove the old volume, and it is worth keeping for a week rather
than a minute:

```bash
docker volume rm streetsempire_street-empire-data
```

That command is the one irreversible step in this whole page. There is no undo and no confirmation.

### Backups

A third container takes a dump of the database, checks it, and prunes the old ones. It is still a container even
though the database is not, because the script is the tested one and the image tag pins the `pg_dump`
that takes the dumps. That pin matters more now, not less: `pg_dump` refuses to dump a server newer than
itself, so `postgres:17` here has to keep up with whatever `apt` installs on the host, and the day it
stops keeping up is the day the backups stop - quietly, because nobody looks at a job that has been fine
for a year.

It connects to the host at `POSTGRES_HOST:POSTGRES_PORT`, and passes that port explicitly rather than
letting libpq default it. That is not tidiness either. On a host, 5432 is whatever answered first: during
testing it turned out to be an unrelated Postgres, which `pg_isready` called alive without hesitation
because it does not authenticate. A dump aimed at the wrong server is worse than no dump, because it
looks like one.

Three things about it matter more than the dump.

- **It dumps once immediately on startup.** A backup misconfigured at deploy time otherwise announces
  itself a day later, which is a day of believing there are backups.
- **It writes to a temporary name and renames on success.** A rename inside a directory is atomic, so
  an interrupted dump can never sit there looking like a good one.
- **It reads every archive back with `pg_restore --list` before keeping it.** A file existing and a
  file being restorable are different claims, and only the second is worth anything.

Dumps land in `./backups` on the host rather than in a Docker volume, deliberately: a backup that only
exists on the machine it is backing up is not a backup of that machine. Copy them somewhere else -
`rsync`, object storage, anything off the box. They are gitignored, because each one is a complete copy
of every account, address and hashed password in the game.

### Restoring

The dumps are the custom format, so you can put back one table without touching the rest - which is
usually what you want, since the thing being undone is one bad migration or one bad afternoon.

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

No password flag: the container carries `PGPASSWORD`, so anything run inside it inherits it. Restoring
should not also be a password hunt.

This whole loop was tested rather than described - a dump taken, an account deleted, the dump restored,
the account back.

### Updating it

```bash
./ops/deploy.sh
```

That pulls the checkout, pulls the image CI built, restarts, and waits until the app actually answers
before calling it done. New migrations apply on the way up. The database is not part of this stack any
more and is not touched by it, and the key ring volume is left alone.

**The VPS does not build anything.** It used to: `up -d --build` ran the .NET SDK, `npm ci` and a Vite
bundle on the machine that was at that moment serving the game, for minutes, using memory the game
wanted - and what came out was an image no test had ever run against, because building from a checkout
does not care whether that commit was green.

The publish job in `.github/workflows/ci.yml` has `needs: [server, tests, client]`, so an image exists
only for a commit that passed all three. It goes to GHCR under three tags doing three different jobs:

| tag | what it is for |
| --- | --- |
| `latest` | the newest green build of main - what a deploy takes when you give it no argument |
| `0.2.6` | the version in `VERSION`, which is the name a human says out loud |
| the commit sha | the only one that never moves, and therefore the only one worth pinning to |

Which makes going back one command, and the same command:

```bash
./ops/deploy.sh 4f3a91c8d2e5b7a1f0c9d8e7b6a5f4e3d2c1b0a9
```

It sets `IMAGE_TAG` for that one run rather than editing anything, so the next plain `./ops/deploy.sh`
returns to `latest`. Pin it in `.env` instead if you want it to stay put across deploys.

**One thing to do once, in GitHub.** A package published from a public repository still starts private,
and the VPS has no credentials. Open the package under the repository's Packages, then Package settings,
and change its visibility to public. The alternative is `docker login ghcr.io` on the VPS with a
read-only token, which is a credential to store and rotate for an image that contains nothing secret -
the app and the built client, with every password and key coming from `.env` at runtime.

**The image reports which commit it is.** `/api/health` returns the version and the build:

```json
{"status":"ok","version":"0.2.6","build":"0.2.6+8f63b72f0dba6dac2fdafc95f3d7dbeaa74ede3e"}
```

The commit has to be passed in as a build argument, because the build context carries `Server/` and not
`.git` - nothing inside the build could work it out. CI passes it. A build by hand leaves it off, which
is honest: a local build genuinely is not any particular commit.

### Bumping the version

One line:

```bash
echo 0.2.7 > VERSION
```

MSBuild reads that file into the assembly, so `/api/health` reports it without anybody typing it out.
Vite reads the same file at build time and bakes it into the bundle, so the number in the app's sidebar
follows too. The client's `package.json` no longer carries a version at all - it is a private package
and the field was only ever another copy to forget.

It used to be written by hand in five places. They agreed, which is what hand-copied numbers do right
up until somebody bumps four of them, and the two that would have lied longest are the health endpoint
and the number in the corner of the page - the two nobody looks at until they need to know what is
actually deployed.

Both halves of the wiring fail quietly if broken: MSBuild falls back to `0.0.0` and vite leaves its
token unreplaced, and neither stops a build. So a test reads `VERSION`, checks it against the version
baked into the assembly, and fails if any of the three files goes back to naming a number itself.

The health endpoint reports the commit alongside it, which is the thing actually worth knowing from a
server:

```json
{ "status": "ok", "version": "0.2.6", "build": "0.2.6+15ba6c9105d8..." }
```

### Where the credentials live

Nothing secret is committed. Copy the template and fill in what you need:

```bash
cp .env.example .env
```

`.env` is gitignored; `.env.example` is the committed copy, and a test fails the build if any key in it
ever carries a value. The names are the `appsettings.json` paths with `__` (two underscores) wherever
the JSON nests - `Auth__Email__ApiKey` is `Auth:Email:ApiKey`. That is .NET's own convention for
environment variables, and using it means anything in `appsettings.json` can be overridden from `.env`,
not only the secrets.

.NET has no notion of a `.env` file. What it has is an environment-variable configuration provider
already in the chain that already understands that naming, so [`DotEnv`](Server/StreetEmpire.Api/Support/DotEnv.cs)
reads the file into the process environment before the builder runs and gets out of the way - rather
than adding a configuration source and then arguing about where it sits in the order. It walks up from
the working directory to find the file, so one `.env` at the repository root serves both
`dotnet run --project Server/...` from the root and `dotnet run` from inside the project.

**A value already set in the real environment is never overwritten.** That is the rule every dotenv
implementation follows and the one that matters in production: a platform injecting a secret as an
environment variable has to beat a `.env` that got copied into an image by accident. The server says
which it read, and how many it left alone, on the first line of its log:

```
Read 4 setting(s) from D:\Github\StreetsEmpire\.env. 0 were left alone because the environment already set them.
```

The format is the usual convention rather than a specification: `KEY=VALUE` a line at a time, `#`
comments, blank lines ignored, an optional `export` in front, quotes stripped when they wrap the whole
value, and `\n` honoured inside double quotes only. A trailing `# comment` ends an unquoted value and
is left alone inside quotes, so a secret with a `#` in it survives. A line that is not a setting is
skipped rather than thrown over - one bad line should never be why a server will not boot.

### Setting Discord up

The game runs without any of this. To turn it on, make an application at
<https://discord.com/developers/applications>, add `http://localhost:5080/api/auth/discord/callback`
as a redirect, and put the two values in `.env`:

```
Auth__Discord__ClientId=...
Auth__Discord__ClientSecret=...
```

Both halves matter: Discord compares the redirect **as a string**, so it has to be registered under
OAuth2 > Redirects *and saved*, with no trailing slash and no `https`. An unregistered or unsaved one
fails on Discord's own page - the browser never comes back, so there is no request to log and nothing
on this side to catch. To make that diagnosable rather than a guess, the server prints the exact string
it will send whenever Discord is configured:

```
Discord sign-in is on. This exact string must be registered and saved under OAuth2 > Redirects: http://localhost:5080/api/auth/discord/callback
```

If `Invalid OAuth2 redirect_uri` comes back, put that line and the Discord dashboard side by side.

The redirect points at the API rather than the client on purpose. The client's dev port moves - Vite
is handed a free one when several sessions run at once - and a moving port cannot be registered with
Discord. Cookies ignore the port, so a session cookie set on `localhost:5080` is still sent by the
browser when it is back on the client's port a moment later. In production, set
`Auth__Discord__RedirectUri` and `Auth__Discord__ReturnUrl` to the real origins, and put the client's
origin in `Cors__AllowedOrigins__0`.

## What changed in 0.2.5

0.2.5 was about the early game, and grew past it.

The opening hours got what they never had: an opening ladder that says what to do next and why, a
walkthrough that shows the page rather than describing it, a first bank that is actually spendable,
and turns that taper rather than running out at the wall five clicks in. The dead zone in the first
hideout tier is filled.

Past that it turned into a release about other people. Crews are people who have agreed not to rob
each other, with ranks, dues, a shared pool and a door that is one setting with three states. Chat
grew a window that survives changing pages and conversations that hold any number of people. Three
more towns went on the map, each with a leaderboard of its own, and the towns started wanting things:
buyers with deadlines, fillable in instalments.

The fights got sharper. Guns split into tiers worth choosing between, one rack holds them all, and
five ways to hit somebody each cost something to try - infesting a house was free and is not any
more. A strike now says no before the click rather than after it, out of one function that answers
both the note and the refusal.

Late in the version the interface moved onto Bootstrap 5.3.8 and then took its own palette back, the
alerts became a bell, the workshop went on a clock, and nine top-level pages folded into five.

### The free attack

Four of the five ways to hit somebody cost you something to try. A drive-by risks the car. A jacking
needs a thug to drive and a space to park what he takes. A poach spends coke by the head, and the coke
goes out whether or not anybody comes back with you. Infesting a house cost turns and nothing else, so
there was never a reason not to do it, to anybody, at any time.

It costs poison now, and poison has to be bought or made. A dose reaches three hoes, which is exactly
what a crate of medicine treats - the defender's problem handed back to the attacker in reverse.
Covering a big house is expensive at both ends, and turning up short against one only buys you as many
hoes as your doses could reach. A part-used dose is a used dose, the same rounding medicine already
had, because otherwise one dose covers a house forever by never quite running out.

The counter sells it, next to the medicine that answers it, and the mix house makes it cheaper - that
room is the chemicals bench and was already turning out cut, so it needed a product rather than a
building. It occupies a shelf like everything else, counts towards net worth at what it cost, and is
not loot: a raid cannot carry off medicine, so it cannot carry off this either.

### A button that offered what it could not do

The menu of strikes is built from the attacker: your thugs, your garage, your coke. It has never been
shown who you are pointed at, so every rule with a far end - jacking a garage with nothing in it,
infesting a house with no hoes, poaching one with nobody to take - had nowhere to be said. The note
under the menu knew, and said "nothing parked there to take"; the button above it stayed live, and the
only way to learn the rule was to spend the click and be refused.

The refusal now comes from one function that answers both questions: the sentence shown under a dead
button is the sentence the launch would have thrown if it had been pressed. That mattered more than
tidiness - the two had already been written twice, once as a note and once as a throw, and a rule
written twice is a rule that will eventually disagree with itself. There is a test that asserts the
pre-flight answer and the thrown refusal are the same string.

Looking at it turned up a second fault, this one self-inflicted. Splitting net worth from plunder left
the profile endpoint passing net worth into a parameter that had been renamed to plunder - the argument
was positional, so it went on compiling while quietly meaning something else. A profile judged you on a
sum including your buildings while the target list beside it judged you on one that did not, and the
same rule gave two answers depending on which screen you were looking at.

### Being shown the game instead of being told about it

A new player arrives at nine tabs of numbers with no way of telling which one matters today. The
opening ladder says what to do next, but not what any of it is, and reading a panel does not tell you
why you would ever open it.

So there is a walkthrough: six steps, one thing lit at a time with everything else dimmed, and a
sentence saying what the lit thing is and what it is for. It changes pages itself as it goes, because
half of what a newcomer has to learn is which tab a thing lives on, and being taken there is the lesson.
It offers itself once on the first real dashboard and then stays out of the way - a tour that reappears
is a nag - with a "Show me around" button beside the ladder for anyone who skipped it or has come back
after a long time away.

Looking at the tabs properly also turned up a fault that had been there since the ladder was written.
"Run a production shift" pointed at the street, and there is no production on the street: a player
following the game's own instructions arrived somewhere with nothing to do. It points at the hideout
now, where the lab the previous rung had them build actually sits, which puts the two on one page and
takes the opening ladder from six tab changes to five. The test covering guidance passed before and
after the fix, because nothing had ever checked where a rung sends you; it does now.

### Half the config nobody was testing

The tuning lives in two places. GameOptions carries defaults in code, appsettings.json carries values in
a file, and the file wins wherever it has something to say - `ApplyDefaultsWhereEmpty` only fills a list
nobody has filled already. Every test in the suite builds its options from the defaults, so every number
the server actually boots with had never been read by anything.

It had already gone wrong. The storage ladder was rebuilt in code while appsettings went on shipping the
old one, which left the running game applying the new rule to the old room sizes: a starting player
capped at ten hoes instead of twenty-five, and worse off than the fifty they had before any of the work
started. Nothing failed, because nothing was looking.

The suite now loads the server's own appsettings.json and runs the same invariants over it - every crew
supplyable, every rung reachable, every value in step with the default it restates, across all ten room
lists by reflection rather than the one that happened to break. It deliberately reads the file in the
server project rather than the copy the build leaves beside the test binary: the first version of this
test read the copy, and passed against a stale file while the source said something else.

The other one worth having is smaller and duller. A test that is written but never added to the manifest
does not fail - it just never runs, and the only symptom is a total that does not go up by one. That
happened twice in a single sitting here. A test now reads this suite's own source and will not pass
while a test body sits unregistered.

### The building that counted for nothing

Everything a player owned was on the books - cash, bank, crew, guns, medicine, cars, product, even the
beer - except the one thing they spent the most on. A fully built hideout represents about 13.4 million
pounds and net worth could not see a penny of it, so the largest investment in the game made your
standing worse the moment you made it. Upgrading was a way down the leaderboard.

**A hideout is now worth every pound that built it.** Valued at cost rather than at some fraction, which
makes an upgrade neutral: the cash goes, a building of the same worth arrives, and the board does not
move. Buying rooms is not a way up the leaderboard and it is no longer a way down one. A tier under
construction counts from the moment it is paid for, or a player would sink for the length of the build
and float back afterwards.

**Fights are weighed on something else: what could actually be carried off.** The anti-farm rules exist
to stop the strong robbing the weak, and they answer that by putting the two sides on a scale. A
hideout is the one thing in the game nobody can take, so counting it there would rule a well-built
player out of fights they can plainly afford, and pull in heavyweights who would turn up to find
nothing worth the trip. So there are two sums: what you are worth, and what is on the table. Rivals
choose their targets on the second one, the same as everybody else.

Two other systems turned out to be quietly asking the same question and needed leaving alone. The
beginner's turn boost fades out as a player reaches 250,000, and on net worth it would have expired the
day they bought a 200,000 building - charging a new player their starter help as a fee for taking the
game's own advice. And the shrine caps its demand at half a shelf, so counting a building would have
pressed every established hideout against that cap at once, and the gods would have asked the same of a
millionaire as of a man who had just bought a roof. Both measure what a player can lay hands on.

The sums exist twice over, once in memory and once as an expression the database can rank by, so the
leaderboard is still a single query. There is a test that translates both to SQL and checks the shape
of what comes out: the ranking sum has to reach the hideout in the database rather than dragging the
table into memory, and the raid sum must not touch it at all.

### A crew the store could not feed

The hideout offered room for fifty hoes from the first minute. The storage room behind it held
seventeen condoms, which is four turns of work for that crew out of a twenty-turn shift. The game was
inviting players into a shortfall and then charging them morale for it every shift they ran - a
punishment for taking the hideout page at its word.

**A crew is now capped by whichever runs out first: the room the building has for them, or the supplies
the store can put behind them for a full shift.** Every crew the game allows is a crew it can feed,
which is the whole promise, and there is a test that walks every building against every room to keep it.

That makes the storage ladder the crew ladder, so it was rebuilt to be one. It opens at 25 hoes and 12
thugs - the smallest thing worth calling a crew - and climbs a rung at a time to the biggest house in
the game. The old opening rung, the one that supplied a fifth of an action, is gone; nobody misses a
room they had outgrown before they understood what it was for. Nothing above the top building supplies
a bigger crew, because no building holds one, so the last upgrade buys room for product instead.

Pimps are deliberately exempt. Nothing supplies a pimp - they eat no condoms and drink no beer - so the
building remains the only thing that can run out of room for one.

Two pieces of writing had to change with it, both versions of the same lie. A refusal used to blame the
building for a ceiling the storage room was setting, which sends a player off to buy a house that will
not move the number by one. And moving up a tier used to promise it "raises your crew caps"; it raises
the ceiling, and the store is what walks you up to it.

### An order you had no room to carry

Contracts asked for between fifteen and sixty units and would only take the whole amount in one
movement. A first storage room holds five weapons, ten of coke and twenty-five of weed - so a new
player could not fill a single weapons order at any size, and most coke orders stayed impossible until
the fourth storage upgrade. The board was aimed at players who could not reach it.

Goods go in a bit at a time now, and the buyer keeps a tally. What makes that more than a convenience
is where the money sits: **a delivery pays the town's ordinary rate the moment it is made, and the
premium arrives whole when the last unit goes in.** Stopping half way leaves you exactly where selling
those goods flat would have, so instalments are never free money - the premium is what finishing buys.
And because it is never split, two trips pay precisely what one trip would have.

The first delivery claims the order. Without that, two people part-fill the same one and whoever worked
hardest and arrived last has simply wasted the goods. The deadline still frees anything nobody
finishes, and rivals respect a claim the same way players do.

### The design was partly fictional

Two things the stylesheet claimed were not happening. It had asked for Inter since the beginning and
nothing had ever loaded it - no `@font-face`, no link, no package - so every player was reading Segoe UI
or SF and the design had never once been seen as written. It also carried weights of 900 and 950 against
a fallback font that had neither, so both rendered as ordinary bold. Inter is now self-hosted, which
costs one dependency and no third-party request, and the weights are three because a variable font
genuinely distinguishes them and six steps of bold is five too many.

Underneath that, 33 distinct font sizes - fifteen of them between .72rem and .88rem, differing by about
a fifth of a pixel. That is not hierarchy, it is noise wearing hierarchy's clothes: sizes the eye cannot
tell apart read as one muddled size. Eight steps replace them, far enough apart to mean something.
Spacing had the same problem in a different currency - 19 gap values and 51 padding values including
3px, 5px, 9px and 13px - now snapped to a 4px rhythm, rounding tighter rather than looser so nothing
grew back into the overflow the mobile pass had just cleared.

The change that matters most in play is the smallest to describe: figures are tabular. Every number here
moves while you watch it, and proportional digits are different widths, so each tick re-flowed the row
it sat in. They hold still now, and columns of them line up on their digits.

Loading a real font surfaced a bug that had been hiding behind the fallback. The browser's own
stylesheet gives `b` and `strong` a weight of `bolder`, which is relative rather than absolute, so one
nested inside a parent already at 700 computed to 900 - invisible while nothing heavier than bold
existed to render it, and a visible jump the moment something did.

### The phone was holding the game at arm's length

The layout already collapsed to a single column at 760px, so the game *fitted* on a phone. What it did
not do was let you play it with one hand, and the three worst parts were the three that matter most.

**Navigation.** The side rail folded down into a horizontal strip of two-letter codes. Three of the nine
destinations sat off the right-hand edge with nothing to indicate they existed, the six you could see
were abbreviations you had to learn, and the whole thing sat at the top of the screen where a thumb
reaches last. It is now a fixed bottom bar: four named destinations - Overview, Street, Crew, Market,
which is the loop the ladder itself teaches - with the remaining five behind More. Nothing hides off an
edge and every destination keeps its word. When you are on a page More holds, More wears that page's
name, so the bar never shows you a screen you cannot locate yourself on.

**The numbers.** The seven status cards were a sideways scroller, which put cash and heat past the right
edge behind a gesture nothing advertised. Those are the two figures every decision in the game is
weighed against, and they were the ones you had to go looking for. They wrap into two columns now.

**The newest panels were the broken ones.** Mobile was handled by naming each grid individually at a
breakpoint, which works right up until somebody adds a panel - so the alliance page, built after that
list was last revisited, kept its desktop columns and squeezed a member's name and town into 35 pixels.
Rows built around a name now give the name the room and put the controls underneath it.

Beyond that: every control clears 44 pixels wherever the pointer is coarse, including the compact ones,
because fitting one more row on screen is a good trade for a mouse and a bad one for a thumb. The page
reaches under a notch and pays it back with safe-area padding so the bar clears the home indicator, and
heights measure the viewport that is actually visible rather than the taller one an address bar claims.

The palette got names at the same time. It had reached 175 loose hex literals, which meant there was
nothing to change but 175 individual decisions; the eleven doing most of the work are now named for
their job, with 212 uses pointing at them.

### Ranks, and the lines a boss draws

- Four rungs: Soldier, Enforcer, Underboss, Boss. Fewer than a clan system in a game with thousands of
  players, because ranks only mean anything when there are enough people for the gaps to matter, and a
  crew here holds six.
- The boss sets **a minimum rank for each power** - open the door, throw people out, spend the treasury,
  take thugs on a raid, post defenders at home - and that is the part of a rank system that actually
  gets used. Ranks alone are words next to names; what makes two crews with the same ranks run
  completely differently is where their boss drew those five lines, and neither crew had to be
  programmed.
- **You can only act on somebody below you.** Strictly below, never equal, because two Underbosses able
  to throw each other out is not a chain of command.
- **The door is one setting with three states:** open to anyone, by application, or invitation only.
  Those are the three things an outsider can do on their own - walk in, ask, or wait to be asked - and
  each state turns the other two away by name rather than silently. Invitations sit off that axis and
  work in all three, because they are the crew reaching out rather than somebody arriving, and a crew
  that could not invite while set to invitation-only would be a contradiction.
- Invitations and applications are one table read from opposite ends, and every road in ends at the same
  place - joining at the bottom - so no route can hand out a rank by accident. Both are re-checked when
  they are accepted rather than trusted from when they were sent.
- Promotion stops below the top. Handing the crew on is its own move, because it is the one that gives
  yours away.

### A crew is people who have agreed not to rob each other

- That is the whole of what an alliance is, and everything else it carries follows from it. Members
  cannot attack each other by any method - raid, drive-by, jacking, infestation, poaching, or a raid on
  their ground - and the check lives in the launch rules rather than in an endpoint, so every route into
  a fight runs into it: a player's, a rival's brain, and the admin's directive alike.
- The source game left the interesting half of this to the honour system: "don't form super alliances,
  it's against the rules". A rule nobody enforces is not a rule, so here it is mechanical.
- **Dues** are a founder-set share of every member's shift, taken off the gross beside the hoe cut
  because it is the same kind of thing and reads in the same sentence. Off the gross rather than off
  what is left: a house paying 40% and dues of 20% gives up 60% of a shift, not 52%, and compounding
  would make the second rate mean something different depending on the first.
- **Crew rank** is the sum of what the members are worth, off the same expression the individual
  leaderboard ranks by, so the two boards can never disagree.
- Six members, not the source game's twenty. That was a game with thousands of players; this world is
  two dozen rivals, where twenty would not be an alliance but everybody against nobody. Rivals already
  run with each other - crews seed themselves into an existing world on first read, the way ground does
  - and a share of rivals always stays unaligned, because a board where everyone has agreed not to fight
  has nothing left in it.
- **The pool** is what the treasury buys: offensive thugs that ride along on a member's raid, defensive
  ones posted to a member's house. Both fight as an armed thug apiece and both die, and what dies is
  gone for good. Thugs on a raid leave the pool until it comes home, so what you take tonight your ally
  does not have - that shared, finite pot is the whole reason a crew is an arrangement between people
  rather than a switch you flip once.
- **A member may bring at most as many borrowed thugs as they brought of their own**, and that is the
  rule keeping the pool from breaking the game. Alliance thugs ignore the hideout's thug cap, which is
  what every combat number is balanced against; tied to your own crew, the pool amplifies rather than
  substitutes, so your tier still sets your ceiling and the crew only doubles it. Losses fall across the
  whole line in proportion, so borrowing is neither free nor a way of using other people as armour.

### Five districts that finally differ

- The source game had five places to scout and its own guide admits it never found a difference between
  any of them: "I've yet to find a significant difference", and the FAQ answer to which is best is a
  shrug. That is a choice offered without ever being explained, which is the same as no choice, so each
  district here changes what a shift is actually for and says so on the tile.
- The **Casino District** pays 45% more and hires almost nobody, with the law watching all of it. The
  **Wino Slums** pay badly and are full of men who will take any work - the place to go for thugs, and
  the quietest street in the game. The **Nightclub District** is where hoes and the pimps who manage
  them turn up. The **Urban Ghetto** is where product changes hands, and where the law knows it.
- **Low Rent** is the neutral one and the default, at exactly the base numbers, so nothing about the
  existing balance moved and a player who never touches the picker works the shift they always did. It
  is also the one the source game's guide-writer said they preferred, which turns out to have been a
  fair call.
- The trade is the same shape everywhere: what you go home with against how much notice you drew getting
  it. A test fails if any district is better at something and worse at nothing.

### The shrine, and the names the day hands out

- Praying to the pimp gods was a slot machine in the source game: burn whatever you like, roll, maybe
  something happens. That is a lever rather than a decision, and it would sit badly in a game where the
  lookout tells you what it takes off your odds and a mule quote tells you what a run will clear. So the
  gods say what they want - a specific good, a specific number - and meeting it is answered every time.
  What stays uncertain is only which blessing lands, and even that is narrowed to the ones that would
  actually do you some good.
- The ask is worked out from the player and the week rather than stored, so it holds all week with no
  row to keep. It scales with what you are worth, bands to two significant figures so ordinary earning
  does not move it under you, and never exceeds half a storage shelf - four percent of a mid empire in
  moonshine is hundreds of bottles, and no room in the game holds a tenth of that.
- Nothing it gives back is money. The blessings are notice the law has already taken, the mood of the
  house, and whether your pimps still believe in you - none of which has a price anywhere else. Turns
  are the one thing rationed behind giving twice what was asked, because they are the only blessing
  that touches the rate the whole game runs at.
- Seven **titles** turn over daily, held by whoever leads a category. They are read out of the fights
  that happened rather than kept as counters: the source game held eight running totals and a button to
  wipe them, and a tally you can clear is not a record of anything.
- Half of them are for things done to you. Silver Tongue and Picked Clean are the same number counted
  from opposite ends, and a board of nothing but winners would only say who is winning - which the
  leaderboard already says. Being publicly the man everybody is robbing is a different fact about the
  world, and a funnier one.

### A weapon was answering only one question

- One generic weapon could tell a fight exactly one thing: is this thug armed. That made arming a crew
  a purchase rather than a decision - you bought weapons until the number matched your thugs, and there
  was nothing else to think about. There are four guns now, and they exist because a weapon does two
  jobs which come apart.
- **Any gun covers a thug.** A thug with a pistol is exactly as content as a thug with a rifle, so
  covering a big crew with cheap guns is a real strategy rather than a mistake.
- **What a gun changes is the fight.** Firepower is measured in pistols - a shotgun is 1.4, an SMG 1.9,
  a rifle 2.5 - and a crew picks up the best guns on the rack rather than a sample of them. Guns beyond
  the crew carrying them are worth nothing at all, however much they cost.
- Which turns the hideout's thug cap into the binding constraint. Buying more bodies is the efficient
  way to get stronger, right until there is nowhere to put another one: better guns are steeply worse
  value per point of firepower and are priced to be. Past the cap they are the only thing left to buy.
- Prices are the source game's - $250, $1,250, $2,500, $5,500 - and every weapon that already existed
  became a pistol. So nobody's fighting strength moved by a single point, because a pistol is worth
  exactly what the one weapon was. What moved is paper value: a rack halves, on the same asset, in the
  same proportion, for everybody.

### What the rack reaches

- The workshop makes what its level has unlocked: pistols and shotguns from the start, SMGs at level
  two, and never rifles. The one gun nobody makes in a back room is what stops the workshop from
  eventually replacing the shop. Its per-weapon cost moved onto the guns themselves, so a level buys
  throughput and reach rather than a discount on one thing.
- All four trade separately on the board and can be named in a contract, because a rack of pistols and
  a rack of rifles are not the same offer and a market that called both "weapons" would price them as
  if they were.
- Losses and storage overflow take the cheapest guns first, every time. A lost fight that destroyed
  your rifles before your pistols would make owning good ones a liability.
- A raid records the mix it left with, so losing five weapons takes the right five off the right
  shelves at home - and two raids at once cannot arm themselves from the same rifles.
- A jacking reads both halves of the guard standing in the garage. Bodies are eyes on the door; guns are
  what happens once you are seen. Six riflemen shut a garage that six pistols would only make risky, and
  because only the firepower above a pistol each counts in the second term, a pistol guard is worth
  exactly what it was before tiers existed. Guns away on a raid are not guarding anything, which is what
  makes hitting somebody mid-raid the opening it should be.
- A drive-by reads the same two halves and weights them the other way round. Finding somebody in the
  open is mostly about how many were watching the road; losing the car is mostly about what they were
  holding. The same six guards with rifles instead of pistols take the hit chance from 78% to 64% and
  the odds of losing the car from 11% to 24% - which is what stops a player with one car and a full turn
  bank grinding any rival's crew down for free.

### One attack verb was making everything you own the same thing

- With only a raid, a player's holdings are one undifferentiated pile of loot. A garage of cars, a
  hundred hoes and a shelf of supplies were all just numbers feeding the same defence roll, and no
  decision a defender made about any of them changed what an attacker would do. There are five ways to
  move on somebody now, and four of them are aimed at exactly one thing.
- A **raid** is what it always was: ten turns, one of two attack lanes, travel, rounds, and whatever the
  crew can carry home. The four **strikes** are its opposite - four to eight turns, settled in one call,
  no lane, no crew committed, and each with a different answer.
- A **drive-by** needs a low-rider. It kills thugs and takes nothing at all, and the better armed the
  street the likelier you lose the car. It is how a player who cannot yet win a raid makes one winnable.
- **Jacking** takes their rides, on odds that are almost entirely the defender's own doing: a garage
  behind a full armed crew is close to untouchable, one behind nobody is a car park with the keys in.
- **Infesting** their hoes is the only attack answered by a purchase rather than by crew or morale.
  Medicine treats who it can and the rest are gone - which is what makes a crate sitting on a shelf,
  costing money and doing nothing, worth owning.
- **Poaching** buys their hoes away with coke. This is the one that makes the payout slider a decision:
  a fully happy house cannot be poached at any price, and a squeezed one can be emptied by a rival with
  a lab. Cut product tempts fewer people, and the coke goes out whether or not anybody comes back.

### The two things the menu needed

- Low-riders are held by the building rather than the storage room. A car is not stock - it is not
  consumed, it does not spoil, and there is nowhere to put a fleet but the hideout - so the garage runs
  from two at the Trap House to fifteen at the Penthouse. That is also what stops a rich player parking
  twenty rides behind a first-tier guard and treating the jacking as somebody else's problem.
- The chop shop is the only counter in the game that buys as well as sells: $25,000 to drive one out,
  $15,000 to sell it back. Net worth counts a ride at what the shop would pay rather than what it cost,
  or buying one would be a free climb up the board.
- Medicine is priced so that covering a house is a real cost that does nothing until somebody attacks,
  and the storage room that supplies a tier's crew through a full shift holds exactly enough to treat
  that whole crew once.

### Two shields, two clocks

- A raid's protection covers everything, strikes included, because walking in behind somebody else's
  victory to finish the job is the dogpile that protection exists to stop.
- A strike sets only its own twenty-minute shield. One column for both would let either loop lock the
  other out: a four-turn drive-by would buy its victim an hour of immunity from the raid that was
  actually coming.
- Rivals use all five, each with its own appetite for a cheap shot and its own order of preference, so
  they read differently in the news: the Hard Charger shoots up streets, the Banker quietly drives off
  with cars, the Crew Builder poaches. They also restock medicine once somebody has actually been
  infesting them - a field that never bought any would make the infestation a one-way ratchet forever.

### Telling a new player anything at all

- Played fresh, the first session was five clicks. A hundred starting turns at twenty a shift is five
  street actions, after which the turn bank is empty for eight hours. In that whole session a player
  saw one verb, never met production, a lab, the market or heat, and the best purchase available to
  them - a ten thousand dollar weed lab - went unmentioned in a room they had no reason to open.
- Next Moves used to be a status readout wearing an advice label: four fixed rows that read the same
  on day one and day one hundred. It ranks real moves now, priced, with a reason, ordered so that what
  is actively costing you money comes before what would merely pay.
- A Getting Started ladder covers the verbs nothing else introduces, and hides itself when finished.
  A checklist a veteran still scrolls past is clutter; the point of it is to stop being needed.
### The wall after five clicks

- The opening bank was half the cap, which read as a courtesy and played as a wall. It is the whole
  cap now: ten shifts instead of five, enough to buy the first lab and still have turns to use it.
- Turns come back faster while you are small and taper to the normal rate by a quarter of a million
  net worth, which is just past the Warehouse. A flat rate is a wall that falls hardest on the people
  least able to take it: twelve an hour meant a new player who spent their bank waited most of a day
  to play again, at the exact moment they had least reason to come back.
- It tapers rather than switching off at a line, so nothing lurches the day you cross a threshold, and
  an established empire earns exactly what it always did.
- The strip reports the rate you actually earn at, not the base one. Paying a boosted rate while
  displaying the flat one would make the countdown quietly wrong for every new player.

### The hole in the first tier

- Measured rather than assumed, the ladder turned out to be denser than it looked: eight things a Trap
  House can buy between $10,000 and $75,000. The problem was one gap after it, $75,000 to $150,000,
  which is a session and a half of earning with nothing to save for.
- The **lookout** fills it at $100,000. Someone on the street watching for the law, cutting the odds of
  a raid landing by a quarter. It never removes the risk, or holding would be free.
- It is also the only new verb in the tier after the workshop. Everything else a first-tier player can
  buy is a bigger version of something already owned, and the lookout is the first answer to heat that
  is not selling everything and waiting.
- A test walks the whole first-tier ladder and fails if any two rungs sit more than two sessions of
  earning apart, so a later re-pricing cannot quietly reopen the hole.

### Neither panel is stored

- Neither is stored. A lab built is a lab on the ladder, and a sale in the log is a sale: asking the
  world what happened cannot drift out of step with it the way a checklist column would.

## What changed in 0.2.4

Territory, player-to-player markets, mule runs, heat, travel between towns as timed flights, and
rivals who play in sittings rather than on a metronome. Organizations did not make it and carry
forward.

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
- Ground reaching the player: losing it, and holding it. A raid you beat off still costs the garrison,
  so it earns its own line rather than leaving a garrison that quietly shrank with no explanation.
  Both appear in the arrival summary and the alert bell, alongside the raider's own row in world news,
  and world news carries a headline for whoever runs the most ground.
- Losing ground reaches the player: a line in the arrival summary, an entry in the alert bell, and the
  raider's own row in world news. The loser's notice is written to them in the second person and is
  deliberately kept out of the public feed, where the raider's row already reports the same event.

### The market

- Players sell to each other on one global board. Stock leaves storage the moment it is listed and
  comes back if the listing is pulled: escrow rather than a promise, or the same fifty weapons could be
  listed twice or spent after somebody had bought them.
- Partial fills, because a listing only one player in the game can afford is a listing nobody buys. The
  house takes a cut of each sale, which is a money sink the game otherwise lacked.
- The seller is paid into the bank rather than cash on hand, which is capped by their safe and
  stealable. A sale that overflowed into nothing while they were offline would be a hole.
- Pulling a listing into a full room leaves the stock on the board rather than destroying it.
- A **workshop** makes weapons from turns and materials, under the store's price. This is what gives
  the board anything worth trading: every other good has a fixed price on both sides, so the tradeable
  spread over them is zero. Weapons are the one thing a player can make that everybody needs.

### Moonshine and cut

- Both the still and the mix house need the **Warehouse** or better. A Trap House is not somewhere you
  hide a still, and gating them there keeps the first tier about learning the game rather than running
  a lab. The gate is checked when making as well as when building, since buying is not the only way to
  end up with a station.
- A **still** brews moonshine, which thugs drink exactly like the shop's beer at well under the shop's
  price. The bought beer is always poured first, so a player is never quietly spending contraband while
  a legal barrel sits next to it.
- A **mix house** makes cut, which is worth nothing on its own and worth whatever the coke it becomes
  is worth. That is why it is priced off the local coke rather than carrying a band of its own: it
  follows the town automatically, because what it is worth is what it makes.
- **Stepping on it** spends that cut: one unit of cut makes one unit of coke, on any coke you hold.
  It used to be a silent bonus inside coke production, which was wrong twice over. A player saving cut
  for a batch watched it disappear into a production run they had not connected it to, and cut could
  never reach coke that arrived any other way - off a plane, off the board, out of a lab overnight,
  which is most of the coke worth stretching.
- What it costs is **strength**. Coke carries a purity, and cutting is a weighted average: the pile
  grows and weakens together. Without that the mix house was simply a cheaper, faster coke lab - a
  unit of filler became a unit of product at full price, about $300 a turn against $220 for producing
  the real thing, with no ceiling on it at all.
- The price follows the square root of purity, which is the only shape that works. Fall proportionally
  and a stretch gains you nothing, so nobody would ever do it. Put a floor under it and total value
  climbs with unit count forever, which is the printer wearing a different hat. A square root means a
  stretch pays, the next one pays less, and eventually the cut costs more than it makes.
- Purity is a property of the pile, so everything that adds coke blends into it: produced, found,
  stolen, bought, flown in, or stretched. Market listings carry the purity they were escrowed at, or
  the board would launder weak product into clean.
- What it also costs is turns, room, and notice. The mix house level sets how fast a batch goes, giving that
  room a second reason to exist, and a stretched pile is a hotter pile: coke draws more heat per unit
  than anything else you can hold.
- Moonshine is priced against the shop's beer, which is the same everywhere, so it does not move with
  the town the way weed, coke and cut do.
- AI rivals trade. They buy weapons off the board when it beats the shop, judged against the shop
  rather than against the other listings, and list what they cannot use.

### Heat

- Everything in this game is illegal, so an illegal flag on one room and not another said nothing.
  What differs is how much notice a thing draws, and that is what heat measures. Coke draws the most
  per unit, then moonshine, then weed, and cut the least, which is the reverse of where it is made.
- Working the streets adds heat by itself, with nothing held at all. The core loop is a crime; a system
  that only watched the stash would have said otherwise.
- Under the floor nobody is looking, however long you sit there. That floor is what makes a small
  operation safe and stops the game punishing a player for existing. Above it, every hour is a roll,
  and the chance climbs with the heat.
- Getting caught takes half of every pile and fines you, and the fine stops at cash on hand: a raid
  that could put a player into debt would be a nastier mechanic than losing the stash. It clears the
  heat that drew it, so one raid does not guarantee the next.
- Heat cools on its own, which is what makes lying low a real move rather than a figure of speech.
- Both halves are sized against the game's own scales rather than against nothing, which is what the
  first tuning got wrong: street work is measured against the turn bank, so spending the whole thing
  is a night's worth of notice that fades by morning, and the per-unit weights are measured against
  the storage rooms, so a full store is worth watching rather than an automatic death sentence. Under
  the first numbers an ordinary evening of work by someone holding nothing reached Hunted, and so did
  simply filling the room you had just paid for.
- It runs on its own clock rather than the turn clock. The turn clock is dragged forward every few
  minutes by anyone at the screen, so a player checking in often would never accumulate a whole hour
  and would never be raided or cool down. Whole hours are consumed and the remainder is left behind.
  A long absence is capped the same way offline lab work is, so a fortnight away costs no more than a
  night.
- Heat reads Quiet, Noticed, Watched or Hunted, because the number alone says nothing about whether
  tonight is the night. It lives in the status strip rather than on the hideout page: it is a live
  risk that changes what a player should do next, so it belongs with the numbers carried between
  pages. The strip scrolls on a narrow screen, so heat is placed ahead of rank and city, which are
  read rather than acted on.

### Mule runs

- Sending crew to another town to buy cheap and carry it home. It is the first thing in the game that
  costs crew and time rather than turns, which is what makes it a different decision rather than a
  cheaper version of one you already had.
- Travelling yourself pays the distance in turns each way and leaves you standing in the wrong town.
  A run costs a fraction of that in turns, but it takes real time, locks up a pimp and hoes who earn
  nothing while they are gone, and is paid for before anybody leaves: fares both ways and their keep
  for the trip. Neither option dominates, which is the point.
- The intelligence centre is the gate. Unlike every other room it makes nothing: it buys how many
  runs can be in the air at once and how much of a route is known before anybody goes. Without one
  there are no runs at all, which is what stops mule running from being free the moment you can
  afford a spare pimp.
- Mules buy at the destination's price, since that is the entire reason to send them. The load is
  capped by the hoes sent, so how greedy to be is the dial the player actually turns.
- Three ways it ends. Delivered brings the load and the unspent cash home. Seized takes a share of
  the load and all the cash they were still carrying, and the heat lands on you, because crew who are
  caught talk. Defected is a pimp far from home with your money deciding not to come back, which is
  what makes who you send a real question rather than picking whoever is spare.
- Runs settle on the clock rather than on a request, so they land whether or not you are watching,
  and find you through the alert bell and the catch-up digest.
- What will not fit in the storage room is dumped, and the notice says so. A run that quietly dropped
  a third of a load already paid for would read as the price being wrong rather than the room.

- The page quotes profit, not gross. The spread alone never decided a run, because the fares are paid
  whether or not it pays: the first tuning had a head costing more to fly than a hoe could carry
  margin for, so every route in the game lost money and the mechanic was dead on arrival. Short hops
  are now thin, bad routes are clearly negative, and a long run into a wide spread is worth the risk.
  Capacity, not odds, is the profit lever: each hoe carries more without lowering bust or defect risk.
- Cash beyond what the crew can carry goods for is dead weight, and the page says so. It comes home
  untouched on a clean run and is taken on a bad one, so sending a fat purse is pure exposure.

- Rivals run mules too, or this would be a player-only edge and the leaderboard would stop meaning
  anything. They build the room themselves and judge a route on what it clears after fares. Because
  rivals live in different towns, they find different routes without being told to.
- A rival sends neither its whole roster nor its whole purse, buys only what it has room to store,
  and subtracts what is already in the air. The first pass did none of that: one rival spent thousands
  on coke and stored none of it, because the shelf was already full when the plane landed.

### Flights take time

- A town's distance is now time as well as turns, for the player as much as for a mule. Travel used
  to be instant, which made distance a pure turn cost: you were somewhere else the moment you decided
  to be.
- While the flight is running you are in the air and cannot act. That check lives in the services
  rather than at each endpoint: there are two dozen ways to act and only one set of places where
  acting happens, and a guard the endpoints have to remember is one that will eventually be forgotten.

### Rivals with a history

- They already fought each other. What they lacked was memory: a rival picked whoever was richest
  every single time and forgot being robbed the moment it happened, so nothing between two of them
  ever turned into a story and the world read as weather rather than as people.
- A grudge is read from the fights that actually happened, not kept as a score, so it is exactly as
  old as the last punch and there is nothing to prune. It also never makes a rival reckless: the win
  margin and the anti-farm rules still decide what they will take on, and a grudge only settles which
  of the fights they were already willing to have they actually pick.
- How hard it lands is character. The Hard Charger weights a score at nearly the target's own worth
  and carries it for three days; the Banker treats a robbery as a cost of doing business and has
  forgotten by the morning.
- World news carries the loudest quarrel, mutual ahead of one-sided, so it is something you can watch
  rather than something happening out of sight.

### How the rivals play

- A rival used to take one action every twenty-odd minutes, evenly, forever. Nothing about that is
  what a player does, and a world of them read as machinery rather than as opponents.
- Rivals now play in sittings. Between them they are gone, and turns bank up exactly as a real
  player's do; then they sit down and spend the lot. A sitting ends when the bank runs dry, with an
  action count and a wall clock only as backstops, because running out of turns is what really ends
  an evening.
- Each rival keeps its own hours, so the world has a rush hour and a quiet stretch. A fifth of them
  keep no hours at all: without those the board is dead for anyone who plays at an odd time, and the
  point of rivals is that the world moves whether you are there or not.
- How often a rival plays is read off its personality rather than a separate dial, using the same
  pacing the personalities already carried. Eager ones sit down five or six times a day, patient ones
  two or three.
- Rivals hesitate. A quarter of the beats in a sitting pass with nothing happening, which is reading
  the screen and changing your mind, and it is the difference between a person and a loop.
- Because rivals now sleep, idle time stopped being a health signal on its own: one quiet for four
  hours is asleep, not stuck. The admin's rivals table shows each one's habits and whether it is
  playing right now, which is what actually distinguishes the two.

### Carried out of 0.2.4

- Organizations. Everything else planned for it shipped.

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

Travel arrived in 0.2.4, as timed flights you cannot act from.

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

It should report version `0.2.6`.

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
