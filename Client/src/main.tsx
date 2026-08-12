import React, { FormEvent, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { api } from './api'
import type { ActionResult, AdminOverview, CombatLog, CombatMission, Dashboard, LeaderboardEntry, PlayerProfile, PlayerTarget, WorldNewsEntry } from './api'
import './styles.css'

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('en-US')

type AdminCheatKey = 'cash' | 'bank' | 'turns' | 'pimps' | 'hoes' | 'thugs' | 'condoms' | 'beer' | 'weapons' | 'weed' | 'coke' | 'morale'
type AppPage = 'overview' | 'street' | 'crew' | 'market' | 'recon' | 'admin'

const adminCheatOptions: { key: AdminCheatKey, label: string, amount: number }[] = [
  { key: 'cash', label: '+$10k Cash', amount: 10_000 },
  { key: 'bank', label: '+$10k Bank', amount: 10_000 },
  { key: 'turns', label: '+50 Turns', amount: 50 },
  { key: 'pimps', label: '+5 Pimps', amount: 5 },
  { key: 'hoes', label: '+25 Hoes', amount: 25 },
  { key: 'thugs', label: '+10 Thugs', amount: 10 },
  { key: 'condoms', label: '+100 Condoms', amount: 100 },
  { key: 'beer', label: '+100 Beer', amount: 100 },
  { key: 'weapons', label: '+10 Weapons', amount: 10 },
  { key: 'weed', label: '+250 Weed', amount: 250 },
  { key: 'coke', label: '+100 Coke', amount: 100 },
  { key: 'morale', label: 'Morale 100%', amount: 100 },
]

const pageMeta: Record<AppPage, { label: string, short: string, kicker: string }> = {
  overview: { label: 'Overview', short: 'OV', kicker: 'Command center' },
  street: { label: 'Street', short: 'ST', kicker: 'Turns and cash' },
  crew: { label: 'Crew', short: 'CR', kicker: 'Morale and hiring' },
  market: { label: 'Market', short: 'MK', kicker: 'Store, product, bank' },
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
  const [worldNews, setWorldNews] = useState<WorldNewsEntry[]>([])
  const [targetQuery, setTargetQuery] = useState('')
  const [activePage, setActivePage] = useState<AppPage>('overview')
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [lastBreakdown, setLastBreakdown] = useState<Record<string, unknown> | null>(null)
  const [busy, setBusy] = useState(false)
  const [streetTurns, setStreetTurns] = useState(5)
  const [productionTurns, setProductionTurns] = useState(5)
  const [hoeCut, setHoeCut] = useState(30)
  const [bankAmount, setBankAmount] = useState(1000)
  const [crewQty, setCrewQty] = useState<Record<'pimps' | 'hoes' | 'thugs', number>>({ pimps: 1, hoes: 1, thugs: 1 })
  const [attackCrew, setAttackCrew] = useState({ pimps: 1, thugs: 1, weapons: 0 })
  // Left empty so each page derives its own default until the player types a quantity.
  const [storeQty, setStoreQty] = useState<Record<string, number>>({})
  const [sellQty, setSellQty] = useState<Record<'weed' | 'coke', number>>({ weed: 10, coke: 5 })
  const [tickSeconds, setTickSeconds] = useState(0)

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
        setWorldNews([])
        setCombatLogs([])
        setCombatMissions([])
        setTargets([])
        setSelectedTarget(null)
        setActivePage('overview')
      } else setError((e as Error).message)
    }
  }

  useEffect(() => { void refresh() }, [])
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
    if (!dashboard || !combatMissions.some(mission => mission.status !== 'Complete')) return
    const timer = window.setInterval(() => { void refresh() }, 5000)
    return () => window.clearInterval(timer)
  }, [dashboard?.playerId, combatMissions])

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
        await api.register(String(form.get('username')), String(form.get('password')), String(form.get('playerName')))
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
    await act(() => api.attack(defenderId, attackCrew.pimps, attackCrew.thugs, attackCrew.weapons))
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
    productionTurns,
    hoeCut,
    bankAmount,
    crewQty,
    attackCrew,
    storeQty,
    sellQty,
    nextTurn,
    totalCrew,
    weaponCoverage,
    managementCapacity,
    setActivePage,
    setTargetQuery,
    setStreetTurns,
    setProductionTurns,
    setHoeCut,
    setBankAmount,
    setCrewQty,
    setAttackCrew,
    setStoreQty,
    setSellQty,
    act,
    searchTargets,
    inspectTarget,
    attackTarget: defenderId => void attackTarget(defenderId),
    cancelMission: missionId => void act(() => api.cancelCombatMission(missionId)),
    runAdminCheat: (cheat, amount) => void act(() => api.adminCheat(cheat, amount)),
    seedBots: count => void act(() => api.adminSeedBots(count)),
    runBots: rounds => void act(() => api.adminRunBots(rounds)),
    setBotAutomation: enabled => void act(() => api.adminSetBotAutomation(enabled)),
  }

  return <main className="game-shell">
    <aside className="app-nav">
      <div className="nav-brand"><span>SE</span><strong>Street Empire</strong><small>0.2.1</small></div>
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
        <div className="player-plate">
          <strong>{dashboard.name}</strong>
          <span>{dashboard.city} / Rank #{dashboard.rank}</span>
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
  worldNews: WorldNewsEntry[]
  combatLogs: CombatLog[]
  combatMissions: CombatMission[]
  targetQuery: string
  busy: boolean
  streetTurns: number
  productionTurns: number
  hoeCut: number
  bankAmount: number
  crewQty: Record<'pimps' | 'hoes' | 'thugs', number>
  attackCrew: { pimps: number, thugs: number, weapons: number }
  storeQty: Record<string, number>
  sellQty: Record<'weed' | 'coke', number>
  nextTurn: string
  totalCrew: number
  weaponCoverage: number
  managementCapacity: number
  setActivePage: (page: AppPage) => void
  setTargetQuery: (query: string) => void
  setStreetTurns: (turns: number) => void
  setProductionTurns: (turns: number) => void
  setHoeCut: (cut: number) => void
  setBankAmount: (amount: number) => void
  setCrewQty: React.Dispatch<React.SetStateAction<Record<'pimps' | 'hoes' | 'thugs', number>>>
  setAttackCrew: React.Dispatch<React.SetStateAction<{ pimps: number, thugs: number, weapons: number }>>
  setStoreQty: React.Dispatch<React.SetStateAction<Record<string, number>>>
  setSellQty: React.Dispatch<React.SetStateAction<Record<'weed' | 'coke', number>>>
  act: (fn: () => Promise<ActionResult | unknown>) => Promise<void>
  searchTargets: (event: FormEvent<HTMLFormElement>) => void
  inspectTarget: (playerId: string) => void
  attackTarget: (defenderId: string) => void
  cancelMission: (missionId: number) => void
  runAdminCheat: (cheat: AdminCheatKey, amount: number) => void
  seedBots: (count: number) => void
  runBots: (rounds: number) => void
  setBotAutomation: (enabled: boolean) => void
}

function renderPage(page: AppPage, ctx: PageContext) {
  switch (page) {
    case 'street': return <StreetPage {...ctx} />
    case 'crew': return <CrewPage {...ctx} />
    case 'market': return <MarketPage {...ctx} />
    case 'recon': return <ReconPage {...ctx} />
    case 'admin': return ctx.adminOverview
      ? <AdminPage {...ctx} overview={ctx.adminOverview} />
      : <OverviewPage {...ctx} />
    default: return <OverviewPage {...ctx} />
  }
}

function OverviewPage(ctx: PageContext) {
  const { dashboard, leaders, worldNews, totalCrew, weaponCoverage, managementCapacity, setActivePage } = ctx
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

      <NextMovePanel dashboard={dashboard} weaponCoverage={weaponCoverage} managementCapacity={managementCapacity} onPage={setActivePage} />

      <section className="panel">
        <div className="panel-title"><h2>Inventory</h2><span>On hand</span></div>
        <MiniInventory dashboard={dashboard} />
      </section>
    </div>

    <div className="overview-stack">
      <section className="panel">
        <div className="panel-title"><h2>Readiness</h2><span>Combat prep</span></div>
        <StatusRow label="Hoe morale" value={`${dashboard.hoeHappiness.toFixed(0)}%`} warn={dashboard.hoeHappiness < 40} />
        <StatusRow label="Thug morale" value={`${dashboard.thugHappiness.toFixed(0)}%`} warn={dashboard.thugHappiness < 40} />
        <StatusRow label="Management" value={`${dashboard.hoes}/${managementCapacity} hoes`} warn={dashboard.hoes > managementCapacity} />
        <StatusRow label="Armed thugs" value={`${Math.min(dashboard.weapons, dashboard.thugs)}/${dashboard.thugs}`} warn={dashboard.weapons < dashboard.thugs} />
        <StatusRow label="Weapon coverage" value={`${weaponCoverage.toFixed(0)}%`} warn={weaponCoverage < 75} />
        <StatusRow label="Combat status" value={dashboard.combatStatus.eligibility} warn={dashboard.combatStatus.isProtected} />
        <StatusRow label="20-turn condoms" value={`${dashboard.condoms}/${dashboard.crewReport.condomsNeededForMaxStreetAction}`} warn={dashboard.condoms < dashboard.crewReport.condomsNeededForMaxStreetAction} />
        <StatusRow label="20-turn beer" value={`${dashboard.beer}/${dashboard.crewReport.beerNeededForMaxStreetAction}`} warn={dashboard.beer < dashboard.crewReport.beerNeededForMaxStreetAction} />
      </section>

      <section className="panel">
        <div className="panel-title"><h2>Top Players</h2><span>Net worth</span></div>
        <Leaderboard leaders={leaders.slice(0, 8)} currentPlayer={dashboard.name} />
      </section>
    </div>

    <WorldNewsPanel entries={worldNews.slice(0, 5)} currentPlayer={dashboard.name} />
  </div>
}

function StreetPage(ctx: PageContext) {
  const { dashboard, combatMissions, busy, streetTurns, hoeCut, bankAmount, storeQty, setActivePage, setStreetTurns, setHoeCut, setBankAmount, setStoreQty, act } = ctx
  const pendingOutgoingAttack = combatMissions.find(mission => mission.attackerId === dashboard.playerId && mission.status !== 'Complete')
  return <div className="page-grid two-column">
    <section className="panel wide-panel">
      <div className="panel-title"><h2>Work the Streets</h2><span>Income + recruiting</span></div>
      <p>Your hoes generate gross income. Their cut is paid before your cash is deposited on hand. Street work can also recruit crew and turn up small amounts of inventory.</p>
      {pendingOutgoingAttack && <div className="mission-lock">
        <strong>Crew is out</strong>
        <span>Street work unlocks after the next mission update in {timeUntil(nextMissionTime(pendingOutgoingAttack))}.</span>
      </div>}
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
        <button className="primary" disabled={busy || !!pendingOutgoingAttack || streetTurns < 1 || streetTurns > dashboard.turns || streetTurns > dashboard.maxActionTurns} onClick={() => void act(() => api.workStreet(streetTurns))}>{pendingOutgoingAttack ? 'Crew Out' : `Work ${streetTurns} Turn${streetTurns === 1 ? '' : 's'}`}</button>
      </div>
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
      <div className="crew-grid">
        <CrewCard name="Pimps" count={dashboard.pimps} desc={`Manage up to ${number.format(managementCapacity)} hoes.`} />
        <CrewCard name="Hoes" count={dashboard.hoes} desc={`${dashboard.hoeHappiness.toFixed(0)}% morale / ${dashboard.hoeCutPercent}% cut`} tone={moraleTone(dashboard.hoeHappiness)} />
        <CrewCard name="Thugs" count={dashboard.thugs} desc={`${dashboard.thugHappiness.toFixed(0)}% morale / ${weaponCoverage.toFixed(0)}% armed`} tone={moraleTone(dashboard.thugHappiness)} />
      </div>
      <div className="crew-combat-strip">
        <AdminMetric label="Free pimps" value={number.format(combatCrew.availablePimps)} />
        <AdminMetric label="Free thugs" value={number.format(combatCrew.availableThugs)} />
        <AdminMetric label="Free weapons" value={number.format(combatCrew.availableWeapons)} />
        <AdminMetric label="Committed" value={`${number.format(combatCrew.committedPimps)} P / ${number.format(combatCrew.committedThugs)} T / ${number.format(combatCrew.committedWeapons)} W`} />
        <AdminMetric label="Attack slots" value={`${combatCrew.activeAttackMissions}/${combatCrew.maxActiveAttackMissions}`} />
      </div>
    </section>

    <HideoutMoralePanel dashboard={dashboard} busy={busy} act={act} />

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
  </div>
}

function MarketPage(ctx: PageContext) {
  const { dashboard, busy, productionTurns, bankAmount, storeQty, sellQty, setProductionTurns, setBankAmount, setStoreQty, setSellQty, act } = ctx
  return <div className="market-page">
    <section className="panel wide-panel">
      <div className="panel-title"><h2>Inventory</h2><span>Supplies + product</span></div>
      <div className="inventory-grid">
        <InventoryCard name="Condoms" count={dashboard.condoms} note="Hoe upkeep" />
        <InventoryCard name="Beer" count={dashboard.beer} note="Thug upkeep" />
        <InventoryCard name="Weapons" count={dashboard.weapons} note="Permanent security" />
        <InventoryCard name="Weed" count={dashboard.weed} note={`${money.format(dashboard.weedSellPrice)} street price`} />
        <InventoryCard name="Coke" count={dashboard.coke} note={`${money.format(dashboard.cokeSellPrice)} street price`} />
      </div>
    </section>

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
        <p>Turn cash-on-hand into inventory, then sell product at fixed 0.2.1 street prices.</p>
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
  const attacking = mission.attackerId === currentPlayerId
  const nextAt = nextMissionTime(mission)
  const title = attacking ? `${mission.attackerName} -> ${mission.defenderName}` : `${mission.attackerName} attacking you`
  const canCancel = attacking && !compact && mission.canCancel && onCancel
  return <div className={`mission-card ${mission.status.toLowerCase()}`}>
    <div className="mission-head">
      <div><strong>{title}</strong><span>{mission.status} / {mission.outcome}</span></div>
      <b>{mission.status === 'Complete' ? 'Done' : timeUntil(nextAt)}</b>
    </div>
    {!compact && <div className="mission-stats">
      <AdminMetric label="Sent" value={`${mission.assignedPimps} P / ${mission.assignedThugs} T / ${mission.assignedWeapons} W`} />
      <AdminMetric label="Remaining" value={`${mission.remainingAttackers} T / ${mission.remainingWeapons} W`} />
      <AdminMetric label="Round" value={`${mission.currentRound}/${mission.maxRounds}`} />
      <AdminMetric label="Morale" value={`${mission.attackerMorale.toFixed(0)} / ${mission.defenderMorale.toFixed(0)}`} />
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
    <div className="mission-events">
      {mission.events.length === 0 && <small>No updates yet.</small>}
      {mission.events.map(event => <div className="mission-event" key={event.id}>
        <strong>{event.kind}{event.round > 0 ? ` ${event.round}` : ''}</strong>
        <span>{new Date(event.createdAtUtc).toLocaleTimeString()}</span>
        <p>{event.summary}</p>
      </div>)}
    </div>
  </div>
}

function AdminPage(ctx: PageContext & { overview: AdminOverview }) {
  return <AdminPanel
    overview={ctx.overview}
    busy={ctx.busy}
    onCheat={ctx.runAdminCheat}
    onSeedBots={ctx.seedBots}
    onRunBots={ctx.runBots}
    onSetBotAutomation={ctx.setBotAutomation}
  />
}

function StatusStrip({ dashboard, nextTurn }: { dashboard: Dashboard, nextTurn: string }) {
  return <section className="status-strip">
    <Stat label="Cash" value={money.format(dashboard.cash)} />
    <Stat label="Bank" value={money.format(dashboard.bankCash)} />
    <Stat label="Net Worth" value={money.format(dashboard.netWorth)} />
    <Stat label="Turns" value={`${dashboard.turns} / ${dashboard.maxTurns}`} sub={nextTurn === 'MAX' ? 'Turn bank full' : `+${dashboard.turnsPerTick} in ${nextTurn}`} />
    <Stat label="Rank" value={`#${dashboard.rank}`} />
    <Stat label="City" value={dashboard.city} />
  </section>
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
  const plannedTurns = Math.max(1, Math.min(streetTurns, dashboard.maxActionTurns))
  const turnLabel = `${plannedTurns} turn${plannedTurns === 1 ? '' : 's'}`
  const report = dashboard.crewReport
  const forPlannedTurns = (maxActionNeed: number) => Math.ceil(maxActionNeed * plannedTurns / dashboard.maxActionTurns)
  const catalog = new Map(dashboard.store.map(item => [item.key, item] as const))
  const supplies = [
    { key: 'condoms', owned: dashboard.condoms, needed: forPlannedTurns(report.condomsNeededForMaxStreetAction), basis: `to work ${turnLabel}` },
    { key: 'beer', owned: dashboard.beer, needed: forPlannedTurns(report.beerNeededForMaxStreetAction), basis: `to work ${turnLabel}` },
    // Weapons are permanent coverage, so their requirement is the crew size rather than the turns.
    { key: 'weapons', owned: dashboard.weapons, needed: dashboard.thugs, basis: 'to arm every thug' },
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
        // Falls back to the shortfall, so the quantity tracks what this action actually needs.
        const qty = storeQty[supply.key] ?? Math.max(1, short)
        const total = qty * item.price
        return <div className={short > 0 ? 'supply-row short' : 'supply-row'} key={supply.key}>
          <div className="supply-copy">
            <strong>{item.name}</strong>
            <span>{number.format(supply.owned)} on hand / {number.format(supply.needed)} {supply.basis}</span>
          </div>
          <em>{short > 0 ? `${number.format(short)} short` : 'Covered'}</em>
          <label>Qty<input aria-label={`${item.name} quantity`} type="number" min={1} max={10000} value={qty} onChange={event => setStoreQty(value => ({ ...value, [supply.key]: Number(event.target.value) }))} /></label>
          <button
            className="primary"
            disabled={busy || qty < 1 || dashboard.cash < total}
            onClick={() => void act(() => api.buyStoreItem(supply.key, qty))}
          >
            Buy {money.format(total)}
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

function NextMovePanel({ dashboard, weaponCoverage, managementCapacity, onPage }: {
  dashboard: Dashboard
  weaponCoverage: number
  managementCapacity: number
  onPage: (page: AppPage) => void
}) {
  const moves: { label: string, detail: string, page: AppPage, urgent?: boolean }[] = [
    {
      label: dashboard.turns > 0 ? 'Spend turns' : 'Wait for turns',
      detail: dashboard.turns > 0 ? `${dashboard.turns} turn${dashboard.turns === 1 ? '' : 's'} ready for street work or production.` : 'Your turn bank is empty.',
      page: dashboard.turns > 0 ? 'street' : 'overview',
      urgent: dashboard.turns >= dashboard.maxActionTurns,
    },
    {
      label: 'Crew pressure',
      detail: dashboard.hoes > managementCapacity ? `${dashboard.hoes - managementCapacity} unmanaged hoes need more pimps.` : `Management is stable at ${dashboard.hoes}/${managementCapacity} hoes.`,
      page: 'crew',
      urgent: dashboard.hoes > managementCapacity,
    },
    {
      label: 'Supply reserve',
      detail: `${dashboard.condoms}/${dashboard.crewReport.condomsNeededForMaxStreetAction} condoms, ${dashboard.beer}/${dashboard.crewReport.beerNeededForMaxStreetAction} beer for a max street action.`,
      page: 'market',
      urgent: dashboard.condoms < dashboard.crewReport.condomsNeededForMaxStreetAction || dashboard.beer < dashboard.crewReport.beerNeededForMaxStreetAction,
    },
    {
      label: 'Combat posture',
      detail: `${weaponCoverage.toFixed(0)}% thug weapon coverage for combat.`,
      page: 'recon',
      urgent: weaponCoverage < 75,
    },
  ]

  return <section className="panel">
    <div className="panel-title"><h2>Next Moves</h2><span>Flow</span></div>
    <div className="flow-list">
      {moves.map(move => <button className={move.urgent ? 'flow-row urgent' : 'flow-row'} type="button" key={move.label} onClick={() => onPage(move.page)}>
        <strong>{move.label}</strong>
        <span>{move.detail}</span>
      </button>)}
    </div>
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
    <div className="panel-title"><h2>Trap House</h2><span>Hideout morale</span></div>
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

function TargetReconPanel({ targets, selectedTarget, query, busy, currentPlayerId, combatMissions, dashboard, attackCrew, setAttackCrew, onQuery, onSearch, onInspect, onAttack }: {
  targets: PlayerTarget[]
  selectedTarget: PlayerProfile | null
  query: string
  busy: boolean
  currentPlayerId: string
  combatMissions: CombatMission[]
  dashboard: Dashboard
  attackCrew: { pimps: number, thugs: number, weapons: number }
  setAttackCrew: React.Dispatch<React.SetStateAction<{ pimps: number, thugs: number, weapons: number }>>
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
  const attackReady = attackCrew.pimps >= 1
    && attackCrew.thugs >= 1
    && attackCrew.weapons >= 0
    && attackCrew.weapons <= attackCrew.thugs
    && attackCrew.pimps <= crew.availablePimps
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
          <em>{target.combatStatus.eligibility} / {target.combatReadiness.riskBand}</em>
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
            <label>Pimps<input type="number" min={1} max={Math.max(1, crew.availablePimps)} value={attackCrew.pimps} onChange={e => setAttackCrew(value => ({ ...value, pimps: Number(e.target.value) }))} /></label>
            <label>Thugs<input type="number" min={1} max={Math.max(1, crew.availableThugs)} value={attackCrew.thugs} onChange={e => setAttackCrew(value => ({ ...value, thugs: Number(e.target.value), weapons: Math.min(value.weapons, Number(e.target.value)) }))} /></label>
            <label>Weapons<input type="number" min={0} max={Math.max(0, Math.min(crew.availableWeapons, attackCrew.thugs))} value={attackCrew.weapons} onChange={e => setAttackCrew(value => ({ ...value, weapons: Number(e.target.value) }))} /></label>
          </div>
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

function AdminPanel({ overview, busy, onCheat, onSeedBots, onRunBots, onSetBotAutomation }: {
  overview: AdminOverview
  busy: boolean
  onCheat: (cheat: AdminCheatKey, amount: number) => void
  onSeedBots: (count: number) => void
  onRunBots: (rounds: number) => void
  onSetBotAutomation: (enabled: boolean) => void
}) {
  const [collapsed, setCollapsed] = useState(false)
  const [customCheat, setCustomCheat] = useState<AdminCheatKey>('cash')
  const [customAmount, setCustomAmount] = useState(10000)
  const [botSeedCount, setBotSeedCount] = useState(10)
  const [botRunRounds, setBotRunRounds] = useState(1)
  const game = overview.economy
  return <div className="panel admin-panel">
    <div className="panel-title admin-title">
      <div><h2>Admin Control Center</h2><span>0.2.1 war room</span></div>
      <button
        className="secondary compact admin-toggle"
        type="button"
        aria-expanded={!collapsed}
        aria-controls="admin-control-center-body"
        onClick={() => setCollapsed(value => !value)}
      >
        {collapsed ? 'Show' : 'Hide'}
      </button>
    </div>
    {!collapsed && <div id="admin-control-center-body" className="admin-body">
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
      <div className="admin-cheats">
        <div className="admin-subtitle"><strong>Cheats</strong><span>Admin-only, audited as ADMIN actions</span></div>
        <div className="admin-cheat-grid">
          {adminCheatOptions.map(option =>
            <button className="secondary compact" disabled={busy} key={option.key} onClick={() => onCheat(option.key, option.amount)}>
              {option.label}
            </button>
          )}
        </div>
        <div className="admin-cheat-custom">
          <label>Cheat<select value={customCheat} onChange={event => setCustomCheat(event.target.value as AdminCheatKey)}>
            {adminCheatOptions.map(option => <option key={option.key} value={option.key}>{option.key}</option>)}
          </select></label>
          <label>Amount<input type="number" min={1} max={1000000000} value={customAmount} onChange={event => setCustomAmount(Number(event.target.value))} /></label>
          <button className="primary compact" disabled={busy || customAmount < 1 || customAmount > 1_000_000_000} onClick={() => onCheat(customCheat, customAmount)}>Apply Cheat</button>
        </div>
      </div>
      <div className="admin-bots">
        <div className="admin-subtitle"><strong>AI Rivals</strong><span>Seed test opponents for 0.2.0</span></div>
        <div className="admin-bot-controls">
          <label>Count<input type="number" min={1} max={15} value={botSeedCount} onChange={event => setBotSeedCount(Number(event.target.value))} /></label>
          <button className="secondary compact" disabled={busy} onClick={() => setBotSeedCount(5)}>5</button>
          <button className="secondary compact" disabled={busy} onClick={() => setBotSeedCount(10)}>10</button>
          <button className="secondary compact" disabled={busy} onClick={() => setBotSeedCount(15)}>15</button>
          <button className="primary compact" disabled={busy || botSeedCount < 1 || botSeedCount > 15} onClick={() => onSeedBots(botSeedCount)}>Seed AI Players</button>
        </div>
        <div className="admin-bot-controls">
          <label>Rounds<input type="number" min={1} max={10} value={botRunRounds} onChange={event => setBotRunRounds(Number(event.target.value))} /></label>
          <button className="secondary compact" disabled={busy} onClick={() => setBotRunRounds(1)}>1</button>
          <button className="secondary compact" disabled={busy} onClick={() => setBotRunRounds(3)}>3</button>
          <button className="secondary compact" disabled={busy} onClick={() => setBotRunRounds(10)}>10</button>
          <button className="primary compact" disabled={busy || overview.botAccounts < 1 || botRunRounds < 1 || botRunRounds > 10} onClick={() => onRunBots(botRunRounds)}>Run AI</button>
        </div>
        <div className="admin-bot-controls automation">
          <div className="admin-bot-status">
            <strong>{overview.botAutomation.enabled ? 'Automatic AI On' : 'Automatic AI Off'}</strong>
            <span>Every {overview.botAutomation.tickSeconds}s / {overview.botAutomation.roundsPerTick} round{overview.botAutomation.roundsPerTick === 1 ? '' : 's'} per tick</span>
          </div>
          <button
            className={overview.botAutomation.enabled ? 'secondary compact' : 'primary compact'}
            disabled={busy || overview.botAccounts < 1}
            onClick={() => onSetBotAutomation(!overview.botAutomation.enabled)}
          >
            {overview.botAutomation.enabled ? 'Turn Off Automatic AI' : 'Turn On Automatic AI'}
          </button>
        </div>
      </div>
    </div>}
  </div>
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

function WorldNewsPanel({ entries, currentPlayer }: { entries: WorldNewsEntry[], currentPlayer: string }) {
  return <div className="panel world-panel">
    <div className="panel-title"><h2>World News</h2><span>Last {entries.length}</span></div>
    <div className="world-news">
      {entries.length === 0 && <p className="coming">No citywide activity yet.</p>}
      {entries.map(entry => <div className={entry.playerName === currentPlayer ? 'world-news-item me' : 'world-news-item'} key={entry.id}>
        <div><strong>{entry.action}</strong><span>{new Date(entry.createdAtUtc).toLocaleString()}</span></div>
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

function attackStatusText(status: { canAttackNow: boolean, eligibility: string, attackTurnCost: number, attackCooldownUntilUtc?: string | null }, missionAgainstTarget?: CombatMission, activeMission?: CombatMission, attackReady = true) {
  if (missionAgainstTarget) return `Mission active, next update in ${timeUntil(nextMissionTime(missionAgainstTarget))}`
  if (!attackReady) return 'Assign available crew'
  if (activeMission) return `Crew already out, next update in ${timeUntil(nextMissionTime(activeMission))}`
  if (status.canAttackNow) return `${status.attackTurnCost} turns to attack`
  if (status.attackCooldownUntilUtc && status.eligibility === 'Cooldown') return `Cooldown until ${new Date(status.attackCooldownUntilUtc).toLocaleString()}`
  return status.eligibility
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

function Stat({ label, value, sub }: { label: string, value: string, sub?: string }) {
  return <div className="stat">
    <span>{label}</span>
    <strong>{value}</strong>
    {sub && <small>{sub}</small>}
  </div>
}

function CrewCard({ name, count, desc, tone }: { name: string, count: number, desc: string, tone?: string }) {
  return <div className={`crew-card ${tone ?? ''}`}><span>{name}</span><strong>{number.format(count)}</strong><p>{desc}</p></div>
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

function StatusRow({ label, value, warn }: { label: string, value: string, warn?: boolean }) {
  return <div className={`status-row ${warn ? 'warn' : ''}`}><span>{label}</span><strong>{value}</strong></div>
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
