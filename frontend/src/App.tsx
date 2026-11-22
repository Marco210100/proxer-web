import { useState, type FormEvent } from 'react'
import { NavLink, Route, Routes, useNavigate } from 'react-router-dom'
import { useSession } from './api'
import Home from './pages/Home'
import Browse from './pages/Browse'
import Search from './pages/Search'
import Entry from './pages/Entry'
import Watch from './pages/Watch'
import MyList from './pages/MyList'
import Season from './pages/Season'
import Calendar from './pages/Calendar'

const navItems = [
  { to: '/', label: 'Start' },
  { to: '/anime', label: 'Anime' },
  { to: '/manga', label: 'Manga' },
  { to: '/season', label: 'Season' },
  { to: '/kalender', label: 'Kalender' },
  { to: '/liste', label: 'Meine Liste' },
]

function Header() {
  const navigate = useNavigate()
  const session = useSession()
  const [query, setQuery] = useState('')

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (query.trim().length > 1) navigate(`/suche?q=${encodeURIComponent(query.trim())}`)
  }

  return (
    <header className="sticky top-0 z-20 border-b border-line bg-bg/85 backdrop-blur">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-3">
        <NavLink to="/" className="text-lg font-bold tracking-tight">
          prox<span className="text-accent">er</span>
        </NavLink>
        <nav className="flex gap-1 text-sm">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                `rounded-full px-3 py-1.5 transition-colors ${isActive ? 'bg-accent-soft text-accent' : 'text-muted hover:text-ink'}`
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
            placeholder="Anime oder Manga suchen…"
            aria-label="Suche"
            className="w-full rounded-full border border-line bg-surface px-4 py-1.5 text-sm outline-none placeholder:text-muted focus:border-accent sm:w-64"
          />
        </form>
        <span className="hidden text-sm text-muted md:block">
          {session.isLoading ? '…' : session.data?.loggedIn ? session.data.username : 'nicht eingeloggt'}
        </span>
      </div>
    </header>
  )
}

export default function App() {
  return (
    <div className="min-h-screen">
      <Header />
      <main className="mx-auto max-w-7xl px-4 py-6">
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/anime" element={<Browse category="anime" />} />
          <Route path="/manga" element={<Browse category="manga" />} />
          <Route path="/season" element={<Season />} />
          <Route path="/kalender" element={<Calendar />} />
          <Route path="/suche" element={<Search />} />
          <Route path="/liste" element={<MyList />} />
          <Route path="/eintrag/:id" element={<Entry />} />
          <Route path="/watch/:id/:episode/:language" element={<Watch />} />
          <Route path="*" element={<p className="text-muted">Diese Seite gibt es nicht.</p>} />
        </Routes>
      </main>
    </div>
  )
}
