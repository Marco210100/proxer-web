import { useSearchParams } from 'react-router-dom'
import { useEntries, type Category } from '../api'
import { Chip, EntryGrid, ErrorNote, Loading, PageTitle, Pager } from '../components'

const mediums: Record<Category, { value: string; label: string }[]> = {
  anime: [
    { value: 'animeseries', label: 'Serien' },
    { value: 'movie', label: 'Filme' },
    { value: 'ova', label: 'OVA' },
    { value: 'airing', label: 'Airing' },
  ],
  manga: [
    { value: 'mangaseries', label: 'Serien' },
    { value: 'oneshot', label: 'One-Shots' },
  ],
}

const providers = [
  { value: 'provider-proxerstream', label: 'Proxer HD' },
  { value: 'provider-aod', label: 'Anime-on-Demand' },
  { value: 'provider-crunchyroll', label: 'Crunchyroll' },
  { value: 'provider-netflix', label: 'Netflix' },
]

const sorts = [
  { value: 'rating', label: 'Beste Bewertung' },
  { value: 'clicks', label: 'Meiste Zugriffe' },
  { value: 'title', label: 'Titel' },
]

export default function Browse({ category }: { category: Category }) {
  const [params, setParams] = useSearchParams()
  const sources = category === 'anime' ? [...mediums.anime, ...providers] : mediums.manga
  const medium = sources.find((m) => m.value === params.get('medium'))?.value ?? sources[0].value
  const sort = sorts.find((s) => s.value === params.get('sort'))?.value ?? 'rating'
  const page = Math.max(1, Number(params.get('seite')) || 1)
  const entries = useEntries(category, medium, sort, page)

  const update = (changes: Record<string, string>) => {
    const next = new URLSearchParams(params)
    next.delete('seite')
    for (const [key, value] of Object.entries(changes)) next.set(key, value)
    setParams(next)
    window.scrollTo({ top: 0 })
  }

  return (
    <div>
      <PageTitle
        aside={
          <select
            value={sort}
            onChange={(event) => update({ sort: event.target.value })}
            aria-label="Sortierung"
            className="rounded-full border border-line bg-surface px-3 py-1.5 text-sm outline-none focus:border-accent"
          >
            {sorts.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
        }
      >
        {category === 'anime' ? 'Anime' : 'Manga'}
      </PageTitle>

      <div className="mb-6 flex flex-wrap items-center gap-2">
        {mediums[category].map((m) => (
          <Chip key={m.value} active={m.value === medium} onClick={() => update({ medium: m.value })}>
            {m.label}
          </Chip>
        ))}
        {category === 'anime' && (
          <>
            <span className="mx-1 h-5 w-px bg-line" aria-hidden />
            <span className="text-xs uppercase tracking-wide text-muted">Anbieter</span>
            {providers.map((p) => (
              <Chip key={p.value} active={p.value === medium} onClick={() => update({ medium: p.value })}>
                {p.label}
              </Chip>
            ))}
          </>
        )}
      </div>

      {entries.isLoading && <Loading />}
      {entries.error && <ErrorNote error={entries.error} />}
      {entries.data && (
        <div className={entries.isPlaceholderData ? 'opacity-50 transition-opacity' : ''}>
          <EntryGrid entries={entries.data.entries} />
          <Pager page={page} hasNext={entries.data.hasNext} onChange={(next) => update({ seite: String(next) })} />
        </div>
      )}
    </div>
  )
}
