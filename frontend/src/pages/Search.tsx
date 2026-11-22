import { useSearchParams } from 'react-router-dom'
import { useSearch } from '../api'
import { EntryGrid, ErrorNote, Loading, PageTitle } from '../components'

export default function Search() {
  const [params] = useSearchParams()
  const query = params.get('q') ?? ''
  const results = useSearch(query)

  return (
    <div>
      <PageTitle>Suche: „{query}“</PageTitle>
      {results.isLoading && <Loading />}
      {results.error && <ErrorNote error={results.error} />}
      {results.data &&
        (results.data.entries.length > 0 ? (
          <EntryGrid entries={results.data.entries} />
        ) : (
          <p className="text-muted">Keine Treffer.</p>
        ))}
    </div>
  )
}
