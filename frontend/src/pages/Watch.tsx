import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { languageLabel, useBookmark, useProgress, useSetProgress, useWatch, type StreamInfo } from '../api'
import { Chip, ErrorNote, Loading } from '../components'

/** Proxer's own player first, then anything else that can be embedded. */
function preferredStream(streams: StreamInfo[]) {
  const embeddable = streams.filter((stream) => stream.embedUrl)
  return embeddable.find((stream) => stream.type.startsWith('proxer') || stream.type.startsWith('ps')) ?? embeddable[0] ?? null
}

export default function Watch() {
  const params = useParams()
  const id = Number(params.id)
  const episode = Number(params.episode)
  const language = params.language ?? ''
  const watch = useWatch(id, episode, language)
  const [streamId, setStreamId] = useState<string | null>(null)
  const progress = useProgress()
  const setProgress = useSetProgress(id, 'anime')
  const bookmark = useBookmark(id, episode, language, 'anime')
  const watched = (progress.data?.find((p) => p.entryId === id)?.episode ?? 0) >= episode

  useEffect(() => setStreamId(null), [id, episode, language])

  const data = watch.data
  const active = data ? (data.streams.find((stream) => stream.id === streamId) ?? preferredStream(data.streams)) : null
  const hasNext = data ? data.episodeCount === null || episode < data.episodeCount : false
  const navButton = 'rounded-full border border-line px-4 py-1.5 text-sm hover:border-accent hover:text-accent'
  const proxerUrl = `https://proxer.me/watch/${id}/${episode}/${language}`

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Link to={`/eintrag/${id}`} className="text-sm text-muted hover:text-accent">
          ← {data?.title ?? 'Zurück'}
        </Link>
        <h1 className="text-lg font-semibold">
          Episode {episode}
          {data?.episodeCount ? <span className="text-muted"> / {data.episodeCount}</span> : null}
          <span className="ml-2 text-sm font-normal text-muted">{languageLabel(language)}</span>
        </h1>
      </div>

      <div className="aspect-video overflow-hidden rounded-xl bg-black ring-1 ring-line">
        {watch.isLoading && (
          <div className="flex h-full items-center justify-center">
            <Loading label="Stream wird geladen…" />
          </div>
        )}
        {active?.embedUrl && (
          <iframe
            key={active.id}
            src={active.embedUrl}
            title={`${data?.title} Episode ${episode}`}
            allow="autoplay; fullscreen; encrypted-media; picture-in-picture"
            allowFullScreen
            className="size-full border-0"
          />
        )}
        {data && !active && (
          <div className="flex h-full flex-col items-center justify-center gap-3 px-6 text-center text-muted">
            <p>Für diese Episode gibt es keinen Stream, der sich hier einbetten lässt.</p>
            <a href={proxerUrl} target="_blank" rel="noreferrer" className={navButton}>
              Auf Proxer öffnen ↗
            </a>
          </div>
        )}
      </div>

      {watch.error && (
        <div className="mt-4">
          <ErrorNote error={watch.error} />
        </div>
      )}

      <div className="mt-4 flex flex-wrap items-center gap-3">
        {episode > 1 && (
          <Link to={`/watch/${id}/${episode - 1}/${language}`} className={navButton}>
            ← Episode {episode - 1}
          </Link>
        )}
        {hasNext && (
          <Link to={`/watch/${id}/${episode + 1}/${language}`} className={navButton}>
            Episode {episode + 1} →
          </Link>
        )}
        {active && (
          <span className="ml-auto text-sm text-muted">
            {active.translatorGroup && `Übersetzt von ${active.translatorGroup} · `}
            {active.uploader && `Hochgeladen von ${active.uploader}`}
          </span>
        )}
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        <button
          type="button"
          className={`${navButton} ${watched ? 'border-good/60 text-good' : ''} disabled:opacity-50`}
          disabled={setProgress.isPending || watched}
          onClick={() => setProgress.mutate(episode)}
        >
          {watched ? '✓ Gesehen' : '✓ Als gesehen markieren'}
        </button>
        <button type="button" className={`${navButton} disabled:opacity-50`} disabled={bookmark.isPending} onClick={() => bookmark.mutate('this')}>
          Lesezeichen: diese Episode
        </button>
        {hasNext && (
          <button type="button" className={`${navButton} disabled:opacity-50`} disabled={bookmark.isPending} onClick={() => bookmark.mutate('next')}>
            Lesezeichen: nächste Episode
          </button>
        )}
      </div>

      {data && data.streams.length > 1 && (
        <div className="mt-5">
          <div className="mb-2 text-xs uppercase tracking-wide text-muted">Hoster</div>
          <div className="flex flex-wrap gap-2">
            {data.streams.map((stream) =>
              stream.embedUrl ? (
                <Chip key={stream.id} active={stream.id === active?.id} onClick={() => setStreamId(stream.id)}>
                  {stream.name}
                </Chip>
              ) : (
                <a key={stream.id} href={proxerUrl} target="_blank" rel="noreferrer" className="rounded-full border border-line px-3 py-1 text-sm text-muted hover:text-ink">
                  {stream.name} ↗
                </a>
              ),
            )}
          </div>
        </div>
      )}
    </div>
  )
}
