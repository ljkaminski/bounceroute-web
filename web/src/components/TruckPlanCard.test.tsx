import { render, screen } from '@testing-library/react'
import type { PlannedStop, RoutePlan, TruckPlan } from '../api/client'
import { TruckPlanCard } from './TruckPlanCard'

const stop = (patch: Partial<PlannedStop>): PlannedStop => ({
  jobId: 1,
  type: 'Setup',
  customerName: 'Avery Thompson',
  street: '118 Oak Hollow Dr',
  scheduledAt: '2026-11-14T13:00:00Z',
  driveMinutesBefore: 15,
  arriveAt: '2026-11-14T13:00:00Z',
  startAt: '2026-11-14T13:00:00Z',
  finishAt: '2026-11-14T13:30:00Z',
  workMinutes: 30,
  minutesLate: 0,
  ...patch,
})

const truck: TruckPlan = {
  truck: 'Truck 1',
  leaveShopAt: '2026-11-14T12:45:00Z',
  doneAt: '2026-11-14T15:30:00Z',
  backAtShopAt: '2026-11-14T15:50:00Z',
  workMinutes: 75,
  driveMinutes: 50,
  driveTimesComplete: true,
  stops: [stop({}), stop({ jobId: 2, minutesLate: 25 })],
}

const route: RoutePlan = { date: '2026-11-14', mapsConfigured: true, shopAddressSet: true, trucks: [truck] }

it('shows when the truck leaves, finishes, and gets back, and warns about late stops', () => {
  render(<TruckPlanCard plan={truck} route={route} />)

  expect(screen.getByText('7:45 AM')).toBeInTheDocument()
  expect(screen.getByText('10:30 AM')).toBeInTheDocument()
  expect(screen.getByText('10:50 AM')).toBeInTheDocument()
  expect(screen.getByText('1 hr 15 min setting up · 50 min driving')).toBeInTheDocument()
  expect(screen.getByText(/1 stop running late \(up to 25 min\)/)).toBeInTheDocument()
})

it('says plainly when drive times are missing', () => {
  render(
    <TruckPlanCard
      plan={{ ...truck, leaveShopAt: null, backAtShopAt: null, driveMinutes: 0, driveTimesComplete: false }}
      route={{ ...route, mapsConfigured: false }}
    />,
  )

  expect(screen.getByText('Drive times not included yet.')).toBeInTheDocument()
  expect(screen.getAllByText('—')).toHaveLength(2)
})
