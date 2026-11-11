import { addDays, formatMinutes, fromDateTimeInput, monthRange, toDateTimeInput, weekendStart } from './dates'

describe('weekendStart', () => {
  it.each([
    ['2026-11-13', '2026-11-13'], // Friday
    ['2026-11-15', '2026-11-13'], // Sunday
    ['2026-11-16', '2026-11-13'], // Monday pickups still count
    ['2026-11-19', '2026-11-13'], // Thursday
  ])('%s starts the weekend of %s', (day, expected) => {
    expect(weekendStart(day)).toBe(expected)
  })
})

describe('date helpers', () => {
  it('adds days across a month boundary', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01')
  })

  it('gives the first and last day of the month', () => {
    expect(monthRange('2026-02-14')).toEqual(['2026-02-01', '2026-02-28'])
  })

  it('round-trips a datetime-local value through ISO', () => {
    const iso = fromDateTimeInput('2026-11-14T09:30')
    expect(iso).toBe('2026-11-14T14:30:00.000Z')
    expect(toDateTimeInput(iso)).toBe('2026-11-14T09:30')
  })
})

it('formats durations the way people say them', () => {
  expect([formatMinutes(45), formatMinutes(60), formatMinutes(75)]).toEqual(['45 min', '1 hr', '1 hr 15 min'])
})
