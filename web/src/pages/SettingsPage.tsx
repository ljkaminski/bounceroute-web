import { useState } from 'react'
import type { Schemas } from '../api/client'
import { useSaveSettings, useSettings } from '../api/hooks'
import { ErrorBox, Loading } from '../components/Feedback'

export function SettingsPage() {
  const settings = useSettings()

  if (settings.isPending) return <Loading what="Loading settings" />
  if (settings.isError) return <ErrorBox error={settings.error} />
  return <SettingsForm initial={settings.data} />
}

function SettingsForm({ initial }: { initial: Schemas['CompanySettingsDto'] }) {
  const save = useSaveSettings()
  const [form, setForm] = useState(initial)
  const set = (key: keyof typeof form, value: string) => setForm((f) => ({ ...f, [key]: value }))

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
