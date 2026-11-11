import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import type { JobCard } from '../api/client'
import { JobCardRow } from './JobCardRow'

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
  units: [{ id: 1, name: 'Castle Combo', isUnit: true, isLoaded: false, inventoryItemId: 4, isWet: true, photoUrl: null }],
  extras: [{ id: 2, name: 'Generator', isUnit: false, isLoaded: false, inventoryItemId: null, isWet: null, photoUrl: null }],
  crew: [{ id: 1, name: 'Jordan Hale', initials: 'JH' }],
  workMinutes: 30,
  partyStartsAt: null,
  partyEndsAt: null,
}

it('shows the stop the way crews read it', () => {
  render(<JobCardRow job={job} />, { wrapper: MemoryRouter })

  expect(screen.getByText('9:30')).toBeInTheDocument()
  expect(screen.getByText('AM')).toBeInTheDocument()
  expect(screen.getByText('NEXT UP')).toBeInTheDocument()
  expect(screen.getByText(/Grass · 1 unit \+ 1 extra · Truck 2/)).toBeInTheDocument()
  expect(screen.getByText(/· Wet/)).toBeInTheDocument()
  expect(screen.getByRole('link')).toHaveAttribute('href', '/jobs/7')
})
