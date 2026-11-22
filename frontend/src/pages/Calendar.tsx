import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { languageLabel, useCalendar, type CalendarEntry } from '../api'
import { Cover, ErrorNote, Loading, PageTitle } from '../components'

const timeFormat = new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit' })

/** "in 3 Std 12 Min" for upcoming releases, null once the time has passed. */
function countdown(target: number, now: number) {
  const minutes = Math.ceil((target - now) / 60)
  if (minutes <= 0) return null
  if (minutes < 60) return `in ${minutes} Min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `in ${hours} Std ${minutes % 60} Min`
  return `in ${Math.floor(hours / 24)} T ${hours % 24} Std`
}

function Entry({ entry, now }: { entry: CalendarEntry; now: number }) {
  // The upload on Proxer can lag behind the Japanese broadcast.
  const release = entry.uploadTime ?? entry.airTime
  const wait = release ? countdown(release, now) : null
  const released = release !== null && wait === null

  return (
    // The title link is stretched over the whole card; the play button sits above it.
    <div className="group relative flex gap-3 rounded-xl border border-line bg-surface p-2.5 transition hover:border-accent">
      <div className="w-16 shrink-0">
        <Cover src={entry.cover} alt="" className="rounded-md" />
      </div>
      <div className="flex min-w-0 flex-1 flex-col justify-center gap-1">
        <Link to={`/eintrag/${entry.entryId}`} className="truncate font-medium after:absolute after:inset-0 after:rounded-xl group-hover:text-accent">
          {entry.title}
        </Link>
        <div className="text-xs text-muted">
          {entry.episode !== null && `Episode ${entry.episode}`}
          {entry.airTime !== null && ` · ${timeFormat.format(entry.airTime * 1000)} Uhr`}
        </div>
        <div>
          {released && entry.episode !== null && entry.language ? (
            <Link
              to={`/watch/${entry.entryId}/${entry.episode}/${entry.language}`}
              className="relative z-10 inline-block rounded-full bg-accent-soft px-3 py-1 text-xs font-medium text-accent hover:bg-accent hover:text-white"
            >
              ▶ {languageLabel(entry.language)}
            </Link>
          ) : (
            wait && <span className="text-xs tabular-nums text-good">auf Proxer {wait}</span>
          )}
        </div>
      </div>
    </div>
  )
}

export default function Calendar() {
  const calendar = useCalendar()
  const [now, setNow] = useState(() => Date.now() / 1000)

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now() / 1000), 30_000)
    return () => clearInterval(timer)
  }, [])

  return (
    <div>
      <PageTitle>Anime-Kalender</PageTitle>
      {calendar.isLoading && <Loading />}
      {calendar.error && <ErrorNote error={calendar.error} />}
      <div className="space-y-8">
        {calendar.data?.map((day) => (
          <section key={day.date}>
            <h2 className="mb-3 text-lg font-semibold">
              {day.date} <span className="text-sm font-normal text-muted">{day.entries.length}</span>
            </h2>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {day.entries.map((entry) => (
                <Entry key={`${entry.entryId}-${entry.episode}`} entry={entry} now={now} />
              ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  )
}
