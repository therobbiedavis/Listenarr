import { apiService } from './api'

type StartupConfig = import('@/types').StartupConfig

let _cache: StartupConfig | null = null
let _cacheTs = 0
let _inflight: Promise<StartupConfig | null> | null = null
// Expose a simple counter for diagnostics/tests
export let fetchCount = 0

export async function getStartupConfigCached(ttlMs = 5000): Promise<StartupConfig | null> {
  const now = Date.now()
  if (_cache && (now - _cacheTs) <= ttlMs) return _cache

  if (!_inflight) {
    fetchCount++
    _inflight = apiService.getStartupConfig()
      .then(cfg => {
        _cache = cfg
        _cacheTs = Date.now()
        return cfg
      })
      .catch(() => {
        // swallow; leave cache as-is
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
