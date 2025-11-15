import { describe, it, expect } from 'vitest'
import evaluateRules from '@/utils/customFilterEvaluator'
import type { Audiobook } from '@/types'

describe('customFilterEvaluator - grouping and precedence', () => {
  const base: Audiobook = {
    id: 1,
    title: 'Alpha Tales',
    authors: ['John Smith'],
    narrators: [],
    monitored: true,
    language: 'en',
    publisher: '',
    qualityProfileId: 0,
    publishYear: '2020',
    files: [],
    filePath: '',
    fileSize: 0
  } as unknown as Audiobook

  it('evaluates simple AND/OR grouping: (A OR B) AND C', () => {
    const rules = [
      { field: 'title', operator: 'contains', value: 'alpha', groupStart: true },
      { field: 'title', operator: 'contains', value: 'beta', conjunction: 'or', groupEnd: true },
      { field: 'author', operator: 'contains', value: 'smith', conjunction: 'and' }
    ]

    // base has title Alpha and author Smith -> (true OR false) AND true => true
    expect(evaluateRules(base, rules)).toBe(true)

    // change base title so first two rules false
    const b2 = { ...base, title: 'Gamma' }
    expect(evaluateRules(b2 as Audiobook, rules)).toBe(false)
  })

  it('respects operator precedence (AND before OR) without parentheses', () => {
    // A OR B AND C should evaluate as A OR (B AND C)
    const rules = [
      { field: 'title', operator: 'contains', value: 'alpha' },
      { field: 'title', operator: 'contains', value: 'beta', conjunction: 'or' },
      { field: 'author', operator: 'contains', value: 'smith', conjunction: 'and' }
    ]

    // base: title contains alpha, so true OR (false AND true) => true
    expect(evaluateRules(base, rules)).toBe(true)

    // b3: title doesn't contain alpha, but contains beta and author smith -> false OR (true AND true) => true
    const b3 = { ...base, title: 'The Beta Story' }
    expect(evaluateRules(b3 as Audiobook, rules)).toBe(true)

    // b4: none match
    const b4 = { ...base, title: 'Gamma', authors: ['No One'] }
    expect(evaluateRules(b4 as Audiobook, rules)).toBe(false)
  })
})
