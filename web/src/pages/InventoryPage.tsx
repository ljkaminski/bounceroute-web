import { useState } from 'react'
import { ApiError, type InventoryCategory, type InventoryItem, type SaveInventoryItemRequest, type WaterUse } from '../api/client'
import {
  useImportInventory,
  useInventory,
  useRemoveInventoryPhoto,
  useSaveInventoryItem,
  useSetupTimes,
  useUploadInventoryPhoto,
} from '../api/hooks'
import { EmptyState, ErrorBox, Loading } from '../components/Feedback'
import { UnitPhoto } from '../components/UnitPhoto'
import { categoryLabels, categoryOrder, groupByCategory, price, sizeText, waterUseLabels } from '../lib/inventory'

export function InventoryPage() {
  const [showRetired, setShowRetired] = useState(false)
  const [adding, setAdding] = useState(false)
  const inventory = useInventory(showRetired)

  const totalUnits = inventory.data?.filter((item) => item.isActive).reduce((sum, item) => sum + item.quantity, 0) ?? 0

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Inventory</h1>
          <p className="text-sm text-slate-500">
            What you own. Bookings pick from this list, and the app stops double-booking.
            {inventory.data && ` ${inventory.data.length} items, ${totalUnits} in all.`}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" className="accent-brand" checked={showRetired} onChange={(event) => setShowRetired(event.target.checked)} />
            Show retired
          </label>
          <ImportButton />
          <button className="btn-primary" onClick={() => setAdding(true)} disabled={adding}>
            Add item
          </button>
        </div>
      </div>

      {adding && <ItemForm onDone={() => setAdding(false)} />}

      {inventory.isPending ? (
        <Loading what="Loading inventory" />
      ) : inventory.isError ? (
        <ErrorBox error={inventory.error} />
      ) : inventory.data.length === 0 ? (
        <EmptyState>No inventory yet. Add items one at a time or import a file.</EmptyState>
      ) : (
        groupByCategory(inventory.data).map((group) => (
          <section key={group.category} className="space-y-2">
            <h2 className="label">{categoryLabels[group.category]}</h2>
            <ul className="card divide-y divide-slate-100">
              {group.items.map((item) => (
                <ItemRow key={item.id} item={item} />
              ))}
            </ul>
          </section>
        ))
      )}
    </div>
  )
}

function ItemRow({ item }: { item: InventoryItem }) {
  const [editing, setEditing] = useState(false)

  return (
    <li className={`p-4 ${item.isActive ? '' : 'opacity-60'}`}>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
        <UnitPhoto url={item.photoUrl} name={item.name} />
        <div className="min-w-48 flex-1">
          <div className="font-semibold">
            {item.name}
            {item.quantity > 1 && <span className="ml-2 rounded-md bg-navy px-1.5 py-0.5 text-xs text-white">×{item.quantity}</span>}
            {!item.isActive && <span className="ml-2 text-xs font-normal text-slate-500">Retired</span>}
          </div>
          <div className="text-xs text-slate-500">
            {[sizeText(item), item.waterUse !== 'None' && waterUseLabels[item.waterUse], item.needsPower && 'Needs power', !item.isUnit && "Extra (doesn't count toward pay)"]
              .filter(Boolean)
              .join(' · ')}
          </div>
          {item.notes && <div className="text-xs text-slate-500">{item.notes}</div>}
        </div>
        <div className="font-semibold text-slate-700">{price(item.dailyPrice)}</div>
        <button className="btn-secondary" onClick={() => setEditing(!editing)}>
          {editing ? 'Cancel' : 'Edit'}
        </button>
      </div>
      {editing && (
        <>
          <PhotoField item={item} />
          <ItemForm item={item} onDone={() => setEditing(false)} />
        </>
      )}
    </li>
  )
}

function PhotoField({ item }: { item: InventoryItem }) {
  const upload = useUploadInventoryPhoto()
  const remove = useRemoveInventoryPhoto()

  return (
    <div className="mt-3 flex flex-wrap items-center gap-4 rounded-xl bg-slate-50 p-4">
      <UnitPhoto url={item.photoUrl} name={item.name} size="lg" />
      <div className="space-y-2">
        <div className="flex flex-wrap gap-2">
          <label className="btn-secondary cursor-pointer">
            {upload.isPending ? 'Uploading…' : item.photoUrl ? 'Replace photo' : 'Add photo'}
            {/* On phones this offers the camera as well as the photo library. */}
            <input type="file" accept="image/*" className="sr-only" disabled={upload.isPending}
              onChange={(event) => {
                const file = event.target.files?.[0]
                if (file) upload.mutate({ itemId: item.id, file })
                event.target.value = ''
              }} />
          </label>
          {item.photoUrl && (
            <button type="button" className="btn-danger" disabled={remove.isPending} onClick={() => remove.mutate(item.id)}>
              Remove photo
            </button>
          )}
        </div>
        <p className="text-xs text-slate-500">Photos are shrunk before upload, so big phone photos are fine.</p>
        {upload.isError && <ErrorBox error={upload.error} />}
      </div>
    </div>
  )
}

const blankItem: SaveInventoryItemRequest = {
  name: '',
  category: 'Combo',
  isUnit: true,
  quantity: 1,
  waterUse: 'WetOrDry',
  needsPower: true,
  lengthFt: null,
  widthFt: null,
  heightFt: null,
  dailyPrice: null,
  notes: '',
  isActive: true,
}

function ItemForm({ item, onDone }: { item?: InventoryItem; onDone: () => void }) {
  const save = useSaveInventoryItem(item?.id)
  const setupTimes = useSetupTimes()
  const [form, setForm] = useState<SaveInventoryItemRequest>(() => (item ? { ...item } : blankItem))
  const set = <K extends keyof SaveInventoryItemRequest>(key: K, value: SaveInventoryItemRequest[K]) => setForm((f) => ({ ...f, [key]: value }))
  const number = (value: string) => (value === '' ? null : Number(value))
  const nameError = save.error instanceof ApiError ? save.error.fieldErrors.Name?.join(' ') : undefined
  const typeMinutes = setupTimes.data?.find((time) => time.category === form.category)?.minutes

  return (
    <form
      className="mt-3 grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-4"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate(form, { onSuccess: onDone })
      }}
    >
      <label className="sm:col-span-2">
        <span className="label">Name</span>
        <input className="field" required maxLength={80} value={form.name} onChange={(e) => set('name', e.target.value)} />
        {nameError && <span className="mt-1 block text-sm text-red-700">{nameError}</span>}
      </label>
      <label>
        <span className="label">Category</span>
        <select className="field" value={form.category} onChange={(e) => set('category', e.target.value as InventoryCategory)}>
          {categoryOrder.map((category) => (
            <option key={category} value={category}>
              {categoryLabels[category]}
            </option>
          ))}
        </select>
      </label>
      <label>
        <span className="label">How many you own</span>
        <input className="field" type="number" min={1} max={100} required value={form.quantity}
          onChange={(e) => set('quantity', Number(e.target.value))} />
      </label>
      <label>
        <span className="label">Water</span>
        <select className="field" value={form.waterUse} onChange={(e) => set('waterUse', e.target.value as WaterUse)}>
          {(Object.keys(waterUseLabels) as WaterUse[]).map((use) => (
            <option key={use} value={use}>
              {waterUseLabels[use]}
            </option>
          ))}
        </select>
      </label>
      <label>
        <span className="label">Daily price ($)</span>
        <input className="field" type="number" min={0} step="0.01" value={form.dailyPrice ?? ''}
          onChange={(e) => set('dailyPrice', number(e.target.value))} />
      </label>
      <fieldset className="sm:col-span-2">
        <span className="label">Size in feet (L × W × H)</span>
        <div className="flex gap-2">
          {(['lengthFt', 'widthFt', 'heightFt'] as const).map((key) => (
            <input key={key} className="field" type="number" min={1} aria-label={key.replace('Ft', '')} value={form[key] ?? ''}
              onChange={(e) => set(key, number(e.target.value))} />
          ))}
        </div>
      </fieldset>
      <label>
        <span className="label">Truck space (pieces)</span>
        <input className="field" type="number" min={0} max={10} value={form.truckSpace ?? ''}
          placeholder={form.category === 'Equipment' ? '0' : '1'} onChange={(e) => set('truckSpace', number(e.target.value))} />
      </label>
      <label>
        <span className="label">Setup minutes</span>
        <input className="field" type="number" min={0} max={600} value={form.setupMinutes ?? ''}
          placeholder={typeMinutes === undefined ? 'Type default' : `${typeMinutes} (type default)`}
          onChange={(e) => set('setupMinutes', number(e.target.value))} />
      </label>
      <label>
        <span className="label">Notes</span>
        <input className="field" maxLength={500} value={form.notes ?? ''} onChange={(e) => set('notes', e.target.value)} />
      </label>
      <div className="flex flex-wrap items-end gap-4 text-sm sm:col-span-2">
        <label className="flex items-center gap-2">
          <input type="checkbox" className="accent-brand" checked={form.isUnit} onChange={(e) => set('isUnit', e.target.checked)} />
          Counts toward crew pay
        </label>
        <label className="flex items-center gap-2">
          <input type="checkbox" className="accent-brand" checked={form.needsPower} onChange={(e) => set('needsPower', e.target.checked)} />
          Needs power
        </label>
        {item && (
          <label className="flex items-center gap-2">
            <input type="checkbox" className="accent-brand" checked={!form.isActive} onChange={(e) => set('isActive', !e.target.checked)} />
            Retired
          </label>
        )}
      </div>
      <div className="flex gap-2 sm:col-span-4">
        <button className="btn-primary" disabled={save.isPending}>
          {item ? 'Save' : 'Add item'}
        </button>
        <button type="button" className="btn-secondary" onClick={onDone}>
          Cancel
        </button>
      </div>
      {save.isError && !nameError && <div className="sm:col-span-4"><ErrorBox error={save.error} /></div>}
    </form>
  )
}

// Loads a JSON array of items (the same shape the API takes) and adds or updates them by name.
function ImportButton() {
  const importItems = useImportInventory()
  const [message, setMessage] = useState<string | null>(null)

  const onFile = async (file: File) => {
    setMessage(null)
    let items: SaveInventoryItemRequest[]
    try {
      items = JSON.parse(await file.text())
      if (!Array.isArray(items)) throw new Error()
    } catch {
      setMessage("That file isn't a JSON list of items.")
      return
    }
    importItems.mutate(items, {
      onSuccess: (result) => setMessage(`Imported: ${result.created} added, ${result.updated} updated.`),
      onError: (error) => setMessage(error.message),
    })
  }

  return (
    <span className="flex items-center gap-2">
      {message && <span className="text-sm text-slate-600">{message}</span>}
      <label className="btn-secondary cursor-pointer">
        {importItems.isPending ? 'Importing…' : 'Import from file'}
        <input type="file" accept=".json,application/json" className="sr-only"
          onChange={(event) => {
            const file = event.target.files?.[0]
            if (file) void onFile(file)
            event.target.value = ''
          }} />
      </label>
    </span>
  )
}
