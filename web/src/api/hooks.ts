import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  api,
  unwrap,
  type CreateUserRequest,
  type SaveBookingRequest,
  type Schemas,
  type SaveInventoryItemRequest,
  type UpdateUserRequest,
} from './client'
import { shrinkPhoto, uploadPhoto } from '../lib/photos'

export const keys = {
  route: (date: string) => ['route', date] as const,
  job: (id: number) => ['job', id] as const,
  bookings: (from: string, to: string) => ['bookings', from, to] as const,
  booking: (id: number) => ['booking', id] as const,
  crew: ['crew'] as const,
  units: (from: string, to: string) => ['units', from, to] as const,
  myUnits: (from: string, to: string) => ['my-units', from, to] as const,
  users: ['users'] as const,
  settings: ['settings'] as const,
  setupTimes: ['setup-times'] as const,
  trucks: ['trucks'] as const,
  trucksOut: (date: string) => ['trucks-out', date] as const,
  suggestion: (date: string) => ['suggestion', date] as const,
  plan: (date: string) => ['plan', date] as const,
  inventory: (includeInactive: boolean) => ['inventory', includeInactive] as const,
  availability: (setupAt: string, pickupAt: string, excludeBookingId?: number) =>
    ['availability', setupAt, pickupAt, excludeBookingId] as const,
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

export const useCrew = (includeInactive = false) =>
  useQuery({
    queryKey: [...keys.crew, includeInactive],
    queryFn: () => unwrap(api.GET('/api/crew', { params: { query: { includeInactive } } })),
  })

export const useInventory = (includeInactive = false) =>
  useQuery({
    queryKey: keys.inventory(includeInactive),
    queryFn: () => unwrap(api.GET('/api/inventory', { params: { query: { includeInactive } } })),
  })

// How many of each item are free for a setup-to-pickup window (ISO timestamps).
export const useAvailability = (setupAt: string | null, pickupAt: string | null, excludeBookingId?: number) =>
  useQuery({
    queryKey: keys.availability(setupAt ?? '', pickupAt ?? '', excludeBookingId),
    queryFn: () =>
      unwrap(api.GET('/api/inventory/availability', {
        params: { query: { setupAt: setupAt!, pickupAt: pickupAt!, excludeBookingId } },
      })),
    enabled: setupAt !== null && pickupAt !== null && pickupAt > setupAt,
  })

export const useSettings = () =>
  useQuery({
    queryKey: keys.settings,
    queryFn: () => unwrap(api.GET('/api/settings')),
    staleTime: 5 * 60_000,
  })

export const useSetupTimes = (enabled = true) =>
  useQuery({
    queryKey: keys.setupTimes,
    queryFn: () => unwrap(api.GET('/api/settings/setup-times')),
    enabled,
  })

export const useRoutePlan = (date: string) =>
  useQuery({
    queryKey: keys.plan(date),
    queryFn: () => unwrap(api.GET('/api/route/plan', { params: { query: { date } } })),
  })

export const useTrucks = (enabled = true) =>
  useQuery({
    queryKey: keys.trucks,
    queryFn: () => unwrap(api.GET('/api/trucks')),
    enabled,
  })

export const useTrucksOut = (date: string, enabled = true) =>
  useQuery({
    queryKey: keys.trucksOut(date),
    queryFn: () => unwrap(api.GET('/api/route/trucks', { params: { query: { date } } })),
    enabled,
  })

// Worked out by the route optimizer, so it can take a few seconds; cached on the server per set of inputs.
export const useSuggestion = (date: string, enabled = true) =>
  useQuery({
    queryKey: keys.suggestion(date),
    queryFn: () => unwrap(api.GET('/api/route/suggestion', { params: { query: { date } } })),
    enabled,
    staleTime: 60_000,
  })

export const useUsers = () =>
  useQuery({
    queryKey: keys.users,
    queryFn: () => unwrap(api.GET('/api/users')),
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
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => unwrap(api.DELETE('/api/bookings/{id}', { params: { path: { id } } })),
    // Refresh everything except the booking that's gone. Re-fetching it gets a 404, and if that lands before the
    // page navigates away, the edit page swaps to "Not found." and the navigation never happens.
    onSuccess: (_, id) => {
      const [name, deletedId] = keys.booking(id)
      return client.invalidateQueries({ predicate: (query) => !(query.queryKey[0] === name && query.queryKey[1] === deletedId) })
    },
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

export function useCreateUser() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: CreateUserRequest) => unwrap(api.POST('/api/users', { body })),
    onSuccess: refresh,
  })
}

export function useUpdateUser() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateUserRequest }) =>
      unwrap(api.PUT('/api/users/{id}', { params: { path: { id } }, body })),
    onSuccess: refresh,
  })
}

export function useResetPassword() {
  return useMutation({
    mutationFn: ({ id, newPassword }: { id: string; newPassword: string }) =>
      unwrap(api.POST('/api/users/{id}/password', { params: { path: { id } }, body: { newPassword } })),
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: (body: { currentPassword: string; newPassword: string }) =>
      unwrap(api.POST('/api/auth/password', { body })),
  })
}

export function useSaveInventoryItem(id: number | undefined) {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: SaveInventoryItemRequest) =>
      id === undefined
        ? unwrap(api.POST('/api/inventory', { body }))
        : unwrap(api.PUT('/api/inventory/{id}', { params: { path: { id } }, body })),
    onSuccess: refresh,
  })
}

export function useImportInventory() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: SaveInventoryItemRequest[]) => unwrap(api.POST('/api/inventory/import', { body })),
    onSuccess: refresh,
  })
}

export function useSaveSettings() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: { companyName: string; defaultCityStateZip: string; phone: string; shopAddress: string }) =>
      unwrap(api.PUT('/api/settings', { body })),
    onSuccess: refresh,
  })
}

export function useUploadInventoryPhoto() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: async ({ itemId, file }: { itemId: number; file: File }) => uploadPhoto(itemId, await shrinkPhoto(file)),
    onSuccess: refresh,
  })
}

export function useRemoveInventoryPhoto() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (itemId: number) => unwrap(api.DELETE('/api/inventory/{id}/photo', { params: { path: { id: itemId } } })),
    onSuccess: refresh,
  })
}

export function useSaveSetupTimes() {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: Schemas['CategorySetupTimeDto'][]) => unwrap(api.PUT('/api/settings/setup-times', { body })),
    onSuccess: refresh,
  })
}

export function useSaveTruck(id: number | undefined) {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (body: Schemas['SaveTruckRequest']) =>
      id === undefined
        ? unwrap(api.POST('/api/trucks', { body }))
        : unwrap(api.PUT('/api/trucks/{id}', { params: { path: { id } }, body })),
    onSuccess: refresh,
  })
}

export function useSaveTrucksOut(date: string) {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (truckIds: number[]) => unwrap(api.PUT('/api/route/trucks', { params: { query: { date } }, body: { truckIds } })),
    onSuccess: refresh,
  })
}

export function useApplySuggestion(date: string) {
  const refresh = useRefreshAll()
  return useMutation({
    mutationFn: (fingerprint: string) =>
      unwrap(api.POST('/api/route/suggestion/apply', { params: { query: { date } }, body: { fingerprint } })),
    onSuccess: refresh,
  })
}
