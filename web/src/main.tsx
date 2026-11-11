import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { ApiError } from './api/client'
import { RequireAuth } from './auth/RequireAuth'
import { RequireRole } from './auth/RequireRole'
import { meKey } from './auth/useAuth'
import { Layout } from './components/Layout'
import './index.css'
import { AccountsPage } from './pages/AccountsPage'
import { BookingFormPage } from './pages/BookingFormPage'
import { BookingsPage } from './pages/BookingsPage'
import { CrewPage } from './pages/CrewPage'
import { JobPage } from './pages/JobPage'
import { ChangePasswordPage } from './pages/ChangePasswordPage'
import { LoginPage } from './pages/LoginPage'
import { MyUnitsPage } from './pages/MyUnitsPage'
import { RoutePage } from './pages/RoutePage'

// When the session runs out mid-use, any 401 sends the app back to the sign-in page.
const signOutOn401 = (error: Error) => {
  if (error instanceof ApiError && error.status === 401) queryClient.setQueryData(meKey, null)
}

const queryClient: QueryClient = new QueryClient({
  queryCache: new QueryCache({ onError: signOutOn401 }),
  mutationCache: new MutationCache({ onError: signOutOn401 }),
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 1,
    },
  },
})

const router = createBrowserRouter([
  { path: 'login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <Layout />,
        children: [
          { index: true, element: <RoutePage /> },
          { path: 'jobs/:id', element: <JobPage /> },
          { path: 'me/units', element: <MyUnitsPage /> },
          { path: 'me/password', element: <ChangePasswordPage /> },
          {
            element: <RequireRole roles={['Owner', 'Office']} />,
            children: [
              { path: 'bookings', element: <BookingsPage /> },
              { path: 'bookings/new', element: <BookingFormPage /> },
              { path: 'bookings/:id/edit', element: <BookingFormPage /> },
              { path: 'crew', element: <CrewPage /> },
            ],
          },
          {
            element: <RequireRole roles={['Owner']} />,
            children: [{ path: 'accounts', element: <AccountsPage /> }],
          },
        ],
      },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
