import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { EntrySummary } from './api'

export function Loading({ label = 'Lädt…' }: { label?: string }) {
  return (
    <div className="flex items-center gap-3 py-10 text-muted" role="status">
      <span className="size-4 animate-spin rounded-full border-2 border-line border-t-accent" />
      {label}
    </div>
  )
}

export function ErrorNote({ error }: { error: unknown }) {
  return (
    <div className="rounded-xl border border-accent/40 bg-accent-soft px-4 py-3 text-sm">
      {error instanceof Error ? error.message : 'Etwas ist schiefgelaufen.'}
    </div>
  )
}

export function PageTitle({ children, aside }: { children: ReactNode; aside?: ReactNode }) {
  return (
    <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
      <h1 className="text-2xl font-bold tracking-tight">{children}</h1>
      {aside}
    </div>
  )
}

export function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-full border px-3 py-1 text-sm transition-colors ${
        active ? 'border-accent bg-accent-soft text-accent' : 'border-line text-muted hover:text-ink'
      }`}
    >
      {children}
    </button>
  )
}

export function Rating({ value }: { value: number | null }) {
  if (value === null) return null
  return (
    <span className="inline-flex items-center gap-1 font-semibold tabular-nums">
      <span className="text-accent">★</span>
      {value.toFixed(2)}
    </span>
  )
}

export function Cover({ src, alt, className = '' }: { src: string; alt: string; className?: string }) {
  return (
    <img
      src={src}
      alt={alt}
      loading="lazy"
      referrerPolicy="no-referrer"
      className={`aspect-[5/7] w-full bg-raised object-cover ${className}`}
    />
  )
}

export function EntryCard({ entry }: { entry: EntrySummary }) {
  return (
    <Link to={`/eintrag/${entry.id}`} className="group block">
      <div className="relative overflow-hidden rounded-xl ring-1 ring-line transition group-hover:ring-accent">
        <Cover src={entry.cover} alt="" className="transition duration-300 group-hover:scale-105" />
        {entry.rating !== null && entry.rating > 0 && (
          <span className="absolute left-2 top-2 rounded-md bg-bg/80 px-1.5 py-0.5 text-xs backdrop-blur">
            <Rating value={entry.rating} />
          </span>
        )}
        {entry.status === 'Airing' && (
          <span className="absolute right-2 top-2 rounded-md bg-good px-1.5 py-0.5 text-xs font-semibold text-bg">Airing</span>
        )}
      </div>
      <div className="mt-2 line-clamp-2 text-sm font-medium leading-snug group-hover:text-accent">{entry.title}</div>
      <div className="mt-0.5 truncate text-xs text-muted">
        {entry.type}
        {entry.genres.length > 0 && ` · ${entry.genres.slice(0, 2).join(', ')}`}
      </div>
    </Link>
  )
}

export function EntryGrid({ entries }: { entries: EntrySummary[] }) {
  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6">
      {entries.map((entry) => (
        <EntryCard key={entry.id} entry={entry} />
      ))}
    </div>
  )
}

export function Pager({ page, hasNext, onChange }: { page: number; hasNext: boolean; onChange: (page: number) => void }) {
  const button = 'rounded-full border border-line px-4 py-1.5 text-sm enabled:hover:border-accent disabled:opacity-40'
  return (
    <div className="mt-8 flex items-center justify-center gap-4">
      <button type="button" className={button} disabled={page <= 1} onClick={() => onChange(page - 1)}>
        ← Zurück
      </button>
      <span className="text-sm tabular-nums text-muted">Seite {page}</span>
      <button type="button" className={button} disabled={!hasNext} onClick={() => onChange(page + 1)}>
        Weiter →
      </button>
    </div>
  )
}
