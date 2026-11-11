import type { usePeriod } from '../lib/usePeriod'

export function PeriodPicker({ period }: { period: ReturnType<typeof usePeriod> }) {
  const { preset, range, choose, setRange } = period

  return (
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
  )
}
