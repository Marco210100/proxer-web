import { Link, useParams } from 'react-router-dom'
import { useInfoPage } from '../api'
import { Cover, ErrorNote, Loading } from '../components'

const kindLabels: Record<string, string> = {
  character: 'Charakter',
  persons: 'Person',
  industry: 'Unternehmen',
  translatorgroups: 'Sub-/Scanlationgruppe',
}

// Styles for the whitelisted markup the backend returns.
const prose =
  'break-words leading-relaxed text-ink/90 [&_a]:text-accent [&_a:hover]:underline [&_h3]:mb-1 [&_h3]:mt-5 [&_h3]:text-xs [&_h3]:uppercase ' +
  '[&_h3]:tracking-wide [&_h3]:text-muted [&_img]:my-2 [&_img]:max-w-full [&_img]:rounded-lg [&_ul]:list-disc [&_ul]:pl-5'

/** Detail page of a character, person, company or translator group. */
export default function InfoPageView() {
  const params = useParams()
  const kind = params.kind ?? ''
  const id = Number(params.id)
  const page = useInfoPage(kind, id)

  if (page.isLoading) return <Loading />
  if (page.error) return <ErrorNote error={page.error} />
  const data = page.data
  if (!data) return null

  return (
    <div className="mx-auto max-w-5xl space-y-10">
      <section className="grid gap-6 md:grid-cols-[240px_1fr]">
        <div className="mx-auto w-48 md:w-full">
          {data.image ? (
            <img src={data.image} alt={data.title} referrerPolicy="no-referrer" className="w-full rounded-xl bg-raised ring-1 ring-line" />
          ) : (
            <div className="aspect-square w-full rounded-xl bg-raised ring-1 ring-line" />
          )}
        </div>
        <div className="min-w-0">
          <div className="text-sm text-muted">{kindLabels[kind] ?? ''}</div>
          <h1 className="mt-1 text-3xl font-bold tracking-tight">{data.title}</h1>
          <a href={`https://proxer.me/${kind}?id=${id}`} target="_blank" rel="noreferrer" className="mt-2 inline-block text-sm text-muted hover:text-accent">
            Auf Proxer öffnen ↗
          </a>
          <dl className="mt-5 grid gap-x-8 gap-y-3 text-sm sm:grid-cols-2">
            {data.facts.map((fact, index) => (
              <div key={`${fact.label}-${index}`} className="min-w-0">
                <dt className="text-xs uppercase tracking-wide text-muted">{fact.label}</dt>
                <dd className="mt-0.5 break-words">{fact.values.join(' · ')}</dd>
              </div>
            ))}
          </dl>
          {/* The backend rebuilds this markup from a fixed whitelist, see CommunityParsers.Sanitize. */}
          {data.bodyHtml && <div className={`mt-4 ${prose}`} dangerouslySetInnerHTML={{ __html: data.bodyHtml }} />}
        </div>
      </section>

      {data.people.length > 0 && (
        <section>
          <h2 className="mb-3 text-xl font-bold tracking-tight">Synchronsprecher</h2>
          <div className="flex flex-wrap gap-2">
            {data.people.map((person) => (
              <Link key={person.id} to={`/seite/persons/${person.id}`} className="rounded-full border border-line px-3 py-1.5 text-sm hover:border-accent hover:text-accent">
                {person.name}
                {person.role && <span className="ml-1.5 text-muted">{person.role}</span>}
              </Link>
            ))}
          </div>
        </section>
      )}

      {data.entries.length > 0 && (
        <section>
          <h2 className="mb-4 text-xl font-bold tracking-tight">Werke</h2>
          <div className="grid grid-cols-2 gap-x-4 gap-y-6 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
            {data.entries.map((entry) => (
              <Link key={entry.id} to={`/eintrag/${entry.id}`} className="group block">
                <div className="overflow-hidden rounded-xl ring-1 ring-line transition group-hover:ring-accent">
                  <Cover src={entry.cover} alt="" />
                </div>
                <div className="mt-2 line-clamp-2 text-sm font-medium leading-snug group-hover:text-accent">{entry.title}</div>
                <div className="mt-0.5 truncate text-xs text-muted">{entry.medium}</div>
              </Link>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
