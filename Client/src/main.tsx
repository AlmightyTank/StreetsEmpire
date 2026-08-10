import React, { FormEvent, useEffect, useMemo, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import { api } from './api'
import type { ActionResult, AdminOverview, Dashboard, LeaderboardEntry, WorldNewsEntry } from './api'
import './styles.css'

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('en-US')

type AdminCheatKey = 'cash' | 'bank' | 'turns' | 'pimps' | 'hoes' | 'thugs' | 'condoms' | 'beer' | 'weapons' | 'weed' | 'coke' | 'morale'

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

function App() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [adminOverview, setAdminOverview] = useState<AdminOverview | null>(null)
  const [leaders, setLeaders] = useState<LeaderboardEntry[]>([])
  const [worldNews, setWorldNews] = useState<WorldNewsEntry[]>([])
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
  const [storeQty, setStoreQty] = useState<Record<string, number>>({ condoms: 25, beer: 12, weapons: 1 })
  const [sellQty, setSellQty] = useState<Record<'weed' | 'coke', number>>({ weed: 10, coke: 5 })
  const [tickSeconds, setTickSeconds] = useState(0)
  const summaryRef = useRef<HTMLElement | null>(null)

  const refresh = async () => {
    try {
      const [d, l, news] = await Promise.all([api.dashboard(), api.leaderboard(), api.worldNews()])
      const admin = d.isAdmin ? await api.adminOverview() : null
      setDashboard(d)
      setAdminOverview(admin)
      setLeaders(l)
      setWorldNews(news)
      setTickSeconds(d.secondsUntilNextTurnTick)
      setHoeCut(d.hoeCutPercent)
      setError('')
    } catch (e) {
      if ((e as Error).message === 'Unauthorized') { setDashboard(null); setAdminOverview(null); setWorldNews([]) }
      else setError((e as Error).message)
    }
  }

  useEffect(() => { void refresh() }, [])
  useEffect(() => {
    const element = summaryRef.current
    if (!element) return

    const setHeight = () => {
      document.documentElement.style.setProperty('--summary-stack-height', `${Math.ceil(element.getBoundingClientRect().height)}px`)
    }
    setHeight()

    const observer = new ResizeObserver(setHeight)
    observer.observe(element)
    window.addEventListener('resize', setHeight)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', setHeight)
    }
  }, [dashboard?.playerId, error, notice, lastBreakdown])
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
          <button className="primary" disabled={busy}>{busy ? 'Working…' : authMode === 'login' ? 'Enter the City' : 'Build My Empire'}</button>
        </form>
      </section>
    </main>
  }

  const totalCrew = dashboard.pimps + dashboard.hoes + dashboard.thugs
  const weaponCoverage = dashboard.thugs === 0 ? 100 : Math.min(100, (dashboard.weapons / dashboard.thugs) * 100)
  const managementCapacity = dashboard.crewReport.managementCapacity

  return <main className="game-shell">
    <header className="topbar">
      <div><strong>STREET EMPIRE</strong><span className="version">0.1.10</span></div>
      <div className="top-actions"><span>{dashboard.name}</span><button onClick={() => void act(api.logout)}>Logout</button></div>
    </header>

    <section ref={summaryRef} className="summary-stack">
      <div className="stats-grid">
        <Stat icon={<CashIcon />} label="Cash on Hand" value={money.format(dashboard.cash)} />
        <Stat icon={<BankIcon />} label="Bank" value={money.format(dashboard.bankCash)} />
        <Stat icon={<WorthIcon />} label="Net Worth" value={money.format(dashboard.netWorth)} />
        <Stat icon={<TurnsIcon />} label="Turns" value={`${dashboard.turns} / ${dashboard.maxTurns}`} sub={nextTurn === 'MAX' ? 'Turn bank full' : `+${dashboard.turnsPerTick} in ${nextTurn}`} />
        <Stat icon={<RankIcon />} label="Rank" value={`#${dashboard.rank}`} />
        <Stat icon={<CityIcon />} label="City" value={dashboard.city} />
      </div>

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

    <div className="layout">
      <section className="main-column">
        {adminOverview && <AdminPanel
          overview={adminOverview}
          busy={busy}
          onCheat={(cheat, amount) => void act(() => api.adminCheat(cheat, amount))}
          onSeedBots={(count) => void act(() => api.adminSeedBots(count))}
          onRunBots={(rounds) => void act(() => api.adminRunBots(rounds))}
          onSetBotAutomation={(enabled) => void act(() => api.adminSetBotAutomation(enabled))}
        />}

        <div className="panel">
          <div className="panel-title"><h2>Your Crew</h2><span>{number.format(totalCrew)} total</span></div>
          <div className="crew-grid">
            <CrewCard name="Pimps" count={dashboard.pimps} desc={`Manage up to ${number.format(managementCapacity)} hoes.`} />
            <CrewCard name="Hoes" count={dashboard.hoes} desc={`${dashboard.hoeHappiness.toFixed(0)}% morale · ${dashboard.hoeCutPercent}% cut`} tone={moraleTone(dashboard.hoeHappiness)} />
            <CrewCard name="Thugs" count={dashboard.thugs} desc={`${dashboard.thugHappiness.toFixed(0)}% morale · ${weaponCoverage.toFixed(0)}% armed`} tone={moraleTone(dashboard.thugHappiness)} />
          </div>
        </div>

        <div className="panel">
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
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Work the Streets</h2><span>Income + recruiting</span></div>
          <p>Your hoes generate gross income. Their cut is paid before your cash is deposited on hand. Street work can also recruit crew and turn up small amounts of inventory.</p>
          <div className="action-row wrap">
            <label>Turns<input type="number" min={1} max={dashboard.maxActionTurns} value={streetTurns} onChange={e => setStreetTurns(Number(e.target.value))} /></label>
            <label>Hoe Cut %<input type="number" min={10} max={80} value={hoeCut} onChange={e => setHoeCut(Number(e.target.value))} /></label>
            <button className="secondary" disabled={busy || hoeCut < 10 || hoeCut > 80 || hoeCut === dashboard.hoeCutPercent} onClick={() => void act(() => api.setHoeCut(hoeCut))}>Save Cut</button>
            <button className="primary" disabled={busy || streetTurns < 1 || streetTurns > dashboard.turns || streetTurns > dashboard.maxActionTurns} onClick={() => void act(() => api.workStreet(streetTurns))}>Work {streetTurns} Turn{streetTurns === 1 ? '' : 's'}</button>
          </div>
          <div className="rule-strip">
            <span>1 pimp manages 10 hoes</span><span>Condoms support hoes</span><span>Beer + weapons support thugs</span>
          </div>
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Inventory</h2><span>Supplies + product</span></div>
          <div className="inventory-grid">
            <InventoryCard name="Condoms" count={dashboard.condoms} note="Hoe upkeep" />
            <InventoryCard name="Beer" count={dashboard.beer} note="Thug upkeep" />
            <InventoryCard name="Weapons" count={dashboard.weapons} note="Permanent security" />
            <InventoryCard name="Weed" count={dashboard.weed} note={`${money.format(dashboard.weedSellPrice)} street price`} />
            <InventoryCard name="Coke" count={dashboard.coke} note={`${money.format(dashboard.cokeSellPrice)} street price`} />
          </div>
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Production</h2><span>Spend turns, build product</span></div>
          <p>Production turns cash-on-hand into inventory. Product can be sold immediately for fixed 0.1.10 street prices.</p>
          <div className="action-row wrap">
            <label>Turns<input type="number" min={1} max={dashboard.maxActionTurns} value={productionTurns} onChange={e => setProductionTurns(Number(e.target.value))} /></label>
            <button className="primary" disabled={busy || productionTurns < 1 || productionTurns > dashboard.turns || productionTurns > dashboard.maxActionTurns} onClick={() => void act(() => api.produce('weed', productionTurns))}>Produce Weed</button>
            <button className="primary" disabled={busy || productionTurns < 1 || productionTurns > dashboard.turns || productionTurns > dashboard.maxActionTurns} onClick={() => void act(() => api.produce('coke', productionTurns))}>Produce Coke</button>
          </div>
          <div className="sell-grid">
            <SellRow name="Weed" owned={dashboard.weed} price={dashboard.weedSellPrice} quantity={sellQty.weed} onQuantity={q => setSellQty(v => ({ ...v, weed: q }))} onSell={() => void act(() => api.sellProduct('weed', sellQty.weed))} disabled={busy} />
            <SellRow name="Coke" owned={dashboard.coke} price={dashboard.cokeSellPrice} quantity={sellQty.coke} onQuantity={q => setSellQty(v => ({ ...v, coke: q }))} onSell={() => void act(() => api.sellProduct('coke', sellQty.coke))} disabled={busy} />
          </div>
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Street Store</h2><span>Cash on hand only</span></div>
          <div className="store-list">
            {dashboard.store.map(item => {
              const qty = storeQty[item.key] ?? 1
              return <div className="store-row" key={item.key}>
                <div><strong>{item.name}</strong><span>{item.category}</span><p>{item.description}</p></div>
                <div className="price">{money.format(item.price)}</div>
                <input aria-label={`${item.name} quantity`} type="number" min={1} max={10000} value={qty} onChange={e => setStoreQty(v => ({ ...v, [item.key]: Number(e.target.value) }))} />
                <button className="primary compact" disabled={busy || qty < 1 || dashboard.cash < qty * item.price} onClick={() => void act(() => api.buyStoreItem(item.key, qty))}>Buy</button>
              </div>
            })}
          </div>
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Bank</h2><span>Protected money foundation</span></div>
          <p>Banked cash still counts toward net worth. PvP protection rules arrive with the combat build.</p>
          <div className="action-row wrap">
            <label>Amount<input type="number" min={1} value={bankAmount} onChange={e => setBankAmount(Number(e.target.value))} /></label>
            <button className="secondary" disabled={busy || bankAmount < 1 || bankAmount > dashboard.cash} onClick={() => void act(() => api.deposit(bankAmount))}>Deposit</button>
            <button className="secondary" disabled={busy || bankAmount < 1 || bankAmount > dashboard.bankCash} onClick={() => void act(() => api.withdraw(bankAmount))}>Withdraw</button>
          </div>
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Activity</h2><span>Last 12 actions</span></div>
          <div className="activity-list">
            {dashboard.recentActivity.map(a => <div className="activity" key={a.id}>
              <div><strong>{a.action}</strong><span>{new Date(a.createdAtUtc).toLocaleString()}</span></div>
              <p>{a.summary}</p>
            </div>)}
          </div>
        </div>
      </section>

      <aside className="side-column sticky">
        <div className="panel">
          <div className="panel-title"><h2>Empire Status</h2><span>0.1.10</span></div>
          <StatusRow label="Hoe morale" value={`${dashboard.hoeHappiness.toFixed(0)}%`} warn={dashboard.hoeHappiness < 40} />
          <StatusRow label="Thug morale" value={`${dashboard.thugHappiness.toFixed(0)}%`} warn={dashboard.thugHappiness < 40} />
          <StatusRow label="Management" value={`${dashboard.hoes}/${managementCapacity} hoes`} warn={dashboard.hoes > managementCapacity} />
          <StatusRow label="Armed thugs" value={`${Math.min(dashboard.weapons, dashboard.thugs)}/${dashboard.thugs}`} warn={dashboard.weapons < dashboard.thugs} />
          <StatusRow label="20-turn condoms" value={`${dashboard.condoms}/${dashboard.crewReport.condomsNeededForMaxStreetAction}`} warn={dashboard.condoms < dashboard.crewReport.condomsNeededForMaxStreetAction} />
          <StatusRow label="20-turn beer" value={`${dashboard.beer}/${dashboard.crewReport.beerNeededForMaxStreetAction}`} warn={dashboard.beer < dashboard.crewReport.beerNeededForMaxStreetAction} />
          <StatusRow label="Supply reserve" value={money.format(dashboard.crewReport.supplyCostForMaxStreetAction)} />
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Top Players</h2><span>Net worth</span></div>
          <div className="leaderboard">
            {leaders.slice(0, 10).map(l => <div className={l.playerName === dashboard.name ? 'leader me' : 'leader'} key={l.rank}>
              <span>#{l.rank}</span><strong>{l.playerName}</strong><span>{money.format(l.netWorth)}</span>
            </div>)}
          </div>
          <p className="coming">PvP still begins in 0.2.0. 0.1.x is locking down the economy first.</p>
        </div>

        <WorldNewsPanel entries={worldNews} currentPlayer={dashboard.name} />
      </aside>
    </div>
  </main>
}

function moraleTone(value: number) {
  if (value < 30) return 'danger'
  if (value < 60) return 'warn'
  return 'good'
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
      <div><h2>Admin Control Center</h2><span>0.1.10 economy</span></div>
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

function AdminMetric({ label, value }: { label: string, value: string }) {
  return <div className="admin-metric"><span>{label}</span><strong>{value}</strong></div>
}

function WorldNewsPanel({ entries, currentPlayer }: { entries: WorldNewsEntry[], currentPlayer: string }) {
  return <div className="panel">
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

function Stat({ icon, label, value, sub }: { icon: ReactNode, label: string, value: string, sub?: string }) {
  return <div className="stat">
    <div className="stat-icon" aria-hidden="true">{icon}</div>
    <div className="stat-copy"><span>{label}</span><strong>{value}</strong>{sub && <small>{sub}</small>}</div>
  </div>
}

function CashIcon() {
  return <svg viewBox="0 0 24 24"><rect x="4" y="7" width="16" height="10" rx="1.5" /><path d="M7 10h1.5M15.5 14H17M12 10.2a2 2 0 1 1 0 3.6 2 2 0 0 1 0-3.6Z" /><path d="M6 5.5 18 3l.8 3.8M6 18.5 18 21l.8-3.8" /></svg>
}

function BankIcon() {
  return <svg viewBox="0 0 24 24"><path d="M4 10h16L12 5 4 10Z" /><path d="M6 10v7M10 10v7M14 10v7M18 10v7M4 19h16" /></svg>
}

function WorthIcon() {
  return <svg viewBox="0 0 24 24"><path d="M5 19V12M10 19V8M15 19V5M20 19V10" /><path d="M3 19h18" /></svg>
}

function TurnsIcon() {
  return <svg viewBox="0 0 24 24"><path d="M18.5 8.5a7 7 0 1 0 1.2 6.1" /><path d="M19 4v5h-5" /><path d="M13.8 12.8 17 16M10.2 11.2 7 8" /></svg>
}

function RankIcon() {
  return <svg viewBox="0 0 24 24"><path d="M8 5h8v4a4 4 0 0 1-8 0V5Z" /><path d="M8 7H5a3 3 0 0 0 3 3M16 7h3a3 3 0 0 1-3 3M12 13v4M8.5 19h7M10 17h4" /></svg>
}

function CityIcon() {
  return <svg viewBox="0 0 24 24"><path d="M12 21s6-5.1 6-10a6 6 0 0 0-12 0c0 4.9 6 10 6 10Z" /><path d="M12 8.5a2.2 2.2 0 1 1 0 4.4 2.2 2.2 0 0 1 0-4.4Z" /></svg>
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
    <div><strong>{name}</strong><span>{number.format(owned)} owned · {money.format(price)} each</span></div>
    <input type="number" min={1} max={Math.max(1, owned)} value={quantity} onChange={e => onQuantity(Number(e.target.value))} />
    <button className="secondary compact" disabled={disabled || quantity < 1 || quantity > owned} onClick={onSell}>Sell</button>
  </div>
}

function StatusRow({ label, value, warn }: { label: string, value: string, warn?: boolean }) {
  return <div className={`status-row ${warn ? 'warn' : ''}`}><span>{label}</span><strong>{value}</strong></div>
}

createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>)
