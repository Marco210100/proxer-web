import { useEntries, useNews } from '../api'
import { EntryCard, ErrorNote, Loading, PageTitle } from '../components'

export default function Home() {
  const news = useNews()
  const top = useEntries('anime', 'animeseries', 'clicks', 1)

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
        <PageTitle>News</PageTitle>
        {news.isLoading && <Loading />}
        {news.error && <ErrorNote error={news.error} />}
        <div className="grid gap-4 md:grid-cols-2">
          {news.data?.map((item) => (
            <a
              key={item.id}
              href={`https://proxer.me/news/${item.id}`}
              target="_blank"
              rel="noreferrer"
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
            </a>
          ))}
        </div>
      </section>
    </div>
  )
}
