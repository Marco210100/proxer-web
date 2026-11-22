import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useOverview } from '../api'
import { Chip, ErrorNote, Loading, PageTitle } from '../components'

const kinds = [
  { key: 'character', label: 'Charaktere' },
  { key: 'persons', label: 'Personen' },
  { key: 'industry', label: 'Unternehmen' },
  { key: 'translatorgroups', label: 'Sub-/Scanlationgruppen' },
]
const letters = ['0-9', ...'ABCDEFGHIJKLMNOPQRSTUVWXYZ']

/** Browse characters, persons, companies and groups: the newest ones or by first letter. */
export default function Overview() {
  const kind = useParams().kind ?? 'character'
  const [params, setParams] = useSearchParams()
  const letter = letters.find((l) => l === params.get('buchstabe')) ?? null
  const overview = useOverview(kind, letter)

  return (
    <div>
      <PageTitle>{kinds.find((k) => k.key === kind)?.label ?? 'Übersicht'}</PageTitle>
      <div className="mb-4 flex flex-wrap gap-2">
        {kinds.map((k) => (
          <Link
            key={k.key}
            to={`/uebersicht/${k.key}`}
            className={`rounded-full border px-3 py-1 text-sm transition-colors ${
              k.key === kind ? 'border-accent bg-accent-soft text-accent' : 'border-line text-muted hover:text-ink'
            }`}
          >
            {k.label}
          </Link>
        ))}
      </div>
      <div className="mb-6 flex flex-wrap gap-1.5">
        <Chip active={letter === null} onClick={() => setParams({})}>Neueste</Chip>
        {letters.map((l) => (
          <Chip key={l} active={l === letter} onClick={() => setParams({ buchstabe: l })}>
            {l}
          </Chip>
        ))}
      </div>

      {overview.isLoading && <Loading />}
      {overview.error && <ErrorNote error={overview.error} />}
      {overview.data?.length === 0 && <p className="text-muted">Keine Einträge.</p>}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
        {overview.data?.map((item) => (
          <Link key={item.id} to={`/seite/${kind}/${item.id}`} className="group block text-center">
            <div
              className="mx-auto aspect-square w-full rounded-xl bg-raised bg-cover bg-top ring-1 ring-line transition group-hover:ring-accent"
              style={item.image ? { backgroundImage: `url(${item.image})` } : undefined}
              role="img"
              aria-label={item.name}
            />
            {item.name && <div className="mt-2 line-clamp-2 text-sm font-medium leading-snug group-hover:text-accent">{item.name}</div>}
            {item.works.length > 0 && <div className="mt-0.5 line-clamp-2 text-xs text-muted">{item.works.join(' · ')}</div>}
          </Link>
        ))}
      </div>
    </div>
  )
}
