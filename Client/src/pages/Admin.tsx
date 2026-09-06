import React, { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { adminApi, api, configApi, opsApi } from '../api'
import type { AdminAuditEntry, AdminBetaKey, AdminBotHealth, AdminConfig, AdminConfigEntry,
  AdminCustomTitle, AdminCustomTitleDraft, AdminGameAnnouncement, AdminGameAnnouncementDraft,
  AdminOversight, AdminOverview, AdminPlayerDetail, AdminPlayerSummary, AnnouncementDeliverySettings,
  BotDirective, CustomTitleCriteria, DiscordCrewChannelSyncResult, DiscordIntegrationSettings,
  DiscordRoleSyncResult, LiveOps, PlayerTarget, ActionResult, GameAnnouncement } from '../api'
import { compactDateTime, money, number } from '../format'
import { ActivityList, AdminMetric, betaKeyStatusClass, BUSY, Button, copyToClipboard,
  DismissibleMessage, firstReason, percent, StatusRow, updateCategories, updateCategoryClass,
  updateSeverities, updateSeverityClass, useRouteTab, WORKING, type Blocked } from '../ui'
import type { PageContext } from '../pagecontext'

/*
  The admin desk.

  The clearest thing in the app to load on demand: it is well over a thousand lines, it is gated on a
  flag almost nobody has, and until now every player downloaded all of it in order to never open it.
*/
// unlike the old self-only cheats these work on anybody and leave a record with a reason.
const adjustPresets: { label: string, resource: string, delta: number }[] = [
  { label: '+$10k cash', resource: 'cash', delta: 10_000 },
  { label: '+$10k bank', resource: 'bank', delta: 10_000 },
  { label: '+50 turns', resource: 'turns', delta: 50 },
  { label: '+5 pimps', resource: 'pimps', delta: 5 },
  { label: '+25 hoes', resource: 'hoes', delta: 25 },
  { label: '+10 thugs', resource: 'thugs', delta: 10 },
  { label: '+100 condoms', resource: 'condoms', delta: 100 },
  { label: '+100 beer', resource: 'beer', delta: 100 },
  // Pistols rather than "weapons": the adjust endpoint takes a tier, and there has been no such column
  // since guns split into four. The button answered 400 to every press.
  { label: '+10 pistols', resource: 'pistols', delta: 10 },
  { label: '+250 weed', resource: 'weed', delta: 250 },
  { label: '+100 coke', resource: 'coke', delta: 100 },
]

function botPresence(bot: AdminBotHealth) {
  if (bot.isInSession) return `playing, ${number.format(bot.sessionActionsLeft)} left`
  if (!bot.nextSessionAtUtc) return 'due to play'
  const minutes = Math.round((new Date(bot.nextSessionAtUtc).getTime() - Date.now()) / 60000)
  if (minutes <= 0) return 'due to play'
  return minutes < 90 ? `back in ${minutes}m` : `back in ${Math.round(minutes / 60)}h`
}

// Only a rival that is meant to be playing and is not counts as stale, so a sleeper is not flagged.
function rivalRowClass(bot: AdminBotHealth) {
  if (bot.isPaused) return 'paused'
  return bot.isInSession && bot.minutesIdle > 30 ? 'stale' : ''
}


const ADMIN_TABS = ['overview', 'players', 'keys', 'ai', 'config', 'titles', 'updates', 'liveops', 'audit'] as const
type AdminTab = typeof ADMIN_TABS[number]

const ADMIN_TAB_META: Record<AdminTab, { label: string, kicker: string }> = {
  overview: { label: 'Overview', kicker: 'Totals and distribution' },
  players: { label: 'Players', kicker: 'Search and enforcement' },
  keys: { label: 'Keys', kicker: 'Mint and revoke' },
  ai: { label: 'AI Rivals', kicker: 'Seed, run, automate' },
  config: { label: 'Tuning', kicker: 'Runtime values' },
  titles: { label: 'Titles', kicker: 'Create earned names' },
  updates: { label: 'Updates', kicker: 'Patch notes and events' },
  liveops: { label: 'Live Ops', kicker: 'Maintenance and banners' },
  audit: { label: 'Audit', kicker: 'Who changed what' }
}

/**
 * One tab at a time rather than six stacked panels. The Admin Control Center used to sit at the bottom
 * holding whatever had no other home: headline totals, a read-only economy dump, and the AI controls.
 * Those are three different jobs, so they now live with the things they belong to.
 */
export function AdminPage(ctx: PageContext & { overview: AdminOverview }) {
  const [tab, setTab] = useRouteTab('admin', ADMIN_TABS, 'overview')
  /*
    One column, said once.

    This read `gtc-1 gtc-md-2 ... gtc-md-1` - somebody wanting a single column and appending gtc-md-1
    to force it. Utilities are generated from a map in value order, so .gtc-md-2 is written to the
    stylesheet after .gtc-md-1; both carry !important and the same specificity, so the later one wins
    whatever order the class attribute lists them in. The override never did anything, on any of the
    five elements that had it.

    Here it showed: the tab strip and every panel under it sat in the first of two columns, 619px of a
    1278px page, with the second column empty and the six tabs folded into three columns of two rows
    with their descriptions wrapping.
  */
  return <div className="d-grid gtc-1 gap-3 align-items-start">
    <nav className="d-grid gtc-fill-150 gap-1 border rounded p-1">
      {ADMIN_TABS.map(name => <button
        key={name}
        type="button"
        className={`admin-tab btn d-grid gap-1 text-start px-3 py-2 ${tab === name ? 'active' : ''}`}
        aria-current={tab === name ? 'page' : undefined}
        onClick={() => setTab(name)}
      >
        <strong>{ADMIN_TAB_META[name].label}</strong>
        {/* Inherits the button's colour so it stays legible once the tab fills in. */}
        <span className="small opacity-75">{ADMIN_TAB_META[name].kicker}</span>
      </button>)}
    </nav>
    {tab === 'overview' && <AdminOverviewTab overview={ctx.overview} busy={ctx.busy} />}
    {tab === 'players' && <AdminPlayersPanel busy={ctx.busy} onChanged={() => void ctx.act(async () => undefined)} />}
    {tab === 'keys' && <AdminKeysPanel busy={ctx.busy} />}
    {tab === 'ai' && <AdminAiTab ctx={ctx} />}
    {tab === 'config' && <><AdminConfigPanel busy={ctx.busy} /><AdminEconomyReadout overview={ctx.overview} /></>}
    {tab === 'titles' && <AdminTitlesPanel busy={ctx.busy} />}
    {tab === 'updates' && <AdminUpdatesPanel busy={ctx.busy} />}
    {tab === 'liveops' && <AdminLiveOpsPanel busy={ctx.busy} />}
    {tab === 'audit' && <AdminAuditPanel />}
  </div>
}

function AdminOverviewTab({ overview, busy }: { overview: AdminOverview, busy: boolean }) {
  return <>
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The World</h2><span>As of {new Date(overview.generatedAtUtc).toLocaleTimeString()}</span></div>
      <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
        <AdminMetric label="Accounts" value={number.format(overview.totalAccounts)} />
        <AdminMetric label="Admins" value={number.format(overview.adminAccounts)} />
        <AdminMetric label="AI rivals" value={number.format(overview.botAccounts)} />
        <AdminMetric label="AI auto" value={overview.botAutomation.enabled ? 'On' : 'Off'} />
        <AdminMetric label="Players" value={number.format(overview.totalPlayers)} />
        <AdminMetric label="Liquid cash" value={money.format(overview.totalLiquidCash)} />
        <AdminMetric label="Net worth" value={money.format(overview.totalNetWorth)} />
        <AdminMetric label="Turns banked" value={number.format(overview.totalTurnsBanked)} />
        <AdminMetric label="Avg hoe morale" value={`${overview.averageHoeMorale.toFixed(0)}%`} />
        <AdminMetric label="Avg thug morale" value={`${overview.averageThugMorale.toFixed(0)}%`} />
      </div>
    </section>
    <AdminOversightPanel busy={busy} />
  </>
}

function AdminEconomyReadout({ overview }: { overview: AdminOverview }) {
  const game = overview.economy
  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>In Effect Now</h2><span>Read-only summary</span></div>
    <div className="mt-3 border-top">
      <StatusRow label="Turns" value={`+${game.turnsPerTick} / ${game.turnTickMinutes}m, cap ${game.maxTurns}`} />
      <StatusRow label="Action limit" value={`${game.maxActionTurns} turns`} />
      <StatusRow label="Store prices" value={`Condom ${money.format(game.condomPrice)}, beer ${money.format(game.beerPrice)}, weapon ${money.format(game.weaponPrice)}`} />
      <StatusRow label="Product prices" value={`Weed ${money.format(game.weedSellPrice)}, coke ${money.format(game.cokeSellPrice)}`} />
      <StatusRow label="Crew hire costs" value={`P ${money.format(game.crew.hirePimpCost)} / H ${money.format(game.crew.hireHoeCost)} / T ${money.format(game.crew.hireThugCost)}`} />
      <StatusRow label="Recruit odds" value={`P ${percent(game.streetAction.pimpRecruitChance)} / H ${percent(game.streetAction.hoeRecruitChance)} / T ${percent(game.streetAction.thugRecruitChance)}`} />
      <StatusRow label="Production" value={`Weed ${money.format(game.production.weed.costPerTurn)} ${game.production.weed.unitsMin}-${game.production.weed.unitsMax}, coke ${money.format(game.production.coke.costPerTurn)} ${game.production.coke.unitsMin}-${game.production.coke.unitsMax}`} />
      <StatusRow label="Morale rules" value={`${game.morale.hoesManagedPerPimp} hoes/pimp, desertion below ${game.morale.desertionThreshold}%`} />
      <StatusRow label="Combat" value={`${game.combat.attackTurnCost} turns, ${game.combat.attackTravelSecondsMin}-${game.combat.attackTravelSecondsMax}s travel, ${game.combat.attackCooldownMinutes}m cooldown`} />
    </div>
  </section>
}

function AdminKeysPanel({ busy }: { busy: boolean }) {
  const [keys, setKeys] = useState<AdminBetaKey[]>([])
  const [total, setTotal] = useState(0)
  const [query, setQuery] = useState('')
  const [label, setLabel] = useState('')
  const [count, setCount] = useState(10)
  const [maxUses, setMaxUses] = useState(1)
  const [reason, setReason] = useState('')
  const [minted, setMinted] = useState<AdminBetaKey[]>([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async (nextQuery = query) => {
    try {
      const board = await adminApi.betaKeys(nextQuery.trim())
      setKeys(board.keys)
      setTotal(board.total)
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load('') }, [])

  const mint = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const created = await adminApi.mintBetaKeys({
        count,
        label: label.trim() || null,
        maxUses,
        reason: reason.trim() || null,
      })
      setMinted(created.keys)
      setMessage(`Minted ${number.format(created.keys.length)} beta key${created.keys.length === 1 ? '' : 's'}.`)
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const revoke = async (key: AdminBetaKey) => {
    if (!window.confirm(`Revoke ${key.displayCode}?`)) return
    setWorking(true); setError(''); setMessage('')
    try {
      const updated = await adminApi.revokeBetaKey(key.id, reason.trim() || undefined)
      setKeys(current => current.map(item => item.id === updated.id ? updated : item))
      setMessage(`${updated.displayCode} revoked.`)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const copy = async (value: string, said: string) => {
    try {
      await copyToClipboard(value)
      setMessage(said)
    } catch { setError('Could not copy to the clipboard.') }
  }

  const mintedBlock = minted.map(key => key.displayCode).join('\n')

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Beta Keys</h2><span>{total > keys.length ? `${keys.length} of ${total}` : `${keys.length}`}</span></div>
    {(error || message) && <div className="d-grid gap-2 mb-3">
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
    </div>}

    <div className="d-grid gtc-1 gtc-xl-2 gap-3 align-items-start">
      <form className="d-grid gap-3 border rounded bg-body-secondary p-3" onSubmit={mint}>
        <div className="panel-title mb-0"><h3 className="h5 mb-0">Mint</h3><span>Admin pool</span></div>
        <div className="d-grid gtc-1 gtc-md-3 gap-3">
          <label className="field">
            Count
            <input
              className="form-control"
              type="number"
              min={1}
              max={500}
              value={count}
              onChange={event => setCount(Math.max(1, Math.min(500, Number(event.target.value) || 1)))}
            />
          </label>
          <label className="field">
            Uses
            <input
              className="form-control"
              type="number"
              min={1}
              max={1000}
              value={maxUses}
              onChange={event => setMaxUses(Math.max(1, Math.min(1000, Number(event.target.value) || 1)))}
            />
          </label>
        </div>
        <label className="field">
          Label
          <input
            className="form-control"
            maxLength={120}
            value={label}
            placeholder="Optional batch label"
            onChange={event => setLabel(event.target.value)}
          />
        </label>
        <label className="field">
          Audit reason
          <input
            className="form-control"
            value={reason}
            placeholder="Optional"
            onChange={event => setReason(event.target.value)}
          />
        </label>
        <Button className="btn btn-primary" blocked={firstReason(busy && WORKING, working && 'The keys are being minted now.')}>
          {working ? 'Working...' : 'Mint Keys'}
        </Button>
      </form>

      <div className="d-grid gap-3">
        <form className="d-flex flex-wrap gap-2" onSubmit={event => { event.preventDefault(); void load(query) }}>
          <input
            className="form-control flex-fill"
            value={query}
            placeholder="Search code, label, player, username"
            onChange={event => setQuery(event.target.value)}
          />
          <Button className="btn btn-secondary" type="submit" blocked={working && WORKING}>Search</Button>
          <button className="btn btn-link text-body-secondary" type="button" onClick={() => { setQuery(''); void load('') }}>
            Clear
          </button>
        </form>
        {minted.length > 0 && <div className="border rounded bg-body-secondary p-3">
          <div className="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
            <strong>Fresh keys</strong>
            <button
              className="btn btn-outline-primary btn-sm"
              type="button"
              onClick={() => void copy(mintedBlock, 'Fresh keys copied.')}
            >Copy Block</button>
          </div>
          <pre className="tnum mb-0 small">{mintedBlock}</pre>
        </div>}
      </div>
    </div>

    <div className="table-responsive mt-3">
      <table className="table table-sm align-middle mb-0">
        <thead>
          <tr>
            <th>Key</th>
            <th>Status</th>
            <th>Uses</th>
            <th>Chain</th>
            <th>Dates</th>
            <th className="text-end">Actions</th>
          </tr>
        </thead>
        <tbody>
          {keys.length === 0 && <tr><td colSpan={6} className="text-body-tertiary">No beta keys found.</td></tr>}
          {keys.map(key => {
            const issuedTo = key.issuedToPlayerName ?? key.issuedToUsername ?? 'Admin pool'
            const redeemedBy = key.redeemedByPlayerName ?? key.redeemedByUsername ?? 'Not redeemed'
            return <tr key={key.id}>
              <td className="tnum">
                <strong>{key.displayCode}</strong>
                {key.label && <small className="d-block text-body-tertiary text-truncate">{key.label}</small>}
              </td>
              <td><span className={`badge ${betaKeyStatusClass(key.status)}`}>{key.status}</span></td>
              <td className="tnum">{key.uses} / {key.maxUses}<small className="d-block text-body-tertiary">{key.usesLeft} left</small></td>
              <td className="small">
                <strong>{issuedTo}</strong>
                <span className="d-block text-body-tertiary">to {redeemedBy}</span>
              </td>
              <td className="small">
                <span className="d-block">Made {compactDateTime(key.createdAtUtc)}</span>
                <span className="d-block text-body-tertiary">Redeemed {compactDateTime(key.redeemedAtUtc)}</span>
              </td>
              <td className="text-end">
                <div className="btn-group btn-group-sm">
                  <button className="btn btn-outline-secondary" type="button" onClick={() => void copy(key.displayCode, 'Key copied.')}>
                    Copy
                  </button>
                  <Button className="btn btn-outline-danger" type="button" blocked={firstReason(
                    working && WORKING,
                    key.status === 'Revoked' && 'This key is already revoked.',
                  )} onClick={() => void revoke(key)}>
                    Revoke
                  </Button>
                </div>
              </td>
            </tr>
          })}
        </tbody>
      </table>
    </div>
  </section>
}

function AdminTitlesPanel({ busy }: { busy: boolean }) {
  const [titles, setTitles] = useState<AdminCustomTitle[]>([])
  const [criteria, setCriteria] = useState<CustomTitleCriteria[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [draft, setDraft] = useState<AdminCustomTitleDraft>(() => emptyCustomTitleDraft())
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [working, setWorking] = useState(false)
  const selected = titles.find(title => title.id === selectedId) ?? null
  const selectedCriteria = criteria.find(x => x.key === draft.criteria) ?? criteria[0]

  const load = async () => {
    try {
      const board = await opsApi.customTitles()
      setTitles(board.titles)
      setCriteria(board.criteria)
      setDraft(current => current.criteria ? current : { ...current, criteria: board.criteria[0]?.key ?? 'net-worth-at-least' })
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const edit = (title: AdminCustomTitle) => {
    setSelectedId(title.id)
    setDraft({
      key: title.key,
      title: title.title,
      detail: title.detail,
      criteria: title.criteria,
      threshold: title.threshold,
      textValue: title.textValue ?? '',
      isActive: title.isActive,
      reason: '',
    })
    setMessage('')
    setError('')
  }

  const reset = () => {
    setSelectedId(null)
    setDraft(emptyCustomTitleDraft(criteria[0]?.key))
    setMessage('')
    setError('')
  }

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const body = {
        ...draft,
        key: draft.key?.trim() || null,
        title: draft.title?.trim() || null,
        detail: draft.detail?.trim() || null,
        textValue: draft.textValue?.trim() || null,
        threshold: selectedCriteria?.needsThreshold ? Number(draft.threshold ?? 0) : 0,
        reason: draft.reason?.trim() || null,
      }
      const saved = selected
        ? await opsApi.updateCustomTitle(selected.id, body)
        : await opsApi.createCustomTitle(body)
      setSelectedId(saved.id)
      setDraft({
        key: saved.key,
        title: saved.title,
        detail: saved.detail,
        criteria: saved.criteria,
        threshold: saved.threshold,
        textValue: saved.textValue ?? '',
        isActive: saved.isActive,
        reason: '',
      })
      setMessage(selected ? 'Title saved.' : 'Title created.')
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const locked = busy || working
  return <div className="d-grid gtc-1 gtc-xl-split-60 gap-3 align-items-start gcol-full">
    <section className="card p-3">
      <div className="panel-title"><h2>Custom Titles</h2><span>{titles.length} defined</span></div>
      {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
      {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
      <div className="d-flex flex-wrap gap-2 mb-3">
        <button className="btn btn-primary btn-sm" type="button" onClick={reset}>New title</button>
        <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void load()}>Refresh</Button>
      </div>
      <div className="d-grid gap-1">
        {titles.length === 0 && <p className="text-body-tertiary small mb-0">No custom titles yet.</p>}
        {titles.map(title => <button
          className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${selectedId === title.id ? 'active border-primary' : ''}`}
          type="button"
          key={title.id}
          onClick={() => edit(title)}
        >
          <span className="d-flex flex-wrap gap-2 align-items-center min-w-0">
            <strong className="text-truncate">{title.title}</strong>
            <span className="badge rounded-pill text-bg-secondary">{title.key}</span>
            <span className={`badge rounded-pill ${title.isActive ? 'text-bg-success' : 'text-bg-light border'}`}>{title.isActive ? 'Active' : 'Paused'}</span>
          </span>
          <small className="text-body-tertiary text-truncate">{title.criteria}{title.threshold > 0 ? ` ${number.format(title.threshold)}` : ''}{title.textValue ? ` ${title.textValue}` : ''}</small>
        </button>)}
      </div>
    </section>

    <section className="card p-3">
      <div className="panel-title"><h2>{selected ? 'Edit Title' : 'New Title'}</h2><span>{draft.key || 'achievement'}</span></div>
      <form className="d-grid gap-3" onSubmit={save}>
        <div className="d-grid gtc-1 gtc-md-2 gap-3">
          <label className="field">
            Key
            <input className="form-control" maxLength={32} value={draft.key ?? ''} onChange={event => setDraft({ ...draft, key: event.target.value })} placeholder="millionaire" required />
          </label>
          <label className="field">
            Title
            <input className="form-control" maxLength={64} value={draft.title ?? ''} onChange={event => setDraft({ ...draft, title: event.target.value })} placeholder="Millionaire" required />
          </label>
        </div>
        <label className="field">
          Detail
          <input className="form-control" maxLength={240} value={draft.detail ?? ''} onChange={event => setDraft({ ...draft, detail: event.target.value })} placeholder="Reached $1,000,000 net worth." />
        </label>
        <div className="d-grid gtc-1 gtc-md-2 gap-3">
          <label className="field">
            Earned by
            <select className="form-select" value={draft.criteria ?? criteria[0]?.key ?? ''} onChange={event => setDraft({ ...draft, criteria: event.target.value })}>
              {criteria.map(option => <option key={option.key} value={option.key}>{option.label}</option>)}
            </select>
          </label>
          {selectedCriteria?.needsThreshold
            ? <label className="field">
              Threshold
              <input className="form-control" type="number" min={1} step={1} value={draft.threshold ?? 0} onChange={event => setDraft({ ...draft, threshold: Number(event.target.value) })} />
            </label>
            : selectedCriteria?.needsText
            ? <label className="field">
              Name
              <input className="form-control" maxLength={64} value={draft.textValue ?? ''} onChange={event => setDraft({ ...draft, textValue: event.target.value })} placeholder={draft.criteria === 'city-is' ? 'Chicago' : 'The Eastside Table'} />
            </label>
            : <div className="d-flex align-items-end"><small className="text-body-tertiary">No extra value needed.</small></div>}
        </div>
        <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
          <input className="form-check-input" type="checkbox" checked={draft.isActive ?? true} onChange={event => setDraft({ ...draft, isActive: event.target.checked })} />
          <span>Active</span>
        </label>
        <label className="field">
          Audit reason
          <input className="form-control" value={draft.reason ?? ''} onChange={event => setDraft({ ...draft, reason: event.target.value })} placeholder="Added a new milestone title" />
        </label>
        <div className="d-flex flex-wrap gap-2">
          <Button className="btn btn-primary" blocked={locked && WORKING}>{locked ? 'Working...' : selected ? 'Save Title' : 'Create Title'}</Button>
          {selected && <Button className="btn btn-secondary" type="button" blocked={locked && WORKING} onClick={reset}>Clear Form</Button>}
        </div>
      </form>
    </section>
  </div>
}

function AdminUpdatesPanel({ busy }: { busy: boolean }) {
  const [posts, setPosts] = useState<AdminGameAnnouncement[]>([])
  const [delivery, setDelivery] = useState<AnnouncementDeliverySettings | null>(null)
  const [discord, setDiscord] = useState<DiscordIntegrationSettings | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [draft, setDraft] = useState<AdminGameAnnouncementDraft>(() => emptyAnnouncementDraft())
  const [includeArchived, setIncludeArchived] = useState(false)
  const [discordWebhookUrl, setDiscordWebhookUrl] = useState('')
  const [discordUsername, setDiscordUsername] = useState('')
  const [discordBotToken, setDiscordBotToken] = useState('')
  const [discordPublicKey, setDiscordPublicKey] = useState('')
  const [discordApplicationId, setDiscordApplicationId] = useState('')
  const [discordGuildId, setDiscordGuildId] = useState('')
  const [discordLinkedRoleId, setDiscordLinkedRoleId] = useState('')
  const [discordTopTenRoleId, setDiscordTopTenRoleId] = useState('')
  const [discordCrewBossRoleId, setDiscordCrewBossRoleId] = useState('')
  const [discordCityRoleMap, setDiscordCityRoleMap] = useState('')
  const [discordCrewRoleMap, setDiscordCrewRoleMap] = useState('')
  const [discordCrewChannelMap, setDiscordCrewChannelMap] = useState('')
  const [discordTitleRoleMap, setDiscordTitleRoleMap] = useState('')
  const [discordConsole, setDiscordConsole] = useState<string[]>([])
  const [reason, setReason] = useState('')
  const [deliveryReason, setDeliveryReason] = useState('')
  const [discordReason, setDiscordReason] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [working, setWorking] = useState(false)
  const selected = posts.find(post => post.id === selectedId) ?? null
  const discordInviteUrl = discordBotInviteUrl(discord, discordApplicationId, discordGuildId)

  const load = async () => {
    try { setPosts(await opsApi.updates(includeArchived)) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [includeArchived])

  const loadDelivery = async () => {
    try {
      const next = await opsApi.updateDelivery()
      setDelivery(next)
      setDiscordUsername(next.discordUsername)
    } catch (e) {
      setError((e as Error).message)
    }
  }
  useEffect(() => { void loadDelivery() }, [])

  const applyDiscordSettings = (next: DiscordIntegrationSettings) => {
    setDiscord(next)
    setDiscordApplicationId(next.applicationId ?? '')
    setDiscordGuildId(next.guildId ?? '')
    setDiscordLinkedRoleId(next.linkedRoleId ?? '')
    setDiscordTopTenRoleId(next.topTenRoleId ?? '')
    setDiscordCrewBossRoleId(next.crewBossRoleId ?? '')
    setDiscordCityRoleMap(next.cityRoleMap ?? '')
    setDiscordCrewRoleMap(next.crewRoleMap ?? '')
    setDiscordCrewChannelMap(next.crewChannelMap ?? '')
    setDiscordTitleRoleMap(next.titleRoleMap ?? '')
  }

  const logDiscord = (line: string, issues: string[] = []) => {
    const stamp = new Date().toLocaleTimeString()
    setDiscordConsole(previous => [`${stamp} ${line}`, ...issues.map(issue => `${stamp} ! ${issue}`), ...previous].slice(0, 10))
  }

  const loadDiscord = async () => {
    try { applyDiscordSettings(await opsApi.discordIntegration()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void loadDiscord() }, [])

  const edit = (post: AdminGameAnnouncement) => {
    setSelectedId(post.id)
    setDraft(draftFromAnnouncement(post))
    setReason('')
    setMessage('')
    setError('')
  }

  const reset = () => {
    setSelectedId(null)
    setDraft(emptyAnnouncementDraft())
    setReason('')
    setMessage('')
    setError('')
  }

  const save = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const body = announcementPayload(draft, reason)
      const saved = selected
        ? await opsApi.updatePost(selected.id, body)
        : await opsApi.createUpdate(body)
      setSelectedId(saved.id)
      setDraft(draftFromAnnouncement(saved))
      setReason('')
      setMessage(saved.isDraft ? 'Draft saved.' : selected ? 'Update saved.' : 'Update published.')
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const archive = async (archived: boolean) => {
    if (!selected) return
    setWorking(true); setError(''); setMessage('')
    try {
      const saved = await opsApi.archiveUpdate(selected.id, archived, reason)
      setReason('')
      setMessage(archived ? 'Update archived.' : 'Update restored.')
      await load()
      setSelectedId(saved.id)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const saveDelivery = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setUpdateDelivery({
        discordWebhookUrl: discordWebhookUrl.trim() || null,
        discordUsername: discordUsername.trim() || null,
        reason: deliveryReason.trim() || null,
      })
      setDelivery(next)
      setDiscordWebhookUrl('')
      setDiscordUsername(next.discordUsername)
      setDeliveryReason('')
      setMessage('Discord announcement settings saved.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const clearDeliveryWebhook = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setUpdateDelivery({
        clearDiscordWebhook: true,
        discordUsername: discordUsername.trim() || null,
        reason: deliveryReason.trim() || null,
      })
      setDelivery(next)
      setDiscordWebhookUrl('')
      setDiscordUsername(next.discordUsername)
      setDeliveryReason('')
      setMessage('Saved webhook cleared.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const saveDiscord = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setDiscordIntegration({
        botToken: discordBotToken.trim() || null,
        publicKey: discordPublicKey.trim() || null,
        applicationId: discordApplicationId.trim() || null,
        guildId: discordGuildId.trim() || null,
        linkedRoleId: discordLinkedRoleId.trim() || null,
        topTenRoleId: discordTopTenRoleId.trim() || null,
        crewBossRoleId: discordCrewBossRoleId.trim() || null,
        cityRoleMap: discordCityRoleMap,
        crewRoleMap: discordCrewRoleMap,
        crewChannelMap: discordCrewChannelMap,
        titleRoleMap: discordTitleRoleMap,
        reason: discordReason.trim() || null,
      })
      applyDiscordSettings(next)
      setDiscordBotToken('')
      setDiscordPublicKey('')
      setDiscordReason('')
      logDiscord('Saved Discord bot settings.')
      setMessage('Discord integration settings saved.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const clearDiscordSecret = async (kind: 'token' | 'key') => {
    setWorking(true); setError(''); setMessage('')
    try {
      const next = await opsApi.setDiscordIntegration({
        clearBotToken: kind === 'token',
        clearPublicKey: kind === 'key',
        reason: discordReason.trim() || null,
      })
      applyDiscordSettings(next)
      if (kind === 'token') setDiscordBotToken('')
      if (kind === 'key') setDiscordPublicKey('')
      setDiscordReason('')
      logDiscord(kind === 'token' ? 'Cleared the saved bot token.' : 'Cleared the saved public key.')
      setMessage(kind === 'token' ? 'Discord bot token cleared.' : 'Discord public key cleared.')
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const registerDiscordCommands = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await opsApi.registerDiscordCommands()
      logDiscord(`Registered ${result.registered} slash command${result.registered === 1 ? '' : 's'}.`)
      setMessage(`Registered ${result.registered} slash command${result.registered === 1 ? '' : 's'} in Discord.`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const syncDiscordRoles = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result: DiscordRoleSyncResult = await opsApi.syncDiscordRoles()
      const tail = result.errors.length > 0 ? ` ${result.errors.length} issue${result.errors.length === 1 ? '' : 's'} reported.` : ''
      logDiscord(`Synced roles for ${result.syncedPlayers}/${result.linkedPlayers} linked members: +${result.rolesAdded} / -${result.rolesRemoved}.`, result.errors)
      setMessage(`Synced ${result.syncedPlayers} linked member${result.syncedPlayers === 1 ? '' : 's'}: +${result.rolesAdded} / -${result.rolesRemoved}.${tail}`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const ensureDiscordRoles = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await opsApi.ensureDiscordRoles()
      const tail = result.errors.length > 0 ? ` ${result.errors.length} role${result.errors.length === 1 ? '' : 's'} could not be created.` : ''
      logDiscord(`Role maps ready: ${result.cityRoles} city, ${result.crewRoles} crew, ${result.titleRoles} title. Created ${result.createdRoles}, reused ${result.reusedRoles}.`, result.errors)
      setMessage(`Role maps ready: ${result.cityRoles} city, ${result.crewRoles} crew, ${result.titleRoles} title. Created ${result.createdRoles}, reused ${result.reusedRoles}.${tail}`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const syncDiscordCrewChannels = async () => {
    setWorking(true); setError(''); setMessage('')
    try {
      const result: DiscordCrewChannelSyncResult = await opsApi.syncDiscordCrewChannels()
      const tail = result.errors.length > 0 ? ` ${result.errors.length} issue${result.errors.length === 1 ? '' : 's'} reported.` : ''
      logDiscord(`Crew channels synced: ${result.channels}/${result.crews} mapped. Created ${result.createdChannels}, reused ${result.reusedChannels}, updated ${result.updatedChannels}.`, result.errors)
      setMessage(`Crew channels synced: ${result.channels}/${result.crews} mapped. Created ${result.createdChannels}, reused ${result.reusedChannels}, updated ${result.updatedChannels}.${tail}`)
      await loadDiscord()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const locked = busy || working
  return <div className="d-grid gtc-1 gtc-xl-split-80 gap-3 align-items-start gcol-full">
    {/*
      Two columns, and the left one is a stack rather than two cells of the same grid.

      The list, the webhook and the editor were three children of a two-column grid, so they laid out
      row by row: the list beside the webhook, and the editor underneath on its own. With the rows
      sized to their tallest cell, that left the short list sitting at the top of a row as tall as the
      webhook form, and several hundred pixels of nothing under it before the editor began.

      The list and the editor belong together anyway - you pick an update in one and edit it in the
      other - and the webhook is a setting that happens to live on this page.
    */}
    <div className="d-grid gap-3 align-content-start">
      <section className="card p-3">
        <div className="panel-title">
          <h2>Updates</h2>
          <span>{posts.length} shown</span>
        </div>
        {error && <DismissibleMessage className="alert alert-danger" onClose={() => setError('')}>{error}</DismissibleMessage>}
        {message && <DismissibleMessage className="alert alert-success" onClose={() => setMessage('')}>{message}</DismissibleMessage>}
        <div className="d-flex flex-wrap gap-2 mb-3">
          <button className="btn btn-primary btn-sm" type="button" onClick={reset}>New update</button>
          <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
            <input className="form-check-input" type="checkbox" checked={includeArchived} onChange={event => setIncludeArchived(event.target.checked)} />
            <span className="small">Include archived</span>
          </label>
        </div>
        <div className="d-grid gap-1">
          {posts.length === 0 && <p className="text-body-tertiary small mb-0">No updates posted yet.</p>}
          {posts.map(post => <button
            className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${selectedId === post.id ? 'active border-primary' : ''}`}
            type="button"
            key={post.id}
            onClick={() => edit(post)}
          >
            <span className="d-flex flex-wrap gap-2 align-items-center min-w-0">
              <strong className="text-truncate">{post.title}</strong>
              <span className={`badge rounded-pill ${updateCategoryClass(post.category)}`}>{post.category}</span>
              <span className={`badge rounded-pill ${updateSeverityClass(post.severity)}`}>{post.severity}</span>
              {post.version && <span className="badge rounded-pill text-bg-secondary">{post.version}</span>}
              {post.isPinned && <span className="badge rounded-pill text-bg-primary">Pinned</span>}
              {post.showOnce && <span className="badge rounded-pill text-bg-warning">Login</span>}
              {post.isDraft && <span className="badge rounded-pill text-bg-light border">Draft</span>}
              {!post.isDraft && !post.archivedAtUtc && <span className="badge rounded-pill text-bg-success">Live</span>}
              {post.sendToDiscord && <span className={`badge rounded-pill ${post.discordSentAtUtc ? 'text-bg-info' : 'text-bg-light border'}`}>Discord</span>}
              {post.archivedAtUtc && <span className="badge rounded-pill text-bg-secondary">Archived</span>}
            </span>
            <small className="text-body-tertiary text-truncate">
              {post.isDraft ? 'Draft publish time ' : 'Published '}
              {new Date(post.publishedAtUtc).toLocaleString()}
            </small>
          </button>)}
        </div>
      </section>

      <section className="card p-3">
        <div className="panel-title"><h2>{selected ? 'Edit Update' : 'New Update'}</h2><span>{draft.version || draft.category}</span></div>
        <form className="d-grid gap-3" onSubmit={save}>
          <div className="d-grid gtc-1 gtc-md-2 gap-2">
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input
                className="form-check-input"
                type="checkbox"
                checked={!draft.isDraft}
                onChange={event => setDraft({ ...draft, isDraft: !event.target.checked })}
              />
              <span>{draft.isDraft ? 'Save as draft' : 'Publish to players'}</span>
            </label>
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input className="form-check-input" type="checkbox" checked={Boolean(draft.isPinned)} onChange={event => setDraft({ ...draft, isPinned: event.target.checked })} />
              <span>Pin in Street Wire</span>
            </label>
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input className="form-check-input" type="checkbox" checked={Boolean(draft.showOnce)} onChange={event => setDraft({ ...draft, showOnce: event.target.checked })} />
              <span>Show once on login</span>
            </label>
            <label className="form-check form-switch d-flex align-items-center gap-2 mb-0">
              <input className="form-check-input" type="checkbox" checked={Boolean(draft.sendToDiscord)} onChange={event => setDraft({ ...draft, sendToDiscord: event.target.checked })} />
              <span>Send to Discord</span>
            </label>
          </div>
          <label className="field">
            Title
            <input className="form-control" maxLength={96} value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} required />
          </label>
          <label className="field">
            Body
            <textarea className="form-control" rows={7} maxLength={4000} value={draft.body} onChange={event => setDraft({ ...draft, body: event.target.value })} required />
          </label>
          <div className="d-grid gtc-1 gtc-md-3 gap-3">
            <label className="field">
              Category
              <select className="form-select" value={draft.category} onChange={event => setDraft({ ...draft, category: event.target.value as GameAnnouncement['category'] })}>
                {updateCategories.map(category =>
                  <option key={category} value={category}>{category}</option>)}
              </select>
            </label>
            <label className="field">
              Severity
              <select className="form-select" value={draft.severity} onChange={event => setDraft({ ...draft, severity: event.target.value as GameAnnouncement['severity'] })}>
                {updateSeverities.map(severity =>
                  <option key={severity} value={severity}>{severity}</option>)}
              </select>
            </label>
            <label className="field">
              Version
              <input className="form-control" maxLength={32} value={draft.version ?? ''} onChange={event => setDraft({ ...draft, version: event.target.value })} placeholder={__APP_VERSION__} />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Starts at
              <input className="form-control" type="datetime-local" value={draft.publishedAtUtc ?? ''} onChange={event => setDraft({ ...draft, publishedAtUtc: event.target.value || null })} />
            </label>
            <label className="field">
              Ends at
              <input className="form-control" type="datetime-local" value={draft.expiresAtUtc ?? ''} onChange={event => setDraft({ ...draft, expiresAtUtc: event.target.value || null })} />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Added
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.added ?? ''} onChange={event => setDraft({ ...draft, added: event.target.value })} />
            </label>
            <label className="field">
              Changed
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.changed ?? ''} onChange={event => setDraft({ ...draft, changed: event.target.value })} />
            </label>
            <label className="field">
              Fixed
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.fixed ?? ''} onChange={event => setDraft({ ...draft, fixed: event.target.value })} />
            </label>
            <label className="field">
              Known issues
              <textarea className="form-control" rows={3} maxLength={2000} value={draft.knownIssues ?? ''} onChange={event => setDraft({ ...draft, knownIssues: event.target.value })} />
            </label>
          </div>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Action label
              <input className="form-control" maxLength={40} value={draft.actionLabel ?? ''} onChange={event => setDraft({ ...draft, actionLabel: event.target.value })} placeholder="Optional" />
            </label>
            <label className="field">
              Action URL
              <input className="form-control" maxLength={240} value={draft.actionUrl ?? ''} onChange={event => setDraft({ ...draft, actionUrl: event.target.value })} placeholder="/account" />
            </label>
          </div>
          <label className="field">
            Audit reason
            <input className="form-control" value={reason} onChange={event => setReason(event.target.value)} placeholder="Why this is being posted or changed" />
          </label>
          <div className="d-flex flex-wrap gap-2">
            <Button className="btn btn-primary" blocked={locked && WORKING}>
              {locked ? 'Working...' : draft.isDraft ? 'Save Draft' : selected ? 'Save and Publish' : 'Publish Update'}
            </Button>
            {selected && <Button className="btn btn-secondary" type="button" blocked={locked && WORKING} onClick={reset}>Clear Form</Button>}
            {selected && <Button
              className="btn btn-outline-danger"
              type="button"
              blocked={locked && WORKING}
              onClick={() => void archive(!selected.archivedAtUtc)}
            >{selected.archivedAtUtc ? 'Restore' : 'Archive'}</Button>}
          </div>
        </form>
      </section>
    </div>

    <div className="d-grid gap-3 align-content-start">
      <section className="card p-3">
        <div className="panel-title">
          <h2>Discord Webhook</h2>
          <span>{delivery?.discordConfigured ? delivery.discordUsesStoredWebhook ? 'Saved in admin' : 'From config' : 'Not set'}</span>
        </div>
        <form className="d-grid gap-3" onSubmit={saveDelivery}>
          <div className="d-flex flex-wrap gap-2">
            <span className={`badge rounded-pill ${delivery?.discordConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>
              {delivery?.discordConfigured ? 'Discord broadcast on' : 'Discord broadcast off'}
            </span>
            {delivery?.discordWebhookHost && <span className="badge rounded-pill text-bg-light border">{delivery.discordWebhookHost}</span>}
          </div>
          <label className="field">
            New webhook URL
            <input
              className="form-control"
              type="password"
              value={discordWebhookUrl}
              onChange={event => setDiscordWebhookUrl(event.target.value)}
              placeholder={delivery?.discordConfigured ? 'Paste a replacement webhook' : 'https://discord.com/api/webhooks/...'}
              autoComplete="off"
            />
            <small className="form-text">Saved URLs are not shown again. Leave blank to keep the current webhook.</small>
          </label>
          <label className="field">
            Webhook name
            <input className="form-control" maxLength={80} value={discordUsername} onChange={event => setDiscordUsername(event.target.value)} placeholder="Street Empire" />
          </label>
          <label className="field">
            Audit reason
            <input className="form-control" value={deliveryReason} onChange={event => setDeliveryReason(event.target.value)} placeholder="Moved announcements to #updates" />
          </label>
          <div className="d-flex flex-wrap gap-2">
            <Button className="btn btn-primary btn-sm" blocked={locked && WORKING}>{locked ? 'Working...' : 'Save Webhook Settings'}</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void loadDelivery()}>Refresh</Button>
            <Button className="btn btn-outline-danger btn-sm" type="button" blocked={firstReason(
              locked && WORKING,
              !delivery?.discordUsesStoredWebhook && 'There is no saved webhook to clear.',
            )} onClick={() => void clearDeliveryWebhook()}>
              Clear saved webhook
            </Button>
          </div>
          {delivery && <small className="text-body-tertiary">
            Last changed {new Date(delivery.updatedAtUtc).toLocaleString()}{delivery.updatedBy ? ` by ${delivery.updatedBy}` : ''}.
          </small>}
        </form>
      </section>

      <section className="card p-3">
        <div className="panel-title">
          <h2>Discord Bot</h2>
          <span>{discord?.gatewayConnected ? 'Online' : discord?.botConfigured ? 'Starting' : 'Needs setup'}</span>
        </div>
        <form className="d-grid gap-3" onSubmit={saveDiscord}>
          <div className="d-flex flex-wrap gap-2">
            <span className={`badge rounded-pill ${discord?.botConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.botConfigured ? 'Bot configured' : 'No bot'}</span>
            <span className={`badge rounded-pill ${discord?.gatewayConnected ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.gatewayConnected ? 'Gateway online' : 'Gateway offline'}</span>
            <span className={`badge rounded-pill ${discord?.slashCommandsConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.slashCommandsConfigured ? 'Slash ready' : 'Slash off'}</span>
            <span className={`badge rounded-pill ${discord?.roleSyncConfigured ? 'text-bg-success' : 'text-bg-secondary'}`}>{discord?.roleSyncConfigured ? 'Role sync ready' : 'Roles off'}</span>
            {discord?.usesStoredBotToken && <span className="badge rounded-pill text-bg-light border">Token saved</span>}
            {discord?.publicKeyConfigured && <span className="badge rounded-pill text-bg-light border">Public key saved</span>}
          </div>
          {discord?.gatewayError && <small className="text-body-tertiary">{discord.gatewayError}</small>}
          <label className="field">
            Interaction endpoint
            <input className="form-control" readOnly value={`${window.location.origin}/api/discord/interactions`} />
          </label>
          <div className="d-flex flex-wrap gap-2">
            {discordInviteUrl
              ? <a className="btn btn-outline-primary btn-sm" href={discordInviteUrl} target="_blank" rel="noreferrer">
                  Add bot to Discord
                </a>
              : <Button className="btn btn-outline-secondary btn-sm" type="button" blocked="Fill in the application ID and server ID below and save, and the invite link appears here.">Add bot to Discord</Button>}
          </div>
          <label className="field">
            Bot token
            <input className="form-control" type="password" value={discordBotToken} onChange={event => setDiscordBotToken(event.target.value)} placeholder={discord?.botConfigured ? 'Paste a replacement token' : 'Discord bot token'} autoComplete="off" />
            <small className="form-text">Saved tokens are not shown again. Leave blank to keep the current one.</small>
          </label>
          <div className="d-grid gtc-1 gtc-md-2 gap-3">
            <label className="field">
              Application ID
              <input className="form-control" value={discordApplicationId} onChange={event => setDiscordApplicationId(event.target.value)} placeholder="123456789012345678" />
            </label>
            <label className="field">
              Guild ID
              <input className="form-control" value={discordGuildId} onChange={event => setDiscordGuildId(event.target.value)} placeholder="123456789012345678" />
            </label>
          </div>
          <label className="field">
            Public key
            <input className="form-control" type="password" value={discordPublicKey} onChange={event => setDiscordPublicKey(event.target.value)} placeholder={discord?.publicKeyConfigured ? 'Paste a replacement public key' : '64-character application public key'} autoComplete="off" />
          </label>
          <div className="d-grid gtc-1 gtc-md-3 gap-3">
            <label className="field">
              Linked role
              <input className="form-control" value={discordLinkedRoleId} onChange={event => setDiscordLinkedRoleId(event.target.value)} placeholder="Role ID" />
            </label>
            <label className="field">
              Top ten role
              <input className="form-control" value={discordTopTenRoleId} onChange={event => setDiscordTopTenRoleId(event.target.value)} placeholder="Role ID" />
            </label>
            <label className="field">
              Crew boss role
              <input className="form-control" value={discordCrewBossRoleId} onChange={event => setDiscordCrewBossRoleId(event.target.value)} placeholder="Role ID" />
            </label>
          </div>
          <label className="field">
            City roles
            <textarea className="form-control" rows={5} value={discordCityRoleMap} onChange={event => setDiscordCityRoleMap(event.target.value)} placeholder={'Chicago=123456789012345678\nMiami=234567890123456789'} />
          </label>
          <label className="field">
            Crew roles
            <textarea className="form-control" rows={5} value={discordCrewRoleMap} onChange={event => setDiscordCrewRoleMap(event.target.value)} placeholder={'The Eastside Table=123456789012345678\nThe Southside Table=234567890123456789'} />
          </label>
          <label className="field">
            Crew channels
            <textarea className="form-control" rows={5} value={discordCrewChannelMap} onChange={event => setDiscordCrewChannelMap(event.target.value)} placeholder={'The Eastside Table=123456789012345678\nThe Southside Table=234567890123456789'} />
            <small className="form-text">Run crew channel sync to let the bot create and fill this map.</small>
          </label>
          <label className="field">
            Title roles
            <textarea className="form-control" rows={5} value={discordTitleRoleMap} onChange={event => setDiscordTitleRoleMap(event.target.value)} placeholder={'killer=123456789012345678\nwheelman=234567890123456789\ndiscord-connected=345678901234567890'} />
          </label>
          <label className="field">
            Audit reason
            <input className="form-control" value={discordReason} onChange={event => setDiscordReason(event.target.value)} placeholder="Added Discord role sync" />
          </label>
          <div className="d-flex flex-wrap gap-2">
            <Button className="btn btn-primary btn-sm" blocked={locked && WORKING}>{locked ? 'Working...' : 'Save Bot Settings'}</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void registerDiscordCommands()}>Register slash commands</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void ensureDiscordRoles()}>Create role maps</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void syncDiscordCrewChannels()}>Sync crew channels</Button>
            <Button className="btn btn-secondary btn-sm" type="button" blocked={locked && WORKING} onClick={() => void syncDiscordRoles()}>Sync roles now</Button>
            <Button className="btn btn-outline-danger btn-sm" type="button" blocked={firstReason(
              locked && WORKING,
              !discord?.usesStoredBotToken && 'There is no saved bot token to clear.',
            )} onClick={() => void clearDiscordSecret('token')}>Clear token</Button>
            <Button className="btn btn-outline-danger btn-sm" type="button" blocked={firstReason(
              locked && WORKING,
              !discord?.publicKeyConfigured && 'There is no saved public key to clear.',
            )} onClick={() => void clearDiscordSecret('key')}>Clear key</Button>
          </div>
          <div className="border rounded bg-body-secondary p-2 d-grid gap-1">
            <strong className="small">Discord Console</strong>
            {discordConsole.length === 0
              ? <small className="text-body-tertiary">No bot actions have run in this browser session.</small>
              : discordConsole.map((line, index) => <small className="font-monospace text-body-tertiary" key={`${line}-${index}`}>{line}</small>)}
          </div>
          {discord && <small className="text-body-tertiary">
            Commands {discord.commandsRegisteredAtUtc ? new Date(discord.commandsRegisteredAtUtc).toLocaleString() : 'not registered'}.
            {' '}Crew channels {discord.crewChannelsSyncedAtUtc ? new Date(discord.crewChannelsSyncedAtUtc).toLocaleString() : 'not synced'}.
            {' '}Roles {discord.rolesSyncedAtUtc ? new Date(discord.rolesSyncedAtUtc).toLocaleString() : 'not synced'}.
            {' '}Gateway {discord.gatewayHeartbeatAtUtc ? `heartbeat ${new Date(discord.gatewayHeartbeatAtUtc).toLocaleString()}` : 'no heartbeat yet'}.
          </small>}
        </form>
      </section>
    </div>
  </div>
}

function emptyCustomTitleDraft(criteria = 'net-worth-at-least'): AdminCustomTitleDraft {
  return {
    key: '',
    title: '',
    detail: '',
    criteria,
    threshold: 1,
    textValue: '',
    isActive: true,
    reason: '',
  }
}

function emptyAnnouncementDraft(): AdminGameAnnouncementDraft {
  return {
    title: '',
    body: '',
    category: 'Info',
    severity: 'Info',
    version: __APP_VERSION__,
    actionLabel: '',
    actionUrl: '',
    isDraft: true,
    isPinned: false,
    showOnce: false,
    sendToDiscord: false,
    publishedAtUtc: '',
    expiresAtUtc: '',
    added: '',
    changed: '',
    fixed: '',
    knownIssues: '',
  }
}

function discordBotInviteUrl(settings: DiscordIntegrationSettings | null, applicationId: string, guildId: string) {
  const clientId = (applicationId.trim() || settings?.applicationId || '').trim()
  if (!/^\d+$/.test(clientId)) return null

  const params = new URLSearchParams({
    client_id: clientId,
    scope: 'bot applications.commands',
    permissions: '268435472',
  })
  const guild = (guildId.trim() || settings?.guildId || '').trim()
  if (/^\d+$/.test(guild)) {
    params.set('guild_id', guild)
    params.set('disable_guild_select', 'true')
  }
  return `https://discord.com/oauth2/authorize?${params.toString()}`
}

function draftFromAnnouncement(post: AdminGameAnnouncement): AdminGameAnnouncementDraft {
  return {
    title: post.title,
    body: post.body,
    category: post.category,
    severity: post.severity,
    version: post.version ?? '',
    actionLabel: post.actionLabel ?? '',
    actionUrl: post.actionUrl ?? '',
    isDraft: post.isDraft,
    isPinned: post.isPinned,
    showOnce: post.showOnce,
    sendToDiscord: post.sendToDiscord,
    publishedAtUtc: toLocalDateTimeInput(post.publishedAtUtc),
    expiresAtUtc: post.expiresAtUtc ? toLocalDateTimeInput(post.expiresAtUtc) : '',
    added: post.added ?? '',
    changed: post.changed ?? '',
    fixed: post.fixed ?? '',
    knownIssues: post.knownIssues ?? '',
  }
}

function announcementPayload(draft: AdminGameAnnouncementDraft, reason: string): AdminGameAnnouncementDraft {
  return {
    title: draft.title.trim(),
    body: draft.body.trim(),
    category: draft.category,
    severity: draft.severity,
    version: draft.version?.trim() || null,
    actionLabel: draft.actionLabel?.trim() || null,
    actionUrl: draft.actionUrl?.trim() || null,
    isDraft: draft.isDraft ?? false,
    isPinned: draft.isPinned ?? false,
    showOnce: draft.showOnce ?? false,
    sendToDiscord: draft.sendToDiscord ?? false,
    publishedAtUtc: draft.publishedAtUtc ? new Date(draft.publishedAtUtc).toISOString() : null,
    expiresAtUtc: draft.expiresAtUtc ? new Date(draft.expiresAtUtc).toISOString() : null,
    added: draft.added?.trim() || null,
    changed: draft.changed?.trim() || null,
    fixed: draft.fixed?.trim() || null,
    knownIssues: draft.knownIssues?.trim() || null,
    reason: reason.trim() || null,
  }
}

function toLocalDateTimeInput(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}

function AdminLiveOpsPanel({ busy }: { busy: boolean }) {
  const [ops, setOps] = useState<LiveOps | null>(null)
  const [announcement, setAnnouncement] = useState('')
  const [maintenanceMessage, setMaintenanceMessage] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async () => {
    try {
      const next = await opsApi.liveOps()
      setOps(next)
      setAnnouncement(next.announcement ?? '')
      setMaintenanceMessage(next.maintenanceMessage ?? '')
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const apply = async (body: Parameters<typeof opsApi.setLiveOps>[0]) => {
    setWorking(true); setError('')
    try {
      const next = await opsApi.setLiveOps(body)
      setOps(next)
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  const locked = busy || working
  return <section className={`card p-3 gcol-full ${ops?.maintenanceMode ? 'border-warning' : ''}`}>
    <div className="panel-title">
      <h2>Live Operations</h2>
      <span>{ops?.maintenanceMode ? 'Maintenance is ON' : 'Game is open'}</span>
    </div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    <p>Maintenance blocks every gameplay action for players while leaving reads and admin access open, so you can verify a deploy before letting anyone back in.</p>
    <div className="control-row">
      <Button
        className={ops?.maintenanceMode ? 'btn btn-primary btn-sm' : 'btn btn-secondary btn-sm'}
        blocked={locked && WORKING}
        onClick={() => void apply({ maintenanceMode: !ops?.maintenanceMode })}
      >
        {ops?.maintenanceMode ? 'End maintenance' : 'Start maintenance'}
      </Button>
      <label className="field">Maintenance notice<input className="form-control" value={maintenanceMessage} onChange={e => setMaintenanceMessage(e.target.value)} placeholder="Back in 10 minutes" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
        onClick={() => void apply({ maintenanceMessage })}>Save notice</Button>
    </div>
    <div className="control-row">
      <label className="grow">Announcement banner<input className="form-control" value={announcement} onChange={e => setAnnouncement(e.target.value)} placeholder="Shown to every player" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
        onClick={() => void apply({ announcement })}>Save banner</Button>
      <Button className="btn btn-secondary btn-sm" blocked={firstReason(
        locked && WORKING,
        !ops?.announcement && 'There is no banner up to clear.',
      )} onClick={() => void apply({ announcement: '' })}>Clear</Button>
    </div>
    {ops && <small className="d-block mt-2 text-body-tertiary small">Last changed {new Date(ops.updatedAtUtc).toLocaleString()}{ops.updatedBy ? ` by ${ops.updatedBy}` : ''}.</small>}
  </section>
}

/**
 * Live tuning. Values here take effect on the next request, without a restart, because the services
 * read configuration per scope. Table-shaped settings (storage levels, lab tiers) stay in appsettings.
 */
function AdminConfigPanel({ busy }: { busy: boolean }) {
  const [config, setConfig] = useState<AdminConfig | null>(null)
  const [filter, setFilter] = useState('')
  const [edits, setEdits] = useState<Record<string, string>>({})
  const [reason, setReason] = useState('')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [working, setWorking] = useState(false)
  const [showAll, setShowAll] = useState(false)

  const load = async () => {
    try { setConfig(await configApi.get()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const run = async (label: string, fn: () => Promise<ActionResult>) => {
    if (reason.trim().length < 3) {
      setError('Give a reason first. Tuning changes are audited.')
      return
    }
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await fn()
      setMessage(`${label}: ${result.summary}`)
      setEdits({})
      await load()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  if (!config) return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Tuning</h2><span>Live config</span></div>
    {error ? <div className="alert alert-danger"><span>{error}</span></div> : <p className="text-body-tertiary small mt-3 mb-0">Loading.</p>}
  </section>

  const needle = filter.trim().toLowerCase()
  const matches = config.settings.filter(entry =>
    (!showAll ? entry.isOverridden || needle.length > 0 : true)
    && (needle.length === 0 || entry.path.toLowerCase().includes(needle)))
  const locked = busy || working

  return <section className="card p-3 gcol-full">
    <div className="panel-title">
      <h2>Tuning</h2>
      <span>{config.overrideCount} override{config.overrideCount === 1 ? '' : 's'} live</span>
    </div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {message && <div className="alert alert-success"><span>{message}</span></div>}
    <p>Changes apply on the next request, no restart. Overrides are stored in the database and layered over appsettings, so clearing one falls back to the shipped value. Table-shaped settings like storage levels are not editable here.</p>

    <label className="field">Reason (recorded in the audit trail)
      <input className="form-control" value={reason} onChange={e => setReason(e.target.value)} placeholder="Why are you retuning this?" />
    </label>

    <div className="control-row">
      <label className="grow">Filter<input className="form-control" value={filter} onChange={e => setFilter(e.target.value)} placeholder="combat, morale, price..." /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING} onClick={() => setShowAll(value => !value)}>
        {showAll ? 'Show overrides only' : `Show all ${config.settings.length}`}
      </Button>
    </div>

    <div className="d-grid gap-1 mt-3 config-list">
      {matches.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">
        {showAll ? 'Nothing matches that filter.' : 'No overrides yet. Filter or show all to change something.'}
      </p>}
      {matches.map(entry => <ConfigRow
        key={entry.path}
        entry={entry}
        draft={edits[entry.path] ?? entry.effectiveValue}
        locked={locked}
        onDraft={value => setEdits(current => ({ ...current, [entry.path]: value }))}
        onSave={() => void run('Set', () => configApi.set(entry.path, edits[entry.path] ?? entry.effectiveValue, reason))}
        onClear={() => void run('Cleared', () => configApi.clear(entry.path, reason))}
      />)}
    </div>
  </section>
}

function ConfigRow({ entry, draft, locked, onDraft, onSave, onClear }: {
  entry: AdminConfigEntry
  draft: string
  locked: boolean
  onDraft: (value: string) => void
  onSave: () => void
  onClear: () => void
}) {
  const dirty = draft.trim() !== entry.effectiveValue.trim()
  return <div className={`config-row d-grid gap-2 align-items-center border-top py-2 ${entry.isOverridden ? 'border-primary' : ''}`}>
    <div className="config-copy d-grid gap-1 min-w-0">
      <strong>{entry.path}</strong>
      <span>
        {entry.type}{entry.isOverridden ? ' / overridden' : ' / from appsettings'}
        {/* Stated rather than discovered by being refused. These are the settings where a number past
            the limit breaks something - a line too long for the column, a chance above certainty - so
            the bound is worth reading before you type over it rather than after. */}
        {entry.minimum != null && entry.maximum != null && ` / ${entry.minimum} to ${entry.maximum}`}
      </span>
    </div>
    <input className="form-control" value={draft} onChange={e => onDraft(e.target.value)} />
    <Button className="btn btn-primary btn-sm" blocked={firstReason(
      locked && WORKING,
      !dirty && 'Nothing has been changed here.',
    )} onClick={onSave}>Save</Button>
    <Button className="btn btn-secondary btn-sm" blocked={firstReason(
      locked && WORKING,
      !entry.isOverridden && 'This one is still the value from appsettings. There is no override to reset.',
    )} onClick={onClear}>Reset</Button>
  </div>
}

function AdminOversightPanel({ busy }: { busy: boolean }) {
  const [data, setData] = useState<AdminOversight | null>(null)
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const load = async () => {
    try { setData(await opsApi.oversight()) } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [])

  const resolve = async (missionId: number) => {
    setWorking(true); setError('')
    try { await opsApi.forceResolve(missionId); await load() }
    catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  if (!data) return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Oversight</h2><span>Economy and combat</span></div>
    {error ? <div className="alert alert-danger"><span>{error}</span></div> : <p className="text-body-tertiary small mt-3 mb-0">Loading.</p>}
  </section>

  const overdue = data.activeMissions.filter(mission => mission.isOverdue)
  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Oversight</h2><span>Economy and combat</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
      <AdminMetric label="Median net worth" value={money.format(data.medianNetWorth)} />
      <AdminMetric label="Richest" value={money.format(data.topNetWorth)} />
      <AdminMetric label="Concentration" value={`${data.giniPercent.toFixed(1)}% Gini`} />
      <AdminMetric label="Active missions" value={number.format(data.activeMissions.length)} />
      <AdminMetric label="Stuck missions" value={number.format(overdue.length)} />
    </div>

    <div className="control-block">
      <strong>Wealth spread</strong>
      <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
        {data.wealthBands.map(band => <AdminMetric key={band.label} label={band.label} value={`${number.format(band.players)} / ${money.format(band.totalNetWorth)}`} />)}
      </div>
      <small>Gini runs 0 (everyone equal) to 100 (one player holds everything).</small>
    </div>

    <div className="control-block">
      <strong>Fastest movers, last 24h</strong>
      <div className="d-grid gap-1">
        {data.fastestMovers.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No logged activity in the last day.</p>}
        {data.fastestMovers.map(mover => <div className="audit-row d-grid gap-1 border-top py-2" key={mover.playerId}>
          <div>
            <strong>{mover.name}{mover.isBot ? ' (AI)' : ''}</strong>
            <span>{money.format(mover.cashGained24h)} in {number.format(mover.actionsLast24h)} actions</span>
          </div>
          <p>Net worth {money.format(mover.netWorth)}</p>
        </div>)}
      </div>
      <small>Approximated from logged cash and bank deltas; the game keeps no net worth history to diff.</small>
    </div>

    <div className="control-block">
      <strong>In-flight missions</strong>
      <div className="d-grid gap-1">
        {data.activeMissions.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nothing in flight.</p>}
        {data.activeMissions.map(mission => <div className={`audit-row d-grid gap-1 border-top py-2 ${mission.isOverdue ? 'border-primary' : ''}`} key={mission.missionId}>
          <div>
            <strong>{mission.status}{mission.isOverdue ? ' / STUCK' : ''}</strong>
            <span>round {mission.currentRound}/{mission.maxRounds}</span>
          </div>
          <p>{mission.commanderName ?? 'A pimp'} ({mission.attackerName}) vs {mission.defenderName}</p>
          <div className="control-row">
            <em>{mission.nextEventAtUtc ? `next ${new Date(mission.nextEventAtUtc).toLocaleTimeString()}` : 'no timer'}</em>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(busy && WORKING, working && WORKING)}
              onClick={() => void resolve(mission.missionId)}>Force resolve</Button>
          </div>
        </div>)}
      </div>
    </div>

    <div className="control-block">
      <strong>AI health</strong>
      <div className="d-grid gap-1">
        {data.bots.map(bot => <div className="audit-row d-grid gap-1 border-top py-2" key={bot.playerId}>
          <div>
            <strong>{bot.name}</strong>
            <span>{bot.personality}</span>
          </div>
          <p>{money.format(bot.netWorth)} / {botPresence(bot)}</p>
        </div>)}
      </div>
    </div>
  </section>
}

/**
 * Player administration. Owns its own state and talks to the admin API directly rather than threading
 * a dozen fields through PageContext, matching how AdminPanel already handles its local controls.
 */
function AdminPlayersPanel({ busy, onChanged }: { busy: boolean, onChanged: () => void }) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<AdminPlayerSummary[]>([])
  const [detail, setDetail] = useState<AdminPlayerDetail | null>(null)
  const [reason, setReason] = useState('')
  const [resource, setResource] = useState('cash')
  const [delta, setDelta] = useState(10000)
  const [renameTo, setRenameTo] = useState('')
  const [suspendHours, setSuspendHours] = useState(24)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [working, setWorking] = useState(false)

  const search = async (event?: FormEvent<HTMLFormElement>) => {
    event?.preventDefault()
    setError('')
    try {
      setResults(await adminApi.searchPlayers(query))
    } catch (e) { setError((e as Error).message) }
  }

  const open = async (playerId: string) => {
    setError(''); setMessage('')
    try {
      const next = await adminApi.playerDetail(playerId)
      setDetail(next)
      setRenameTo(next.summary.name)
    } catch (e) { setError((e as Error).message) }
  }

  // Every mutation needs a reason: it is what makes the audit trail worth having.
  const run = async (label: string, fn: () => Promise<ActionResult>, requireReason = true) => {
    if (requireReason && reason.trim().length < 3) {
      setError('Give a reason first. It goes in the audit trail.')
      return
    }
    setWorking(true); setError(''); setMessage('')
    try {
      const result = await fn()
      setMessage(`${label}: ${result.summary}`)
      if (detail) await open(detail.summary.playerId)
      await search()
      onChanged()
    } catch (e) { setError((e as Error).message) }
    finally { setWorking(false) }
  }

  useEffect(() => { void search() }, [])
  const locked = busy || working
  const target = detail?.summary

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Players</h2><span>Find and fix</span></div>
    <form className="d-grid gtc-1 gtc-md-1-auto gap-2 align-items-end mb-3" onSubmit={search}>
      <label className="field">Search<input className="form-control" value={query} onChange={e => setQuery(e.target.value)} placeholder="Player, username, or city" /></label>
      <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}>Search</Button>
    </form>

    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {message && <div className="alert alert-success"><span>{message}</span></div>}

    <div className="d-grid gtc-1 gtc-lg-split-280 gap-3 mt-3">
      <div className="admin-player-list d-grid gap-1 align-content-start overflow-y-auto">
        {results.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No players matched.</p>}
        {results.map(player => <Button
          className={`btn admin-player-row d-grid gap-1 column-gap-2 align-items-center text-start border rounded bg-body-secondary p-2 ${target?.playerId === player.playerId ? 'active border-primary' : ''}`}
          key={player.playerId}
          type="button"
          blocked={locked && WORKING}
          onClick={() => void open(player.playerId)}
        >
          <strong>{player.name}</strong>
          <small>
            {player.username}{player.isBot ? ' / AI' : ''}{player.isAdmin ? ' / admin' : ''}
            {/* Marked in the list, so a search that matched on identity shows why without a click. */}
            {player.discordUsername && ` / ${player.discordUsername}`}
            {player.emailVerified && ' / ✉'}
          </small>
          <em>{enforcementLabel(player)}</em>
          <b>{money.format(player.netWorth)}</b>
        </Button>)}
      </div>

      {detail && target && <div className="d-grid gap-3 align-content-start border rounded bg-body-tertiary p-3">
        <div className="d-flex justify-content-between align-items-start gap-3">
          <div className="d-grid gap-1">
            <strong className="text-body fs-6">{target.name}</strong>
            <span className="text-body-secondary small">{target.username} / {target.city}</span>
          </div>
          <b className={`badge ${target.isBanned ? 'text-bg-danger' : 'text-bg-success'}`}>{enforcementLabel(target)}</b>
        </div>
        <div className="tnum d-grid gtc-2 gtc-md-3 gtc-xl-5 gap-2">
          <AdminMetric label="Net worth" value={money.format(target.netWorth)} />
          <AdminMetric label="Cash" value={money.format(target.cash)} />
          <AdminMetric label="Bank" value={money.format(target.bankCash)} />
          <AdminMetric label="Turns" value={number.format(target.turns)} />
          <AdminMetric label="Crew" value={`${target.pimps} P / ${target.hoes} H / ${target.thugs} T`} />
          <AdminMetric label="Morale" value={`${detail.hoeHappiness.toFixed(0)}% / ${detail.thugHappiness.toFixed(0)}%`} />
          <AdminMetric label="Hideout" value={`${detail.hideout.tierName} S${detail.hideout.storageLevel}/V${detail.hideout.safeLevel}`} />
          <AdminMetric label="Joined" value={new Date(target.createdAtUtc).toLocaleDateString()} />
        </div>

        {/*
          Who this account actually is, rather than what it owns.

          A moderator handling a returning ban evader is asking one question - is this the same person -
          and the panel could not previously answer it at all. A username is the first thing somebody
          changes on the way to a second account; a Discord snowflake is the last, which is why it is
          shown as well as the handle and why both are searchable.
        */}
        <div className="tnum d-grid gtc-1 gtc-md-3 gap-2">
          <AdminMetric
            label="Email"
            value={target.email ?? '—'}
            sub={target.email ? (target.emailVerified ? 'Confirmed' : 'Not confirmed') : 'None set'}
          />
          <AdminMetric label="Discord" value={target.discordUsername ?? '—'} sub={target.discordUsername ? 'Connected' : 'Not connected'} />
          <AdminMetric label="Discord ID" value={target.discordUserId ?? '—'} sub="Survives a rename" />
        </div>

        <label className="field">Reason (recorded in the audit trail)
          <input className="form-control" value={reason} onChange={e => setReason(e.target.value)} placeholder="Why are you doing this?" />
        </label>

        <div className="control-block">
          <strong>Quick grants</strong>
          <div className="d-grid gtc-1 gtc-md-4 gap-2">
            {adjustPresets.map(preset => <Button
              className="btn btn-secondary btn-sm"
              key={preset.label}
              blocked={locked && WORKING}
              onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, preset.resource, preset.delta, reason))}
            >{preset.label}</Button>)}
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Morale set', () => adminApi.setMorale(target.playerId, 100, reason))}>Morale 100%</Button>
          </div>
        </div>

        <div className="control-block">
          <strong>Adjust a resource</strong>
          <div className="control-row">
            <label className="field">Resource<select className="form-select" value={resource} onChange={e => setResource(e.target.value)}>
              {detail.adjustableResources.map(key => <option key={key} value={key}>{key}</option>)}
            </select></label>
            <label className="field">Change<input className="form-control" type="number" value={delta} onChange={e => setDelta(Number(e.target.value))} /></label>
            <Button className="btn btn-primary btn-sm" blocked={firstReason(
              locked && WORKING,
              delta === 0 && 'A change of zero does nothing. Set an amount first.',
            )} onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, resource, delta, reason))}>
              Apply
            </Button>
          </div>
          <small>Negative values take resources away. Nothing drops below zero.</small>
        </div>

        <div className="control-block">
          <strong>Account</strong>
          <div className="control-row">
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Banned', () => adminApi.enforcement(target.playerId, 'ban', null, reason))}>
              Ban
            </Button>
            <label className="field">Suspend hours<input className="form-control" type="number" min={1} value={suspendHours} onChange={e => setSuspendHours(Number(e.target.value))} /></label>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(
              locked && WORKING,
              suspendHours < 1 && 'A suspension has to run for at least an hour.',
            )} onClick={() => void run('Suspended', () => adminApi.enforcement(
                target.playerId,
                'suspend',
                new Date(Date.now() + suspendHours * 3600_000).toISOString(),
                reason))}>
              Suspend
            </Button>
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Cleared', () => adminApi.enforcement(target.playerId, 'clear', null, reason))}>
              Lift
            </Button>
            <Button className="btn btn-secondary btn-sm" blocked={locked && WORKING}
              onClick={() => void run('Logged out', () => adminApi.forceLogout(target.playerId, reason))}>
              Force logout
            </Button>
          </div>
        </div>

        <div className="control-block">
          <strong>Identity and rights</strong>
          <div className="control-row">
            <label className="field">Name<input className="form-control" value={renameTo} onChange={e => setRenameTo(e.target.value)} minLength={3} maxLength={32} /></label>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(
              locked && WORKING,
              renameTo.trim() === target.name && `They are already called ${target.name}.`,
            )} onClick={() => void run('Renamed', () => adminApi.rename(target.playerId, renameTo, reason))}>
              Rename
            </Button>
            <Button className="btn btn-secondary btn-sm" blocked={firstReason(
              locked && WORKING,
              target.isBot && 'A rival run by the game cannot be given admin rights.',
            )} onClick={() => void run('Rights changed', () => adminApi.setAdminRights(target.playerId, !target.isAdmin, reason))}>
              {target.isAdmin ? 'Revoke admin' : 'Grant admin'}
            </Button>
          </div>
        </div>

        <div className="control-block">
          <strong>Recent activity</strong>
          <ActivityList entries={detail.recentActivity.slice(0, 6)} />
        </div>

        {detail.auditTrail.length > 0 && <div className="control-block">
          <strong>Admin history for this player</strong>
          <AuditList entries={detail.auditTrail} />
        </div>}
      </div>}
    </div>
  </section>
}

function AdminAuditPanel() {
  const [entries, setEntries] = useState<AdminAuditEntry[]>([])
  const [error, setError] = useState('')
  useEffect(() => {
    void (async () => {
      try { setEntries(await adminApi.audit()) } catch (e) { setError((e as Error).message) }
    })()
  }, [])

  return <section className="card p-3 gcol-full">
    <div className="panel-title"><h2>Audit Trail</h2><span>Every admin action</span></div>
    {error && <div className="alert alert-danger"><span>{error}</span></div>}
    {entries.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">No admin actions recorded yet.</p>}
    <AuditList entries={entries.slice(0, 30)} />
  </section>
}

function AuditList({ entries }: { entries: AdminAuditEntry[] }) {
  return <div className="d-grid gap-1">
    {entries.map(entry => <div className="audit-row d-grid gap-1 border-top py-2" key={entry.id}>
      <div>
        <strong>{entry.action}</strong>
        <span>{entry.actorUsername}{entry.targetName ? ` -> ${entry.targetName}` : ''}</span>
      </div>
      <p>{entry.summary}</p>
      {entry.reason && <small>"{entry.reason}"</small>}
      <em>{new Date(entry.createdAtUtc).toLocaleString()}</em>
    </div>)}
  </div>
}

function enforcementLabel(player: AdminPlayerSummary) {
  if (player.isBanned) return 'Banned'
  if (player.suspendedUntilUtc && new Date(player.suspendedUntilUtc) > new Date()) return 'Suspended'
  return 'Active'
}

/**
 * Defence alerts. Opening the panel marks everything read by moving the server-side watermark, then
 * refreshes so the badge clears. The count itself rides on the dashboard, so the bell costs no extra
 * request until it is opened.
 */
// Shown once on arrival and only when something actually happened. A popup that says the world stood
// still while you were out is an interruption with nothing behind it.

function BotDirectivePanel({ bot, targets, selfId, selfName, busy, onRun }: {
  bot: AdminBotHealth
  targets: AdminBotHealth[]
  selfId: string
  selfName: string
  busy: boolean
  onRun: (directive: BotDirective) => void
}) {
  const [action, setAction] = useState('street')
  const [turns, setTurns] = useState(10)
  const [quantity, setQuantity] = useState(10)
  const [amount, setAmount] = useState(10000)
  const [product, setProduct] = useState('weed')
  const [item, setItem] = useState('condoms')
  const [role, setRole] = useState('hoes')
  const [strategy, setStrategy] = useState('rest')
  const [room, setRoom] = useState('storage')
  const [defenderId, setDefenderId] = useState(selfId)

  const directive = (): BotDirective => {
    switch (action) {
      case 'street': return { action, turns }
      case 'produce': return { action, product, turns }
      case 'sell': return { action, product, quantity }
      case 'buy': return { action, item, quantity }
      case 'hire': case 'fire': return { action, role, quantity }
      case 'deposit': case 'withdraw': return { action, amount }
      case 'recover': return { action, strategy }
      case 'upgrade': return { action, room }
      case 'attack': return { action, defenderId, thugs: quantity, weapons: quantity }
      default: return { action }
    }
  }

  return <div className="mt-3 border rounded bg-body-tertiary p-3">
    <div className="d-flex flex-column flex-md-row justify-content-between align-items-start align-items-md-baseline gap-1 gap-md-3">
      <strong>Direct {bot.name}</strong>
      <span className="eyebrow">Runs through the real rules, so a refusal is the game refusing</span>
    </div>
    <div className="control-row mt-2">
      <label className="field">Action<select className="form-select" value={action} onChange={e => setAction(e.target.value)}>
        <option value="street">Work the streets</option>
        <option value="produce">Produce</option>
        <option value="sell">Sell product</option>
        <option value="buy">Buy supplies</option>
        <option value="hire">Hire crew</option>
        <option value="fire">Fire crew</option>
        <option value="deposit">Deposit</option>
        <option value="withdraw">Withdraw</option>
        <option value="recover">Recover morale</option>
        <option value="upgrade">Upgrade hideout</option>
        <option value="attack">Attack someone</option>
      </select></label>

      {(action === 'street' || action === 'produce') &&
        <label className="field">Turns<input className="form-control" type="number" min={1} max={20} value={turns} onChange={e => setTurns(Number(e.target.value))} /></label>}
      {(action === 'produce' || action === 'sell') &&
        <label className="field">Product<select className="form-select" value={product} onChange={e => setProduct(e.target.value)}>
          <option value="weed">Weed</option><option value="coke">Coke</option>
        </select></label>}
      {action === 'buy' &&
        <label className="field">Item<select className="form-select" value={item} onChange={e => setItem(e.target.value)}>
          {/* The store sells guns by tier, so this offers them by tier. "weapons" was refused. */}
          <option value="condoms">Condoms</option><option value="beer">Beer</option><option value="medicine">Medicine</option>
          <option value="pistols">Pistols</option><option value="shotguns">Shotguns</option>
          <option value="smgs">SMGs</option><option value="rifles">Rifles</option>
        </select></label>}
      {(action === 'hire' || action === 'fire') &&
        <label className="field">Role<select className="form-select" value={role} onChange={e => setRole(e.target.value)}>
          <option value="pimps">Pimps</option><option value="hoes">Hoes</option><option value="thugs">Thugs</option>
        </select></label>}
      {(action === 'sell' || action === 'buy' || action === 'hire' || action === 'fire') &&
        <label className="field">Quantity<input className="form-control" type="number" min={1} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>}
      {(action === 'deposit' || action === 'withdraw') &&
        <label className="field">Amount<input className="form-control" type="number" min={1} step={1000} value={amount} onChange={e => setAmount(Number(e.target.value))} /></label>}
      {action === 'recover' &&
        <label className="field">Strategy<select className="form-select" value={strategy} onChange={e => setStrategy(e.target.value)}>
          <option value="rest">Rest</option><option value="party">Party</option>
        </select></label>}
      {action === 'upgrade' &&
        <label className="field">Room<select className="form-select" value={room} onChange={e => setRoom(e.target.value)}>
          <option value="tier">Building tier</option><option value="storage">Storage</option>
          <option value="safe">Safe</option><option value="weedlab">Weed lab</option><option value="cokelab">Coke lab</option>
        </select></label>}
      {action === 'attack' && <>
        <label className="field">Target<select className="form-select" value={defenderId} onChange={e => setDefenderId(e.target.value)}>
          <option value={selfId}>{selfName} (you)</option>
          {targets.map(t => <option key={t.playerId} value={t.playerId}>{t.name}</option>)}
        </select></label>
        <label className="field">Thugs<input className="form-control" type="number" min={1} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>
      </>}

      <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => onRun(directive())}>Do it</Button>
    </div>
  </div>
}

function AdminAiTab({ ctx }: { ctx: PageContext & { overview: AdminOverview } }) {
  const { overview, busy, seedBots, runBots, setBotAutomation } = ctx
  const auto = overview.botAutomation
  const [seedCount, setSeedCount] = useState(10)
  const [runRounds, setRunRounds] = useState(1)
  const [tickSeconds, setTickSeconds] = useState(auto.tickSeconds)
  const [roundsPerTick, setRoundsPerTick] = useState(auto.roundsPerTick)
  const [roster, setRoster] = useState<AdminBotHealth[]>([])
  const [rosterError, setRosterError] = useState('')
  const [working, setWorking] = useState<string | null>(null)
  const [directing, setDirecting] = useState<string | null>(null)

  // Re-reads the roster rather than patching it locally, so an action's real effect on net worth and
  // idle time shows up instead of just the flag that was toggled.
  const rivalAction = async (playerId: string, run: () => Promise<unknown>) => {
    setWorking(playerId); setRosterError('')
    try {
      await run()
      setRoster((await opsApi.oversight()).bots)
    } catch (e) { setRosterError((e as Error).message) }
    finally { setWorking(null) }
  }

  // Follow the server whenever it reports different timings, so an edit made elsewhere does not leave
  // stale numbers sitting in the inputs.
  useEffect(() => { setTickSeconds(auto.tickSeconds); setRoundsPerTick(auto.roundsPerTick) }, [auto.tickSeconds, auto.roundsPerTick])
  useEffect(() => {
    opsApi.oversight()
      .then((data: AdminOversight) => setRoster(data.bots))
      .catch((e: unknown) => setRosterError((e as Error).message))
  }, [overview.generatedAtUtc])

  const timingChanged = tickSeconds !== auto.tickSeconds || roundsPerTick !== auto.roundsPerTick
  const timingValid = tickSeconds >= auto.minTickSeconds && tickSeconds <= auto.maxTickSeconds
    && roundsPerTick >= auto.minRoundsPerTick && roundsPerTick <= auto.maxRoundsPerTick
  const atDefaults = auto.tickSeconds === auto.defaultTickSeconds && auto.roundsPerTick === auto.defaultRoundsPerTick

  return <>
    <section className="card p-3 gcol-full">
      <div className="panel-title">
        <h2>Automatic AI</h2>
        <span>{auto.enabled ? `On, ${auto.roundsPerTick} round(s) every ${auto.tickSeconds}s` : 'Off'}</span>
      </div>
      <p>
        Rivals act on their own on this loop. The setting is saved, so it survives a restart, and the
        timing takes effect on the next tick without one.
      </p>
      <div className="control-row">
        <Button
          className={auto.enabled ? 'btn btn-secondary btn-sm' : 'btn btn-primary btn-sm'}
          blocked={firstReason(
            busy && WORKING,
            overview.botAccounts < 1 && 'There are no rivals for the loop to run. Seed some below first.',
          )}
          onClick={() => setBotAutomation(!auto.enabled)}
        >
          {auto.enabled ? 'Turn off' : 'Turn on'}
        </Button>
        <label className="field">Tick seconds<input className="form-control"
          type="number"
          min={auto.minTickSeconds}
          max={auto.maxTickSeconds}
          value={tickSeconds}
          onChange={e => setTickSeconds(Number(e.target.value))}
        /></label>
        <label className="field">Rounds per tick<input className="form-control"
          type="number"
          min={auto.minRoundsPerTick}
          max={auto.maxRoundsPerTick}
          value={roundsPerTick}
          onChange={e => setRoundsPerTick(Number(e.target.value))}
        /></label>
        <Button
          className="btn btn-secondary btn-sm"
          blocked={firstReason(
            busy && WORKING,
            !timingChanged && 'The timing is already what is saved.',
            !timingValid && `Tick has to be ${auto.minTickSeconds}-${auto.maxTickSeconds}s and rounds ${auto.minRoundsPerTick}-${auto.maxRoundsPerTick}.`,
          )}
          onClick={() => setBotAutomation(auto.enabled, { tickSeconds, roundsPerTick })}
        >
          Save timing
        </Button>
        <Button
          className="btn btn-secondary btn-sm"
          blocked={firstReason(
            busy && WORKING,
            atDefaults && 'The timing is already at the defaults.',
          )}
          onClick={() => setBotAutomation(auto.enabled, { resetTiming: true })}
        >
          Reset to {auto.defaultTickSeconds}s / {auto.defaultRoundsPerTick}
        </Button>
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>Seed and Run</h2><span>{number.format(overview.botAccounts)} rivals exist</span></div>
      <div className="control-row">
        <label className="field">Seed count<input className="form-control" type="number" min={1} max={15} value={seedCount} onChange={e => setSeedCount(Number(e.target.value))} /></label>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setSeedCount(5)}>5</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setSeedCount(10)}>10</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setSeedCount(15)}>15</Button>
        <Button className="btn btn-primary btn-sm" blocked={firstReason(
          busy && WORKING,
          (seedCount < 1 || seedCount > 15) && 'Seed between 1 and 15 rivals at a time.',
        )} onClick={() => seedBots(seedCount)}>Seed rivals</Button>
      </div>
      <div className="control-row">
        <label className="field">Rounds<input className="form-control" type="number" min={1} max={10} value={runRounds} onChange={e => setRunRounds(Number(e.target.value))} /></label>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setRunRounds(1)}>1</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setRunRounds(3)}>3</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => setRunRounds(10)}>10</Button>
        <Button className="btn btn-primary btn-sm" blocked={firstReason(
          busy && WORKING,
          overview.botAccounts < 1 && 'There are no rivals to run. Seed some first.',
          (runRounds < 1 || runRounds > 10) && 'Run between 1 and 10 rounds at a time.',
        )} onClick={() => runBots(runRounds)}>Run now</Button>
      </div>
    </section>

    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The Rivals</h2><span>Personality and playing habits</span></div>
      {rosterError && <div className="alert alert-danger"><span>{rosterError}</span></div>}
      {roster.length === 0 && !rosterError && <p className="text-body-tertiary small mt-3 mb-0">No AI rivals yet.</p>}
      {roster.length > 0 && <div className="table-responsive mt-3"><table className="table table-sm table-hover align-middle game-table">
        <thead><tr><th>Name</th><th>Personality</th><th>Net worth</th><th>Idle</th><th>Habits</th><th>State</th><th /></tr></thead>
        <tbody>
          {roster.map(bot => <tr key={bot.playerId} className={rivalRowClass(bot)}>
            <td>{bot.name}</td>
            <td>{bot.personality}</td>
            <td>{money.format(bot.netWorth)}</td>
            <td>{bot.lastActionAtUtc ? `${number.format(bot.minutesIdle)}m` : 'never acted'}</td>
            <td>{bot.habits}</td>
            <td>{bot.isPaused ? 'Paused' : botPresence(bot)}</td>
            <td className="d-flex gap-1">
              <Button
                className="btn btn-secondary btn-sm"
                blocked={working === bot.playerId && `${bot.name} is mid-action. Wait for it to land.`}
                onClick={() => void rivalAction(bot.playerId, () => opsApi.setBotPaused(bot.playerId, !bot.isPaused))}
              >
                {bot.isPaused ? 'Resume' : 'Pause'}
              </Button>
              <Button
                className="btn btn-secondary btn-sm"
                blocked={firstReason(
                  working === bot.playerId && `${bot.name} is mid-action. Wait for it to land.`,
                  bot.isPaused && `${bot.name} is paused. Resume them first.`,
                )}
                title="Act now, ignoring the cooldown"
                onClick={() => void rivalAction(bot.playerId, () => opsApi.actNow(bot.playerId))}
              >
                Act now
              </Button>
              <Button
                className="btn btn-secondary btn-sm"
                blocked={working === bot.playerId && `${bot.name} is mid-action. Wait for it to land.`}
                onClick={() => setDirecting(id => id === bot.playerId ? null : bot.playerId)}
              >
                {directing === bot.playerId ? 'Close' : 'Direct'}
              </Button>
            </td>
          </tr>)}
        </tbody>
      </table></div>}
      {directing && <BotDirectivePanel
        bot={roster.find(x => x.playerId === directing)!}
        targets={roster.filter(x => x.playerId !== directing)}
        selfId={ctx.dashboard.playerId}
        selfName={ctx.dashboard.name}
        busy={working === directing}
        onRun={directive => void rivalAction(directing, () => opsApi.directBot(directing, directive))}
      />}
    </section>
  </>
}

