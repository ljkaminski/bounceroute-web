import { useState } from 'react'
import { Navigate, useLocation, useNavigate, type Location } from 'react-router'
import { useCurrentUser, useLogin } from '../auth/useAuth'

export function LoginPage() {
  const me = useCurrentUser()
  const login = useLogin()
  const navigate = useNavigate()
  const from = (useLocation().state as { from?: Location } | null)?.from
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const destination = from ? `${from.pathname}${from.search}` : '/'
  if (me.data) return <Navigate to={destination} replace />

  return (
    <div className="grid min-h-dvh place-items-center bg-navy px-4">
      <form
        className="card w-full max-w-sm space-y-4 p-6"
        onSubmit={(event) => {
          event.preventDefault()
          login.mutate({ email, password }, { onSuccess: () => navigate(destination, { replace: true }) })
        }}
      >
        <div className="flex items-center gap-2 text-xl font-bold">
          <img src="/favicon.svg" alt="" className="size-8" />
          <span>
            Bounce<span className="text-brand">Route</span>
          </span>
        </div>
        <p className="text-sm text-slate-500">Sign in with the email and password the owner gave you.</p>
        <label className="block">
          <span className="label">Email</span>
          <input className="field" type="email" autoComplete="username" required autoFocus
            value={email} onChange={(event) => setEmail(event.target.value)} />
        </label>
        <label className="block">
          <span className="label">Password</span>
          <input className="field" type="password" autoComplete="current-password" required
            value={password} onChange={(event) => setPassword(event.target.value)} />
        </label>
        {login.isError && (
          <p role="alert" className="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-800 ring-1 ring-red-200">
            {login.error instanceof Error ? login.error.message : 'Sign-in failed.'}
          </p>
        )}
        <button className="btn-primary w-full" disabled={login.isPending}>
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
