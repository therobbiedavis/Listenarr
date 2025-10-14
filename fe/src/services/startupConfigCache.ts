import { apiService } from './api'

type StartupConfig = import('@/types').StartupConfig

let _cache: StartupConfig | null = null
let _cacheTs = 0
let _inflight: Promise<StartupConfig | null> | null = null
// Expose a simple counter for diagnostics/tests
export let fetchCount = 0

export async function getStartupConfigCached(ttlMs = 5000): Promise<StartupConfig | null> {
  const now = Date.now()
  // If we have a recent cached value (even if it's null from a previous failed fetch),
  // return it to avoid repeated immediate retries.
  if (_cacheTs !== 0 && (now - _cacheTs) <= ttlMs) return _cache

  if (!_inflight) {
    fetchCount++
    _inflight = apiService.getStartupConfig()
      .then(cfg => {
        _cache = cfg
        _cacheTs = Date.now()
        return cfg
      })
      .catch(() => {
        // On error (including 401 unauthorized), cache the null result for the TTL
        // so we don't immediately hammer the backend with repeated requests.
        _cache = null
        _cacheTs = Date.now()
        return null
      })
      .finally(() => { _inflight = null })
  }

  return _inflight
}

export function resetCache() {
  _cache = null
  _cacheTs = 0
  _inflight = null
  fetchCount = 0
}

// Synchronous access to the cached startup config (may be null)
export function getCachedStartupConfig(): StartupConfig | null {
  return _cache
}
