import { useSaveTrucksOut, useTrucks, useTrucksOut } from '../api/hooks'

// Tap a truck to send it out today or keep it home. Saves right away; at least one truck stays on.
export function TrucksOutPicker({ date }: { date: string }) {
  const trucks = useTrucks()
  const out = useTrucksOut(date)
  const save = useSaveTrucksOut(date)

  if (!trucks.data || !out.data || trucks.data.length === 0) return null
  const outIds = save.isPending && save.variables ? save.variables : out.data.truckIds
  const choices = trucks.data.filter((truck) => truck.isActive || outIds.includes(truck.id))

  const toggle = (id: number) => {
    const next = outIds.includes(id) ? outIds.filter((other) => other !== id) : [...outIds, id]
    if (next.length > 0) save.mutate(next)
  }

  return (
    <div className="card flex flex-wrap items-center gap-2 px-4 py-3">
      <span className="mr-1 text-sm font-semibold">Trucks going out:</span>
      {choices.map((truck) => {
        const on = outIds.includes(truck.id)
        const last = on && outIds.length === 1
        return (
          <button
            key={truck.id}
            type="button"
            aria-pressed={on}
            disabled={last}
            title={last ? 'At least one truck goes out' : undefined}
            onClick={() => toggle(truck.id)}
            className={`rounded-full px-4 py-1.5 text-sm font-semibold ring-1 ${on ? 'bg-navy text-white ring-navy' : 'bg-white text-slate-500 ring-slate-300 line-through'} disabled:cursor-default`}
          >
            {truck.name}
          </button>
        )
      })}
      <span className="text-xs text-slate-500">Tap a truck to turn it on or off for this day.</span>
    </div>
  )
}
