import { useState } from 'react'
import { Link } from 'react-router-dom'
import { languageLabel, useArticles, useDiscussions, useEntryHistory, useEntryUpdates, useGallery } from '../api'
import { ErrorNote, Loading, Pager } from '../components'

const empty = (text: string) => <p className="text-muted">{text}</p>
const table = 'w-full overflow-hidden rounded-xl border border-line bg-surface text-sm'
const cell = 'px-4 py-2.5'

export function EntryUpdates({ id }: { id: number }) {
  const updates = useEntryUpdates(id)
  if (updates.isLoading) return <Loading />
  if (updates.error) return <ErrorNote error={updates.error} />
  if (!updates.data?.length) return empty('Keine Uploads verzeichnet.')

  return (
    <ul className={`${table} divide-y divide-line`}>
      {updates.data.map((update, index) => (
        <li key={index} className={`${cell} flex flex-wrap items-center gap-x-4 gap-y-1`}>
          <span className="min-w-0 flex-1">{update.title}</span>
          <span className="text-muted">{update.language}</span>
          <span className="tabular-nums text-muted">{update.date}</span>
          {update.uploaderId ? (
            <Link to={`/user/${update.uploaderId}`} className="text-muted hover:text-accent">
              {update.uploader}
            </Link>
          ) : (
            <span className="text-muted">{update.uploader}</span>
          )}
          {update.episode !== null && update.languageCode && (
            <Link to={`/watch/${id}/${update.episode}/${update.languageCode}`} className="rounded-full bg-accent-soft px-3 py-1 text-xs font-medium text-accent hover:bg-accent hover:text-white">
              ▶ Ansehen
            </Link>
          )}
        </li>
      ))}
    </ul>
  )
}

export function Discussions({ id }: { id: number }) {
  const discussions = useDiscussions(id)
  if (discussions.isLoading) return <Loading />
  if (discussions.error) return <ErrorNote error={discussions.error} />
  if (!discussions.data?.length) return empty('Keine Diskussionen oder News zu diesem Werk.')

  return (
    <ul className="space-y-2">
      {discussions.data.map((topic) => (
        <li key={topic.topicId}>
          <Link
            to={`/forum/${topic.categoryId}/${topic.topicId}`}
            className="group block rounded-xl border border-line bg-surface px-4 py-3 transition hover:border-accent"
          >
            <div className="font-medium leading-snug group-hover:text-accent">{topic.title}</div>
            <div className="mt-1 text-xs text-muted">
              {topic.category} · von {topic.author || 'unbekannt'} · {topic.replies} Antworten · {topic.views} Aufrufe · zuletzt {topic.lastPost}
            </div>
          </Link>
        </li>
      ))}
    </ul>
  )
}

export function Gallery({ id }: { id: number }) {
  const [page, setPage] = useState(1)
  const gallery = useGallery(id, page)
  if (gallery.isLoading) return <Loading />
  if (gallery.error) return <ErrorNote error={gallery.error} />
  const data = gallery.data
  if (!data?.images.length) return empty('Keine Bilder vorhanden.')

  return (
    <div className={gallery.isPlaceholderData ? 'opacity-50' : ''}>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
        {data.images.map((image) => (
          <a
            key={image.id}
            href={`https://proxer.me/gallery?s=detail&id=${image.id}`}
            target="_blank"
            rel="noreferrer"
            title={image.title}
            className="group block overflow-hidden rounded-xl ring-1 ring-line transition hover:ring-accent"
          >
            <img src={image.image} alt={image.title} loading="lazy" referrerPolicy="no-referrer" className="aspect-square w-full bg-raised object-cover transition duration-300 group-hover:scale-105" />
          </a>
        ))}
      </div>
      {(page > 1 || data.hasNext) && <Pager page={page} hasNext={data.hasNext} onChange={setPage} />}
    </div>
  )
}

export function EntryHistory({ id }: { id: number }) {
  const history = useEntryHistory(id)
  if (history.isLoading) return <Loading />
  if (history.error) return <ErrorNote error={history.error} />
  if (!history.data?.length) return empty('Du hast hiervon noch nichts angesehen.')

  return (
    <ul className={`${table} divide-y divide-line`}>
      {history.data.map((item, index) => (
        <li key={index} className={`${cell} flex flex-wrap items-center gap-x-4 gap-y-1`}>
          <span className="min-w-0 flex-1">
            Episode {item.episode} <span className="text-muted">· {item.language}</span>
          </span>
          <span className="tabular-nums text-muted">{item.date}</span>
          {item.episodeNumber !== null && item.languageCode && (
            <Link to={`/watch/${id}/${item.episodeNumber}/${item.languageCode}`} className="rounded-full border border-line px-3 py-1 text-xs hover:border-accent hover:text-accent">
              ▶ {languageLabel(item.languageCode)}
            </Link>
          )}
        </li>
      ))}
    </ul>
  )
}

export function Articles({ id }: { id: number }) {
  const articles = useArticles(id)
  if (articles.isLoading) return <Loading />
  if (articles.error) return <ErrorNote error={articles.error} />
  if (!articles.data?.length) return empty('Keine Artikel eingetragen.')

  return (
    <div className="space-y-8">
      {articles.data.map((section) => (
        <section key={section.title}>
          <h3 className="mb-3 text-xs uppercase tracking-wide text-muted">
            {section.title} <span className="tabular-nums">{section.items.length}</span>
          </h3>
          <div className="grid grid-cols-2 gap-x-4 gap-y-5 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
            {section.items.map((item) => (
              <a key={item.id} href={`https://proxer.me/article?id=${item.id}`} target="_blank" rel="noreferrer" className="group block">
                <div
                  className="aspect-[5/7] w-full rounded-xl bg-raised bg-contain bg-center bg-no-repeat ring-1 ring-line transition group-hover:ring-accent"
                  style={item.image ? { backgroundImage: `url(${item.image})` } : undefined}
                />
                <div className="mt-2 line-clamp-2 text-xs leading-snug group-hover:text-accent">{item.title}</div>
              </a>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
