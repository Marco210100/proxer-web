import { useSearchParams } from 'react-router-dom'
import { useSeason } from '../api'
import { Chip, EntryGrid, ErrorNote, Loading, PageTitle } from '../components'

// Season numbers as used by Proxer.
const seasons = [
  { value: 1, label: 'Winter' },
  { value: 2, label: 'Frühling' },
  { value: 3, label: 'Sommer' },
  { value: 4, label: 'Herbst' },
]

export default function Season() {
  const [params, setParams] = useSearchParams()
  const season = useSeason(Number(params.get('jahr')) || null, Number(params.get('season')) || null)
  const data = season.data

  const select = (year: number, value: number) => setParams({ jahr: String(year), season: String(value) })
  // One step back or forward, rolling over the year at the ends.
  const step = (direction: 1 | -1) => {
    if (!data) return
    const index = data.year * 4 + (data.season - 1) + direction
    select(Math.floor(index / 4), (index % 4) + 1)
  }
  const arrow = 'rounded-full border border-line px-3 py-1 text-sm hover:border-accent hover:text-accent'

  return (
    <div>
      <PageTitle
        aside={
          data && (
            <div className="flex items-center gap-2">
              <button type="button" className={arrow} onClick={() => step(-1)} aria-label="Vorherige Season">
                ←
              </button>
              <button type="button" className={arrow} onClick={() => step(1)} aria-label="Nächste Season">
                →
              </button>
            </div>
          )
        }
      >
        Season{data && ` · ${seasons.find((s) => s.value === data.season)?.label} ${data.year}`}
      </PageTitle>

      {data && (
        <div className="mb-6 flex flex-wrap items-center gap-2">
          {seasons.map((s) => (
            <Chip key={s.value} active={s.value === data.season} onClick={() => select(data.year, s.value)}>
              {s.label}
            </Chip>
          ))}
          <span className="mx-1 h-5 w-px bg-line" aria-hidden />
          <select
            value={data.year}
            onChange={(event) => select(Number(event.target.value), data.season)}
            aria-label="Jahr"
            className="rounded-full border border-line bg-surface px-3 py-1 text-sm outline-none focus:border-accent"
          >
            {Array.from({ length: 40 }, (_, index) => new Date().getFullYear() + 1 - index).map((year) => (
              <option key={year} value={year}>
                {year}
              </option>
            ))}
          </select>
        </div>
      )}

      {season.isLoading && <Loading />}
      {season.error && <ErrorNote error={season.error} />}
      {data && (
        <div className={`space-y-10 ${season.isPlaceholderData ? 'opacity-50 transition-opacity' : ''}`}>
          {data.groups.length === 0 && <p className="text-muted">Für diese Season ist nichts eingetragen.</p>}
          {data.groups.map((group) => (
            <section key={group.title}>
              <h2 className="mb-4 text-xl font-bold tracking-tight">
                {group.title} <span className="text-sm font-normal text-muted">{group.entries.length}</span>
              </h2>
              <EntryGrid entries={group.entries} />
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
