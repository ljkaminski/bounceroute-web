import { useState } from 'react'
import { ApiError } from '../api/client'
import { useChangePassword } from '../api/hooks'
import { ErrorBox } from '../components/Feedback'

export function ChangePasswordPage() {
  const change = useChangePassword()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const mismatch = confirm !== '' && confirm !== newPassword
  const errors = change.error instanceof ApiError ? change.error.fieldErrors : {}

  return (
    <form
      className="card mx-auto max-w-md space-y-4 p-6"
      onSubmit={(event) => {
        event.preventDefault()
        if (mismatch) return
        change.mutate({ currentPassword, newPassword }, {
          onSuccess: () => {
            setCurrentPassword('')
            setNewPassword('')
            setConfirm('')
          },
        })
      }}
    >
      <div>
        <h1 className="text-xl font-bold">Change password</h1>
        <p className="text-sm text-slate-500">You'll stay signed in here. Other devices will be signed out.</p>
      </div>
      <label className="block">
        <span className="label">Current password</span>
        <input className="field" type="password" autoComplete="current-password" required
          value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
        {errors.CurrentPassword && <span className="mt-1 block text-sm text-red-700">{errors.CurrentPassword.join(' ')}</span>}
      </label>
      <label className="block">
        <span className="label">New password</span>
        <input className="field" type="password" autoComplete="new-password" required minLength={10}
          value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
        <span className="mt-1 block text-xs text-slate-500">At least 10 characters. A short phrase is easy to type on a phone.</span>
        {errors.Password && <span className="mt-1 block text-sm text-red-700">{errors.Password.join(' ')}</span>}
      </label>
      <label className="block">
        <span className="label">Type it again</span>
        <input className="field" type="password" autoComplete="new-password" required
          value={confirm} onChange={(event) => setConfirm(event.target.value)} />
        {mismatch && <span className="mt-1 block text-sm text-red-700">Doesn't match the new password.</span>}
      </label>
      {change.isError && Object.keys(errors).length === 0 && <ErrorBox error={change.error} />}
      {change.isSuccess && <p className="text-sm text-green-700">Password changed.</p>}
      <button className="btn-primary w-full" disabled={change.isPending || mismatch}>
        {change.isPending ? 'Saving…' : 'Change password'}
      </button>
    </form>
  )
}
