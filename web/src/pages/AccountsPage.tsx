import { useState } from 'react'
import { ApiError, type CrewMember, type User } from '../api/client'
import { useCreateUser, useCrew, useResetPassword, useUpdateUser, useUsers } from '../api/hooks'
import type { Role } from '../auth/roles'
import { useCurrentUser } from '../auth/useAuth'
import { ErrorBox, Loading } from '../components/Feedback'

const roles: { value: Role; label: string; help: string }[] = [
  { value: 'Crew', label: 'Crew', help: 'Their own jobs and units' },
  { value: 'Office', label: 'Office', help: 'Bookings, scheduling, crew, pay' },
  { value: 'Owner', label: 'Owner', help: 'Everything, including accounts' },
]

// The API names fields like "Email" or "CrewMemberId"; show each next to its input.
function fieldError(error: unknown, field: string) {
  if (!(error instanceof ApiError)) return undefined
  const entry = Object.entries(error.fieldErrors).find(([key]) => key.toLowerCase() === field.toLowerCase())
  return entry?.[1].join(' ')
}

export function AccountsPage() {
  const users = useUsers()
  const crew = useCrew()

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold">Accounts</h1>
        <p className="text-sm text-slate-500">Who can sign in. There is no public sign-up; add people here.</p>
      </div>

      {users.isPending ? (
        <Loading what="Loading accounts" />
      ) : users.isError ? (
        <ErrorBox error={users.error} />
      ) : (
        <ul className="card divide-y divide-slate-100">
          {users.data.map((user) => (
            <AccountRow key={user.id} user={user} crew={crew.data ?? []} />
          ))}
        </ul>
      )}

      <AddAccountForm crew={crew.data ?? []} />
    </div>
  )
}

function AccountRow({ user, crew }: { user: User; crew: CrewMember[] }) {
  const me = useCurrentUser().data
  const update = useUpdateUser()
  const reset = useResetPassword()
  const [editing, setEditing] = useState(false)
  const [displayName, setDisplayName] = useState(user.displayName)
  const [role, setRole] = useState<Role>(user.role)
  const [crewMemberId, setCrewMemberId] = useState<number | null>(user.crewMemberId)
  const isMe = user.id === me?.id

  const save = (changes: Partial<{ isDisabled: boolean }> = {}) =>
    update.mutate(
      { id: user.id, body: { displayName, role, crewMemberId, isDisabled: changes.isDisabled ?? user.isDisabled } },
      { onSuccess: () => setEditing(false) },
    )

  const resetPassword = () => {
    const newPassword = prompt(`New password for ${user.displayName} (at least 10 characters):`)
    if (newPassword) reset.mutate({ id: user.id, newPassword })
  }

  return (
    <li className="p-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="min-w-0 flex-1">
          <div className="font-semibold">
            {user.displayName} {isMe && <span className="text-xs font-normal text-slate-400">(you)</span>}
          </div>
          <div className="truncate text-sm text-slate-500">{user.email}</div>
        </div>
        <span className="rounded-md bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-700">{user.role}</span>
        {user.crewMemberName && <span className="text-xs text-slate-500">Crew: {user.crewMemberName}</span>}
        {user.isDisabled && <span className="rounded-md bg-red-50 px-2 py-0.5 text-xs font-semibold text-red-700">Turned off</span>}
        <button className="btn-secondary" onClick={() => setEditing(!editing)}>
          {editing ? 'Cancel' : 'Edit'}
        </button>
      </div>

      {editing && (
        <div className="mt-4 grid gap-3 rounded-xl bg-slate-50 p-4 sm:grid-cols-3">
          <label>
            <span className="label">Name</span>
            <input className="field" value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
          </label>
          <RoleSelect value={role} onChange={setRole} disabled={isMe} />
          <CrewSelect crew={crew} value={crewMemberId} onChange={setCrewMemberId} required={role === 'Crew'}
            error={fieldError(update.error, 'CrewMemberId')} />
          <div className="flex flex-wrap gap-2 sm:col-span-3">
            <button className="btn-primary" onClick={() => save()} disabled={update.isPending}>
              Save
            </button>
            <button className="btn-secondary" onClick={resetPassword} disabled={reset.isPending}>
              Reset password
            </button>
            {!isMe && (
              <button className={user.isDisabled ? 'btn-secondary' : 'btn-danger'} disabled={update.isPending}
                onClick={() => save({ isDisabled: !user.isDisabled })}>
                {user.isDisabled ? 'Turn back on' : 'Turn off'}
              </button>
            )}
          </div>
          {update.isError && !fieldError(update.error, 'CrewMemberId') && (
            <div className="sm:col-span-3"><ErrorBox error={update.error} /></div>
          )}
          {reset.isError && <div className="sm:col-span-3"><ErrorBox error={reset.error} /></div>}
          {reset.isSuccess && <p className="text-sm text-green-700 sm:col-span-3">Password changed. Let them know the new one.</p>}
        </div>
      )}
    </li>
  )
}

function AddAccountForm({ crew }: { crew: CrewMember[] }) {
  const create = useCreateUser()
  const blank = { email: '', displayName: '', role: 'Crew' as Role, crewMemberId: null as number | null, password: '' }
  const [form, setForm] = useState(blank)
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((f) => ({ ...f, [key]: value }))

  // Picking a crew member fills in their name, since that's almost always what you want.
  const chooseCrew = (id: number | null) =>
    setForm((f) => ({ ...f, crewMemberId: id, displayName: f.displayName || crew.find((c) => c.id === id)?.name || '' }))

  return (
    <form
      className="card grid gap-3 p-5 sm:grid-cols-2"
      onSubmit={(event) => {
        event.preventDefault()
        create.mutate(form, { onSuccess: () => setForm(blank) })
      }}
    >
      <h2 className="font-bold sm:col-span-2">Add an account</h2>
      <RoleSelect value={form.role} onChange={(role) => set('role', role)} />
      <CrewSelect crew={crew} value={form.crewMemberId} onChange={chooseCrew} required={form.role === 'Crew'}
        error={fieldError(create.error, 'CrewMemberId')} />
      <label>
        <span className="label">Name</span>
        <input className="field" required maxLength={80} value={form.displayName} onChange={(event) => set('displayName', event.target.value)} />
      </label>
      <label>
        <span className="label">Email</span>
        <input className="field" type="email" required value={form.email} onChange={(event) => set('email', event.target.value)} />
        {fieldError(create.error, 'Email') && <span className="mt-1 block text-sm text-red-700">{fieldError(create.error, 'Email')}</span>}
      </label>
      <label className="sm:col-span-2">
        <span className="label">Starting password</span>
        <input className="field" type="text" autoComplete="off" required minLength={10} value={form.password}
          onChange={(event) => set('password', event.target.value)} />
        <span className="mt-1 block text-xs text-slate-500">At least 10 characters. Give it to them; they can change it after signing in.</span>
        {fieldError(create.error, 'Password') && <span className="mt-1 block text-sm text-red-700">{fieldError(create.error, 'Password')}</span>}
      </label>
      <div className="sm:col-span-2">
        <button className="btn-primary" disabled={create.isPending}>
          {create.isPending ? 'Adding…' : 'Add account'}
        </button>
        {create.isSuccess && <span className="ml-3 text-sm text-green-700">Account added.</span>}
      </div>
    </form>
  )
}

function RoleSelect({ value, onChange, disabled }: { value: Role; onChange: (role: Role) => void; disabled?: boolean }) {
  return (
    <label>
      <span className="label">Role</span>
      <select className="field" value={value} disabled={disabled} onChange={(event) => onChange(event.target.value as Role)}>
        {roles.map((role) => (
          <option key={role.value} value={role.value}>
            {role.label}: {role.help}
          </option>
        ))}
      </select>
    </label>
  )
}

function CrewSelect(props: {
  crew: CrewMember[]
  value: number | null
  onChange: (id: number | null) => void
  required: boolean
  error?: string
}) {
  const { crew, value, onChange, required, error } = props
  return (
    <label>
      <span className="label">Crew member {required ? '' : '(optional)'}</span>
      <select className="field" required={required} value={value ?? ''}
        onChange={(event) => onChange(event.target.value ? Number(event.target.value) : null)}>
        <option value="">{required ? 'Choose crew member' : 'Not on a crew'}</option>
        {crew.map((member) => (
          <option key={member.id} value={member.id}>
            {member.name}
          </option>
        ))}
      </select>
      {error && <span className="mt-1 block text-sm text-red-700">{error}</span>}
    </label>
  )
}
