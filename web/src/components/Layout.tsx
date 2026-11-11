import { NavLink, Outlet } from 'react-router'
import { useCurrentUser, useLogout } from '../auth/useAuth'

const links = [
  { to: '/', label: 'Route', end: true },
  { to: '/bookings', label: 'Bookings', end: false },
  { to: '/crew', label: 'Crew & pay', end: false },
]

export function Layout() {
  const me = useCurrentUser().data
  const logout = useLogout()

  return (
    <div className="min-h-dvh">
      <header className="bg-navy text-white">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-x-8 gap-y-2 px-4 py-3">
          <NavLink to="/" className="flex items-center gap-2 text-lg font-bold">
            <img src="/favicon.svg" alt="" className="size-7" />
            <span>
              Bounce<span className="text-brand">Route</span>
            </span>
          </NavLink>
          <nav className="-mx-1 flex gap-1 overflow-x-auto text-sm font-medium">
            {links.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end={link.end}
                className={({ isActive }) =>
                  `rounded-lg px-3 py-1.5 ${isActive ? 'bg-navy-soft text-white' : 'text-slate-300 hover:text-white'}`
                }
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto flex items-center gap-3 text-sm">
            <span className="text-slate-300">{me?.displayName}</span>
            <button className="text-slate-400 hover:text-white" onClick={() => logout.mutate()} disabled={logout.isPending}>
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
