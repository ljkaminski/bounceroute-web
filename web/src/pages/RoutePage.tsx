import { Link, useSearchParams } from 'react-router'
import { useRouteDay } from '../api/hooks'
import { EmptyState, ErrorBox, Loading } from '../components/Feedback'
import { JobCardRow } from '../components/JobCardRow'
import { addDays, formatDay, today } from '../lib/dates'

export function RoutePage() {
  const [params, setParams] = useSearchParams()
  const date = params.get('date') ?? today()
  const route = useRouteDay(date)
  const goTo = (day: string) => setParams(day === today() ? {} : { date: day })

  const jobs = route.data?.jobs ?? []
  const done = jobs.filter((job) => job.status === 'Done').length
  const units = jobs.reduce((sum, job) => sum + job.units.length, 0)

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold text-brand">{date === today() ? "Today's route" : 'Route'}</p>
          <h1 className="text-2xl font-bold">{formatDay(date)}</h1>
          {jobs.length > 0 && (
            <p className="mt-1 text-sm text-slate-500">
              {jobs.length} stops · {units} units · {done} of {jobs.length} done
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

      {route.isPending ? (
        <Loading what="Loading route" />
      ) : route.isError ? (
        <ErrorBox error={route.error} />
      ) : jobs.length === 0 ? (
        <EmptyState>
          No jobs on this day.{' '}
          <Link to="/bookings/new" className="font-semibold text-brand hover:underline">
            Add a booking
          </Link>
        </EmptyState>
      ) : (
        <ol className="space-y-3">
          {jobs.map((job) => (
            <li key={job.jobId}>
              <JobCardRow job={job} />
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}
