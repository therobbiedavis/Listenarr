// Open Library API Service
// Documentation: https://openlibrary.org/dev/docs/api/search

export interface OpenLibraryBook {
  key: string;
  title: string;
  author_name?: string[];
  author_key?: string[];
  first_publish_year?: number;
  isbn?: string[];
  publisher?: string[];
  cover_i?: number;
  edition_count?: number;
  language?: string[];
  subject?: string[];
  ebook_access?: 'public' | 'borrowable' | 'printdisabled' | 'no_ebook';
  has_fulltext?: boolean;
  public_scan_b?: boolean;
}

export interface OpenLibrarySearchResponse {
  start: number;
  num_found: number;
  numFound?: number;
  numFoundExact?: boolean;
  docs: OpenLibraryBook[];
}

export interface BookSearchQuery {
  title?: string;
  author?: string;
  isbn?: string;
  general?: string; // For general keyword search
}

export class OpenLibraryService {
  private readonly baseUrl = 'https://openlibrary.org';

  /**
   * Search for books using various criteria
   */
  async searchBooks(query: BookSearchQuery, limit = 10, offset = 0): Promise<OpenLibrarySearchResponse> {
    const searchParams = new URLSearchParams();
    
    if (query.title) {
      searchParams.append('title', query.title);
    }
    
    if (query.author) {
      searchParams.append('author', query.author);
    }
    
    if (query.isbn) {
      searchParams.append('isbn', query.isbn);
    }
    
    if (query.general) {
      searchParams.append('q', query.general);
    }
    
    // If no specific fields, use title and author as general search
    if (!query.title && !query.author && !query.isbn && !query.general) {
      throw new Error('At least one search parameter is required');
    }
    
    // Add pagination
    searchParams.append('limit', limit.toString());
    searchParams.append('offset', offset.toString());
    
    // Request specific fields to optimize response
    const fields = [
      'key',
      'title',
      'author_name',
      'author_key',
      'first_publish_year',
      'isbn',
      'publisher',
      'cover_i',
      'edition_count',
      'language',
      'subject',
      'ebook_access',
      'has_fulltext',
      'public_scan_b'
    ];
    searchParams.append('fields', fields.join(','));
    
    const url = `${this.baseUrl}/search.json?${searchParams.toString()}`;
    
    try {
      const response = await fetch(url);
      
      if (!response.ok) {
        throw new Error(`Open Library API error: ${response.status} ${response.statusText}`);
      }
      
      const data: OpenLibrarySearchResponse = await response.json();
      return data;
    } catch (error) {
      console.error('Error searching Open Library:', error);
      throw new Error('Failed to search books. Please check your connection and try again.');
    }
  }

  /**
   * Search by title and author (most common use case)
   */
  async searchByTitleAndAuthor(title: string, author?: string, limit = 10): Promise<OpenLibrarySearchResponse> {
    return this.searchBooks({ title, author }, limit);
  }

  /**
   * Search by ISBN
   */
  async searchByISBN(isbn: string): Promise<OpenLibrarySearchResponse> {
    // Clean ISBN (remove hyphens and spaces)
    const cleanISBN = isbn.replace(/[-\s]/g, '');
    return this.searchBooks({ isbn: cleanISBN }, 5);
  }

  /**
   * General keyword search
   */
  async searchByKeywords(keywords: string, limit = 10): Promise<OpenLibrarySearchResponse> {
    return this.searchBooks({ general: keywords }, limit);
  }

  /**
   * Get cover image URL for a book
   */
  getCoverUrl(coverId: number, size: 'S' | 'M' | 'L' = 'M'): string {
    return `https://covers.openlibrary.org/b/id/${coverId}-${size}.jpg`;
  }

  /**
   * Get book URL on Open Library
   */
  getBookUrl(key: string): string {
    return `${this.baseUrl}${key}`;
  }

  /**
   * Extract ISBNs from a book record
   */
  getISBNs(book: OpenLibraryBook): string[] {
    return book.isbn || [];
  }

  /**
   * Get primary ISBN (ISBN-13 preferred, then ISBN-10)
   */
  getPrimaryISBN(book: OpenLibraryBook): string | null {
    const isbns = this.getISBNs(book);
    if (isbns.length === 0) return null;
    
    // Prefer ISBN-13 (13 digits)
    const isbn13 = isbns.find(isbn => isbn.replace(/[-\s]/g, '').length === 13);
    if (isbn13) return isbn13;
    
    // Fall back to ISBN-10
    const isbn10 = isbns.find(isbn => isbn.replace(/[-\s]/g, '').length === 10);
    if (isbn10) return isbn10;
    
    // Return first available
    return isbns[0] || null;
  }

  /**
   * Format authors for display
   */
  formatAuthors(book: OpenLibraryBook): string {
    if (!book.author_name || book.author_name.length === 0) {
      return 'Unknown Author';
    }
    
    if (book.author_name.length === 1) {
      return book.author_name[0] || 'Unknown Author';
    }
    
    if (book.author_name.length === 2) {
      return book.author_name.join(' & ');
    }
    
    // For more than 2 authors
    return `${book.author_name.slice(0, -1).join(', ')} & ${book.author_name[book.author_name.length - 1]}`;
  }

  /**
   * Create a search suggestion for finding the ASIN
   */
  createAsinSearchHint(book: OpenLibraryBook): string {
    const title = book.title;
    const author = this.formatAuthors(book);
    const isbn = this.getPrimaryISBN(book);
    
    let hint = `Search Amazon for: "${title}" by ${author}`;
    
    if (isbn) {
      hint += ` (ISBN: ${isbn})`;
    }
    
    return hint;
  }

  /**
   * Validate search input
   */
  validateSearchInput(query: BookSearchQuery): { valid: boolean; message?: string } {
    const hasTitle = query.title && query.title.trim().length > 0;
    const hasAuthor = query.author && query.author.trim().length > 0;
    const hasISBN = query.isbn && query.isbn.trim().length > 0;
    const hasGeneral = query.general && query.general.trim().length > 0;
    
    if (!hasTitle && !hasAuthor && !hasISBN && !hasGeneral) {
      return {
        valid: false,
        message: 'Please enter a book title, author name, or ISBN'
      };
    }
    
    // Validate ISBN format if provided
    if (hasISBN) {
      const cleanISBN = query.isbn!.replace(/[-\s]/g, '');
      if (!/^\d{10}(\d{3})?$/.test(cleanISBN)) {
        return {
          valid: false,
          message: 'Please enter a valid ISBN (10 or 13 digits)'
        };
      }
    }
    
    return { valid: true };
  }
}

// Export singleton instance
export const openLibraryService = new OpenLibraryService();