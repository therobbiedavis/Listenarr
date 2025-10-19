import type { Download, QueueItem, Audiobook } from '@/types'

// SignalR client for real-time download updates
// Using native WebSocket with fallback to long polling

// In DEV use localhost. In production prefer configured VITE_API_BASE_URL origin;
// fall back to same-origin by using a relative '/api' which implies current host.
const API_BASE_URL = import.meta.env.DEV
  ? 'http://localhost:5000'
  : (import.meta.env.VITE_API_BASE_URL ? import.meta.env.VITE_API_BASE_URL.replace('/api', '') : '')

type DownloadUpdateCallback = (downloads: Download[]) => void
type DownloadListCallback = (downloads: Download[]) => void
type QueueUpdateCallback = (queue: QueueItem[]) => void
type ScanJobCallback = (job: { jobId: string; audiobookId: number; status: string; found?: number; created?: number; error?: string }) => void

class SignalRService {
  private connection: WebSocket | null = null
  private reconnectAttempts = 0
  private maxReconnectAttempts = 10
  private reconnectDelay = 2000
  private isConnecting = false
  private messageId = 0
  private downloadUpdateCallbacks: Set<DownloadUpdateCallback> = new Set()
  private downloadListCallbacks: Set<DownloadListCallback> = new Set()
  private queueUpdateCallbacks: Set<QueueUpdateCallback> = new Set()
  private audiobookUpdateCallbacks: Set<(a: Audiobook) => void> = new Set()
  private scanJobCallbacks: Set<ScanJobCallback> = new Set()
  private filesRemovedCallbacks: Set<(payload: { audiobookId: number; removed: Array<{ id: number; path: string }> }) => void> = new Set()
  private searchProgressCallbacks: Set<(payload: { message: string; asin?: string | null }) => void> = new Set()
  private toastCallbacks: Set<(payload: { level: string; title: string; message: string; timeoutMs?: number }) => void> = new Set()
  private pingInterval: number | null = null

  async connect(): Promise<void> {
    if (this.connection?.readyState === WebSocket.OPEN || this.isConnecting) {
      return
    }

    this.isConnecting = true
    const wsUrl = API_BASE_URL.replace('http://', 'ws://').replace('https://', 'wss://')
    
    try {
      // Using SignalR protocol over WebSocket
      let hubUrl = `${wsUrl}/hubs/downloads`
      try {
        // Try to include API key as access_token if the startup config contains one
        // This will allow the server-side ApiKeyMiddleware to accept the websocket handshake
        const { getStartupConfigCached } = await import('./startupConfigCache')
        const sc = await getStartupConfigCached(2000)
        const apiKey = sc?.apiKey
        if (apiKey) {
          const sep = hubUrl.includes('?') ? '&' : '?'
          hubUrl = `${hubUrl}${sep}access_token=${encodeURIComponent(apiKey)}`
        }
      } catch (e) {
        // ignore errors while reading startup config (log debug info)
        try { console.debug('[SignalR] startupConfig read failed', e) } catch {}
      }

      console.log('[SignalR] Connecting to:', hubUrl)
      this.connection = new WebSocket(hubUrl)
      
      this.connection.onopen = () => {
        console.log('[SignalR] Connected to download hub')
        this.reconnectAttempts = 0
        this.isConnecting = false
        this.sendHandshake()
        this.startPingInterval()
      }

      this.connection.onmessage = (event) => {
        this.handleMessage(event.data)
      }

      this.connection.onerror = (error) => {
        console.error('[SignalR] WebSocket error:', error)
      }

      this.connection.onclose = () => {
        console.log('[SignalR] Connection closed')
        this.isConnecting = false
        this.stopPingInterval()
        this.attemptReconnect()
      }
    } catch (error) {
      console.error('[SignalR] Failed to connect:', error)
      this.isConnecting = false
      this.attemptReconnect()
    }
  }

  private sendHandshake() {
    // SignalR handshake message
    const handshake = {
      protocol: 'json',
      version: 1
    }
    this.send(JSON.stringify(handshake) + '\x1e')
  }

  private handleMessage(data: string) {
    try {
      // SignalR messages are terminated with \x1e
      const messages = data.split('\x1e').filter(m => m.length > 0)
      
      for (const message of messages) {
        const parsed = JSON.parse(message)
        
        // Handle different message types
        if (parsed.type === 1) {
          // Invocation message from server
          this.handleInvocation(parsed)
        } else if (parsed.type === 2) {
          // Stream item
          console.log('[SignalR] Stream item:', parsed)
        } else if (parsed.type === 3) {
          // Completion
          console.log('[SignalR] Completion:', parsed)
        } else if (parsed.type === 6) {
          // Ping
          this.sendPong()
        } else if (parsed.type === 7) {
          // Close
          console.log('[SignalR] Server requested close')
          this.disconnect()
        } else if (!parsed.type) {
          // Handshake response
          console.log('[SignalR] Handshake response:', parsed)
        }
      }
    } catch (error) {
      console.error('[SignalR] Error parsing message:', error, data)
    }
  }

  private handleInvocation(message: { type: number; invocationId?: string; target: string; arguments: unknown[] }) {
    const { target, arguments: args } = message
    
    console.log('[SignalR] Received:', target, args)
    
    switch (target) {
      case 'DownloadUpdate':
        // Single or multiple download updates
        if (args && args[0]) {
          const downloads = Array.isArray(args[0]) ? args[0] : [args[0]]
          this.downloadUpdateCallbacks.forEach(cb => cb(downloads as Download[]))
        }
        break
        
      case 'DownloadsList':
        // Full downloads list
        if (args && args[0]) {
          this.downloadListCallbacks.forEach(cb => cb(args[0] as Download[]))
        }
        break
        
      case 'QueueUpdate':
        // Queue update from external download clients
        if (args && args[0]) {
          this.queueUpdateCallbacks.forEach(cb => cb(args[0] as QueueItem[]))
        }
        break

      case 'AudiobookUpdate':
        if (args && args[0]) {
          const ab = args[0] as Audiobook
          this.audiobookUpdateCallbacks.forEach(cb => cb(ab))
        }
        break
      case 'ScanJobUpdate':
        if (args && args[0]) {
          const job = args[0] as unknown as { jobId: string; audiobookId: number; status: string; found?: number; created?: number; error?: string }
          this.scanJobCallbacks.forEach(cb => cb(job))
        }
        break
      case 'FilesRemoved':
        if (args && args[0]) {
          const payload = args[0] as unknown as { audiobookId: number; removed: Array<{ id: number; path: string }> }
          this.filesRemovedCallbacks.forEach(cb => cb(payload))
        }
        break
      case 'SearchProgress':
        if (args && args[0]) {
          const payload = args[0] as { message: string; asin?: string | null }
          this.searchProgressCallbacks.forEach(cb => cb(payload))
        }
        break
      case 'ToastMessage':
        if (args && args[0]) {
          const payload = args[0] as { level: string; title: string; message: string; timeoutMs?: number }
          this.toastCallbacks.forEach(cb => cb(payload))
        }
        break
    }
  }

  private send(data: string) {
    if (this.connection?.readyState === WebSocket.OPEN) {
      this.connection.send(data)
    }
  }

  private sendPong() {
    const pong = { type: 6 }
    this.send(JSON.stringify(pong) + '\x1e')
  }

  private startPingInterval() {
    this.stopPingInterval()
    // Send ping every 15 seconds to keep connection alive
    this.pingInterval = window.setInterval(() => {
      const ping = { type: 6 }
      this.send(JSON.stringify(ping) + '\x1e')
    }, 15000)
  }

  private stopPingInterval() {
    if (this.pingInterval) {
      clearInterval(this.pingInterval)
      this.pingInterval = null
    }
  }

  private attemptReconnect() {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('[SignalR] Max reconnect attempts reached')
      return
    }

    this.reconnectAttempts++
    const delay = this.reconnectDelay * Math.pow(1.5, this.reconnectAttempts - 1)
    
    console.log(`[SignalR] Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`)
    
    setTimeout(() => {
      this.connect()
    }, delay)
  }

  disconnect() {
    this.stopPingInterval()
    if (this.connection) {
      this.connection.close()
      this.connection = null
    }
    this.isConnecting = false
  }

  // Subscribe to download updates
  onDownloadUpdate(callback: DownloadUpdateCallback): () => void {
    this.downloadUpdateCallbacks.add(callback)
    
    // Return unsubscribe function
    return () => {
      this.downloadUpdateCallbacks.delete(callback)
    }
  }

  // Subscribe to full downloads list
  onDownloadsList(callback: DownloadListCallback): () => void {
    this.downloadListCallbacks.add(callback)
    
    // Return unsubscribe function
    return () => {
      this.downloadListCallbacks.delete(callback)
    }
  }

  // Subscribe to queue updates (external download clients)
  onQueueUpdate(callback: QueueUpdateCallback): () => void {
    this.queueUpdateCallbacks.add(callback)
    
    // Return unsubscribe function
    return () => {
      this.queueUpdateCallbacks.delete(callback)
    }
  }

  // Subscribe to audiobook updates (full audiobook object)
  onAudiobookUpdate(callback: (a: Audiobook) => void): () => void {
    this.audiobookUpdateCallbacks.add(callback)
    return () => { this.audiobookUpdateCallbacks.delete(callback) }
  }

  // Subscribe to scan job updates
  onScanJobUpdate(callback: ScanJobCallback): () => void {
    this.scanJobCallbacks.add(callback)
    return () => { this.scanJobCallbacks.delete(callback) }
  }

  // Subscribe to search progress messages (from server-side search operations)
  onSearchProgress(callback: (payload: { message: string; asin?: string | null }) => void): () => void {
    this.searchProgressCallbacks.add(callback)
    return () => { this.searchProgressCallbacks.delete(callback) }
  }

  // Subscribe to server-sent toast messages
  onToast(callback: (payload: { level: string; title: string; message: string; timeoutMs?: number }) => void): () => void {
    this.toastCallbacks.add(callback)
    return () => { this.toastCallbacks.delete(callback) }
  }

  // Subscribe to files removed notifications
  onFilesRemoved(callback: (payload: { audiobookId: number; removed: Array<{ id: number; path: string }> }) => void): () => void {
    this.filesRemovedCallbacks.add(callback)
    return () => { this.filesRemovedCallbacks.delete(callback) }
  }

  // Request current downloads from server
  requestDownloadsUpdate() {
    const message = {
      type: 1, // Invocation
      invocationId: String(++this.messageId),
      target: 'RequestDownloadsUpdate',
      arguments: []
    }
    this.send(JSON.stringify(message) + '\x1e')
  }

  get isConnected(): boolean {
    return this.connection?.readyState === WebSocket.OPEN
  }
}

// Singleton instance
export const signalRService = new SignalRService()

// Auto-connect when module is imported
signalRService.connect()
