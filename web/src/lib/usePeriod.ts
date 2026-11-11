import { useState } from 'react'
import { addDays, monthRange, parseDateKey, today, weekendStart } from './dates'

export type Preset = 'weekend' | 'week' | 'month' | 'custom'

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

// This weekend (Fri–Mon), this week, this month, or any two dates.
export function usePeriod() {
  const [preset, setPreset] = useState<Preset>('weekend')
  const [range, setRange] = useState(() => presetRange('weekend'))

  const choose = (next: Preset) => {
    setPreset(next)
    if (next !== 'custom') setRange(presetRange(next))
  }

  return { preset, range, choose, setRange }
}
