import { Link } from 'react-router-dom'
import { useEntries, useNews, useUpdates } from '../api'
import { Cover, EntryCard, ErrorNote, Loading, PageTitle } from '../components'

export default function Home() {
  const news = useNews()
  const top = useEntries('anime', 'animeseries', 'clicks', 1)
  const updates = useUpdates('anime')
  // One card per entry, newest upload first.
  const latest = updates.data?.filter((item, index, all) => all.findIndex((other) => other.entryId === item.entryId) === index).slice(0, 14)

  return (
    <div className="space-y-10">
      <section>
        <PageTitle>Beliebte Anime</PageTitle>
        {top.isLoading && <Loading />}
        {top.error && <ErrorNote error={top.error} />}
        {top.data && (
          <div className="flex gap-4 overflow-x-auto pb-2">
            {top.data.entries.slice(0, 14).map((entry) => (
              <div key={entry.id} className="w-36 shrink-0 sm:w-40">
                <EntryCard entry={entry} />
              </div>
            ))}
          </div>
        )}
      </section>

      <section>
        <PageTitle>Neu hochgeladen</PageTitle>
        {updates.isLoading && <Loading />}
        {updates.error && <ErrorNote error={updates.error} />}
        <div className="flex gap-4 overflow-x-auto pb-2">
          {latest?.map((item) => (
            <Link key={item.entryId} to={`/eintrag/${item.entryId}`} className="group w-36 shrink-0 sm:w-40">
              <div className="overflow-hidden rounded-xl ring-1 ring-line transition group-hover:ring-accent">
                <Cover src={item.cover} alt="" />
              </div>
              <div className="mt-2 line-clamp-2 text-sm font-medium leading-snug group-hover:text-accent">{item.title}</div>
              <div className="mt-0.5 truncate text-xs text-muted">
                {item.language} · {item.date}
              </div>
            </Link>
          ))}
        </div>
      </section>

      <section>
        <PageTitle>News</PageTitle>
        {news.isLoading && <Loading />}
        {news.error && <ErrorNote error={news.error} />}
        <div className="grid gap-4 md:grid-cols-2">
          {news.data?.map((item) => (
            // A news item is a topic in Proxer's news forum, so it opens in the app's forum view.
            <Link
              key={item.id}
              to={`/forum/56/${item.id}`}
              className="flex overflow-hidden rounded-xl border border-line bg-surface transition hover:border-accent"
            >
              {item.image && (
                <img src={item.image} alt="" loading="lazy" referrerPolicy="no-referrer" className="w-32 shrink-0 object-cover sm:w-40" />
              )}
              <div className="min-w-0 p-4">
                <div className="text-xs text-muted">
                  {item.date} · {item.author}
                </div>
                <h2 className="mt-1 font-semibold leading-snug">{item.title}</h2>
                <p className="mt-1 line-clamp-2 text-sm text-muted">{item.text}</p>
              </div>
            </Link>
          ))}
        </div>
      </section>
    </div>
  )
}
