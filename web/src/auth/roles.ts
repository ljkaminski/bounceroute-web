import type { CurrentUser } from '../api/client'

export type Role = CurrentUser['role']

export const isOffice = (user: CurrentUser | null | undefined) => user?.role === 'Owner' || user?.role === 'Office'
