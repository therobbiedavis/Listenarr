import { apiService } from './api'

export interface AsinLookupResponse {
  success: boolean
  asin?: string
  error?: string
}

export const amazonService = {
  async getAsinFromIsbn(isbn: string): Promise<AsinLookupResponse> {
    try {
      const data = await apiService['request']<AsinLookupResponse>(`/amazon/asin-from-isbn/${encodeURIComponent(isbn)}`)
      return data
    } catch (error) {
      const message = error instanceof Error ? error.message : 'ASIN lookup failed'
      return { success: false, error: message }
    }
  }
}

export default amazonService
