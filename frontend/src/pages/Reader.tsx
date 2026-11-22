import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError, languageLabel, useBookmark, useChapter, useChapterCheck, useProgress, useSetProgress } from '../api'
import { ErrorNote, Loading } from '../components'

const button = 'rounded-full border border-line px-4 py-1.5 text-sm transition-colors enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-50'

/** Shown when Proxer wants its bot check: the person passes it themselves in a window that uses the app's session. */
function BotCheck({ id, chapter, language, onRetry, retrying }: { id: number; chapter: number; language: string; onRetry: () => void; retrying: boolean }) {
  const check = useChapterCheck()
  const opened = check.isSuccess && check.data.ok

  return (
    <div className="mx-auto max-w-xl rounded-xl border border-line bg-surface p-5">
      <h2 className="text-lg font-semibold">Proxer möchte eine Bot-Prüfung</h2>
      <p className="mt-2 text-sm leading-relaxed text-muted">
        Vor dem Lesen von Kapiteln prüft Proxer, ob ein Mensch dahintersitzt. Die App öffnet dafür ein Browserfenster mit ihrer
        eigenen Sitzung. Bestehst du die Prüfung dort, kann die App die Kapitel danach selbst laden.
      </p>
      <ol className="mt-4 list-decimal space-y-1 pl-5 text-sm">
        <li>Fenster öffnen.</li>
        <li>Warten, bis die Prüfung „Success!“ zeigt; falls ein Kästchen erscheint, anklicken.</li>
        <li>Fenster schließen und hier „Kapitel laden“ wählen.</li>
      </ol>
      <div className="mt-5 flex flex-wrap gap-2">
        <button type="button" className={button} disabled={check.isPending} onClick={() => check.mutate({ entryId: id, chapter, language })}>
          {check.isPending ? 'Öffnet…' : opened ? 'Fenster erneut öffnen' : 'Fenster für die Prüfung öffnen'}
        </button>
        <button
          type="button"
          disabled={retrying}
          onClick={onRetry}
          className="rounded-full bg-accent px-4 py-1.5 text-sm font-semibold text-white enabled:hover:brightness-110 disabled:opacity-50"
        >
          {retrying ? 'Lädt…' : 'Kapitel laden'}
        </button>
      </div>
      {opened && <p className="mt-3 text-sm text-good">Das Fenster ist offen. Es kann einen Moment dauern, bis es erscheint.</p>}
      {check.error && (
        <div className="mt-3">
          <ErrorNote error={check.error} />
        </div>
      )}
    </div>
  )
}

export default function Reader() {
  const params = useParams()
  const navigate = useNavigate()
  const id = Number(params.id)
  const chapterNumber = Number(params.chapter)
  const language = params.language ?? ''
  const chapter = useChapter(id, chapterNumber, language)
  const progress = useProgress()
  const setProgress = useSetProgress(id, 'manga')
  const bookmark = useBookmark(id, chapterNumber, language, 'manga')
  const [loaded, setLoaded] = useState(0)
  const read = (progress.data?.find((p) => p.entryId === id)?.episode ?? 0) >= chapterNumber

  const data = chapter.data
  const next = data?.nextChapter ?? null
  const previous = chapterNumber > 1 ? chapterNumber - 1 : null
  const go = (target: number) => navigate(`/lesen/${id}/${target}/${language}`)

  useEffect(() => {
    setLoaded(0)
    window.scrollTo({ top: 0 })
  }, [id, chapterNumber, language])

  // Left and right arrow keys switch chapters, as on Proxer.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement) return
      if (event.key === 'ArrowRight' && next) navigate(`/lesen/${id}/${next}/${language}`)
      if (event.key === 'ArrowLeft' && previous) navigate(`/lesen/${id}/${previous}/${language}`)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [id, language, next, previous, navigate])

  const botCheck = chapter.error instanceof ApiError && chapter.error.status === 428

  const navigation = (
    <div className="flex flex-wrap items-center justify-center gap-2">
      {previous && (
        <button type="button" className={button} onClick={() => go(previous)}>
          ← Kapitel {previous}
        </button>
      )}
      <button
        type="button"
        className={`${button} ${read ? 'border-good/60 text-good' : ''}`}
        disabled={setProgress.isPending || read}
        onClick={() => setProgress.mutate(chapterNumber)}
      >
        {read ? '✓ Gelesen' : '✓ Als gelesen markieren'}
      </button>
      <button type="button" className={button} disabled={bookmark.isPending} onClick={() => bookmark.mutate('this')}>
        Lesezeichen
      </button>
      {next && (
        <button type="button" className={button} onClick={() => go(next)}>
          Kapitel {next} →
        </button>
      )}
    </div>
  )

  return (
    <div className="mx-auto max-w-4xl">
      <div className="mb-4 flex flex-wrap items-center gap-x-3 gap-y-1">
        <Link to={`/eintrag/${id}`} className="text-sm text-muted hover:text-accent">
          ← {data?.entryTitle || 'Zurück'}
        </Link>
        <h1 className="text-lg font-semibold">
          Kapitel {chapterNumber}
          {data?.chapterCount ? <span className="text-muted"> / {data.chapterCount}</span> : null}
          <span className="ml-2 text-sm font-normal text-muted">{languageLabel(language)}</span>
        </h1>
        {data?.title && <span className="w-full truncate text-sm text-muted">{data.title}</span>}
      </div>

      {chapter.isLoading && <Loading label="Kapitel wird geladen…" />}
      {botCheck && <BotCheck id={id} chapter={chapterNumber} language={language} onRetry={() => chapter.refetch()} retrying={chapter.isFetching} />}
      {chapter.error && !botCheck && <ErrorNote error={chapter.error} />}

      {data && (
        <>
          {navigation}
          <p className="mt-3 text-center text-xs tabular-nums text-muted">
            {Math.min(loaded, data.pages.length)} von {data.pages.length} Seiten geladen
          </p>
          <div className="mt-4 space-y-1">
            {data.pages.map((page, index) => (
              <img
                key={page.url}
                src={page.url}
                alt={`Seite ${index + 1}`}
                loading={index < 3 ? 'eager' : 'lazy'}
                referrerPolicy="no-referrer"
                onLoad={() => setLoaded((n) => n + 1)}
                style={page.width && page.height ? { aspectRatio: `${page.width} / ${page.height}` } : undefined}
                className="mx-auto block w-full max-w-[900px] bg-raised"
              />
            ))}
          </div>
          <div className="mt-6">{navigation}</div>
        </>
      )}
    </div>
  )
}
