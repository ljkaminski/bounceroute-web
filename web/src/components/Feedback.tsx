import { ApiError } from '../api/client'

export function Loading({ what = 'Loading' }: { what?: string }) {
  return <p className="py-10 text-center text-sm text-slate-500">{what}…</p>
}

export function ErrorBox({ error }: { error: unknown }) {
  const message =
    error instanceof ApiError && error.status === 404
      ? 'Not found.'
      : error instanceof Error
        ? error.message
        : 'Something went wrong.'
  return (
    <div role="alert" className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-800 ring-1 ring-red-200">
      {message} {error instanceof TypeError && 'Is the API running?'}
    </div>
  )
}

export function EmptyState({ children }: { children: React.ReactNode }) {
  return <div className="card px-6 py-12 text-center text-sm text-slate-500">{children}</div>
}
