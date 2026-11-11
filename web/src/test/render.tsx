import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'

// Answers API calls by "METHOD /path"; anything not listed fails the test loudly.
export function mockApi(routes: Record<string, () => { status: number; body?: unknown }>) {
  const calls: { method: string; path: string; body: unknown }[] = []
  vi.stubGlobal('fetch', async (request: Request) => {
    const path = new URL(request.url).pathname
    const key = `${request.method} ${path}`
    const text = await request.text()
    calls.push({ method: request.method, path, body: text ? JSON.parse(text) : undefined })
    const handler = routes[key]
    if (!handler) throw new Error(`Unexpected request ${key}`)
    const { status, body } = handler()
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
  })
  return calls
}

export function renderRoutes(routes: RouteObject[], initialPath: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [initialPath] })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}
