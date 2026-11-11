import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { CurrentUser, JobCard } from '../api/client'
import { mockApi, renderRoutes } from '../test/render'
import { JobPage } from './JobPage'

const job: JobCard = {
  jobId: 7,
  bookingId: 3,
  jobNumber: 1042,
  type: 'Setup',
  scheduledAt: '2026-11-14T14:30:00Z',
  truck: 'Truck 2',
  status: 'Upcoming',
  completedAt: null,
  completedBy: null,
  isNextUp: true,
  customerName: 'Avery Thompson',
  phone: '(252) 555-0141',
  street: '118 Oak Hollow Dr',
  cityStateZip: 'Greenville, NC 27858',
  surface: 'Grass',
  siteNotes: '',
  units: [{ id: 1, name: 'Castle Combo', isUnit: true, isLoaded: false, inventoryItemId: null, isWet: null, photoUrl: null }],
  extras: [],
  crew: [{ id: 1, name: 'Jordan Hale', initials: 'JH' }],
  workMinutes: 30,
}

const user = (role: CurrentUser['role']): CurrentUser => ({
  id: 'u1',
  email: 'someone@example.com',
  displayName: 'Someone',
  role,
  crewMemberId: role === 'Crew' ? 1 : null,
})

const routes = [{ path: '/jobs/:id', element: <JobPage /> }]

afterEach(() => vi.unstubAllGlobals())

it('lets crew mark their own job complete without picking a name', async () => {
  const calls = mockApi({
    'GET /api/auth/me': () => ({ status: 200, body: user('Crew') }),
    'GET /api/jobs/7': () => ({ status: 200, body: job }),
    'POST /api/jobs/7/complete': () => ({ status: 200, body: { ...job, status: 'Done', completedBy: 'Jordan Hale' } }),
  })
  renderRoutes(routes, '/jobs/7')

  await userEvent.click(await screen.findByRole('button', { name: 'Mark complete' }))

  expect(screen.queryByLabelText('Completed by')).not.toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'Edit booking' })).not.toBeInTheDocument()
  expect(calls.find((call) => call.method === 'POST')?.body).toEqual({ crewMemberId: null })
})

it('lets office staff edit the booking and choose who completed it', async () => {
  mockApi({
    'GET /api/auth/me': () => ({ status: 200, body: user('Office') }),
    'GET /api/jobs/7': () => ({ status: 200, body: job }),
    'GET /api/crew': () => ({ status: 200, body: [{ id: 1, name: 'Jordan Hale', initials: 'JH', role: 'Crew', isActive: true }] }),
  })
  renderRoutes(routes, '/jobs/7')

  expect(await screen.findByRole('link', { name: 'Edit booking' })).toBeInTheDocument()
  expect(screen.getByLabelText('Completed by')).toBeInTheDocument()
})
