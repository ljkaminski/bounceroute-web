import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { Booking } from '../api/client'
import { mockApi, renderRoutes } from '../test/render'
import { BookingFormPage } from './BookingFormPage'

const booking: Booking = {
  id: 7,
  jobNumber: 1042,
  customerName: 'Avery Thompson',
  phone: '',
  street: '1 Test St',
  cityStateZip: 'Monroe, NC 28110',
  surface: 'Grass',
  siteNotes: '',
  partyStartsAt: null,
  partyEndsAt: null,
  setup: { id: 1, scheduledAt: '2026-11-14T13:00:00Z', truck: 'Truck 1', status: 'Upcoming', crewMemberIds: [] },
  pickup: { id: 2, scheduledAt: '2026-11-14T22:00:00Z', truck: 'Truck 1', status: 'Upcoming', crewMemberIds: [] },
  items: [],
}

afterEach(() => vi.unstubAllGlobals())

it('goes back to the bookings list after deleting, even though the booking is now gone', async () => {
  let deleted = false
  vi.stubGlobal('confirm', () => true)
  const calls = mockApi({
    'GET /api/bookings/7': () => (deleted ? { status: 404 } : { status: 200, body: booking }),
    'DELETE /api/bookings/7': () => {
      deleted = true
      return { status: 204 }
    },
    'GET /api/crew': () => ({ status: 200, body: [] }),
    'GET /api/trucks': () => ({ status: 200, body: [] }),
    'GET /api/inventory': () => ({ status: 200, body: [] }),
    'GET /api/inventory/availability': () => ({ status: 200, body: [] }),
    'GET /api/settings': () => ({ status: 200, body: { companyName: '', defaultCityStateZip: '', phone: '', shopAddress: '', mapsConfigured: false } }),
  })
  renderRoutes(
    [
      { path: '/bookings/:id/edit', element: <BookingFormPage /> },
      { path: '/bookings', element: <p>Bookings list</p> },
    ],
    '/bookings/7/edit',
  )

  await userEvent.click(await screen.findByRole('button', { name: 'Delete' }))

  expect(await screen.findByText('Bookings list')).toBeInTheDocument()
  // Re-fetching the deleted booking is what made the page sometimes land on "Not found." instead of the list.
  const afterDelete = calls.slice(calls.findIndex((call) => call.method === 'DELETE') + 1)
  expect(afterDelete.filter((call) => call.path === '/api/bookings/7')).toEqual([])
})

it('moves the crew times when the party time changes', async () => {
  mockApi({
    'GET /api/crew': () => ({ status: 200, body: [] }),
    'GET /api/trucks': () => ({ status: 200, body: [{ id: 1, name: 'Truck 1', capacity: 6, isActive: true, crewMemberIds: [] }] }),
    'GET /api/inventory': () => ({ status: 200, body: [] }),
    'GET /api/inventory/availability': () => ({ status: 200, body: [] }),
    'GET /api/settings': () => ({ status: 200, body: { companyName: '', defaultCityStateZip: 'Monroe, NC 28110', phone: '', shopAddress: '', mapsConfigured: false } }),
  })
  renderRoutes([{ path: '/bookings/new', element: <BookingFormPage /> }], '/bookings/new')

  const partyStarts = await screen.findByLabelText('Party starts')
  const partyEnds = screen.getByLabelText('Party ends')
  await userEvent.clear(partyStarts)
  await userEvent.type(partyStarts, '2026-11-21T13:00')
  await userEvent.clear(partyEnds)
  await userEvent.type(partyEnds, '2026-11-21T17:30')

  const [setupArrives, pickupArrives] = screen.getAllByLabelText(/Crew arrives/)
  expect(setupArrives).toHaveValue('2026-11-21T12:00')
  expect(pickupArrives).toHaveValue('2026-11-21T17:30')
  expect(screen.getAllByRole('combobox', { name: 'Truck' })[0]).toHaveValue('Truck 1')
})
