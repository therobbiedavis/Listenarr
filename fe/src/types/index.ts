export interface SearchResult {
  id: string
  title: string
  artist: string
  album: string
  category: string
  size: number
  seeders: number
  leechers: number
  magnetLink: string
  torrentUrl: string
  nzbUrl: string
  source: string
  publishedDate: string
  quality: string
  format: string
  // Extended audiobook metadata (optional)
  description?: string
  publisher?: string
  language?: string
  runtime?: number
  narrator?: string
  imageUrl?: string
  asin?: string
  series?: string
  seriesNumber?: string
  isEnriched?: boolean
}

export interface Download {
  id: string
  title: string
  artist: string
  album: string
  originalUrl: string
  status: 'Queued' | 'Downloading' | 'Completed' | 'Failed' | 'Processing' | 'Ready'
  progress: number
  totalSize: number
  downloadedSize: number
  downloadPath: string
  finalPath: string
  startedAt: string
  completedAt?: string
  errorMessage?: string
  downloadClientId: string
  metadata: Record<string, any>
}

export interface ApiConfiguration {
  id: string
  name: string
  baseUrl: string
  apiKey: string
  type: 'torrent' | 'nzb'
  isEnabled: boolean
  priority: number
  headers: Record<string, string>
  parameters: Record<string, string>
  rateLimitPerMinute?: string
  createdAt: string
  lastUsed?: string
}

export interface DownloadClientConfiguration {
  id: string
  name: string
  type: 'qbittorrent' | 'transmission' | 'sabnzbd' | 'nzbget'
  host: string
  port: number
  username: string
  password: string
  downloadPath: string
  useSSL: boolean
  isEnabled: boolean
  settings: Record<string, any>
}

export interface ApplicationSettings {
  outputPath: string
  fileNamingPattern: string
  enableMetadataProcessing: boolean
  enableCoverArtDownload: boolean
  audnexusApiUrl: string
  maxConcurrentDownloads: number
  pollingIntervalSeconds: number
  enableNotifications: boolean
  allowedFileExtensions: string[]
}

export interface AudibleBookMetadata {
  title: string
  subtitle?: string
  authors: string[]
  publishYear?: string
  series?: string
  seriesNumber?: string
  description?: string
  genres?: string[]
  tags?: string[]
  narrators?: string[]
  isbn?: string
  asin: string
  publisher?: string
  language?: string
  runtime?: number
  version?: string
  imageUrl?: string
  explicit?: boolean
  abridged?: boolean
  source?: string
}

export interface Audiobook {
  id: number
  title: string
  subtitle?: string
  authors?: string[]
  publishYear?: string
  series?: string
  seriesNumber?: string
  description?: string
  genres?: string[]
  tags?: string[]
  narrators?: string[]
  isbn?: string
  asin?: string
  publisher?: string
  language?: string
  runtime?: number
  version?: string
  imageUrl?: string
  explicit?: boolean
  abridged?: boolean
  monitored?: boolean
  filePath?: string
  fileSize?: number
  quality?: string
}