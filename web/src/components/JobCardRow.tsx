import { Link } from 'react-router'
import type { JobCard } from '../api/client'
import { formatTime } from '../lib/dates'
import { CrewChips, StatusBadge, TypeBadge } from './Badges'

export function JobCardRow({ job }: { job: JobCard }) {
  const units = job.units.length
  const wet = job.units.some((unit) => unit.isWet)
  return (
    <Link
      to={`/jobs/${job.jobId}`}
      className={`card flex gap-4 p-4 transition hover:ring-slate-300 ${job.isNextUp ? 'ring-2 ring-brand hover:ring-brand' : ''} ${job.status === 'Done' ? 'opacity-60' : ''}`}
    >
      <div className="w-16 shrink-0 text-center">
        <div className="text-lg leading-tight font-bold">{formatTime(job.scheduledAt).replace(/ [AP]M$/, '')}</div>
        <div className="text-xs font-semibold text-slate-500">{formatTime(job.scheduledAt).slice(-2)}</div>
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <TypeBadge type={job.type} />
          <StatusBadge job={job} />
          <span className="text-xs text-slate-400">#{job.jobNumber}</span>
        </div>
        <div className="mt-1 truncate font-semibold">{job.customerName}</div>
        <div className="truncate text-sm text-slate-600">{job.street}</div>
        <div className="mt-1 text-xs text-slate-500">
          {job.surface} · {units} {units === 1 ? 'unit' : 'units'}
          {job.extras.length > 0 && ` + ${job.extras.length} extra${job.extras.length === 1 ? '' : 's'}`}
          {job.truck && ` · ${job.truck}`}
          {wet && <span className="font-semibold text-sky-700"> · Wet</span>}
        </div>
      </div>
      <div className="hidden shrink-0 self-center sm:block">
        <CrewChips crew={job.crew} />
      </div>
    </Link>
  )
}
