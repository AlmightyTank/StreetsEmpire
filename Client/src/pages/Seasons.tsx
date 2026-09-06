import React, { useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import type { Season, SeasonArchiveEntry, SeasonStanding, SeasonTable } from '../api'
import { money, number } from '../format'
import { AdminMetric, Button, SectionTabs, StatusRow, timeLeft, useRouteTab, useSecondHand } from '../ui'
import type { PageContext } from '../pagecontext'

/*
  The clock everybody is playing against, and the shelf their trophies sit on.
*/

const SEASON_TABS = ['now', 'past', 'you'] as const
export function SeasonsPage(ctx: PageContext) {
  const [tab, setTab] = useRouteTab('seasons', SEASON_TABS, 'now')
  const [season, setSeason] = useState<Season | null>(null)
  const [shelf, setShelf] = useState<SeasonArchiveEntry[] | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let live = true
    void Promise.all([api.season(), api.seasons()])
      .then(([current, all]) => { if (!live) return; setSeason(current); setShelf(all) })
      .catch(() => { if (live) setFailed(true) })
    return () => { live = false }
  }, [])

  return <div className="d-grid gap-3">
    <SectionTabs
      label="Season sections"
      active={tab}
      onActive={setTab}
      tabs={[
        { key: 'now', label: 'This Season' },
        { key: 'past', label: 'Finished' },
        { key: 'you', label: 'Your Record' },
      ]}
    />
    {failed && <p className="alert alert-danger mb-0">The season record would not load. The standings are live either way.</p>}
    {tab === 'now' && <ThisSeasonTab ctx={ctx} season={season} now={shelf?.find(entry => entry.running) ?? null} />}
    {tab === 'past' && <FinishedSeasonsTab shelf={shelf} you={ctx.dashboard.playerId} />}
    {tab === 'you' && <YourRecordTab season={season} name={ctx.dashboard.name} />}
  </div>
}

/*
  What a roll does, said as two lists rather than as a paragraph.

  The rule is one sentence - the empire goes and the person stays - and somebody about to lose a month
  of work does not want the sentence, they want the inventory. Held here rather than inline because the
  two columns are the same shape, and the whole point of them is being read against each other.
*/
const SEASON_KEEPS = [
  'Your account and how you sign in',
  'Your player name and your town',
  'Your alliance and who you run with',
  'Every honour you have ever won',
  'Every season result ever recorded',
]

const SEASON_TAKES = [
  'Cash and bank',
  'Your pimps, hoes, thugs, and named roster reset to the starting crew',
  'The building and every room in it',
  'All stock, at whatever it was worth',
  'All held ground, and the work put into it',
  'Every combat clock and shield',
  'The alliance treasury and its thug pool',
]

function ThisSeasonTab({ ctx, season, now }: {
  ctx: PageContext
  season: Season | null
  now: SeasonArchiveEntry | null
}) {
  const { dashboard } = ctx
  const yourRaidRow = season?.currentStandings.find(row => row.playerId === dashboard.playerId && row.raidScore > 0) ?? null
  useSecondHand(season?.enabled === true)

  return <div className="d-grid gtc-1 gtc-xl-split-108 gap-3 align-items-start">
    <div className="d-grid gap-3 align-items-start">
      {season && <section className="card p-3">
        <div className="panel-title">
          <h2>{season.name}</h2>
          <span>{season.enabled ? `${timeLeft(season.endsAtUtc)} left` : 'No end date'}</span>
        </div>

        {season.enabled
          ? <>
            <SeasonProgress season={season} />
            <div className="tnum d-grid gtc-fill-140 gap-2 mt-3">
              <AdminMetric label="Day" value={`${dayOfSeason(season)} of ${season.lengthDays}`} />
              <AdminMetric
                label="Ends"
                value={new Date(season.endsAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short' })}
                sub={new Date(season.endsAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              />
              <AdminMetric label="Empires in it" value={now ? number.format(now.players) : '-'} />
              <AdminMetric
                label="Your raid rank"
                value={yourRaidRow ? `#${yourRaidRow.rank}` : 'Unranked'}
                sub={yourRaidRow ? `${money.format(yourRaidRow.raidScore)} taken` : 'No raid of yours has scored yet'}
              />
            </div>
          </>
          : <p className="text-body-tertiary mt-3 mb-0">
            Seasons are not running on this world. Nothing resets, the date on the clock is only a
            marker, and the raid board is only a record of what has happened so far.
          </p>}
      </section>}

      <section className="card p-3">
        <div className="panel-title"><h2>What a roll does</h2><span>The empire goes, the person stays</span></div>
        <div className="d-grid gtc-1 gtc-md-2 gap-3 mt-3">
          <div className="d-grid gap-1 align-content-start">
            <strong className="text-success-emphasis small">Comes through untouched</strong>
            {SEASON_KEEPS.map(item => <small className="text-body-secondary border-top py-1" key={item}>{item}</small>)}
          </div>
          <div className="d-grid gap-1 align-content-start">
            <strong className="text-danger-emphasis small">Goes back to day one</strong>
            {SEASON_TAKES.map(item => <small className="text-body-secondary border-top py-1" key={item}>{item}</small>)}
          </div>
        </div>
      </section>

      {season && <section className="card p-3">
        <div className="panel-title"><h2>What finishing well is worth</h2><span>Opening cash, next season</span></div>
        <StatusRow label="Champion" value={money.format(season.championHeadStart)} />
        <StatusRow label="Top three" value={money.format(season.topThreeHeadStart)} />
        <StatusRow label="Top ten" value={money.format(season.topTenHeadStart)} />
        {/* The run itself, which is the part worth protecting and the reason to keep playing a season
            you have already won. Only shown to somebody who has one - a zero here is noise. */}
        {season.yourTopTenStreak > 0 && <div className="d-grid gap-1 mt-3 border border-primary rounded bg-body-tertiary px-3 py-2">
          <span className="eyebrow text-primary">Your run</span>
          <strong className="fs-5">{money.format(season.yourHeadStart)}</strong>
          <small className="text-body-secondary lh-sm">
            Stacked over {number.format(season.yourTopTenStreak)} season
            {season.yourTopTenStreak === 1 ? '' : 's'} running in the top ten. Finish in the top ten
            again and this season's prize is added to it. Finish outside it, even once, and all of it
            goes.
          </small>
        </div>}
        <p className="text-body-tertiary small mt-3 mb-0">
          These stack. Finish in the top ten and what you won is added to whatever your last run was
          worth, season after season - and the whole pile is emptied the first time you finish outside
          it, whether you came eleventh or last. A long run is the biggest prize in the game and it is
          never more than one ordinary season from nothing.
        </p>
      </section>}
    </div>

    {/* The live board: this season's raid table, and the table it will finish on. */}
    <section className="card p-3">
      <SeasonRaidBoard rows={season?.currentStandings ?? []} you={dashboard.playerId} />
    </section>
  </div>
}

/** <param name="you">Your player id. Rows are matched on it, never on a name two empires can share.</param> */
function SeasonRaidBoard({ rows, you }: { rows: SeasonStanding[], you: string }) {
  const scored = rows.filter(row => row.raidScore > 0)
  return <>
    <div className="panel-title">
      <h2>Raid Take</h2>
      <span>{scored.length > 0 ? 'Cash and product stolen' : 'No raids scored yet'}</span>
    </div>
    {scored.length === 0
      ? <p className="text-body-tertiary small mt-3 mb-0">No completed raids have put money or product on the season board yet.</p>
      : <div className="leaderboard tnum d-grid overflow-y-auto mt-3">
        {scored.map(row => <SeasonRow key={row.rank} row={row} mine={row.playerId === you} />)}
      </div>}
  </>
}

/** How far through the season is, as a bar rather than two dates to subtract in your head. */
function SeasonProgress({ season }: { season: Season }) {
  const start = new Date(season.startedAtUtc).getTime()
  const end = new Date(season.endsAtUtc).getTime()
  const percent = end <= start ? 100 : Math.min(100, Math.max(0, ((Date.now() - start) / (end - start)) * 100))
  return <div
    className="progress mt-3"
    role="progressbar"
    aria-label="Season progress"
    aria-valuenow={Math.round(percent)}
    aria-valuemin={0}
    aria-valuemax={100}
  >
    <div className="progress-bar bg-primary" style={{ width: `${Math.max(2, percent)}%` }} />
  </div>
}

/** Day one is the day it opened, not day zero. Capped, because a season can sit past its end date. */
function dayOfSeason(season: Season) {
  const elapsed = Date.now() - new Date(season.startedAtUtc).getTime()
  return Math.min(season.lengthDays, Math.max(1, Math.floor(elapsed / 86_400_000) + 1))
}

/**
 * The seasons that have ended, and one of them in full.
 *
 * A list beside a table rather than a table per season down one column: the archive only ever grows,
 * and the question is nearly always about one particular season.
 */
/** <param name="you">Your player id, passed down to the table so a row knows whether it is yours.</param> */
function FinishedSeasonsTab({ shelf, you }: { shelf: SeasonArchiveEntry[] | null, you: string }) {
  const [picked, setPicked] = useState<number | null>(null)
  const [table, setTable] = useState<SeasonTable | null>(null)
  const [loading, setLoading] = useState(false)

  const finished = (shelf ?? []).filter(entry => !entry.running)
  const chosen = picked ?? finished[0]?.number ?? null

  useEffect(() => {
    if (chosen === null) return
    let live = true
    setLoading(true)
    void api.seasonTable(chosen)
      .then(value => { if (live) setTable(value) })
      .catch(() => { if (live) setTable(null) })
      .finally(() => { if (live) setLoading(false) })
    return () => { live = false }
  }, [chosen])

  if (shelf === null) return <p className="text-body-tertiary mb-0">Reading the record.</p>

  if (finished.length === 0) return <section className="card p-3">
    <div className="panel-title"><h2>Nothing has finished yet</h2><span>The world is on its first</span></div>
    <p className="mt-3 mb-0">
      When this season ends, everybody in it gets a line here - where they came, what they took in raids,
      what town they did it in and what the season was called. Written for everybody rather than only
      the top, because a season somebody came fortieth in is still a season they played.
    </p>
  </section>

  return <div className="d-grid gtc-1 gtc-xl-split-280 gap-3 align-items-start">
    <section className="card p-3">
      <div className="panel-title"><h2>Seasons</h2><span>{finished.length} finished</span></div>
      <div className="d-grid gap-2 mt-3">
        {finished.map(entry => <button
          className={`btn btn-secondary d-grid gap-1 text-start ${chosen === entry.number ? 'border-primary text-primary' : ''}`}
          key={entry.number}
          type="button"
          aria-current={chosen === entry.number ? 'true' : undefined}
          onClick={() => setPicked(entry.number)}
        >
          <span className="d-flex justify-content-between align-items-baseline gap-2">
            <strong className="min-w-0 text-truncate">{entry.name}</strong>
            <small className="text-body-tertiary flex-shrink-0">
              {entry.endedAtUtc ? new Date(entry.endedAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' }) : ''}
            </small>
          </span>
          <small className="text-body-tertiary text-truncate">
            {entry.championName ? `${entry.championName} took ${money.format(entry.championRaidScore)}` : 'Nobody was in it'} / {number.format(entry.players)} finished
          </small>
          {/* The line that makes the archive worth opening for somebody who never came top ten. */}
          {typeof entry.yourRank === 'number' && <small className={entry.yourHonour ? 'text-warning-emphasis' : 'text-body-secondary'}>
            You finished #{entry.yourRank}{entry.yourHonour ? ` - ${entry.yourHonour}` : ''}
          </small>}
        </button>)}
      </div>
    </section>

    <section className="card p-3">
      {table
        ? <SeasonFinalTable table={table} you={you} />
        : <p className="text-body-tertiary mb-0">{loading ? 'Reading the table.' : 'That season has no table on the record.'}</p>}
    </section>
  </div>
}

function SeasonFinalTable({ table, you }: { table: SeasonTable, you: string }) {
  const rows = table.table
  const yours = table.you

  return <>
    <div className="panel-title">
      <h2>{table.name}</h2>
      <span>{table.endedAtUtc ? `Ended ${new Date(table.endedAtUtc).toLocaleDateString()}` : 'Still running'}</span>
    </div>

    <div className="tnum d-grid gtc-fill-140 gap-2 mt-3">
      <AdminMetric label="Finished" value={number.format(table.players)} sub="empires in it" />
      <AdminMetric
        label="Won by"
        value={rows[0]?.playerName ?? '-'}
        sub={rows[0] ? money.format(rows[0].raidScore) : undefined}
      />
      <AdminMetric
        label="You"
        value={yours ? `#${yours.rank}` : '-'}
        sub={yours ? (yours.honour ?? 'a season played') : 'you were not in this one'}
      />
    </div>

    {rows.length === 0
      ? <p className="text-body-tertiary small mt-3 mb-0">No table was written for this one.</p>
      : <div className="leaderboard tnum d-grid overflow-y-auto mt-3">
        {rows.map(row => <SeasonRow key={row.rank} row={row} mine={row.playerId === you} />)}
      </div>}

    {/* The page stops at a hundred; the record does not. Somebody past it still gets their own line. */}
    {yours && !rows.some(row => row.rank === yours.rank) && <div className="mt-3">
      <strong className="d-block text-body-secondary small">Your line, past the end of the table above</strong>
      <div className="tnum d-grid"><SeasonRow row={yours} mine /></div>
    </div>}

    {rows.length >= 100 && <p className="text-body-tertiary small mt-2 mb-0">
      The first hundred of {number.format(table.players)}. Every finish is on the record whether or not
      it is on this page.
    </p>}
  </>
}

function SeasonRow({ row, mine }: { row: SeasonStanding, mine: boolean }) {
  return <div className={`leader d-grid gap-2 p-2 border-top ${mine ? 'bg-success-subtle' : ''}`}>
    <span className="text-body-secondary">#{row.rank}</span>
    <span className="d-grid min-w-0">
      <strong className="min-w-0 text-truncate">{row.playerName}</strong>
      <small className="text-body-tertiary text-truncate">{row.crewName ? `${row.crewName} / ` : ''}{row.city}</small>
    </span>
    <span className="d-grid justify-items-end gap-1">
      <span className="text-body-secondary">{money.format(row.raidScore)}</span>
      <small className="text-body-tertiary">{raidTake(row)}</small>
      <HonourBadge honour={row.honour} />
    </span>
  </div>
}

function raidTake(row: SeasonStanding) {
  return `${money.format(row.raidCashTaken)} / ${number.format(row.raidWeedTaken)} weed / ${number.format(row.raidCokeTaken)} coke`
}

/**
 * The three finishes worth a name, and nothing for the rest.
 *
 * Kept few on purpose: an honour everybody has is a participation sticker, and the point of these is
 * that they are the only thing a reset does not take.
 */
function HonourBadge({ honour }: { honour?: string | null }) {
  if (!honour) return null
  const tone = honour === 'Champion'
    ? 'text-bg-warning'
    : honour === 'Top Three' ? 'text-bg-light border' : 'border text-body-secondary'
  return <span className={`badge rounded-pill ${tone}`}>{honour}</span>
}

/** What somebody has to show for every season they have been through. The half of the game that lasts. */
function YourRecordTab({ season, name }: { season: Season | null, name: string }) {
  if (!season) return <p className="text-body-tertiary mb-0">Reading the record.</p>

  const honours = season.honours
  const best = honours.reduce<number | null>((low, x) => low === null || x.rank < low ? x.rank : low, null)
  const championships = honours.filter(x => x.honour === 'Champion').length
  const topTens = honours.filter(x => x.rank <= 10).length

  return <div className="d-grid gap-3">
    <section className="card p-3">
      <div className="panel-title">
        <h2>{name}</h2>
        <span>{honours.length === 0 ? 'No seasons finished' : `${honours.length} season${honours.length === 1 ? '' : 's'} finished`}</span>
      </div>
      {honours.length === 0
        ? <p className="mt-3 mb-0">
          You have not been through a roll yet. When this season ends you get a line here - where you
          came, what you took in raids, and what it was called - and it stays there through every season
          after it, which is more than anything else you own can say.
        </p>
        : <div className="tnum d-grid gtc-fill-140 gap-2 mt-3">
          <AdminMetric label="Seasons" value={number.format(honours.length)} />
          <AdminMetric label="Best finish" value={best === null ? '-' : `#${best}`} />
          <AdminMetric label="Championships" value={number.format(championships)} />
          <AdminMetric label="Top ten finishes" value={number.format(topTens)} />
        </div>}
    </section>

    {honours.length > 0 && <section className="card p-3">
      <div className="panel-title"><h2>Every finish</h2><span>Newest first</span></div>
      <div className="d-grid mt-3">
        {honours.map(honour => <div className="d-flex justify-content-between align-items-baseline gap-2 border-top py-2" key={honour.number}>
          <span className="d-grid min-w-0">
            <strong className="min-w-0 text-truncate">{honour.name}</strong>
            <small className="text-body-tertiary">
              #{honour.rank}{honour.endedAtUtc ? ` / ended ${new Date(honour.endedAtUtc).toLocaleDateString()}` : ''}
            </small>
          </span>
          <span className="d-grid justify-items-end gap-1 flex-shrink-0">
            <span className="tnum text-body-secondary">{money.format(honour.raidScore)}</span>
            <HonourBadge honour={honour.honour} />
          </span>
        </div>)}
      </div>
    </section>}
  </div>
}
