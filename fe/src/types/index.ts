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
  score?: number
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
  qualityProfileId?: number
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

export interface SystemInfo {
  version: string
  operatingSystem: string
  runtime: string
  uptime: string
  memory: MemoryInfo
  cpu: CpuInfo
  startTime: string
}

export interface MemoryInfo {
  usedBytes: number
  totalBytes: number
  freeBytes: number
  usedPercentage: number
  usedFormatted: string
  totalFormatted: string
  freeFormatted: string
}

export interface CpuInfo {
  usagePercentage: number
  processorCount: number
}

export interface StorageInfo {
  usedBytes: number
  totalBytes: number
  freeBytes: number
  usedPercentage: number
  usedFormatted: string
  totalFormatted: string
  freeFormatted: string
  driveName: string
  status: string
}

export interface ServiceHealth {
  status: string // "healthy", "warning", "error", "unknown"
  version: string
  uptime: string
  downloadClients: DownloadClientHealth
  externalApis: ExternalApiHealth
}

export interface DownloadClientHealth {
  status: string
  connected: number
  total: number
  clients: ClientStatus[]
}

export interface ExternalApiHealth {
  status: string
  connected: number
  total: number
  apis: ApiStatus[]
}

export interface ClientStatus {
  name: string
  status: string // "connected", "disconnected", "unknown"
  type?: string
}

export interface ApiStatus {
  name: string
  status: string // "connected", "disconnected", "unknown"
  enabled: boolean
}

export interface LogEntry {
  id: string
  timestamp: string // ISO date string
  level: string // "Info", "Warning", "Error", "Debug"
  message: string
  exception?: string
  source?: string
}

export interface QualityProfile {
  id?: number
  name: string
  description?: string
  qualities: QualityDefinition[]
  cutoffQuality?: string
  minimumSize?: number // MB (optional - no minimum if not set)
  maximumSize?: number // MB (optional - no maximum if not set)
  preferredFormats?: string[] // e.g., ["m4b", "mp3", "m4a", "flac", "opus"]
  preferredWords?: string[] // Words that increase score
  mustNotContain?: string[] // Instant rejection
  mustContain?: string[] // Must be present
  preferredLanguages?: string[] // e.g., ["English", "Spanish"]
  minimumSeeders?: number
  isDefault?: boolean
  preferNewerReleases?: boolean
  maximumAge?: number // days (0 = no limit)
  createdAt?: string
  updatedAt?: string
}

export interface QualityDefinition {
  quality: string // e.g., "320kbps", "192kbps", "lossless"
  allowed: boolean
  priority: number // Lower = higher priority
}

export interface QualityScore {
  searchResult: SearchResult
  totalScore: number
  scoreBreakdown: Record<string, number>
  rejectionReasons: string[]
  isRejected: boolean
}

export type SearchSortBy = 'Seeders' | 'Size' | 'PublishedDate' | 'Title' | 'Source' | 'Quality'

export type SearchSortDirection = 'Ascending' | 'Descending'

