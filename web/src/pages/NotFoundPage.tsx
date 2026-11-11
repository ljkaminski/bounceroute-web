import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="card mx-auto max-w-md space-y-3 p-8 text-center">
      <h1 className="text-xl font-bold">Page not found</h1>
      <p className="text-sm text-slate-500">That link doesn't go anywhere in BounceRoute.</p>
      <Link to="/" className="btn-primary">
        Back to the route
      </Link>
    </div>
  )
}
