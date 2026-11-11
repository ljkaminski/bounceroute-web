import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { Layout } from './components/Layout'
import './index.css'
import { BookingFormPage } from './pages/BookingFormPage'
import { BookingsPage } from './pages/BookingsPage'
import { CrewPage } from './pages/CrewPage'
import { JobPage } from './pages/JobPage'
import { RoutePage } from './pages/RoutePage'

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 15_000, retry: 1 } },
})

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { index: true, element: <RoutePage /> },
      { path: 'jobs/:id', element: <JobPage /> },
      { path: 'bookings', element: <BookingsPage /> },
      { path: 'bookings/new', element: <BookingFormPage /> },
      { path: 'bookings/:id/edit', element: <BookingFormPage /> },
      { path: 'crew', element: <CrewPage /> },
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
