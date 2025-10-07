import type { 
  SearchResult, 
  Download, 
  ApiConfiguration, 
  DownloadClientConfiguration, 
  ApplicationSettings,
  Audiobook,
  History,
  Indexer,
  QueueItem
} from '@/types'

// In development, use relative URLs (proxied by Vite to avoid CORS)
// In production, use the configured API base URL
const API_BASE_URL = import.meta.env.DEV 
  ? '/api' 
  : (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5146/api')
const BACKEND_BASE_URL = import.meta.env.DEV
  ? ''
  : API_BASE_URL.replace('/api', '')

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
      ...options,
    }

    try {
      const response = await fetch(url, config)
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }
      
      // Handle empty responses (204 No Content or empty body)
      const text = await response.text()
      if (!text || text.trim().length === 0) {
        return null as T
      }
      
      return JSON.parse(text) as T
    } catch (error) {
      console.error('API request failed:', error)
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

  async searchIndexers(query: string, category?: string): Promise<SearchResult[]> {
    const params = new URLSearchParams({ query })
    if (category) params.append('category', category)
    
    return this.request<SearchResult[]>(`/search/indexers?${params}`)
  }

  async searchByApi(apiId: string, query: string, category?: string): Promise<SearchResult[]> {
    const params = new URLSearchParams({ query })
    if (category) params.append('category', category)
    
    return this.request<SearchResult[]>(`/search/api/${apiId}?${params}`)
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

  async sendToDownloadClient(searchResult: SearchResult, downloadClientId?: string): Promise<{
    downloadId: string
    message: string
  }> {
    return this.request<{
      downloadId: string
      message: string
    }>('/download/send', {
      method: 'POST',
      body: JSON.stringify({ searchResult, downloadClientId })
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

  async addToLibrary(audiobook: Partial<Audiobook>): Promise<{ message: string; audiobook: Audiobook }> {
    return this.request<{ message: string; audiobook: Audiobook }>('/library/add', {
      method: 'POST',
      body: JSON.stringify(audiobook)
    })
  }

  async getAudiobook(id: number): Promise<Audiobook> {
    return this.request<Audiobook>(`/library/${id}`)
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