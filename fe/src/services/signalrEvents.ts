import { ref } from 'vue'

// Reactive state for SignalR connection diagnostics and UI
export const isConnected = ref<boolean>(false)
export const lastError = ref<string | null>(null)
export const reconnectAttempts = ref<number>(0)

const connectedListeners: Set<() => void> = new Set()
const disconnectedListeners: Set<() => void> = new Set()

export function onConnected(cb: () => void): () => void {
  connectedListeners.add(cb)
  return () => { connectedListeners.delete(cb) }
}

export function onDisconnected(cb: () => void): () => void {
  disconnectedListeners.add(cb)
  return () => { disconnectedListeners.delete(cb) }
}

export function setConnected(val: boolean) {
  isConnected.value = val
  try {
    if (val) {
      for (const cb of Array.from(connectedListeners)) {
        try { cb() } catch {}
      }
    } else {
      for (const cb of Array.from(disconnectedListeners)) {
        try { cb() } catch {}
      }
    }
  } catch {}
}

export function setLastError(err: string | null) {
  lastError.value = err
}

export function setReconnectAttempts(n: number) {
  reconnectAttempts.value = n
}

export default {
  isConnected,
  lastError,
  reconnectAttempts,
  onConnected,
  onDisconnected,
  setConnected,
  setLastError,
  setReconnectAttempts
}
