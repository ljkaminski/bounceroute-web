import { useState } from 'react'
import { Link } from 'react-router'
import { useBookings } from '../api/hooks'
import { EmptyState, ErrorBox, Loading } from '../components/Feedback'
import { addDays, formatShortDateTime, today } from '../lib/dates'

export function BookingsPage() {
  const [from, setFrom] = useState(today)
  const [to, setTo] = useState(() => addDays(today(), 13))
  const bookings = useBookings(from, to)

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">Bookings</h1>
          <p className="text-sm text-slate-500">By setup date</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <label>
            <span className="label">From</span>
            <input type="date" className="field" value={from} onChange={(event) => event.target.value && setFrom(event.target.value)} />
          </label>
          <label>
            <span className="label">To</span>
            <input type="date" className="field" value={to} onChange={(event) => event.target.value && setTo(event.target.value)} />
          </label>
          <Link to="/bookings/new" className="btn-primary">
            New booking
          </Link>
        </div>
      </div>

      {bookings.isPending ? (
        <Loading what="Loading bookings" />
      ) : bookings.isError ? (
        <ErrorBox error={bookings.error} />
      ) : bookings.data.length === 0 ? (
        <EmptyState>No bookings set up in these dates.</EmptyState>
      ) : (
        <div className="card overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-slate-200 text-xs text-slate-500 uppercase">
              <tr>
                <th className="px-4 py-3">Job</th>
                <th className="px-4 py-3">Customer</th>
                <th className="px-4 py-3">Setup</th>
                <th className="px-4 py-3">Pickup</th>
                <th className="px-4 py-3 text-right">Units</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {bookings.data.map((booking) => (
                <tr key={booking.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 text-slate-500">
                    <Link to={`/bookings/${booking.id}/edit`} className="font-semibold text-brand hover:underline">
                      #{booking.jobNumber}
                    </Link>
                  </td>
                  <td className="px-4 py-3">
                    <div className="font-medium">{booking.customerName}</div>
                    <div className="text-xs text-slate-500">{booking.street}</div>
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap">{formatShortDateTime(booking.setupAt)}</td>
                  <td className="px-4 py-3 whitespace-nowrap">{formatShortDateTime(booking.pickupAt)}</td>
                  <td className="px-4 py-3 text-right">
                    {booking.unitCount}
                    {booking.isComplete && <span className="ml-2 text-xs font-semibold text-green-700">Done</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
