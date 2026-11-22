import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'

// Mirrors backend/Proxer.Api/Proxer/Models.cs
export interface SessionInfo { loggedIn: boolean; username: string | null }
export interface NewsItem { id: string; title: string; text: string; image: string | null; date: string; author: string; category: string | null }
export interface EntrySummary { id: number; title: string; genres: string[]; type: string; status: string; languages: string[]; rating: number | null; votes: number | null; cover: string }
export interface EntryPage { entries: EntrySummary[]; page: number; hasNext: boolean }
export interface SeasonGroup { title: string; entries: EntrySummary[] }
export interface SeasonPage { year: number; season: number; groups: SeasonGroup[] }
export interface CalendarEntry { entryId: number; title: string; cover: string; episode: number | null; language: string | null; airTime: number | null; uploadTime: number | null; genres: string[] }
export interface CalendarDay { date: string; entries: CalendarEntry[] }
export interface Fact { label: string; values: string[] }
export interface Genre { name: string; description: string | null }
export interface EntryDetails { id: number; title: string; type: string; cover: string; rating: number | null; votes: number | null; genres: Genre[]; description: string; facts: Fact[]; tabs: string[]; ongoing: boolean }
export interface EpisodeLanguage { language: string; hosters: string[] }
export interface Episode { number: number; title: string | null; languages: EpisodeLanguage[] }
export interface EpisodePage { category: string; start: number; end: number; languages: string[]; episodes: Episode[]; page: number; pageCount: number }
export interface StreamInfo { id: string; name: string; type: string; hosterType: string; embedUrl: string | null; uploader: string | null; translatorGroup: string | null }
export interface WatchInfo { entryId: number; title: string; episode: number; episodeCount: number | null; language: string; streams: StreamInfo[] }
export interface ListProgress { entryId: number; state: number; episode: number }
export interface MyListItem { id: number; name: string; medium: string; episodes: number; episode: number; state: number; rating: number; cover: string }

export type Category = 'anime' | 'manga'

export class ApiError extends Error {
  status: number
  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, init)
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new ApiError(body?.error ?? `Fehler ${response.status}`, response.status)
  }
  return response.json()
}

const get = <T,>(path: string) => request<T>(path)

const post = <T,>(path: string, body: unknown) =>
  request<T>(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })

const minutes = (n: number) => n * 60_000

export const useSession = () =>
  useQuery({ queryKey: ['session'], queryFn: () => get<SessionInfo>('/session'), staleTime: minutes(10) })

export interface Credentials { username: string; password: string; secretKey: string | null }

/**
 * Switches the app to the new session state and drops everything cached for the previous account.
 * The session query itself must stay in the cache: removing it would detach the mounted app from it.
 */
function applySession(client: QueryClient, session: SessionInfo) {
  client.setQueryData(['session'], session)
  client.removeQueries({ predicate: (query) => query.queryKey[0] !== 'session' })
}

export function useLogin() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (credentials: Credentials) => post<SessionInfo>('/session/login', credentials),
    onSuccess: (session) => applySession(client, session),
  })
}

export function useLogout() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => post<SessionInfo>('/session/logout', {}),
    onSuccess: (session) => applySession(client, session),
  })
}

export const useNews = () =>
  useQuery({ queryKey: ['news'], queryFn: () => get<NewsItem[]>('/news'), staleTime: minutes(5) })

export const useEntries = (category: Category, medium: string, sort: string, page: number) =>
  useQuery({
    queryKey: ['entries', category, medium, sort, page],
    // Provider catalogues ("provider-netflix") live under their own endpoint.
    queryFn: () =>
      medium.startsWith('provider-')
        ? get<EntryPage>(`/providers/${medium.slice('provider-'.length)}?sort=${sort}&page=${page}`)
        : get<EntryPage>(`/entries?category=${category}&medium=${medium}&sort=${sort}&page=${page}`),
    staleTime: minutes(15),
    placeholderData: (previous) => previous,
  })

/** Without year and season the backend answers with the current season. */
export const useSeason = (year: number | null, season: number | null) =>
  useQuery({
    queryKey: ['season', year, season],
    queryFn: () => get<SeasonPage>(year && season ? `/season?year=${year}&season=${season}` : '/season'),
    staleTime: minutes(30),
    placeholderData: (previous) => previous,
  })

export const useCalendar = () =>
  useQuery({ queryKey: ['calendar'], queryFn: () => get<CalendarDay[]>('/calendar'), staleTime: minutes(10), refetchInterval: minutes(10) })

export const useSearch = (query: string) =>
  useQuery({
    queryKey: ['search', query],
    queryFn: () => get<EntryPage>(`/search?q=${encodeURIComponent(query)}`),
    enabled: query.trim().length > 1,
    staleTime: minutes(10),
  })

export const useEntry = (id: number) =>
  useQuery({ queryKey: ['entry', id], queryFn: () => get<EntryDetails>(`/entries/${id}`), staleTime: minutes(60) })

/** Ongoing series are re-checked every 2 minutes, finished ones only once an hour. */
export const useEpisodes = (id: number, page: number, ongoing: boolean) =>
  useQuery({
    queryKey: ['episodes', id, page],
    queryFn: () => get<EpisodePage>(`/entries/${id}/episodes?page=${page}`),
    staleTime: ongoing ? minutes(2) : minutes(60),
    refetchInterval: ongoing ? minutes(2) : false,
    placeholderData: (previous, previousQuery) => (previousQuery?.queryKey[1] === id ? previous : undefined),
  })

/** Fetches details and the shown episode page straight from Proxer, bypassing the caches. */
export function useRefreshEntry(id: number, page: number) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const details = await get<EntryDetails>(`/entries/${id}?refresh=true`)
      const episodes = await get<EpisodePage>(`/entries/${id}/episodes?page=${page}&refresh=true`)
      return { details, episodes }
    },
    onSuccess: ({ details, episodes }) => {
      client.setQueryData(['entry', id], details)
      client.setQueryData(['episodes', id, page], episodes)
      // Other pages of this entry are refetched when they are opened next.
      client.invalidateQueries({ queryKey: ['episodes', id], predicate: (query) => query.queryKey[2] !== page })
    },
  })
}

export const useWatch = (id: number, episode: number, language: string) =>
  useQuery({
    queryKey: ['watch', id, episode, language],
    queryFn: () => get<WatchInfo>(`/watch/${id}/${episode}/${language}`),
    staleTime: minutes(2),
    retry: false,
  })

export const useProgress = () =>
  useQuery({ queryKey: ['progress'], queryFn: () => get<ListProgress[]>('/me/progress'), staleTime: minutes(1) })

export const useMyList = (category: Category) =>
  useQuery({ queryKey: ['mylist', category], queryFn: () => get<MyListItem[]>(`/me/list/${category}`), staleTime: minutes(2) })

const languageLabels: Record<string, string> = {
  gersub: 'GerSub', gerdub: 'GerDub', engsub: 'EngSub', engdub: 'EngDub', de: 'Deutsch', en: 'Englisch',
}
export const languageLabel = (language: string) => languageLabels[language] ?? language

/** Proxer marks blocked languages with a pseudo hoster named "message". */
export const isPlayable = (language: EpisodeLanguage) => language.hosters.some((h) => h !== 'message')
