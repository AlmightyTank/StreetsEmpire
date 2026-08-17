import React, { FormEvent, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { adminApi, api, configApi, opsApi } from './api'
import type { ActionResult, AdminAuditEntry, Alert, AdminConfig, AdminConfigEntry, AdminOverview, AdminBotHealth, AdminOversight, AdminPlayerDetail, AdminPlayerSummary, CombatLog, CombatMission, Dashboard, HideoutRoom, HideoutRoomUpgrade, LeaderboardEntry, LiveOps, Pimp, BotDirective, MoraleDirection, MoraleTrend, MarketBoard, MuleBoard, MuleQuote, PlayerProfile, PlayerTarget, TerritoryBoard, TravelStatus, WorldNews, WorldNewsEntry, CatchUp, CityMarket } from './api'
import './styles.css'

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('en-US')

type AppPage = 'overview' | 'street' | 'crew' | 'hideout' | 'territory' | 'market' | 'mules' | 'recon' | 'admin'

// Quick grants for the selected player. Every one goes through the audited adjust endpoint, so
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
  { label: '+10 weapons', resource: 'weapons', delta: 10 },
  { label: '+250 weed', resource: 'weed', delta: 250 },
  { label: '+100 coke', resource: 'coke', delta: 100 },
]

const pageMeta: Record<AppPage, { label: string, short: string, kicker: string }> = {
  overview: { label: 'Overview', short: 'OV', kicker: 'Command center' },
  street: { label: 'Street', short: 'ST', kicker: 'Turns and cash' },
  crew: { label: 'Crew', short: 'CR', kicker: 'Morale and hiring' },
  hideout: { label: 'Hideout', short: 'HO', kicker: 'Capacity and upgrades' },
  territory: { label: 'Territory', short: 'TR', kicker: 'Ground you hold' },
  market: { label: 'Market', short: 'MK', kicker: 'Store, product, bank' },
  mules: { label: 'Mules', short: 'MU', kicker: 'Runs out of town' },
  recon: { label: 'Combat', short: 'CB', kicker: 'Targets and missions' },
  admin: { label: 'Admin', short: 'AD', kicker: 'Control center' },
}

function App() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [adminOverview, setAdminOverview] = useState<AdminOverview | null>(null)
  const [leaders, setLeaders] = useState<LeaderboardEntry[]>([])
  const [targets, setTargets] = useState<PlayerTarget[]>([])
  const [selectedTarget, setSelectedTarget] = useState<PlayerProfile | null>(null)
  const [combatLogs, setCombatLogs] = useState<CombatLog[]>([])
  const [combatMissions, setCombatMissions] = useState<CombatMission[]>([])
  const [worldNews, setWorldNews] = useState<WorldNews>({ headlines: [], feed: [] })
  const [targetQuery, setTargetQuery] = useState('')
  const [activePage, setActivePage] = useState<AppPage>('overview')
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  // Fetched rather than hardcoded: the towns come from the territory map, so a city with no ground
  // could never be offered as somewhere to set up.
  const [cities, setCities] = useState<string[]>([])
  useEffect(() => { void api.cities().then(setCities).catch(() => setCities([])) }, [])
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [lastBreakdown, setLastBreakdown] = useState<Record<string, unknown> | null>(null)
  const [busy, setBusy] = useState(false)
  const [streetTurns, setStreetTurns] = useState(5)
  const [autoBuySupplies, setAutoBuySupplies] = useState(false)
  const [productionTurns, setProductionTurns] = useState(5)
  const [hoeCut, setHoeCut] = useState(30)
  const [bankAmount, setBankAmount] = useState(1000)
  const [crewQty, setCrewQty] = useState<Record<'pimps' | 'hoes' | 'thugs', number>>({ pimps: 1, hoes: 1, thugs: 1 })
  const [attackCrew, setAttackCrew] = useState({ thugs: 1, weapons: 0 })
  const [commanderId, setCommanderId] = useState<number | null>(null)
  // Left empty so each page derives its own default until the player types a quantity.
  const [storeQty, setStoreQty] = useState<Record<string, number>>({})
  const [sellQty, setSellQty] = useState<Record<'weed' | 'coke', number>>({ weed: 10, coke: 5 })
  const [tickSeconds, setTickSeconds] = useState(0)
  const [catchUp, setCatchUp] = useState<CatchUp | null>(null)

  /**
   * Full reload after an action. `pollMissions` instead re-reads only what a running mission changes,
   * which keeps the 5-second poll from re-fetching the leaderboard, world news and target list.
   */
  const refresh = async () => {
    try {
      const [d, l, news, targetList, combatHistory, missions] = await Promise.all([api.dashboard(), api.leaderboard(), api.worldNews(), api.targets(targetQuery), api.combatLogs(), api.combatMissions()])
      const admin = d.isAdmin ? await api.adminOverview() : null
      setDashboard(d)
      setAdminOverview(admin)
      setLeaders(l)
      setWorldNews(news)
      setTargets(targetList)
      setCombatLogs(combatHistory)
      setCombatMissions(missions)
      setTickSeconds(d.secondsUntilNextTurnTick)
      setHoeCut(d.hoeCutPercent)
      setError('')
    } catch (e) {
      if ((e as Error).message === 'Unauthorized') {
        setDashboard(null)
        setAdminOverview(null)
        setWorldNews({ headlines: [], feed: [] })
        setCombatLogs([])
        setCombatMissions([])
        setTargets([])
        setSelectedTarget(null)
        setActivePage('overview')
      } else setError((e as Error).message)
    }
  }

  // A boolean, not the mission array: depending on the array rebuilt the interval on every poll.
  const hasActiveMission = combatMissions.some(mission => mission.status !== 'Complete')
  const hadActiveMission = useRef(false)
  const catchUpFetched = useRef(false)

  const pollMissions = async () => {
    try {
      const [missions, d] = await Promise.all([api.combatMissions(), api.dashboard()])
      setCombatMissions(missions)
      setDashboard(d)
      setTickSeconds(d.secondsUntilNextTurnTick)
    } catch {
      // A dropped poll is not worth surfacing; the next tick or action will resync.
    }
  }

  useEffect(() => { void refresh() }, [])
  useEffect(() => {
    // Once per arrival, and never from refresh(): reading the digest advances the server's watermark,
    // so calling it after every action would consume the news before it could be shown.
    if (!dashboard || catchUpFetched.current) return
    catchUpFetched.current = true
    void api.catchUp().then(news => { if (news.hasNews) setCatchUp(news) }).catch(() => {})
  }, [dashboard?.playerId])
  useEffect(() => {
    if (activePage === 'admin' && !adminOverview)
      setActivePage('overview')
  }, [activePage, adminOverview])
  useEffect(() => {
    if (!dashboard || dashboard.turns >= dashboard.maxTurns) return
    const timer = window.setInterval(() => {
      setTickSeconds(s => {
        if (s <= 1) {
          void refresh()
          return dashboard.turnTickMinutes * 60
        }
        return s - 1
      })
    }, 1000)
    return () => window.clearInterval(timer)
  }, [dashboard?.playerId, dashboard?.turns, dashboard?.maxTurns, dashboard?.turnTickMinutes])
  useEffect(() => {
    if (!dashboard || !hasActiveMission) return
    let inFlight = false
    const timer = window.setInterval(() => {
      // Skip a tick rather than stacking requests when a poll outlives the interval.
      if (inFlight) return
      inFlight = true
      void pollMissions().finally(() => { inFlight = false })
    }, 5000)
    return () => window.clearInterval(timer)
  }, [dashboard?.playerId, hasActiveMission])
  useEffect(() => {
    // The narrow poll leaves combat history and world news behind, so resync once on the way out.
    if (hadActiveMission.current && !hasActiveMission) void refresh()
    hadActiveMission.current = hasActiveMission
  }, [hasActiveMission])
  useEffect(() => {
    // A hideout build lands server-side on the next refresh. Without this the new caps wait for the
    // turn tick, so a 30 minute build can read as finished for another ten.
    const readyAt = dashboard?.hideout.building?.completesAtUtc
    if (!readyAt) return
    const timer = window.setTimeout(() => void refresh(), Math.max(1000, new Date(readyAt).getTime() - Date.now() + 1000))
    return () => window.clearTimeout(timer)
  }, [dashboard?.hideout.building?.completesAtUtc])

  const nextTurn = useMemo(() => {
    if (!dashboard || dashboard.turns >= dashboard.maxTurns) return 'MAX'
    const m = Math.floor(tickSeconds / 60)
    const s = tickSeconds % 60
    return `${m}:${String(s).padStart(2, '0')}`
  }, [dashboard, tickSeconds])

  const auth = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true); setError('')
    try {
      if (authMode === 'register')
        await api.register(String(form.get('username')), String(form.get('password')), String(form.get('playerName')), String(form.get('city')))
      else
        await api.login(String(form.get('username')), String(form.get('password')))
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const act = async (fn: () => Promise<ActionResult | unknown>) => {
    setBusy(true); setError(''); setNotice(''); setLastBreakdown(null)
    try {
      const result = await fn() as ActionResult | undefined
      if (result?.summary) setNotice(result.summary)
      if (result?.breakdown) setLastBreakdown(result.breakdown)
      await refresh()
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const searchTargets = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true); setError('')
    try {
      setTargets(await api.targets(targetQuery))
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const inspectTarget = async (playerId: string) => {
    setBusy(true); setError('')
    try {
      setSelectedTarget(await api.playerProfile(playerId))
    } catch (e) { setError((e as Error).message) }
    finally { setBusy(false) }
  }

  const attackTarget = async (defenderId: string) => {
    await act(() => api.attack(defenderId, attackCrew.thugs, attackCrew.weapons, commanderId))
    try {
      setSelectedTarget(await api.playerProfile(defenderId))
    } catch {
      // The action result already refreshed the main screen; this only keeps the inspected card current.
    }
  }

  if (!dashboard) {
    return <main className="auth-shell">
      <section className="auth-card panel">
        <div className="brand-mark">SE</div>
        <h1>Street Empire</h1>
        <p className="muted">Old-school browser strategy, rebuilt.</p>
        <div className="tabs">
          <button className={authMode === 'login' ? 'active' : ''} onClick={() => setAuthMode('login')}>Login</button>
          <button className={authMode === 'register' ? 'active' : ''} onClick={() => setAuthMode('register')}>Create Account</button>
        </div>
        <form onSubmit={auth}>
          <label>Username<input name="username" minLength={3} maxLength={32} required /></label>
          {authMode === 'register' && <label>Player Name<input name="playerName" minLength={3} maxLength={32} required /></label>}
        {authMode === 'register' && <label>
          Town
          <select name="city" defaultValue={cities[0] ?? ''}>
            {cities.map(city => <option key={city} value={city}>{city}</option>)}
          </select>
          <small>Ground is fought over inside a town. This is the map you will be playing on.</small>
        </label>}
          <label>Password<input name="password" type="password" minLength={8} required /></label>
          {error && <DismissibleMessage className="error" onClose={() => setError('')}>{error}</DismissibleMessage>}
          <button className="primary" disabled={busy}>{busy ? 'Working...' : authMode === 'login' ? 'Enter the City' : 'Build My Empire'}</button>
        </form>
      </section>
    </main>
  }

  const totalCrew = dashboard.pimps + dashboard.hoes + dashboard.thugs
  const weaponCoverage = dashboard.thugs === 0 ? 100 : Math.min(100, (dashboard.weapons / dashboard.thugs) * 100)
  const managementCapacity = dashboard.crewReport.managementCapacity
  const visiblePages = (Object.keys(pageMeta) as AppPage[]).filter(page => page !== 'admin' || adminOverview)
  const contentContext: PageContext = {
    dashboard,
    adminOverview,
    leaders,
    targets,
    selectedTarget,
    worldNews,
    combatLogs,
    combatMissions,
    targetQuery,
    busy,
    streetTurns,
    autoBuySupplies,
    productionTurns,
    hoeCut,
    bankAmount,
    crewQty,
    attackCrew,
    commanderId,
    storeQty,
    sellQty,
    nextTurn,
    totalCrew,
    weaponCoverage,
    managementCapacity,
    setActivePage,
    setTargetQuery,
    setStreetTurns,
    setAutoBuySupplies,
    setProductionTurns,
    setHoeCut,
    setBankAmount,
    setCrewQty,
    setAttackCrew,
    setCommanderId,
    setStoreQty,
    setSellQty,
    act,
    searchTargets,
    inspectTarget,
    attackTarget: defenderId => void attackTarget(defenderId),
    cancelMission: missionId => void act(() => api.cancelCombatMission(missionId)),
    seedBots: count => void act(() => api.adminSeedBots(count)),
    runBots: rounds => void act(() => api.adminRunBots(rounds)),
    setBotAutomation: (enabled, timing) => void act(() => api.adminSetBotAutomation(enabled, timing)),
  }

  return <main className="game-shell">
    {catchUp && <CatchUpDialog news={catchUp} onClose={() => setCatchUp(null)} />}
    <aside className="app-nav">
      <div className="nav-brand"><span>SE</span><strong>Street Empire</strong><small>0.2.5</small></div>
      <nav>
        {visiblePages.map(page => <button
          className={activePage === page ? 'active' : ''}
          key={page}
          type="button"
          onClick={() => setActivePage(page)}
        >
          <span>{pageMeta[page].short}</span>
          <strong>{pageMeta[page].label}</strong>
        </button>)}
      </nav>
      <button className="logout-link" onClick={() => void act(api.logout)}>Logout</button>
    </aside>

    <section className="app-main">
      <header className="command-header">
        <div>
          <span>{pageMeta[activePage].kicker}</span>
          <h1>{pageMeta[activePage].label}</h1>
        </div>
        <div className="header-right">
          <AlertBell unread={dashboard.unreadDefenceAlerts} onRead={() => void refresh()} />
          <div className="player-plate">
            <strong>{dashboard.name}</strong>
            <span>{dashboard.city} / Rank #{dashboard.rank}</span>
          </div>
        </div>
      </header>

      <StatusStrip dashboard={dashboard} nextTurn={nextTurn} />

      <section className="alerts">
        {error && <DismissibleMessage className="error banner" onClose={() => setError('')}>{error}</DismissibleMessage>}
        {notice && <DismissibleMessage className="notice banner" onClose={() => setNotice('')}>{notice}</DismissibleMessage>}
        {lastBreakdown && <div className="breakdown banner notification">
          <div className="breakdown-items">
            {Object.entries(lastBreakdown).filter(([, value]) => value !== 0 && value !== null).slice(0, 18).map(([key, value]) =>
              <span key={key}><strong>{formatBreakdownKey(key)}</strong>{formatBreakdownValue(key, value)}</span>
            )}
          </div>
          <button className="dismiss" type="button" aria-label="Close breakdown" onClick={() => setLastBreakdown(null)}>x</button>
        </div>}
      </section>

      {renderPage(activePage, contentContext)}
    </section>
  </main>
}

type PageContext = {
  dashboard: Dashboard
  adminOverview: AdminOverview | null
  leaders: LeaderboardEntry[]
  targets: PlayerTarget[]
  selectedTarget: PlayerProfile | null
  worldNews: WorldNews
  combatLogs: CombatLog[]
  combatMissions: CombatMission[]
  targetQuery: string
  busy: boolean
  streetTurns: number
  autoBuySupplies: boolean
  productionTurns: number
  hoeCut: number
  bankAmount: number
  crewQty: Record<'pimps' | 'hoes' | 'thugs', number>
  attackCrew: { thugs: number, weapons: number }
  commanderId: number | null
  storeQty: Record<string, number>
  sellQty: Record<'weed' | 'coke', number>
  nextTurn: string
  totalCrew: number
  weaponCoverage: number
  managementCapacity: number
  setActivePage: (page: AppPage) => void
  setTargetQuery: (query: string) => void
  setStreetTurns: (turns: number) => void
  setAutoBuySupplies: (enabled: boolean) => void
  setProductionTurns: (turns: number) => void
  setHoeCut: (cut: number) => void
  setBankAmount: (amount: number) => void
  setCrewQty: React.Dispatch<React.SetStateAction<Record<'pimps' | 'hoes' | 'thugs', number>>>
  setAttackCrew: React.Dispatch<React.SetStateAction<{ thugs: number, weapons: number }>>
  setCommanderId: (id: number | null) => void
  setStoreQty: React.Dispatch<React.SetStateAction<Record<string, number>>>
  setSellQty: React.Dispatch<React.SetStateAction<Record<'weed' | 'coke', number>>>
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  searchTargets: (event: FormEvent<HTMLFormElement>) => void
  inspectTarget: (playerId: string) => void
  attackTarget: (defenderId: string) => void
  cancelMission: (missionId: number) => void
  seedBots: (count: number) => void
  runBots: (rounds: number) => void
  setBotAutomation: (enabled: boolean, timing?: { tickSeconds?: number, roundsPerTick?: number, resetTiming?: boolean }) => void
}

function renderPage(page: AppPage, ctx: PageContext) {
  switch (page) {
    case 'street': return <StreetPage {...ctx} />
    case 'crew': return <CrewPage {...ctx} />
    case 'hideout': return <HideoutPage {...ctx} />
    case 'territory': return <TerritoryPage {...ctx} />
    case 'market': return <MarketPage {...ctx} />
    case 'mules': return <MulePage {...ctx} />
    case 'recon': return <ReconPage {...ctx} />
    case 'admin': return ctx.adminOverview
      ? <AdminPage {...ctx} overview={ctx.adminOverview} />
      : <OverviewPage {...ctx} />
    default: return <OverviewPage {...ctx} />
  }
}

function OverviewPage(ctx: PageContext) {
  const { dashboard, leaders, worldNews, totalCrew, weaponCoverage, managementCapacity, busy, act, setActivePage } = ctx
  return <div className="overview-layout">
    <div className="overview-stack">
      <section className="panel hero-panel">
        <span className="eyebrow">Empire Snapshot</span>
        <h2>{dashboard.name}</h2>
        <div className="hero-metrics">
          <AdminMetric label="Net worth" value={money.format(dashboard.netWorth)} />
          <AdminMetric label="Crew" value={number.format(totalCrew)} />
          <AdminMetric label="Turns" value={`${dashboard.turns}/${dashboard.maxTurns}`} />
        </div>
        <div className="quick-actions">
          <button className="primary" onClick={() => setActivePage('street')}>Work Streets</button>
          <button className="secondary" onClick={() => setActivePage('crew')}>Manage Crew</button>
          <button className="secondary" onClick={() => setActivePage('market')}>Open Market</button>
          <button className="secondary" onClick={() => setActivePage('recon')}>Combat</button>
        </div>
      </section>

      <NextMovePanel dashboard={dashboard} onPage={setActivePage} />
      <OpeningLadderPanel dashboard={dashboard} onPage={setActivePage} />

      <TravelPanel markets={dashboard.cityMarkets} turns={dashboard.turns} travel={dashboard.travel} busy={busy} act={act} />
    </div>

    <div className="overview-stack">
      <section className="panel">
        <div className="panel-title"><h2>Readiness</h2><span>Combat prep</span></div>
        <StatusRow
          label="Hoe morale"
          value={`${dashboard.hoeHappiness.toFixed(0)}%`}
          warn={dashboard.hoeHappiness < 40}
          trend={<MoraleArrow trend={dashboard.moraleTrend} crew="hoe" />}
        />
        <StatusRow
          label="Thug morale"
          value={`${dashboard.thugHappiness.toFixed(0)}%`}
          warn={dashboard.thugHappiness < 40}
          trend={<MoraleArrow trend={dashboard.moraleTrend} crew="thug" />}
        />
        <StatusRow label="Management" value={`${dashboard.hoes}/${managementCapacity} hoes`} warn={dashboard.hoes > managementCapacity} />
        <StatusRow label="Armed thugs" value={`${Math.min(dashboard.weapons, dashboard.thugs)}/${dashboard.thugs}`} warn={dashboard.weapons < dashboard.thugs} />
        <StatusRow label="Weapon coverage" value={`${weaponCoverage.toFixed(0)}%`} warn={weaponCoverage < 75} />
        <StatusRow label="Combat status" value={dashboard.combatStatus.eligibility} warn={dashboard.combatStatus.isProtected} />
        <StatusRow label="20-turn condoms" value={`${dashboard.condoms}/${dashboard.crewReport.condomsNeededForMaxStreetAction}`} warn={dashboard.condoms < dashboard.crewReport.condomsNeededForMaxStreetAction} />
        <StatusRow label="20-turn beer" value={`${dashboard.beer}/${dashboard.crewReport.beerNeededForMaxStreetAction}`} warn={dashboard.beer < dashboard.crewReport.beerNeededForMaxStreetAction} />
      </section>

      {/* Directly under readiness, because the last two readiness rows are counts of these same piles. */}
      <section className="panel">
        <div className="panel-title"><h2>Inventory</h2><span>On hand</span></div>
        <MiniInventory dashboard={dashboard} />
      </section>

      <section className="panel">
        <div className="panel-title"><h2>Top Players</h2><span>Net worth</span></div>
        <Leaderboard leaders={leaders.slice(0, 8)} currentPlayer={dashboard.name} />
      </section>
    </div>

    <WorldNewsPanel news={worldNews} currentPlayer={dashboard.name} />
  </div>
}

function StreetPage(ctx: PageContext) {
  const { dashboard, combatMissions, busy, streetTurns, autoBuySupplies, hoeCut, bankAmount, storeQty, setActivePage, setStreetTurns, setAutoBuySupplies, setHoeCut, setBankAmount, setStoreQty, act } = ctx
  const pendingOutgoingAttack = combatMissions.find(mission => mission.attackerId === dashboard.playerId && mission.status !== 'Complete')
  const restock = restockEstimate(dashboard, streetTurns)
  return <div className="page-grid two-column">
    <section className="panel wide-panel">
      <div className="panel-title"><h2>Work the Streets</h2><span>Income + recruiting</span></div>
      <p>Your hoes generate gross income. Their cut is paid before your cash is deposited on hand. Street work can also recruit crew and turn up small amounts of inventory.</p>
      {pendingOutgoingAttack && <div className="mission-lock">
        <strong>Crew is out</strong>
        <span>Street work unlocks after the next mission update in {timeUntil(nextMissionTime(pendingOutgoingAttack))}.</span>
      </div>}
      <StorageSupplyNotice dashboard={dashboard} />
      <StreetSupplyPanel
        dashboard={dashboard}
        busy={busy}
        streetTurns={streetTurns}
        storeQty={storeQty}
        setStoreQty={setStoreQty}
        act={act}
        onMarket={() => setActivePage('market')}
      />
      <div className="action-row wrap">
        <label>Turns<input type="number" min={1} max={dashboard.maxActionTurns} value={streetTurns} onChange={e => setStreetTurns(Number(e.target.value))} /></label>
        <label>Hoe Cut %<input type="number" min={10} max={80} value={hoeCut} onChange={e => setHoeCut(Number(e.target.value))} /></label>
        <button className="secondary" disabled={busy || hoeCut < 10 || hoeCut > 80 || hoeCut === dashboard.hoeCutPercent} onClick={() => void act(() => api.setHoeCut(hoeCut))}>Save Cut</button>
        <button className="primary" disabled={busy || !!pendingOutgoingAttack || streetTurns < 1 || streetTurns > dashboard.turns || streetTurns > dashboard.maxActionTurns} onClick={() => void act(() => api.workStreet(streetTurns, autoBuySupplies))}>{pendingOutgoingAttack ? 'Crew Out' : `Work ${streetTurns} Turn${streetTurns === 1 ? '' : 's'}`}</button>
      </div>
      <label className={autoBuySupplies ? 'auto-buy active' : 'auto-buy'}>
        <input type="checkbox" checked={autoBuySupplies} onChange={event => setAutoBuySupplies(event.target.checked)} />
        <span>
          <strong>Auto-buy upkeep before working</strong>
          <small>{restockLabel(restock, dashboard.cash)}</small>
        </span>
      </label>
      <div className="rule-strip">
        <span>1 pimp manages 10 hoes</span><span>Condoms support hoes</span><span>Beer + weapons support thugs</span>
      </div>
    </section>

    <BankPanel dashboard={dashboard} busy={busy} bankAmount={bankAmount} setBankAmount={setBankAmount} act={act} />

    <section className="panel">
      <div className="panel-title"><h2>Activity</h2><span>Last 12 actions</span></div>
      <ActivityList entries={dashboard.recentActivity} />
    </section>
  </div>
}

function CrewPage(ctx: PageContext) {
  const { dashboard, busy, crewQty, totalCrew, weaponCoverage, managementCapacity, setCrewQty, act } = ctx
  const combatCrew = dashboard.combatCrew
  return <div className="page-grid">
    <section className="panel wide-panel">
      <div className="panel-title"><h2>Your Crew</h2><span>{number.format(totalCrew)} total</span></div>
      <StorageSupplyNotice dashboard={dashboard} />
      <div className="crew-grid">
        <CrewCard name="Pimps" count={dashboard.pimps} cap={dashboard.hideout.maxPimps} desc={`Manage up to ${number.format(managementCapacity)} hoes.`} />
        <CrewCard name="Hoes" count={dashboard.hoes} cap={dashboard.hideout.maxHoes} desc={`${dashboard.hoeHappiness.toFixed(0)}% morale / ${dashboard.hoeCutPercent}% cut`} tone={moraleTone(dashboard.hoeHappiness)} trend={<MoraleArrow trend={dashboard.moraleTrend} crew="hoe" />} />
        <CrewCard name="Thugs" count={dashboard.thugs} cap={dashboard.hideout.maxThugs} desc={`${dashboard.thugHappiness.toFixed(0)}% morale / ${weaponCoverage.toFixed(0)}% armed`} tone={moraleTone(dashboard.thugHappiness)} trend={<MoraleArrow trend={dashboard.moraleTrend} crew="thug" />} />
      </div>
      <div className="crew-combat-strip">
        <AdminMetric label="Free pimps" value={number.format(combatCrew.availablePimps)} />
        <AdminMetric label="Free thugs" value={number.format(combatCrew.availableThugs)} />
        <AdminMetric label="Free weapons" value={number.format(combatCrew.availableWeapons)} />
        <AdminMetric label="Committed" value={`${number.format(combatCrew.committedPimps)} P / ${number.format(combatCrew.committedThugs)} T / ${number.format(combatCrew.committedWeapons)} W`} />
        <AdminMetric label="Attack slots" value={`${combatCrew.activeAttackMissions}/${combatCrew.maxActiveAttackMissions}`} />
      </div>
    </section>

    <section className="panel wide-panel">
      <div className="panel-title"><h2>Crew Management</h2><span>Hire + fire</span></div>
      <div className="crew-manage-list">
        <CrewManageRow
          label="Pimps"
          owned={dashboard.pimps}
          quantity={crewQty.pimps}
          hireCost={dashboard.crewReport.hirePimpCost}
          cash={dashboard.cash}
          busy={busy}
          canFire={dashboard.pimps - crewQty.pimps >= 1}
          onQuantity={quantity => setCrewQty(value => ({ ...value, pimps: quantity }))}
          onHire={() => void act(() => api.hireCrew('pimps', crewQty.pimps))}
          onFire={() => void act(() => api.fireCrew('pimps', crewQty.pimps))}
          note={`${number.format(managementCapacity)} hoe management capacity`}
        />
        <CrewManageRow
          label="Hoes"
          owned={dashboard.hoes}
          quantity={crewQty.hoes}
          hireCost={dashboard.crewReport.hireHoeCost}
          cash={dashboard.cash}
          busy={busy}
          canHire={dashboard.hoeHappiness >= dashboard.crewReport.minHoeMoraleToHire}
          canFire={dashboard.hoes >= crewQty.hoes}
          onQuantity={quantity => setCrewQty(value => ({ ...value, hoes: quantity }))}
          onHire={() => void act(() => api.hireCrew('hoes', crewQty.hoes))}
          onFire={() => void act(() => api.fireCrew('hoes', crewQty.hoes))}
          note={`${dashboard.hoeHappiness.toFixed(0)}% morale, ${dashboard.crewReport.minHoeMoraleToHire.toFixed(0)}% needed to hire`}
        />
        <CrewManageRow
          label="Thugs"
          owned={dashboard.thugs}
          quantity={crewQty.thugs}
          hireCost={dashboard.crewReport.hireThugCost}
          cash={dashboard.cash}
          busy={busy}
          canHire={dashboard.thugHappiness >= dashboard.crewReport.minThugMoraleToHire}
          canFire={dashboard.thugs >= crewQty.thugs}
          onQuantity={quantity => setCrewQty(value => ({ ...value, thugs: quantity }))}
          onHire={() => void act(() => api.hireCrew('thugs', crewQty.thugs))}
          onFire={() => void act(() => api.fireCrew('thugs', crewQty.thugs))}
          note={`${dashboard.crewReport.armedThugs}/${dashboard.thugs} armed`}
        />
      </div>
    </section>

    <PimpRosterPanel dashboard={dashboard} />
  </div>
}

function HideoutPage(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const hideout = dashboard.hideout
  return <div className="page-grid two-column">
    <section className="panel wide-panel">
      <div className="panel-title"><h2>{hideout.tierName}</h2><span>Tier {hideout.tier}</span></div>
      <p>Your hideout sets every hard limit you operate under. Crew beyond its capacity walks away, goods beyond your storage room spill, and cash beyond your safe is swept into the bank.</p>
      <div className="capacity-grid">
        <CapacityBar label="Pimps" used={dashboard.pimps} cap={hideout.maxPimps} />
        <CapacityBar label="Hoes" used={dashboard.hoes} cap={hideout.maxHoes} />
        <CapacityBar label="Thugs" used={dashboard.thugs} cap={hideout.maxThugs} />
        <CapacityBar label="Cash on hand" used={dashboard.cash} cap={hideout.maxCash} money />
        <CapacityBar label="Condoms" used={dashboard.condoms} cap={hideout.maxCondoms} />
        <CapacityBar label="Beer" used={dashboard.beer} cap={hideout.maxBeer} />
        <CapacityBar label="Weapons" used={dashboard.weapons} cap={hideout.maxWeapons} />
        <CapacityBar label="Weed" used={dashboard.weed} cap={hideout.maxWeed} />
        <CapacityBar label="Coke" used={dashboard.coke} cap={hideout.maxCoke} />
        <CapacityBar label="Moonshine" used={dashboard.moonshine} cap={hideout.maxMoonshine} />
        <CapacityBar label="Cut" used={dashboard.cut} cap={hideout.maxCut} />
      </div>
    </section>

    <HideoutTierPanel dashboard={dashboard} busy={busy} act={act} />

    <section className="panel wide-panel">
      <div className="panel-title"><h2>Rooms</h2><span>Paid from the bank first</span></div>
      <div className="room-list">
        <RoomRow
          name="Storage Room"
          level={hideout.storageLevel}
          detail={`Holds ${number.format(hideout.maxCondoms)} condoms, ${number.format(hideout.maxBeer)} beer, ${number.format(hideout.maxWeapons)} weapons, ${number.format(hideout.maxWeed)} weed, ${number.format(hideout.maxCoke)} coke`}
          upgrade={hideout.storageUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('storage'))}
        />
        <RoomRow
          name="Safe"
          level={hideout.safeLevel}
          detail={`Holds ${money.format(hideout.maxCash)} cash on hand`}
          upgrade={hideout.safeUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('safe'))}
        />
        <RoomRow
          name="Weed Lab"
          level={hideout.weedLabLevel}
          detail={hideout.weedLabLevel === 0
            ? 'Not built. Raises weed production turns and makes weed on its own.'
            : `+${hideout.weedLabYieldBonusPercent}% per production turn, and ${number.format(hideout.weedLabPassivePerHour)} weed an hour on its own`}
          upgrade={hideout.weedLabUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('weedlab'))}
        />
        <RoomRow
          name="Coke Lab"
          level={hideout.cokeLabLevel}
          detail={hideout.cokeLabLevel === 0
            ? 'Not built. Raises coke production turns and makes coke on its own.'
            : `+${hideout.cokeLabYieldBonusPercent}% per production turn, and ${number.format(hideout.cokeLabPassivePerHour)} coke an hour on its own`}
          upgrade={hideout.cokeLabUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('cokelab'))}
        />
        <RoomRow
          name="Intelligence Centre"
          level={hideout.intelligenceLevel}
          detail={hideout.intelligenceLevel === 0
            ? 'Not built. Makes nothing. Lets you run mules out of town, and knows the routes they take.'
            : `${hideout.concurrentRunCap} mule run(s) out at once, on routes you already know`}
          upgrade={hideout.intelligenceUpgrade}
          funds={dashboard.cash + dashboard.bankCash}
          busy={busy}
          onUpgrade={() => void act(() => api.upgradeHideout('intelligence'))}
        />
      </div>
      {(hideout.weedLabLevel > 0 || hideout.cokeLabLevel > 0) && <p className="hint">
        Labs keep running while you are away, up to {hideout.maxOfflineProductionHours} hours of work at a time,
        and stop at whatever your storage room holds. What they make is contraband, so a full lab and a
        full store draw heat whether you are here or not.
      </p>}
    </section>

    <HideoutStationsPanel dashboard={dashboard} busy={busy} act={act} />

    <HideoutMoralePanel dashboard={dashboard} busy={busy} act={act} />
  </div>
}

/**
 * Mule runs: crew sent to another town to buy cheap and carry home.
 *
 * Built around the one number that decides whether a run is worth making - what a unit costs there
 * against what it fetches here - because every other figure on the page is a consequence of it.
 */
function MulePage(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const [board, setBoard] = useState<MuleBoard | null>(null)
  const [quote, setQuote] = useState<MuleQuote | null>(null)
  const [error, setError] = useState('')
  const [city, setCity] = useState('')
  const [good, setGood] = useState('weed')
  const [hoes, setHoes] = useState(3)
  const [cash, setCash] = useState(10000)
  const [pimpId, setPimpId] = useState<number | null>(null)

  const load = async () => {
    try {
      const next = await api.mules()
      setBoard(next)
      setError('')
      if (!city && next.destinations.length > 0) setCity(next.destinations[0].city)
      if (pimpId === null) setPimpId(next.pimps.find(p => !p.isAway)?.id ?? null)
    } catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.city, dashboard.turns, dashboard.hoes])

  // Re-quoted whenever the shape of the run changes, since every figure on the ticket moves with it.
  useEffect(() => {
    if (!city) return
    let stale = false
    void (async () => {
      try {
        const next = await api.muleQuote(city, good, hoes, cash)
        if (!stale) setQuote(next)
      } catch { if (!stale) setQuote(null) }
    })()
    return () => { stale = true }
  }, [city, good, hoes, cash])

  if (!board) return <div className="page-grid one-column"><section className="panel wide-panel">
    <div className="panel-title"><h2>Mules</h2><span>Loading</span></div>
    {error && <div className="error banner"><span>{error}</span></div>}
  </section></div>

  const free = board.pimps.filter(p => !p.isAway)
  const out = board.runs.filter(r => r.status !== 'Done')
  const home = board.runs.filter(r => r.status === 'Done')
  const spread = quote ? quote.homePrice - quote.unitPriceThere : 0
  const unspendable = quote ? quote.cashSent - quote.unitsAffordable * quote.unitPriceThere : 0

  const send = async () => {
    if (!pimpId) return
    await act(() => api.launchMule(city, good, hoes, cash, pimpId))
    await load()
  }

  return <div className="page-grid one-column">
    <section className="panel wide-panel">
      <div className="panel-title">
        <h2>Mule Runs</h2>
        <span>{board.runsOut} of {board.concurrentRunCap} out</span>
      </div>
      {error && <div className="error banner"><span>{error}</span></div>}
      <p>
        Send a pimp and hoes to another town to buy cheap and carry it home. Going yourself costs the
        distance in turns each way and leaves you standing in the wrong town. A run costs a fraction of
        that in turns, but it takes real time, the crew earn nothing while they are gone, and you pay
        their fares and keep before anybody leaves.
      </p>
      {board.concurrentRunCap === 0 && <div className="error banner">
        <span>You need an intelligence centre before you can run mules. Build one on the Hideout page.</span>
      </div>}
    </section>

    {board.concurrentRunCap > 0 && <section className="panel wide-panel">
      <div className="panel-title"><h2>Plan a run</h2><span>Prices are what they cost there</span></div>
      <div className="mule-form">
        <label>Town<select value={city} onChange={e => setCity(e.target.value)}>
          {board.destinations.map(d => <option key={d.city} value={d.city}>
            {d.city} - {d.flightMinutes}m each way, {d.risk.toLowerCase()} risk
          </option>)}
        </select></label>
        <label>Good<select value={good} onChange={e => setGood(e.target.value)}>
          <option value="weed">Weed</option>
          <option value="coke">Coke</option>
        </select></label>
        <label>Hoes<input
          type="number"
          min={1}
          max={Math.min(board.maxHoesPerRun, Math.max(1, board.hoesAvailable))}
          value={hoes}
          onChange={e => setHoes(Number(e.target.value))}
        /></label>
        <label>Cash to send<input
          type="number"
          min={0}
          step={1000}
          value={cash}
          onChange={e => setCash(Number(e.target.value))}
        /></label>
        <label>Led by<select value={pimpId ?? ''} onChange={e => setPimpId(Number(e.target.value))}>
          {free.length === 0 && <option value="">No pimp free</option>}
          {free.map(p => <option key={p.id} value={p.id}>{p.name} - {p.loyalty}% loyal</option>)}
        </select></label>
      </div>

      {quote && <div className="mule-ticket">
        <div className="mule-figures">
          <MuleFigure label="Buys there" value={`${money.format(quote.unitPriceThere)} each`} />
          <MuleFigure label="Sells here" value={`${money.format(quote.homePrice)} each`} tone={spread > 0 ? 'good' : 'bad'} />
          <MuleFigure label="They can carry" value={`${number.format(quote.capacity)} ${quote.good}`} />
          <MuleFigure label="Your money buys" value={`${number.format(quote.unitsAffordable)} ${quote.good}`} />
          <MuleFigure label="Turns" value={number.format(quote.turns)} />
          <MuleFigure label="Round trip" value={`${quote.tripMinutes} min`} />
          <MuleFigure label="Fares and keep" value={money.format(quote.fare + quote.upkeep)} />
          <MuleFigure label="Spent on goods" value={money.format(quote.projectedSpend)} />
          <MuleFigure
            label="Profit if clean"
            value={money.format(quote.projectedProfit)}
            tone={quote.projectedProfit > 0 ? 'good' : 'bad'}
          />
          <MuleFigure label="Caught" value={`${quote.bustChancePercent}%`} tone={quote.bustChancePercent >= 25 ? 'bad' : undefined} />
          <MuleFigure label="He runs" value={`${quote.defectChancePercent}%`} tone={quote.defectChancePercent > 0 ? 'bad' : undefined} />
        </div>
        {/* The spread alone does not decide it: fares and keep are paid whether or not the run pays. */}
        <p className={quote.projectedProfit > 0 ? 'mule-verdict good' : 'mule-verdict bad'}>
          {spread <= 0
            ? `${quote.good} is no cheaper in ${quote.destinationCity} than it is here. There is nothing to make on this route.`
            : quote.projectedProfit <= 0
              ? `A clean run still loses ${money.format(-quote.projectedProfit)}. The ${money.format(quote.fare + quote.upkeep)} in fares and keep is more than ${number.format(quote.unitsAffordable)} ${quote.good} makes at ${money.format(spread)} a unit. Send more hoes, or find a wider spread.`
              : `Clean, this run comes home ${money.format(quote.projectedProfit)} up: ${number.format(quote.unitsAffordable)} ${quote.good} worth ${money.format(quote.projectedGross)}, less ${money.format(quote.fare + quote.upkeep + quote.projectedSpend)} spent getting it.`}
        </p>
        {unspendable > 0 && <p className="hint">
          {money.format(unspendable)} of what you send cannot be spent: {quote.hoes} hoe(s) only carry {number.format(quote.capacity)}.
          It comes home with them, unless they are stopped, in which case it is taken too.
        </p>}
        <button
          className="primary"
          disabled={busy || !pimpId || board.runsOut >= board.concurrentRunCap || hoes > board.hoesAvailable}
          onClick={() => void send()}
        >
          Send {quote.hoes} hoe(s) to {quote.destinationCity}
        </button>
      </div>}
    </section>}

    {out.length > 0 && <section className="panel wide-panel">
      <div className="panel-title"><h2>In the air</h2><span>{out.length} out</span></div>
      <div className="room-list">
        {out.map(run => <div className="room-row" key={run.id}>
          <div className="room-copy">
            <strong>{run.pimpName} to {run.destinationCity}</strong>
            <span>
              {run.hoes} hoe(s) carrying up to {number.format(run.capacity)} {run.good}, {money.format(run.cashSent)} to buy with
            </span>
            <small>
              {run.status === 'Outbound' ? 'On the way out' : 'On the way back'} - {run.bustChancePercent}% caught, {run.defectChancePercent}% he runs
            </small>
          </div>
          <em>{run.secondsRemaining > 0 ? `${Math.ceil(run.secondsRemaining / 60)}m` : 'Landing'}</em>
        </div>)}
      </div>
    </section>}

    {home.length > 0 && <section className="panel wide-panel">
      <div className="panel-title"><h2>Recently home</h2><span>Last 12 hours</span></div>
      <div className="room-list">
        {home.map(run => <div className={run.outcome === 'Delivered' ? 'room-row' : 'room-row illegal'} key={run.id}>
          <div className="room-copy">
            <strong>{run.pimpName} from {run.destinationCity}</strong>
            <span>{run.summary}</span>
          </div>
          <em>{run.outcome}</em>
        </div>)}
      </div>
    </section>}
  </div>
}

function MuleFigure({ label, value, tone }: { label: string, value: string, tone?: 'good' | 'bad' }) {
  return <div className={tone ? `mule-figure ${tone}` : 'mule-figure'}>
    <span>{label}</span>
    <strong>{value}</strong>
  </div>
}

function CapacityBar({ label, used, cap, money: asMoney = false }: { label: string, used: number, cap: number, money?: boolean }) {
  const percent = cap <= 0 ? 0 : Math.min(100, (used / cap) * 100)
  const over = used > cap
  const format = (value: number) => asMoney ? money.format(value) : number.format(value)
  return <div className={over ? 'capacity over' : percent >= 90 ? 'capacity near' : 'capacity'}>
    <div className="capacity-head">
      <span>{label}</span>
      <strong>{format(used)} / {format(cap)}</strong>
    </div>
    <div className="capacity-track"><div className="capacity-fill" style={{ width: `${Math.max(2, percent)}%` }} /></div>
    {over && <small>Over capacity. You keep this, but cannot take on more until it drains.</small>}
  </div>
}

/**
 * The making stations. Each is shown next to the price it exists to beat, because a station whose
 * output costs more than the thing it replaces has no reason to be built.
 */
function HideoutStationsPanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const [turns, setTurns] = useState<Record<string, number>>({})
  const stations = dashboard.hideout.stations ?? []
  if (stations.length === 0) return null

  return <section className="panel wide-panel">
    <div className="panel-title"><h2>Production</h2><span>Turns and materials into goods</span></div>
    <p>
      What you make here is what the market has to trade. Each station is priced against the thing it
      replaces, so building one only pays while its output costs less than buying the same thing.
    </p>
    <div className="room-list">
      {stations.map(station => {
        const runTurns = turns[station.key] ?? 5
        const built = station.level > 0
        return <div className={station.heatPerUnit > 0 ? 'room-row illegal' : 'room-row'} key={station.key}>
          <div className="room-copy">
            <strong>{station.name}{station.heatPerUnit > 0 && <small> contraband</small>}</strong>
            <span>
              {built
                ? `${number.format(station.perTurn)} ${station.good} a turn at ${money.format(station.costPerUnit)} each, against ${money.format(station.comparePrice)} for ${station.compareLabel}`
                : `Not built. Makes ${station.good} for less than ${money.format(station.comparePrice)}, the price of ${station.compareLabel}.`}
            </span>
            {station.heatPerUnit > 0 && built && <small>
              Each one held adds {station.heatPerUnit} heat. Make and sell rather than stockpile.
            </small>}
            {station.upgrade?.tierLocked && <small>Level {station.upgrade.level} needs the {station.upgrade.requiredTierName} or better.</small>}
          </div>
          <em>{built ? `Level ${station.level}` : 'Not built'}</em>
          <div className="territory-actions">
            {built && <>
              <label>Turns<input
                type="number"
                min={1}
                max={dashboard.maxActionTurns}
                value={runTurns}
                onChange={e => setTurns(v => ({ ...v, [station.key]: Number(e.target.value) }))}
              /></label>
              <button
                className="primary compact"
                disabled={busy || runTurns < 1 || runTurns > dashboard.turns}
                onClick={() => void act(() => api.forge(runTurns, station.key))}
              >
                Make {number.format(station.perTurn * runTurns)}
              </button>
            </>}
            <button
              className="secondary compact"
              disabled={busy || !station.upgrade || station.upgrade.tierLocked || dashboard.cash + dashboard.bankCash < station.upgrade.cost}
              onClick={() => void act(() => api.upgradeHideout(station.key as HideoutRoom))}
            >
              {!station.upgrade ? 'Maxed' : built ? `Upgrade ${money.format(station.upgrade.cost)}` : `Build ${money.format(station.upgrade.cost)}`}
            </button>
          </div>
        </div>
      })}
    </div>

    <CutCokePanel dashboard={dashboard} busy={busy} act={act} />
  </section>
}

/**
 * Stepping on the coke you already hold.
 *
 * Sits under the stations rather than inside the mix house row because it is not a station: the mix
 * house makes cut, and this spends it. The coke worth stretching is usually coke that was never
 * produced here at all - off a plane, off the board, out of a lab overnight.
 */
function CutCokePanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const hideout = dashboard.hideout
  const mix = hideout.stations?.find(s => s.key === 'mix')
  const [turns, setTurns] = useState(5)
  if (!mix || mix.level === 0) return null

  const perTurn = 10 * mix.level
  const room = Math.max(0, hideout.maxCoke - dashboard.coke)
  // Every limit at once, the same way the server bounds it, so the button never promises a batch the
  // rules will refuse.
  const batch = Math.min(turns * perTurn, dashboard.cut, dashboard.coke, room)
  const turnsNeeded = Math.max(1, Math.ceil(batch / perTurn))
  // Mirrors the server: blending is a weighted average, and price follows the square root of purity.
  const purity = dashboard.cokePurityPercent / 100
  const blended = dashboard.coke + batch <= 0 ? 1 : (dashboard.coke * purity) / (dashboard.coke + batch)
  const afterPurity = Math.round(blended * 100)
  const list = dashboard.cokeSellPrice
  const afterPrice = Math.max(1, Math.round(list * Math.sqrt(blended)))
  const nowValue = dashboard.coke * dashboard.cokeSellPriceAtPurity
  const afterValue = (dashboard.coke + batch) * afterPrice
  const blocked = dashboard.cut <= 0
    ? 'You have no cut to work with.'
    : dashboard.coke <= 0
      ? 'You have no coke to step on.'
      : room <= 0
        ? 'Your store is full of coke already.'
        : null

  return <div className="cut-panel">
    <div className="cut-head">
      <strong>Step on it</strong>
      <span>
        {number.format(dashboard.cut)} cut, {number.format(dashboard.coke)} coke at {dashboard.cokePurityPercent}% pure,
        room for {number.format(room)} more
      </span>
    </div>
    <p>
      One unit of cut makes one unit of coke, on any coke you hold however it got here. What it costs
      is strength: the pile grows and weakens together, and buyers pay for what is actually in it. A
      stretch still pays, but each one pays less than the last, and a bigger pile of coke draws more
      notice than anything else you can hold.
    </p>
    {blocked
      ? <p className="hint">{blocked}</p>
      : <p className="hint">
          {number.format(batch)} coke from {number.format(batch)} cut, in {turnsNeeded} turn{turnsNeeded === 1 ? '' : 's'}.
          {' '}Purity {dashboard.cokePurityPercent}% to {afterPurity}%, so a unit drops from{' '}
          {money.format(dashboard.cokeSellPriceAtPurity)} to about {money.format(afterPrice)}.
          {batch < turns * perTurn && ' That is everything available.'}
        </p>}
    <p className={afterValue > nowValue ? 'mule-verdict good' : 'mule-verdict bad'}>
      {afterValue > nowValue
        ? `Worth ${money.format(afterValue - nowValue)} more in total: ${number.format(dashboard.coke + batch)} weaker units beat ${number.format(dashboard.coke)} clean ones, for now.`
        : 'This pile is already stretched too thin. More filler is worth less than the room it takes.'}
    </p>
    <div className="territory-actions">
      <label>Turns<input
        type="number"
        min={1}
        max={dashboard.maxActionTurns}
        value={turns}
        onChange={e => setTurns(Number(e.target.value))}
      /></label>
      <button
        className="primary compact"
        disabled={busy || blocked !== null || batch <= 0 || turnsNeeded > dashboard.turns}
        onClick={() => void act(() => api.cutCoke(turns))}
      >
        Cut {number.format(batch)} coke
      </button>
    </div>
  </div>
}

function HideoutTierPanel({ dashboard, busy, act }: { dashboard: Dashboard, busy: boolean, act: PageContext['act'] }) {
  const hideout = dashboard.hideout
  const building = hideout.building
  const next = hideout.nextTier
  // Cash and bank together, matching what the server charges. Checking cash on hand alone greyed the
  // button out for exactly the players who could afford it, since a tier costs more than any safe below
  // it holds and the rest of their money is necessarily in the bank.
  const canAffordTier = !next || dashboard.cash + dashboard.bankCash >= next.cost

  // The panel keeps its own second hand. The app-wide one stops once turns are maxed, which would
  // otherwise freeze the countdown for exactly the players most likely to be building something.
  const [, setNow] = useState(0)
  useEffect(() => {
    if (!building) return
    const timer = window.setInterval(() => setNow(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [building?.completesAtUtc])

  return <section className="panel wide-panel">
    <div className="panel-title"><h2>The Building</h2><span>Crew capacity</span></div>
    {building
      ? <div className="build-progress">
        <strong>Building the {building.name}</strong>
        <span>Ready in {timeUntil(building.completesAtUtc)}. Your crew caps stay where they are until it lands.</span>
      </div>
      : next
        ? <>
          <p>
            Moving up to the <strong>{next.name}</strong> raises your crew caps to {number.format(next.maxPimps)} pimps,{' '}
            {number.format(next.maxHoes)} hoes, and {number.format(next.maxThugs)} thugs, and unlocks the rooms your
            current building is too small to hold.
          </p>
          <div className="room-row">
            <div className="room-copy">
              <strong>{next.name}</strong>
              <span>
                {money.format(next.cost)} and {next.turns} turns. Takes {next.buildMinutes} minutes to build.
                Paid from the bank first, then cash on hand.
              </span>
            </div>
            <em>Tier {next.level}</em>
            <button
              className="primary"
              disabled={busy || !canAffordTier || dashboard.turns < next.turns}
              onClick={() => void act(() => api.upgradeHideout('tier'))}
            >
              {!canAffordTier ? 'Not enough money' : dashboard.turns < next.turns ? 'Not enough turns' : 'Start building'}
            </button>
          </div>
        </>
        : <p>The {hideout.tierName} is the biggest building there is. Nothing left to move up to.</p>}
  </section>
}

// funds, not cash on hand: the server pays for a room from the bank first, because the safe is one of
// the things being bought and several rooms cost more than the safe below them holds.
function RoomRow({ name, level, detail, upgrade, funds, busy, onUpgrade }: {
  name: string
  level: number
  detail: string
  upgrade?: HideoutRoomUpgrade | null
  funds: number
  busy: boolean
  onUpgrade: () => void
}) {
  const locked = upgrade?.tierLocked ?? false
  return <div className="room-row">
    <div className="room-copy">
      <strong>{name}</strong>
      <span>{detail}</span>
      {locked && <small>Level {upgrade!.level} needs the {upgrade!.requiredTierName} or better.</small>}
    </div>
    <em>{level === 0 ? 'Not built' : `Level ${level}`}</em>
    <button className="primary" disabled={busy || !upgrade || locked || funds < upgrade.cost} onClick={onUpgrade}>
      {!upgrade ? 'Maxed' : locked ? 'Locked' : `Upgrade ${money.format(upgrade.cost)}`}
    </button>
  </div>
}

/**
 * The map. Ground is held with thugs who count as away from home, so what this page really shows is
 * how much of your crew is standing somewhere else.
 */
function TerritoryPage(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const [board, setBoard] = useState<TerritoryBoard | null>(null)
  const [error, setError] = useState('')
  const [thugs, setThugs] = useState<Record<number, number>>({})
  const [pimpFor, setPimpFor] = useState<Record<number, number | null>>({})

  const load = async () => {
    try { setBoard(await api.territories()); setError('') }
    catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.thugs, dashboard.turns])

  const run = async (fn: () => Promise<unknown>) => {
    await act(fn)
    await load()
  }

  if (!board) return <div className="page-grid one-column"><section className="panel wide-panel">
    <div className="panel-title"><h2>Territory</h2><span>Loading</span></div>
    {error && <div className="error banner"><span>{error}</span></div>}
  </section></div>

  const effects = board.effects
  const anyEffect = effects.streetIncomePercent || effects.productionYieldPercent || effects.moraleRecoveryPercent || effects.lootPercent
  const force = (id: number) => thugs[id] ?? board.minimumGarrison
  const chosen = (id: number) => pimpFor[id] ?? null
  // Only pimps who are actually free. Anyone out commanding a raid, or already running other ground,
  // cannot take a second posting, and the server refuses it anyway.
  const freePimps = (id: number) => dashboard.crew.filter(p => !p.isCommanding
    && !board.territories.some(t => t.id !== id && t.heldByYou && t.garrisonPimpName === p.name))

  return <div className="page-grid one-column">
    <section className="panel wide-panel">
      <div className="panel-title">
        <h2>{board.city}</h2>
        <span>{board.held} of {board.holdingCap} held</span>
      </div>
      <p>
        This is {board.city}, and it is the only map you fight over.
        Holding ground takes {board.minimumGarrison} thugs standing on it, and they are not at home while they do.
        You have <strong>{number.format(board.freeThugs)}</strong> free of {number.format(dashboard.thugs)}.
        Claiming empty ground costs {board.claimTurnCost} turns; taking it off somebody costs a raid and one of your two lanes.
      </p>
      {anyEffect
        ? <div className="rule-strip">
          {effects.streetIncomePercent > 0 && <span>+{effects.streetIncomePercent}% street income</span>}
          {effects.productionYieldPercent > 0 && <span>+{effects.productionYieldPercent}% production</span>}
          {effects.moraleRecoveryPercent > 0 && <span>+{effects.moraleRecoveryPercent}% morale recovery</span>}
          {effects.lootPercent > 0 && <span>+{effects.lootPercent}% haul</span>}
        </div>
        : <p className="hint">You hold nothing yet, so none of your work is being amplified.</p>}
      {error && <div className="error banner"><span>{error}</span></div>}
    </section>

    <section className="panel wide-panel">
      <div className="panel-title"><h2>The Map</h2><span>{board.territories.length} pieces in {board.city}</span></div>
      <div className="territory-grid">
        {board.territories.map(t => <div className={`territory-card ${t.heldByYou ? 'mine' : t.holderId ? 'taken' : 'open'}`} key={t.id}>
          <div className="territory-head">
            <strong>{t.name}</strong>
            <em>{t.typeLabel}</em>
          </div>
          <span className="territory-effect">{t.effect}</span>
          <span className="territory-holder">
            {t.heldByYou ? `Yours, ${number.format(t.garrisonThugs)} thug(s) on it`
              : t.holderId ? `${t.holderName} holds it with ${number.format(t.garrisonThugs)} thug(s)`
                : 'Nobody holds this'}
            {' / '}{t.city}
          </span>
          {t.garrisonPimpName && <span className="territory-pimp-name">
            Run by {t.garrisonPimpName}{t.garrisonBonusPercent > 0 ? ` (+${t.garrisonBonusPercent}% defence)` : ''}
          </span>}
          {t.isProtected && t.protectedUntilUtc && <small>Settled for {timeUntil(t.protectedUntilUtc)}</small>}
          {t.blockedReason && <small>{t.blockedReason}</small>}
          <div className="territory-actions">
            <label>Thugs<input
              type="number"
              min={t.heldByYou ? 0 : board.minimumGarrison}
              value={force(t.id)}
              onChange={e => setThugs(v => ({ ...v, [t.id]: Number(e.target.value) }))}
            /></label>
            {(t.heldByYou || t.canClaim) && <label className="territory-pimp">Run by<select
              value={chosen(t.id) ?? ''}
              onChange={e => setPimpFor(v => ({ ...v, [t.id]: e.target.value ? Number(e.target.value) : null }))}
            >
              <option value="">Nobody</option>
              {freePimps(t.id).map(p => <option key={p.id} value={p.id}>
                {p.name} ({p.specialty}{p.specialty === 'Enforcer' ? ` +${p.bonusPercent}%` : ''})
              </option>)}
            </select></label>}
            {t.heldByYou && <>
              <button className="secondary compact" disabled={busy} onClick={() => void run(() => api.setGarrison(t.id, force(t.id), chosen(t.id)))}>Set garrison</button>
              <button className="secondary compact" disabled={busy} onClick={() => void run(() => api.setGarrison(t.id, 0, null))}>Give up</button>
            </>}
            {t.canClaim && <button className="primary compact" disabled={busy} onClick={() => void run(() => api.claimTerritory(t.id, force(t.id), chosen(t.id)))}>Claim</button>}
            {t.canRaid && <button className="primary compact" disabled={busy} onClick={() => void run(() => api.raidTerritory(t.id, force(t.id), force(t.id)))}>Raid it</button>}
          </div>
        </div>)}
      </div>
    </section>
  </div>
}

/**
 * The player-to-player board. It works because turns are scarcer than cash: somebody with turns and no
 * money makes weapons, somebody with money and no turns buys them rather than spending the turns.
 */
function TradingPanel(ctx: PageContext) {
  const { dashboard, busy, act } = ctx
  const [board, setBoard] = useState<MarketBoard | null>(null)
  const [error, setError] = useState('')
  const [item, setItem] = useState('weapons')
  const [qty, setQty] = useState(1)
  const [price, setPrice] = useState(0)
  const [buyQty, setBuyQty] = useState<Record<number, number>>({})

  const load = async () => {
    try { setBoard(await api.market()); setError('') }
    catch (e) { setError((e as Error).message) }
  }
  useEffect(() => { void load() }, [dashboard.weapons, dashboard.weed, dashboard.coke, dashboard.condoms, dashboard.beer])

  const run = async (fn: () => Promise<unknown>) => { await act(fn); await load() }
  const good = board?.goods.find(g => g.item === item)
  // Seeded from what the game itself pays, so the first listing is not a guess in the dark.
  useEffect(() => { if (good && price === 0) setPrice(good.referencePrice) }, [good?.item])

  if (!board) return <section className="panel wide-panel">
    <div className="panel-title"><h2>Market</h2><span>Loading</span></div>
    {error && <div className="error banner"><span>{error}</span></div>}
  </section>

  return <>
    <section className="panel wide-panel">
      <div className="panel-title">
        <h2>Market</h2>
        <span>{board.houseCutPercent}% to the house / {board.yourOpenListings} of {board.maxListingsPerPlayer} listings</span>
      </div>
      <p>
        Sell to other players instead of the game. Stock leaves your storage the moment you list it and
        comes back if you pull the listing. What the game pays is shown for reference, not as a limit.
      </p>
      {error && <div className="error banner"><span>{error}</span></div>}
      <div className="admin-action-row">
        <label>Good<select value={item} onChange={e => { setItem(e.target.value); setPrice(board.goods.find(g => g.item === e.target.value)?.referencePrice ?? 0) }}>
          {board.goods.map(g => <option key={g.item} value={g.item}>{g.label} ({number.format(g.held)} held)</option>)}
        </select></label>
        <label>Quantity<input type="number" min={1} max={good?.held ?? 1} value={qty} onChange={e => setQty(Number(e.target.value))} /></label>
        <label>Price each<input type="number" min={1} value={price} onChange={e => setPrice(Number(e.target.value))} /></label>
        <button
          className="primary compact"
          disabled={busy || !good || qty < 1 || qty > (good?.held ?? 0) || price < 1}
          onClick={() => void run(() => api.listOnMarket(item, qty, price))}
        >
          List for {money.format(qty * price)}
        </button>
      </div>
      {good && <p className="hint">
        The game pays {money.format(good.referencePrice)} for {good.label.toLowerCase()}.
        {good.bestPrice ? ` Cheapest on the board right now is ${money.format(good.bestPrice)}.` : ' Nothing listed yet.'}
        {' '}You hold {number.format(good.held)} with room for {number.format(good.room)} more.
      </p>}
    </section>

    <section className="panel wide-panel">
      <div className="panel-title"><h2>On the Board</h2><span>{board.listings.length} listings</span></div>
      {board.listings.length === 0 && <p className="coming">Nothing for sale. Be the first.</p>}
      {board.listings.length > 0 && <div className="admin-table-scroll"><table className="admin-table">
        <thead><tr><th>Good</th><th>Left</th><th>Each</th><th>vs game</th><th>Seller</th><th /></tr></thead>
        <tbody>
          {board.listings.map(l => <tr key={l.id} className={l.yours ? 'paused' : ''}>
            <td>{l.itemLabel}</td>
            <td>{number.format(l.quantity)} of {number.format(l.originalQuantity)}</td>
            <td>{money.format(l.pricePerUnit)}</td>
            <td>{l.referencePrice > 0 ? `${Math.round((l.pricePerUnit / l.referencePrice - 1) * 100)}%` : '-'}</td>
            <td>{l.yours ? 'You' : l.sellerName}</td>
            <td className="admin-table-actions">
              {l.yours
                ? <button className="secondary compact" disabled={busy} onClick={() => void run(() => api.cancelListing(l.id))}>Pull it</button>
                : <>
                  <input
                    type="number"
                    min={1}
                    max={l.quantity}
                    value={buyQty[l.id] ?? l.quantity}
                    onChange={e => setBuyQty(v => ({ ...v, [l.id]: Number(e.target.value) }))}
                  />
                  <button className="primary compact" disabled={busy} onClick={() => void run(() => api.buyOnMarket(l.id, buyQty[l.id] ?? l.quantity))}>Buy</button>
                </>}
            </td>
          </tr>)}
        </tbody>
      </table></div>}
    </section>
  </>
}

function MarketPage(ctx: PageContext) {
  const { dashboard, busy, productionTurns, bankAmount, storeQty, sellQty, setProductionTurns, setBankAmount, setStoreQty, setSellQty, act } = ctx
  return <div className="market-page">
    <section className="panel wide-panel">
      <div className="panel-title"><h2>Inventory</h2><span>{dashboard.city} prices, travel on Overview</span></div>
      <div className="inventory-grid">
        <InventoryCard name="Condoms" count={dashboard.condoms} note="Hoe upkeep" />
        <InventoryCard name="Beer" count={dashboard.beer} note="Thug upkeep" />
        <InventoryCard name="Weapons" count={dashboard.weapons} note="Permanent security" />
        <InventoryCard name="Weed" count={dashboard.weed} note={`${money.format(dashboard.weedSellPrice)} ${dashboard.currentMarket.weed.toLowerCase()}`} />
        {/* Quotes what this pile actually fetches, not the list price, since cut coke is not coke. */}
        <InventoryCard
          name="Coke"
          count={dashboard.coke}
          note={dashboard.cokePurityPercent >= 100
            ? `${money.format(dashboard.cokeSellPrice)} ${dashboard.currentMarket.coke.toLowerCase()}`
            : `${money.format(dashboard.cokeSellPriceAtPurity)} at ${dashboard.cokePurityPercent}% pure`}
        />
      </div>
    </section>

    <TradingPanel {...ctx} />

    <section className="panel market-store">
      <div className="panel-title"><h2>Street Store</h2><span>Cash on hand only</span></div>
      <div className="store-list">
        {dashboard.store.map(item => {
          const qty = storeQty[item.key] ?? 1
          return <div className="store-row" key={item.key}>
            <div className="store-copy">
              <div><strong>{item.name}</strong><span>{item.category}</span></div>
              <p>{item.description}</p>
            </div>
            <div className="store-purchase">
              <div className="store-price">
                <span>Unit</span>
                <strong>{money.format(item.price)}</strong>
              </div>
              <label>Qty<input aria-label={`${item.name} quantity`} type="number" min={1} max={10000} value={qty} onChange={e => setStoreQty(v => ({ ...v, [item.key]: Number(e.target.value) }))} /></label>
              <div className="store-total">
                <span>Total</span>
                <strong>{money.format(qty * item.price)}</strong>
              </div>
              <button className="primary compact" disabled={busy || qty < 1 || dashboard.cash < qty * item.price} onClick={() => void act(() => api.buyStoreItem(item.key, qty))}>Buy</button>
            </div>
          </div>
        })}
      </div>
    </section>

    <BankPanel dashboard={dashboard} busy={busy} bankAmount={bankAmount} setBankAmount={setBankAmount} act={act} className="market-bank" />

    <section className="panel market-production">
      <div className="panel-title"><h2>Production</h2><span>Spend turns, build product</span></div>
      <div className="production-command">
        <p>Turn cash-on-hand into inventory, then sell product at this city's street prices.</p>
        <label>Turns<input type="number" min={1} max={dashboard.maxActionTurns} value={productionTurns} onChange={e => setProductionTurns(Number(e.target.value))} /></label>
      </div>
      <div className="product-grid">
        <ProductTradeCard
          name="Weed"
          owned={dashboard.weed}
          price={dashboard.weedSellPrice}
          quantity={sellQty.weed}
          disabled={busy}
          canProduce={productionTurns >= 1 && productionTurns <= dashboard.turns && productionTurns <= dashboard.maxActionTurns}
          onProduce={() => void act(() => api.produce('weed', productionTurns))}
          onQuantity={q => setSellQty(v => ({ ...v, weed: q }))}
          onSell={() => void act(() => api.sellProduct('weed', sellQty.weed))}
        />
        <ProductTradeCard
          name="Coke"
          owned={dashboard.coke}
          price={dashboard.cokeSellPrice}
          quantity={sellQty.coke}
          disabled={busy}
          canProduce={productionTurns >= 1 && productionTurns <= dashboard.turns && productionTurns <= dashboard.maxActionTurns}
          onProduce={() => void act(() => api.produce('coke', productionTurns))}
          onQuantity={q => setSellQty(v => ({ ...v, coke: q }))}
          onSell={() => void act(() => api.sellProduct('coke', sellQty.coke))}
        />
      </div>
    </section>
  </div>
}

function TravelPanel({ markets, turns, travel, busy, act }: { markets: CityMarket[], turns: number, travel: TravelStatus, busy: boolean, act: PageContext['act'] }) {
  const here = markets.find(x => x.current)
  // The city you stand in leads the list: every other row's price is read as a move against it, so
  // the baseline has to sit where the eye starts rather than wherever the alphabet dropped it.
  const ordered = here ? [here, ...markets.filter(x => !x.current)] : markets
  return <section className="panel travel-panel">
    {/* The stake is on the title line because it is the number that decides whether to bank first. */}
    <div className="panel-title"><h2>Travel</h2><span>{turns.toLocaleString()} turns, carrying {money.format(travel.carriedValue)}</span></div>
    {travel.blockedReason !== null && <p className="travel-blocked">{travel.blockedReason}</p>}
    {travel.carriedValue > 0 && <p className="travel-note">A stop takes {travel.seizureMinPercent}–{travel.seizureMaxPercent}% of what you carry.</p>}
    <div className="city-market-list">
      <div className="city-market head">
        <span>City</span><span>Weed</span><span>Coke</span><span>Trip</span>
      </div>
      {ordered.map(city => {
        const shortfall = city.travelTurns - turns
        return <div className={city.current ? 'city-market current' : 'city-market'} key={city.city}>
          <div className="city-market-head">
            <strong>{city.city}</strong>
            <span>{city.current ? 'Current city' : riskLine(city, travel)}</span>
          </div>
          <CityPrice price={city.weedSellPrice} base={here?.weedSellPrice} showDelta={!city.current} />
          <CityPrice price={city.cokeSellPrice} base={here?.cokeSellPrice} showDelta={!city.current} />
          <div className="city-market-go">
            {city.current
              ? <span className="here">You are here</span>
              : <>
                <button
                  className="secondary compact"
                  disabled={busy || travel.blockedReason !== null || shortfall > 0}
                  onClick={() => void act(() => api.travel(city.city))}
                >
                  Travel
                </button>
                <small className={shortfall > 0 ? 'short' : undefined}>
                  {shortfall > 0 ? `need ${shortfall} more` : `${city.travelTurns} turns`}
                </small>
              </>}
          </div>
        </div>
      })}
    </div>
  </section>
}

/// Chance and severity on one line. A break-even share only reads as a number while it sits inside
/// the seizure range; outside it the honest answer is a phrase, because "break-even 8%" on a run
/// where every possible stop already costs more than staying tells the player nothing they can use.
function riskLine(city: CityMarket, travel: TravelStatus) {
  const bust = `${city.bustChancePercent}% bust`
  const breakEven = city.breakEvenSeizurePercent
  if (breakEven === null) return bust
  if (breakEven <= 0) return `${bust}, pays less here`
  if (breakEven <= travel.seizureMinPercent) return `${bust}, any stop costs the trip`
  if (breakEven >= travel.seizureMaxPercent) return `${bust}, no stop can cost the trip`
  return `${bust}, break-even ${breakEven}%`
}

/// The price leads and the change against your own city sits under it: the number a player acts on
/// is the difference, and making them hold your city's price in their head to find it is the work
/// the panel should be doing.
function CityPrice({ price, base, showDelta }: { price: number, base: number | undefined, showDelta: boolean }) {
  const delta = base === undefined ? 0 : price - base
  return <div className="city-price">
    <b>{money.format(price)}</b>
    {showDelta && base !== undefined && (delta === 0
      ? <small>same</small>
      : <small className={delta > 0 ? 'up' : 'down'}>{delta > 0 ? '+' : '−'}{money.format(Math.abs(delta))}</small>)}
  </div>
}

function ReconPage(ctx: PageContext) {
  return <div className="page-grid two-column">
    <TargetReconPanel
      targets={ctx.targets}
      selectedTarget={ctx.selectedTarget}
      query={ctx.targetQuery}
      busy={ctx.busy}
      currentPlayerId={ctx.dashboard.playerId}
      combatMissions={ctx.combatMissions}
      dashboard={ctx.dashboard}
      attackCrew={ctx.attackCrew}
      setAttackCrew={ctx.setAttackCrew}
      commanderId={ctx.commanderId}
      setCommanderId={ctx.setCommanderId}
      onQuery={ctx.setTargetQuery}
      onSearch={ctx.searchTargets}
      onInspect={ctx.inspectTarget}
      onAttack={ctx.attackTarget}
    />
    <CombatMissionsPanel ctx={ctx} />
    <CombatHistoryPanel entries={ctx.combatLogs} currentPlayerId={ctx.dashboard.playerId} />
    <section className="panel">
      <div className="panel-title"><h2>Top Players</h2><span>Net worth</span></div>
      <Leaderboard leaders={ctx.leaders} currentPlayer={ctx.dashboard.name} />
    </section>
  </div>
}

function CombatMissionsPanel({ ctx }: { ctx: PageContext }) {
  const active = ctx.combatMissions.filter(mission => mission.status !== 'Complete')
  const completed = ctx.combatMissions.filter(mission => mission.status === 'Complete').slice(0, 8)
  const crew = ctx.dashboard.combatCrew
  return <>
    <section className="panel combat-missions-panel">
      <div className="panel-title"><h2>Active Missions</h2><span>{active.length} active</span></div>
      <div className="war-readiness">
        <AdminMetric label="Available pimps" value={number.format(crew.availablePimps)} />
        <AdminMetric label="Available thugs" value={number.format(crew.availableThugs)} />
        <AdminMetric label="Available weapons" value={number.format(crew.availableWeapons)} />
        <AdminMetric label="Active missions" value={`${crew.activeAttackMissions}/${crew.maxActiveAttackMissions}`} />
      </div>
      <div className="mission-list">
        {active.length === 0 && <p className="coming">No active missions.</p>}
        {active.map(mission => <MissionCard mission={mission} currentPlayerId={ctx.dashboard.playerId} busy={ctx.busy} onCancel={ctx.cancelMission} key={mission.id} />)}
      </div>
    </section>

    <section className="panel">
      <div className="panel-title"><h2>Recent Results</h2><span>Completed</span></div>
      <div className="mission-list compact">
        {completed.length === 0 && <p className="coming">No completed missions yet.</p>}
        {completed.map(mission => <MissionCard mission={mission} currentPlayerId={ctx.dashboard.playerId} compact key={mission.id} />)}
      </div>
    </section>
  </>
}

function MissionCard({ mission, currentPlayerId, compact = false, busy = false, onCancel }: { mission: CombatMission, currentPlayerId: string, compact?: boolean, busy?: boolean, onCancel?: (missionId: number) => void }) {
  // Finished missions fold away: their round-by-round log is history, not something to scroll past.
  const [expanded, setExpanded] = useState(false)
  const showEvents = !compact || expanded
  const attacking = mission.attackerId === currentPlayerId
  const nextAt = nextMissionTime(mission)
  const commander = mission.commanderName ?? 'A pimp'
  const title = attacking ? `${commander} -> ${mission.defenderName}` : `${mission.attackerName} attacking you`
  const canCancel = attacking && !compact && mission.canCancel && onCancel
  return <div className={`mission-card ${mission.status.toLowerCase()}`}>
    <div className="mission-head">
      <div><strong>{title}</strong><span>{mission.status} / {mission.outcome}</span></div>
      {compact
        ? <button
            className="mission-toggle"
            type="button"
            aria-expanded={expanded}
            onClick={() => setExpanded(value => !value)}
          >
            {expanded ? 'Hide log' : `${mission.events.length} update${mission.events.length === 1 ? '' : 's'}`}
          </button>
        : <b>{mission.status === 'Complete' ? 'Done' : timeUntil(nextAt)}</b>}
    </div>
    {!compact && <div className="mission-stats">
      <AdminMetric label="Commander" value={mission.commanderBonusPercent > 0 ? `${commander} +${mission.commanderBonusPercent}%` : commander} />
      <AdminMetric label="Remaining" value={`${mission.remainingAttackers} T / ${mission.remainingWeapons} W`} />
      <AdminMetric label="Round" value={`${mission.currentRound}/${mission.maxRounds}`} />
      <AdminMetric label="Morale" value={`${mission.attackerMorale.toFixed(0)} / ${mission.defenderMorale.toFixed(0)}`} />
      {mission.lootMultiplierPercent < 100 && <AdminMetric label="Haul" value={`${mission.lootMultiplierPercent}% (repeat target)`} />}
    </div>}
    <p>{mission.summary}</p>
    {canCancel && <div className="mission-actions">
      <span>Call the crew back now for {money.format(mission.cancelCashCost)} cash on hand.</span>
      <button
        className="secondary compact"
        disabled={busy}
        onClick={() => {
          if (window.confirm(`Cancel this attack for ${money.format(mission.cancelCashCost)}?`))
            onCancel(mission.id)
        }}
      >Cancel Mission</button>
    </div>}
    {showEvents && <div className="mission-events">
      {mission.events.length === 0 && <small>No updates yet.</small>}
      {mission.events.map(event => <div className="mission-event" key={event.id}>
        <strong>{event.kind}{event.round > 0 ? ` ${event.round}` : ''}</strong>
        <span>{new Date(event.createdAtUtc).toLocaleTimeString()}</span>
        <p>{event.summary}</p>
      </div>)}
    </div>}
  </div>
}

const ADMIN_TABS = ['overview', 'players', 'ai', 'config', 'liveops', 'audit'] as const
type AdminTab = typeof ADMIN_TABS[number]

const ADMIN_TAB_META: Record<AdminTab, { label: string, kicker: string }> = {
  overview: { label: 'Overview', kicker: 'Totals and distribution' },
  players: { label: 'Players', kicker: 'Search and enforcement' },
  ai: { label: 'AI Rivals', kicker: 'Seed, run, automate' },
  config: { label: 'Tuning', kicker: 'Runtime values' },
  liveops: { label: 'Live Ops', kicker: 'Maintenance and banners' },
  audit: { label: 'Audit', kicker: 'Who changed what' }
}

/**
 * One tab at a time rather than six stacked panels. The Admin Control Center used to sit at the bottom
 * holding whatever had no other home: headline totals, a read-only economy dump, and the AI controls.
 * Those are three different jobs, so they now live with the things they belong to.
 */
function AdminPage(ctx: PageContext & { overview: AdminOverview }) {
  const [tab, setTab] = useState<AdminTab>('overview')
  return <div className="page-grid one-column">
    <nav className="admin-tabs">
      {ADMIN_TABS.map(name => <button
        key={name}
        type="button"
        className={tab === name ? 'active' : ''}
        onClick={() => setTab(name)}
      >
        <strong>{ADMIN_TAB_META[name].label}</strong>
        <span>{ADMIN_TAB_META[name].kicker}</span>
      </button>)}
    </nav>
    {tab === 'overview' && <AdminOverviewTab overview={ctx.overview} busy={ctx.busy} />}
    {tab === 'players' && <AdminPlayersPanel busy={ctx.busy} onChanged={() => void ctx.act(async () => undefined)} />}
    {tab === 'ai' && <AdminAiTab ctx={ctx} />}
    {tab === 'config' && <><AdminConfigPanel busy={ctx.busy} /><AdminEconomyReadout overview={ctx.overview} /></>}
    {tab === 'liveops' && <AdminLiveOpsPanel busy={ctx.busy} />}
    {tab === 'audit' && <AdminAuditPanel />}
  </div>
}

function AdminOverviewTab({ overview, busy }: { overview: AdminOverview, busy: boolean }) {
  return <>
    <section className="panel wide-panel">
      <div className="panel-title"><h2>The World</h2><span>As of {new Date(overview.generatedAtUtc).toLocaleTimeString()}</span></div>
      <div className="admin-metrics">
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
  return <section className="panel wide-panel">
    <div className="panel-title"><h2>In Effect Now</h2><span>Read-only summary</span></div>
    <div className="admin-config">
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
  return <section className={ops?.maintenanceMode ? 'panel wide-panel maintenance-on' : 'panel wide-panel'}>
    <div className="panel-title">
      <h2>Live Operations</h2>
      <span>{ops?.maintenanceMode ? 'Maintenance is ON' : 'Game is open'}</span>
    </div>
    {error && <div className="error banner"><span>{error}</span></div>}
    <p>Maintenance blocks every gameplay action for players while leaving reads and admin access open, so you can verify a deploy before letting anyone back in.</p>
    <div className="admin-action-row">
      <button
        className={ops?.maintenanceMode ? 'primary compact' : 'secondary compact'}
        disabled={locked}
        onClick={() => void apply({ maintenanceMode: !ops?.maintenanceMode })}
      >
        {ops?.maintenanceMode ? 'End maintenance' : 'Start maintenance'}
      </button>
      <label>Maintenance notice<input value={maintenanceMessage} onChange={e => setMaintenanceMessage(e.target.value)} placeholder="Back in 10 minutes" /></label>
      <button className="secondary compact" disabled={locked}
        onClick={() => void apply({ maintenanceMessage })}>Save notice</button>
    </div>
    <div className="admin-action-row">
      <label className="grow">Announcement banner<input value={announcement} onChange={e => setAnnouncement(e.target.value)} placeholder="Shown to every player" /></label>
      <button className="secondary compact" disabled={locked}
        onClick={() => void apply({ announcement })}>Save banner</button>
      <button className="secondary compact" disabled={locked || !ops?.announcement}
        onClick={() => void apply({ announcement: '' })}>Clear</button>
    </div>
    {ops && <small className="admin-updated">Last changed {new Date(ops.updatedAtUtc).toLocaleString()}{ops.updatedBy ? ` by ${ops.updatedBy}` : ''}.</small>}
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

  if (!config) return <section className="panel wide-panel">
    <div className="panel-title"><h2>Tuning</h2><span>Live config</span></div>
    {error ? <div className="error banner"><span>{error}</span></div> : <p className="coming">Loading.</p>}
  </section>

  const needle = filter.trim().toLowerCase()
  const matches = config.settings.filter(entry =>
    (!showAll ? entry.isOverridden || needle.length > 0 : true)
    && (needle.length === 0 || entry.path.toLowerCase().includes(needle)))
  const locked = busy || working

  return <section className="panel wide-panel">
    <div className="panel-title">
      <h2>Tuning</h2>
      <span>{config.overrideCount} override{config.overrideCount === 1 ? '' : 's'} live</span>
    </div>
    {error && <div className="error banner"><span>{error}</span></div>}
    {message && <div className="notice banner"><span>{message}</span></div>}
    <p>Changes apply on the next request, no restart. Overrides are stored in the database and layered over appsettings, so clearing one falls back to the shipped value. Table-shaped settings like storage levels are not editable here.</p>

    <label className="admin-reason">Reason (recorded in the audit trail)
      <input value={reason} onChange={e => setReason(e.target.value)} placeholder="Why are you retuning this?" />
    </label>

    <div className="admin-action-row">
      <label className="grow">Filter<input value={filter} onChange={e => setFilter(e.target.value)} placeholder="combat, morale, price..." /></label>
      <button className="secondary compact" disabled={locked} onClick={() => setShowAll(value => !value)}>
        {showAll ? 'Show overrides only' : `Show all ${config.settings.length}`}
      </button>
    </div>

    <div className="config-list">
      {matches.length === 0 && <p className="coming">
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
  return <div className={entry.isOverridden ? 'config-row overridden' : 'config-row'}>
    <div className="config-copy">
      <strong>{entry.path}</strong>
      <span>{entry.type}{entry.isOverridden ? ' / overridden' : ' / from appsettings'}</span>
    </div>
    <input value={draft} onChange={e => onDraft(e.target.value)} />
    <button className="primary compact" disabled={locked || !dirty} onClick={onSave}>Save</button>
    <button className="secondary compact" disabled={locked || !entry.isOverridden} onClick={onClear}>Reset</button>
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

  if (!data) return <section className="panel wide-panel">
    <div className="panel-title"><h2>Oversight</h2><span>Economy and combat</span></div>
    {error ? <div className="error banner"><span>{error}</span></div> : <p className="coming">Loading.</p>}
  </section>

  const overdue = data.activeMissions.filter(mission => mission.isOverdue)
  return <section className="panel wide-panel">
    <div className="panel-title"><h2>Oversight</h2><span>Economy and combat</span></div>
    {error && <div className="error banner"><span>{error}</span></div>}
    <div className="admin-metrics">
      <AdminMetric label="Median net worth" value={money.format(data.medianNetWorth)} />
      <AdminMetric label="Richest" value={money.format(data.topNetWorth)} />
      <AdminMetric label="Concentration" value={`${data.giniPercent.toFixed(1)}% Gini`} />
      <AdminMetric label="Active missions" value={number.format(data.activeMissions.length)} />
      <AdminMetric label="Stuck missions" value={number.format(overdue.length)} />
    </div>

    <div className="admin-action-block">
      <strong>Wealth spread</strong>
      <div className="admin-metrics">
        {data.wealthBands.map(band => <AdminMetric key={band.label} label={band.label} value={`${number.format(band.players)} / ${money.format(band.totalNetWorth)}`} />)}
      </div>
      <small>Gini runs 0 (everyone equal) to 100 (one player holds everything).</small>
    </div>

    <div className="admin-action-block">
      <strong>Fastest movers, last 24h</strong>
      <div className="audit-list">
        {data.fastestMovers.length === 0 && <p className="coming">No logged activity in the last day.</p>}
        {data.fastestMovers.map(mover => <div className="audit-row" key={mover.playerId}>
          <div>
            <strong>{mover.name}{mover.isBot ? ' (AI)' : ''}</strong>
            <span>{money.format(mover.cashGained24h)} in {number.format(mover.actionsLast24h)} actions</span>
          </div>
          <p>Net worth {money.format(mover.netWorth)}</p>
        </div>)}
      </div>
      <small>Approximated from logged cash and bank deltas; the game keeps no net worth history to diff.</small>
    </div>

    <div className="admin-action-block">
      <strong>In-flight missions</strong>
      <div className="audit-list">
        {data.activeMissions.length === 0 && <p className="coming">Nothing in flight.</p>}
        {data.activeMissions.map(mission => <div className={mission.isOverdue ? 'audit-row overdue' : 'audit-row'} key={mission.missionId}>
          <div>
            <strong>{mission.status}{mission.isOverdue ? ' / STUCK' : ''}</strong>
            <span>round {mission.currentRound}/{mission.maxRounds}</span>
          </div>
          <p>{mission.commanderName ?? 'A pimp'} ({mission.attackerName}) vs {mission.defenderName}</p>
          <div className="admin-action-row">
            <em>{mission.nextEventAtUtc ? `next ${new Date(mission.nextEventAtUtc).toLocaleTimeString()}` : 'no timer'}</em>
            <button className="secondary compact" disabled={busy || working}
              onClick={() => void resolve(mission.missionId)}>Force resolve</button>
          </div>
        </div>)}
      </div>
    </div>

    <div className="admin-action-block">
      <strong>AI health</strong>
      <div className="audit-list">
        {data.bots.map(bot => <div className="audit-row" key={bot.playerId}>
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

  return <section className="panel wide-panel">
    <div className="panel-title"><h2>Players</h2><span>Find and fix</span></div>
    <form className="target-search" onSubmit={search}>
      <label>Search<input value={query} onChange={e => setQuery(e.target.value)} placeholder="Player, username, or city" /></label>
      <button className="secondary compact" disabled={locked}>Search</button>
    </form>

    {error && <div className="error banner"><span>{error}</span></div>}
    {message && <div className="notice banner"><span>{message}</span></div>}

    <div className="admin-players">
      <div className="admin-player-list">
        {results.length === 0 && <p className="coming">No players matched.</p>}
        {results.map(player => <button
          className={target?.playerId === player.playerId ? 'admin-player-row active' : 'admin-player-row'}
          key={player.playerId}
          type="button"
          disabled={locked}
          onClick={() => void open(player.playerId)}
        >
          <strong>{player.name}</strong>
          <small>{player.username}{player.isBot ? ' / AI' : ''}{player.isAdmin ? ' / admin' : ''}</small>
          <em>{enforcementLabel(player)}</em>
          <b>{money.format(player.netWorth)}</b>
        </button>)}
      </div>

      {detail && target && <div className="admin-player-detail">
        <div className="admin-player-head">
          <div><strong>{target.name}</strong><span>{target.username} / {target.city}</span></div>
          <b className={target.isBanned ? 'tag banned' : 'tag ok'}>{enforcementLabel(target)}</b>
        </div>
        <div className="admin-metrics">
          <AdminMetric label="Net worth" value={money.format(target.netWorth)} />
          <AdminMetric label="Cash" value={money.format(target.cash)} />
          <AdminMetric label="Bank" value={money.format(target.bankCash)} />
          <AdminMetric label="Turns" value={number.format(target.turns)} />
          <AdminMetric label="Crew" value={`${target.pimps} P / ${target.hoes} H / ${target.thugs} T`} />
          <AdminMetric label="Morale" value={`${detail.hoeHappiness.toFixed(0)}% / ${detail.thugHappiness.toFixed(0)}%`} />
          <AdminMetric label="Hideout" value={`${detail.hideout.tierName} S${detail.hideout.storageLevel}/V${detail.hideout.safeLevel}`} />
          <AdminMetric label="Joined" value={new Date(target.createdAtUtc).toLocaleDateString()} />
        </div>

        <label className="admin-reason">Reason (recorded in the audit trail)
          <input value={reason} onChange={e => setReason(e.target.value)} placeholder="Why are you doing this?" />
        </label>

        <div className="admin-action-block">
          <strong>Quick grants</strong>
          <div className="admin-cheat-grid">
            {adjustPresets.map(preset => <button
              className="secondary compact"
              key={preset.label}
              disabled={locked}
              onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, preset.resource, preset.delta, reason))}
            >{preset.label}</button>)}
            <button className="secondary compact" disabled={locked}
              onClick={() => void run('Morale set', () => adminApi.setMorale(target.playerId, 100, reason))}>Morale 100%</button>
          </div>
        </div>

        <div className="admin-action-block">
          <strong>Adjust a resource</strong>
          <div className="admin-action-row">
            <label>Resource<select value={resource} onChange={e => setResource(e.target.value)}>
              {detail.adjustableResources.map(key => <option key={key} value={key}>{key}</option>)}
            </select></label>
            <label>Change<input type="number" value={delta} onChange={e => setDelta(Number(e.target.value))} /></label>
            <button className="primary compact" disabled={locked || delta === 0}
              onClick={() => void run('Adjusted', () => adminApi.adjust(target.playerId, resource, delta, reason))}>
              Apply
            </button>
          </div>
          <small>Negative values take resources away. Nothing drops below zero.</small>
        </div>

        <div className="admin-action-block">
          <strong>Account</strong>
          <div className="admin-action-row">
            <button className="secondary compact" disabled={locked}
              onClick={() => void run('Banned', () => adminApi.enforcement(target.playerId, 'ban', null, reason))}>
              Ban
            </button>
            <label>Suspend hours<input type="number" min={1} value={suspendHours} onChange={e => setSuspendHours(Number(e.target.value))} /></label>
            <button className="secondary compact" disabled={locked || suspendHours < 1}
              onClick={() => void run('Suspended', () => adminApi.enforcement(
                target.playerId,
                'suspend',
                new Date(Date.now() + suspendHours * 3600_000).toISOString(),
                reason))}>
              Suspend
            </button>
            <button className="secondary compact" disabled={locked}
              onClick={() => void run('Cleared', () => adminApi.enforcement(target.playerId, 'clear', null, reason))}>
              Lift
            </button>
            <button className="secondary compact" disabled={locked}
              onClick={() => void run('Logged out', () => adminApi.forceLogout(target.playerId, reason))}>
              Force logout
            </button>
          </div>
        </div>

        <div className="admin-action-block">
          <strong>Identity and rights</strong>
          <div className="admin-action-row">
            <label>Name<input value={renameTo} onChange={e => setRenameTo(e.target.value)} minLength={3} maxLength={32} /></label>
            <button className="secondary compact" disabled={locked || renameTo.trim() === target.name}
              onClick={() => void run('Renamed', () => adminApi.rename(target.playerId, renameTo, reason))}>
              Rename
            </button>
            <button className="secondary compact" disabled={locked || target.isBot}
              onClick={() => void run('Rights changed', () => adminApi.setAdminRights(target.playerId, !target.isAdmin, reason))}>
              {target.isAdmin ? 'Revoke admin' : 'Grant admin'}
            </button>
          </div>
        </div>

        <div className="admin-action-block">
          <strong>Recent activity</strong>
          <ActivityList entries={detail.recentActivity.slice(0, 6)} />
        </div>

        {detail.auditTrail.length > 0 && <div className="admin-action-block">
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

  return <section className="panel wide-panel">
    <div className="panel-title"><h2>Audit Trail</h2><span>Every admin action</span></div>
    {error && <div className="error banner"><span>{error}</span></div>}
    {entries.length === 0 && <p className="coming">No admin actions recorded yet.</p>}
    <AuditList entries={entries.slice(0, 30)} />
  </section>
}

function AuditList({ entries }: { entries: AdminAuditEntry[] }) {
  return <div className="audit-list">
    {entries.map(entry => <div className="audit-row" key={entry.id}>
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
function CatchUpDialog({ news, onClose }: { news: CatchUp, onClose: () => void }) {
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const away = news.awayMinutes < 60
    ? `${news.awayMinutes} minute${news.awayMinutes === 1 ? '' : 's'}`
    : `${Math.floor(news.awayMinutes / 60)} hour${Math.floor(news.awayMinutes / 60) === 1 ? '' : 's'}`

  return <div className="modal-backdrop" onClick={onClose}>
    <div className="modal catch-up" role="dialog" aria-label="While you were away" onClick={event => event.stopPropagation()}>
      <div className="modal-head">
        <h2>While you were away</h2>
        <span>{away} since you last looked in</span>
      </div>
      <div className="catch-up-items">
        {news.items.map((item, index) => <div className={`catch-up-item ${item.tone}`} key={`${item.kind}-${index}`}>
          <strong>{item.headline}</strong>
          <span>{item.detail}</span>
        </div>)}
      </div>
      <button className="primary" onClick={onClose}>Back to work</button>
    </div>
  </div>
}

function AlertBell({ unread, onRead }: { unread: number, onRead: () => void }) {
  const [open, setOpen] = useState(false)
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [error, setError] = useState('')

  const toggle = async () => {
    if (open) {
      setOpen(false)
      return
    }
    setOpen(true)
    setError('')
    try {
      const loaded = await api.alerts()
      setAlerts(loaded.alerts)
      if (loaded.unreadCount > 0) {
        await api.markAlertsSeen()
        onRead()
      }
    } catch (e) { setError((e as Error).message) }
  }

  return <div className="alert-bell">
    <button className={unread > 0 ? 'bell unread' : 'bell'} type="button" onClick={() => void toggle()} aria-expanded={open}>
      Alerts
      {unread > 0 && <b>{unread > 99 ? '99+' : unread}</b>}
    </button>
    {open && <div className="alert-panel">
      <div className="alert-panel-head">
        <strong>Alerts</strong>
        <button className="dismiss" type="button" aria-label="Close alerts" onClick={() => setOpen(false)}>x</button>
      </div>
      {error && <p className="coming">{error}</p>}
      {!error && alerts.length === 0 && <p className="coming">Nothing has happened to you yet.</p>}
      {alerts.map(alert => <div className={alertClass(alert)} key={alert.id}>
        <strong>{alert.headline}</strong>
        <span>{alert.detail}</span>
        <small>{new Date(alert.createdAtUtc).toLocaleString()}</small>
      </div>)}
    </div>}
  </div>
}

/**
 * What a rival is doing right now. Idle minutes stopped meaning anything on their own once rivals
 * played in sessions: one quiet for four hours is asleep, not stuck, and the admin needs to be able
 * to tell those apart at a glance.
 */
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

function alertClass(alert: Alert) {
  const base = alert.tone === 'bad' ? 'alert-row hit' : 'alert-row held'
  return alert.isUnread ? `${base} fresh` : base
}

function StatusStrip({ dashboard, nextTurn }: { dashboard: Dashboard, nextTurn: string }) {
  return <section className="status-strip">
    <Stat label="Cash" value={money.format(dashboard.cash)} />
    <Stat label="Bank" value={money.format(dashboard.bankCash)} />
    <Stat label="Net Worth" value={money.format(dashboard.netWorth)} />
    <Stat label="Turns" value={`${dashboard.turns} / ${dashboard.maxTurns}`} sub={nextTurn === 'MAX' ? 'Turn bank full' : `+${dashboard.turnsPerTick} in ${nextTurn}`} />
    <Stat
      label="Heat"
      value={dashboard.hideout.heatLabel}
      sub={dashboard.hideout.heatDetail}
      tone={`heat-${dashboard.hideout.heatLabel.toLowerCase()}`}
      title={dashboard.hideout.heatNote}
    />
    <Stat label="Rank" value={`#${dashboard.rank}`} />
    <Stat label="City" value={dashboard.city} />
  </section>
}

/** Upkeep a street action of this length burns, scaled from the server's max-action figures. */
function upkeepFor(dashboard: Dashboard, turns: number) {
  const planned = Math.max(1, Math.min(turns, dashboard.maxActionTurns))
  const scale = (maxActionNeed: number) => Math.ceil(maxActionNeed * planned / dashboard.maxActionTurns)
  return {
    planned,
    condoms: scale(dashboard.crewReport.condomsNeededForMaxStreetAction),
    beer: scale(dashboard.crewReport.beerNeededForMaxStreetAction),
  }
}

/**
 * Mirrors the server's auto-buy: shortfall, limited by storage room and by cash. Only an estimate for
 * display; the API does the real arithmetic and is the authority on what gets spent.
 */
function restockEstimate(dashboard: Dashboard, turns: number) {
  const upkeep = upkeepFor(dashboard, turns)
  const price = (key: string) => dashboard.store.find(item => item.key === key)?.price ?? 0
  const wanted = (need: number, held: number, cap: number) => Math.max(0, Math.min(need - held, cap - held))
  const condoms = wanted(upkeep.condoms, dashboard.condoms, dashboard.hideout.maxCondoms)
  const beer = wanted(upkeep.beer, dashboard.beer, dashboard.hideout.maxBeer)
  const cost = condoms * price('condoms') + beer * price('beer')
  return { condoms, beer, cost }
}

function restockLabel(restock: { condoms: number, beer: number, cost: number }, cash: number) {
  if (restock.condoms === 0 && restock.beer === 0)
    return 'Nothing to top up for this action.'
  const parts: string[] = []
  if (restock.condoms > 0) parts.push(`${number.format(restock.condoms)} condoms`)
  if (restock.beer > 0) parts.push(`${number.format(restock.beer)} beer`)
  const short = cash < restock.cost ? ' Your cash covers only part of it, so it buys what it can.' : ''
  return `Buys ${parts.join(' and ')} for about ${money.format(restock.cost)}.${short}`
}

function StreetSupplyPanel({ dashboard, busy, streetTurns, storeQty, setStoreQty, act, onMarket }: {
  dashboard: Dashboard
  busy: boolean
  streetTurns: number
  storeQty: Record<string, number>
  setStoreQty: React.Dispatch<React.SetStateAction<Record<string, number>>>
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  onMarket: () => void
}) {
  const upkeep = upkeepFor(dashboard, streetTurns)
  const plannedTurns = upkeep.planned
  const turnLabel = `${plannedTurns} turn${plannedTurns === 1 ? '' : 's'}`
  const catalog = new Map(dashboard.store.map(item => [item.key, item] as const))
  const hideout = dashboard.hideout
  const supplies = [
    { key: 'condoms', owned: dashboard.condoms, cap: hideout.maxCondoms, needed: upkeep.condoms, basis: `to work ${turnLabel}` },
    { key: 'beer', owned: dashboard.beer, cap: hideout.maxBeer, needed: upkeep.beer, basis: `to work ${turnLabel}` },
    // Weapons are permanent coverage, so their requirement is the crew size rather than the turns.
    { key: 'weapons', owned: dashboard.weapons, cap: hideout.maxWeapons, needed: dashboard.thugs, basis: 'to arm every thug' },
  ].filter(supply => catalog.has(supply.key))
  if (supplies.length === 0) return null

  return <div className="supply-panel">
    <div className="supply-head">
      <div><strong>Supplies</strong><span>Checked against {turnLabel}</span></div>
      <button className="primary" type="button" onClick={onMarket}>Open Market</button>
    </div>
    <div className="supply-list">
      {supplies.map(supply => {
        const item = catalog.get(supply.key)!
        const short = Math.max(0, supply.needed - supply.owned)
        // The storage room refuses buys that do not fit, so never offer more than the room left.
        const room = Math.max(0, supply.cap - supply.owned)
        const qty = Math.min(storeQty[supply.key] ?? Math.max(1, short), Math.max(1, room))
        const total = qty * item.price
        return <div className={short > 0 ? 'supply-row short' : 'supply-row'} key={supply.key}>
          <div className="supply-copy">
            <strong>{item.name}</strong>
            <span>{number.format(supply.owned)} on hand / {number.format(supply.needed)} {supply.basis} / {number.format(supply.cap)} storage</span>
          </div>
          <em>{room === 0 ? 'Storage full' : short > 0 ? `${number.format(short)} short` : 'Covered'}</em>
          <label>Qty<input aria-label={`${item.name} quantity`} type="number" min={1} max={Math.max(1, room)} value={qty} onChange={event => setStoreQty(value => ({ ...value, [supply.key]: Number(event.target.value) }))} /></label>
          <button
            className="primary"
            disabled={busy || qty < 1 || room === 0 || qty > room || dashboard.cash < total}
            onClick={() => void act(() => api.buyStoreItem(supply.key, qty))}
          >
            {room === 0 ? 'Storage Full' : `Buy ${money.format(total)}`}
          </button>
        </div>
      })}
    </div>
    <div className="supply-carry">
      <span>Street work also turns up product.</span>
      <small>Carrying {number.format(dashboard.weed)} weed / {number.format(dashboard.coke)} coke</small>
    </div>
  </div>
}

/**
 * What to do next, ranked by the server against the state the player is actually in.
 *
 * This used to be four fixed rows - turns, crew pressure, supplies, combat posture - that read the
 * same on day one and day one hundred and never named a move. A new player finished their whole
 * first session having clicked one button five times, with the best purchase available to them
 * sitting unmentioned in a room they had no reason to open.
 */
function NextMovePanel({ dashboard, onPage }: { dashboard: Dashboard, onPage: (page: AppPage) => void }) {
  const moves = dashboard.guidance?.moves ?? []
  if (moves.length === 0) return null

  return <section className="panel">
    <div className="panel-title"><h2>Next Moves</h2><span>Worth doing now</span></div>
    <div className="flow-list">
      {moves.map(move => <button
        className={move.urgent ? 'flow-row urgent' : 'flow-row'}
        type="button"
        key={move.label}
        onClick={() => onPage(move.page as AppPage)}
      >
        <strong>{move.label}{move.cost > 0 && <b className="flow-cost">{money.format(move.cost)}</b>}</strong>
        <span>{move.why}</span>
      </button>)}
    </div>
  </section>
}

/**
 * The opening ladder, and the only place the game explains itself.
 *
 * Hidden once it is finished rather than kept forever: a checklist a veteran still has to scroll past
 * is clutter, and the whole point of it is to stop being needed.
 */
function OpeningLadderPanel({ dashboard, onPage }: { dashboard: Dashboard, onPage: (page: AppPage) => void }) {
  const guidance = dashboard.guidance
  if (!guidance || guidance.objectivesDone >= guidance.objectivesTotal) return null
  // The next unfinished rung, plus what has been done, so progress is visible without listing it all.
  const next = guidance.objectives.find(o => !o.done)

  return <section className="panel">
    <div className="panel-title">
      <h2>Getting Started</h2>
      <span>{guidance.objectivesDone} of {guidance.objectivesTotal}</span>
    </div>
    <div className="ladder">
      {guidance.objectives.map(step => <button
        className={step.done ? 'ladder-row done' : step === next ? 'ladder-row next' : 'ladder-row'}
        type="button"
        key={step.label}
        onClick={() => onPage(step.page as AppPage)}
      >
        <em>{step.done ? '✓' : ''}</em>
        <div>
          <strong>{step.label}</strong>
          {step === next && <span>{step.why}</span>}
        </div>
      </button>)}
    </div>
  </section>
}

function PimpRosterPanel({ dashboard }: { dashboard: Dashboard }) {
  const crew = dashboard.crew
  const fallen = dashboard.fallenCrew
  return <section className="panel wide-panel">
    <div className="panel-title"><h2>Your Pimps</h2><span>{crew.length}/{dashboard.hideout.maxPimps} on the payroll</span></div>
    <p>Pimps are the only crew you know by name. One of them commands each attack, and loyalty slides when the operation is miserable or a mission goes badly.</p>
    <div className="pimp-list">
      {crew.length === 0 && <p className="coming">No pimps left. Hire one before you can run the streets or attack.</p>}
      {crew.map(pimp => <div className={pimp.isCommanding ? 'pimp-row out' : 'pimp-row'} key={pimp.id}>
        <div className="pimp-copy">
          <strong>{pimp.name} <b className={pimp.specialty === 'Enforcer' ? 'tag enforcer' : 'tag hustler'}>{pimp.specialty} +{pimp.bonusPercent}%</b></strong>
          <span>{pimp.specialty === 'Enforcer' ? 'Sharpens attacks they lead and the house while home' : 'Lifts street income while home'}</span>
          <span>{pimp.missionsLed === 0 ? 'No missions led yet' : `${number.format(pimp.missionsLed)} mission${pimp.missionsLed === 1 ? '' : 's'} led / ${number.format(pimp.victories)} won`}</span>
        </div>
        <em>{pimp.isCommanding ? 'Out commanding' : 'At the house'}</em>
        <div className={`pimp-loyalty ${moraleTone(pimp.loyalty)}`}>
          <span>Loyalty</span>
          <strong>{pimp.loyalty.toFixed(0)}%</strong>
        </div>
      </div>)}
    </div>
    {fallen.length > 0 && <div className="pimp-fallen">
      <strong>Gone</strong>
      <div className="pimp-fallen-list">
        {fallen.map(pimp => <div className="pimp-gone" key={pimp.id}>
          <b>{pimp.name}</b>
          <span>{pimp.lostReason}</span>
          <small>{pimp.lostAtUtc ? new Date(pimp.lostAtUtc).toLocaleDateString() : ''}</small>
        </div>)}
      </div>
    </div>}
  </section>
}

function moraleTone(value: number) {
  if (value < 30) return 'danger'
  if (value < 60) return 'warn'
  return 'good'
}

function HideoutMoralePanel({ dashboard, busy, act }: {
  dashboard: Dashboard
  busy: boolean
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
}) {
  const report = dashboard.crewReport
  const moraleFull = dashboard.hoeHappiness >= 100 && dashboard.thugHappiness >= 100
  const canRest = !busy
    && !moraleFull
    && dashboard.turns >= report.hqRestTurnCost
    && dashboard.cash >= report.hqRestCashCost
  const canParty = !busy
    && !moraleFull
    && dashboard.turns >= report.hqPartyTurnCost
    && dashboard.cash >= report.hqPartyCashCost
    && dashboard.beer >= report.hqPartyBeerCost
    && dashboard.weed >= report.hqPartyWeedCost

  return <section className="panel wide-panel hideout-panel">
    <div className="panel-title"><h2>{dashboard.hideout.tierName}</h2><span>Hideout morale</span></div>
    <div className="hideout-layout">
      <div className="hideout-copy">
        <strong>Current hideout</strong>
        <p>Your crew comes back here after street work and fights. Low morale heals slowly over time, or you can spend turns and supplies to stabilize them faster.</p>
      </div>
      <div className="hideout-actions">
        <button className="secondary" disabled={!canRest} onClick={() => void act(() => api.recoverMorale('rest'))}>
          Rest Crew
          <span>{report.hqRestTurnCost} turns / {money.format(report.hqRestCashCost)} / +{report.hqRestMoraleGain.toFixed(0)}%</span>
        </button>
        <button className="primary" disabled={!canParty} onClick={() => void act(() => api.recoverMorale('party'))}>
          Throw Party
          <span>{report.hqPartyTurnCost} turns / {money.format(report.hqPartyCashCost)} / {report.hqPartyBeerCost} beer / {report.hqPartyWeedCost} weed</span>
        </button>
      </div>
    </div>
  </section>
}

function TargetReconPanel({ targets, selectedTarget, query, busy, currentPlayerId, combatMissions, dashboard, attackCrew, setAttackCrew, commanderId, setCommanderId, onQuery, onSearch, onInspect, onAttack }: {
  targets: PlayerTarget[]
  selectedTarget: PlayerProfile | null
  query: string
  busy: boolean
  currentPlayerId: string
  combatMissions: CombatMission[]
  dashboard: Dashboard
  attackCrew: { thugs: number, weapons: number }
  setAttackCrew: React.Dispatch<React.SetStateAction<{ thugs: number, weapons: number }>>
  commanderId: number | null
  setCommanderId: (id: number | null) => void
  onQuery: (query: string) => void
  onSearch: (event: FormEvent<HTMLFormElement>) => void
  onInspect: (playerId: string) => void
  onAttack: (playerId: string) => void
}) {
  const profile = selectedTarget
  const activeOutgoingMissions = combatMissions.filter(mission => mission.attackerId === currentPlayerId && mission.status !== 'Complete')
  const activeAgainstProfile = profile
    ? activeOutgoingMissions.find(mission => mission.defenderId === profile.playerId)
    : undefined
  const crew = dashboard.combatCrew
  const freeCommanders = dashboard.crew.filter(pimp => !pimp.isCommanding)
  const attackReady = crew.availablePimps >= 1
    && attackCrew.thugs >= 1
    && attackCrew.weapons >= 0
    && attackCrew.weapons <= attackCrew.thugs
    && attackCrew.thugs <= crew.availableThugs
    && attackCrew.weapons <= crew.availableWeapons
    && crew.activeAttackMissions < crew.maxActiveAttackMissions
  return <div className="panel target-panel">
    <div className="panel-title"><h2>Combat Targets</h2><span>Scout + launch</span></div>
    <form className="target-search" onSubmit={onSearch}>
      <label>Search<input value={query} onChange={event => onQuery(event.target.value)} placeholder="Name or city" /></label>
      <button className="secondary compact" disabled={busy}>Search</button>
    </form>
    <div className="target-layout">
      <div className="target-list">
        {targets.length === 0 && <p className="coming">No targets found.</p>}
        {targets.map(target => <button
          className={profile?.playerId === target.playerId ? 'target-row active' : 'target-row'}
          key={target.playerId}
          type="button"
          disabled={busy}
          onClick={() => onInspect(target.playerId)}
        >
          <span>#{target.rank}</span>
          <strong>{target.name}</strong>
          <small>{target.city}{target.aiPersonality ? ` / ${target.aiPersonality}` : target.isBot ? ' / AI' : ''}</small>
          <em className={target.combatStatus.mismatchReason ? 'blocked' : undefined}>{target.combatStatus.eligibility} / {target.combatReadiness.riskBand}</em>
          <b>{money.format(target.netWorth)}</b>
        </button>)}
      </div>
      {profile && <div className="target-profile">
        <div className="target-profile-head">
          <div><strong>{profile.name}</strong><span>{profile.city}{profile.aiPersonality ? ` / ${profile.aiPersonality}` : profile.isBot ? ' / AI rival' : ''}</span></div>
          <b>#{profile.rank}</b>
        </div>
        <div className="attack-assign">
          <StatusRow label="Available" value={`${crew.availablePimps} P / ${crew.availableThugs} T / ${crew.availableWeapons} W`} warn={crew.availablePimps < 1 || crew.availableThugs < 1} />
          <StatusRow label="Committed" value={`${crew.committedPimps} P / ${crew.committedThugs} T / ${crew.committedWeapons} W`} warn={crew.committedThugs > 0} />
          <div className="attack-inputs">
            <label>Commander
              <select
                value={commanderId ?? ''}
                onChange={event => setCommanderId(event.target.value === '' ? null : Number(event.target.value))}
              >
                <option value="">Best available</option>
                {freeCommanders.map(pimp => <option key={pimp.id} value={pimp.id}>
                  {pimp.name} - {pimp.specialty} +{pimp.bonusPercent}%
                </option>)}
              </select>
            </label>
            <label>Thugs<input type="number" min={1} max={Math.max(1, crew.availableThugs)} value={attackCrew.thugs} onChange={e => setAttackCrew(value => ({ ...value, thugs: Number(e.target.value), weapons: Math.min(value.weapons, Number(e.target.value)) }))} /></label>
            <label>Weapons<input type="number" min={0} max={Math.max(0, Math.min(crew.availableWeapons, attackCrew.thugs))} value={attackCrew.weapons} onChange={e => setAttackCrew(value => ({ ...value, weapons: Number(e.target.value) }))} /></label>
          </div>
          <small className="attack-note">{commanderNote(freeCommanders.find(x => x.id === commanderId) ?? null)}</small>
        </div>
        <div className="target-actions">
          <button
            className="primary"
            type="button"
            disabled={busy || !!activeAgainstProfile || !attackReady || !profile.combatStatus.canAttackNow}
            onClick={() => onAttack(profile.playerId)}
          >
            Attack Target
          </button>
          <span>{attackStatusText(
            profile.combatStatus,
            activeAgainstProfile,
            activeOutgoingMissions[0],
            attackReady)}</span>
        </div>
        <div className="target-metrics">
          <AdminMetric label="Net worth" value={money.format(profile.netWorth)} />
          <AdminMetric label="Cash" value={money.format(profile.cash)} />
          <AdminMetric label="Bank" value={money.format(profile.bankCash)} />
          <AdminMetric label="Attack" value={number.format(profile.combatReadiness.attackPower)} />
          <AdminMetric label="Defense" value={number.format(profile.combatReadiness.defensePower)} />
          <AdminMetric label="Risk" value={profile.combatReadiness.riskBand} />
          <AdminMetric label="Combat" value={profile.combatStatus.eligibility} />
        </div>
        <div className="target-readiness">
          <StatusRow label="Crew" value={`${profile.pimps} P / ${profile.hoes} H / ${profile.thugs} T`} />
          <StatusRow label="Weapons" value={`${profile.combatReadiness.armedThugs}/${profile.thugs} armed`} warn={profile.combatReadiness.uncoveredThugs > 0} />
          <StatusRow label="Weapon coverage" value={`${profile.combatReadiness.weaponCoveragePercent.toFixed(0)}%`} warn={profile.combatReadiness.weaponCoveragePercent < 75} />
          <StatusRow label="Protection" value={combatProtectionText(profile.combatStatus)} warn={profile.combatStatus.isProtected} />
          <StatusRow label="24h combat" value={`${profile.combatStatus.recentAttacksMade} attacks / ${profile.combatStatus.recentDefenses} defenses`} />
          {profile.combatStatus.mismatchReason && <StatusRow label="Blocked" value={profile.combatStatus.mismatchReason} warn />}
          <StatusRow label="Hoe morale" value={`${profile.hoeHappiness.toFixed(0)}%`} warn={profile.hoeHappiness < 50} />
          <StatusRow label="Thug morale" value={`${profile.thugHappiness.toFixed(0)}%`} warn={profile.thugHappiness < 50} />
          <StatusRow label="Product" value={`${number.format(profile.weed)} weed / ${number.format(profile.coke)} coke`} />
        </div>
        <div className="target-activity">
          <strong>Public Activity</strong>
          {profile.publicActivity.length === 0 && <p className="coming">No public activity yet.</p>}
          <ActivityList entries={profile.publicActivity} />
        </div>
      </div>}
    </div>
  </div>
}

function BankPanel({ dashboard, busy, bankAmount, setBankAmount, act, className }: {
  dashboard: Dashboard
  busy: boolean
  bankAmount: number
  setBankAmount: (amount: number) => void
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  className?: string
}) {
  return <section className={`panel ${className ?? ''}`}>
    <div className="panel-title"><h2>Bank</h2><span>Cash handling</span></div>
    <p>Banked cash still counts toward net worth. Combat can steal cash on hand, but bank cash stays protected.</p>
    <div className="action-row wrap">
      <label>Amount<input type="number" min={1} value={bankAmount} onChange={e => setBankAmount(Number(e.target.value))} /></label>
      <button className="secondary" disabled={busy || bankAmount < 1 || bankAmount > dashboard.cash} onClick={() => void act(() => api.deposit(bankAmount))}>Deposit</button>
      <button className="secondary" disabled={busy || bankAmount < 1 || bankAmount > dashboard.bankCash} onClick={() => void act(() => api.withdraw(bankAmount))}>Withdraw</button>
    </div>
  </section>
}

function CombatHistoryPanel({ entries, currentPlayerId }: { entries: CombatLog[], currentPlayerId: string }) {
  return <section className="panel combat-history-panel">
    <div className="panel-title"><h2>Combat History</h2><span>Last {entries.length}</span></div>
    <div className="combat-history">
      {entries.length === 0 && <p className="coming">No fights yet.</p>}
      {entries.map(entry => {
        const attacking = entry.attackerId === currentPlayerId
        const pending = entry.outcome === 'Pending'
        return <div className={`${attacking ? 'combat-entry attack' : 'combat-entry defense'}${pending ? ' pending' : ''}`} key={entry.id}>
          <div><strong>{attacking ? 'Attack' : 'Defense'} / {entry.outcome}</strong><span>{new Date(entry.createdAtUtc).toLocaleString()}</span></div>
          <p>{entry.summary}</p>
          <small>{entry.attackerName} vs {entry.defenderName} / {pending && entry.resolvesAtUtc ? `ETA ${timeUntil(entry.resolvesAtUtc)}` : `${entry.attackerPower}-${entry.defenderPower} power`}</small>
        </div>
      })}
    </div>
  </section>
}

function ProductTradeCard({ name, owned, price, quantity, canProduce, disabled, onProduce, onQuantity, onSell }: {
  name: 'Weed' | 'Coke'
  owned: number
  price: number
  quantity: number
  canProduce: boolean
  disabled: boolean
  onProduce: () => void
  onQuantity: (quantity: number) => void
  onSell: () => void
}) {
  return <div className="product-card">
    <div className="product-card-head">
      <div><strong>{name}</strong><span>{number.format(owned)} owned</span></div>
      <b>{money.format(price)}</b>
    </div>
    <button className="primary compact" disabled={disabled || !canProduce} onClick={onProduce}>Produce {name}</button>
    <div className="product-sell">
      <label>Sell Qty<input type="number" min={1} max={Math.max(1, owned)} value={quantity} onChange={e => onQuantity(Number(e.target.value))} /></label>
      <button className="secondary compact" disabled={disabled || quantity < 1 || quantity > owned} onClick={onSell}>Sell</button>
    </div>
  </div>
}

/**
 * Everything about the AI rivals in one place: how many exist, running them by hand, and the
 * automatic loop. The loop's timing is editable here because it was previously fixed at startup from
 * appsettings, so tuning it meant a restart, and the on/off switch lived only in memory so a restart
 * silently reverted it.
 */
/**
 * Tells one rival exactly what to do next, so a scenario can be set up rather than waited for.
 * Everything runs through the same services a real player's action does, so a refusal here is a real
 * game rule refusing, which is often the thing being tested.
 */
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

  return <div className="bot-directive">
    <div className="admin-subtitle">
      <strong>Direct {bot.name}</strong>
      <span>Runs through the real rules, so a refusal is the game refusing</span>
    </div>
    <div className="admin-action-row">
      <label>Action<select value={action} onChange={e => setAction(e.target.value)}>
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
        <label>Turns<input type="number" min={1} max={20} value={turns} onChange={e => setTurns(Number(e.target.value))} /></label>}
      {(action === 'produce' || action === 'sell') &&
        <label>Product<select value={product} onChange={e => setProduct(e.target.value)}>
          <option value="weed">Weed</option><option value="coke">Coke</option>
        </select></label>}
      {action === 'buy' &&
        <label>Item<select value={item} onChange={e => setItem(e.target.value)}>
          <option value="condoms">Condoms</option><option value="beer">Beer</option><option value="weapons">Weapons</option>
        </select></label>}
      {(action === 'hire' || action === 'fire') &&
        <label>Role<select value={role} onChange={e => setRole(e.target.value)}>
          <option value="pimps">Pimps</option><option value="hoes">Hoes</option><option value="thugs">Thugs</option>
        </select></label>}
      {(action === 'sell' || action === 'buy' || action === 'hire' || action === 'fire') &&
        <label>Quantity<input type="number" min={1} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>}
      {(action === 'deposit' || action === 'withdraw') &&
        <label>Amount<input type="number" min={1} step={1000} value={amount} onChange={e => setAmount(Number(e.target.value))} /></label>}
      {action === 'recover' &&
        <label>Strategy<select value={strategy} onChange={e => setStrategy(e.target.value)}>
          <option value="rest">Rest</option><option value="party">Party</option>
        </select></label>}
      {action === 'upgrade' &&
        <label>Room<select value={room} onChange={e => setRoom(e.target.value)}>
          <option value="tier">Building tier</option><option value="storage">Storage</option>
          <option value="safe">Safe</option><option value="weedlab">Weed lab</option><option value="cokelab">Coke lab</option>
        </select></label>}
      {action === 'attack' && <>
        <label>Target<select value={defenderId} onChange={e => setDefenderId(e.target.value)}>
          <option value={selfId}>{selfName} (you)</option>
          {targets.map(t => <option key={t.playerId} value={t.playerId}>{t.name}</option>)}
        </select></label>
        <label>Thugs<input type="number" min={1} value={quantity} onChange={e => setQuantity(Number(e.target.value))} /></label>
      </>}

      <button className="primary compact" disabled={busy} onClick={() => onRun(directive())}>Do it</button>
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
    <section className="panel wide-panel">
      <div className="panel-title">
        <h2>Automatic AI</h2>
        <span>{auto.enabled ? `On, ${auto.roundsPerTick} round(s) every ${auto.tickSeconds}s` : 'Off'}</span>
      </div>
      <p>
        Rivals act on their own on this loop. The setting is saved, so it survives a restart, and the
        timing takes effect on the next tick without one.
      </p>
      <div className="admin-action-row">
        <button
          className={auto.enabled ? 'secondary compact' : 'primary compact'}
          disabled={busy || overview.botAccounts < 1}
          onClick={() => setBotAutomation(!auto.enabled)}
        >
          {auto.enabled ? 'Turn off' : 'Turn on'}
        </button>
        <label>Tick seconds<input
          type="number"
          min={auto.minTickSeconds}
          max={auto.maxTickSeconds}
          value={tickSeconds}
          onChange={e => setTickSeconds(Number(e.target.value))}
        /></label>
        <label>Rounds per tick<input
          type="number"
          min={auto.minRoundsPerTick}
          max={auto.maxRoundsPerTick}
          value={roundsPerTick}
          onChange={e => setRoundsPerTick(Number(e.target.value))}
        /></label>
        <button
          className="secondary compact"
          disabled={busy || !timingChanged || !timingValid}
          onClick={() => setBotAutomation(auto.enabled, { tickSeconds, roundsPerTick })}
        >
          Save timing
        </button>
        <button
          className="secondary compact"
          disabled={busy || atDefaults}
          onClick={() => setBotAutomation(auto.enabled, { resetTiming: true })}
        >
          Reset to {auto.defaultTickSeconds}s / {auto.defaultRoundsPerTick}
        </button>
      </div>
      {!timingValid && <p className="hint">
        Tick must be {auto.minTickSeconds}-{auto.maxTickSeconds}s and rounds {auto.minRoundsPerTick}-{auto.maxRoundsPerTick}.
      </p>}
      {overview.botAccounts < 1 && <p className="hint">Seed some rivals below before turning the loop on.</p>}
    </section>

    <section className="panel wide-panel">
      <div className="panel-title"><h2>Seed and Run</h2><span>{number.format(overview.botAccounts)} rivals exist</span></div>
      <div className="admin-action-row">
        <label>Seed count<input type="number" min={1} max={15} value={seedCount} onChange={e => setSeedCount(Number(e.target.value))} /></label>
        <button className="secondary compact" disabled={busy} onClick={() => setSeedCount(5)}>5</button>
        <button className="secondary compact" disabled={busy} onClick={() => setSeedCount(10)}>10</button>
        <button className="secondary compact" disabled={busy} onClick={() => setSeedCount(15)}>15</button>
        <button className="primary compact" disabled={busy || seedCount < 1 || seedCount > 15} onClick={() => seedBots(seedCount)}>Seed rivals</button>
      </div>
      <div className="admin-action-row">
        <label>Rounds<input type="number" min={1} max={10} value={runRounds} onChange={e => setRunRounds(Number(e.target.value))} /></label>
        <button className="secondary compact" disabled={busy} onClick={() => setRunRounds(1)}>1</button>
        <button className="secondary compact" disabled={busy} onClick={() => setRunRounds(3)}>3</button>
        <button className="secondary compact" disabled={busy} onClick={() => setRunRounds(10)}>10</button>
        <button className="primary compact" disabled={busy || overview.botAccounts < 1 || runRounds < 1 || runRounds > 10} onClick={() => runBots(runRounds)}>Run now</button>
      </div>
    </section>

    <section className="panel wide-panel">
      <div className="panel-title"><h2>The Rivals</h2><span>Personality and playing habits</span></div>
      {rosterError && <div className="error banner"><span>{rosterError}</span></div>}
      {roster.length === 0 && !rosterError && <p className="coming">No AI rivals yet.</p>}
      {roster.length > 0 && <div className="admin-table-scroll"><table className="admin-table">
        <thead><tr><th>Name</th><th>Personality</th><th>Net worth</th><th>Idle</th><th>Habits</th><th>State</th><th /></tr></thead>
        <tbody>
          {roster.map(bot => <tr key={bot.playerId} className={rivalRowClass(bot)}>
            <td>{bot.name}</td>
            <td>{bot.personality}</td>
            <td>{money.format(bot.netWorth)}</td>
            <td>{bot.lastActionAtUtc ? `${number.format(bot.minutesIdle)}m` : 'never acted'}</td>
            <td>{bot.habits}</td>
            <td>{bot.isPaused ? 'Paused' : botPresence(bot)}</td>
            <td className="admin-table-actions">
              <button
                className="secondary compact"
                disabled={working === bot.playerId}
                onClick={() => void rivalAction(bot.playerId, () => opsApi.setBotPaused(bot.playerId, !bot.isPaused))}
              >
                {bot.isPaused ? 'Resume' : 'Pause'}
              </button>
              <button
                className="secondary compact"
                disabled={working === bot.playerId || bot.isPaused}
                title={bot.isPaused ? 'Resume them first' : 'Act now, ignoring the cooldown'}
                onClick={() => void rivalAction(bot.playerId, () => opsApi.actNow(bot.playerId))}
              >
                Act now
              </button>
              <button
                className="secondary compact"
                disabled={working === bot.playerId}
                onClick={() => setDirecting(id => id === bot.playerId ? null : bot.playerId)}
              >
                {directing === bot.playerId ? 'Close' : 'Direct'}
              </button>
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

function MiniInventory({ dashboard }: { dashboard: Dashboard }) {
  return <div className="mini-inventory">
    <StatusRow label="Condoms" value={number.format(dashboard.condoms)} />
    <StatusRow label="Beer" value={number.format(dashboard.beer)} />
    <StatusRow label="Weapons" value={number.format(dashboard.weapons)} warn={dashboard.weapons < dashboard.thugs} />
    <StatusRow label="Weed" value={number.format(dashboard.weed)} />
    <StatusRow label="Coke" value={number.format(dashboard.coke)} />
  </div>
}

function Leaderboard({ leaders, currentPlayer }: { leaders: LeaderboardEntry[], currentPlayer: string }) {
  return <div className="leaderboard">
    {leaders.map(l => <div className={l.playerName === currentPlayer ? 'leader me' : 'leader'} key={l.rank}>
      <span>#{l.rank}</span><strong>{l.playerName}</strong><span>{money.format(l.netWorth)}</span>
    </div>)}
  </div>
}

function ActivityList({ entries }: { entries: { id: number, action: string, createdAtUtc: string, summary: string }[] }) {
  return <div className="activity-list">
    {entries.length === 0 && <p className="coming">No activity yet.</p>}
    {entries.map(a => <div className="activity" key={a.id}>
      <div><strong>{a.action}</strong><span>{new Date(a.createdAtUtc).toLocaleString()}</span></div>
      <p>{a.summary}</p>
    </div>)}
  </div>
}

function AdminMetric({ label, value }: { label: string, value: string }) {
  return <div className="admin-metric"><span>{label}</span><strong>{value}</strong></div>
}

const NEWS_LABELS: Record<WorldNewsEntry['category'], string> = {
  combat: 'Fight',
  build: 'Built',
  arrival: 'Arrival',
  crew: 'Crew',
  money: 'Money'
}

function WorldNewsPanel({ news, currentPlayer }: { news: WorldNews, currentPlayer: string }) {
  const entries = news.feed.slice(0, 8)
  return <div className="panel world-panel">
    <div className="panel-title"><h2>World News</h2><span>What is worth knowing</span></div>
    {news.headlines.length > 0 && <div className="headline-grid">
      {news.headlines.map(headline => <div className={`headline headline-${headline.kind}`} key={headline.kind}>
        <strong>{headline.title}</strong>
        <span>{headline.detail}</span>
      </div>)}
    </div>}
    <div className="world-news">
      {entries.length === 0 && <p className="coming">Nothing worth reporting yet. Small moves stay off the page.</p>}
      {entries.map(entry => <div className={entry.playerName === currentPlayer ? 'world-news-item me' : 'world-news-item'} key={entry.id}>
        <div><strong className={`news-tag ${entry.category}`}>{NEWS_LABELS[entry.category] ?? entry.action}</strong><span>{new Date(entry.createdAtUtc).toLocaleString()}</span></div>
        <p>{entry.summary}</p>
        <small>{entry.playerName} / {entry.city}{entry.turnsSpent > 0 ? ` / ${entry.turnsSpent} turn${entry.turnsSpent === 1 ? '' : 's'}` : ''}</small>
      </div>)}
    </div>
  </div>
}

function percent(value: number) {
  return `${(value * 100).toFixed(value < 0.1 ? 1 : 0)}%`
}

function combatProtectionText(status: { isProtected: boolean, protectionUntilUtc?: string | null }) {
  if (!status.isProtected || !status.protectionUntilUtc) return 'None'
  return `Until ${new Date(status.protectionUntilUtc).toLocaleString()}`
}

function attackStatusText(status: { canAttackNow: boolean, eligibility: string, attackTurnCost: number, attackCooldownUntilUtc?: string | null, mismatchReason?: string | null }, missionAgainstTarget?: CombatMission, activeMission?: CombatMission, attackReady = true) {
  if (missionAgainstTarget) return `Mission active, next update in ${timeUntil(nextMissionTime(missionAgainstTarget))}`
  // A mismatch is a hard block, so say why before anything else the player could act on.
  if (status.mismatchReason) return status.mismatchReason
  if (!attackReady) return 'Assign available crew'
  if (activeMission) return `Crew already out, next update in ${timeUntil(nextMissionTime(activeMission))}`
  if (status.canAttackNow) return `${status.attackTurnCost} turns to attack`
  if (status.attackCooldownUntilUtc && status.eligibility === 'Cooldown') return `Cooldown until ${new Date(status.attackCooldownUntilUtc).toLocaleString()}`
  return status.eligibility
}

function commanderNote(pimp: Pimp | null) {
  if (!pimp) return 'Server fields your strongest enforcer'
  return pimp.specialty === 'Enforcer'
    ? `+${pimp.bonusPercent}% attack power while leading`
    : `Hustler: no combat bonus, and their +${pimp.bonusPercent}% street income stays home`
}

function nextMissionTime(mission: CombatMission) {
  return mission.status === 'Traveling'
    ? mission.arrivesAtUtc
    : mission.status === 'Fighting'
      ? mission.nextRoundAtUtc ?? mission.arrivesAtUtc
      : mission.status === 'Returning'
        ? mission.returnsAtUtc ?? mission.arrivesAtUtc
        : mission.completedAtUtc ?? mission.returnsAtUtc ?? mission.arrivesAtUtc
}

function timeUntil(value: string) {
  const seconds = Math.max(0, Math.ceil((new Date(value).getTime() - Date.now()) / 1000))
  const minutes = Math.floor(seconds / 60)
  const remainder = seconds % 60
  // Hideout builds run for hours, where a bare minute count stops being readable.
  if (minutes >= 60) return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m`
  return minutes <= 0 ? `${seconds}s` : `${minutes}m ${String(remainder).padStart(2, '0')}s`
}

function formatBreakdownKey(key: string) {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, value => value.toUpperCase())
}

function formatBreakdownValue(key: string, value: unknown) {
  if (typeof value === 'number') {
    const moneyKey = /gross|payout|profit|cost|price|total|amount/i.test(key)
    if (moneyKey) return money.format(value)
    return Number.isInteger(value) ? number.format(value) : value.toFixed(2)
  }
  return String(value)
}

function DismissibleMessage({ className, children, onClose }: { className: string, children: ReactNode, onClose: () => void }) {
  return <div className={`${className} notification`}>
    <span>{children}</span>
    <button className="dismiss" type="button" aria-label="Close notification" onClick={onClose}>x</button>
  </div>
}

function Stat({ label, value, sub, tone, title }: { label: string, value: string, sub?: string, tone?: string, title?: string }) {
  return <div className={tone ? `stat ${tone}` : 'stat'} title={title}>
    <span>{label}</span>
    <strong>{value}</strong>
    {sub && <small>{sub}</small>}
  </div>
}

// A full storage room is a harder limit than what you currently hold: past it there is nothing to buy,
// and every full-length shift runs a shortage until the room itself is bigger. Warning only, never
// blocking, since a crew built for fighting does not have to be supplyable for street work.
function StorageSupplyNotice({ dashboard }: { dashboard: Dashboard }) {
  const report = dashboard.crewReport
  const hoesOver = dashboard.hoes > report.hoesStorageCanSupply
  const thugsOver = dashboard.thugs > report.thugsStorageCanSupply
  if (!hoesOver && !thugsOver) return null

  const over: string[] = []
  if (hoesOver) over.push(`${number.format(report.hoesStorageCanSupply)} of your ${number.format(dashboard.hoes)} hoes`)
  if (thugsOver) over.push(`${number.format(report.thugsStorageCanSupply)} of your ${number.format(dashboard.thugs)} thugs`)

  return <div className="supply-warning">
    <strong>Your storage room cannot supply this crew</strong>
    <span>
      Even completely full, a level {dashboard.hideout.storageLevel} room carries {over.join(' and ')} through a
      full-length street action. Every shift past that runs a shortage and morale falls.
      {report.storageLevelToSupplyCrew
        ? ` A level ${report.storageLevelToSupplyCrew} storage room would cover them.`
        : ' No storage room in the game is big enough for a crew this size.'}
    </span>
  </div>
}

function CrewCard({ name, count, desc, tone, cap, trend }: { name: string, count: number, desc: string, tone?: string, cap?: number, trend?: ReactNode }) {
  return <div className={`crew-card ${tone ?? ''}`}>
    <span>{name}</span>
    <strong>{number.format(count)}{cap !== undefined && <small> / {number.format(cap)}</small>}</strong>
    <p>{desc}{trend}</p>
  </div>
}

function InventoryCard({ name, count, note }: { name: string, count: number, note: string }) {
  return <div className="inventory-card"><span>{name}</span><strong>{number.format(count)}</strong><small>{note}</small></div>
}

function CrewManageRow({ label, owned, quantity, hireCost, cash, busy, canHire = true, canFire, onQuantity, onHire, onFire, note }: {
  label: string
  owned: number
  quantity: number
  hireCost: number
  cash: number
  busy: boolean
  canHire?: boolean
  canFire: boolean
  onQuantity: (quantity: number) => void
  onHire: () => void
  onFire: () => void
  note: string
}) {
  const totalCost = quantity * hireCost
  return <div className="crew-manage-row">
    <div><strong>{label}</strong><span>{number.format(owned)} owned | {money.format(hireCost)} each | {note}</span></div>
    <input aria-label={`${label} quantity`} type="number" min={1} max={1000} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <button className="primary compact" disabled={busy || quantity < 1 || !canHire || cash < totalCost} onClick={onHire}>Hire</button>
    <button className="secondary compact" disabled={busy || quantity < 1 || !canFire} onClick={onFire}>Fire</button>
  </div>
}

function SellRow({ name, owned, price, quantity, onQuantity, onSell, disabled }: {
  name: string
  owned: number
  price: number
  quantity: number
  onQuantity: (quantity: number) => void
  onSell: () => void
  disabled: boolean
}) {
  return <div className="sell-row">
    <div><strong>{name}</strong><span>{number.format(owned)} owned | {money.format(price)} each</span></div>
    <input type="number" min={1} max={Math.max(1, owned)} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <button className="secondary compact" disabled={disabled || quantity < 1 || quantity > owned} onClick={onSell}>Sell</button>
  </div>
}

function StatusRow({ label, value, warn, trend }: { label: string, value: string, warn?: boolean, trend?: ReactNode }) {
  return <div className={`status-row ${warn ? 'warn' : ''}`}>
    <span>{label}</span>
    <strong>{value}{trend}</strong>
  </div>
}

const MORALE_ARROWS: Record<MoraleDirection, string> = { up: '▲', down: '▼', steady: '–', unknown: '' }

// An arrow is a claim about the past, so it only appears when the server actually has a baseline to
// compare against. No recent activity reports nothing rather than a flat line nobody earned.
function MoraleArrow({ trend, crew }: { trend: MoraleTrend, crew: 'hoe' | 'thug' }) {
  const direction = crew === 'hoe' ? trend.hoeDirection : trend.thugDirection
  const delta = crew === 'hoe' ? trend.hoeDelta : trend.thugDelta
  if (direction === 'unknown') return null

  const sign = delta !== null && delta !== undefined && delta > 0 ? '+' : ''
  const title = direction === 'steady'
    ? 'Steady since your last action'
    : `${sign}${delta?.toFixed(1)} since your last action`
  return <em className={`morale-arrow ${direction}`} title={title}>{MORALE_ARROWS[direction]}</em>
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
