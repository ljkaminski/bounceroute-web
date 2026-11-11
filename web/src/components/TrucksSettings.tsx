import { useState } from 'react'
import type { CrewMember, Truck } from '../api/client'
import { useCrew, useSaveTruck, useTrucks } from '../api/hooks'
import { ErrorBox, Loading } from './Feedback'

// The owner's list of trucks: how much each carries and who usually rides it.
export function TrucksSettings() {
  const trucks = useTrucks()
  const crew = useCrew()
  const [adding, setAdding] = useState(false)

  return (
    <section className="card mx-auto max-w-lg space-y-4 p-6">
      <div>
        <h2 className="text-xl font-bold">Trucks</h2>
        <p className="text-sm text-slate-500">
          How many pieces fit on each truck. Most units are 1 piece; the 30 ft slides are 2. The route planner never loads more than this.
        </p>
      </div>
      {trucks.isPending ? (
        <Loading what="Loading trucks" />
      ) : trucks.isError ? (
        <ErrorBox error={trucks.error} />
      ) : (
        <ul className="space-y-3">
          {trucks.data.map((truck) => (
            <li key={truck.id}>
              <TruckForm truck={truck} crew={crew.data ?? []} />
            </li>
          ))}
        </ul>
      )}
      {adding ? (
        <TruckForm crew={crew.data ?? []} onDone={() => setAdding(false)} />
      ) : (
        <button type="button" className="btn-secondary" onClick={() => setAdding(true)}>
          Add a truck
        </button>
      )}
    </section>
  )
}

function TruckForm({ truck, crew, onDone }: { truck?: Truck; crew: CrewMember[]; onDone?: () => void }) {
  const save = useSaveTruck(truck?.id)
  const [name, setName] = useState(truck?.name ?? '')
  const [capacity, setCapacity] = useState(truck?.capacity ?? 6)
  const [isActive, setIsActive] = useState(truck?.isActive ?? true)
  const [crewIds, setCrewIds] = useState<number[]>(truck ? [...truck.crewMemberIds] : [])
  const toggle = (id: number) => setCrewIds(crewIds.includes(id) ? crewIds.filter((other) => other !== id) : [...crewIds, id])

  return (
    <form
      className="space-y-3 rounded-xl bg-slate-50 p-4"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate({ name, capacity, isActive, crewMemberIds: crewIds }, { onSuccess: () => onDone?.() })
      }}
    >
      <div className="grid grid-cols-2 gap-3">
        <label>
          <span className="label">Truck name</span>
          <input className="field" required maxLength={30} placeholder="Truck 1" value={name} onChange={(e) => setName(e.target.value)} />
        </label>
        <label>
          <span className="label">Pieces it holds</span>
          <input className="field" type="number" min={1} max={50} required value={capacity} onChange={(e) => setCapacity(Number(e.target.value))} />
        </label>
      </div>
      <div>
        <span className="label">Usual crew (optional)</span>
        <div className="flex flex-wrap gap-2">
          {crew.map((member) => {
            const on = crewIds.includes(member.id)
            return (
              <button key={member.id} type="button" aria-pressed={on} onClick={() => toggle(member.id)}
                className={`rounded-full px-3 py-1 text-sm font-medium ring-1 ${on ? 'bg-navy text-white ring-navy' : 'bg-white text-slate-600 ring-slate-300'}`}>
                {member.name}
              </button>
            )
          })}
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="accent-brand" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
          In service
        </label>
        <button className="btn-primary ml-auto" disabled={save.isPending}>
          {truck ? 'Save' : 'Add truck'}
        </button>
        {onDone && (
          <button type="button" className="btn-secondary" onClick={onDone}>
            Cancel
          </button>
        )}
      </div>
      {save.isError && <ErrorBox error={save.error} />}
      {save.isSuccess && truck && <p className="text-sm text-green-700">Saved.</p>}
    </form>
  )
}
