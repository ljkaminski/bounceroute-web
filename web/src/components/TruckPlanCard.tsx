import type { RoutePlan, TruckPlan } from '../api/client'
import { formatMinutes, formatTime } from '../lib/dates'

// One truck's day at a glance: when to leave, when the last stop is done, and anything running late.
export function TruckPlanCard({ plan, route }: { plan: TruckPlan; route: RoutePlan }) {
  const late = plan.stops.filter((stop) => stop.minutesLate > 0)

  return (
    <div className="card p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="font-bold">{plan.truck}</h3>
        <span className="text-sm text-slate-500">
          {plan.stops.length} {plan.stops.length === 1 ? 'stop' : 'stops'}
        </span>
      </div>
      <dl className="mt-2 grid grid-cols-3 gap-2 text-center">
        <Stat label="Leave shop" value={plan.leaveShopAt ? formatTime(plan.leaveShopAt) : '—'} />
        <Stat label="Last stop done" value={formatTime(plan.doneAt)} highlight />
        <Stat label="Back at shop" value={plan.backAtShopAt ? formatTime(plan.backAtShopAt) : '—'} />
      </dl>
      <p className="mt-2 text-xs text-slate-500">
        {formatMinutes(plan.workMinutes)} setting up{plan.driveMinutes > 0 && ` · ${formatMinutes(plan.driveMinutes)} driving`}
      </p>
      {late.length > 0 && (
        <p className="mt-2 rounded-lg bg-red-50 px-3 py-1.5 text-xs font-semibold text-red-800">
          {late.length === 1 ? '1 stop' : `${late.length} stops`} running late (up to {formatMinutes(Math.max(...late.map((stop) => stop.minutesLate)))})
        </p>
      )}
      {!plan.driveTimesComplete && (
        <p className="mt-2 text-xs text-amber-700">
          {!route.mapsConfigured
            ? 'Drive times not included yet.'
            : !route.shopAddressSet
              ? 'Add the shop address in Settings to include driving to and from the shop.'
              : "Some addresses couldn't be found on the map, so drive times are missing."}
        </p>
      )}
    </div>
  )
}

function Stat({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div className={`rounded-lg px-2 py-1.5 ${highlight ? 'bg-orange-50 ring-1 ring-brand/40' : 'bg-slate-50'}`}>
      <dt className="text-[11px] font-semibold tracking-wide text-slate-500 uppercase">{label}</dt>
      <dd className="text-base font-bold">{value}</dd>
    </div>
  )
}
