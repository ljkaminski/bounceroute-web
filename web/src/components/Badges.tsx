import type { JobCard } from '../api/client'

export function TypeBadge({ type }: { type: JobCard['type'] }) {
  const style = type === 'Setup' ? 'bg-blue-50 text-blue-700 ring-blue-200' : 'bg-violet-50 text-violet-700 ring-violet-200'
  return <span className={`rounded-md px-2 py-0.5 text-xs font-bold tracking-wide uppercase ring-1 ${style}`}>{type}</span>
}

export function StatusBadge({ job }: { job: Pick<JobCard, 'status' | 'isNextUp'> }) {
  if (job.status === 'Done')
    return <span className="rounded-md bg-green-50 px-2 py-0.5 text-xs font-bold text-green-700 ring-1 ring-green-200">Done</span>
  if (job.isNextUp)
    return <span className="rounded-md bg-brand px-2 py-0.5 text-xs font-bold text-white">NEXT UP</span>
  return <span className="rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600">Upcoming</span>
}

export function CrewChips({ crew }: { crew: JobCard['crew'] }) {
  if (crew.length === 0) return <span className="text-xs text-amber-700">No crew assigned</span>
  return (
    <div className="flex -space-x-1.5">
      {crew.map((member) => (
        <span
          key={member.id}
          title={member.name}
          className="grid size-7 place-items-center rounded-full bg-navy text-[11px] font-bold text-white ring-2 ring-white"
        >
          {member.initials}
        </span>
      ))}
    </div>
  )
}
