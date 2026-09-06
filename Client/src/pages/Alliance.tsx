import React, { useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import type { AllianceAssistCall, AllianceBoard, AllianceBrief, AllianceDoorKey, AllianceMember,
  AlliancePact, AlliancePower, AllianceRequest, AllianceSummary, AllianceTransfer, Dashboard,
  ActionResult, Pimp } from '../api'
import { compactDateTime, money, number } from '../format'
import { AdminMetric, BUSY, Button, countdown, DismissibleMessage, firstReason, PlayerAvatar,
  PlayerName, secondsUntil, StatusRow, timeUntil, useRouteTab, useSecondsTicker, type Blocked } from '../ui'
import { spendable, type PageContext } from '../pagecontext'

/*
  The crew: who you run with, what you owe them, and who they are at war with.
*/
export function AlliancePage(ctx: PageContext) {
  const { busy, act } = ctx
  const [board, setBoard] = useState<AllianceBoard | null>(null)
  const [name, setName] = useState('')
  const [motto, setMotto] = useState('')

  const load = async () => {
    try {
      setBoard(await api.alliances())
    } catch {
      // The page is readable without the board; an error banner over an empty list says nothing.
    }
  }
  useEffect(() => { void load() }, [])

  const run = (fn: () => Promise<ActionResult>) => void act(async () => {
    const result = await fn()
    await load()
    return result
  })

  if (!board) return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start"><section className="card p-3"><p className="text-body-tertiary small mt-3 mb-0">Reading the board.</p></section></div>

  const yours = board.yours
  return <div className="d-grid gtc-1 gtc-md-2 gap-3 align-items-start gtc-xl-split-135">
    {yours
      ? <section className="card p-3 gcol-full">
        <div className="panel-title"><h2>{yours.name}</h2><span>#{yours.rank} / {yours.members} of {yours.maxMembers}</span></div>
        {yours.motto && <p className="fst-italic text-primary mb-2">{yours.motto}</p>}
        <p>
          Nobody on this list can attack you and you cannot attack them, by any method. That is what the{' '}
          {yours.duesPercent}% off every shift is buying.
        </p>
        <div className="tnum d-grid gtc-1 gtc-md-3 gtc-xl-5 gap-2 mb-3">
          <AdminMetric label="Crew worth" value={money.format(yours.netWorth)} />
          <AdminMetric label="Treasury" value={money.format(board.treasury)} />
          <AdminMetric label="Dues" value={`${yours.duesPercent}%`} />
          <AdminMetric label="Pool" value={`${yours.offensiveThugs} off / ${yours.defensiveThugs} def`} />
          <AdminMetric label="City control" value={yours.cityControlThugs > 0 ? `+${yours.cityControlThugs}` : 'None'} />
          <AdminMetric label="You are" value={board.yourRank} />
        </div>

        <div className="d-grid gap-1 my-3">
          {board.members.map(member => <AllianceMemberRow
            key={member.playerId}
            member={member}
            board={board}
            busy={busy}
            onAct={run}
          />)}
        </div>

        <AllianceRequestsPanel board={board} busy={busy} onAct={run} />

        <AllianceAssistPanel board={board} ownPlayerId={ctx.dashboard.playerId} busy={busy} onAct={run} />

        <AllianceWarPanel board={board} busy={busy} />

        <AlliancePactsPanel board={board} busy={busy} onAct={run} />

        <AlliancePoolPanel board={board} crew={yours} busy={busy} onAct={run} />

        <AllianceTransfersPanel transfers={board.transfers} />

        {board.yourRank === 'Boss' && <AllianceSettingsPanel crew={yours} board={board} maxDues={board.maxDuesPercent} busy={busy} onSave={run} />}

        <div className="control-row">
          <Button className="btn btn-secondary" blocked={busy && BUSY} onClick={() => run(() => api.leaveAlliance())}>
            {yours.youFounded && yours.members > 1 ? 'Leave (throw everybody out first)' : 'Leave the crew'}
          </Button>
        </div>
      </section>
      : <section className="card p-3 gcol-full">
        <div className="panel-title"><h2>Start a Crew</h2><span>{money.format(board.foundingCost)}</span></div>
        {/*
          Three sentences rather than one, and one of them says what the money is for.

          "A crew is people who" works as a heading and grates as the opening of a paragraph - a
          singular subject with a plural after it. The rest ran on through two "and"s and finished on
          "a share into a shared pot", which repeats itself in five words and still leaves a player
          deciding whether to spend the founding fee with no idea what the pot does.

          The truce sentence is now word for word the one the in-crew panel already uses, since it is
          the same promise and there is no reason for the game to phrase it twice.
        */}
        <p>
          A crew is an agreement not to rob each other. Nobody in one can attack you and you cannot
          attack them, by any method. It costs a cut of every shift any of you works, and that fills a
          treasury the crew spends on thugs to send along on a raid or post at a member's house.
        </p>
        <div className="control-row">
          <label className="field">Name<input className="form-control" value={name} maxLength={32} onChange={event => setName(event.target.value)} /></label>
          <label className="field">Motto<input className="form-control" value={motto} maxLength={140} onChange={event => setMotto(event.target.value)} /></label>
          <Button
            className="btn btn-primary"
            blocked={firstReason(
              busy && BUSY,
              name.trim().length < 3 && 'A crew needs a name of at least three characters.',
            )}
            onClick={() => run(() => api.foundAlliance(name.trim(), motto.trim()))}
          >Found it</Button>
        </div>
      </section>}

    {/* Spans, because the alliance page has exactly two children and the other one spans too - the
        second column of this grid was being held open for something that never renders. */}
    <section className="card p-3 gcol-full">
      <div className="panel-title"><h2>The Board</h2><span>{board.board.length} crews</span></div>
      {board.board.length === 0 && <p className="text-body-tertiary small mt-3 mb-0">Nobody is running with anybody yet.</p>}
      <div className="tnum d-grid gap-1 my-3">
        {board.board.map(crew => <div className={`alliance-row d-grid gap-2 align-items-center border rounded bg-body-tertiary p-2 ${crew.yours ? 'border-primary' : ''}`} key={crew.id}>
          <span>#{crew.rank}</span>
          {/*
            A stack, not two inline elements in a row. Both of these are inline and JSX eats the
            newline between them, so they rendered welded together - "The Eastside TableOpen to
            anyone". The name goes above the things attached to it, which is what the row's own
            comment in the stylesheet says a crew is.
          */}
          <div className="d-grid">
            <strong>{crew.name}</strong>
            <small className="text-body-secondary">
              {crew.doorLabel} / {crew.members} of {crew.maxMembers} / {crew.duesPercent}% dues
              {crew.cityControlThugs > 0 ? ` / +${crew.cityControlThugs} city thugs` : ''}
              {/* A record you cannot see from outside is not a reputation. */}
              {crew.warsWon + crew.warsLost > 0 ? ` / ${crew.warsWon}-${crew.warsLost} in wars` : ''}
            </small>
            {crew.atWarWith && <small className="text-danger-emphasis">At war with {crew.atWarWith}</small>}
          </div>
          <b>{money.format(crew.netWorth)}</b>
          {/*
            Every control in one cell, however many there turn out to be.

            They used to be siblings of the row itself, each taking a grid column of its own, and the
            row declares four. Three are spoken for by the rank, the name and the money, which left
            exactly one for the buttons - fine while there was only ever one, and there is only ever
            one for a crew you are not in. From inside a crew there are two: ally with them, or
            declare on them. The second had nowhere to go, so it fell into an implicit row and landed
            in the rank column, and "War costs $250,000" was rendered a character at a time down a
            42-pixel strip.

            A cell that holds them means the row's column count stops depending on how many buttons a
            particular crew happens to earn.
          */}
          <div className="alliance-row-actions d-flex flex-wrap align-items-center gap-2">
            {/* One door, one thing an outsider can do about it. Offering a button the crew has said it
                does not want is how a player learns a rule by being refused. */}
            {!yours && crew.members >= crew.maxMembers && <em>Full</em>}
            {!yours && crew.members < crew.maxMembers && crew.door === 'Open' && <Button
              className="btn btn-secondary btn-sm"
              blocked={busy && BUSY}
              onClick={() => run(() => api.joinAlliance(crew.id))}
            >Join</Button>}
            {!yours && crew.members < crew.maxMembers && crew.door === 'Application' && <Button
              className="btn btn-secondary btn-sm"
              blocked={busy && BUSY}
              onClick={() => run(() => api.applyToAlliance(crew.id))}
            >Ask</Button>}
            {!yours && crew.members < crew.maxMembers && crew.door === 'InviteOnly' && <em title={crew.doorDetail}>Invite only</em>}
            {yours && !crew.yours && !hasPactWith(board, crew.id) && <Button
              className="btn btn-secondary btn-sm"
              blocked={busy && BUSY}
              onClick={() => run(() => api.requestAlliancePact(crew.id))}
            >Ally</Button>}
            {/* Offered only where it could actually be pressed: your rank has to allow spending the
                treasury, neither crew can already be in a war, and you cannot declare on people you
                hold a truce with. Every one of those is refused by the server too - this is so nobody
                learns the rules by being told no. */}
            {yours && !crew.yours && board.warTerms.youCanDeclare && !hasPactWith(board, crew.id)
              && !board.war && !crew.atWarWith && <Button
                className="btn btn-outline-danger btn-sm text-nowrap"
                blocked={firstReason(
                  busy && BUSY,
                  board.treasury < board.warTerms.stake && `A war stakes ${money.format(board.warTerms.stake)} and the treasury holds ${money.format(board.treasury)}.`,
                )}
                title={`${money.format(board.warTerms.stake)} out of the treasury, ${board.warTerms.durationHours} hours, winner takes the stake and ${board.warTerms.tributePercent}% of the losing treasury.`}
                onClick={() => run(() => api.declareWar(crew.id))}
              >{board.treasury < board.warTerms.stake ? `War costs ${money.format(board.warTerms.stake)}` : 'Declare war'}</Button>}
          </div>
        </div>)}
      </div>
    </section>
  </div>
}

function hasPactWith(board: AllianceBoard, allianceId: number) {
  return board.pacts.some(pact =>
    pact.status !== 'Canceled'
    && pact.status !== 'Declined'
    && (pact.requestingAllianceId === allianceId || pact.targetAllianceId === allianceId))
}

/**
 * One name on the roster, with whatever this viewer is entitled to do about them.
 *
 * The buttons are drawn from the powers the server sent rather than from a guess about rank, so a crew
 * whose boss moved a line sees the change immediately and the client never has to know what the lines
 * currently are.
 */
function AllianceMemberRow({ member, board, busy, onAct }: {
  member: AllianceMember
  board: AllianceBoard
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const canExpel = board.powers.find(x => x.power === 'Expel')?.youHaveIt ?? false
  const isBoss = board.yourRank === 'Boss'
  const [item, setItem] = useState('cash')
  const [quantity, setQuantity] = useState(1)
  const [sendOpen, setSendOpen] = useState(false)
  // Promotable ranks stop below the top: handing the crew over is its own move because it is the one
  // that gives yours away.
  const promotable = board.ranks.filter(x => x !== 'Boss')

  return <div className={`alliance-member d-grid gap-2 align-items-center border rounded bg-body-tertiary p-2 ${member.isYou ? 'border-primary' : ''}`}>
    <div className="min-w-0">
      <strong className="d-block text-truncate"><PlayerName playerId={member.playerId}>{member.name}</PlayerName></strong>
      <small className="d-block text-body-secondary">{member.rankLabel}{member.isFounder ? ' / founded it' : ''} - {member.city} / {member.pimps}P {member.hoes}H {member.thugs}T{member.defenders > 0 ? ` / ${member.defenders} posted` : ''}</small>
    </div>
    <b className="tnum">{money.format(member.netWorth)}</b>
    {!member.isYou && <div className="alliance-member-actions d-flex flex-wrap align-items-center gap-1">
      {isBoss && <select className="form-select"
        value={member.rank === 'Boss' ? '' : member.rank}
        disabled={busy || member.rank === 'Boss'}
        onChange={event => onAct(() => api.setAllianceRank(member.playerId, event.target.value))}
      >
        {member.rank === 'Boss' && <option value="">Boss</option>}
        {promotable.map(rank => <option key={rank} value={rank}>{rank}</option>)}
      </select>}
      {isBoss && <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => onAct(() => api.handOverAlliance(member.playerId))}
      >Hand over</Button>}
      {canExpel && member.youOutrankThem && <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => onAct(() => api.expelMember(member.playerId))}
      >Throw out</Button>}
      <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => setSendOpen(value => !value)}
      >Send</Button>
    </div>}
    {!member.isYou && sendOpen && <div className="alliance-transfer-controls d-flex flex-wrap align-items-end gap-1">
      <label className="field mb-0">Send
        <select className="form-select" value={item} disabled={busy} onChange={event => setItem(event.target.value)}>
          {allianceSendItems.map(option => <option key={option.key} value={option.key}>{option.label}</option>)}
        </select>
      </label>
      <label className="field mb-0">Qty<input className="form-control" type="number" min={1} value={quantity} onChange={event => setQuantity(Number(event.target.value))} /></label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          quantity < 1 && 'Send at least one.',
        )}
        onClick={() => onAct(() => api.sendAllianceResource(member.playerId, item, quantity))}
      >Confirm</Button>
    </div>}
  </div>
}

const allianceSendItems = [
  { key: 'cash', label: 'Cash' },
  { key: 'thugs', label: 'Thugs' },
  { key: 'weed', label: 'Weed' },
  { key: 'coke', label: 'Coke' },
  { key: 'beer', label: 'Beer' },
  { key: 'medicine', label: 'Medicine' },
  { key: 'poison', label: 'Poison' },
  { key: 'moonshine', label: 'Moonshine' },
  { key: 'cut', label: 'Cut' },
  { key: 'pistols', label: 'Pistols' },
  { key: 'shotguns', label: 'Shotguns' },
  { key: 'smgs', label: 'SMGs' },
  { key: 'rifles', label: 'Rifles' },
]

/**
 * Who is waiting on somebody. Invitations to this player and applications to their crew sit in one
 * list, because from here they are the same thing: an ask with your name on the answer.
 */
function AllianceRequestsPanel({ board, busy, onAct }: {
  board: AllianceBoard
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const answerable = board.requests.filter(x => x.yoursToAnswer)
  // Asks the crew has sent and is still waiting to hear about. Nobody is waiting on you for these, but
  // without them a boss can never see who has been asked or take an ask back.
  const sent = board.requests.filter(x => !x.yoursToAnswer)
  if (answerable.length === 0 && sent.length === 0) return null

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    {sent.length > 0 && <>
      <strong className="d-block mb-1 text-primary small">Asked, waiting to hear</strong>
      {sent.map(ask => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={ask.id}>
        <div className="min-w-0">
          <strong className="d-block text-truncate">{ask.kind === 'Invitation' ? ask.playerName : ask.allianceName}</strong>
          <small className="d-block text-body-secondary">{ask.kind === 'Invitation' ? 'has not answered yet' : 'has not answered your application'}</small>
        </div>
        {ask.kind === 'Invitation'
          ? <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.withdrawAllianceRequest(ask.id))}>Take it back</Button>
          : <small className="text-body-secondary text-sm-end">Waiting on the crew</small>}
      </div>)}
    </>}
    {answerable.length > 0 && <strong className="d-block mb-1 text-primary small">Waiting on you</strong>}
    {answerable.map(ask => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={ask.id}>
      <div className="min-w-0">
        <strong className="d-block text-truncate">{ask.kind === 'Invitation' ? ask.allianceName : ask.playerName}</strong>
        <small className="d-block text-body-secondary">{ask.kind === 'Invitation' ? 'asked you to run with them' : 'is asking for a place'}{ask.note ? ` - "${ask.note}"` : ''}</small>
      </div>
      <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAllianceRequest(ask.id, true))}>Accept</Button>
      <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAllianceRequest(ask.id, false))}>Refuse</Button>
    </div>)}
  </div>
}

/**
 * The war, if there is one, and the record if there is not.
 *
 * A crew was a reason to exist and no reason to act - everything it carried was defensive, and two
 * crews could sit beside each other for a month with nothing to decide. This is the panel where that
 * stops being true, so it says the terms out loud whether or not a war is on: what the fights a crew
 * already fights are worth, what the clock is, and what changes hands at the end.
 */
function AllianceWarPanel({ board, busy }: { board: AllianceBoard, busy: boolean }) {
  const war = board.war
  const terms = board.warTerms

  // The panel keeps its own second hand, like the building does. The app-wide one stops once turns
  // are maxed, which would freeze a war clock for exactly the crews who have stopped earning to fight.
  const [, setNow] = useState(0)
  useEffect(() => {
    if (!war || war.settled) return
    const timer = window.setInterval(() => setNow(value => value + 1), 1000)
    return () => window.clearInterval(timer)
  }, [war?.id, war?.settled])

  if (!board.yours) return null

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-danger small">Wars</strong>
    {war
      ? <div className="d-grid gap-1 border rounded border-danger p-3">
        <div className="d-flex justify-content-between align-items-baseline gap-2">
          <strong>Against {war.opponentName}</strong>
          <em className="eyebrow fst-normal">{timeUntil(war.endsAtUtc)} left</em>
        </div>
        <div className="tnum d-grid gtc-1 gtc-md-3 gap-2 my-2">
          <AdminMetric label="You" value={`${war.yourScore}`} />
          <AdminMetric label="Them" value={`${war.theirScore}`} />
          <AdminMetric label="On the table" value={money.format(war.stake)} />
        </div>
        <span className="text-body-secondary small">
          {war.youDeclared
            ? `${war.declaredByName} declared it, and the stake is yours until somebody wins it.`
            : `${war.opponentName} declared it. The stake is theirs, and it is yours if you beat them.`}
          {' '}A raid won is {terms.pointsForRaidWon}, a raid turned away is {terms.pointsForDefenceHeld},
          and taking ground is {terms.pointsForGroundTaken}. It takes {terms.minScoreToWin} to win
          anything at all, and the winner takes the stake plus {terms.tributePercent}% of the losing
          treasury.
        </span>
        <small className="text-body-tertiary">
          Nothing about a war lifts a protection. The wealth floor, the ratio, the shield on somebody
          who has just been hit and the falling haul on a repeat all still apply - so this is a reason
          to fight, not a licence.
        </small>
      </div>
      : <p className="text-body-secondary small mb-0">
        No war on. Declaring costs the treasury {money.format(terms.stake)} and runs {terms.durationHours} hours;
        the winner takes that back plus {terms.tributePercent}% of the losing crew's treasury, up to{' '}
        {money.format(terms.maxTribute)}. {terms.youCanDeclare
          ? 'Pick a crew off the board below.'
          : 'Somebody who can spend the treasury has to call it.'}
      </p>}
    {board.warHistory.length > 0 && <div className="d-grid gap-1 mt-2">
      {board.warHistory.map(past => <div key={past.id} className="d-flex justify-content-between align-items-baseline gap-2 border-top py-1">
        <small className={past.youWon === true ? 'text-success-emphasis' : past.youWon === false ? 'text-danger-emphasis' : 'text-body-secondary'}>
          {past.youWon === true ? 'Won' : past.youWon === false ? 'Lost' : 'Drew'} against {past.opponentName}
        </small>
        <small className="text-body-tertiary tnum">{past.yourScore}-{past.theirScore}</small>
      </div>)}
    </div>}
    {busy && <small className="text-body-tertiary">Working.</small>}
  </div>
}

function AlliancePactsPanel({ board, busy, onAct }: {
  board: AllianceBoard
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  if (!board.yours || board.pacts.length === 0) return null
  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Allied crews</strong>
    {board.pacts.map(pact => <AlliancePactRow key={pact.id} pact={pact} ownAllianceId={board.yours?.id ?? 0} busy={busy} onAct={onAct} />)}
  </div>
}

function AlliancePactRow({ pact, ownAllianceId, busy, onAct }: {
  pact: AlliancePact
  ownAllianceId: number
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const other = pact.requestingAllianceId === ownAllianceId ? pact.targetAllianceName : pact.requestingAllianceName
  return <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2">
    <div className="min-w-0">
      <strong className="d-block text-truncate">{other}</strong>
      <small className="d-block text-body-secondary">{pact.status === 'Active' ? 'active pact' : 'waiting on an answer'}</small>
    </div>
    {pact.yoursToAnswer
      ? <>
        <Button className="btn btn-primary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAlliancePact(pact.id, true))}>Accept</Button>
        <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.answerAlliancePact(pact.id, false))}>Refuse</Button>
      </>
      : <Button className="btn btn-secondary btn-sm" blocked={busy && BUSY} onClick={() => onAct(() => api.cancelAlliancePact(pact.id))}>
        {pact.status === 'Active' ? 'Break pact' : 'Take it back'}
      </Button>}
  </div>
}

function AllianceAssistPanel({ board, ownPlayerId, busy, onAct }: {
  board: AllianceBoard
  /** Passed through to the rows: only whoever sent help is offered the button to take it back. */
  ownPlayerId: string
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  if (!board.yours) return null
  /*
    An `and`, not an `or`.

    This read `status === 'Open' || missionStatus !== 'Complete'`, and since nothing ever closed a call,
    an unanswered one on a fight that finished last week passed the first clause and stayed on the page
    for good - offering to send help to a raid long over, and answering "that fight is no longer taking
    help" to anybody who tried. The server closes them now, and this stops showing the closed ones.
  */
  const calls = board.assistCalls.filter(call => call.status !== 'Closed' && call.missionStatus !== 'Complete')
  if (calls.length === 0) return null

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Assist calls</strong>
    {calls.map(call => <AllianceAssistRow key={call.id} call={call} ownAllianceId={board.yours?.id ?? 0} ownPlayerId={ownPlayerId} busy={busy} onAct={onAct} />)}
  </div>
}

function AllianceAssistRow({ call, ownAllianceId, ownPlayerId, busy, onAct }: {
  call: AllianceAssistCall
  ownAllianceId: number
  /** Taking help back is personal: it goes to whoever sent it, not to whoever is looking at the page. */
  ownPlayerId: string
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const canAnswer = call.status === 'Open' && call.allyAllianceId === ownAllianceId && call.missionStatus !== 'Complete'
  // Only the person who sent it, and only once the fight it was sent to has finished.
  const canRecall = call.status === 'Answered'
    && call.missionStatus === 'Complete'
    && call.respondedByPlayerId === ownPlayerId
  const [thugs, setThugs] = useState(0)
  const [pistols, setPistols] = useState(0)
  const [shotguns, setShotguns] = useState(0)
  const [smgs, setSmgs] = useState(0)
  const [rifles, setRifles] = useState(0)
  const sentWeapons = call.pistolsSent + call.shotgunsSent + call.smgsSent + call.riflesSent

  return <div className="d-grid gap-2 border-top py-2">
    <div className="d-flex flex-wrap justify-content-between gap-2">
      <div>
        <strong>{call.defenderName} vs {call.attackerName}</strong>
        <small>{call.defenderAllianceName} called {call.allyAllianceName} / {call.missionStatus}</small>
      </div>
      {call.status !== 'Open' && <em>{call.thugsSent} thugs / {sentWeapons} guns sent</em>}
    </div>
    {canRecall && <div className="d-flex flex-wrap align-items-center gap-2">
      <span className="text-body-tertiary small">
        The fight is over. What you sent still counts as theirs until you take it back, and whatever did
        not survive it is gone.
      </span>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={busy && BUSY}
        onClick={() => onAct(() => api.recallAllianceAssist(call.id))}
      >Take back what is left</Button>
    </div>}
    {canAnswer && <div className="d-grid gtc-2 gtc-md-fill-120 gap-2">
      <label className="field">Thugs<input className="form-control" type="number" min={0} value={thugs} onChange={event => setThugs(Number(event.target.value))} /></label>
      <label className="field">Pistols<input className="form-control" type="number" min={0} value={pistols} onChange={event => setPistols(Number(event.target.value))} /></label>
      <label className="field">Shotguns<input className="form-control" type="number" min={0} value={shotguns} onChange={event => setShotguns(Number(event.target.value))} /></label>
      <label className="field">SMGs<input className="form-control" type="number" min={0} value={smgs} onChange={event => setSmgs(Number(event.target.value))} /></label>
      <label className="field">Rifles<input className="form-control" type="number" min={0} value={rifles} onChange={event => setRifles(Number(event.target.value))} /></label>
      <Button
        className="btn btn-primary btn-sm align-self-end"
        blocked={firstReason(
          busy && BUSY,
          thugs + pistols + shotguns + smgs + rifles < 1 && 'Put something in the boxes above. Help with nothing in it is not help.',
        )}
        onClick={() => onAct(() => api.answerAllianceAssist(call.id, thugs, pistols, shotguns, smgs, rifles))}
      >Send help</Button>
    </div>}
  </div>
}

function AllianceTransfersPanel({ transfers }: { transfers: AllianceTransfer[] }) {
  if (transfers.length === 0) return null
  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Recent sends</strong>
    {transfers.slice(0, 6).map(transfer => <div className="alliance-ask d-grid gap-2 align-items-center border-top py-2" key={transfer.id}>
      <div>
        <strong>
          <PlayerName playerId={transfer.fromPlayerId}>{transfer.fromPlayerName}</PlayerName>
          {' to '}
          <PlayerName playerId={transfer.toPlayerId}>{transfer.toPlayerName}</PlayerName>
        </strong>
        <small>{transfer.quantity.toLocaleString()} {transfer.label.toLowerCase()}</small>
      </div>
      <em>{new Date(transfer.createdAtUtc).toLocaleString()}</em>
    </div>)}
  </div>
}

/**
 * The shared pool: what the crew has bought, and what this member may borrow of it.
 *
 * The borrow limit is stated on the panel rather than discovered by being refused, because it is the
 * rule that makes the pool interesting - you can bring as many as you brought yourself, so the crew
 * doubles you rather than replacing you.
 */
function AlliancePoolPanel({ board, crew, busy, onAct }: {
  board: AllianceBoard
  crew: AllianceSummary
  busy: boolean
  onAct: (fn: () => Promise<ActionResult>) => void
}) {
  const [buy, setBuy] = useState(1)
  const [post, setPost] = useState(1)
  const room = Math.max(0, board.borrowLimit - board.yourDefenders)
  const cities = crew.controlledCities.map(city => `${city.city} +${city.bonusThugs}`).join(' / ')

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <StatusRow label="Pool" value={`${crew.offensiveThugs} offensive / ${crew.defensiveThugs} defensive`} />
    {crew.cityControlThugs > 0 && <StatusRow label="City control" value={`+${crew.cityControlThugs} thugs (${cities})`} />}
    <StatusRow
      label="You may borrow"
      value={board.borrowLimit === 0 ? 'Nothing until you have thugs of your own' : `${board.borrowLimit} (${board.yourDefenders} standing here)`}
      warn={board.borrowLimit === 0}
    />

    {crew.youFounded && <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Buy<input className="form-control" type="number" min={1} value={buy} onChange={event => setBuy(Number(event.target.value))} /></label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          buy < 1 && 'Buy at least one.',
          board.treasury < board.offensiveThugCost * buy && `${number.format(buy)} offensive thugs cost ${money.format(board.offensiveThugCost * buy)} and the treasury holds ${money.format(board.treasury)}.`,
        )}
        onClick={() => onAct(() => api.buyAllianceThugs('offensive', buy))}
      >Offensive {money.format(board.offensiveThugCost * buy)}</Button>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          buy < 1 && 'Buy at least one.',
          board.treasury < board.defensiveThugCost * buy && `${number.format(buy)} defensive thugs cost ${money.format(board.defensiveThugCost * buy)} and the treasury holds ${money.format(board.treasury)}.`,
        )}
        onClick={() => onAct(() => api.buyAllianceThugs('defensive', buy))}
      >Defensive {money.format(board.defensiveThugCost * buy)}</Button>
    </div>}

    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Defenders<input className="form-control" type="number" min={1} value={post} onChange={event => setPost(Number(event.target.value))} /></label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          post < 1 && 'Post at least one.',
          post > room && (board.borrowLimit === 0
            ? 'You can borrow nothing until you have thugs of your own.'
            : `You may borrow ${number.format(board.borrowLimit)} and ${number.format(board.yourDefenders)} of them already stand at your place.`),
          crew.defensiveThugs < post && `The pool has ${number.format(crew.defensiveThugs)} defensive thugs in it and you are posting ${number.format(post)}.`,
        )}
        onClick={() => onAct(() => api.postDefenders(post))}
      >Post to your place</Button>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          post < 1 && 'Send back at least one.',
          board.yourDefenders < post && `You have ${number.format(board.yourDefenders)} of the crew's thugs standing here and you are sending back ${number.format(post)}.`,
        )}
        onClick={() => onAct(() => api.postDefenders(-post))}
      >Send back</Button>
    </div>
    <small className="d-block mt-1 text-body-tertiary small measure">
      Offensive thugs ride along on a raid and defensive ones stand at your place. Both die like anybody
      else, and what dies is gone from the pool for good.
    </small>
  </div>
}

/**
 * The boss's authority: the rate, the door, the sign on it, and where every other line is drawn.
 *
 * The thresholds sit here rather than beside the powers they gate because they are one decision - how
 * much of this crew do I run personally - and a boss changing their mind should not have to make it
 * five times in five places.
 */
function AllianceSettingsPanel({ crew, board, maxDues, busy, onSave }: {
  crew: AllianceSummary
  board: AllianceBoard
  maxDues: number
  busy: boolean
  onSave: (fn: () => Promise<ActionResult>) => void
}) {
  const [crewName, setCrewName] = useState(crew.name)
  const [dues, setDues] = useState(crew.duesPercent)
  const [door, setDoor] = useState<AllianceDoorKey>(crew.door)
  useEffect(() => {
    setCrewName(crew.name)
    setDues(crew.duesPercent)
    setDoor(crew.door)
  }, [crew.id, crew.name, crew.duesPercent, crew.door])
  const nameTicker = useSecondsTicker(!!crew.nameChangeReadyAtUtc)
  const nameCooldownSeconds = secondsUntil(crew.nameChangeReadyAtUtc, nameTicker)

  return <div className="d-grid gap-2 mb-3 border rounded bg-body-tertiary p-2">
    <strong className="d-block mb-1 text-primary small">Who may do what</strong>
    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Crew name
        <input className="form-control" maxLength={32} value={crewName} onChange={event => setCrewName(event.target.value)} />
        <small className="form-text">
          {nameCooldownSeconds > 0
            ? `You can change it again in ${timeUntil(crew.nameChangeReadyAtUtc!)}.`
            : 'Shown on the crew board, rosters, wars, and season tables.'}
        </small>
      </label>
      <Button
        className="btn btn-secondary btn-sm align-self-end"
        blocked={firstReason(
          busy && BUSY,
          crewName.trim().length < 3 && 'A crew name needs at least three characters.',
          crewName.trim().length > 32 && 'A crew name must be 32 characters or less.',
          crewName.trim() === crew.name && 'That is already the crew name.',
          nameCooldownSeconds > 0 && `You can change the crew name again in ${timeUntil(crew.nameChangeReadyAtUtc!)}.`,
        )}
        onClick={() => onSave(() => api.updateAlliance({ name: crewName.trim() }))}
      >Rename</Button>
    </div>
    <div className="alliance-powers d-grid gap-2">
      {board.powers.map(power => <label className="d-grid gap-1 small" key={power.power}>
        <span>{power.label}</span>
        <select className="form-select"
          value={power.minRank}
          disabled={busy}
          onChange={event => onSave(() => api.updateAlliance({ powers: { [power.power]: event.target.value } }))}
        >
          {board.ranks.map(rank => <option key={rank} value={rank}>{rank} and up</option>)}
        </select>
      </label>)}
    </div>
    <div className="d-grid gtc-1 gtc-md-3 gap-2">
      <label className="field">Dues %<input className="form-control" type="number" min={0} max={maxDues} value={dues} onChange={event => setDues(Number(event.target.value))} /></label>
      <label className="field">Door
        <select className="form-select" value={door} disabled={busy} onChange={event => setDoor(event.target.value as AllianceDoorKey)}>
          {board.doors.map(option => <option key={option.door} value={option.door}>{option.label}</option>)}
        </select>
      </label>
      <Button
        className="btn btn-secondary btn-sm"
        blocked={firstReason(
          busy && BUSY,
          dues < 0 && 'Dues cannot be negative. The crew pays you, not the other way about.',
          dues > maxDues && `Dues top out at ${maxDues}%.`,
        )}
        onClick={() => onSave(() => api.updateAlliance({ duesPercent: dues, door }))}
      >Save</Button>
    </div>
    <small className="d-block mt-1 text-body-tertiary small measure">
      Dues come off the gross of every member's shift, beside the hoe cut. The ceiling is {maxDues}%.{' '}
      {board.doors.find(x => x.door === door)?.detail}
    </small>
  </div>
}

