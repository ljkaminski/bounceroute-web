import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, unwrap, type CurrentUser } from '../api/client'

export const meKey = ['me'] as const

// The signed-in user, or null when signed out. A 401 here is an answer, not an error.
export function useCurrentUser() {
  return useQuery({
    queryKey: meKey,
    queryFn: async () => {
      try {
        return await unwrap(api.GET('/api/auth/me'))
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: Infinity,
  })
}

export function useLogin() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (body: { email: string; password: string }) => unwrap(api.POST('/api/auth/login', { body })),
    onSuccess: (user) => {
      // Drop anything cached for a previous user before showing this one's data.
      client.removeQueries({ predicate: (query) => query.queryKey[0] !== meKey[0] })
      client.setQueryData<CurrentUser | null>(meKey, user)
    },
  })
}

export function useLogout() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => unwrap(api.POST('/api/auth/logout')),
    onSettled: () => {
      client.clear()
      client.setQueryData(meKey, null)
    },
  })
}
