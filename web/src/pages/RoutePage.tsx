import { Link, useSearchParams } from 'react-router'
import { useRouteDay, useRoutePlan, useSettings } from '../api/hooks'
import { isOffice } from '../auth/roles'
import { useCurrentUser } from '../auth/useAuth'
import { EmptyState, ErrorBox, Loading } from '../components/Feedback'
import { JobCardRow } from '../components/JobCardRow'
import { SuggestionCard } from '../components/SuggestionCard'
import { TrucksOutPicker } from '../components/TrucksOutPicker'
import { TruckPlanCard } from '../components/TruckPlanCard'
import { addDays, formatDay, today } from '../lib/dates'

const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? '' : 's'}`

export function RoutePage() {
  const [params, setParams] = useSearchParams()
  const date = params.get('date') ?? today()
  const route = useRouteDay(date)
  const plan = useRoutePlan(date)
  const plannedStops = new Map(plan.data?.trucks.flatMap((truck) => truck.stops).map((stop) => [stop.jobId, stop]))
  const office = isOffice(useCurrentUser().data)
  const suggestRoutes = useSettings().data?.suggestRoutes ?? false
  const goTo = (day: string) => setParams(day === today() ? {} : { date: day })

  const jobs = route.data?.jobs ?? []
  const done = jobs.filter((job) => job.status === 'Done').length
  const units = jobs.reduce((sum, job) => sum + job.units.length, 0)

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold text-brand">
            {office ? (date === today() ? "Today's route" : 'Route') : date === today() ? 'My route today' : 'My route'}
          </p>
          <h1 className="text-2xl font-bold">{formatDay(date)}</h1>
          {jobs.length > 0 && (
            <p className="mt-1 text-sm text-slate-500">
              {plural(jobs.length, 'stop')} · {plural(units, 'unit')} · {done} of {jobs.length} done
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          <button className="btn-secondary" onClick={() => goTo(addDays(date, -1))} aria-label="Previous day">
            ‹
          </button>
          <button className="btn-secondary" onClick={() => goTo(today())} disabled={date === today()}>
            Today
          </button>
          <input
            type="date"
            className="field w-auto"
            value={date}
            onChange={(event) => event.target.value && goTo(event.target.value)}
            aria-label="Pick a day"
          />
          <button className="btn-secondary" onClick={() => goTo(addDays(date, 1))} aria-label="Next day">
            ›
          </button>
        </div>
      </div>

      {office && <TrucksOutPicker date={date} />}
      {office && suggestRoutes && jobs.length > 0 && <SuggestionCard key={date} date={date} />}

      {route.isPending ? (
        <Loading what="Loading route" />
      ) : route.isError ? (
        <ErrorBox error={route.error} />
      ) : jobs.length === 0 ? (
        <EmptyState>
          {office ? (
            <>
              No jobs on this day.{' '}
              <Link to="/bookings/new" className="font-semibold text-brand hover:underline">
                Add a booking
              </Link>
            </>
          ) : (
            "You're not on any jobs this day."
          )}
        </EmptyState>
      ) : (
        <>
          {plan.data && plan.data.trucks.length > 0 && (
            <section aria-label="Truck plans" className="grid gap-3 sm:grid-cols-2">
              {plan.data.trucks.map((truck) => (
                <TruckPlanCard key={truck.truck} plan={truck} route={plan.data} />
              ))}
            </section>
          )}
          <ol className="space-y-3">
            {jobs.map((job) => (
              <li key={job.jobId}>
                <JobCardRow job={job} plan={plannedStops.get(job.jobId)} />
              </li>
            ))}
          </ol>
        </>
      )}
    </div>
  )
}
