import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useCompleteJob, useCrew, useJob, useReopenJob, useSetItemLoaded } from '../api/hooks'
import type { JobCard } from '../api/client'
import { isOffice } from '../auth/roles'
import { useCurrentUser } from '../auth/useAuth'
import { CrewChips, StatusBadge, TypeBadge } from '../components/Badges'
import { ErrorBox, Loading } from '../components/Feedback'
import { formatShortDateTime, formatTime, toDateKey } from '../lib/dates'

export function JobPage() {
  const id = Number(useParams().id)
  const job = useJob(id)

  if (job.isPending) return <Loading what="Loading job" />
  if (job.isError) return <ErrorBox error={job.error} />
  return <JobDetail job={job.data} />
}

function JobDetail({ job }: { job: JobCard }) {
  const office = isOffice(useCurrentUser().data)
  const address = `${job.street}, ${job.cityStateZip}`
  const routeDate = toDateKey(new Date(job.scheduledAt))

  return (
    <div className="space-y-5">
      <Link to={`/?date=${routeDate}`} className="inline-block text-sm font-semibold text-slate-500 hover:text-slate-800">
        ‹ Back to route
      </Link>

      <section className="card p-5">
        <div className="flex flex-wrap items-center gap-2">
          <TypeBadge type={job.type} />
          <StatusBadge job={job} />
          <span className="text-sm text-slate-400">Job #{job.jobNumber}</span>
        </div>
        <h1 className="mt-2 text-2xl font-bold">{job.customerName}</h1>
        <p className="text-slate-600">{address}</p>
        <p className="mt-1 text-sm text-slate-500">
          {formatShortDateTime(job.scheduledAt)} · {job.surface} · {job.truck || 'No truck set'}
        </p>

        <div className="mt-4 flex flex-wrap gap-2">
          {job.phone && (
            <a className="btn-secondary" href={`tel:${job.phone.replace(/[^\d+]/g, '')}`}>
              Call {job.phone}
            </a>
          )}
          <a
            className="btn-secondary"
            href={`https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(address)}`}
            target="_blank"
            rel="noreferrer"
          >
            Directions
          </a>
          {office && (
            <Link className="btn-secondary" to={`/bookings/${job.bookingId}/edit`}>
              Edit booking
            </Link>
          )}
        </div>

        {job.siteNotes && (
          <p className="mt-4 rounded-xl bg-amber-50 px-4 py-3 text-sm text-amber-900 ring-1 ring-amber-200">
            <span className="font-semibold">Site notes: </span>
            {job.siteNotes}
          </p>
        )}
      </section>

      <div className="grid gap-5 md:grid-cols-2">
        <section className="card p-5">
          <h2 className="font-bold">Loading list</h2>
          <LoadList title="Units" items={job.units} />
          <LoadList title="Extras" items={job.extras} />
        </section>

        <section className="card space-y-4 p-5">
          <div>
            <h2 className="font-bold">Crew</h2>
            <div className="mt-2 flex items-center gap-3">
              <CrewChips crew={job.crew} />
              <span className="text-sm text-slate-600">{job.crew.map((member) => member.name).join(', ')}</span>
            </div>
          </div>
          {office ? <CompletePanel job={job} /> : <CrewCompletePanel job={job} />}
        </section>
      </div>
    </div>
  )
}

function LoadList({ title, items }: { title: string; items: JobCard['units'] }) {
  const setLoaded = useSetItemLoaded()
  if (items.length === 0) return null

  return (
    <div className="mt-3">
      <h3 className="label">{title}</h3>
      <ul className="space-y-1">
        {items.map((item) => (
          <li key={item.id}>
            <label className="flex cursor-pointer items-center gap-3 rounded-lg px-2 py-1.5 hover:bg-slate-50">
              <input
                type="checkbox"
                className="size-4 accent-brand"
                checked={item.isLoaded}
                disabled={setLoaded.isPending}
                onChange={(event) => setLoaded.mutate({ itemId: item.id, isLoaded: event.target.checked })}
              />
              <span className={item.isLoaded ? 'text-slate-400 line-through' : ''}>{item.name}</span>
            </label>
          </li>
        ))}
      </ul>
    </div>
  )
}

function CompletedNote({ job }: { job: JobCard }) {
  const reopen = useReopenJob()
  return (
    <div className="rounded-xl bg-green-50 p-4 text-sm text-green-900 ring-1 ring-green-200">
      Completed {job.completedAt && `at ${formatTime(job.completedAt)}`}
      {job.completedBy && ` by ${job.completedBy}`}.
      <button className="ml-2 font-semibold underline" onClick={() => reopen.mutate(job.jobId)} disabled={reopen.isPending}>
        Undo
      </button>
    </div>
  )
}

// Crew complete their own jobs; the API records who they are.
function CrewCompletePanel({ job }: { job: JobCard }) {
  const complete = useCompleteJob()
  if (job.status === 'Done') return <CompletedNote job={job} />

  return (
    <div className="space-y-2">
      <button className="btn-primary w-full py-3 text-base" disabled={complete.isPending}
        onClick={() => complete.mutate({ jobId: job.jobId })}>
        Mark complete
      </button>
      {complete.isError && <ErrorBox error={complete.error} />}
    </div>
  )
}

// Office staff record which crew member finished the job.
function CompletePanel({ job }: { job: JobCard }) {
  const crew = useCrew()
  const complete = useCompleteJob()
  const [crewMemberId, setCrewMemberId] = useState<number | ''>(job.crew[0]?.id ?? '')

  if (job.status === 'Done') return <CompletedNote job={job} />

  return (
    <form
      className="space-y-2"
      onSubmit={(event) => {
        event.preventDefault()
        if (crewMemberId !== '') complete.mutate({ jobId: job.jobId, crewMemberId })
      }}
    >
      <label className="label" htmlFor="completed-by">
        Completed by
      </label>
      <div className="flex gap-2">
        <select
          id="completed-by"
          className="field"
          value={crewMemberId}
          onChange={(event) => setCrewMemberId(event.target.value ? Number(event.target.value) : '')}
        >
          <option value="">Choose crew member</option>
          {crew.data?.map((member) => (
            <option key={member.id} value={member.id}>
              {member.name}
            </option>
          ))}
        </select>
        <button className="btn-primary shrink-0" disabled={crewMemberId === '' || complete.isPending}>
          Mark complete
        </button>
      </div>
      {complete.isError && <ErrorBox error={complete.error} />}
    </form>
  )
}
