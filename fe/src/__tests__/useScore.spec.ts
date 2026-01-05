import { describe, it, expect } from 'vitest'
import { getScoreBreakdownTooltip } from '@/composables/useScore'
import type { QualityScore, SearchResult } from '@/types'

describe('useScore composable', () => {
  it('includes Smart composite breakdown when provided', () => {
    const fakeResult = {
      id: 'r1',
      title: 'T',
      artist: '',
      album: '',
      category: '',
      source: '',
      publishedDate: '',
      format: '',
      size: 0,
      magnetLink: '',
      torrentUrl: '',
      nzbUrl: '',
      downloadType: '',
      quality: '',
    } as unknown as SearchResult
    const score: QualityScore = {
      searchResult: fakeResult,
      totalScore: 100,
      scoreBreakdown: { Quality: 90 },
      rejectionReasons: [],
      isRejected: false,
      smartScore: 1234.5,
      smartScoreBreakdown: { Quality: 90000, Format: 8500, Seed: 2000 },
    }

    const tooltip = getScoreBreakdownTooltip(score)
    expect(tooltip).toContain('Smart (composite) breakdown:')
    // Normalized quality should appear (90000 -> 90 when divided by 1000)
    expect(tooltip).toContain('Quality: +90')
    // Smart total now is the average of normalized components: Quality=90, Format=85, Seed=20 -> avg=~65
    expect(tooltip).toContain('Smart Total: 65')
  })
})
