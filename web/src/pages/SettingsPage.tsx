import { useState } from 'react'
import type { Schemas } from '../api/client'
import { useSaveSettings, useSaveSetupTimes, useSettings, useSetupTimes } from '../api/hooks'
import { categoryLabels } from '../lib/inventory'
import { ErrorBox, Loading } from '../components/Feedback'
import { TrucksSettings } from '../components/TrucksSettings'

export function SettingsPage() {
  const settings = useSettings()

  const times = useSetupTimes()

  if (settings.isPending || times.isPending) return <Loading what="Loading settings" />
  if (settings.isError) return <ErrorBox error={settings.error} />
  if (times.isError) return <ErrorBox error={times.error} />
  return (
    <div className="space-y-5">
      <SettingsForm initial={settings.data} />
      <TrucksSettings />
      <SetupTimesForm initial={times.data} />
    </div>
  )
}

function SettingsForm({ initial }: { initial: Schemas['CompanySettingsDto'] }) {
  const save = useSaveSettings()
  const [form, setForm] = useState({
    companyName: initial.companyName,
    defaultCityStateZip: initial.defaultCityStateZip,
    phone: initial.phone,
    shopAddress: initial.shopAddress,
    suggestRoutes: initial.suggestRoutes,
  })
  const set = (key: Exclude<keyof typeof form, 'suggestRoutes'>, value: string) => setForm((f) => ({ ...f, [key]: value }))

  return (
    <form
      className="card mx-auto max-w-lg space-y-4 p-6"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate(form)
      }}
    >
      <div>
        <h1 className="text-xl font-bold">Company settings</h1>
        <p className="text-sm text-slate-500">Shown in the app header and used as defaults on new bookings.</p>
      </div>
      <label className="block">
        <span className="label">Company name</span>
        <input className="field" maxLength={100} value={form.companyName} onChange={(e) => set('companyName', e.target.value)} />
      </label>
      <label className="block">
        <span className="label">Default city, state, zip for new bookings</span>
        <input className="field" maxLength={100} value={form.defaultCityStateZip} onChange={(e) => set('defaultCityStateZip', e.target.value)} />
      </label>
      <label className="block">
        <span className="label">Shop address (where trucks start and end the day)</span>
        <input className="field" maxLength={200} placeholder="Street, city, state zip" value={form.shopAddress}
          onChange={(e) => set('shopAddress', e.target.value)} />
        <span className="mt-1 block text-xs text-slate-500">
          {initial.mapsConfigured
            ? 'Used to work out drive times to the first stop and back.'
            : "Drive times are off until a map service key is set up, so route plans count setup time only."}
        </span>
      </label>
      <label className="flex items-start gap-3 rounded-xl bg-slate-50 p-3">
        <input type="checkbox" className="mt-1 size-4 accent-brand" checked={form.suggestRoutes}
          onChange={(e) => setForm((f) => ({ ...f, suggestRoutes: e.target.checked }))} />
        <span>
          <span className="block font-semibold">Suggest better routes</span>
          <span className="block text-sm text-slate-500">
            {form.suggestRoutes
              ? 'On: when there\'s a better way to run a day, the Route page shows it with a "Use this plan" button.'
              : 'Off: the office picks trucks and times for every booking. The Route page still shows timing and late warnings.'}
          </span>
        </span>
      </label>
      <label className="block">
        <span className="label">Phone</span>
        <input className="field" type="tel" maxLength={20} value={form.phone} onChange={(e) => set('phone', e.target.value)} />
      </label>
      {save.isError && <ErrorBox error={save.error} />}
      {save.isSuccess && <p className="text-sm text-green-700">Saved.</p>}
      <button className="btn-primary" disabled={save.isPending}>
        {save.isPending ? 'Saving…' : 'Save'}
      </button>
    </form>
  )
}

function SetupTimesForm({ initial }: { initial: Schemas['CategorySetupTimeDto'][] }) {
  const save = useSaveSetupTimes()
  const [times, setTimes] = useState(initial)

  return (
    <form
      className="card mx-auto max-w-lg space-y-4 p-6"
      onSubmit={(event) => {
        event.preventDefault()
        save.mutate(times)
      }}
    >
      <div>
        <h2 className="text-xl font-bold">Setup times</h2>
        <p className="text-sm text-slate-500">
          Minutes to set up one unit of each type. Pickup counts the same. Used to plan when each truck will be done.
          A single unit can be set differently on the Inventory page.
        </p>
      </div>
      <div className="grid grid-cols-2 gap-3">
        {times.map((time) => (
          <label key={time.category} className="block">
            <span className="label">{categoryLabels[time.category]}</span>
            <div className="flex items-center gap-2">
              <input className="field" type="number" min={0} max={600} required value={time.minutes}
                onChange={(e) => setTimes(times.map((t) => (t.category === time.category ? { ...t, minutes: Number(e.target.value) } : t)))} />
              <span className="text-sm text-slate-500">min</span>
            </div>
          </label>
        ))}
      </div>
      {save.isError && <ErrorBox error={save.error} />}
      {save.isSuccess && <p className="text-sm text-green-700">Saved.</p>}
      <button className="btn-primary" disabled={save.isPending}>
        {save.isPending ? 'Saving…' : 'Save setup times'}
      </button>
    </form>
  )
}
