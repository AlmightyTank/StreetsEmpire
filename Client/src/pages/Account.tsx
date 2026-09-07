import React, { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { api, discordStartUrl, profileBanners } from '../api'
import type { Account, AccountInviteKey, AuthProviders, BlockedList, Dashboard, PlayerSession,
  PlayerTitle, ProfileBanner } from '../api'
import { compactDateTime, money, number } from '../format'
import { AdminMetric, bannerClass, betaKeyStatusClass, BUSY, Button, copyToClipboard,
  countdown, DismissibleMessage, firstReason, PlayerAvatar, ProfileBadgeStrip, profileAccentClass,
  secondsUntil, StatusRow, timeUntil, useRouteTab, useSecondsTicker, type Blocked } from '../ui'
import { applyPreferences, loadPreferences, savePreferences, systemPrefersReducedMotion,
  watchSystemMotion, type Preferences } from '../preferences'
import type { PageContext } from '../pagecontext'

/*
  Everything about the person rather than the empire: the name, the address, how you get back in.

  Opened rarely and never in the middle of a session, which makes it the third thing worth fetching
  on demand rather than shipping to everybody who loads the page.
*/
const ACCOUNT_TABS = ['profile', 'display', 'signin', 'invites', 'privacy', 'alerts', 'security'] as const
type AccountTab = typeof ACCOUNT_TABS[number]

const ACCOUNT_TAB_META: Record<AccountTab, { label: string, kicker: string }> = {
  profile: { label: 'Profile', kicker: 'Who you are here' },
  display: { label: 'Display', kicker: 'How this device shows it' },
  signin: { label: 'Account', kicker: 'Name and sign-in' },
  invites: { label: 'Invites', kicker: 'Beta keys you hold' },
  privacy: { label: 'Privacy', kicker: 'Discord and messages' },
  alerts: { label: 'Alerts', kicker: 'Email and sync' },
  security: { label: 'Security', kicker: 'Sessions and last doors' },
}

const PROFILE_ACCENTS: Account['profileAccent'][] = ['Gold', 'Teal', 'Rose', 'Steel']


/**
 * How this device shows the game, which is not a fact about the account: a phone and a monitor want
 * different densities, and reduced motion belongs to the machine that is doing the moving. Kept in
 * localStorage for that reason - see preferences.ts.
 */
function AccountDisplayPanel() {
  const [preferences, setPreferences] = useState<Preferences>(loadPreferences)

  const change = (next: Preferences) => {
    setPreferences(next)
    savePreferences(next)
    applyPreferences(next)
  }

  const systemReduced = systemPrefersReducedMotion()

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title"><h2>Display</h2><span>This device only</span></div>
    <p className="text-body-secondary">
      Kept on this device rather than on your account, because the answers are usually different on a
      phone and on a monitor. Signing in somewhere else starts from that machine's own settings.
    </p>

    <div className="d-grid gap-3">
      <label className="form-check form-switch d-flex align-items-start gap-2 m-0">
        <input
          className="form-check-input flex-shrink-0"
          type="checkbox"
          role="switch"
          checked={preferences.compact}
          onChange={event => change({ ...preferences, compact: event.target.checked })}
        />
        <span className="min-w-0">
          <strong className="d-block">Compact</strong>
          <small className="text-body-tertiary">
            Tighter rows and padding on the long lists - the leaderboard, the feed, the market. Buttons
            and the tab bar keep their size, since a smaller target is a harder one to hit.
          </small>
        </span>
      </label>

      <label className="form-check form-switch d-flex align-items-start gap-2 m-0">
        <input
          className="form-check-input flex-shrink-0"
          type="checkbox"
          role="switch"
          checked={preferences.reduceMotion ?? systemReduced}
          onChange={event => change({ ...preferences, reduceMotion: event.target.checked })}
        />
        <span className="min-w-0">
          <strong className="d-block">Reduce animations</strong>
          <small className="text-body-tertiary">
            {preferences.reduceMotion === null
              ? `Following this device, which currently asks for ${systemReduced ? 'reduced' : 'full'} motion.`
              : 'Set here, ignoring what this device asks for.'}
          </small>
        </span>
      </label>

      {preferences.reduceMotion !== null && <div>
        <button
          className="btn btn-link p-0 text-body-secondary"
          type="button"
          onClick={() => change({ ...preferences, reduceMotion: null })}
        >Follow this device instead</button>
      </div>}
    </div>

    <hr className="my-3" />
    <p className="text-body-tertiary small mb-0">
      The game is dark and only dark for now. A light theme is not a switch here: every panel, input and
      table colour is compiled into the stylesheet as a dark value, so light means authoring a second
      palette rather than flipping one.
    </p>
  </section>
}

function AccountInvitesPanel({ busy }: { busy: boolean }) {
  const [keys, setKeys] = useState<AccountInviteKey[]>([])
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)
  const available = keys.filter(key => key.status === 'Available' && key.usesLeft > 0)

  const load = async () => {
    setLoading(true); setError('')
    try {
      const board = await api.invites()
      setKeys(board.keys)
    } catch (e) { setError((e as Error).message) }
    finally { setLoading(false) }
  }
  useEffect(() => { void load() }, [])

  const copy = async (value: string, said: string) => {
    try {
      await copyToClipboard(value)
      setMessage(said)
    } catch { setError('Could not copy to the clipboard.') }
  }

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title">
      <h2>Invites</h2>
      <span>{loading ? 'Reading' : `${available.length} available`}</span>
    </div>
    {(error || message) && <div className="d-grid gap-2 mb-3">
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
    </div>}

    <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-3">
      <div className="tnum d-grid gtc-3 gap-2">
        <AdminMetric label="Total" value={number.format(keys.length)} />
        <AdminMetric label="Available" value={number.format(available.length)} />
        <AdminMetric label="Used" value={number.format(keys.filter(key => key.status === 'Used').length)} />
      </div>
      <Button
        className="btn btn-outline-primary"
        type="button"
        blocked={firstReason(
          busy && BUSY,
          available.length === 0 && 'You have no unused invites left to copy.',
        )}
        onClick={() => void copy(available.map(key => key.displayCode).join('\n'), 'Available invites copied.')}
      >Copy Available</Button>
    </div>

    <div className="table-responsive">
      <table className="table table-sm align-middle mb-0">
        <thead>
          <tr>
            <th>Key</th>
            <th>Status</th>
            <th>Uses</th>
            <th>Redeemed by</th>
            <th>Dates</th>
            <th className="text-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {!loading && keys.length === 0 && <tr>
            <td colSpan={6} className="text-body-tertiary">No invites have been issued to this account.</td>
          </tr>}
          {loading && <tr><td colSpan={6} className="text-body-tertiary">Reading your invites.</td></tr>}
          {keys.map(key => <tr key={key.id}>
            <td className="tnum">
              <strong>{key.displayCode}</strong>
              {key.label && <small className="d-block text-body-tertiary text-truncate">{key.label}</small>}
            </td>
            <td><span className={`badge ${betaKeyStatusClass(key.status)}`}>{key.status}</span></td>
            <td className="tnum">{key.uses} / {key.maxUses}<small className="d-block text-body-tertiary">{key.usesLeft} left</small></td>
            <td className="small">{key.redeemedByPlayerName ?? 'Not redeemed'}</td>
            <td className="small">
              <span className="d-block">Made {compactDateTime(key.createdAtUtc)}</span>
              <span className="d-block text-body-tertiary">Redeemed {compactDateTime(key.redeemedAtUtc)}</span>
            </td>
            <td className="text-end">
              <button className="btn btn-outline-secondary btn-sm" type="button" onClick={() => void copy(key.displayCode, 'Invite copied.')}>
                Copy
              </button>
            </td>
          </tr>)}
        </tbody>
      </table>
    </div>
  </section>
}





/**
 * The account tab.
 *
 * Everything a player owns hangs off one account, and until recently the only thing holding that
 * account was a username and a password chosen on the day they signed up, with no way to change
 * either and nowhere to look at them. This is that place.
 *
 * The rule the whole tab is arranged around is that at least one way in has to stay open. A player
 * who removes their password and then disconnects Discord owns an empire nobody can reach, so the
 * page says which is the last one standing and the server refuses the change regardless of what the
 * page says - this is the explanation, not the enforcement.
 *
 * It keeps its own state rather than going through the shared act(), because none of it is a game
 * action: nothing here spends a turn, moves a number, or belongs in the activity log, and running it
 * through the dashboard refresh would only throw away the one sentence worth reading.
 */
export function AccountPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('account', ACCOUNT_TABS, 'profile')
  const [account, setAccount] = useState<Account | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [email, setEmail] = useState('')

  const load = async () => {
    try {
      const loaded = await api.account()
      setAccount(loaded)
      setEmail(loaded.email ?? '')
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  /** Every control on the tab does the same three things, so they say so once. */
  const run = async (fn: () => Promise<Account | void>, said: string, form?: HTMLFormElement) => {
    setBusy(true); setError(''); setNotice('')
    const previousPlayerName = account?.playerName
    try {
      const updated = await fn()
      if (updated) {
        setAccount(updated)
        setEmail(updated.email ?? '')
        if (previousPlayerName && updated.playerName !== previousPlayerName) await ctx.refresh()
      }
      setNotice(said)
      // Passwords typed into a form have no business surviving the submit that used them.
      form?.querySelectorAll('input[type=password]').forEach(input => { (input as HTMLInputElement).value = '' })
    } catch (e) {
      setError((e as Error).message)
      // A refused attempt still burned one, and the count only comes back on a fresh read.
      await load()
    }
    finally { setBusy(false) }
  }

  if (!account) return <div className="d-grid gap-3">
    <section className="card p-3"><p className="text-body-tertiary small mb-0">Reading your account.</p></section>
    {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
  </div>

  // Only what the panels take. Spreading the whole component state would let a panel quietly start
  // depending on something it has no business touching.
  const panel: AccountPanel = { account, busy, run, fail: setError }

  /*
    Accounts made before signing up required one of the two exist, and nothing was ever going to tell
    them. They keep working - it is a rule about signing up, not about carrying on playing - but an
    account with no way back is one forgotten password from being gone, and the owner should hear that
    from the page rather than from the day it happens.
  */
  const strandable = waysBackIn(account).length === 0

  return <div className="d-grid gtc-1 gap-3 align-items-start">
    <nav className="d-grid gtc-fill-150 gap-1 border rounded p-1">
      {ACCOUNT_TABS.map(name => <button
        key={name}
        type="button"
        className={`admin-tab btn d-grid gap-1 text-start px-3 py-2 ${tab === name ? 'active' : ''}`}
        aria-current={tab === name ? 'page' : undefined}
        onClick={() => setTab(name)}
      >
        <strong>{ACCOUNT_TAB_META[name].label}</strong>
        <span className="small opacity-75">{ACCOUNT_TAB_META[name].kicker}</span>
      </button>)}
    </nav>

    {(error || notice) && <div className="d-grid gap-2">
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {notice && <DismissibleMessage className="alert alert-success" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
    </div>}

    {/* Not dismissible, and on every tab. It is true until it is fixed, and hiding it would be doing
        the player a favour they did not ask for. */}
    {strandable && <div className="alert alert-warning d-flex flex-wrap align-items-center justify-content-between gap-3 mb-0">
      <span>
        <strong>There is no way back into this account.</strong> Forget your password and it is gone -
        confirm an email address or connect Discord, and there is a way back.
      </span>
      {tab !== 'signin' && <button className="btn btn-warning flex-shrink-0" type="button" onClick={() => setTab('signin')}>
        Fix this
      </button>}
    </div>}

    <div className="account-grid d-grid gtc-1 gtc-xl-2 gap-3 align-items-start min-w-0">
      {tab === 'profile' && <AccountProfilePanel {...panel} dashboard={ctx.dashboard} onTab={setTab} />}
      {tab === 'signin' && <>
        <AccountNamePanel {...panel} />
        <AccountEmailPanel {...panel} email={email} setEmail={setEmail} />
        <AccountPasswordPanel {...panel} />
        <AccountDiscordPanel {...panel} />
      </>}
      {tab === 'invites' && <AccountInvitesPanel busy={busy} />}
      {tab === 'display' && <>
        <AccountDisplayPanel />
        <AccountWalkthroughPanel onTour={ctx.openTour} />
      </>}
      {tab === 'privacy' && <AccountPrivacyPanel {...panel} />}
      {tab === 'alerts' && <AccountAlertsPanel {...panel} />}
      {tab === 'security' && <AccountSecurityPanel {...panel} onTab={setTab} />}
    </div>
  </div>
}

/**
 * A door back into the walkthrough.
 *
 * It used to live on the Getting Started panel, which is the one place it was certain to be useless:
 * that panel is on the Overview, it is aimed at somebody in their first week, and it disappears once
 * the opening ladder is done. The player who actually wants this is the one who came back after a
 * month and cannot remember what banking was for - and by then the button had gone.
 *
 * Settings, because that is where somebody looks for a thing they half remember switching off.
 */
function AccountWalkthroughPanel({ onTour }: { onTour: () => void }) {
  return <section className="card p-3">
    <div className="panel-title"><h2>Walkthrough</h2><span>The opening four moves</span></div>
    <p className="text-body-secondary mt-3 mb-0">
      The short tour a new account gets: pricing a shift before working it, working one, banking what
      it paid, and buying what the next one burns. It runs once when the account is made. Nothing here
      is spent by looking at it again.
    </p>
    <div className="d-flex mt-3">
      <button className="btn btn-primary" type="button" onClick={onTour}>Run it again</button>
    </div>
  </section>
}

/** What the panels below all take. Bundled because every one of them takes all of it. */
type AccountPanel = {
  account: Account
  busy: boolean
  run: (fn: () => Promise<Account | void>, said: string, form?: HTMLFormElement) => Promise<void>
  /** For a refusal the page can make on its own, without troubling the server about it. */
  fail: (message: string) => void
}

function AccountNamePanel({ account, busy, run }: AccountPanel) {
  const [playerName, setPlayerName] = useState(account.playerName)
  useEffect(() => { setPlayerName(account.playerName) }, [account.playerName])
  const nameTicker = useSecondsTicker(!!account.playerNameChangeReadyAtUtc)
  const nameCooldownSeconds = secondsUntil(account.playerNameChangeReadyAtUtc, nameTicker)

  const savePlayerName = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(() => api.setPlayerName(playerName.trim()), 'Player name changed.')
  }

  return <section className="card p-3">
    <div className="panel-title"><h2>Player Name</h2><span>{nameCooldownSeconds > 0 ? timeUntil(account.playerNameChangeReadyAtUtc!) : 'Ready'}</span></div>
    <p>
      This is the name other players see on ladders, news, profiles, chat, crew rosters, wars, and season
      tables. Your username stays private and still handles sign-in.
    </p>
    <form className="d-grid gap-3" onSubmit={savePlayerName}>
      <label className="field">
        Display name
        <input
          className="form-control"
          maxLength={32}
          value={playerName}
          onChange={event => setPlayerName(event.target.value)}
        />
        <small className="form-text">
          {nameCooldownSeconds > 0
            ? `You can change it again in ${timeUntil(account.playerNameChangeReadyAtUtc!)}.`
            : 'Names must be 3-32 characters.'}
        </small>
      </label>
      <Button
        className="btn btn-primary"
        blocked={firstReason(
          busy && BUSY,
          playerName.trim().length < 3 && 'Player name must be at least three characters.',
          playerName.trim().length > 32 && 'Player name must be 32 characters or less.',
          playerName.trim() === account.playerName && 'That is already your player name.',
          nameCooldownSeconds > 0 && `You can change your player name again in ${timeUntil(account.playerNameChangeReadyAtUtc!)}.`,
        )}
      >
        {busy ? 'Working...' : 'Change Name'}
      </Button>
    </form>
  </section>
}

function AccountProfilePanel({ account, dashboard, busy, run, fail, onTab }: AccountPanel & { dashboard: Dashboard, onTab: (tab: AccountTab) => void }) {
  // Two names, and they are not the same thing, which is worth saying plainly on the page where both
  // appear: one is how you sign in and nobody else sees it, the other is what the whole city calls you.
  const open = waysIn(account)
  const [tagline, setTagline] = useState(account.profileTagline ?? '')
  const [pronouns, setPronouns] = useState(account.profilePronouns ?? '')
  const [location, setLocation] = useState(account.profileLocation ?? '')
  const [accent, setAccent] = useState<Account['profileAccent']>(account.profileAccent)
  const [banner, setBanner] = useState<ProfileBanner>(account.profileBanner)
  const [featured, setFeatured] = useState(account.featuredTitle ?? '')
  // What the picker may offer is what they hold today, which is a live answer rather than part of the
  // account - see the endpoint. Empty for almost everybody, which is what makes a title worth having.
  const [held, setHeld] = useState<PlayerTitle[]>([])
  useEffect(() => { void (async () => { try { setHeld(await api.myTitles()) } catch { /* the picker just stays empty */ } })() }, [])
  useEffect(() => {
    setTagline(account.profileTagline ?? '')
    setPronouns(account.profilePronouns ?? '')
    setLocation(account.profileLocation ?? '')
    setAccent(account.profileAccent)
    setBanner(account.profileBanner)
    setFeatured(account.featuredTitle ?? '')
  }, [account.profileTagline, account.profilePronouns, account.profileLocation, account.profileAccent,
      account.profileBanner, account.featuredTitle])

  const saveProfile = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(() => api.setProfile(tagline.trim(), pronouns.trim(), location.trim(), accent, banner, featured), 'Profile saved.')
  }

  const uploadAvatar = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const file = (new FormData(form).get('avatar') as File | null)
    if (!(file instanceof File) || file.size === 0) { fail('Choose an image file first.'); return }
    if (file.size > 1_000_000) { fail('Avatar image must be 1 MB or smaller.'); return }
    void run(() => api.uploadCustomAvatar(file), 'Custom avatar uploaded.', form)
  }

  return <>
    <section className="card p-3 gcol-xl-full">
      <div className="d-flex flex-wrap align-items-center gap-3 mb-3">
        <AccountAvatar account={account} size={72} />
        <div className="min-w-0 flex-fill">
          <div className="panel-title mb-0"><h2>{account.playerName}</h2><span>{dashboard.city} / Rank #{dashboard.rank}</span></div>
          <small className="text-body-tertiary">
            {account.avatarSource === 'Discord' ? 'Using Discord avatar' : account.avatarSource === 'Custom' ? 'Using custom avatar' : 'Using default avatar'}
          </small>
          {account.profileTagline && <p className={`mb-0 mt-1 ${profileAccentClass(account.profileAccent)} text-truncate`}>{account.profileTagline}</p>}
          {(account.profilePronouns || account.profileLocation) && <small className="d-block text-body-tertiary text-truncate">
            {[account.profilePronouns, account.profileLocation].filter(Boolean).join(' / ')}
          </small>}
          <ProfileBadgeStrip badges={account.profileBadges ?? []} />
        </div>
      </div>
      <div className="tnum d-grid gtc-2 gtc-md-4 gap-2 mb-3">
        <AdminMetric label="Player name" value={account.playerName} />
        <AdminMetric label="Username" value={account.username} />
        <AdminMetric label="Ways in" value={`${open.length} of 2`} />
        <AdminMetric label="Since" value={new Date(account.createdAtUtc).toLocaleDateString()} />
      </div>
      <p className="mb-0">
        Your <strong className="text-primary">player name</strong> is what the city sees - the ladder, the
        news, the wanted list. Your <strong className="text-primary">username</strong> is only ever how you
        sign in, and nobody else is shown it.
      </p>
      <div className="d-grid gtc-1 gtc-lg-2 gap-3 mt-3">
        <form className="d-grid gap-3 border rounded bg-body-secondary p-3" onSubmit={saveProfile}>
          <label className="field">
            Tagline
            <input
              className="form-control"
              maxLength={140}
              value={tagline}
              placeholder="One line the city sees"
              onChange={event => setTagline(event.target.value)}
            />
            <small className="form-text">{Math.max(0, 140 - tagline.length)} characters left.</small>
          </label>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Pronouns
              <input
                className="form-control"
                maxLength={64}
                value={pronouns}
                placeholder="Optional"
                onChange={event => setPronouns(event.target.value)}
              />
            </label>
            <label className="field">
              Profile location
              <input
                className="form-control"
                maxLength={64}
                value={location}
                placeholder="Optional"
                onChange={event => setLocation(event.target.value)}
              />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Accent
              <select
                className="form-select"
                value={accent}
                onChange={event => setAccent(event.target.value as Account['profileAccent'])}
              >
                {PROFILE_ACCENTS.map(option => <option value={option} key={option}>{option}</option>)}
              </select>
            </label>
            <label className="field">
              Banner
              <select
                className="form-select"
                value={banner}
                onChange={event => setBanner(event.target.value as ProfileBanner)}
              >
                {profileBanners.map(option => <option value={option.key} key={option.key}>{option.label}</option>)}
              </select>
              <small className="form-text">Behind your name when somebody opens your profile.</small>
            </label>
          </div>
          {/*
            Titles are worked out fresh from the day's fighting, so this offers what you hold now and
            remembers the choice either way - one taken from you this afternoon is one you may hold
            again tomorrow, and forgetting it every time would make this a setting nobody could keep.
          */}
          <label className="field">
            Lead with
            <select
              className="form-select"
              value={featured}
              onChange={event => setFeatured(event.target.value)}
            >
              <option value="">Whatever I hold</option>
              {held.map(title => <option value={title.key} key={title.key}>{title.title}</option>)}
              {/* Their choice, still selectable, even on a day they have lost it. */}
              {featured !== '' && !held.some(x => x.key === featured)
                && <option value={featured}>{featured} (not held today)</option>}
            </select>
            <small className="form-text">
              {held.length === 0
                ? 'You hold no titles today. They are won by the day’s fighting, and most days nobody holds one.'
                : 'Shown first on your card, ahead of the rest.'}
            </small>
          </label>

          {/* Shown rather than described. A named gradient means nothing until you see it. */}
          <div className={`profile-banner ${bannerClass(banner)} d-flex align-items-end p-2`}>
            <strong className={`${profileAccentClass(accent)} text-truncate`}>{account.playerName}</strong>
          </div>
          <Button
            className="btn btn-primary"
            blocked={firstReason(
              busy && BUSY,
              tagline.trim() === (account.profileTagline ?? '')
                && pronouns.trim() === (account.profilePronouns ?? '')
                && location.trim() === (account.profileLocation ?? '')
                && accent === account.profileAccent
                && banner === account.profileBanner
                && featured === (account.featuredTitle ?? '')
                && 'Nothing on the card has been changed.',
            )}
          >
            {busy ? 'Working...' : 'Save Profile'}
          </Button>
        </form>

        <form className="avatar-form d-grid gap-3 border rounded bg-body-secondary p-3" onSubmit={uploadAvatar}>
          <div className="d-flex align-items-center gap-3 min-w-0">
            <PlayerAvatar name={account.playerName} username={account.username} avatarUrl={account.customAvatarUrl} size={56} />
            <div className="min-w-0">
              <span className="eyebrow d-block">Uploaded avatar</span>
              <strong className="d-block text-truncate">{account.customAvatarUrl ? 'Ready' : 'None uploaded'}</strong>
            </div>
          </div>
          <label className="field">
            Custom avatar
            <input className="form-control" name="avatar" type="file" accept="image/png,image/jpeg,image/gif,image/webp" />
            <small className="form-text">PNG, JPG, GIF, or WebP. 1 MB max.</small>
          </label>
          <div className="avatar-actions d-flex flex-wrap gap-2">
            <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Upload and Use'}</Button>
            <Button
              className="btn btn-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                !account.customAvatarUrl && 'You have not uploaded a picture yet.',
                account.avatarSource === 'Custom' && 'Your uploaded picture is the one already in use.',
              )}
              onClick={() => void run(() => api.setAvatarSource('Custom'), 'Custom avatar selected.')}
            >Use Custom</Button>
            <Button
              className="btn btn-outline-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                account.avatarSource === 'None' && 'You are already on the default picture.',
              )}
              onClick={() => void run(() => api.setAvatarSource('None'), 'Default avatar selected.')}
            >Use Default</Button>
            <Button
              className="btn btn-outline-danger"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                !account.customAvatarUrl && 'There is no uploaded picture to remove.',
              )}
              onClick={() => void run(() => api.deleteCustomAvatar(), 'Custom avatar removed.')}
            >Remove Custom</Button>
          </div>
        </form>
      </div>
    </section>

    {/*
      Two panels rather than one, because a name and a way in are not the same kind of thing and putting
      them in one list of "ways in" says something false. An email address is a second name for the
      password door - it opens nothing on its own, and the day the password goes it is worth nothing.
      A player reading a tile that said otherwise might close the only door they had.
    */}
    <section className="card p-3">
      <div className="panel-title"><h2>Ways In</h2><span>{open.length} of 2</span></div>
      <p>
        Two things can actually let you in, and you need to keep at least one. The game will not let you
        close the last one.
      </p>
      <div className="d-grid gtc-1 gtc-md-2 gap-2">
        <WayInTile label="Password" open={account.hasPassword} detail={account.hasPassword ? 'Set' : 'Never set'} />
        <WayInTile label="Discord" open={account.discordConnected} detail={account.discordUsername ?? 'Not connected'} />
      </div>
      <button className="btn btn-secondary mt-3" type="button" onClick={() => onTab('signin')}>Manage sign-in</button>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>Names You Can Type</h2><span>{signInNames(account).length} of 2</span></div>
      <p>
        Either of these goes in the box on the sign-in screen, with your password. They are names, not
        keys - neither of them opens anything without the password beside it.
      </p>
      <div className="d-grid gtc-1 gtc-md-2 gap-2">
        <WayInTile label="Username" open detail={account.username} />
        <WayInTile
          label="Email"
          open={account.emailVerified}
          detail={account.email
            ? account.emailVerified ? account.email : `${account.email} - not confirmed`
            : 'None set'}
        />
      </div>
    </section>
  </>
}

function AccountAvatar({ account, size = 56 }: { account: Account, size?: number }) {
  return <PlayerAvatar name={account.playerName} username={account.username} avatarUrl={account.avatarUrl} size={size} />
}


function waysIn(account: Account) {
  return [
    account.hasPassword && 'password',
    account.discordConnected && 'discord',
  ].filter(Boolean)
}

/** The names the sign-in box will accept. Only a confirmed address is one. */
function signInNames(account: Account) {
  return ['username', account.emailVerified && 'email'].filter(Boolean)
}

/**
 * The things that could get somebody back in, which is a different list from the things that let them
 * in. A password is a way in and is not a way back: forget it and there is nothing left to prove the
 * account was ever yours. Only a confirmed address and a Discord answer this one.
 */
function waysBackIn(account: Account) {
  return [
    account.emailVerified && 'email',
    account.discordConnected && 'discord',
  ].filter(Boolean)
}

function WayInTile({ label, open, detail }: { label: string, open: boolean, detail: string }) {
  return <div className={`stat d-grid gap-1 border rounded bg-body-secondary p-3 ${open ? 'border-primary' : ''}`}>
    <span className="eyebrow">{label}</span>
    <strong className={`min-w-0 fs-6 lh-1 text-truncate ${open ? 'text-primary' : 'text-body-tertiary'}`}>
      {open ? 'Open' : 'Closed'}
    </strong>
    <small className="small text-truncate" title={detail}>{detail}</small>
  </div>
}

function AccountEmailPanel({ account, busy, run, email, setEmail }: AccountPanel & { email: string, setEmail: (value: string) => void }) {
  const pending = account.verification
  const now = useSecondsTicker(pending !== null)
  const expiresIn = secondsUntil(pending?.expiresAtUtc, now)
  const resendIn = secondsUntil(pending?.resendableAtUtc, now)
  const emailChanged = (account.email ?? '') !== email.trim()
  // Emptying the box is a removal, and a removal is only allowed while something else could still get
  // them back in. Changing it to a different address is always fine - one is still there to confirm.
  const removingLastWayBack = email.trim().length === 0 && account.email !== null && !account.discordConnected

  const saveEmail = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const current = new FormData(form).get('currentPassword')
    void run(
      () => api.setEmail(email.trim(), String(current ?? '')),
      email.trim() ? 'Email saved. Confirm it to sign in with it.' : 'Email removed.',
      form)
  }

  const confirm = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    void run(() => api.confirmEmail(String(new FormData(form).get('code') ?? '')), 'Address confirmed.', form)
      .then(() => { form.reset() })
  }

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Email</h2>
      <span className={account.emailVerified ? 'text-primary' : ''}>
        {!account.email ? 'None' : account.emailVerified ? 'Confirmed' : 'Not confirmed'}
      </span>
    </div>
    <p>
      A second name to sign in under, with the same password. It only becomes a way in once you have
      confirmed it, so an address typed by somebody who cannot read the mail opens nothing.
    </p>

    {/*
      Said out loud rather than hidden. Mail written to a server log is exactly right on a laptop and
      exactly wrong anywhere else, and a player who never gets a code deserves to know which it is.
    */}
    {!account.emailDelivers && <div className="alert alert-warning">
      No email provider is configured on this server, so codes are written to the server log instead of
      being sent. Fine for development; nobody will receive anything.
    </div>}

    {account.email && !account.emailVerified && <div className="border border-primary rounded p-3 mb-3 d-grid gap-3">
      <div>
        <span className="eyebrow d-block">Confirm this address</span>
        <p className="mb-0 mt-1">
          {pending
            ? <>A six-digit code went to <strong className="text-primary">{pending.sentTo}</strong>.
              It is good for another <strong className="tnum">{countdown(expiresIn)}</strong>, and you have{' '}
              <strong className="tnum">{pending.attemptsRemaining}</strong> {pending.attemptsRemaining === 1 ? 'try' : 'tries'} left.</>
            : <>Nothing is waiting. Ask for a code and it will arrive at{' '}
              <strong className="text-primary">{account.email}</strong>.</>}
        </p>
      </div>

      {pending && expiresIn > 0 && <form className="d-flex flex-wrap align-items-end gap-2" onSubmit={confirm}>
        <label className="field flex-fill min-w-0">
          Code
          {/*
            One box rather than six. Six boxes look the part and then fight the player over pasting,
            backspacing and autofill, all to save typing that nobody was struggling with.
          */}
          <input
            className="form-control tnum fs-4 text-center"
            style={{ letterSpacing: '.4em' }}
            name="code"
            inputMode="numeric"
            autoComplete="one-time-code"
            pattern="[0-9]*"
            maxLength={6}
            placeholder="000000"
            required
          />
        </label>
        <Button className="btn btn-primary" blocked={busy && BUSY}>{busy ? 'Working...' : 'Confirm'}</Button>
      </form>}

      <Button
        className="btn btn-secondary"
        type="button"
        blocked={firstReason(
          busy && BUSY,
          resendIn > 0 && `A code went out already. You can ask for another in ${countdown(resendIn)}.`,
        )}
        onClick={() => void run(() => api.sendEmailCode(), 'A new code is on its way.')}
      >
        {resendIn > 0
          ? `Send another in ${countdown(resendIn)}`
          : pending ? 'Send a new code' : 'Send a code'}
      </Button>
    </div>}

    {account.emailVerified && account.emailVerifiedAtUtc && <p className="text-body-tertiary small">
      Confirmed on {new Date(account.emailVerifiedAtUtc).toLocaleDateString()}.
    </p>}

    <form className="d-grid gap-3" onSubmit={saveEmail}>
      <label className="field">
        Address
        <input
          className="form-control"
          type="email"
          maxLength={254}
          value={email}
          placeholder="nobody@example.com"
          onChange={event => setEmail(event.target.value)}
        />
        <small className="form-text">Empty removes it. Changing it starts the confirmation again.</small>
      </label>
      {account.hasPassword && <label className="field">
        Current password
        <input className="form-control" name="currentPassword" type="password" autoComplete="current-password" required />
        <small className="form-text">Changing where a sign-in can come from costs the password.</small>
      </label>}
      {/*
        Removing the last way back in is refused by the server whatever this button says, so the button
        says it first. A refusal a player could have seen coming is a worse refusal than one that
        explains itself before they click.
      */}
      <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !emailChanged && 'That is the address already on the account.',
        removingLastWayBack && 'This address is the only way back into your account if you forget your password. Connect Discord on this page and you can remove it.',
      )}>
        {busy ? 'Working...' : email.trim() ? 'Save Email' : 'Remove Email'}
      </Button>
    </form>
  </section>
}

function AccountPasswordPanel({ account, busy, run, fail }: AccountPanel) {
  const savePassword = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const next = String(data.get('newPassword') ?? '')
    // Caught here rather than sent: the server has no way to know what was typed in the second box,
    // and a round trip to be told something the page already knew is a round trip wasted.
    if (next !== String(data.get('confirmPassword') ?? '')) { fail('The two new passwords do not match.'); return }
    void run(
      () => api.setPassword(String(data.get('currentPassword') ?? ''), next),
      account.hasPassword ? 'Password changed. Every other session has been signed out.' : 'Password set.',
      form)
  }

  return <section className="card p-3">
    <div className="panel-title"><h2>Password</h2><span>{account.hasPassword ? 'Set' : 'None'}</span></div>
    <p>
      {account.hasPassword
        ? 'Changing it signs out every other session on this account, and keeps this one.'
        : 'You signed up through Discord and have never set one. Set a password and you can sign in with your username as well.'}
    </p>
    <form className="d-grid gap-3" onSubmit={savePassword}>
      {account.hasPassword && <label className="field">
        Current password
        <input className="form-control" name="currentPassword" type="password" autoComplete="current-password" required />
      </label>}
      <label className="field">
        New password
        <input className="form-control" name="newPassword" type="password" autoComplete="new-password" minLength={8} required />
        <small className="form-text">Eight characters at the very least.</small>
      </label>
      <label className="field">
        New password again
        <input className="form-control" name="confirmPassword" type="password" autoComplete="new-password" minLength={8} required />
      </label>
      <Button className="btn btn-primary" blocked={busy && BUSY}>
        {busy ? 'Working...' : account.hasPassword ? 'Change Password' : 'Set Password'}
      </Button>
    </form>
  </section>
}

function AccountDiscordPanel({ account, busy, run }: AccountPanel) {
  // Disconnecting takes away a way in, which is the same kind of act as changing the address, so it
  // costs the same thing. An account with no password has nothing to prove with and is not asked.
  const [password, setPassword] = useState('')
  // Two separate reasons the connection might be stuck, and they are not the same reason - one is
  // about getting in at all, the other about getting back in after forgetting the password.
  const discordIsTheOnlyWayIn = account.discordConnected && !account.hasPassword
  const discordIsTheOnlyWayBackIn = account.discordConnected && !account.emailVerified

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Discord</h2><span>{account.discordConnected ? 'Connected' : 'Not connected'}</span>
    </div>
    {account.discordConnected
      ? <>
        <p>
          Connected to <strong className="text-primary">{account.discordUsername}</strong>
          {account.discordLinkedAtUtc && <> since {new Date(account.discordLinkedAtUtc).toLocaleDateString()}</>}.
          That Discord account signs straight in, on any browser, without a password.
        </p>
        {account.discordLinkRewardClaimedAtUtc && <div className="alert alert-success">
          Link reward claimed: $10,000, 25 condoms, 25 beer, and the Discord Connected title.
        </div>}
        {!account.discordLinkRewardClaimedAtUtc && <div className="alert alert-primary d-flex flex-wrap align-items-center justify-content-between gap-2">
          <span>Claim your first-link reward: $10,000, 25 condoms, 25 beer, and the Discord Connected title.</span>
          <Button
            className="btn btn-primary btn-sm"
            type="button"
            blocked={busy && BUSY}
            onClick={() => void run(() => api.claimDiscordLinkReward(), 'Discord link reward claimed.')}
          >
            {busy ? 'Working...' : 'Claim reward'}
          </Button>
        </div>}
        <div className="border rounded bg-body-secondary p-3 mb-3 d-grid gap-3">
          <div className="d-flex align-items-center gap-3 min-w-0">
            {account.discordAvatarUrl
              ? <img
                src={account.discordAvatarUrl}
                alt=""
                className="border border-primary object-fit-cover flex-shrink-0"
                style={{ width: 56, height: 56, borderRadius: '50%' }}
                referrerPolicy="no-referrer"
              />
              : <AccountAvatar account={account} />}
            <div className="min-w-0">
              <span className="eyebrow d-block">Avatar</span>
              <strong className="d-block text-truncate">
                {account.avatarSource === 'Discord'
                  ? 'Synced from Discord'
                  : account.avatarSource === 'Custom' ? 'Using custom avatar' : 'Default'}
              </strong>
              <small className="text-body-tertiary">
                {account.discordAvatarUrl ? 'Refresh after changing it on Discord.' : 'No custom Discord avatar found.'}
              </small>
            </div>
          </div>
          <div className="avatar-actions d-flex flex-wrap gap-2">
            <Button
              className="btn btn-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                !account.discordAvatarUrl && 'Discord has no custom picture for you to use.',
                account.avatarSource === 'Discord' && 'Your Discord picture is the one already in use.',
              )}
              onClick={() => void run(() => api.setAvatarSource('Discord'), 'Discord avatar selected.')}
            >
              Use Discord avatar
            </Button>
            <Button
              className="btn btn-outline-secondary"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                account.avatarSource === 'None' && 'You are already on the default picture.',
              )}
              onClick={() => void run(() => api.setAvatarSource('None'), 'Default avatar selected.')}
            >
              Use default
            </Button>
            <a className="btn btn-outline-secondary d-inline-flex align-items-center gap-2" href={discordStartUrl()}>
              <i className="bi bi-arrow-repeat" aria-hidden="true" />
              Refresh from Discord
            </a>
          </div>
        </div>
        {discordIsTheOnlyWayIn
          ? <div className="alert alert-warning mb-0">
            This is the only way into your empire. Set a password before disconnecting it.
          </div>
          : discordIsTheOnlyWayBackIn
          ? <div className="alert alert-warning mb-0">
            This is the only way back into your empire if you forget your password. Confirm an email
            address before disconnecting it.
          </div>
          : <div className="d-grid gap-2">
            {account.hasPassword && <label className="field">
              Current password
              <input
                className="form-control"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={event => setPassword(event.target.value)}
              />
              <small className="form-text">Taking away a way in costs the password, as changing your address does.</small>
            </label>}
            <Button
              className="btn btn-outline-danger"
              type="button"
              blocked={firstReason(
                busy && BUSY,
                account.hasPassword && password.length === 0 && 'Type your password above. Taking away a way in costs it.',
              )}
              onClick={() => void run(
                async () => { const a = await api.disconnectDiscord(password); setPassword(''); return a },
                'Discord disconnected.')}
            >Disconnect Discord</Button>
          </div>}
      </>
      : account.discordConfigured
        ? <>
          <p>
            Connect one and it becomes a way in: one button on the sign-in screen, no password typed.
            You keep your username and password either way.
          </p>
          <p className="text-body-tertiary small">
            First link pays $10,000, 25 condoms, 25 beer, and unlocks the Discord Connected title.
          </p>
          {/*
            A link, not a button. Connecting is the same round trip through Discord that signing in is,
            and the only difference is that this one starts with a session already in hand - which is
            what tells the callback to attach rather than to sign somebody in.
          */}
          <a className="btn btn-secondary d-inline-flex align-items-center justify-content-center gap-2" href={discordStartUrl()}>
            <i className="bi bi-discord" aria-hidden="true" />
            Connect Discord
          </a>
        </>
        : <p className="mb-0 text-body-tertiary">
          This server has no Discord credentials set, so there is nothing to connect to yet.
        </p>}
  </section>
}

function AccountPrivacyPanel({ account, busy, run }: AccountPanel) {
  const [showDiscord, setShowDiscord] = useState(account.showDiscordOnProfile)
  const [dmPolicy, setDmPolicy] = useState<Account['directMessagePolicy']>(account.directMessagePolicy)
  const [showActivity, setShowActivity] = useState(account.showActivityOnProfile)
  useEffect(() => {
    setShowDiscord(account.showDiscordOnProfile)
    setDmPolicy(account.directMessagePolicy)
    setShowActivity(account.showActivityOnProfile)
  }, [account.showDiscordOnProfile, account.directMessagePolicy, account.showActivityOnProfile])

  const changed = showDiscord !== account.showDiscordOnProfile
    || dmPolicy !== account.directMessagePolicy
    || showActivity !== account.showActivityOnProfile
  const save = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(() => api.setPrivacy(showDiscord, dmPolicy, showActivity), 'Privacy saved.')
  }

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title"><h2>Privacy</h2><span>{
      dmPolicy === 'Everyone' ? 'Open'
        : dmPolicy === 'Alliance' ? 'Crew only'
          : dmPolicy === 'AllianceAndPacts' ? 'Crew and allies'
            : 'Closed'
    }</span></div>
    <form className="d-grid gap-3" onSubmit={save}>
      <label className={`form-check form-switch border rounded bg-body-secondary p-3 ps-5 ${!account.discordConnected ? 'text-body-tertiary' : ''}`}>
        <input
          className="form-check-input"
          type="checkbox"
          checked={showDiscord}
          disabled={!account.discordConnected}
          onChange={event => setShowDiscord(event.target.checked)}
        />
        <strong className="d-block">Show Discord on public profile</strong>
        <small className="form-text">
          {account.discordConnected
            ? account.discordUsername ?? 'Connected Discord'
            : 'Connect Discord before showing it publicly.'}
        </small>
      </label>
      <label className="field">
        Direct messages
        <select
          className="form-select"
          value={dmPolicy}
          onChange={event => setDmPolicy(event.target.value as Account['directMessagePolicy'])}
        >
          <option value="Everyone">Everyone</option>
          <option value="AllianceAndPacts">My crew and our allies</option>
          <option value="Alliance">My crew only</option>
          <option value="Nobody">Nobody</option>
        </select>
        <small className="form-text">
          Allies are crews yours has a standing pact with. Existing blocks still win over this setting.
        </small>
      </label>
      {/*
        The one genuinely private thing on a profile, and the reason this is a switch rather than a
        blanket setting. Your city and your numbers are on the leaderboard whatever you choose here -
        this is the eight-action list with timestamps and takings, which is available nowhere else.
      */}
      <label className="form-check form-switch border rounded bg-body-secondary p-3 ps-5">
        <input
          className="form-check-input"
          type="checkbox"
          checked={showActivity}
          onChange={event => setShowActivity(event.target.checked)}
        />
        <strong className="d-block">Show recent activity on my profile</strong>
        <small className="form-text">
          The last eight things you did, with times and takings, to anybody who opens your profile. Your
          city and your worth are on the leaderboard either way; this is the part that is not.
        </small>
      </label>
      <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !changed && 'Nothing here has been changed.',
      )}>{busy ? 'Working...' : 'Save Privacy'}</Button>
    </form>
  </section>
}

function AccountAlertsPanel({ account, busy, run }: AccountPanel) {
  const [syncDiscord, setSyncDiscord] = useState(account.syncDiscordAvatar)
  const [security, setSecurity] = useState(account.emailSecurityNotices)
  const [combat, setCombat] = useState(account.emailCombatNotices)
  const [alliance, setAlliance] = useState(account.emailAllianceNotices)
  const [discordSecurity, setDiscordSecurity] = useState(account.discordSecurityNotices)
  const [discordCombat, setDiscordCombat] = useState(account.discordCombatNotices)
  const [discordCrew, setDiscordCrew] = useState(account.discordCrewNotices)
  const [discordMarket, setDiscordMarket] = useState(account.discordMarketNotices)
  const [discordMachine, setDiscordMachine] = useState(account.discordMachineNotices)
  const [bellCombat, setBellCombat] = useState(account.noticeCombat)
  const [bellCrew, setBellCrew] = useState(account.noticeCrew)
  const [bellMarket, setBellMarket] = useState(account.noticeMarket)
  useEffect(() => {
    setSyncDiscord(account.syncDiscordAvatar)
    setSecurity(account.emailSecurityNotices)
    setCombat(account.emailCombatNotices)
    setAlliance(account.emailAllianceNotices)
    setDiscordSecurity(account.discordSecurityNotices)
    setDiscordCombat(account.discordCombatNotices)
    setDiscordCrew(account.discordCrewNotices)
    setDiscordMarket(account.discordMarketNotices)
    setDiscordMachine(account.discordMachineNotices)
    setBellCombat(account.noticeCombat)
    setBellCrew(account.noticeCrew)
    setBellMarket(account.noticeMarket)
  }, [account.syncDiscordAvatar, account.emailSecurityNotices, account.emailCombatNotices, account.emailAllianceNotices,
      account.discordSecurityNotices, account.discordCombatNotices, account.discordCrewNotices,
      account.discordMarketNotices, account.discordMachineNotices,
      account.noticeCombat, account.noticeCrew, account.noticeMarket])

  const changed = syncDiscord !== account.syncDiscordAvatar
    || security !== account.emailSecurityNotices
    || combat !== account.emailCombatNotices
    || alliance !== account.emailAllianceNotices
    || discordSecurity !== account.discordSecurityNotices
    || discordCombat !== account.discordCombatNotices
    || discordCrew !== account.discordCrewNotices
    || discordMarket !== account.discordMarketNotices
    || discordMachine !== account.discordMachineNotices
    || bellCombat !== account.noticeCombat
    || bellCrew !== account.noticeCrew
    || bellMarket !== account.noticeMarket

  const save = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void run(
      () => api.setNotificationPreferences(
        syncDiscord,
        security,
        combat,
        alliance,
        discordSecurity,
        discordCombat,
        discordCrew,
        discordMarket,
        discordMachine,
        bellCombat,
        bellCrew,
        bellMarket),
      'Alert settings saved.')
  }

  return <section className="card p-3 gcol-xl-full">
    <div className="panel-title"><h2>Alerts</h2><span>{security || combat || alliance || discordSecurity || discordCombat || discordCrew || discordMarket || discordMachine ? 'On' : 'Quiet'}</span></div>
    <form className="d-grid gap-3" onSubmit={save}>
      <label className={`form-check form-switch border rounded bg-body-secondary p-3 ps-5 ${!account.discordConnected ? 'text-body-tertiary' : ''}`}>
        <input
          className="form-check-input"
          type="checkbox"
          checked={syncDiscord}
          disabled={!account.discordConnected}
          onChange={event => setSyncDiscord(event.target.checked)}
        />
        <strong className="d-block">Prefer Discord avatar after refresh</strong>
        <small className="form-text">
          {account.discordConnected
            ? 'When Discord refreshes and has an avatar, it becomes your selected account avatar.'
            : 'Connect Discord before turning this on.'}
        </small>
      </label>
      {/*
        Refreshing is a trip back through Discord rather than a call the server can make on its own,
        because no Discord token is kept here - only the account id it handed over. That is the more
        private arrangement of the two and this is the cost of it: one click, and Discord asks nothing
        again if you are still signed in there.

        The handle has always refreshed itself on every Discord sign-in. What was missing was any
        record of when, which is the half this reports.
      */}
      {account.discordConnected && <div className="border rounded bg-body-secondary p-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
        <div className="min-w-0">
          <strong className="d-block text-truncate">
            <i className="bi bi-discord me-1" aria-hidden="true" />
            {account.discordUsername ?? 'Connected'}
          </strong>
          <small className="text-body-tertiary">
            {account.discordSyncedAtUtc
              ? `Last checked ${new Date(account.discordSyncedAtUtc).toLocaleString()}.`
              : 'Not checked since this was added - refresh to pull your current handle and avatar.'}
          </small>
        </div>
        <a className="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-2" href={discordStartUrl()}>
          <i className="bi bi-arrow-clockwise" aria-hidden="true" />
          Refresh from Discord
        </a>
      </div>}
      <div>
        <span className="eyebrow d-block mb-2">By email</span>
      </div>
      <div className="d-grid gtc-1 gtc-md-3 gap-2">
        <NoticeToggle
          label="Security"
          detail="Password, Discord, sessions, and account access."
          checked={security}
          onChange={setSecurity}
        />
        <NoticeToggle
          label="Combat"
          detail="Future fight and defence email alerts."
          checked={combat}
          onChange={setCombat}
        />
        <NoticeToggle
          label="Crew"
          detail="Future crew requests, pacts, and transfers."
          checked={alliance}
          onChange={setAlliance}
        />
      </div>

      <div>
        <span className="eyebrow d-block mb-2">By Discord DM</span>
        <div className="d-grid gtc-1 gtc-md-4 gap-2">
          <NoticeToggle
            label="Security"
            detail={account.discordConnected ? 'Password, Discord, sessions, and account access.' : 'Connect Discord before turning this on.'}
            checked={discordSecurity}
            disabled={!account.discordConnected}
            onChange={setDiscordSecurity}
          />
          <NoticeToggle
            label="Combat"
            detail={account.discordConnected ? 'Raids on your house, and ground won or lost.' : 'Connect Discord before turning this on.'}
            checked={discordCombat}
            disabled={!account.discordConnected}
            onChange={setDiscordCombat}
          />
          <NoticeToggle
            label="Crew"
            detail={account.discordConnected ? 'Allies calling for help or crew business that needs eyes.' : 'Connect Discord before turning this on.'}
            checked={discordCrew}
            disabled={!account.discordConnected}
            onChange={setDiscordCrew}
          />
          <NoticeToggle
            label="Market"
            detail={account.discordConnected ? 'Somebody buying what you put up for sale.' : 'Connect Discord before turning this on.'}
            checked={discordMarket}
            disabled={!account.discordConnected}
            onChange={setDiscordMarket}
          />
          {/*
            The bell shows these to everybody, because a panel nobody asked for costs nothing to
            ignore. A DM is not that - it arrives wherever you are - so your own machinery gets a
            switch here that it does not need there.
          */}
          <NoticeToggle
            label="Your own machinery"
            detail={account.discordConnected ? 'Labs, builds, mules and ground finishing while you were somewhere else.' : 'Connect Discord before turning this on.'}
            checked={discordMachine}
            disabled={!account.discordConnected}
            onChange={setDiscordMachine}
          />
        </div>
      </div>

      {/*
        A different channel, not a duplicate of the three above. Somebody who wants no mail at all still
        wants the bell, and somebody who wants mail about a raid does not necessarily want it about a
        sale - so these are their own columns rather than one set of switches governing both.

        Turning one off takes it out of the unread count as well as the list: a badge over something you
        asked not to be told about is the notification you switched off.
      */}
      <div>
        <span className="eyebrow d-block mb-2">In the game, on the bell</span>
        <div className="d-grid gtc-1 gtc-md-3 gap-2">
          <NoticeToggle
            label="Combat"
            detail="Raids on your house, and ground won or lost."
            checked={bellCombat}
            onChange={setBellCombat}
          />
          <NoticeToggle
            label="Crew"
            detail="Allies calling for help while they are being raided."
            checked={bellCrew}
            onChange={setBellCrew}
          />
          <NoticeToggle
            label="Market"
            detail="Somebody buying what you put up for sale."
            checked={bellMarket}
            onChange={setBellMarket}
          />
        </div>
        <small className="form-text d-block mt-2">
          Your labs, builds and mule runs always ring. They are your own machinery reporting in, and
          there is nowhere else they are said.
        </small>
      </div>

      <Button className="btn btn-primary" blocked={firstReason(
        busy && BUSY,
        !changed && 'Nothing here has been changed.',
      )}>{busy ? 'Working...' : 'Save Alerts'}</Button>
    </form>
  </section>
}

function NoticeToggle({ label, detail, checked, disabled, onChange }: {
  label: string
  detail: string
  checked: boolean
  disabled?: boolean
  onChange: (value: boolean) => void
}) {
  return <label className={`form-check form-switch border rounded bg-body-secondary p-3 ps-5 ${checked ? 'border-primary' : ''}`}>
    <input
      className="form-check-input"
      type="checkbox"
      checked={checked}
      disabled={disabled}
      onChange={event => onChange(event.target.checked)}
    />
    <strong className="d-block">{label}</strong>
    <small className="form-text">{detail}</small>
  </label>
}

/**
 * Where you are signed in, and the ability to end one of them.
 *
 * The list is loaded here rather than arriving with the account, because it is the one thing on this
 * page that changes without anybody touching it - a session moves every few minutes as somebody plays -
 * and folding it into the account payload would make every other panel refetch it for nothing.
 */
function SessionsCard({ account, busy, run }: { account: Account, busy: boolean, run: AccountPanel['run'] }) {
  const [sessions, setSessions] = useState<PlayerSession[] | null>(null)
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    try { setSessions(await api.sessions()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  // Only an account with a password can be asked for one. A Discord-only account has nothing to prove
  // with and is already proving itself with the cookie, which is the same exemption the password form
  // makes rather than a hole opened here.
  const needsPassword = account.hasPassword

  const revokeOne = async (session: PlayerSession) => {
    setError('')
    try {
      await api.revokeSession(session.id, password)
      setPassword('')
      await load()
    } catch (e) { setError((e as Error).message) }
  }

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Sessions</h2>
      <span>{sessions === null ? 'Reading' : `${sessions.length} signed in`}</span>
    </div>
    <p>
      A sign-in lasts a fortnight and renews itself while you play, which is convenient right up until
      you leave yourself signed in on a machine you no longer have.
    </p>

    {needsPassword && <label className="field mb-3">
      Current password
      <input
        className="form-control"
        type="password"
        autoComplete="current-password"
        value={password}
        onChange={event => setPassword(event.target.value)}
      />
      <small className="form-text">
        Ending a session is how somebody who has taken one would lock you out of your own account, so it
        costs the password - which a stolen cookie does not carry.
      </small>
    </label>}

    {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}

    {sessions !== null && <div className="d-grid gap-2 mb-3">
      {sessions.length === 0 && <p className="text-body-tertiary small mb-0">
        Nothing recorded yet. Sessions from before this was added are not listed - they still work, and
        signing out everywhere ends them.
      </p>}
      {sessions.map(session => <div
        key={session.id}
        className={`session-row border rounded p-2 d-grid gap-2 align-items-center ${session.isCurrent ? 'border-primary' : 'bg-body-secondary'}`}
      >
        <div className="min-w-0">
          <strong className="d-block text-truncate">
            {session.isCurrent ? 'This device' : session.ipAddress ?? 'Unknown address'}
          </strong>
          <small className="session-user-agent d-block text-body-tertiary">{session.userAgent ?? 'Unknown browser'}</small>
          <small className="d-block text-body-tertiary">
            Last seen {new Date(session.lastSeenAtUtc).toLocaleString()}
            {session.isCurrent ? '' : ` / signed in ${new Date(session.createdAtUtc).toLocaleDateString()}`}
          </small>
        </div>
        <Button
          className="btn btn-outline-danger btn-sm"
          type="button"
          blocked={busy && BUSY}
          onClick={() => void revokeOne(session)}
        >{session.isCurrent ? 'Sign out here' : 'End it'}</Button>
      </div>)}
    </div>}

    <Button
      className="btn btn-outline-danger"
      type="button"
      blocked={busy && BUSY}
      onClick={() => void run(
        async () => { const a = await api.revokeSessions(password); setPassword(''); await load(); return a },
        'Every other session has been signed out.')}
    >{busy ? 'Working...' : 'Sign out everywhere else'}</Button>
  </section>
}

/**
 * Ten single-use ways back in, shown once.
 *
 * Once is not a limitation to work around - it is the reason these are safe to have. What the server
 * keeps is a hash, exactly as it does for a password, so there is no endpoint that could say them again
 * and no column that hands somebody with database access a way into every account in the game.
 */
function RecoveryCodesCard({ account, busy }: { account: Account, busy: boolean }) {
  const [remaining, setRemaining] = useState<number | null>(null)
  const [password, setPassword] = useState('')
  const [codes, setCodes] = useState<string[] | null>(null)
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async () => {
    try { setRemaining((await api.recoveryCodesLeft()).remaining) } catch { /* the count is not the point */ }
  }
  useEffect(() => { void load() }, [])

  const issue = async () => {
    setWorking(true); setError('')
    try {
      setCodes((await api.issueRecoveryCodes(password)).codes)
      setPassword('')
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  return <section className="card p-3">
    <div className="panel-title">
      <h2>Recovery codes</h2>
      <span>{remaining === null ? 'Reading' : remaining === 0 ? 'None made' : `${remaining} left`}</span>
    </div>
    <p>
      Ten one-time codes. Any of them gets you back in without an email and without Discord, which is the
      case neither of the other two doors can answer - a lost mailbox, or a Discord account you no longer
      have. Each one works once.
    </p>
    <p className="text-body-tertiary small">
      They do not replace your email or your Discord: you still cannot remove your last way back in. A
      sheet of paper is the thing most easily lost, so it is a spare set of keys rather than the door.
    </p>

    {codes
      ? <>
        <div className="alert alert-warning">
          Written down now or not at all. They are stored hashed, exactly as your password is, so this is
          the only time they can be shown.
        </div>
        <pre className="border rounded bg-body-tertiary p-3 mb-3 tnum">{codes.join('\n')}</pre>
        <button
          className="btn btn-secondary"
          type="button"
          onClick={() => void navigator.clipboard?.writeText(codes.join('\n'))}
        >Copy them</button>
        <button className="btn btn-link text-body-secondary" type="button" onClick={() => setCodes(null)}>
          I have written them down
        </button>
      </>
      : <div className="d-grid gap-3">
        {account.hasPassword && <label className="field">
          Current password
          <input
            className="form-control"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
          />
        </label>}
        {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
        <div>
          <Button
            className="btn btn-outline-primary"
            type="button"
            blocked={firstReason(
              busy && BUSY,
              working && 'Your codes are being made now.',
              account.hasPassword && password.length === 0 && 'Type your password above first.',
            )}
            onClick={() => void issue()}
          >{working ? 'Working...' : remaining ? 'Make a new set' : 'Make my codes'}</Button>
        </div>
        {remaining !== null && remaining > 0 && <small className="text-body-tertiary">
          Making a new set voids the old one, so any sheet you already have stops working.
        </small>}
      </div>}
  </section>
}

function AccountSecurityPanel({ account, busy, run, onTab }: AccountPanel & { onTab: (tab: AccountTab) => void }) {
  const open = waysIn(account)
  const back = waysBackIn(account)
  const enoughOfBoth = open.length > 1 && back.length > 1
  return <>
    <SessionsCard account={account} busy={busy} run={run} />
    <RecoveryCodesCard account={account} busy={busy} />

    {/*
      Two counters rather than one, because the panel used to answer one question and imply the other.
      It said "you can close either one and still get back in", which stopped being true the day a way
      *in* and a way *back in* came apart - closing Discord with no confirmed address leaves a player
      signed in and unrecoverable, which is exactly the state the counts exist to show.
    */}
    <section className="card p-3">
      <div className="panel-title">
        <h2>The Last Door</h2><span>{open.length} in / {back.length} back</span>
      </div>
      <p>
        Two different questions, and the pair above answers both. <strong className="text-primary">In</strong> is
        what signs you in: a password, or a connected Discord. <strong className="text-primary">Back</strong> is
        what could still prove the account was yours once the password is gone: a confirmed email
        address, or that same Discord.
      </p>
      <p>
        A password answers the first and never the second - forget it and it proves nothing - which is
        why the two are rarely the same number.
      </p>
      <p className={enoughOfBoth ? 'mb-0' : ''}>
        {enoughOfBoth
          ? 'You have a spare of each, so nothing here is load-bearing. Close any one of them and you can still get in, and still get back.'
          : 'The game refuses to let you close a last one of either. That is a poor substitute for having a spare of each.'}
      </p>
      {!enoughOfBoth && <button className="btn btn-primary" type="button" onClick={() => onTab('signin')}>
        Add another
      </button>}
    </section>
  </>
}

