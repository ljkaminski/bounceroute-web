// Dates in the UI are plain "yyyy-mm-dd" strings in the browser's time zone, which is the
// office's time zone. The API takes and returns full ISO timestamps for job times.

const pad = (n: number) => String(n).padStart(2, '0')

export function toDateKey(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export function parseDateKey(key: string): Date {
  const [year, month, day] = key.split('-').map(Number)
  return new Date(year, month - 1, day)
}

export const today = () => toDateKey(new Date())

export function addDays(key: string, days: number): string {
  const date = parseDateKey(key)
  date.setDate(date.getDate() + days)
  return toDateKey(date)
}

// The weekend run starts Friday morning and includes Monday pickups.
export function weekendStart(key: string): string {
  const daysSinceFriday = (parseDateKey(key).getDay() - 5 + 7) % 7
  return addDays(key, -daysSinceFriday)
}

export function monthRange(key: string): [string, string] {
  const date = parseDateKey(key)
  return [
    toDateKey(new Date(date.getFullYear(), date.getMonth(), 1)),
    toDateKey(new Date(date.getFullYear(), date.getMonth() + 1, 0)),
  ]
}

// <input type="datetime-local"> works in local wall-clock time with no zone.
export function toDateTimeInput(iso: string): string {
  const date = new Date(iso)
  return `${toDateKey(date)}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export const fromDateTimeInput = (value: string) => new Date(value).toISOString()

export const formatTime = (iso: string) =>
  new Date(iso).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' })

export const formatDay = (key: string) =>
  parseDateKey(key).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })

export const formatShortDateTime = (iso: string) =>
  new Date(iso).toLocaleString('en-US', { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })

// 75 -> "1 hr 15 min", 45 -> "45 min"
export function formatMinutes(total: number): string {
  const hours = Math.floor(total / 60)
  const minutes = total % 60
  if (hours === 0) return `${minutes} min`
  return minutes === 0 ? `${hours} hr` : `${hours} hr ${minutes} min`
}
