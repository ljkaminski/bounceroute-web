// One line on the booking: an inventory item, or a one-off typed by hand.
export type BookingItemForm = {
  key: number
  inventoryItemId: number | null
  name: string
  isUnit: boolean
  isWet: boolean | null
}

let nextKey = 1
export const newBookingItem = (patch: Partial<BookingItemForm> = {}): BookingItemForm => ({
  key: nextKey++,
  inventoryItemId: null,
  name: '',
  isUnit: false,
  isWet: null,
  ...patch,
})
