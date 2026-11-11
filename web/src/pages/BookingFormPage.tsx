import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError, type Booking, type CrewMember, type SaveBookingRequest, type Surface } from '../api/client'
import { useBooking, useCrew, useDeleteBooking, useSaveBooking } from '../api/hooks'
import { BookingItemsEditor } from '../components/BookingItemsEditor'
import { ErrorBox, Loading } from '../components/Feedback'
import { newBookingItem, type BookingItemForm } from '../lib/bookingItems'
import { addDays, fromDateTimeInput, toDateKey, toDateTimeInput, today } from '../lib/dates'

const surfaces: Surface[] = ['Grass', 'Concrete', 'Gravel', 'Inside']

type ScheduleForm = { scheduledAt: string; truck: string; crewMemberIds: number[] }
type BookingForm = {
  customerName: string
  phone: string
  street: string
  cityStateZip: string
  surface: Surface
  siteNotes: string
  setup: ScheduleForm
  pickup: ScheduleForm
  sameCrew: boolean
  items: BookingItemForm[]
}


function blankForm(): BookingForm {
  const tomorrow = addDays(today(), 1)
  return {
    customerName: '',
    phone: '',
    street: '',
    cityStateZip: 'Greenville, NC 27858',
    surface: 'Grass',
    siteNotes: '',
    setup: { scheduledAt: `${tomorrow}T09:00`, truck: 'Truck 1', crewMemberIds: [] },
    pickup: { scheduledAt: `${tomorrow}T17:00`, truck: 'Truck 1', crewMemberIds: [] },
    sameCrew: true,
    items: [],
  }
}

function formFrom(booking: Booking): BookingForm {
  const schedule = (job: Booking['setup']): ScheduleForm => ({
    scheduledAt: toDateTimeInput(job.scheduledAt),
    truck: job.truck,
    crewMemberIds: [...job.crewMemberIds],
  })
  return {
    customerName: booking.customerName,
    phone: booking.phone,
    street: booking.street,
    cityStateZip: booking.cityStateZip,
    surface: booking.surface,
    siteNotes: booking.siteNotes,
    setup: schedule(booking.setup),
    pickup: schedule(booking.pickup),
    sameCrew:
      booking.setup.truck === booking.pickup.truck &&
      booking.setup.crewMemberIds.join() === booking.pickup.crewMemberIds.join(),
    items: booking.items.map((item) =>
      newBookingItem({ inventoryItemId: item.inventoryItemId, name: item.name, isUnit: item.isUnit, isWet: item.isWet }),
    ),
  }
}

function toRequest(form: BookingForm): SaveBookingRequest {
  const schedule = (job: ScheduleForm) => ({
    scheduledAt: fromDateTimeInput(job.scheduledAt),
    truck: job.truck,
    crewMemberIds: job.crewMemberIds,
  })
  const pickup = form.sameCrew ? { ...form.setup, scheduledAt: form.pickup.scheduledAt } : form.pickup
  return {
    customerName: form.customerName,
    phone: form.phone,
    street: form.street,
    cityStateZip: form.cityStateZip,
    surface: form.surface,
    siteNotes: form.siteNotes,
    setup: schedule(form.setup),
    pickup: schedule(pickup),
    items: form.items.map((item) =>
      item.inventoryItemId === null
        ? { name: item.name, isUnit: item.isUnit }
        : { name: null, isUnit: item.isUnit, inventoryItemId: item.inventoryItemId, isWet: item.isWet },
    ),
  }
}

export function BookingFormPage() {
  const { id } = useParams()
  const bookingId = id === undefined ? undefined : Number(id)
  const booking = useBooking(bookingId)

  if (bookingId === undefined) return <BookingEditor />
  if (booking.isPending) return <Loading what="Loading booking" />
  if (booking.isError) return <ErrorBox error={booking.error} />
  return <BookingEditor booking={booking.data} />
}

function BookingEditor({ booking }: { booking?: Booking }) {
  const navigate = useNavigate()
  const crew = useCrew()
  const save = useSaveBooking(booking?.id)
  const remove = useDeleteBooking()
  const [form, setForm] = useState<BookingForm>(() => (booking ? formFrom(booking) : blankForm()))

  const set = <K extends keyof BookingForm>(key: K, value: BookingForm[K]) => setForm((f) => ({ ...f, [key]: value }))
  const setSchedule = (which: 'setup' | 'pickup', patch: Partial<ScheduleForm>) =>
    setForm((f) => ({ ...f, [which]: { ...f[which], ...patch } }))

  // The API names fields like "CustomerName" or "Pickup.ScheduledAt".
  const fieldErrors = save.error instanceof ApiError ? save.error.fieldErrors : {}
  const errorFor = (...names: string[]) =>
    Object.entries(fieldErrors)
      .filter(([field]) => names.some((name) => field.toLowerCase() === name.toLowerCase()))
      .flatMap(([, messages]) => messages)
      .join(' ')

  const submit = (event: React.FormEvent) => {
    event.preventDefault()
    save.mutate(toRequest(form), {
      onSuccess: (saved) => navigate(`/?date=${toDateKey(new Date(saved.setup.scheduledAt))}`),
    })
  }

  const deleteBooking = () => {
    if (booking && confirm(`Delete job #${booking.jobNumber} for ${booking.customerName}?`))
      remove.mutate(booking.id, { onSuccess: () => navigate('/bookings') })
  }

  return (
    <form onSubmit={submit} className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <Link to="/bookings" className="inline-block text-sm font-semibold text-slate-500 hover:text-slate-800">
            ‹ Bookings
          </Link>
          <h1 className="text-2xl font-bold">{booking ? `Edit job #${booking.jobNumber}` : 'New booking'}</h1>
        </div>
        <div className="flex gap-2">
          {booking && (
            <button type="button" className="btn-danger" onClick={deleteBooking} disabled={remove.isPending}>
              Delete
            </button>
          )}
          <button className="btn-primary" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save booking'}
          </button>
        </div>
      </div>

      {save.isError && Object.keys(fieldErrors).length === 0 && <ErrorBox error={save.error} />}

      <section className="card grid gap-4 p-5 sm:grid-cols-2">
        <h2 className="font-bold sm:col-span-2">Customer</h2>
        <Field label="Name" error={errorFor('CustomerName')}>
          <input className="field" required maxLength={100} value={form.customerName} onChange={(e) => set('customerName', e.target.value)} />
        </Field>
        <Field label="Phone" error={errorFor('Phone')}>
          <input className="field" type="tel" maxLength={20} value={form.phone} onChange={(e) => set('phone', e.target.value)} />
        </Field>
        <Field label="Street" error={errorFor('Street')}>
          <input className="field" required maxLength={150} value={form.street} onChange={(e) => set('street', e.target.value)} />
        </Field>
        <Field label="City, state, zip" error={errorFor('CityStateZip')}>
          <input className="field" maxLength={100} value={form.cityStateZip} onChange={(e) => set('cityStateZip', e.target.value)} />
        </Field>
        <Field label="Ground">
          <select className="field" value={form.surface} onChange={(e) => set('surface', e.target.value as Surface)}>
            {surfaces.map((surface) => (
              <option key={surface}>{surface}</option>
            ))}
          </select>
        </Field>
        <Field label="Site notes" error={errorFor('SiteNotes')}>
          <textarea className="field" rows={2} maxLength={500} value={form.siteNotes} onChange={(e) => set('siteNotes', e.target.value)}
            placeholder="Gate codes, dogs, slope, where to park…" />
        </Field>
      </section>

      <div className="grid gap-5 md:grid-cols-2">
        <ScheduleSection title="Setup" schedule={form.setup} crew={crew.data ?? []}
          onChange={(patch) => setSchedule('setup', patch)} error={errorFor('Setup.ScheduledAt', 'CrewMemberIds')} />
        <ScheduleSection title="Pickup" schedule={form.sameCrew ? { ...form.setup, scheduledAt: form.pickup.scheduledAt } : form.pickup}
          crew={crew.data ?? []} timeOnly={form.sameCrew}
          onChange={(patch) => setSchedule('pickup', patch)} error={errorFor('Pickup.ScheduledAt')}>
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" className="accent-brand" checked={form.sameCrew}
              onChange={(e) => setForm((f) => ({ ...f, sameCrew: e.target.checked, pickup: { ...f.setup, scheduledAt: f.pickup.scheduledAt } }))} />
            Same truck and crew as setup
          </label>
        </ScheduleSection>
      </div>

      <BookingItemsEditor
        items={form.items}
        onChange={(items) => set('items', items)}
        setupAt={form.setup.scheduledAt}
        pickupAt={form.pickup.scheduledAt}
        bookingId={booking?.id}
        error={errorFor('Items')}
      />
    </form>
  )
}

function ScheduleSection(props: {
  title: string
  schedule: ScheduleForm
  crew: CrewMember[]
  onChange: (patch: Partial<ScheduleForm>) => void
  error?: string
  timeOnly?: boolean
  children?: React.ReactNode
}) {
  const { title, schedule, crew, onChange, error, timeOnly, children } = props
  const toggle = (id: number) =>
    onChange({
      crewMemberIds: schedule.crewMemberIds.includes(id)
        ? schedule.crewMemberIds.filter((other) => other !== id)
        : [...schedule.crewMemberIds, id],
    })

  return (
    <section className="card space-y-4 p-5">
      <h2 className="font-bold">{title}</h2>
      {children}
      <Field label="When" error={error}>
        <input type="datetime-local" className="field" required value={schedule.scheduledAt}
          onChange={(e) => onChange({ scheduledAt: e.target.value })} />
      </Field>
      <fieldset disabled={timeOnly} className="space-y-4 disabled:opacity-60">
        <Field label="Truck">
          <input className="field" maxLength={30} value={schedule.truck} onChange={(e) => onChange({ truck: e.target.value })} />
        </Field>
        <div>
          <span className="label">Crew</span>
          <div className="flex flex-wrap gap-2">
            {crew.map((member) => {
              const on = schedule.crewMemberIds.includes(member.id)
              return (
                <button key={member.id} type="button" aria-pressed={on} onClick={() => toggle(member.id)}
                  className={`rounded-full px-3 py-1 text-sm font-medium ring-1 ${on ? 'bg-navy text-white ring-navy' : 'bg-white text-slate-600 ring-slate-300'}`}>
                  {member.name}
                </button>
              )
            })}
          </div>
        </div>
      </fieldset>
    </section>
  )
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="label">{label}</span>
      {children}
      {error && <span className="mt-1 block text-sm text-red-700">{error}</span>}
    </label>
  )
}
