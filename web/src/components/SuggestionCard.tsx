import { useEffect, useState } from 'react'
import { ApiError, type RouteSuggestion } from '../api/client'
import { useApplySuggestion, useSuggestion } from '../api/hooks'
import { formatTime } from '../lib/dates'

// Shows up on its own when the route planner finds a better way to run the day. One button uses it.
export function SuggestionCard({ date }: { date: string }) {
  const suggestion = useSuggestion(date)
  const apply = useApplySuggestion(date)
  const [done, setDone] = useState(false)

  // The confirmation stays long enough to read, then the card goes back to watching for a better plan.
  useEffect(() => {
    if (!done) return
    const timer = setTimeout(() => setDone(false), 6000)
    return () => clearTimeout(timer)
  }, [done])

  if (done)
    return (
      <div role="status" className="rounded-2xl bg-green-50 px-5 py-4 text-green-900 ring-1 ring-green-200">
        <p className="font-semibold">Plan updated.</p>
        <p className="text-sm">Crews will see the new trucks and times on their phones.</p>
      </div>
    )

  if (suggestion.isPending) return <p className="text-sm text-slate-500">Checking for a better plan…</p>
  if (suggestion.isError || !suggestion.data) return null
  const plan = suggestion.data

  if (plan.status === 'NotAvailable') {
    // Nothing to say on an empty day or a day already under way.
    if (!plan.reason || plan.reason.startsWith('No jobs') || plan.reason.includes('has started')) return null
    return <p className="rounded-xl bg-amber-50 px-4 py-2 text-sm text-amber-900 ring-1 ring-amber-200">{plan.reason}</p>
  }

  if (plan.status === 'AlreadyBest')
    return (
      <div className="space-y-2">
        <p className="text-sm text-green-700">✓ This is already the best plan for this day.</p>
        <CantFit plan={plan} />
      </div>
    )

  const conflict = apply.error instanceof ApiError && apply.error.status === 409

  return (
    <section aria-label="Better plan" className="space-y-3 rounded-2xl bg-white p-5 ring-2 ring-green-500">
      <div>
        <h2 className="text-lg font-bold text-green-800">We found a better plan for this day</h2>
        <ul className="mt-1 space-y-0.5 text-sm text-slate-700">
          {benefits(plan).map((line) => (
            <li key={line}>✓ {line}</li>
          ))}
        </ul>
      </div>

      <p className="text-sm text-slate-700">{changeSummary(plan)}</p>

      <div className="grid gap-3 sm:grid-cols-2">
        {plan.suggestedTrucks.map((truck) => (
          <div key={truck.truck} className="rounded-xl bg-slate-50 p-3">
            <h3 className="font-bold">{truck.truck}</h3>
            <p className="text-xs text-slate-500">
              Leave {truck.leaveShopAt ? formatTime(truck.leaveShopAt) : '—'} · back {truck.backAtShopAt ? formatTime(truck.backAtShopAt) : formatTime(truck.doneAt)}
            </p>
            <ol className="mt-2 space-y-1 text-sm">
              {truck.stops.map((stop) => (
                <li key={stop.jobId} className="flex gap-2">
                  <span className="w-20 shrink-0 font-semibold whitespace-nowrap">{formatTime(stop.startAt)}</span>
                  <span>
                    {stop.customerName} <span className="text-slate-500">{stop.type === 'Setup' ? 'setup' : 'pickup'}</span>
                  </span>
                </li>
              ))}
            </ol>
          </div>
        ))}
      </div>

      <CantFit plan={plan} />

      <button
        type="button"
        className="btn-primary w-full py-3 text-base sm:w-auto"
        disabled={apply.isPending}
        onClick={() => apply.mutate(plan.fingerprint!, { onSuccess: () => setDone(true) })}
      >
        {apply.isPending ? 'Updating…' : 'Use this plan'}
      </button>
      {conflict && <p className="text-sm text-amber-800">Something changed on this day, so here's the updated plan. Take a look and try again.</p>}
      {apply.isError && !conflict && <p className="text-sm text-red-700">{apply.error.message}</p>}
    </section>
  )
}

function CantFit({ plan }: { plan: RouteSuggestion }) {
  if (plan.cantFit.length === 0) return null
  return (
    <p className="rounded-xl bg-amber-50 px-4 py-2 text-sm text-amber-900 ring-1 ring-amber-200">
      Not enough room or time on the trucks going out for: <b>{plan.cantFit.join(', ')}</b>. Turn on another truck above, or move
      that booking.
    </p>
  )
}

// The reasons to use it, in plain words.
function benefits(plan: RouteSuggestion): string[] {
  const lines: string[] = []
  const now = plan.current
  const next = plan.suggested
  if (now && next) {
    if (next.lateStops === 0 && now.lateStops > 0) lines.push('Everything is set up before the parties start')
    else if (next.lateStops < now.lateStops)
      lines.push(`${now.lateStops - next.lateStops} fewer ${now.lateStops - next.lateStops === 1 ? 'stop' : 'stops'} running late`)
    if (next.allDoneAt && now.allDoneAt && new Date(next.allDoneAt) < new Date(now.allDoneAt))
      lines.push(`Everyone back by ${formatTime(next.allDoneAt)} instead of ${formatTime(now.allDoneAt)}`)
  }
  if (lines.length === 0) lines.push('Puts every stop on a truck that is going out')
  return lines
}

// One sentence about what the plan moves, e.g. "Moves 5 parties to Truck 2 and adjusts arrival times."
function changeSummary(plan: RouteSuggestion): string {
  const moved = plan.changes.filter((change) => change.fromTruck !== change.toTruck)
  const parties = new Map<string, Set<string>>()
  for (const change of moved) parties.set(change.toTruck, (parties.get(change.toTruck) ?? new Set()).add(change.customerName))
  const moves = [...parties].map(([truck, names]) => `${names.size} ${names.size === 1 ? 'party' : 'parties'} to ${truck}`)
  if (moves.length === 0) return 'Keeps every truck the same and adjusts arrival times:'
  const retimed = plan.changes.length > moved.length
  return `Moves ${moves.join(' and ')}${retimed ? ' and adjusts arrival times' : ''}. The new day for each truck:`
}
