import { describe, it, expect } from 'vitest'
import { getScoreBreakdownTooltip } from '@/composables/useScore'
import type { QualityScore, SearchResult } from '@/types'

describe('useScore composable - rejection behavior', () => {
  it('returns only rejection reason for rejected scores', () => {
    const fakeResult = { id: 'r1', title: 'T', artist: '', album: '', category: '', source: '', publishedDate: '', format: '', size: 0, magnetLink: '', torrentUrl: '', nzbUrl: '', downloadType: '', quality: '' } as unknown as SearchResult
    const score: QualityScore = {
      searchResult: fakeResult,
      totalScore: -1,
      scoreBreakdown: {},
      rejectionReasons: ['Low seeders'],
      isRejected: true
    }

    const tooltip = getScoreBreakdownTooltip(score)
    expect(tooltip).toBe('Rejected: Low seeders')
  })
})