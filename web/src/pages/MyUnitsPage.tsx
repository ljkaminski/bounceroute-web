import { useMyUnits } from '../api/hooks'
import { ErrorBox, Loading } from '../components/Feedback'
import { PeriodPicker } from '../components/PeriodPicker'
import { usePeriod } from '../lib/usePeriod'

export function MyUnitsPage() {
  const period = usePeriod()
  const units = useMyUnits(period.range[0], period.range[1])
  const mine = units.data?.crew[0]

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold">My units</h1>
        <p className="text-sm text-slate-500">Units on jobs you were on that are marked complete.</p>
      </div>

      <PeriodPicker period={period} />

      {units.isPending ? (
        <Loading what="Counting units" />
      ) : units.isError ? (
        <ErrorBox error={units.error} />
      ) : (
        <div className="grid gap-4 sm:grid-cols-3">
          <Stat label="Total units" value={mine?.totalUnits ?? 0} highlight />
          <Stat label="Setup units" value={mine?.setupUnits ?? 0} />
          <Stat label="Pickup units" value={mine?.pickupUnits ?? 0} />
          <p className="text-sm text-slate-500 sm:col-span-3">
            From {mine?.jobs ?? 0} completed {mine?.jobs === 1 ? 'job' : 'jobs'}.
          </p>
        </div>
      )}
    </div>
  )
}

function Stat({ label, value, highlight }: { label: string; value: number; highlight?: boolean }) {
  return (
    <div className={`card p-5 ${highlight ? 'ring-2 ring-brand' : ''}`}>
      <div className="label">{label}</div>
      <div className="text-4xl font-bold">{value}</div>
    </div>
  )
}
