import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import type { InventoryItem } from '../api/client'
import type { BookingItemForm } from '../lib/bookingItems'
import { mockApi, renderRoutes } from '../test/render'
import { BookingItemsEditor } from './BookingItemsEditor'

const item = (id: number, name: string, patch: Partial<InventoryItem> = {}): InventoryItem => ({
  id,
  name,
  category: 'Combo',
  isUnit: true,
  quantity: 1,
  waterUse: 'WetOrDry',
  needsPower: true,
  lengthFt: 30,
  widthFt: 14,
  heightFt: 16,
  dailyPrice: 250,
  notes: '',
  isActive: true,
  ...patch,
})

const onItems = vi.fn<(items: BookingItemForm[]) => void>()

function Harness() {
  const [items, setItems] = useState<BookingItemForm[]>([])
  const change = (next: BookingItemForm[]) => {
    setItems(next)
    onItems(next)
  }
  return <BookingItemsEditor items={items} onChange={change} setupAt="2026-11-14T09:00" pickupAt="2026-11-14T17:00" />
}

afterEach(() => vi.unstubAllGlobals())

it('marks fully booked items and asks wet or dry for combos', async () => {
  mockApi({
    'GET /api/inventory': () => ({
      status: 200,
      body: [item(1, 'Ocean Combo'), item(2, 'Pink Combo', { quantity: 3 }), item(3, 'Castle Obstacle', { category: 'ObstacleCourse', waterUse: 'DryOnly' })],
    }),
    'GET /api/inventory/availability': () => ({
      status: 200,
      body: [
        { inventoryItemId: 1, quantity: 1, booked: 1, available: 0 },
        { inventoryItemId: 2, quantity: 3, booked: 1, available: 2 },
        { inventoryItemId: 3, quantity: 1, booked: 0, available: 1 },
      ],
    }),
  })
  renderRoutes([{ path: '/', element: <Harness /> }], '/')

  const picker = await screen.findByLabelText('Add from inventory')
  expect(await within(picker).findByRole('option', { name: 'Ocean Combo (booked)' })).toBeDisabled()
  expect(within(picker).getByRole('option', { name: 'Pink Combo (2 of 3 free)' })).toBeEnabled()

  await userEvent.selectOptions(picker, 'Pink Combo (2 of 3 free)')
  await userEvent.click(screen.getByRole('button', { name: 'Add' }))
  await userEvent.click(within(screen.getByRole('group', { name: 'Pink Combo wet or dry' })).getByRole('button', { name: 'Wet' }))

  expect(onItems.mock.lastCall?.[0]).toMatchObject([{ inventoryItemId: 2, name: 'Pink Combo', isUnit: true, isWet: true }])
  expect(within(picker).getByRole('option', { name: 'Pink Combo (1 of 3 free)' })).toBeEnabled()
})
