import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Comments, Music, People, Recommendations, Relations } from './EntryTabs'
import { isPlayable, languageLabel, useEntry, useEpisodes, useListAction, useProgress, useRefreshEntry, useSetProgress, useSubscribe } from '../api'
import { Chip, Cover, ErrorNote, Loading, Rating } from '../components'

// Companies and groups are shown as links below the facts instead.
const hiddenFacts = new Set(['Original Titel', 'Industrie', 'Gruppen'])

const tabLabels = [
  { key: 'list', label: 'Episoden' },
  { key: 'relation', label: 'Verbindungen' },
  { key: 'recommendations', label: 'Empfehlungen' },
  { key: 'comments', label: 'Kommentare' },
  { key: 'music', label: 'Musik' },
]

function Episodes({ id, ongoing, page, setPage }: { id: number; ongoing: boolean; page: number; setPage: (page: number) => void }) {
  const episodes = useEpisodes(id, page, ongoing)
  const progress = useProgress()
  const watched = progress.data?.find((p) => p.entryId === id)?.episode ?? 0
  const setProgress = useSetProgress(id, episodes.data?.category ?? 'anime')

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
            <button
              type="button"
              disabled={setProgress.isPending}
              // Clicking the last watched episode takes the mark back by one.
              onClick={() => setProgress.mutate(episode.number === watched ? episode.number - 1 : episode.number)}
              title={episode.number <= watched ? 'Gesehen – klicken, um den Stand hierher zu setzen' : 'Bis hierher als gesehen markieren'}
              aria-label={`${isManga ? 'Kapitel' : 'Episode'} ${episode.number} als Stand setzen`}
              aria-pressed={episode.number <= watched}
              className={`size-7 rounded-full border text-sm transition-colors disabled:opacity-50 ${
                episode.number <= watched ? 'border-good/60 text-good' : 'border-line text-line hover:border-good hover:text-good'
              }`}
            >
              ✓
            </button>
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

function ListActions({ id }: { id: number }) {
  const progress = useProgress()
  const action = useListAction(id)
  const subscribe = useSubscribe(id)
  const mine = progress.data?.find((p) => p.entryId === id)
  const button =
    'rounded-full border border-line px-3 py-1.5 text-sm transition-colors enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-50'

  return (
    <div className="mt-4 flex flex-wrap items-center gap-2">
      <button type="button" className={button} disabled={action.isPending} onClick={() => action.mutate('note')}>
        + Vermerken
      </button>
      <button type="button" className={button} disabled={action.isPending} onClick={() => action.mutate('finish')}>
        ✓ Abgeschlossen
      </button>
      <button type="button" className={button} disabled={action.isPending} onClick={() => action.mutate('favor')}>
        ♥ Favorisieren
      </button>
      <button
        type="button"
        className={`${button} ${mine?.subscribed ? 'border-accent text-accent' : ''}`}
        disabled={subscribe.isPending}
        aria-pressed={mine?.subscribed ?? false}
        onClick={() => subscribe.mutate(!mine?.subscribed)}
        title="Benachrichtigt dich über neue Staffeln und News zu diesem Werk"
      >
        {mine?.subscribed ? '🔔 Abonniert' : '🔔 Abonnieren'}
      </button>
      {mine && <span className="text-sm text-muted">Auf deiner Liste: {listStates[mine.state] ?? 'eingetragen'}</span>}
    </div>
  )
}

const listStates = ['Abgeschlossen', 'Am Schauen', 'Geplant', 'Abgebrochen']

export default function Entry() {
  const id = Number(useParams().id)
  const entry = useEntry(id)
  const [params, setParams] = useSearchParams()
  const [page, setPage] = useState(1)
  const refresh = useRefreshEntry(id, page)

  useEffect(() => setPage(1), [id])

  // Only offer the tabs Proxer has for this entry, in a fixed order.
  const tabs = tabLabels.filter((t) => entry.data?.tabs.includes(t.key))
  const tab = tabs.find((t) => t.key === params.get('tab'))?.key ?? tabs[0]?.key

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
          <ListActions id={data.id} />
          <div className="mt-4 flex flex-wrap gap-2">
            {data.genres.map((genre) => (
              <span key={genre.name} title={genre.description ?? undefined} className="rounded-full bg-raised px-3 py-1 text-xs">
                {genre.name}
              </span>
            ))}
          </div>
          {data.tags.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-2">
              {data.tags.map((tag) => (
                <span key={tag.name} title={tag.description ?? undefined} className="rounded-full border border-line px-3 py-1 text-xs text-muted">
                  {tag.name}
                </span>
              ))}
            </div>
          )}
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
          {data.links.length > 0 && (
            <div className="mt-5">
              <div className="mb-2 text-xs uppercase tracking-wide text-muted">Unternehmen und Gruppen</div>
              <div className="flex flex-wrap gap-2">
                {data.links.map((link) => (
                  <Link
                    key={`${link.kind}-${link.id}`}
                    to={`/seite/${link.kind}/${link.id}`}
                    className="rounded-full border border-line px-3 py-1 text-xs hover:border-accent hover:text-accent"
                  >
                    {link.name}
                    {link.role && <span className="ml-1.5 text-muted">{link.role}</span>}
                  </Link>
                ))}
              </div>
            </div>
          )}
        </div>
      </section>

      {(data.persons.length > 0 || data.characters.length > 0) && (
        <section className="grid gap-6 lg:grid-cols-2">
          <People title="Charaktere" people={data.characters} kind="character" />
          <People title="Personen" people={data.persons} kind="persons" />
        </section>
      )}

      <section>
        <div className="mb-5 flex flex-wrap items-center gap-2 border-b border-line pb-3">
          {tabs.map((t) => (
            <Chip key={t.key} active={t.key === tab} onClick={() => setParams(t.key === tabs[0].key ? {} : { tab: t.key }, { replace: true })}>
              {t.label}
            </Chip>
          ))}
          {tab === 'list' && (
            <>
              {data.ongoing && <span className="ml-2 rounded-md bg-good px-1.5 py-0.5 text-xs font-semibold text-bg">läuft</span>}
              <button
                type="button"
                onClick={() => refresh.mutate()}
                disabled={refresh.isPending}
                title="Holt Details und Episoden frisch von Proxer"
                className="ml-auto rounded-full border border-line px-4 py-1.5 text-sm enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-50"
              >
                {refresh.isPending ? 'Aktualisiert…' : '↻ Aktualisieren'}
              </button>
            </>
          )}
        </div>
        {refresh.error && (
          <div className="mb-4">
            <ErrorNote error={refresh.error} />
          </div>
        )}
        {tab === 'list' && <Episodes id={data.id} ongoing={data.ongoing} page={page} setPage={setPage} />}
        {tab === 'relation' && <Relations id={data.id} />}
        {tab === 'recommendations' && <Recommendations id={data.id} />}
        {tab === 'comments' && <Comments key={data.id} id={data.id} />}
        {tab === 'music' && <Music id={data.id} />}
      </section>
    </div>
  )
}
