import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

export type Schemas = components['schemas']
export type JobCard = Schemas['JobCardDto']
export type Booking = Schemas['BookingDto']
export type BookingSummary = Schemas['BookingSummaryDto']
export type SaveBookingRequest = Schemas['SaveBookingRequest']
export type CrewMember = Schemas['CrewMemberDto']
export type UnitTotalsReport = Schemas['UnitTotalsReport']
export type Surface = Schemas['Surface']

// Empty in development (Vite proxies /api); set VITE_API_URL when the API lives elsewhere.
export const api = createClient<paths>({ baseUrl: import.meta.env.VITE_API_URL ?? '' })

// Carries the API's field errors (from a 400 validation problem) so forms can show them by field.
export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, body: unknown) {
    const problem = (body ?? {}) as { title?: string; errors?: Record<string, string[]> }
    super(problem.title ?? `Request failed (${status})`)
    this.status = status
    this.fieldErrors = problem.errors ?? {}
  }
}

// openapi-fetch returns { data, error, response }; React Query wants a value or a thrown error.
export async function unwrap<T>(request: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await request
  if (!response.ok) throw new ApiError(response.status, error)
  return data as T
}
