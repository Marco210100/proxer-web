import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { toast } from './toast'

// Mirrors backend/Proxer.Api/Proxer/Models.cs
export interface SessionInfo { loggedIn: boolean; username: string | null; userId: number | null }
export interface NewsItem { id: string; title: string; text: string; image: string | null; date: string; author: string; category: string | null }
export interface EntrySummary { id: number; title: string; genres: string[]; type: string; status: string; languages: string[]; rating: number | null; votes: number | null; cover: string }
export interface EntryPage { entries: EntrySummary[]; page: number; hasNext: boolean }
export interface SeasonGroup { title: string; entries: EntrySummary[] }
export interface SeasonPage { year: number; season: number; groups: SeasonGroup[] }
export interface CalendarEntry { entryId: number; title: string; cover: string; episode: number | null; language: string | null; airTime: number | null; uploadTime: number | null; genres: string[] }
export interface CalendarDay { date: string; entries: CalendarEntry[] }
export interface Fact { label: string; values: string[] }
export interface Genre { name: string; description: string | null }
export interface EntryDetails { id: number; title: string; type: string; cover: string; rating: number | null; votes: number | null; genres: Genre[]; description: string; facts: Fact[]; tabs: string[]; ongoing: boolean; tags: Genre[]; persons: PersonRef[]; characters: PersonRef[]; links: FactLink[] }
export interface PersonRef { id: number; name: string; role: string; image: string | null }
export interface RelatedEntry { entry: EntrySummary; year: string; relation: string }
export interface Recommendation { entry: EntrySummary; up: number; down: number }
export interface CommentPart { spoiler: boolean; text: string }
export interface CommentRating { category: string; stars: number }
export interface Comment { id: string; userId: number | null; username: string; avatar: string | null; content: CommentPart[]; ratings: CommentRating[]; rating: number | null; state: string; likes: number }
export interface CommentPage { comments: Comment[]; page: number; hasNext: boolean }
export interface Song { number: number; title: string; length: string }
export interface Album { name: string; releaseDate: string | null; cover: string | null; songs: Song[] }
export interface EpisodeLanguage { language: string; hosters: string[] }
export interface Episode { number: number; title: string | null; languages: EpisodeLanguage[] }
export interface EpisodePage { category: string; start: number; end: number; languages: string[]; episodes: Episode[]; page: number; pageCount: number }
export interface StreamInfo { id: string; name: string; type: string; hosterType: string; embedUrl: string | null; uploader: string | null; translatorGroup: string | null }
export interface WatchInfo { entryId: number; title: string; episode: number; episodeCount: number | null; language: string; streams: StreamInfo[] }
export interface ListProgress { entryId: number; state: number; episode: number; subscribed: boolean }
export interface Bookmark { id: string; entryId: number; title: string; category: Category; episode: number; language: string; medium: string; available: boolean; cover: string }
export interface ActionResult { ok: boolean; message: string }
export interface MyListItem { id: number; cid: string; name: string; medium: string; episodes: number; episode: number; state: number; rating: number; cover: string }

export interface NotificationCounts { messages: number; requests: number; news: number; other: number }
export interface Notification { id: string; text: string; date: string; link: string | null; external: string | null }
export interface NotificationPage { items: Notification[]; page: number; hasNext: boolean }
export interface Conversation { id: number; name: string; date: string; avatar: string | null; unread: boolean }
export interface Participant { id: number; name: string; avatar: string | null }
export interface Message { id: string; fromId: number; username: string; avatar: string | null; text: string; timestamp: number; system: boolean; mine: boolean }
export interface ConversationPage { id: number; participants: Participant[]; messages: Message[]; page: number; hasMore: boolean }
export interface ForumCategory { id: number; name: string; depth: number }
export interface ForumTopic { id: number; categoryId: number; title: string; excerpt: string | null; replies: number; views: string; author: string; lastPostBy: string; lastPostAge: string; lastAvatar: string | null; sticky: boolean }
export interface ForumTopicPage { categoryId: number; title: string; topics: ForumTopic[]; page: number; pageCount: number }
export interface ForumPost { id: string; userId: number | null; username: string; avatar: string | null; date: string; age: string; html: string }
export interface ForumThread { categoryId: number; topicId: number; title: string; posts: ForumPost[]; page: number; pageCount: number }
export interface UserProfile { id: number; name: string; avatar: string | null; statusText: string | null; facts: Fact[] }

export interface LinkedEntry { id: number; title: string; medium: string; cover: string }
export type PageKind = 'character' | 'persons' | 'industry' | 'translatorgroups'
export interface InfoPage { kind: PageKind; id: number; title: string; image: string | null; facts: Fact[]; bodyHtml: string; entries: LinkedEntry[]; people: PersonRef[] }
export interface FactLink { kind: PageKind; id: number; name: string; role: string }
export interface UpdateItem { entryId: number; title: string; language: string; medium: string; uploader: string; date: string; cover: string }

export interface EpisodeUpdate { uploader: string; uploaderId: number | null; title: string; language: string; date: string; episode: number | null; languageCode: string | null }
export interface Discussion { categoryId: number; topicId: number; title: string; replies: number; views: string; author: string; category: string; lastPost: string }
export interface GalleryImage { id: string; title: string; image: string }
export interface GalleryPage { images: GalleryImage[]; page: number; hasNext: boolean }
export interface HistoryItem { episode: string; language: string; date: string; episodeNumber: number | null; languageCode: string | null }
export interface ArticleItem { id: string; title: string; image: string | null }
export interface ArticleSection { title: string; items: ArticleItem[] }
export interface SearchGenre { id: number; name: string; description: string | null }
export interface OverviewItem { id: number; name: string; image: string | null; works: string[] }
export interface SearchFilters { q: string; type: string; language: string; status: string; sort: string; genres: number[]; excludeGenres: number[] }

export type Category = 'anime' | 'manga' | 'novel'

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

export const useEntries = (category: Category, medium: string, sort: string, page: number, letter = 'all') =>
  useQuery({
    queryKey: ['entries', category, medium, sort, page, letter],
    // Provider catalogues ("provider-netflix") live under their own endpoint.
    queryFn: () =>
      medium.startsWith('provider-')
        ? get<EntryPage>(`/providers/${medium.slice('provider-'.length)}?sort=${sort}&page=${page}&letter=${letter}`)
        : get<EntryPage>(`/entries?category=${category}&medium=${medium}&sort=${sort}&page=${page}&letter=${letter}`),
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

/** A search needs at least a two-letter name or one filter, otherwise Proxer would return its whole catalogue. */
export const isSearchable = (f: SearchFilters) =>
  f.q.trim().length > 1 || f.type !== 'all' || f.language !== 'alle' || f.status !== 'all' || f.genres.length > 0 || f.excludeGenres.length > 0

export const useSearch = (filters: SearchFilters) =>
  useQuery({
    queryKey: ['search', filters],
    queryFn: () => {
      const params = new URLSearchParams({ q: filters.q.trim(), type: filters.type, language: filters.language, status: filters.status, sort: filters.sort })
      if (filters.genres.length) params.set('genres', filters.genres.join(','))
      if (filters.excludeGenres.length) params.set('excludeGenres', filters.excludeGenres.join(','))
      return get<EntryPage>(`/search?${params}`)
    },
    enabled: isSearchable(filters),
    staleTime: minutes(10),
    placeholderData: (previous) => previous,
  })

export const useSearchGenres = () =>
  useQuery({ queryKey: ['search-genres'], queryFn: () => get<SearchGenre[]>('/search/genres'), staleTime: minutes(1440) })

export const useOverview = (kind: string, letter: string | null) =>
  useQuery({ queryKey: ['overview', kind, letter], queryFn: () => get<OverviewItem[]>(`/overview/${kind}${letter ? `?letter=${letter}` : ''}`), staleTime: minutes(120) })

export const useEntryUpdates = (id: number) =>
  useQuery({ queryKey: ['entry-updates', id], queryFn: () => get<EpisodeUpdate[]>(`/entries/${id}/updates`), staleTime: minutes(10) })

export const useDiscussions = (id: number) =>
  useQuery({ queryKey: ['discussions', id], queryFn: () => get<Discussion[]>(`/entries/${id}/discussions`), staleTime: minutes(15) })

export const useGallery = (id: number, page: number) =>
  useQuery({ queryKey: ['gallery', id, page], queryFn: () => get<GalleryPage>(`/entries/${id}/gallery?page=${page}`), staleTime: minutes(60), placeholderData: (previous, previousQuery) => (previousQuery?.queryKey[1] === id ? previous : undefined) })

export const useEntryHistory = (id: number) =>
  useQuery({ queryKey: ['entry-history', id], queryFn: () => get<HistoryItem[]>(`/entries/${id}/history`), staleTime: minutes(2) })

export const useArticles = (id: number) =>
  useQuery({ queryKey: ['articles', id], queryFn: () => get<ArticleSection[]>(`/entries/${id}/articles`), staleTime: minutes(120) })

export const useEntry = (id: number) =>
  useQuery({ queryKey: ['entry', id], queryFn: () => get<EntryDetails>(`/entries/${id}`), staleTime: minutes(60) })

export const useRelations = (id: number) =>
  useQuery({ queryKey: ['relations', id], queryFn: () => get<RelatedEntry[]>(`/entries/${id}/relations`), staleTime: minutes(60) })

export const useRecommendations = (id: number) =>
  useQuery({ queryKey: ['recommendations', id], queryFn: () => get<Recommendation[]>(`/entries/${id}/recommendations`), staleTime: minutes(30) })

export const useComments = (id: number, page: number, sort: 'rating' | 'latest') =>
  useQuery({
    queryKey: ['comments', id, page, sort],
    queryFn: () => get<CommentPage>(`/entries/${id}/comments?page=${page}&sort=${sort}`),
    staleTime: minutes(5),
    placeholderData: (previous, previousQuery) => (previousQuery?.queryKey[1] === id ? previous : undefined),
  })

export const useMusic = (id: number) =>
  useQuery({ queryKey: ['music', id], queryFn: () => get<Album[]>(`/entries/${id}/music`), staleTime: minutes(60) })

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

export const useNotificationCounts = () =>
  useQuery({ queryKey: ['notification-counts'], queryFn: () => get<NotificationCounts>('/notifications/counts'), staleTime: minutes(2), refetchInterval: minutes(5) })

export const useNotifications = (page: number) =>
  useQuery({ queryKey: ['notifications', page], queryFn: () => get<NotificationPage>(`/notifications?page=${page}`), staleTime: minutes(2), placeholderData: (previous) => previous })

export const useConversations = () =>
  useQuery({ queryKey: ['conversations'], queryFn: () => get<Conversation[]>('/messages'), staleTime: minutes(1) })

export const useConversation = (id: number, page: number) =>
  useQuery({ queryKey: ['conversation', id, page], queryFn: () => get<ConversationPage>(`/messages/${id}?page=${page}`), staleTime: minutes(1) })

export const useForumCategories = () =>
  useQuery({ queryKey: ['forum'], queryFn: () => get<ForumCategory[]>('/forum'), staleTime: minutes(360) })

export const useForumTopics = (categoryId: number, page: number) =>
  useQuery({ queryKey: ['forum', categoryId, page], queryFn: () => get<ForumTopicPage>(`/forum/${categoryId}?page=${page}`), staleTime: minutes(5), placeholderData: (previous) => previous })

export const useForumThread = (categoryId: number, topicId: number, page: number) =>
  useQuery({ queryKey: ['forum', categoryId, topicId, page], queryFn: () => get<ForumThread>(`/forum/${categoryId}/${topicId}?page=${page}`), staleTime: minutes(3), placeholderData: (previous) => previous })

export const useProfile = (id: number) =>
  useQuery({ queryKey: ['profile', id], queryFn: () => get<UserProfile>(`/users/${id}`), staleTime: minutes(15) })

export const useInfoPage = (kind: string, id: number) =>
  useQuery({ queryKey: ['page', kind, id], queryFn: () => get<InfoPage>(`/pages/${kind}/${id}`), staleTime: minutes(120) })

export const useUpdates = (category: 'anime' | 'manga') =>
  useQuery({ queryKey: ['updates', category], queryFn: () => get<UpdateItem[]>(`/updates/${category}`), staleTime: minutes(5) })

export const useBookmarks = () =>
  useQuery({ queryKey: ['bookmarks'], queryFn: () => get<Bookmark[]>('/me/bookmarks'), staleTime: minutes(2) })

/**
 * A write action on the Proxer account. Shows Proxer's answer as a toast and reloads everything
 * that mirrors the personal list.
 */
function useAction<T>(run: (input: T) => Promise<ActionResult>, done: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: (result) => {
      toast(result.ok ? result.message || done : result.message || 'Die Aktion ist fehlgeschlagen.', result.ok)
      for (const key of ['mylist', 'progress', 'bookmarks', 'notifications', 'notification-counts', 'conversations', 'conversation'])
        client.invalidateQueries({ queryKey: [key] })
    },
    onError: (error) => toast(error instanceof Error ? error.message : 'Die Aktion ist fehlgeschlagen.', false),
  })
}

export type ListAction = 'note' | 'favor' | 'finish'

export const useListAction = (id: number) =>
  useAction((action: ListAction) => post<ActionResult>(`/entries/${id}/list-action`, { action }), 'Gespeichert.')

export const useSubscribe = (id: number) =>
  useAction((subscribe: boolean) => post<ActionResult>(`/entries/${id}/subscribe`, { subscribe }), 'Gespeichert.')

/** Sets how far an entry has been watched or read; adds it to the list when needed. */
export const useSetProgress = (id: number, category: string) =>
  useAction((episode: number) => post<ActionResult>(`/entries/${id}/progress`, { episode, category }), 'Fortschritt gespeichert.')

export const useBookmark = (id: number, episode: number, language: string, category: Category) =>
  useAction(
    (target: 'this' | 'next') => post<ActionResult>(`/watch/${id}/${episode}/${language}/bookmark`, { target, category }),
    'Lesezeichen gesetzt.',
  )

export const useDeleteBookmark = () =>
  useAction((bookmarkId: string) => post<ActionResult>(`/me/bookmarks/${bookmarkId}/delete`, {}), 'Lesezeichen entfernt.')

export const useSetListEpisode = () =>
  useAction(({ cid, value }: { cid: string; value: number }) => post<ActionResult>(`/me/list/${cid}/episode`, { value }), 'Fortschritt gespeichert.')

export const useSetListState = () =>
  useAction(({ cid, state }: { cid: string; state: number }) => post<ActionResult>(`/me/list/${cid}/state`, { state }), 'Status geändert.')

export const useDeleteNotification = () =>
  useAction((notificationId: string) => post<ActionResult>(`/notifications/${notificationId}/delete`, {}), 'Benachrichtigung entfernt.')

export const useSendMessage = (conversationId: number) =>
  useAction((text: string) => post<ActionResult>(`/messages/${conversationId}/send`, { text }), 'Nachricht gesendet.')

export const useDeleteListEntry = () =>
  useAction((cid: string) => post<ActionResult>(`/me/list/${cid}/delete`, {}), 'Eintrag entfernt.')

const languageLabels: Record<string, string> = {
  gersub: 'GerSub', gerdub: 'GerDub', engsub: 'EngSub', engdub: 'EngDub', de: 'Deutsch', en: 'Englisch',
}
export const languageLabel = (language: string) => languageLabels[language] ?? language

/** Proxer marks blocked languages with a pseudo hoster named "message". */
export const isPlayable = (language: EpisodeLanguage) => language.hosters.some((h) => h !== 'message')
