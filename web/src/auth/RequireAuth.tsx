import { Navigate, Outlet, useLocation } from 'react-router'
import { ErrorBox, Loading } from '../components/Feedback'
import { useCurrentUser } from './useAuth'

export function RequireAuth() {
  const me = useCurrentUser()
  const location = useLocation()

  if (me.isPending) return <Loading what="Checking sign-in" />
  if (me.isError) return <div className="p-6"><ErrorBox error={me.error} /></div>
  if (me.data === null) return <Navigate to="/login" replace state={{ from: location }} />
  return <Outlet />
}
