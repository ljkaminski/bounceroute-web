import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { RouteSuggestion } from '../api/client'
import { mockApi, renderRoutes } from '../test/render'
import { SuggestionCard } from './SuggestionCard'

const better: RouteSuggestion = {
  date: '2026-11-14',
  status: 'Better',
  reason: null,
  current: { lateStops: 1, allDoneAt: '2026-11-15T00:30:00Z', trucksUsed: 1 },
  suggested: { lateStops: 0, allDoneAt: '2026-11-14T23:10:00Z', trucksUsed: 2 },
  changes: [
    {
      jobId: 3,
      type: 'Setup',
      customerName: 'Blake',
      fromTruck: 'Truck 1',
      toTruck: 'Truck 2',
      fromTime: '2026-11-14T16:30:00Z',
      toTime: '2026-11-14T16:00:00Z',
    },
  ],
  cantFit: [],
  fingerprint: 'abc123',
  suggestedTrucks: [
    {
      truck: 'Truck 2',
      leaveShopAt: '2026-11-14T15:50:00Z',
      doneAt: '2026-11-14T16:30:00Z',
      backAtShopAt: '2026-11-14T16:40:00Z',
      workMinutes: 30,
      driveMinutes: 20,
      driveTimesComplete: true,
      stops: [
        {
          jobId: 3,
          type: 'Setup',
          customerName: 'Blake',
          street: '1 Blake St',
          scheduledAt: '2026-11-14T16:00:00Z',
          driveMinutesBefore: 10,
          arriveAt: '2026-11-14T16:00:00Z',
          startAt: '2026-11-14T16:00:00Z',
          finishAt: '2026-11-14T16:30:00Z',
          workMinutes: 30,
          minutesLate: 0,
          partyStartsAt: '2026-11-14T17:00:00Z',
        },
      ],
    },
  ],
}

const routes = [{ path: '/', element: <SuggestionCard date="2026-11-14" /> }]

afterEach(() => vi.unstubAllGlobals())

it('explains the better plan in plain words and uses it with one button', async () => {
  const calls = mockApi({
    'GET /api/route/suggestion': () => ({ status: 200, body: better }),
    'POST /api/route/suggestion/apply': () => ({ status: 200, body: { jobsChanged: 1 } }),
  })
  renderRoutes(routes, '/')

  expect(await screen.findByText('We found a better plan for this day')).toBeInTheDocument()
  expect(screen.getByText(/Everything is set up before the parties start/)).toBeInTheDocument()
  expect(screen.getByText(/Everyone back by 6:10 PM instead of 7:30 PM/)).toBeInTheDocument()
  expect(screen.getByText('Moves 1 party to Truck 2. The new day for each truck:')).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Truck 2' })).toBeInTheDocument()
  expect(screen.getByText('11:00 AM')).toBeInTheDocument()

  await userEvent.click(screen.getByRole('button', { name: 'Use this plan' }))

  expect(await screen.findByText('Plan updated.')).toBeInTheDocument()
  expect(calls.find((call) => call.method === 'POST')?.body).toEqual({ fingerprint: 'abc123' })
})

it('says so quietly when the plan is already the best, and names stops that will not fit', async () => {
  mockApi({
    'GET /api/route/suggestion': () => ({
      status: 200,
      body: { ...better, status: 'AlreadyBest', changes: [], cantFit: ['Grace Church (setup)'] },
    }),
  })
  renderRoutes(routes, '/')

  expect(await screen.findByText(/already the best plan/)).toBeInTheDocument()
  expect(screen.getByText('Grace Church (setup)')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Use this plan' })).not.toBeInTheDocument()
})
