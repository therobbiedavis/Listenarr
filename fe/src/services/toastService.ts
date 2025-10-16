import { reactive } from 'vue'

type Toast = { id: string; level: string; title: string; message: string; timeoutMs?: number }

const state = reactive<{ toasts: Toast[]; callbacks: Set<(t: Toast[]) => void> }>({ toasts: [], callbacks: new Set() })

function genId() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2,8)}`
}

function notifySubscribers() {
  for (const cb of state.callbacks) {
    try { cb(state.toasts) } catch { /* swallow subscriber errors */ }
  }
}

function push(level: string, title: string, message: string, timeoutMs = 5000) {
  const t: Toast = { id: genId(), level, title, message, timeoutMs }
  state.toasts.unshift(t)
  if (state.toasts.length > 6) state.toasts.pop()
  notifySubscribers()
  if (timeoutMs && timeoutMs > 0) {
    setTimeout(() => { dismiss(t.id) }, timeoutMs)
  }
  return t.id
}

function dismiss(id: string) {
  const idx = state.toasts.findIndex(t => t.id === id)
  if (idx >= 0) {
    state.toasts.splice(idx, 1)
    notifySubscribers()
  }
}

function subscribe(cb: (toasts: Toast[]) => void) {
  state.callbacks.add(cb)
  // Invoke immediately with current state
  try { cb(state.toasts) } catch {}
  return () => { state.callbacks.delete(cb) }
}

export function useToast() {
  return {
    toasts: state.toasts,
    push,
    dismiss,
    subscribe,
    info: (title: string, message: string, timeoutMs?: number) => push('info', title, message, timeoutMs),
    success: (title: string, message: string, timeoutMs?: number) => push('success', title, message, timeoutMs),
    warning: (title: string, message: string, timeoutMs?: number) => push('warning', title, message, timeoutMs),
    error: (title: string, message: string, timeoutMs?: number) => push('error', title, message, timeoutMs),
  }
}
