// lightweight composable for SignalR state
import * as events from '@/services/signalrEvents'

export function useSignalR() {
  return {
    isConnected: events.isConnected,
    lastError: events.lastError,
    reconnectAttempts: events.reconnectAttempts,
    onConnected: events.onConnected,
    onDisconnected: events.onDisconnected
  }
}

export default useSignalR
