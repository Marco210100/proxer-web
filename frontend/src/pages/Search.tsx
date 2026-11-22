import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { isSearchable, useSearch, useSearchGenres, type SearchFilters } from '../api'
import { EntryGrid, ErrorNote, Loading, PageTitle } from '../components'

// Values as expected by Proxer's extended search.
const types = [
  ['all', 'Alles'], ['all-anime', 'Alle Anime'], ['animeseries', 'Anime-Serie'], ['movie', 'Movie'], ['ova', 'Special'],
  ['all-manga', 'Alle Manga'], ['mangaseries', 'Manga-Serie'], ['oneshot', 'One-Shot'], ['doujin', 'Doujinshi'],
  ['all-novel', 'Alle Novels'], ['lightnovel', 'Light Novel'], ['all18', 'Alles inklusive 18+'], ['hentai', 'Hentai'], ['hmanga', 'H-Manga'],
]
const languages = [['alle', 'Alle Sprachen'], ['de', 'Deutsch'], ['en', 'Englisch']]
const statuses = [['all', 'Jeder Status'], ['0', 'Nicht erschienen'], ['1', 'Abgeschlossen'], ['2', 'Am Laufen'], ['3', 'Abgebrochen']]
const sorts = [['relevance', 'Relevanz'], ['name', 'Name'], ['rating', 'Bewertung'], ['clicks', 'Zugriffe'], ['count', 'Anzahl Episoden']]

const ids = (value: string | null) => (value ? value.split(',').map(Number).filter((n) => Number.isInteger(n) && n > 0) : [])
const pick = (options: string[][], value: string | null) => options.find(([key]) => key === value)?.[0] ?? options[0][0]

export default function Search() {
  const [params, setParams] = useSearchParams()
  const [showGenres, setShowGenres] = useState(false)
  const genres = useSearchGenres()

  // The address bar holds the whole search, so results can be bookmarked and shared.
  const filters: SearchFilters = {
    q: params.get('q') ?? '',
    type: pick(types, params.get('typ')),
    language: pick(languages, params.get('sprache')),
    status: pick(statuses, params.get('status')),
    sort: pick(sorts, params.get('sortierung')),
    genres: ids(params.get('genres')),
    excludeGenres: ids(params.get('ohne')),
  }
  const results = useSearch(filters)

  const update = (changes: Record<string, string>) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) value ? next.set(key, value) : next.delete(key)
    setParams(next, { replace: true })
  }

  // One click includes a genre, a second excludes it, a third clears it.
  const cycleGenre = (id: number) => {
    const included = filters.genres.includes(id)
    const excluded = filters.excludeGenres.includes(id)
    update({
      genres: (included ? filters.genres.filter((g) => g !== id) : excluded ? filters.genres : [...filters.genres, id]).join(','),
      ohne: (included ? [...filters.excludeGenres, id] : filters.excludeGenres.filter((g) => g !== id)).join(','),
    })
  }

  const select = (label: string, key: string, value: string, options: string[][]) => (
    <select
      value={value}
      onChange={(event) => update({ [key]: event.target.value })}
      aria-label={label}
      className="rounded-full border border-line bg-surface px-3 py-1.5 text-sm outline-none focus:border-accent"
    >
      {options.map(([optionValue, optionLabel]) => (
        <option key={optionValue} value={optionValue}>
          {optionLabel}
        </option>
      ))}
    </select>
  )
  const chosen = filters.genres.length + filters.excludeGenres.length

  return (
    <div>
      <PageTitle>Suche{filters.q && `: „${filters.q}“`}</PageTitle>

      <div className="mb-3 flex flex-wrap items-center gap-2">
        {select('Typ', 'typ', filters.type, types)}
        {select('Sprache', 'sprache', filters.language, languages)}
        {select('Status', 'status', filters.status, statuses)}
        {select('Sortierung', 'sortierung', filters.sort, sorts)}
        <button
          type="button"
          onClick={() => setShowGenres(!showGenres)}
          aria-expanded={showGenres}
          className={`rounded-full border px-3 py-1.5 text-sm ${chosen > 0 ? 'border-accent text-accent' : 'border-line text-muted hover:text-ink'}`}
        >
          Genres{chosen > 0 && ` (${chosen})`} {showGenres ? '▴' : '▾'}
        </button>
      </div>

      {showGenres && (
        <div className="mb-4 rounded-xl border border-line bg-surface p-4">
          <p className="mb-3 text-xs text-muted">Einmal klicken: muss enthalten sein. Zweimal: darf nicht enthalten sein. Dreimal: egal.</p>
          {genres.isLoading && <Loading />}
          {genres.error && <ErrorNote error={genres.error} />}
          <div className="flex flex-wrap gap-2">
            {genres.data?.map((genre) => {
              const included = filters.genres.includes(genre.id)
              const excluded = filters.excludeGenres.includes(genre.id)
              return (
                <button
                  key={genre.id}
                  type="button"
                  onClick={() => cycleGenre(genre.id)}
                  title={genre.description ?? undefined}
                  aria-label={`${genre.name}: ${included ? 'enthalten' : excluded ? 'ausgeschlossen' : 'egal'}`}
                  className={`rounded-full border px-3 py-1 text-xs transition-colors ${
                    included ? 'border-good/60 text-good' : excluded ? 'border-accent text-accent line-through' : 'border-line text-muted hover:text-ink'
                  }`}
                >
                  {included && '+ '}
                  {excluded && '− '}
                  {genre.name}
                </button>
              )
            })}
          </div>
        </div>
      )}

      {!isSearchable(filters) && <p className="text-muted">Gib oben einen Suchbegriff ein oder wähle mindestens einen Filter.</p>}
      {results.isLoading && <Loading />}
      {results.error && <ErrorNote error={results.error} />}
      {isSearchable(filters) && results.data && (
        <div className={results.isPlaceholderData ? 'opacity-50 transition-opacity' : ''}>
          <p className="mb-4 text-sm text-muted">{results.data.entries.length} Treffer</p>
          {results.data.entries.length > 0 ? <EntryGrid entries={results.data.entries} /> : <p className="text-muted">Keine Treffer.</p>}
        </div>
      )}
    </div>
  )
}
