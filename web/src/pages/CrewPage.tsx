import { useState } from 'react'
import { useAddCrewMember, useUnitTotals } from '../api/hooks'
import { ErrorBox, Loading } from '../components/Feedback'
import { addDays, monthRange, parseDateKey, today, weekendStart } from '../lib/dates'

type Preset = 'weekend' | 'week' | 'month' | 'custom'

function presetRange(preset: Exclude<Preset, 'custom'>): [string, string] {
  const now = today()
  if (preset === 'weekend') {
    const friday = weekendStart(now)
    return [friday, addDays(friday, 3)]
  }
  if (preset === 'week') {
    const monday = addDays(now, -((parseDateKey(now).getDay() + 6) % 7))
    return [monday, addDays(monday, 6)]
  }
  return monthRange(now)
}

export function CrewPage() {
  const [preset, setPreset] = useState<Preset>('weekend')
  const [range, setRange] = useState(() => presetRange('weekend'))
  const totals = useUnitTotals(range[0], range[1])

  const choose = (next: Preset) => {
    setPreset(next)
    if (next !== 'custom') setRange(presetRange(next))
  }

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold">Crew & pay</h1>
        <p className="text-sm text-slate-500">Units on completed jobs. Extras like generators don't count.</p>
      </div>

      <div className="flex flex-wrap items-end gap-2">
        <div className="flex rounded-xl bg-white p-1 ring-1 ring-slate-300" role="group" aria-label="Period">
          {(['weekend', 'week', 'month', 'custom'] as const).map((option) => (
            <button
              key={option}
              onClick={() => choose(option)}
              aria-pressed={preset === option}
              className={`rounded-lg px-3 py-1.5 text-sm font-semibold ${preset === option ? 'bg-navy text-white' : 'text-slate-600'}`}
            >
              {option === 'weekend' ? 'This weekend' : option === 'custom' ? 'Custom' : `This ${option}`}
            </button>
          ))}
        </div>
        {preset === 'custom' && (
          <>
            <input type="date" className="field w-auto" aria-label="From" value={range[0]}
              onChange={(event) => event.target.value && setRange([event.target.value, range[1]])} />
            <input type="date" className="field w-auto" aria-label="To" value={range[1]}
              onChange={(event) => event.target.value && setRange([range[0], event.target.value])} />
          </>
        )}
      </div>

      {totals.isPending ? (
        <Loading what="Counting units" />
      ) : totals.isError ? (
        <ErrorBox error={totals.error} />
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 text-xs text-slate-500 uppercase">
              <tr>
                <th className="px-4 py-3">Crew member</th>
                <th className="px-4 py-3 text-right">Jobs</th>
                <th className="px-4 py-3 text-right">Setup units</th>
                <th className="px-4 py-3 text-right">Pickup units</th>
                <th className="px-4 py-3 text-right">Total units</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {totals.data.crew.map((member) => (
                <tr key={member.crewMemberId}>
                  <td className="px-4 py-3">
                    <span className="mr-3 inline-grid size-7 place-items-center rounded-full bg-navy text-[11px] font-bold text-white">
                      {member.initials}
                    </span>
                    {member.name}
                  </td>
                  <td className="px-4 py-3 text-right">{member.jobs}</td>
                  <td className="px-4 py-3 text-right">{member.setupUnits}</td>
                  <td className="px-4 py-3 text-right">{member.pickupUnits}</td>
                  <td className="px-4 py-3 text-right text-base font-bold">{member.totalUnits}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <AddCrewForm />
    </div>
  )
}

function AddCrewForm() {
  const add = useAddCrewMember()
  const [name, setName] = useState('')
  const [role, setRole] = useState('Crew')

  return (
    <form
      className="card flex flex-wrap items-end gap-3 p-4"
      onSubmit={(event) => {
        event.preventDefault()
        add.mutate({ name, role }, { onSuccess: () => setName('') })
      }}
    >
      <label className="min-w-48 flex-1">
        <span className="label">New crew member</span>
        <input className="field" value={name} onChange={(event) => setName(event.target.value)} placeholder="Full name" required />
      </label>
      <label>
        <span className="label">Role</span>
        <select className="field" value={role} onChange={(event) => setRole(event.target.value)}>
          <option>Crew</option>
          <option>Crew Lead</option>
        </select>
      </label>
      <button className="btn-primary" disabled={add.isPending}>
        Add
      </button>
      {add.isError && <div className="w-full"><ErrorBox error={add.error} /></div>}
    </form>
  )
}
