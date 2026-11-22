import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { isPlayable, languageLabel, useEntry, useEpisodes, useProgress, useRefreshEntry } from '../api'
import { Chip, Cover, ErrorNote, Loading, Rating } from '../components'

const hiddenFacts = new Set(['Original Titel'])

function Episodes({ id, ongoing, page, setPage }: { id: number; ongoing: boolean; page: number; setPage: (page: number) => void }) {
  const episodes = useEpisodes(id, page, ongoing)
  const progress = useProgress()
  const watched = progress.data?.find((p) => p.entryId === id)?.episode ?? 0

  if (episodes.isLoading) return <Loading label="Episoden laden…" />
  if (episodes.error) return <ErrorNote error={episodes.error} />
  const data = episodes.data
  if (!data || data.episodes.length === 0) return <p className="text-muted">Keine Episoden oder Kapitel vorhanden.</p>

  const isManga = data.category === 'manga'
  return (
    <div className={episodes.isPlaceholderData ? 'opacity-50' : ''}>
      {data.pageCount > 1 && (
        <div className="mb-4 flex flex-wrap gap-2">
          {Array.from({ length: data.pageCount }, (_, index) => index + 1).map((p) => (
            <Chip key={p} active={p === page} onClick={() => setPage(p)}>
              {(p - 1) * 50 + 1}–{p * 50}
            </Chip>
          ))}
        </div>
      )}
      <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
        {data.episodes.map((episode) => (
          <li key={episode.number} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2.5">
            <span className="w-10 text-right text-sm tabular-nums text-muted">{episode.number}</span>
            <span className="min-w-0 flex-1 truncate text-sm">
              {episode.title ?? `${isManga ? 'Kapitel' : 'Episode'} ${episode.number}`}
            </span>
            {episode.number <= watched && (
              <span className="text-sm text-good" title="Gesehen">
                ✓
              </span>
            )}
            <span className="flex gap-2">
              {episode.languages.map((language) =>
                isManga ? (
                  <a
                    key={language.language}
                    href={`https://proxer.me/chapter/${id}/${episode.number}/${language.language}`}
                    target="_blank"
                    rel="noreferrer"
                    className="rounded-full border border-line px-3 py-1 text-xs hover:border-accent hover:text-accent"
                  >
                    {languageLabel(language.language)} ↗
                  </a>
                ) : isPlayable(language) ? (
                  <Link
                    key={language.language}
                    to={`/watch/${id}/${episode.number}/${language.language}`}
                    className="rounded-full bg-accent-soft px-3 py-1 text-xs font-medium text-accent hover:bg-accent hover:text-white"
                  >
                    ▶ {languageLabel(language.language)}
                  </Link>
                ) : (
                  <span
                    key={language.language}
                    title="In dieser Sprache nicht verfügbar"
                    className="rounded-full border border-line px-3 py-1 text-xs text-muted line-through"
                  >
                    {languageLabel(language.language)}
                  </span>
                ),
              )}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

export default function Entry() {
  const id = Number(useParams().id)
  const entry = useEntry(id)
  const [page, setPage] = useState(1)
  const refresh = useRefreshEntry(id, page)

  useEffect(() => setPage(1), [id])

  if (entry.isLoading) return <Loading />
  if (entry.error) return <ErrorNote error={entry.error} />
  const data = entry.data
  if (!data) return null

  return (
    <div className="space-y-10">
      <section className="grid gap-6 md:grid-cols-[220px_1fr]">
        <div className="mx-auto w-44 md:w-full">
          <Cover src={data.cover} alt={`Cover von ${data.title}`} className="rounded-xl ring-1 ring-line" />
        </div>
        <div className="min-w-0">
          <div className="text-sm text-muted">{data.type}</div>
          <h1 className="mt-1 text-3xl font-bold tracking-tight">{data.title}</h1>
          <div className="mt-2 flex items-center gap-3 text-sm">
            <Rating value={data.rating} />
            {data.votes !== null && <span className="text-muted">{data.votes.toLocaleString('de-DE')} Stimmen</span>}
            <a href={`https://proxer.me/info/${data.id}`} target="_blank" rel="noreferrer" className="ml-auto text-muted hover:text-accent">
              Auf Proxer öffnen ↗
            </a>
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            {data.genres.map((genre) => (
              <span key={genre.name} title={genre.description ?? undefined} className="rounded-full bg-raised px-3 py-1 text-xs">
                {genre.name}
              </span>
            ))}
          </div>
          <p className="mt-5 max-w-3xl whitespace-pre-line leading-relaxed text-ink/90">{data.description}</p>
          <dl className="mt-6 grid gap-x-8 gap-y-3 text-sm sm:grid-cols-2">
            {data.facts
              .filter((fact) => !hiddenFacts.has(fact.label))
              .map((fact) => (
                <div key={fact.label} className="min-w-0">
                  <dt className="text-xs uppercase tracking-wide text-muted">{fact.label}</dt>
                  <dd className="mt-0.5">{fact.values.join(' · ')}</dd>
                </div>
              ))}
          </dl>
        </div>
      </section>

      {data.tabs.includes('list') && (
        <section>
          <div className="mb-4 flex flex-wrap items-center gap-3">
            <h2 className="text-xl font-bold tracking-tight">Episoden</h2>
            {data.ongoing && <span className="rounded-md bg-good px-1.5 py-0.5 text-xs font-semibold text-bg">läuft</span>}
            <button
              type="button"
              onClick={() => refresh.mutate()}
              disabled={refresh.isPending}
              title="Holt Details und Episoden frisch von Proxer"
              className="ml-auto rounded-full border border-line px-4 py-1.5 text-sm enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-50"
            >
              {refresh.isPending ? 'Aktualisiert…' : '↻ Aktualisieren'}
            </button>
          </div>
          {refresh.error && (
            <div className="mb-4">
              <ErrorNote error={refresh.error} />
            </div>
          )}
          <Episodes id={data.id} ongoing={data.ongoing} page={page} setPage={setPage} />
        </section>
      )}
    </div>
  )
}
