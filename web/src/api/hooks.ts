import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, unwrap, type SaveBookingRequest } from './client'

export const keys = {
  route: (date: string) => ['route', date] as const,
  job: (id: number) => ['job', id] as const,
  bookings: (from: string, to: string) => ['bookings', from, to] as const,
  booking: (id: number) => ['booking', id] as const,
  crew: ['crew'] as const,
  units: (from: string, to: string) => ['units', from, to] as const,
  myUnits: (from: string, to: string) => ['my-units', from, to] as const,
}

export const useRouteDay = (date: string) =>
  useQuery({
    queryKey: keys.route(date),
    queryFn: () => unwrap(api.GET('/api/route', { params: { query: { date } } })),
  })

export const useJob = (id: number) =>
  useQuery({
    queryKey: keys.job(id),
    queryFn: () => unwrap(api.GET('/api/jobs/{id}', { params: { path: { id } } })),
  })

export const useBookings = (from: string, to: string) =>
  useQuery({
    queryKey: keys.bookings(from, to),
    queryFn: () => unwrap(api.GET('/api/bookings', { params: { query: { from, to } } })),
  })

export const useBooking = (id: number | undefined) =>
  useQuery({
    queryKey: keys.booking(id ?? 0),
    queryFn: () => unwrap(api.GET('/api/bookings/{id}', { params: { path: { id: id! } } })),
    enabled: id !== undefined,
  })

export const useCrew = () =>
  useQuery({
    queryKey: keys.crew,
    queryFn: () => unwrap(api.GET('/api/crew', { params: { query: {} } })),
  })

export const useUnitTotals = (from: string, to: string) =>
  useQuery({
    queryKey: keys.units(from, to),
    queryFn: () => unwrap(api.GET('/api/crew/units', { params: { query: { from, to } } })),
  })

export const useMyUnits = (from: string, to: string) =>
  useQuery({
    queryKey: keys.myUnits(from, to),
    queryFn: () => unwrap(api.GET('/api/me/units', { params: { query: { from, to } } })),
  })

// Any change to a job, booking, or crew can move what the route, booking list, and pay totals show,
// so mutations refresh all of them. The data is small; simple beats clever here.
function useRefreshAll() {
  const client = useQueryClient()
  return () => client.invalidateQueries()
}

export function useSaveBooking(id: number | undefined) {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: SaveBookingRequest) =>
      id === undefined
        ? unwrap(api.POST('/api/bookings', { body }))
        : unwrap(api.PUT('/api/bookings/{id}', { params: { path: { id } }, body })),
    onSuccess: refresh,
  })
}

export function useDeleteBooking() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/bookings/{id}', { params: { path: { id } } })),
    onSuccess: refresh,
  })
}

export function useCompleteJob() {
  const refresh = useRefreshAll()
  return useMutation({
    // Crew leave crewMemberId out and the API records them; office staff pick someone.
    mutationFn: ({ jobId, crewMemberId = null }: { jobId: number; crewMemberId?: number | null }) =>
      unwrap(api.POST('/api/jobs/{id}/complete', { params: { path: { id: jobId } }, body: { crewMemberId } })),
    onSuccess: refresh,
  })
}

export function useReopenJob() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (jobId: number) => unwrap(api.POST('/api/jobs/{id}/reopen', { params: { path: { id: jobId } } })),
    onSuccess: refresh,
  })
}

export function useSetItemLoaded() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: ({ itemId, isLoaded }: { itemId: number; isLoaded: boolean }) =>
      unwrap(api.PUT('/api/booking-items/{id}/loaded', { params: { path: { id: itemId } }, body: { isLoaded } })),
    onSuccess: refresh,
  })
}

export function useAddCrewMember() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: { name: string; role: string }) =>
      unwrap(api.POST('/api/crew', { body: { ...body, initials: null, isActive: true } })),
    onSuccess: refresh,
  })
}
