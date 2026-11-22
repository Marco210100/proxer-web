import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMyList, type Category } from '../api'
import { Chip, Cover, ErrorNote, Loading, PageTitle } from '../components'

// State numbers as used by Proxer's list.
const states = [
  { value: 1, label: 'Am Schauen' },
  { value: 2, label: 'Geplant' },
  { value: 0, label: 'Abgeschlossen' },
  { value: 3, label: 'Abgebrochen' },
]

export default function MyList() {
  const [category, setCategory] = useState<Category>('anime')
  const [state, setState] = useState(1)
  const list = useMyList(category)
  const items = list.data?.filter((item) => item.state === state) ?? []
  const count = (value: number) => list.data?.filter((item) => item.state === value).length ?? 0

  return (
    <div>
      <PageTitle
        aside={
          <div className="flex gap-2">
            <Chip active={category === 'anime'} onClick={() => setCategory('anime')}>Anime</Chip>
            <Chip active={category === 'manga'} onClick={() => setCategory('manga')}>Manga</Chip>
          </div>
        }
      >
        Meine Liste
      </PageTitle>

      <div className="mb-6 flex flex-wrap gap-2">
        {states.map((s) => (
          <Chip key={s.value} active={s.value === state} onClick={() => setState(s.value)}>
            {s.label} <span className="tabular-nums opacity-70">{count(s.value)}</span>
          </Chip>
        ))}
      </div>

      {list.isLoading && <Loading />}
      {list.error && <ErrorNote error={list.error} />}
      {list.data && items.length === 0 && <p className="text-muted">Hier ist noch nichts eingetragen.</p>}

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((item) => {
          const percent = item.episodes > 0 ? Math.min(100, (item.episode / item.episodes) * 100) : 0
          return (
            <Link
              key={item.id}
              to={`/eintrag/${item.id}`}
              className="flex gap-3 rounded-xl border border-line bg-surface p-2.5 transition hover:border-accent"
            >
              <div className="w-14 shrink-0">
                <Cover src={item.cover} alt="" className="rounded-md" />
              </div>
              <div className="flex min-w-0 flex-1 flex-col justify-center">
                <div className="truncate font-medium">{item.name}</div>
                <div className="mt-0.5 text-xs tabular-nums text-muted">
                  {item.episode} / {item.episodes || '?'}
                  {item.rating > 0 && <span className="ml-2 text-accent">★ {item.rating}</span>}
                </div>
                <div className="mt-2 h-1 overflow-hidden rounded-full bg-raised">
                  <div className="h-full rounded-full bg-accent" style={{ width: `${percent}%` }} />
                </div>
              </div>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
