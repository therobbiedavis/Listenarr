import { describe, it, expect, beforeEach } from 'vitest'
import * as cache from '@/services/startupConfigCache'
import { apiService } from '@/services/api'

// Mock apiService.getStartupConfig with a delayed resolver
let originalGet: unknown

beforeEach(() => {
  cache.resetCache()
  originalGet = (apiService as unknown as { getStartupConfig?: unknown }).getStartupConfig
})

describe('startupConfigCache', () => {
  it('deduplicates concurrent calls', async () => {
    let resolve: (value: unknown) => void
    const p = new Promise<unknown>((res) => {
      resolve = res
    })
    ;(apiService as unknown as { getStartupConfig?: () => Promise<unknown> }).getStartupConfig =
      () => {
        return p
      }

    // Start multiple concurrent callers
    const callers = Promise.all([
      cache.getStartupConfigCached(),
      cache.getStartupConfigCached(),
      cache.getStartupConfigCached(),
    ])

    // let the calls be inflight for a moment
    setTimeout(() => resolve({ authenticationRequired: 'Enabled' }), 50)

    const results = await callers
    expect(results.length).toBe(3)
    // fetchCount should be exactly 1
    expect(cache.fetchCount).toBe(1)
  })
})

// restore
const restore = originalGet as unknown
if (restore) {
  ;(apiService as unknown as { getStartupConfig?: unknown }).getStartupConfig = restore
}
