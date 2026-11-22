import { useState, type FormEvent } from 'react'
import { useLogin } from '../api'
import { ErrorNote } from '../components'

export default function Login() {
  const login = useLogin()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [secretKey, setSecretKey] = useState('')
  const [twoFactor, setTwoFactor] = useState(false)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    login.mutate({ username, password, secretKey: twoFactor ? secretKey : null })
  }

  const field = 'w-full rounded-lg border border-line bg-bg px-3 py-2 outline-none placeholder:text-muted focus:border-accent'

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded-2xl border border-line bg-surface p-6">
        <div>
          <div className="text-2xl font-bold tracking-tight">
            prox<span className="text-accent">er</span>
          </div>
          <p className="mt-1 text-sm text-muted">Melde dich mit deinem Proxer-Account an.</p>
        </div>

        <label className="block text-sm">
          <span className="mb-1 block text-muted">Benutzername</span>
          <input
            className={field}
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            autoComplete="username"
            autoFocus
            required
          />
        </label>

        <label className="block text-sm">
          <span className="mb-1 block text-muted">Passwort</span>
          <input
            className={field}
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            required
          />
        </label>

        {twoFactor ? (
          <label className="block text-sm">
            <span className="mb-1 block text-muted">2FA-Code</span>
            <input
              className={field}
              value={secretKey}
              onChange={(event) => setSecretKey(event.target.value)}
              autoComplete="one-time-code"
              inputMode="numeric"
            />
          </label>
        ) : (
          <button type="button" onClick={() => setTwoFactor(true)} className="text-sm text-muted hover:text-accent">
            Ich nutze Zwei-Faktor-Authentifizierung
          </button>
        )}

        {login.error && <ErrorNote error={login.error} />}

        <button
          type="submit"
          disabled={login.isPending}
          className="w-full rounded-lg bg-accent px-4 py-2 font-semibold text-white transition enabled:hover:brightness-110 disabled:opacity-60"
        >
          {login.isPending ? 'Anmelden…' : 'Anmelden'}
        </button>

        <p className="text-xs leading-relaxed text-muted">
          Dein Passwort geht nur an dein lokales Backend und von dort an Proxer. Gespeichert wird ausschließlich die
          Sitzung, nicht das Passwort.
        </p>
      </form>
    </div>
  )
}
