import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { describe, it, expect } from 'vitest'
import ManualSearchModal from '@/components/ManualSearchModal.vue'

describe('ManualSearchModal.vue', () => {
  const stubs = {
    PhMagnifyingGlass: true,
    PhX: true,
    PhSpinner: true,
    PhArrowClockwise: true,
    PhArrowUp: true,
    PhArrowDown: true,
    PhXCircle: true,
    PhDownloadSimple: true,
    PhArrowsDownUp: true,
    // Ensure ScorePopover renders its default slot in tests so the inner badge is present
    ScorePopover: { template: '<div><slot /></div>' }
  }

  it('uses details page for Usenet title links instead of direct NZB', async () => {
    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: null }, global: { stubs } })

    // Set a usenet-style result where id is an informational URL that should be used for the title link
    ;(wrapper.vm as any).results = [{
      id: 'https://indexer/info/123',
      title: 'Test Usenet',
      downloadType: 'Usenet',
      resultUrl: '',
      sourceLink: 'https://indexer/info/123',
      nzbUrl: 'https://indexer/download/123.nzb',
      source: 'altHUB',
      size: 123
    }]

    await nextTick()

    const anchor = wrapper.find('a.title-text')
    expect(anchor.exists()).toBe(true)
    expect(anchor.attributes('href')).toBe('https://indexer/info/123')
  })

  it('does not show language badge when language is Unknown', async () => {
    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: null }, global: { stubs } })

    ;(wrapper.vm as any).results = [{
      id: 'u2',
      title: 'Lang Test',
      language: 'Unknown',
      downloadType: 'Usenet',
      resultUrl: 'https://indexer/info/2',
      source: 'alt',
      size: 0
    }]

    await nextTick()

    const langBadge = wrapper.find('.language-badge')
    expect(langBadge.exists()).toBe(false)
  })

  it('does not show duplicate format fallback when format equals quality', async () => {
    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: null }, global: { stubs } })

    ;(wrapper.vm as any).results = [{
      id: 'q1',
      title: 'Format Fallback Test',
      quality: 'FLAC',
      format: 'FLAC',
      downloadType: 'Torrent',
      resultUrl: 'https://indexer/info/4',
      source: 'test',
      size: 0
    }]

    await nextTick()

    const badge = wrapper.find('.col-quality .quality-badge')
    expect(badge.exists()).toBe(true)
    expect(badge.text()).toContain('FLAC')
    // Should not contain duplicate 'FLAC' after the dot
    expect(badge.text()).not.toContain('FLAC · FLAC')
  })

  it('shows rejection reason instead of score for rejected results', async () => {
    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: null }, global: { stubs } })

    const fake = {
      id: 'r3',
      title: 'Rejected Test',
      downloadType: 'Torrent',
      resultUrl: 'https://indexer/info/3',
      source: 'test',
      size: 0
    }

    ;(wrapper.vm as any).results = [fake]

    const scoreObj: any = {
      searchResult: fake,
      totalScore: -1,
      scoreBreakdown: {},
      rejectionReasons: ['No seeds'],
      isRejected: true
    }

    // Try to set via .value (ref) when available
    if ((wrapper.vm as any).qualityScores && (wrapper.vm as any).qualityScores.value && typeof (wrapper.vm as any).qualityScores.value.set === 'function') {
      (wrapper.vm as any).qualityScores.value.set('r3', scoreObj)
    }

    // Also set directly on the unwrapped proxy for compatibility with test runner behavior
    if ((wrapper.vm as any).qualityScores && typeof (wrapper.vm as any).qualityScores.set === 'function') {
      (wrapper.vm as any).qualityScores.set('r3', scoreObj)
    }

    await nextTick()

    const badge = wrapper.find('.col-score .score-badge.rejected')
    expect(badge.exists()).toBe(true)
    // Badge should read 'Rejected'
    expect(badge.text()).toContain('Rejected')
    // The title/hover should contain the rejection reason
    expect(badge.attributes('title')).toContain('No seeds')
  })

  it('shows Smart total as the score badge when smartScore is present', async () => {
    const wrapper = mount(ManualSearchModal as any, { props: { isOpen: true, audiobook: null }, global: { stubs } })

    ;(wrapper.vm as any).results = [{
      id: 'r1',
      title: 'Smart Score Test',
      downloadType: 'Torrent',
      resultUrl: 'https://indexer/info/1',
      source: 'test',
      size: 0
    }]

    // Provide a quality score with a smartScore. Ensure both ref.value and unwrapped Map get the entry
    const scoreObj: any = {
      searchResult: (wrapper.vm as any).results[0],
      totalScore: 47,
      scoreBreakdown: { Quality: 65 },
      rejectionReasons: [],
      isRejected: false,
      smartScore: 12345,
      smartScoreBreakdown: { Quality: 65000 }
    }

    // Try to set via .value (ref) when available
    if ((wrapper.vm as any).qualityScores && (wrapper.vm as any).qualityScores.value && typeof (wrapper.vm as any).qualityScores.value.set === 'function') {
      (wrapper.vm as any).qualityScores.value.set('r1', scoreObj)
    }

    // Also set directly on the unwrapped proxy for compatibility with test runner behavior
    if ((wrapper.vm as any).qualityScores && typeof (wrapper.vm as any).qualityScores.set === 'function') {
      (wrapper.vm as any).qualityScores.set('r1', scoreObj)
    }

    // As a last-resort replace the Map entirely
    // Provide smartScoreBreakdown so the visible total is computed from component averages
    scoreObj.smartScore = 1234.5
    scoreObj.smartScoreBreakdown = { Quality: 90000, Format: 8500, Seed: 2000 }
    ;(wrapper.vm as any).qualityScores = new Map([[ 'r1', scoreObj ]])

    await nextTick()

    const badge = wrapper.find('.col-score .score-badge')
    expect(badge.exists()).toBe(true)
    // Normalized components: Quality=90, Format=85, Seed=20 -> avg ~65
    expect(badge.text()).toContain('65')
  })
})