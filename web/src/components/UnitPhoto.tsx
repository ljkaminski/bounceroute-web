import { apiUrl } from '../lib/photos'

const sizes = { sm: 'size-10', md: 'size-16', lg: 'size-40' }

// A unit's photo, or a plain placeholder when there isn't one. Opens full size in a new tab unless it sits
// inside something already clickable (like a checkbox label), where a link would steal the tap.
export function UnitPhoto(props: { url: string | null; name: string; size?: keyof typeof sizes; link?: boolean }) {
  const { url, name, size = 'md', link = true } = props
  if (!url)
    return (
      <div className={`${sizes[size]} grid shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-300`} aria-hidden>
        <svg viewBox="0 0 24 24" className="size-1/2" fill="none" stroke="currentColor" strokeWidth="1.5">
          <path d="M4 17l4-5 3 4 2-3 4 4M4 5h16v14H4z" strokeLinejoin="round" />
        </svg>
      </div>
    )

  const image = (
    <img src={apiUrl(url)} alt={name} loading="lazy" className={`${sizes[size]} shrink-0 rounded-lg bg-slate-100 object-cover ring-1 ring-slate-200`} />
  )
  if (!link) return image

  return (
    <a href={apiUrl(url)} target="_blank" rel="noreferrer" className="shrink-0">
      {image}
    </a>
  )
}
