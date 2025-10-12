import type { 
  SearchResult, 
  Download, 
  ApiConfiguration, 
  DownloadClientConfiguration, 
  ApplicationSettings,
  Audiobook,
  History,
  Indexer,
  QueueItem,
  RemotePathMapping,
  QualityScore,
  TranslatePathRequest,
  TranslatePathResponse,
  SystemInfo,
  StorageInfo,
  ServiceHealth,
  LogEntry,
  QualityProfile,
  SearchSortBy,
  SearchSortDirection,
  AudibleBookMetadata
} from '@/types'
import { getStartupConfigCached } from './startupConfigCache'

// In development, use relative URLs (proxied by Vite to avoid CORS)
// In production, prefer a configured VITE_API_BASE_URL but fall back to a relative '/api'
const API_BASE_URL = import.meta.env.DEV
  ? '/api'
  : (import.meta.env.VITE_API_BASE_URL || '/api')

// Backend base (origin) used to build absolute image URLs or websocket origins
const BACKEND_BASE_URL = import.meta.env.DEV
  ? ''
  : API_BASE_URL.replace('/api', '')

type ErrorWithStatus = Error & { status?: number; body?: string; retryAfter?: number }

class ApiService {
  private async request<T>(
    endpoint: string, 
    options: RequestInit = {}
  ): Promise<T> {
    const url = `${API_BASE_URL}${endpoint}`
    
    const config: RequestInit = {
      headers: {
        'Content-Type': 'application/json',
        ...options.headers,
      },
      // Include cookies for same-site auth
      credentials: 'include',
      ...options,
    }

    // Attach API key from startup config if present (cached call)
    try {
      const sc = await getStartupConfigCached(2000)
      const apiKey = sc?.apiKey
      if (apiKey) {
        const hdrs = config.headers as Record<string, string> | undefined
        config.headers = { ...(hdrs || {}), 'X-Api-Key': apiKey }
      }
    } catch {}

    // Auto-attach antiforgery token for unsafe HTTP methods when not already provided.
    try {
      const method = (config.method || 'GET').toString().toUpperCase()
      if (['POST', 'PUT', 'DELETE', 'PATCH'].includes(method)) {
        // Only attach if header not already set
        const hdrs = config.headers as Record<string, string> | undefined
        if (!hdrs || !hdrs['X-XSRF-TOKEN']) {
          const token = await this.fetchAntiforgeryToken()
          if (token) {
            config.headers = { ...(config.headers as Record<string, string>), 'X-XSRF-TOKEN': token }
          }
        }
      }
    } catch (e) {
      // Swallow errors fetching token; the server will return a clear error if required
      try { console.debug('[ApiService] failed to fetch CSRF token', e) } catch {}
    }

    try {
      // Debug: log outbound request details in development
      if (import.meta.env.DEV) {
        try { console.debug('[ApiService] request', { url, config }) } catch {}
      }

      const response = await fetch(url, config)

      if (!response.ok) {
        const respText = await response.text().catch(() => '')

        // If the server returns 401, redirect to login (don't surface raw 401 errors to the UI)
        if (response.status === 401) {
          // Avoid causing a SPA redirect loop when the app is trying to fetch
          // the startup configuration during router boot. Let callers (router/auth)
          // handle 401 for that specific endpoint instead of performing a navigation
          // here which can trigger nested navigation during beforeEach.
          if (endpoint && endpoint.startsWith('/configuration/startupconfig')) {
            const err = new Error(`HTTP error! status: 401 - ${respText}`) as ErrorWithStatus
            err.status = 401
            err.body = respText
            throw err
          }
          // Sanitize redirect to avoid open-redirects or unsafe values
              try {
                const { normalizeRedirect } = await import('@/utils/redirect')
                const current = window.location.pathname + window.location.search + window.location.hash
                const safe = normalizeRedirect(current)
                if (!current.startsWith('/login')) {
                  if (import.meta.env.DEV) {
                    try { console.debug('[ApiService] 401 received, redirecting to login', { current, safe }) } catch {}
                  }

                  // Persist the safe redirect in sessionStorage as a fallback in case the
                  // query parameter gets lost or sanitized during navigation. This helps
                  // recover the intended SPA destination after login.
                  try {
                    sessionStorage.setItem('listenarr_pending_redirect', safe)
                  } catch {}

                  // Perform a full-page redirect to the login route with a safe redirect query.
                  // Avoid dynamic importing the router here to prevent circular imports and
                  // Vite chunking warnings. SPA navigation will still work after login via the
                  // redirect query parameter.
                  window.location.href = `/login?redirect=${encodeURIComponent(safe)}`

                  // stop further processing by throwing a specific error
                  throw new Error('Redirecting to login')
                }
              } catch {
                // fallback to a safe redirect to root
                window.location.href = '/login?redirect=%2F'
                throw new Error('Redirecting to login')
              }
        }

        const err = new Error(`HTTP error! status: ${response.status} - ${respText}`)
        const typedErr = err as Error & { status?: number; body?: string }
        typedErr.status = response.status
        typedErr.body = respText
        throw err
      }

      // Handle empty responses (204 No Content or empty body)
      const text = await response.text()
      if (!text || text.trim().length === 0) {
        return null as T
      }

      return JSON.parse(text) as T
    } catch (error) {
      // Enhanced logging for browser console to capture connection failures
      try {
        console.error('[ApiService] request failed', { url, options: config, error })
      } catch {}
      throw error
    }
  }

  // Search API
  async search(query: string, category?: string, apiIds?: string[]): Promise<SearchResult[]> {
    const params = new URLSearchParams({ query })
    if (category) params.append('category', category)
    if (apiIds) apiIds.forEach(id => params.append('apiIds', id))
    
    return this.request<SearchResult[]>(`/search?${params}`)
  }

  async searchIndexers(query: string, category?: string, sortBy?: SearchSortBy, sortDirection?: SearchSortDirection): Promise<SearchResult[]> {
    const params = new URLSearchParams({ query })
    if (category) params.append('category', category)
    if (sortBy) params.append('sortBy', sortBy)
    if (sortDirection) params.append('sortDirection', sortDirection)
    
    return this.request<SearchResult[]>(`/search/indexers?${params}`)
  }

  async searchByApi(apiId: string, query: string, category?: string): Promise<SearchResult[]> {
    const params = new URLSearchParams({ query })
    if (category) params.append('category', category)
    
    return this.request<SearchResult[]>(`/search/${apiId}?${params}`)
  }

  async testApiConnection(apiId: string): Promise<boolean> {
    return this.request<boolean>(`/search/test/${apiId}`, { method: 'POST' })
  }

  // Downloads API
  async getDownloads(): Promise<Download[]> {
    return this.request<Download[]>('/downloads')
  }

  async getDownload(id: string): Promise<Download> {
    return this.request<Download>(`/downloads/${id}`)
  }

  async startDownload(searchResult: SearchResult, downloadClientId: string): Promise<string> {
    return this.request<string>('/downloads', {
      method: 'POST',
      body: JSON.stringify({ searchResult, downloadClientId })
    })
  }

  async cancelDownload(id: string): Promise<boolean> {
    return this.request<boolean>(`/downloads/${id}`, { method: 'DELETE' })
  }

  async searchAndDownload(audiobookId: number): Promise<{
    success: boolean
    message?: string
    downloadId?: string
    indexerUsed?: string
    downloadClientUsed?: string
    searchResult?: SearchResult
  }> {
    return this.request<{
      success: boolean
      message?: string
      downloadId?: string
      indexerUsed?: string
      downloadClientUsed?: string
      searchResult?: SearchResult
    }>('/download/search-and-download', {
      method: 'POST',
      body: JSON.stringify({ audiobookId })
    })
  }

  async sendToDownloadClient(searchResult: SearchResult, downloadClientId?: string, audiobookId?: number): Promise<{
    downloadId: string
    message: string
  }> {
    return this.request<{
      downloadId: string
      message: string
    }>('/download/send', {
      method: 'POST',
      body: JSON.stringify({ searchResult, downloadClientId, audiobookId })
    })
  }

  // Download Queue API
  async getQueue(): Promise<QueueItem[]> {
    return this.request<QueueItem[]>('/download/queue')
  }

  async removeFromQueue(downloadId: string, downloadClientId?: string): Promise<{ message: string }> {
    const params = downloadClientId ? `?downloadClientId=${downloadClientId}` : ''
    return this.request<{ message: string }>(`/download/queue/${downloadId}${params}`, {
      method: 'DELETE'
    })
  }

  // API Configuration
  async getApiConfigurations(): Promise<ApiConfiguration[]> {
    return this.request<ApiConfiguration[]>('/configuration/apis')
  }

  async getApiConfiguration(id: string): Promise<ApiConfiguration> {
    return this.request<ApiConfiguration>(`/configuration/apis/${id}`)
  }

  async saveApiConfiguration(config: ApiConfiguration): Promise<string> {
    return this.request<string>('/configuration/apis', {
      method: 'POST',
      body: JSON.stringify(config)
    })
  }

  async deleteApiConfiguration(id: string): Promise<boolean> {
    return this.request<boolean>(`/configuration/apis/${id}`, { method: 'DELETE' })
  }

  // Download Client Configuration
  async getDownloadClientConfigurations(): Promise<DownloadClientConfiguration[]> {
    return this.request<DownloadClientConfiguration[]>('/configuration/download-clients')
  }

  async getDownloadClientConfiguration(id: string): Promise<DownloadClientConfiguration> {
    return this.request<DownloadClientConfiguration>(`/configuration/download-clients/${id}`)
  }

  async saveDownloadClientConfiguration(config: DownloadClientConfiguration): Promise<string> {
    return this.request<string>('/configuration/download-clients', {
      method: 'POST',
      body: JSON.stringify(config)
    })
  }

  async deleteDownloadClientConfiguration(id: string): Promise<boolean> {
    return this.request<boolean>(`/configuration/download-clients/${id}`, { method: 'DELETE' })
  }

  // Application Settings
  async getApplicationSettings(): Promise<ApplicationSettings> {
    return this.request<ApplicationSettings>('/configuration/settings')
  }

  async saveApplicationSettings(settings: ApplicationSettings): Promise<ApplicationSettings> {
    return this.request<ApplicationSettings>('/configuration/settings', {
      method: 'POST',
      body: JSON.stringify(settings)
    })
  }

  // Startup configuration (read + write) — backend exposes under /configuration/startupconfig
  async getStartupConfig(): Promise<import('@/types').StartupConfig> {
    // Make a direct fetch here to avoid calling `request()` which itself uses
    // `getStartupConfigCached()` (would cause a recursion / loop).
    const resp = await fetch(`${API_BASE_URL}/configuration/startupconfig`, { method: 'GET', credentials: 'include' })
    if (!resp.ok) {
      const txt = await resp.text().catch(() => '')
      const err = new Error(`Startup config fetch failed: ${resp.status} ${txt}`) as ErrorWithStatus
      err.status = resp.status
      throw err
    }
    const json = await resp.json().catch(() => null)
    return json as import('@/types').StartupConfig
  }

  async saveStartupConfig(config: import('@/types').StartupConfig): Promise<import('@/types').StartupConfig> {
    return this.request<import('@/types').StartupConfig>('/configuration/startupconfig', {
      method: 'POST',
      body: JSON.stringify(config)
    })
  }

  // Regenerate server-side API key. Returns the new API key in the response.
  async regenerateApiKey(): Promise<{ apiKey: string }> {
    return this.request<{ apiKey: string }>('/configuration/apikey/regenerate', { method: 'POST' })
  }

  // Amazon ASIN lookup
  async getAsinFromIsbn(isbn: string): Promise<{ success: boolean; asin?: string; error?: string }> {
    return this.request<{ success: boolean; asin?: string; error?: string }>(`/amazon/asin-from-isbn/${encodeURIComponent(isbn)}`)
  }

  // Audible Metadata API
  async getAudibleMetadata<T>(asin: string): Promise<T> {
    return this.request<T>(`/audible/metadata/${asin}`)
  }

  // Library API
  async getLibrary(): Promise<Audiobook[]> {
    return this.request<Audiobook[]>('/library')
  }

  async addToLibrary(metadata: AudibleBookMetadata, options?: { monitored?: boolean; qualityProfileId?: number; autoSearch?: boolean }): Promise<{ message: string; audiobook: Audiobook }> {
    const request = {
      metadata,
      monitored: options?.monitored ?? true,
      qualityProfileId: options?.qualityProfileId,
      autoSearch: options?.autoSearch ?? false
    }
    return this.request<{ message: string; audiobook: Audiobook }>('/library/add', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  }

  async getAudiobook(id: number): Promise<Audiobook> {
    return this.request<Audiobook>(`/library/${id}`)
  }

  async scanAudiobook(id: number, path?: string): Promise<{ message: string; scannedPath?: string; found: number; created: number; audiobook?: Audiobook; jobId?: string }> {
    return this.request(`/library/${id}/scan`, {
      method: 'POST',
      body: JSON.stringify({ path })
    })
  }

  async updateAudiobook(id: number, audiobook: Partial<Audiobook>): Promise<{ message: string; audiobook: Audiobook }> {
    return this.request<{ message: string; audiobook: Audiobook }>(`/library/${id}`, {
      method: 'PUT',
      body: JSON.stringify(audiobook)
    })
  }

  async removeFromLibrary(id: number): Promise<{ message: string; id: number }> {
    return this.request<{ message: string; id: number }>(`/library/${id}`, {
      method: 'DELETE'
    })
  }

  async bulkRemoveFromLibrary(ids: number[]): Promise<{ message: string; deletedCount: number; deletedImagesCount: number; ids: number[] }> {
    return this.request<{ message: string; deletedCount: number; deletedImagesCount: number; ids: number[] }>('/library/delete-bulk', {
      method: 'POST',
      body: JSON.stringify({ ids })
    })
  }

  async bulkUpdateAudiobooks(ids: number[], updates: Record<string, boolean | number | string>): Promise<{ message: string; results: Array<{ id: number; success: boolean; errors: string[] }> }> {
    return this.request<{ message: string; results: Array<{ id: number; success: boolean; errors: string[] }> }>('/library/bulk-update', {
      method: 'POST',
      body: JSON.stringify({ ids, updates })
    })
  }

  // File System API
  async browseDirectory(path?: string): Promise<{
    currentPath: string
    parentPath: string | null
    items: Array<{
      name: string
      path: string
      isDirectory: boolean
      lastModified: string
    }>
  }> {
    const params = path ? `?path=${encodeURIComponent(path)}` : ''
    return this.request(`/filesystem/browse${params}`)
  }

  async validatePath(path: string): Promise<{
    isValid: boolean
    exists: boolean
    isWritable: boolean
    message: string
  }> {
    return this.request(`/filesystem/validate?path=${encodeURIComponent(path)}`)
  }

  // Helper to convert relative image URLs to absolute
  getImageUrl(imageUrl: string | undefined): string {
    if (!imageUrl) return ''
    // If already absolute URL, return as is
    if (imageUrl.startsWith('http://') || imageUrl.startsWith('https://')) {
      return imageUrl
    }
    // Convert relative URL to absolute
    return `${BACKEND_BASE_URL}${imageUrl}`
  }

  // History API
  async getHistory(limit?: number, offset?: number): Promise<{
    history: History[]
    total: number
    limit: number
    offset: number
  }> {
    const params = new URLSearchParams()
    if (limit) params.append('limit', limit.toString())
    if (offset) params.append('offset', offset.toString())
    const queryString = params.toString()
    return this.request<{
      history: History[]
      total: number
      limit: number
      offset: number
    }>(`/history${queryString ? '?' + queryString : ''}`)
  }

  async getHistoryByAudiobookId(audiobookId: number): Promise<History[]> {
    return this.request<History[]>(`/history/audiobook/${audiobookId}`)
  }

  async getHistoryByEventType(eventType: string, limit?: number): Promise<History[]> {
    const params = limit ? `?limit=${limit}` : ''
    return this.request<History[]>(`/history/type/${eventType}${params}`)
  }

  async getHistoryBySource(source: string, limit?: number): Promise<History[]> {
    const params = limit ? `?limit=${limit}` : ''
    return this.request<History[]>(`/history/source/${source}${params}`)
  }

  async getRecentHistory(limit: number = 50): Promise<History[]> {
    return this.request<History[]>(`/history/recent?limit=${limit}`)
  }

  async deleteHistoryEntry(id: number): Promise<{ message: string; id: number }> {
    return this.request<{ message: string; id: number }>(`/history/${id}`, {
      method: 'DELETE'
    })
  }

  async clearAllHistory(): Promise<{ message: string; deletedCount: number }> {
    return this.request<{ message: string; deletedCount: number }>('/history/clear', {
      method: 'DELETE'
    })
  }

  async cleanupOldHistory(days: number = 90): Promise<{ message: string; deletedCount: number }> {
    return this.request<{ message: string; deletedCount: number }>(`/history/cleanup?days=${days}`, {
      method: 'DELETE'
    })
  }

  // Indexers API
  async getIndexers(): Promise<Indexer[]> {
    return this.request<Indexer[]>('/indexers')
  }

  async getIndexerById(id: number): Promise<Indexer> {
    return this.request<Indexer>(`/indexers/${id}`)
  }

  async createIndexer(indexer: Omit<Indexer, 'id' | 'createdAt' | 'updatedAt'>): Promise<Indexer> {
    return this.request<Indexer>('/indexers', {
      method: 'POST',
      body: JSON.stringify(indexer)
    })
  }

  async updateIndexer(id: number, indexer: Partial<Indexer>): Promise<Indexer> {
    return this.request<Indexer>(`/indexers/${id}`, {
      method: 'PUT',
      body: JSON.stringify(indexer)
    })
  }

  async deleteIndexer(id: number): Promise<{ message: string; id: number }> {
    return this.request<{ message: string; id: number }>(`/indexers/${id}`, {
      method: 'DELETE'
    })
  }

  async testIndexer(id: number): Promise<{ success: boolean; message: string; error?: string; indexer: Indexer }> {
    return this.request<{ success: boolean; message: string; error?: string; indexer: Indexer }>(`/indexers/${id}/test`, {
      method: 'POST'
    })
  }

  async toggleIndexer(id: number): Promise<Indexer> {
    return this.request<Indexer>(`/indexers/${id}/toggle`, {
      method: 'PUT'
    })
  }

  async getEnabledIndexers(): Promise<Indexer[]> {
    return this.request<Indexer[]>('/indexers/enabled')
  }

  // Remote Path Mappings
  async getRemotePathMappings(): Promise<RemotePathMapping[]> {
    return this.request<RemotePathMapping[]>('/remotepath')
  }

  async getRemotePathMappingById(id: number): Promise<RemotePathMapping> {
    return this.request<RemotePathMapping>(`/remotepath/${id}`)
  }

  async getRemotePathMappingsByClient(downloadClientId: string): Promise<RemotePathMapping[]> {
    return this.request<RemotePathMapping[]>(`/remotepath/client/${encodeURIComponent(downloadClientId)}`)
  }

  async createRemotePathMapping(mapping: Omit<RemotePathMapping, 'id' | 'createdAt' | 'updatedAt'>): Promise<RemotePathMapping> {
    return this.request<RemotePathMapping>('/remotepath', {
      method: 'POST',
      body: JSON.stringify(mapping)
    })
  }

  async updateRemotePathMapping(id: number, mapping: Partial<RemotePathMapping>): Promise<RemotePathMapping> {
    return this.request<RemotePathMapping>(`/remotepath/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ ...mapping, id })
    })
  }

  async deleteRemotePathMapping(id: number): Promise<void> {
    return this.request<void>(`/remotepath/${id}`, {
      method: 'DELETE'
    })
  }

  async translatePath(request: TranslatePathRequest): Promise<TranslatePathResponse> {
    return this.request<TranslatePathResponse>('/remotepath/translate', {
      method: 'POST',
      body: JSON.stringify(request)
    })
  }

  // System endpoints
  async getSystemInfo(): Promise<SystemInfo> {
    return this.request<SystemInfo>('/system/info')
  }

  async getStorageInfo(): Promise<StorageInfo> {
    return this.request<StorageInfo>('/system/storage')
  }

  async getServiceHealth(): Promise<ServiceHealth> {
    return this.request<ServiceHealth>('/system/health')
  }

  async getLogs(limit: number = 100): Promise<LogEntry[]> {
    return this.request<LogEntry[]>(`/system/logs?limit=${limit}`)
  }

  async downloadLogs(): Promise<void> {
    const url = `${API_BASE_URL}/system/logs/download`
    window.open(url, '_blank')
  }

  // Quality Profile endpoints
  async getQualityProfiles(): Promise<QualityProfile[]> {
    return this.request<QualityProfile[]>('/qualityprofile')
  }

  async getQualityProfileById(id: number): Promise<QualityProfile> {
    return this.request<QualityProfile>(`/qualityprofile/${id}`)
  }

  async getDefaultQualityProfile(): Promise<QualityProfile> {
    return this.request<QualityProfile>('/qualityprofile/default')
  }

  async createQualityProfile(profile: Omit<QualityProfile, 'id' | 'createdAt' | 'updatedAt'>): Promise<QualityProfile> {
    return this.request<QualityProfile>('/qualityprofile', {
      method: 'POST',
      body: JSON.stringify(profile)
    })
  }

  async updateQualityProfile(id: number, profile: Partial<QualityProfile>): Promise<QualityProfile> {
    return this.request<QualityProfile>(`/qualityprofile/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ ...profile, id })
    })
  }

  async deleteQualityProfile(id: number): Promise<{ message: string; id: number }> {
    return this.request<{ message: string; id: number }>(`/qualityprofile/${id}`, {
      method: 'DELETE'
    })
  }

  async scoreSearchResults(profileId: number, searchResults: SearchResult[]): Promise<QualityScore[]> {
    return this.request<QualityScore[]>(`/qualityprofile/${profileId}/score`, {
      method: 'POST',
      body: JSON.stringify(searchResults)
    })
  }

  // Antiforgery token for SPA (calls our new /api/antiforgery/token endpoint)
  async fetchAntiforgeryToken(): Promise<string | null> {
    try {
      // Include API key header when available to ensure token endpoints are reachable
      const headers: Record<string, string> = {}
      try {
        const sc = await getStartupConfigCached(2000)
        const apiKey = sc?.apiKey
        if (apiKey) headers['X-Api-Key'] = apiKey
      } catch {}

      const resp = await fetch(`${API_BASE_URL}/antiforgery/token`, { method: 'GET', credentials: 'include', headers })
      if (!resp.ok) return null
      const json = await resp.json()
      return json?.token ?? null
    } catch {
      return null
    }
  }

  // Account login - expects server to set auth cookie
  async login(username: string, password: string, rememberMe: boolean, csrfToken?: string): Promise<void> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' }
    if (csrfToken) headers['X-XSRF-TOKEN'] = csrfToken
    // Include API key when present so login requests that bypass request() still send the token
    try {
      const sc = await getStartupConfigCached(2000)
      const apiKey = sc?.apiKey
      if (apiKey) headers['X-Api-Key'] = apiKey
    } catch {}

    const resp = await fetch(`${API_BASE_URL}/account/login`, {
      method: 'POST',
      credentials: 'include',
      headers,
      body: JSON.stringify({ username, password, rememberMe })
    })

    if (resp.status === 429) {
      const body = await resp.json().catch(() => ({}))
      const retryAfter = body?.retryAfterSeconds ?? parseInt(resp.headers.get('Retry-After') || '0')
      const err: ErrorWithStatus = new Error('Too many login attempts');
      err.status = 429
      err.retryAfter = retryAfter
      throw err
    }

    if (!resp.ok) {
      const txt = await resp.text().catch(() => '')
      const err = new Error(`Login failed: ${resp.status} ${txt}`);
      (err as ErrorWithStatus).status = resp.status
      throw err
    }
  }

  // Current authenticated user (me)
  async getCurrentUser(): Promise<{ authenticated: boolean; name?: string }> {
    return this.request<{ authenticated: boolean; name?: string }>('/account/me')
  }

  async logout(): Promise<void> {
    await this.request<void>('/account/logout', { method: 'POST' })
  }

  // Admin users
  async getAdminUsers(): Promise<Array<{ id: number; username: string; email?: string; isAdmin: boolean; createdAt: string }>> {
    return this.request<Array<{ id: number; username: string; email?: string; isAdmin: boolean; createdAt: string }>>('/account/admins')
  }
}

export const apiService = new ApiService()

// Export individual indexer functions for convenience
export const getIndexers = () => apiService.getIndexers()
export const getIndexerById = (id: number) => apiService.getIndexerById(id)
export const createIndexer = (indexer: Omit<Indexer, 'id' | 'createdAt' | 'updatedAt'>) => apiService.createIndexer(indexer)
export const updateIndexer = (id: number, indexer: Partial<Indexer>) => apiService.updateIndexer(id, indexer)
export const deleteIndexer = (id: number) => apiService.deleteIndexer(id)
export const testIndexer = (id: number) => apiService.testIndexer(id)
export const toggleIndexer = (id: number) => apiService.toggleIndexer(id)
export const getEnabledIndexers = () => apiService.getEnabledIndexers()

// Export individual remote path mapping functions for convenience
export const getRemotePathMappings = () => apiService.getRemotePathMappings()
export const getRemotePathMappingById = (id: number) => apiService.getRemotePathMappingById(id)
export const getRemotePathMappingsByClient = (downloadClientId: string) => apiService.getRemotePathMappingsByClient(downloadClientId)
export const createRemotePathMapping = (mapping: Omit<RemotePathMapping, 'id' | 'createdAt' | 'updatedAt'>) => apiService.createRemotePathMapping(mapping)
export const updateRemotePathMapping = (id: number, mapping: Partial<RemotePathMapping>) => apiService.updateRemotePathMapping(id, mapping)
export const deleteRemotePathMapping = (id: number) => apiService.deleteRemotePathMapping(id)
export const translatePath = (request: TranslatePathRequest) => apiService.translatePath(request)
// Export individual system functions for convenience
export const getSystemInfo = () => apiService.getSystemInfo()
export const getStorageInfo = () => apiService.getStorageInfo()
export const getServiceHealth = () => apiService.getServiceHealth()
export const getLogs = (limit?: number) => apiService.getLogs(limit)
export const downloadLogs = () => apiService.downloadLogs()

// Export individual quality profile functions for convenience
export const getQualityProfiles = () => apiService.getQualityProfiles()
export const getQualityProfileById = (id: number) => apiService.getQualityProfileById(id)
export const getDefaultQualityProfile = () => apiService.getDefaultQualityProfile()
export const createQualityProfile = (profile: Omit<QualityProfile, 'id' | 'createdAt' | 'updatedAt'>) => apiService.createQualityProfile(profile)
export const updateQualityProfile = (id: number, profile: Partial<QualityProfile>) => apiService.updateQualityProfile(id, profile)
export const deleteQualityProfile = (id: number) => apiService.deleteQualityProfile(id)
export const scoreSearchResults = (profileId: number, searchResults: SearchResult[]) => apiService.scoreSearchResults(profileId, searchResults)


