import React, { FormEvent, useEffect, useMemo, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { ActionResult, api, Dashboard, LeaderboardEntry } from './api'
import './styles.css'

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 })
const number = new Intl.NumberFormat('en-US')

function App() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [leaders, setLeaders] = useState<LeaderboardEntry[]>([])
  const [authMode, setAuthMode] = useState<'login' | 'register'>('login')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)
  const [streetTurns, setStreetTurns] = useState(5)
  const [productionTurns, setProductionTurns] = useState(5)
  const [hoeCut, setHoeCut] = useState(30)
  const [bankAmount, setBankAmount] = useState(1000)
  const [storeQty, setStoreQty] = useState<Record<string, number>>({ condoms: 25, beer: 12, weapons: 1 })
  const [sellQty, setSellQty] = useState<Record<'weed' | 'coke', number>>({ weed: 10, coke: 5 })
  const [tickSeconds, setTickSeconds] = useState(0)

  const refresh = async () => {
    try {
      const [d, l] = await Promise.all([api.dashboard(), api.leaderboard()])
      setDashboard(d)
      setLeaders(l)
      setTickSeconds(d.secondsUntilNextTurnTick)
      setHoeCut(d.hoeCutPercent)
      setError('')
    } catch (e) {
      if ((e as Error).message === 'Unauthorized') setDashboard(null)
      else setError((e as Error).message)
    }
  }

  useEffect(() => { void refresh() }, [])
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
    setBusy(true); setError(''); setNotice('')
    try {
      const result = await fn() as ActionResult | undefined
      if (result?.summary) setNotice(result.summary)
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
          {error && <div className="error">{error}</div>}
          <button className="primary" disabled={busy}>{busy ? 'Working…' : authMode === 'login' ? 'Enter the City' : 'Build My Empire'}</button>
        </form>
      </section>
    </main>
  }

  const totalCrew = dashboard.pimps + dashboard.hoes + dashboard.thugs
  const weaponCoverage = dashboard.thugs === 0 ? 100 : Math.min(100, (dashboard.weapons / dashboard.thugs) * 100)
  const managementCapacity = dashboard.pimps * 10

  return <main className="game-shell">
    <header className="topbar">
      <div><strong>STREET EMPIRE</strong><span className="version">0.1.1</span></div>
      <div className="top-actions"><span>{dashboard.name}</span><button onClick={() => void act(api.logout)}>Logout</button></div>
    </header>

    <section className="stats-grid">
      <Stat label="Cash on Hand" value={money.format(dashboard.cash)} />
      <Stat label="Bank" value={money.format(dashboard.bankCash)} />
      <Stat label="Net Worth" value={money.format(dashboard.netWorth)} />
      <Stat label="Turns" value={`${dashboard.turns} / ${dashboard.maxTurns}`} sub={nextTurn === 'MAX' ? 'Turn bank full' : `+${dashboard.turnsPerTick} in ${nextTurn}`} />
      <Stat label="Rank" value={`#${dashboard.rank}`} />
      <Stat label="City" value={dashboard.city} />
    </section>

    {error && <div className="error banner">{error}</div>}
    {notice && <div className="notice banner">{notice}</div>}

    <div className="layout">
      <section className="main-column">
        <div className="panel">
          <div className="panel-title"><h2>Your Crew</h2><span>{number.format(totalCrew)} total</span></div>
          <div className="crew-grid">
            <CrewCard name="Pimps" count={dashboard.pimps} desc={`Manage up to ${number.format(managementCapacity)} hoes.`} />
            <CrewCard name="Hoes" count={dashboard.hoes} desc={`${dashboard.hoeHappiness.toFixed(0)}% morale · ${dashboard.hoeCutPercent}% cut`} tone={moraleTone(dashboard.hoeHappiness)} />
            <CrewCard name="Thugs" count={dashboard.thugs} desc={`${dashboard.thugHappiness.toFixed(0)}% morale · ${weaponCoverage.toFixed(0)}% armed`} tone={moraleTone(dashboard.thugHappiness)} />
          </div>
        </div>

        <div className="panel">
          <div className="panel-title"><h2>Work the Streets</h2><span>Income + recruiting</span></div>
          <p>Your hoes generate gross income. Their cut is paid before your cash is deposited on hand. Street work can also recruit crew and turn up small amounts of inventory.</p>
          <div className="action-row wrap">
            <label>Turns<input type="number" min={1} max={20} value={streetTurns} onChange={e => setStreetTurns(Number(e.target.value))} /></label>
            <label>Hoe Cut %<input type="number" min={10} max={80} value={hoeCut} onChange={e => setHoeCut(Number(e.target.value))} /></label>
            <button className="secondary" disabled={busy || hoeCut < 10 || hoeCut > 80 || hoeCut === dashboard.hoeCutPercent} onClick={() => void act(() => api.setHoeCut(hoeCut))}>Save Cut</button>
            <button className="primary" disabled={busy || streetTurns < 1 || streetTurns > dashboard.turns} onClick={() => void act(() => api.workStreet(streetTurns))}>Work {streetTurns} Turn{streetTurns === 1 ? '' : 's'}</button>
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
          <p>Production turns cash-on-hand into inventory. Product can be sold immediately for fixed 0.1.1 street prices.</p>
          <div className="action-row wrap">
            <label>Turns<input type="number" min={1} max={20} value={productionTurns} onChange={e => setProductionTurns(Number(e.target.value))} /></label>
            <button className="primary" disabled={busy || productionTurns > dashboard.turns} onClick={() => void act(() => api.produce('weed', productionTurns))}>Produce Weed</button>
            <button className="primary" disabled={busy || productionTurns > dashboard.turns} onClick={() => void act(() => api.produce('coke', productionTurns))}>Produce Coke</button>
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

      <aside className="side-column">
        <div className="panel sticky">
          <div className="panel-title"><h2>Empire Status</h2><span>0.1.1</span></div>
          <StatusRow label="Hoe morale" value={`${dashboard.hoeHappiness.toFixed(0)}%`} warn={dashboard.hoeHappiness < 40} />
          <StatusRow label="Thug morale" value={`${dashboard.thugHappiness.toFixed(0)}%`} warn={dashboard.thugHappiness < 40} />
          <StatusRow label="Management" value={`${dashboard.hoes}/${managementCapacity} hoes`} warn={dashboard.hoes > managementCapacity} />
          <StatusRow label="Armed thugs" value={`${Math.min(dashboard.weapons, dashboard.thugs)}/${dashboard.thugs}`} warn={dashboard.weapons < dashboard.thugs} />
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
      </aside>
    </div>
  </main>
}

function moraleTone(value: number) {
  if (value < 30) return 'danger'
  if (value < 60) return 'warn'
  return 'good'
}

function Stat({ label, value, sub }: { label: string, value: string, sub?: string }) {
  return <div className="stat panel"><span>{label}</span><strong>{value}</strong>{sub && <small>{sub}</small>}</div>
}

function CrewCard({ name, count, desc, tone }: { name: string, count: number, desc: string, tone?: string }) {
  return <div className={`crew-card ${tone ?? ''}`}><span>{name}</span><strong>{number.format(count)}</strong><p>{desc}</p></div>
}

function InventoryCard({ name, count, note }: { name: string, count: number, note: string }) {
  return <div className="inventory-card"><span>{name}</span><strong>{number.format(count)}</strong><small>{note}</small></div>
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
