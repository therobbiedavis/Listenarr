export interface AudnexusBook {
  asin: string
  title: string
  authors: Author[]
  narrators: Narrator[]
  description: string
  summary: string
  publisherName: string
  publishDate: string
  releaseDate: string
  language: string
  genres: Genre[]
  tags: Tag[]
  seriesPrimary?: Series
  seriesSecondary?: Series[]
  image: string
  runtimeLengthMin: number
  runtimeLengthFormat: string
  formatType: string
  copyright: string
  isbn: string
  asinList: string[]
  region: string
}

export interface Author {
  asin: string
  name: string
}

export interface Narrator {
  asin: string
  name: string
}

export interface Genre {
  asin: string
  name: string
  type: string
}

export interface Tag {
  asin: string
  name: string
}

export interface Series {
  asin: string
  name: string
  position: string
}

export interface AudnexusSearchResult {
  asin: string
  title: string
  author: string
  narrator: string
  description: string
  image: string
  publishDate: string
  runtimeLengthMin: number
  runtimeLengthFormat: string
  genres: string[]
  series?: {
    name: string
    position: string
  }
}

class AudnexusService {
  private readonly baseUrl = 'https://api.audnex.us'

  private async request<T>(endpoint: string): Promise<T> {
    const url = `${this.baseUrl}${endpoint}`
    
    try {
      const response = await fetch(url, {
        headers: {
          'Accept': 'application/json',
          'User-Agent': 'Listenarr/1.0.0'
        }
      })
      
      if (!response.ok) {
        if (response.status === 404) {
          throw new Error('Book not found with the provided ASIN')
        }
        throw new Error(`Audnexus API error: ${response.status} ${response.statusText}`)
      }
      
      return await response.json()
    } catch (error) {
      console.error('Audnexus API request failed:', error)
      throw error
    }
  }

  async searchByAsin(asin: string): Promise<AudnexusSearchResult> {
    // Validate ASIN format (basic validation)
    if (!asin || asin.length !== 10) {
      throw new Error('Invalid ASIN format. ASIN must be 10 characters long.')
    }

    const book = await this.request<AudnexusBook>(`/books/${asin}`)
    
    // Transform the detailed book data into our search result format
    return {
      asin: book.asin,
      title: book.title,
      author: book.authors.map(a => a.name).join(', '),
      narrator: book.narrators.map(n => n.name).join(', '),
      description: book.description || book.summary,
      image: book.image,
      publishDate: book.publishDate || book.releaseDate,
      runtimeLengthMin: book.runtimeLengthMin,
      runtimeLengthFormat: book.runtimeLengthFormat,
      genres: book.genres.map(g => g.name),
      series: book.seriesPrimary ? {
        name: book.seriesPrimary.name,
        position: book.seriesPrimary.position
      } : undefined
    }
  }

  async getBookDetails(asin: string): Promise<AudnexusBook> {
    return this.request<AudnexusBook>(`/books/${asin}`)
  }

  // Helper method to validate ASIN format
  validateAsin(asin: string): boolean {
    // Basic ASIN validation - 10 characters, alphanumeric
    const asinRegex = /^[A-Z0-9]{10}$/
    return asinRegex.test(asin.toUpperCase())
  }

  // Helper method to format runtime
  formatRuntime(minutes: number): string {
    const hours = Math.floor(minutes / 60)
    const mins = minutes % 60
    return `${hours}h ${mins}m`
  }
}

export const audnexusService = new AudnexusService()