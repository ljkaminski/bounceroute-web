import { useState } from 'react'
import type { InventoryItem } from '../api/client'
import { useAvailability, useInventory } from '../api/hooks'
import { fromDateTimeInput } from '../lib/dates'
import { newBookingItem, type BookingItemForm } from '../lib/bookingItems'
import { categoryLabels, groupByCategory, sizeText } from '../lib/inventory'
import { UnitPhoto } from './UnitPhoto'

const toIso = (value: string) => {
  try {
    return value ? fromDateTimeInput(value) : null
  } catch {
    return null
  }
}

export function BookingItemsEditor(props: {
  items: BookingItemForm[]
  onChange: (items: BookingItemForm[]) => void
  setupAt: string
  pickupAt: string
  bookingId?: number
  error?: string
}) {
  const { items, onChange, setupAt, pickupAt, bookingId, error } = props
  const inventory = useInventory(true)
  const availability = useAvailability(toIso(setupAt), toIso(pickupAt), bookingId)
  const [picked, setPicked] = useState('')
  const [oneOff, setOneOff] = useState({ name: '', isUnit: false })

  const byId = new Map((inventory.data ?? []).map((item) => [item.id, item]))
  const onThisBooking = (id: number) => items.filter((item) => item.inventoryItemId === id).length
  // Free for these dates, minus what's already on this form.
  const free = (id: number) => {
    const entry = availability.data?.find((a) => a.inventoryItemId === id)
    return entry ? entry.available - onThisBooking(id) : undefined
  }

  const add = (item: InventoryItem) =>
    onChange([
      ...items,
      newBookingItem({
        inventoryItemId: item.id,
        name: item.name,
        isUnit: item.isUnit,
        isWet: item.waterUse === 'WetOnly' ? true : item.waterUse === 'DryOnly' ? false : null,
      }),
    ])
  const update = (key: number, patch: Partial<BookingItemForm>) =>
    onChange(items.map((item) => (item.key === key ? { ...item, ...patch } : item)))
  const remove = (key: number) => onChange(items.filter((item) => item.key !== key))

  const activeGroups = groupByCategory((inventory.data ?? []).filter((item) => item.isActive))

  return (
    <section className="card space-y-4 p-5">
      <div>
        <h2 className="font-bold">Items</h2>
        <p className="text-xs text-slate-500">Units count toward crew pay; extras don't. Free counts are for the setup-to-pickup window above.</p>
      </div>

      {items.length > 0 && (
        <ul className="divide-y divide-slate-100 rounded-xl ring-1 ring-slate-200">
          {items.map((line) => {
            const owned = line.inventoryItemId === null ? undefined : byId.get(line.inventoryItemId)
            const overbooked = owned && (free(owned.id) ?? 0) < 0
            return (
              <li key={line.key} className="flex flex-wrap items-center gap-3 px-3 py-2">
                {owned && <UnitPhoto url={owned.photoUrl} name={line.name} size="sm" />}
                <div className="min-w-40 flex-1">
                  <div className="font-medium">{line.name}</div>
                  <div className="text-xs text-slate-500">
                    {[owned ? sizeText(owned) : 'One-off item', line.isUnit ? 'Unit' : 'Extra'].filter(Boolean).join(' · ')}
                  </div>
                  {overbooked && <div className="text-xs font-semibold text-red-700">Not enough free for these dates</div>}
                </div>
                {owned?.waterUse === 'WetOrDry' && (
                  <div className="flex rounded-lg bg-slate-100 p-0.5 text-sm" role="group" aria-label={`${line.name} wet or dry`}>
                    {([['Dry', false], ['Wet', true]] as const).map(([label, wet]) => (
                      <button key={label} type="button" aria-pressed={line.isWet === wet} onClick={() => update(line.key, { isWet: wet })}
                        className={`rounded-md px-3 py-1 font-medium ${line.isWet === wet ? (wet ? 'bg-sky-600 text-white' : 'bg-navy text-white') : 'text-slate-600'}`}>
                        {label}
                      </button>
                    ))}
                  </div>
                )}
                {owned?.waterUse === 'WetOnly' && <span className="text-xs font-semibold text-sky-700">Wet</span>}
                {owned?.waterUse === 'DryOnly' && <span className="text-xs font-semibold text-slate-500">Dry</span>}
                <button type="button" className="btn-secondary px-3" aria-label={`Remove ${line.name}`} onClick={() => remove(line.key)}>
                  ✕
                </button>
              </li>
            )
          })}
        </ul>
      )}

      <div className="flex flex-wrap items-end gap-2">
        <label className="min-w-64 flex-1">
          <span className="label">Add from inventory</span>
          <select className="field" value={picked} onChange={(event) => setPicked(event.target.value)}>
            <option value="">Choose an item…</option>
            {activeGroups.map((group) => (
              <optgroup key={group.category} label={categoryLabels[group.category]}>
                {group.items.map((item) => {
                  const left = free(item.id)
                  return (
                    <option key={item.id} value={item.id} disabled={left !== undefined && left <= 0}>
                      {item.name}
                      {left === undefined ? '' : left <= 0 ? ' (booked)' : item.quantity > 1 ? ` (${left} of ${item.quantity} free)` : ''}
                    </option>
                  )
                })}
              </optgroup>
            ))}
          </select>
        </label>
        <button type="button" className="btn-secondary" disabled={!picked}
          onClick={() => {
            const item = byId.get(Number(picked))
            if (item) add(item)
            setPicked('')
          }}>
          Add
        </button>
      </div>

      <details className="text-sm">
        <summary className="cursor-pointer text-slate-500">Add something that isn't in inventory</summary>
        <div className="mt-2 flex flex-wrap items-end gap-2">
          <input className="field min-w-48 flex-1" placeholder="e.g. Tables (4)" maxLength={80} value={oneOff.name}
            onChange={(event) => setOneOff({ ...oneOff, name: event.target.value })} aria-label="One-off item name" />
          <select className="field w-28" value={oneOff.isUnit ? 'unit' : 'extra'} aria-label="One-off item kind"
            onChange={(event) => setOneOff({ ...oneOff, isUnit: event.target.value === 'unit' })}>
            <option value="extra">Extra</option>
            <option value="unit">Unit</option>
          </select>
          <button type="button" className="btn-secondary" disabled={!oneOff.name.trim()}
            onClick={() => {
              onChange([...items, newBookingItem({ name: oneOff.name.trim(), isUnit: oneOff.isUnit })])
              setOneOff({ name: '', isUnit: false })
            }}>
            Add one-off
          </button>
        </div>
      </details>

      {error && <p className="text-sm text-red-700">{error}</p>}
    </section>
  )
}
