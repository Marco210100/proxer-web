import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  useConversation,
  useConversations,
  useDeleteNotification,
  useNotifications,
  useProfile,
  useSendMessage,
  type Message,
} from '../api'
import { ErrorNote, Loading, PageTitle, Pager } from '../components'

export function Avatar({ src, size = 'size-10' }: { src: string | null; size?: string }) {
  return (
    <div
      className={`${size} shrink-0 rounded-full bg-raised bg-cover bg-center ring-1 ring-line`}
      style={src ? { backgroundImage: `url(${src})` } : undefined}
    />
  )
}

export function Notifications() {
  const [page, setPage] = useState(1)
  const notifications = useNotifications(page)
  const remove = useDeleteNotification()

  return (
    <div className="mx-auto max-w-3xl">
      <PageTitle>Benachrichtigungen</PageTitle>
      {notifications.isLoading && <Loading />}
      {notifications.error && <ErrorNote error={notifications.error} />}
      {notifications.data && (
        <div className={notifications.isPlaceholderData ? 'opacity-50' : ''}>
          {notifications.data.items.length === 0 && <p className="text-muted">Keine Benachrichtigungen.</p>}
          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            {notifications.data.items.map((item) => {
              const body = (
                <>
                  <span className="block text-sm">{item.text}</span>
                  <span className="text-xs text-muted">{item.date}</span>
                </>
              )
              return (
                <li key={item.id} className="flex items-center gap-3 px-4 py-3">
                  {item.link ? (
                    <Link to={item.link} className="min-w-0 flex-1 hover:text-accent">
                      {body}
                    </Link>
                  ) : item.external ? (
                    <a href={item.external} target="_blank" rel="noreferrer" className="min-w-0 flex-1 hover:text-accent">
                      {body}
                    </a>
                  ) : (
                    <div className="min-w-0 flex-1">{body}</div>
                  )}
                  <button
                    type="button"
                    onClick={() => remove.mutate(item.id)}
                    disabled={remove.isPending}
                    aria-label="Benachrichtigung entfernen"
                    title="Entfernen"
                    className="flex size-7 items-center justify-center rounded-full border border-line text-sm text-muted enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-40"
                  >
                    ✕
                  </button>
                </li>
              )
            })}
          </ul>
          {(page > 1 || notifications.data.hasNext) && <Pager page={page} hasNext={notifications.data.hasNext} onChange={setPage} />}
        </div>
      )}
    </div>
  )
}

export function Conversations() {
  const conversations = useConversations()
  return (
    <div className="mx-auto max-w-3xl">
      <PageTitle>Nachrichten</PageTitle>
      {conversations.isLoading && <Loading />}
      {conversations.error && <ErrorNote error={conversations.error} />}
      {conversations.data?.length === 0 && <p className="text-muted">Keine Unterhaltungen.</p>}
      <ul className="space-y-2">
        {conversations.data?.map((conversation) => (
          <li key={conversation.id}>
            <Link
              to={`/nachrichten/${conversation.id}`}
              className="group flex items-center gap-3 rounded-xl border border-line bg-surface px-4 py-3 transition hover:border-accent"
            >
              <Avatar src={conversation.avatar} />
              <div className="min-w-0 flex-1">
                <div className="truncate font-medium group-hover:text-accent">{conversation.name}</div>
                <div className="text-xs text-muted">{conversation.date}</div>
              </div>
              {conversation.unread && <span className="rounded-full bg-accent px-2 py-0.5 text-xs font-semibold text-white">neu</span>}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}

const timeFormat = new Intl.DateTimeFormat('de-DE', { dateStyle: 'medium', timeStyle: 'short' })

function Bubble({ message }: { message: Message }) {
  if (message.system)
    return <div className="py-1 text-center text-xs text-muted">{message.text}</div>
  return (
    <div className={`flex gap-2 ${message.mine ? 'flex-row-reverse' : ''}`}>
      <Avatar src={message.avatar} size="size-8" />
      <div className={`max-w-[75%] rounded-2xl px-3.5 py-2 ${message.mine ? 'bg-accent-soft' : 'bg-raised'}`}>
        {!message.mine && <div className="text-xs font-medium text-accent">{message.username}</div>}
        <p className="whitespace-pre-line break-words text-sm leading-relaxed">{message.text}</p>
        <div className="mt-0.5 text-right text-[11px] text-muted">{timeFormat.format(message.timestamp * 1000)}</div>
      </div>
    </div>
  )
}

/** One page of a conversation; pages are stacked oldest first. */
function MessagePage({ id, page, onOlder, isOldest }: { id: number; page: number; onOlder: () => void; isOldest: boolean }) {
  const conversation = useConversation(id, page)
  if (conversation.isLoading) return <Loading />
  if (conversation.error) return <ErrorNote error={conversation.error} />
  const messages = [...(conversation.data?.messages ?? [])].reverse()
  return (
    <>
      {isOldest && conversation.data?.hasMore && (
        <button type="button" onClick={onOlder} className="mx-auto block rounded-full border border-line px-4 py-1 text-xs text-muted hover:border-accent hover:text-accent">
          Ältere Nachrichten laden
        </button>
      )}
      {messages.map((message) => (
        <Bubble key={message.id} message={message} />
      ))}
    </>
  )
}

export function ConversationView() {
  const id = Number(useParams().id)
  const [pages, setPages] = useState(1)
  const [text, setText] = useState('')
  const first = useConversation(id, 0)
  const send = useSendMessage(id)
  const others = first.data?.participants ?? []

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!text.trim()) return
    send.mutate(text, { onSuccess: (result) => result.ok && setText('') })
  }

  return (
    <div className="mx-auto max-w-3xl">
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Link to="/nachrichten" className="text-sm text-muted hover:text-accent">
          ← Nachrichten
        </Link>
        <div className="flex flex-wrap gap-2 text-sm">
          {others.map((person) => (
            <Link key={person.id} to={`/user/${person.id}`} className="rounded-full bg-raised px-3 py-1 hover:text-accent">
              {person.name}
            </Link>
          ))}
        </div>
      </div>

      <div className="space-y-3 rounded-xl border border-line bg-surface p-4">
        {Array.from({ length: pages }, (_, index) => pages - 1 - index).map((page) => (
          <MessagePage key={page} id={id} page={page} isOldest={page === pages - 1} onOlder={() => setPages(pages + 1)} />
        ))}
      </div>

      <form onSubmit={submit} className="mt-3 flex gap-2">
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder="Nachricht schreiben…"
          aria-label="Nachricht"
          rows={2}
          maxLength={65000}
          className="min-w-0 flex-1 resize-y rounded-xl border border-line bg-surface px-3 py-2 text-sm outline-none placeholder:text-muted focus:border-accent"
        />
        <button
          type="submit"
          disabled={send.isPending || !text.trim()}
          className="self-end rounded-xl bg-accent px-4 py-2 text-sm font-semibold text-white enabled:hover:brightness-110 disabled:opacity-50"
        >
          {send.isPending ? 'Sendet…' : 'Senden'}
        </button>
      </form>
    </div>
  )
}

export function Profile() {
  const id = Number(useParams().id)
  const profile = useProfile(id)
  if (profile.isLoading) return <Loading />
  if (profile.error) return <ErrorNote error={profile.error} />
  const data = profile.data
  if (!data) return null

  return (
    <div className="mx-auto max-w-3xl">
      <div className="flex flex-wrap items-center gap-5">
        <Avatar src={data.avatar} size="size-28" />
        <div className="min-w-0">
          <h1 className="text-3xl font-bold tracking-tight">{data.name}</h1>
          {data.statusText && <p className="mt-1 text-muted">{data.statusText}</p>}
          <a href={`https://proxer.me/user/${data.id}`} target="_blank" rel="noreferrer" className="mt-2 inline-block text-sm text-muted hover:text-accent">
            Auf Proxer öffnen ↗
          </a>
        </div>
      </div>
      <dl className="mt-8 grid gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
        {data.facts.map((fact) => (
          <div key={fact.label} className="min-w-0">
            <dt className="text-xs uppercase tracking-wide text-muted">{fact.label}</dt>
            <dd className="mt-0.5 whitespace-pre-line break-words">{fact.values.join(' · ')}</dd>
          </div>
        ))}
      </dl>
    </div>
  )
}
