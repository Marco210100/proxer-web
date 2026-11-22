import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useForumCategories, useForumThread, useForumTopics } from '../api'
import { ErrorNote, Loading, PageTitle } from '../components'
import { Avatar } from './Community'

/** Numbered pager for forum lists, which can run to dozens of pages. */
function PageLinks({ page, pageCount, onChange }: { page: number; pageCount: number; onChange: (page: number) => void }) {
  if (pageCount <= 1) return null
  const wanted = new Set([1, 2, page - 1, page, page + 1, pageCount - 1, pageCount])
  const pages = [...wanted].filter((p) => p >= 1 && p <= pageCount).sort((a, b) => a - b)
  return (
    <nav className="my-5 flex flex-wrap items-center justify-center gap-1.5" aria-label="Seiten">
      {pages.map((p, index) => (
        <span key={p} className="flex items-center gap-1.5">
          {index > 0 && p - pages[index - 1] > 1 && <span className="text-muted">…</span>}
          <button
            type="button"
            onClick={() => onChange(p)}
            aria-current={p === page ? 'page' : undefined}
            className={`min-w-8 rounded-full border px-2.5 py-1 text-sm tabular-nums ${
              p === page ? 'border-accent bg-accent-soft text-accent' : 'border-line text-muted hover:text-ink'
            }`}
          >
            {p}
          </button>
        </span>
      ))}
    </nav>
  )
}

function usePage() {
  const [params, setParams] = useSearchParams()
  const page = Math.max(1, Number(params.get('seite')) || 1)
  const setPage = (next: number) => {
    setParams(next > 1 ? { seite: String(next) } : {})
    window.scrollTo({ top: 0 })
  }
  return [page, setPage] as const
}

export function ForumIndex() {
  const categories = useForumCategories()
  return (
    <div className="mx-auto max-w-3xl">
      <PageTitle>Forum</PageTitle>
      {categories.isLoading && <Loading />}
      {categories.error && <ErrorNote error={categories.error} />}
      <div className="space-y-1">
        {categories.data?.map((category) =>
          category.depth === 0 ? (
            <h2 key={category.id} className="pb-1 pt-5 text-xs uppercase tracking-wide text-muted first:pt-0">
              {category.name}
            </h2>
          ) : (
            <Link
              key={category.id}
              to={`/forum/${category.id}`}
              style={{ marginLeft: `${(category.depth - 1) * 1.5}rem` }}
              className="block rounded-lg border border-line bg-surface px-4 py-2.5 text-sm transition hover:border-accent hover:text-accent"
            >
              {category.name}
            </Link>
          ),
        )}
      </div>
    </div>
  )
}

export function ForumCategoryView() {
  const categoryId = Number(useParams().categoryId)
  const [page, setPage] = usePage()
  const topics = useForumTopics(categoryId, page)
  const data = topics.data

  return (
    <div className="mx-auto max-w-4xl">
      <Link to="/forum" className="text-sm text-muted hover:text-accent">
        ← Forum
      </Link>
      <div className="mt-2">
        <PageTitle>{data?.title ?? 'Forum'}</PageTitle>
      </div>
      {topics.isLoading && <Loading />}
      {topics.error && <ErrorNote error={topics.error} />}
      {data && (
        <div className={topics.isPlaceholderData ? 'opacity-50' : ''}>
          {data.topics.length === 0 && <p className="text-muted">In diesem Bereich gibt es keine Themen.</p>}
          <ul className="space-y-2">
            {data.topics.map((topic) => (
              <li key={topic.id}>
                <Link
                  to={`/forum/${categoryId}/${topic.id}`}
                  className="group flex items-center gap-4 rounded-xl border border-line bg-surface px-4 py-3 transition hover:border-accent"
                >
                  <div className="min-w-0 flex-1">
                    <div className="font-medium leading-snug group-hover:text-accent">
                      {topic.sticky && <span className="mr-2 rounded bg-raised px-1.5 py-0.5 text-[11px] text-muted">Angepinnt</span>}
                      {topic.title}
                    </div>
                    {topic.excerpt && <p className="mt-0.5 line-clamp-1 text-sm text-muted">{topic.excerpt}</p>}
                    <div className="mt-1 text-xs text-muted">
                      von {topic.author || 'unbekannt'} · {topic.replies} Antworten · {topic.views} Aufrufe
                    </div>
                  </div>
                  <div className="hidden w-44 shrink-0 items-center gap-2 text-xs text-muted sm:flex">
                    <Avatar src={topic.lastAvatar} size="size-8" />
                    <div className="min-w-0">
                      <div className="truncate text-ink">{topic.lastPostBy}</div>
                      <div className="truncate">{topic.lastPostAge}</div>
                    </div>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
          <PageLinks page={page} pageCount={data.pageCount} onChange={setPage} />
        </div>
      )}
    </div>
  )
}

// Styles for the whitelisted markup the backend returns for a post body.
const postBody =
  'break-words text-sm leading-relaxed [&_a]:text-accent [&_a:hover]:underline [&_img]:my-2 [&_img]:max-h-96 [&_img]:max-w-full [&_img]:rounded-lg ' +
  '[&_blockquote]:my-2 [&_blockquote]:border-l-2 [&_blockquote]:border-line [&_blockquote]:pl-3 [&_blockquote]:text-muted ' +
  '[&_details]:my-2 [&_details]:rounded-lg [&_details]:border [&_details]:border-line [&_details]:px-3 [&_details]:py-1.5 ' +
  '[&_summary]:cursor-pointer [&_summary]:text-xs [&_summary]:text-muted [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal [&_ol]:pl-5 ' +
  '[&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:bg-raised [&_pre]:p-2'

export function ForumThreadView() {
  const params = useParams()
  const categoryId = Number(params.categoryId)
  const topicId = Number(params.topicId)
  const [page, setPage] = usePage()
  const thread = useForumThread(categoryId, topicId, page)
  const data = thread.data

  return (
    <div className="mx-auto max-w-4xl">
      <Link to={`/forum/${categoryId}`} className="text-sm text-muted hover:text-accent">
        ← Zurück zum Bereich
      </Link>
      <div className="mt-2">
        <PageTitle
          aside={
            <a href={`https://proxer.me/forum/${categoryId}/${topicId}`} target="_blank" rel="noreferrer" className="text-sm text-muted hover:text-accent">
              Auf Proxer antworten ↗
            </a>
          }
        >
          {data?.title ?? 'Thema'}
        </PageTitle>
      </div>
      {thread.isLoading && <Loading />}
      {thread.error && <ErrorNote error={thread.error} />}
      {data && (
        <div className={thread.isPlaceholderData ? 'opacity-50' : ''}>
          <PageLinks page={page} pageCount={data.pageCount} onChange={setPage} />
          <div className="space-y-3">
            {data.posts.map((post) => (
              <article key={post.id} className="rounded-xl border border-line bg-surface p-4">
                <header className="mb-3 flex items-center gap-3">
                  <Avatar src={post.avatar} />
                  <div className="min-w-0">
                    {post.userId ? (
                      <Link to={`/user/${post.userId}`} className="font-medium hover:text-accent">
                        {post.username}
                      </Link>
                    ) : (
                      <span className="font-medium">{post.username}</span>
                    )}
                    <div className="text-xs text-muted" title={post.date}>
                      {post.age}
                    </div>
                  </div>
                  <span className="ml-auto text-xs tabular-nums text-muted">#{post.id}</span>
                </header>
                {/* The backend rebuilds this markup from a fixed whitelist, see CommunityParsers.Sanitize. */}
                <div className={postBody} dangerouslySetInnerHTML={{ __html: post.html }} />
              </article>
            ))}
          </div>
          <PageLinks page={page} pageCount={data.pageCount} onChange={setPage} />
        </div>
      )}
    </div>
  )
}
