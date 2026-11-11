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

      <div>
        <h3 className="label">What changes</h3>
        <ul className="space-y-1 text-sm">
          {plan.changes.map((change) => (
            <li key={change.jobId} className="rounded-lg bg-slate-50 px-3 py-2">
              <span className="font-semibold">{change.customerName}</span> {change.type === 'Setup' ? 'setup' : 'pickup'}:{' '}
              {change.fromTruck !== change.toTruck && (
                <>
                  move to <b>{change.toTruck}</b>,{' '}
                </>
              )}
              arrive <b>{formatTime(change.toTime)}</b>
              <span className="text-slate-500">
                {' '}
                (was {formatTime(change.fromTime)}
                {change.fromTruck !== change.toTruck && ` on ${change.fromTruck}`})
              </span>
            </li>
          ))}
        </ul>
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
