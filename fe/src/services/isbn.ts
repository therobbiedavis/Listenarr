// ISBN Service using Open Library API
import { logger } from '@/utils/logger'

interface OpenLibraryAuthor {
  name: string
}

interface OpenLibraryPublisher {
  name: string
}

interface OpenLibrarySubject {
  name: string
}

interface OpenLibraryAuthorRef {
  key?: string
}

import { useConfigurationStore } from '@/stores/configuration'
import { createAmazonSearchUrl } from '@/utils/marketDomains'
import { errorTracking } from '@/services/errorTracking'

export interface ISBNBook {
  isbn: string
  title: string
  authors: string[]
  published_date?: string
  publishers?: string[]
  number_of_pages?: number
  subjects?: string[]
  cover_url?: string
  description?: string
  amazon_search_url: string
}

export interface ISBNSearchResult {
  book: ISBNBook | null
  found: boolean
  error?: string
}

class ISBNService {
  private readonly baseUrl = 'https://openlibrary.org'

  /**
   * Validate ISBN format (both ISBN-10 and ISBN-13)
   */
  validateISBN(isbn: string): boolean {
    const cleaned = isbn.replace(/[-\s]/g, '')

    // ISBN-10: 10 digits, last can be X
    if (/^\d{9}[\dX]$/i.test(cleaned)) {
      return this.validateISBN10(cleaned)
    }

    // ISBN-13: 13 digits
    if (/^\d{13}$/.test(cleaned)) {
      return this.validateISBN13(cleaned)
    }

    return false
  }

  /**
   * Validate ISBN-10 checksum
   */
  private validateISBN10(isbn: string): boolean {
    if (isbn.length !== 10) return false

    let sum = 0
    for (let i = 0; i < 9; i++) {
      const char = isbn[i]
      if (!char || !/\d/.test(char)) return false
      sum += parseInt(char, 10) * (10 - i)
    }
    const lastChar = isbn[9]
    if (!lastChar) return false

    const checksum = lastChar.toUpperCase() === 'X' ? 10 : parseInt(lastChar, 10)
    if (isNaN(checksum)) return false

    sum += checksum
    return sum % 11 === 0
  }

  /**
   * Validate ISBN-13 checksum
   */
  private validateISBN13(isbn: string): boolean {
    if (isbn.length !== 13) return false

    let sum = 0
    for (let i = 0; i < 12; i++) {
      const char = isbn[i]
      if (!char || !/\d/.test(char)) return false
      const digit = parseInt(char, 10)
      sum += i % 2 === 0 ? digit : digit * 3
    }
    const lastChar = isbn[12]
    if (!lastChar || !/\d/.test(lastChar)) return false

    const checksum = parseInt(lastChar, 10)
    return (10 - (sum % 10)) % 10 === checksum
  }

  /**
   * Detect if a string looks like an ISBN
   */
  detectISBN(input: string): boolean {
    const cleaned = input.replace(/[-\s]/g, '')
    return (
      /^\d{10}$/.test(cleaned) ||
      /^\d{9}[\dX]$/i.test(cleaned) ||
      /^\d{13}$/.test(cleaned) ||
      /^978\d{10}$/.test(cleaned) ||
      /^979\d{10}$/.test(cleaned)
    )
  }

  /**
   * Clean and format ISBN
   */
  cleanISBN(isbn: string): string {
    return isbn.replace(/[-\s]/g, '').toUpperCase()
  }

  /**
   * Search for book by ISBN using Open Library API
   */
  async searchByISBN(isbn: string): Promise<ISBNSearchResult> {
    const cleanedISBN = this.cleanISBN(isbn)

    if (!this.validateISBN(cleanedISBN)) {
      return {
        book: null,
        found: false,
        error: 'Invalid ISBN format',
      }
    }

    try {
      // Try the Books API first
      const bookUrl = `${this.baseUrl}/api/books?bibkeys=ISBN:${cleanedISBN}&format=json&jscmd=data`
      const bookResponse = await fetch(bookUrl)

      if (!bookResponse.ok) {
        throw new Error(`HTTP error! status: ${bookResponse.status}`)
      }

      const bookData = await bookResponse.json()
      const bookKey = `ISBN:${cleanedISBN}`

      if (bookData[bookKey]) {
        const book = bookData[bookKey]
        const result: ISBNBook = {
          isbn: cleanedISBN,
          title: book.title || 'Unknown Title',
          authors: book.authors?.map((author: OpenLibraryAuthor) => author.name) || [],
          published_date: book.publish_date,
          publishers: book.publishers?.map((pub: OpenLibraryPublisher) => pub.name) || [],
          number_of_pages: book.number_of_pages,
          subjects: book.subjects?.map((subject: OpenLibrarySubject) => subject.name) || [],
          cover_url: book.cover?.large || book.cover?.medium || book.cover?.small,
          description: book.excerpts?.[0]?.text,
          amazon_search_url: this.createAmazonSearchUrl(book.title, book.authors?.[0]?.name),
        }

        return {
          book: result,
          found: true,
        }
      }

      // If not found in Books API, try the ISBN API
      const isbnUrl = `${this.baseUrl}/isbn/${cleanedISBN}.json`
      const isbnResponse = await fetch(isbnUrl)

      if (!isbnResponse.ok) {
        return {
          book: null,
          found: false,
          error: 'Book not found',
        }
      }

      const isbnData = await isbnResponse.json()

      // Get author details
      const authors = await this.getAuthors(isbnData.authors || [])

      const result: ISBNBook = {
        isbn: cleanedISBN,
        title: isbnData.title || 'Unknown Title',
        authors: authors,
        published_date: isbnData.publish_date,
        publishers: isbnData.publishers || [],
        number_of_pages: isbnData.number_of_pages,
        subjects: isbnData.subjects || [],
        cover_url: isbnData.covers?.[0]
          ? `https://covers.openlibrary.org/b/id/${isbnData.covers[0]}-L.jpg`
          : undefined,
        amazon_search_url: this.createAmazonSearchUrl(isbnData.title, authors[0]),
      }

      return {
        book: result,
        found: true,
      }
    } catch (error) {
      errorTracking.captureException(error as Error, {
        component: 'ISBNService',
        operation: 'search',
        metadata: { isbn },
      })
      return {
        book: null,
        found: false,
        error: error instanceof Error ? error.message : 'Search failed',
      }
    }
  }

  /**
   * Get author names from Open Library author references
   */
  private async getAuthors(authorRefs: OpenLibraryAuthorRef[]): Promise<string[]> {
    const authors: string[] = []

    for (const authorRef of authorRefs.slice(0, 3)) {
      // Limit to first 3 authors
      try {
        if (typeof authorRef === 'string') {
          // If it's just a string, use it directly
          authors.push(authorRef)
        } else if (authorRef.key) {
          // If it's a reference, fetch the author details
          const authorUrl = `${this.baseUrl}${authorRef.key}.json`
          const response = await fetch(authorUrl)
          if (response.ok) {
            const authorData = await response.json()
            if (authorData.name) {
              authors.push(authorData.name)
            }
          }
        }
      } catch (error) {
        logger.warn('Failed to fetch author:', error)
      }
    }

    return authors
  }

  /**
   * Create Amazon search URL for finding the audiobook
   */
  private createAmazonSearchUrl(title: string, author?: string): string {
    const configStore = useConfigurationStore()
    const region = (configStore.applicationSettings as unknown as { region?: string })?.region
    return createAmazonSearchUrl(title, author, region)
  }

  /**
   * Generate search hint for Amazon
   */
  createAmazonSearchHint(book: ISBNBook): string {
    const authorText = book.authors.length > 0 ? ` by ${book.authors[0]}` : ''
    return `Search Amazon for: "${book.title}${authorText}" audiobook`
  }
}

export const isbnService = new ISBNService()
export default isbnService
