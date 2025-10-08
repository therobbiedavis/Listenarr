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
  downloadType: string // "Torrent", "Usenet", or "DDL"
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
  status: 'Queued' | 'Downloading' | 'Paused' | 'Completed' | 'Failed' | 'Processing' | 'Ready'
  progress: number
  totalSize: number
  downloadedSize: number
  downloadPath: string
  finalPath: string
  startedAt: string
  completedAt?: string
  errorMessage?: string
  downloadClientId: string
  metadata: Record<string, unknown>
}

export interface QueueItem {
  id: string
  title: string
  author?: string
  series?: string
  seriesNumber?: string
  quality: string
  status: string // downloading, paused, queued, completed, failed
  progress: number // 0-100
  size: number // in bytes
  downloaded: number // in bytes
  downloadSpeed: number // bytes per second
  eta?: number // seconds remaining
  indexer?: string
  downloadClient: string
  downloadClientId: string
  downloadClientType: string
  addedAt: string
  errorMessage?: string
  canPause: boolean
  canRemove: boolean
  seeders?: number
  leechers?: number
  ratio?: number
  remotePath?: string // Path as seen by download client
  localPath?: string // Path translated for Listenarr
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
  settings: Record<string, unknown>
}

export interface RemotePathMapping {
  id: number
  downloadClientId: string
  name?: string
  remotePath: string
  localPath: string
  createdAt: string
  updatedAt: string
}

export interface TranslatePathRequest {
  downloadClientId: string
  remotePath: string
}

export interface TranslatePathResponse {
  downloadClientId: string
  remotePath: string
  localPath: string
  translated: boolean
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

export interface History {
  id: number
  audiobookId?: number
  audiobookTitle?: string
  eventType: string
  message?: string
  source?: string
  timestamp: string
  data?: string
}

export interface Indexer {
  id: number
  name: string
  type: string // "Torrent" or "Usenet"
  implementation: string // "Newznab", "Torznab", "Custom"
  url: string
  apiKey?: string
  categories?: string
  animeCategories?: string
  tags?: string
  enableRss: boolean
  enableAutomaticSearch: boolean
  enableInteractiveSearch: boolean
  enableAnimeStandardSearch: boolean
  isEnabled: boolean
  priority: number
  minimumAge: number
  retention: number
  maximumSize: number
  additionalSettings?: string
  createdAt: string
  updatedAt: string
  lastTestedAt?: string
  lastTestSuccessful?: boolean
  lastTestError?: string
}