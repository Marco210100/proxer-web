import { useState } from 'react'
import { Link } from 'react-router-dom'
import {
  languageLabel,
  useBookmarks,
  useDeleteBookmark,
  useDeleteListEntry,
  useMyList,
  useSetListEpisode,
  useSetListState,
  type Category,
  type MyListItem,
} from '../api'
import { Chip, Cover, ErrorNote, Loading, PageTitle } from '../components'

// State numbers as used by Proxer's list.
const states = [
  { value: 1, label: 'Am Schauen' },
  { value: 2, label: 'Geplant' },
  { value: 0, label: 'Abgeschlossen' },
  { value: 3, label: 'Abgebrochen' },
]

const iconButton =
  'flex size-7 items-center justify-center rounded-full border border-line text-sm text-muted transition-colors enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-40'

function ListCard({ item }: { item: MyListItem }) {
  const setEpisode = useSetListEpisode()
  const setState = useSetListState()
  const remove = useDeleteListEntry()
  const busy = setEpisode.isPending || setState.isPending || remove.isPending
  const percent = item.episodes > 0 ? Math.min(100, (item.episode / item.episodes) * 100) : 0
  const canAdvance = item.episodes === 0 || item.episode < item.episodes

  return (
    <div className={`flex gap-3 rounded-xl border border-line bg-surface p-2.5 ${busy ? 'opacity-60' : ''}`}>
      <Link to={`/eintrag/${item.id}`} className="w-14 shrink-0">
        <Cover src={item.cover} alt="" className="rounded-md" />
      </Link>
      <div className="flex min-w-0 flex-1 flex-col justify-center">
        <Link to={`/eintrag/${item.id}`} className="truncate font-medium hover:text-accent">
          {item.name}
        </Link>
        <div className="mt-0.5 text-xs tabular-nums text-muted">
          {item.episode} / {item.episodes || '?'}
          {item.rating > 0 && <span className="ml-2 text-accent">★ {item.rating}</span>}
        </div>
        <div className="mt-2 h-1 overflow-hidden rounded-full bg-raised">
          <div className="h-full rounded-full bg-accent" style={{ width: `${percent}%` }} />
        </div>
        <div className="mt-2 flex items-center gap-1.5">
          <button
            type="button"
            className={iconButton}
            disabled={busy || item.episode <= 0}
            onClick={() => setEpisode.mutate({ cid: item.cid, value: item.episode - 1 })}
            aria-label={`${item.name}: eine Episode zurück`}
            title="Eine zurück"
          >
            −
          </button>
          <button
            type="button"
            className={iconButton}
            disabled={busy || !canAdvance}
            onClick={() => setEpisode.mutate({ cid: item.cid, value: item.episode + 1 })}
            aria-label={`${item.name}: eine Episode weiter`}
            title="Eine weiter"
          >
            +
          </button>
          <select
            value={item.state}
            disabled={busy}
            onChange={(event) => setState.mutate({ cid: item.cid, state: Number(event.target.value) })}
            aria-label={`${item.name}: Status`}
            className="ml-1 min-w-0 flex-1 rounded-full border border-line bg-surface px-2 py-1 text-xs outline-none focus:border-accent"
          >
            {states.map((s) => (
              <option key={s.value} value={s.value}>
                {s.label}
              </option>
            ))}
          </select>
          <button
            type="button"
            className={iconButton}
            disabled={busy}
            onClick={() => {
              if (window.confirm(`„${item.name}“ wirklich von deiner Liste entfernen? Bewertung und Kommentar gehen dabei verloren.`))
                remove.mutate(item.cid)
            }}
            aria-label={`${item.name} von der Liste entfernen`}
            title="Von der Liste entfernen"
          >
            ✕
          </button>
        </div>
      </div>
    </div>
  )
}

function Bookmarks({ category }: { category: Category }) {
  const bookmarks = useBookmarks()
  const remove = useDeleteBookmark()
  const items = bookmarks.data?.filter((b) => b.category === category) ?? []

  if (bookmarks.isLoading) return <Loading />
  if (bookmarks.error) return <ErrorNote error={bookmarks.error} />
  if (items.length === 0) return <p className="text-muted">Keine Lesezeichen gesetzt.</p>

  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {items.map((bookmark) => {
        const unit = category === 'anime' ? 'Episode' : 'Kapitel'
        const target = `/${category === 'anime' ? 'watch' : 'lesen'}/${bookmark.entryId}/${bookmark.episode}/${bookmark.language}`
        const label = `${bookmark.available ? '▶' : '○'} ${unit} ${bookmark.episode} · ${languageLabel(bookmark.language)}`
        const linkClass = `inline-block rounded-full px-3 py-1 text-xs font-medium ${
          bookmark.available ? 'bg-accent-soft text-accent hover:bg-accent hover:text-white' : 'border border-line text-muted'
        }`
        return (
          <div key={bookmark.id} className="flex gap-3 rounded-xl border border-line bg-surface p-2.5">
            <Link to={`/eintrag/${bookmark.entryId}`} className="w-14 shrink-0">
              <Cover src={bookmark.cover} alt="" className="rounded-md" />
            </Link>
            <div className="flex min-w-0 flex-1 flex-col justify-center gap-1.5">
              <Link to={`/eintrag/${bookmark.entryId}`} className="truncate font-medium hover:text-accent">
                {bookmark.title}
              </Link>
              <div className="flex items-center gap-2">
                <Link to={target} className={linkClass} title={bookmark.available ? undefined : 'Noch nicht verfügbar'}>
                  {label}
                </Link>
                <button
                  type="button"
                  className={`${iconButton} ml-auto`}
                  disabled={remove.isPending}
                  onClick={() => remove.mutate(bookmark.id)}
                  aria-label={`Lesezeichen für ${bookmark.title} entfernen`}
                  title="Lesezeichen entfernen"
                >
                  ✕
                </button>
              </div>
            </div>
          </div>
        )
      })}
    </div>
  )
}

export default function MyList() {
  const [category, setCategory] = useState<Category>('anime')
  const [view, setView] = useState<number | 'bookmarks'>(1)
  const list = useMyList(category)
  const bookmarks = useBookmarks()
  const items = list.data?.filter((item) => item.state === view) ?? []
  const count = (value: number) => list.data?.filter((item) => item.state === value).length ?? 0

  return (
    <div>
      <PageTitle
        aside={
          <div className="flex gap-2">
            <Chip active={category === 'anime'} onClick={() => setCategory('anime')}>Anime</Chip>
            <Chip active={category === 'manga'} onClick={() => setCategory('manga')}>Manga</Chip>
            <Chip active={category === 'novel'} onClick={() => setCategory('novel')}>Novel</Chip>
          </div>
        }
      >
        Meine Liste
      </PageTitle>

      <div className="mb-6 flex flex-wrap gap-2">
        {states.map((s) => (
          <Chip key={s.value} active={s.value === view} onClick={() => setView(s.value)}>
            {s.label} <span className="tabular-nums opacity-70">{count(s.value)}</span>
          </Chip>
        ))}
        <span className="mx-1 h-7 w-px bg-line" aria-hidden />
        <Chip active={view === 'bookmarks'} onClick={() => setView('bookmarks')}>
          Lesezeichen{' '}
          <span className="tabular-nums opacity-70">{bookmarks.data?.filter((b) => b.category === category).length ?? 0}</span>
        </Chip>
      </div>

      {view === 'bookmarks' ? (
        <Bookmarks category={category} />
      ) : (
        <>
          {list.isLoading && <Loading />}
          {list.error && <ErrorNote error={list.error} />}
          {list.data && items.length === 0 && <p className="text-muted">Hier ist noch nichts eingetragen.</p>}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {items.map((item) => (
              <ListCard key={item.cid} item={item} />
            ))}
          </div>
        </>
      )}
    </div>
  )
}
