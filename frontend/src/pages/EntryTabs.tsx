import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useComments, useMusic, useRecommendations, useRelations, type Comment, type PersonRef } from '../api'
import { Chip, Cover, EntryCard, ErrorNote, Loading, Pager } from '../components'

const empty = (text: string) => <p className="text-muted">{text}</p>

export function People({ title, people, kind }: { title: string; people: PersonRef[]; kind: 'character' | 'persons' }) {
  if (people.length === 0) return null
  return (
    <div>
      <h3 className="mb-3 text-xs uppercase tracking-wide text-muted">{title}</h3>
      <div className="flex gap-4 overflow-x-auto pb-1">
        {people.map((person) => (
          <Link key={person.id} to={`/seite/${kind}/${person.id}`} className="group w-24 shrink-0 text-center">
            <div
              className="mx-auto size-20 rounded-full bg-raised bg-cover bg-top ring-1 ring-line"
              style={person.image ? { backgroundImage: `url(${person.image})` } : undefined}
              role="img"
              aria-label={person.name}
            />
            <div className="mt-2 line-clamp-2 text-xs font-medium leading-snug group-hover:text-accent">{person.name}</div>
            <div className="mt-0.5 line-clamp-2 text-[11px] leading-snug text-muted">{person.role}</div>
          </Link>
        ))}
      </div>
    </div>
  )
}

export function Relations({ id }: { id: number }) {
  const relations = useRelations(id)
  if (relations.isLoading) return <Loading />
  if (relations.error) return <ErrorNote error={relations.error} />
  if (!relations.data?.length) return empty('Keine Verbindungen eingetragen.')

  return (
    <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {relations.data.map(({ entry, year, relation }) => (
        <li key={entry.id}>
          <Link
            to={`/eintrag/${entry.id}`}
            className="group flex h-full gap-3 rounded-xl border border-line bg-surface p-2.5 transition hover:border-accent"
          >
            <div className="w-14 shrink-0">
              <Cover src={entry.cover} alt="" className="rounded-md" />
            </div>
            <div className="flex min-w-0 flex-1 flex-col justify-center">
              <div className="truncate font-medium group-hover:text-accent">{entry.title}</div>
              <div className="mt-0.5 truncate text-xs text-muted">
                {entry.type}
                {year && ` · ${year}`}
              </div>
              {relation && <div className="mt-1 truncate text-xs text-accent">{relation}</div>}
            </div>
          </Link>
        </li>
      ))}
    </ul>
  )
}

export function Recommendations({ id }: { id: number }) {
  const recommendations = useRecommendations(id)
  if (recommendations.isLoading) return <Loading />
  if (recommendations.error) return <ErrorNote error={recommendations.error} />
  if (!recommendations.data?.length) return empty('Noch keine Empfehlungen.')

  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6">
      {recommendations.data.map(({ entry, up, down }) => (
        <div key={entry.id}>
          <EntryCard entry={entry} />
          <div className="mt-1 flex gap-3 text-xs tabular-nums text-muted" title="Stimmen der Proxer-Nutzer für und gegen diese Empfehlung">
            <span className="text-good">▲ {up}</span>
            <span>▼ {down}</span>
          </div>
        </div>
      ))}
    </div>
  )
}

function Stars({ value, max }: { value: number; max: number }) {
  return (
    <span className="tracking-tight" aria-label={`${value} von ${max}`}>
      <span className="text-accent">{'★'.repeat(value)}</span>
      <span className="text-line">{'★'.repeat(Math.max(0, max - value))}</span>
    </span>
  )
}

function Spoiler({ text }: { text: string }) {
  const [open, setOpen] = useState(false)
  return (
    <div className="my-2 rounded-lg border border-line">
      <button type="button" onClick={() => setOpen(!open)} className="w-full px-3 py-1.5 text-left text-xs text-muted hover:text-accent">
        {open ? '▾ Spoiler verbergen' : '▸ Spoiler anzeigen'}
      </button>
      {open && <p className="whitespace-pre-line border-t border-line px-3 py-2">{text}</p>}
    </div>
  )
}

function CommentCard({ comment }: { comment: Comment }) {
  return (
    <article className="rounded-xl border border-line bg-surface p-4">
      <header className="flex flex-wrap items-center gap-3">
        <div
          className="size-10 shrink-0 rounded-full bg-raised bg-cover bg-center"
          style={comment.avatar ? { backgroundImage: `url(${comment.avatar})` } : undefined}
        />
        <div className="min-w-0">
          <div className="truncate font-medium">{comment.username}</div>
          <div className="text-xs text-muted">{comment.state}</div>
        </div>
        <div className="ml-auto text-right text-sm">
          {comment.rating !== null && <Stars value={comment.rating} max={10} />}
          <div className="text-xs text-muted">{comment.likes} hilfreich</div>
        </div>
      </header>

      {comment.ratings.length > 0 && (
        <dl className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-xs">
          {comment.ratings.map((rating) => (
            <div key={rating.category} className="flex items-center gap-1.5">
              <dt className="text-muted">{rating.category}</dt>
              <dd>
                <Stars value={rating.stars} max={5} />
              </dd>
            </div>
          ))}
        </dl>
      )}

      <div className="mt-3 text-sm leading-relaxed text-ink/90">
        {comment.content.map((part, index) =>
          part.spoiler ? (
            <Spoiler key={index} text={part.text} />
          ) : (
            <p key={index} className="whitespace-pre-line">
              {part.text}
            </p>
          ),
        )}
      </div>
    </article>
  )
}

export function Comments({ id }: { id: number }) {
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState<'rating' | 'latest'>('rating')
  const comments = useComments(id, page, sort)
  const choose = (value: 'rating' | 'latest') => {
    setSort(value)
    setPage(1)
  }

  return (
    <div>
      <div className="mb-4 flex gap-2">
        <Chip active={sort === 'rating'} onClick={() => choose('rating')}>Beliebteste</Chip>
        <Chip active={sort === 'latest'} onClick={() => choose('latest')}>Neueste</Chip>
      </div>
      {comments.isLoading && <Loading />}
      {comments.error && <ErrorNote error={comments.error} />}
      {comments.data && (
        <div className={comments.isPlaceholderData ? 'opacity-50' : ''}>
          {comments.data.comments.length === 0 && empty('Noch keine Kommentare.')}
          <div className="space-y-3">
            {comments.data.comments.map((comment) => (
              <CommentCard key={comment.id} comment={comment} />
            ))}
          </div>
          {(page > 1 || comments.data.hasNext) && <Pager page={page} hasNext={comments.data.hasNext} onChange={setPage} />}
        </div>
      )}
    </div>
  )
}

export function Music({ id }: { id: number }) {
  const music = useMusic(id)
  if (music.isLoading) return <Loading />
  if (music.error) return <ErrorNote error={music.error} />
  if (!music.data?.length) return empty('Keine Musik eingetragen.')

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {music.data.map((album) => (
        <section key={album.name} className="flex gap-4 rounded-xl border border-line bg-surface p-4">
          {album.cover && (
            <img src={album.cover} alt="" loading="lazy" referrerPolicy="no-referrer" className="size-24 shrink-0 rounded-lg bg-raised object-cover" />
          )}
          <div className="min-w-0 flex-1">
            <h3 className="font-semibold leading-snug">{album.name}</h3>
            {album.releaseDate && <div className="text-xs text-muted">{album.releaseDate}</div>}
            <ol className="mt-2 space-y-1 text-sm">
              {album.songs.map((song) => (
                <li key={song.number} className="flex gap-3">
                  <span className="w-5 text-right tabular-nums text-muted">{song.number}</span>
                  <span className="min-w-0 flex-1 truncate">{song.title}</span>
                  <span className="tabular-nums text-muted">{song.length}</span>
                </li>
              ))}
            </ol>
          </div>
        </section>
      ))}
    </div>
  )
}
