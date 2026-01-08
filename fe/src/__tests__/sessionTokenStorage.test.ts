import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { sessionTokenManager } from '@/utils/sessionToken'

describe('sessionTokenManager storage propagation', () => {
  beforeEach(() => {
    // Ensure clean localStorage before each test
    try {
      localStorage.removeItem('listenarr_session_token')
    } catch {}
  })

  afterEach(() => {
    try {
      localStorage.removeItem('listenarr_session_token')
    } catch {}
  })

  it('notifies subscribers when storage is changed (cross-tab)', () => {
    const events: Array<string | null> = []
    const unsub = sessionTokenManager.onTokenChange((token) => {
      events.push(token)
    })

    // Simulate another tab writing to localStorage by dispatching a StorageEvent
    const token = 'abc123'
    // Manually set localStorage (emulating other tab)
    localStorage.setItem('listenarr_session_token', token)
    // Emit storage event
    const ev = new StorageEvent('storage', { key: 'listenarr_session_token', newValue: token })
    window.dispatchEvent(ev)

    expect(events.length).toBeGreaterThanOrEqual(1)
    expect(events[events.length - 1]).toBe(token)

    // Now simulate removal
    localStorage.removeItem('listenarr_session_token')
    const ev2 = new StorageEvent('storage', { key: 'listenarr_session_token', newValue: null })
    window.dispatchEvent(ev2)

    expect(events[events.length - 1]).toBe(null)

    unsub()
  })
})
