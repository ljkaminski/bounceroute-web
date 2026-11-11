import { useState } from 'react'
import { useAddCrewMember, useUnitTotals } from '../api/hooks'
import { ErrorBox, Loading } from '../components/Feedback'
import { PeriodPicker } from '../components/PeriodPicker'
import { usePeriod } from '../lib/usePeriod'

export function CrewPage() {
  const period = usePeriod()
  const totals = useUnitTotals(period.range[0], period.range[1])

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold">Crew & pay</h1>
        <p className="text-sm text-slate-500">Units on completed jobs. Extras like generators don't count.</p>
      </div>

      <PeriodPicker period={period} />

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
