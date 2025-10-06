import type { 
  SearchResult, 
  Download, 
  ApiConfiguration, 
  DownloadClientConfiguration, 
  ApplicationSettings,
  Audiobook
} from '@/types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'
const BACKEND_BASE_URL = API_BASE_URL.replace('/api', '') // e.g., http://localhost:5146

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
      
      return await response.json()
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

  async saveApplicationSettings(settings: ApplicationSettings): Promise<void> {
    await this.request<void>('/configuration/settings', {
      method: 'POST',
      body: JSON.stringify(settings)
    })
  }

  // Amazon ASIN lookup
  async getAsinFromIsbn(isbn: string): Promise<{ success: boolean; asin?: string; error?: string }> {
    return this.request<{ success: boolean; asin?: string; error?: string }>(`/amazon/asin-from-isbn/${encodeURIComponent(isbn)}`)
  }

  // Library API
  async getLibrary(): Promise<Audiobook[]> {
    return this.request<Audiobook[]>('/library')
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
}

export const apiService = new ApiService()