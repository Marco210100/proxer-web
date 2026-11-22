import { useState, type FormEvent } from 'react'
import { NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { useLogout, useNotificationCounts, useSession } from './api'
import { ErrorNote, Loading } from './components'
import Login from './pages/Login'
import { Toasts } from './toast'
import Home from './pages/Home'
import Browse from './pages/Browse'
import Search from './pages/Search'
import Entry from './pages/Entry'
import Watch from './pages/Watch'
import MyList from './pages/MyList'
import Season from './pages/Season'
import Calendar from './pages/Calendar'
import { ConversationView, Conversations, Notifications, Profile } from './pages/Community'
import { ForumCategoryView, ForumIndex, ForumThreadView } from './pages/Forum'
import InfoPageView from './pages/InfoPage'
import Overview from './pages/Overview'

const navItems = [
  { to: '/', label: 'Start' },
  { to: '/anime', label: 'Anime' },
  { to: '/manga', label: 'Manga' },
  { to: '/novel', label: 'Novel' },
  { to: '/season', label: 'Season' },
  { to: '/kalender', label: 'Kalender' },
  { to: '/forum', label: 'Forum' },
  { to: '/uebersicht/character', label: 'Mehr' },
  { to: '/liste', label: 'Meine Liste' },
]

function Header() {
  const navigate = useNavigate()
  const session = useSession()
  const logout = useLogout()
  const counts = useNotificationCounts()
  const [query, setQuery] = useState('')
  const pill = ({ isActive }: { isActive: boolean }) =>
    `rounded-full border px-3 py-1.5 text-sm transition-colors ${
      isActive ? 'border-accent text-accent' : 'border-line text-muted hover:border-accent hover:text-accent'
    }`
  const badge = (count: number) =>
    count > 0 && (
      <span className="ml-1.5 rounded-full bg-accent px-1.5 py-0.5 text-[11px] font-semibold leading-none tabular-nums text-white">
        {count > 99 ? '99+' : count}
      </span>
    )

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (query.trim().length > 1) navigate(`/suche?q=${encodeURIComponent(query.trim())}`)
  }

  return (
    <header className="sticky top-0 z-20 border-b border-line bg-bg/85 backdrop-blur">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-3 gap-y-2 px-4 py-3">
        <NavLink to="/" className="text-lg font-bold tracking-tight">
          prox<span className="text-accent">er</span>
        </NavLink>
        <nav className="flex flex-wrap gap-1 text-sm">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                `rounded-full px-2.5 py-1.5 transition-colors ${isActive ? 'bg-accent-soft text-accent' : 'text-muted hover:text-ink'}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <form onSubmit={submit} className="ml-auto flex min-w-40 flex-1 justify-end sm:flex-none">
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Suchen…"
            aria-label="Suche"
            className="w-full rounded-full border border-line bg-surface px-4 py-1.5 text-sm outline-none placeholder:text-muted focus:border-accent sm:w-36"
          />
        </form>
        <NavLink to="/nachrichten" className={pill} aria-label="Nachrichten" title="Nachrichten">
          ✉{badge(counts.data?.messages ?? 0)}
        </NavLink>
        <NavLink to="/benachrichtigungen" className={pill} aria-label="Benachrichtigungen" title="Benachrichtigungen">
          🔔{badge((counts.data?.other ?? 0) + (counts.data?.requests ?? 0))}
        </NavLink>
        {session.data?.userId ? (
          <NavLink to={`/user/${session.data.userId}`} className="hidden text-sm text-muted hover:text-accent md:block">
            {session.data.username}
          </NavLink>
        ) : (
          <span className="hidden text-sm text-muted md:block">{session.data?.username}</span>
        )}
        <button
          type="button"
          onClick={() => logout.mutate()}
          disabled={logout.isPending}
          className="rounded-full border border-line px-3 py-1.5 text-sm text-muted enabled:hover:border-accent enabled:hover:text-accent disabled:opacity-50"
        >
          {logout.isPending ? 'Abmelden…' : 'Abmelden'}
        </button>
      </div>
    </header>
  )
}

export default function App() {
  const session = useSession()
  const location = useLocation()

  if (session.isLoading)
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Loading label="Sitzung wird geprüft…" />
      </div>
    )
  if (session.error)
    return (
      <div className="mx-auto max-w-md px-4 py-20">
        <ErrorNote error={session.error} />
      </div>
    )
  if (!session.data?.loggedIn) return <Login />

  return (
    <div className="min-h-screen">
      <Header />
      <Toasts />
      <main className="mx-auto max-w-7xl px-4 py-6">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/anime" element={<Browse category="anime" />} />
          <Route path="/manga" element={<Browse category="manga" />} />
          <Route path="/novel" element={<Browse category="novel" />} />
          <Route path="/season" element={<Season />} />
          <Route path="/kalender" element={<Calendar />} />
          <Route path="/suche" element={<Search />} />
          <Route path="/benachrichtigungen" element={<Notifications />} />
          <Route path="/nachrichten" element={<Conversations />} />
          <Route path="/nachrichten/:id" element={<ConversationView key={location.pathname} />} />
          <Route path="/forum" element={<ForumIndex />} />
          <Route path="/forum/:categoryId" element={<ForumCategoryView />} />
          <Route path="/forum/:categoryId/:topicId" element={<ForumThreadView />} />
          <Route path="/user/:id" element={<Profile />} />
          <Route path="/seite/:kind/:id" element={<InfoPageView />} />
          <Route path="/uebersicht/:kind" element={<Overview />} />
          <Route path="/liste" element={<MyList />} />
          <Route path="/eintrag/:id" element={<Entry />} />
          <Route path="/watch/:id/:episode/:language" element={<Watch />} />
          <Route path="*" element={<p className="text-muted">Diese Seite gibt es nicht.</p>} />
        </Routes>
      </main>
    </div>
  )
}
