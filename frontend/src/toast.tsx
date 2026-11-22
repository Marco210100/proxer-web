import { useEffect, useState } from 'react'

interface Toast {
  id: number
  text: string
  ok: boolean
}

// A tiny module-level store: any code can raise a toast, the single <Toasts /> shows them.
let toasts: Toast[] = []
let nextId = 1
const listeners = new Set<(toasts: Toast[]) => void>()
const emit = () => listeners.forEach((listener) => listener(toasts))

export function toast(text: string, ok = true) {
  const item = { id: nextId++, text, ok }
  toasts = [...toasts, item]
  emit()
  setTimeout(() => {
    toasts = toasts.filter((t) => t !== item)
    emit()
  }, 4500)
}

export function Toasts() {
  const [items, setItems] = useState(toasts)
  useEffect(() => {
    listeners.add(setItems)
    return () => {
      listeners.delete(setItems)
    }
  }, [])

  return (
    <div className="pointer-events-none fixed bottom-4 right-4 z-50 flex max-w-sm flex-col gap-2" role="status" aria-live="polite">
      {items.map((item) => (
        <div
          key={item.id}
          className={`rounded-xl border bg-raised px-4 py-2.5 text-sm shadow-lg ${item.ok ? 'border-good/50' : 'border-accent/60'}`}
        >
          {item.text}
        </div>
      ))}
    </div>
  )
}
