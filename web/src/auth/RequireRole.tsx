import { Navigate, Outlet } from 'react-router'
import type { Role } from './roles'
import { useCurrentUser } from './useAuth'

// Sits inside RequireAuth, so the user is already loaded. The API enforces the same rules;
// this just keeps people off screens that would only show them a 403.
export function RequireRole({ roles }: { roles: Role[] }) {
  const me = useCurrentUser().data
  return me && roles.includes(me.role) ? <Outlet /> : <Navigate to="/" replace />
}
