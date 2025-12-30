import type { QualityScore } from '@/types'

// Exported helper: compute normalized components and normalized total from a smart breakdown
export function computeNormalizedSmart(breakdown?: Record<string, number>): { components: Record<string, number>, total: number } {
  const comp: Record<string, number> = {}
  if (!breakdown) return { components: comp, total: 0 }

  const normalizeComponent = (key: string, raw: number): number => {
    const k = key.toLowerCase()
    if (k === 'quality') return Math.round(raw / 1000)
    if (k === 'format') return Math.round(raw / 100)
    if (k === 'indexer') return Math.round(raw / 500)
    if (k === 'seed' || k === 'seeders' || k === 'seeds') return Math.round(raw / 100)
    if (k === 'age') return Math.round(raw / 10)
    if (k === 'size') return Math.round(raw)
    return Math.round(raw / 1000)
  }

  Object.keys(breakdown).forEach(k => {
    comp[k] = normalizeComponent(k, breakdown[k] ?? 0)
  })

  const keys = Object.keys(comp)
  const total = keys.length > 0 ? Math.round(Object.values(comp).reduce((s, v) => s + v, 0) / keys.length) : 0
  return { components: comp, total }
}

export function getScoreBreakdownTooltip(score: QualityScore | undefined): string {
  if (!score) return 'Score not available'

  const scoreMeta = score as unknown as { isRejected?: boolean; rejectionReasons?: string[] }
  // Treat a negative total score or explicit isRejected flag as a rejection.
  const isRejected = (typeof score.totalScore === 'number' && score.totalScore < 0) || Boolean(scoreMeta.isRejected)
  const parts: string[] = []

  // If this is a backend rejection, show only the rejection reason for clarity
  if (isRejected) {
    const reasons = scoreMeta.rejectionReasons
    if (reasons && Array.isArray(reasons) && reasons.length > 0) {
      return `Rejected: ${reasons.join('; ')}`
    }
    return 'Rejected'
  }

  // If a Prowlarr-style smart composite was provided by the backend, show only that breakdown
  const smart = score as unknown as { smartScore?: number; smartScoreBreakdown?: Record<string, number> }
  if (typeof smart.smartScore === 'number' && smart.smartScore > 0) {
    parts.push('Smart (composite) breakdown:')
    const sb = smart.smartScoreBreakdown || {}

    // Use shared helper to compute normalized components and total
    const { components, total } = computeNormalizedSmart(sb)

    Object.keys(components).forEach(k => {
      const v = components[k]
      const sign = v > 0 ? '+' : ''
      parts.push(`${k}: ${sign}${v}`)
    })

    // Math summary: show how the total was calculated from normalized components
    // Smart Total: average of normalized components
    parts.push(`Smart Total: ${total}`)

    // If this score is a backend rejection (sentinel), include rejection reasons as well
    if (isRejected) {
      parts.push('')
      parts.push('Status: Rejected')
      const reasons = scoreMeta.rejectionReasons
      if (reasons && Array.isArray(reasons) && reasons.length > 0) {
        parts.push(`Rejection Reasons: ${reasons.join('; ')}`)
      }
    }

    return parts.join('\n')
  }

  // Backwards compatible behavior when no smart composite is available: show legacy breakdown
  const breakdown = score.scoreBreakdown || {}

  // List each contribution as a signed per-line value, then put total at the bottom.
  const detailOrder = ['Quality', 'Seeders', 'PreferredWords', 'Format', 'Language', 'Age', 'Size']
  // Build contributions from all keys except Quality first (we'll derive Quality so lines sum to reported total).
  const contributions: { key: string; value: number }[] = []

  const pushNonQuality = (key: string) => {
    if (key === 'Quality') return
    const v = (breakdown as Record<string, number | undefined>)[key]
    if (v === undefined) return
    contributions.push({ key, value: v ?? 0 })
  }

  // Add keys in preferred order (non-quality)
  detailOrder.forEach(pushNonQuality)
  // Add any other keys present in the breakdown that weren't added yet
  // Include all numeric keys (including diagnostic keys) so the popover shows every
  // parameter that affected the score. Quality is handled separately.
  Object.keys(breakdown).forEach(k => {
    if (k === 'Quality') return
    if (detailOrder.includes(k)) return
    pushNonQuality(k)
  })

  // Compute sum of non-quality contributions
  const nonQualitySum = contributions.reduce((s, c) => s + c.value, 0)

  // Determine quality raw value (if provided)
  const qualityRaw = (breakdown as Record<string, number | undefined>)['Quality']

  // Compute quality contribution:
  // If backend provided a raw quality score, the contribution is (raw - 100) (e.g., 90 => -10).
  // Otherwise, derive it so that 100 + nonQualitySum + qualityContribution = reported total.
  let qualityContribution: number
  if (qualityRaw !== undefined) {
    // Use the raw quality contribution (raw - 100) directly. Do not apply any correction.
    qualityContribution = (qualityRaw ?? 0) - 100
  } else {
    // Derive quality contribution so that the equation holds when raw isn't provided.
    qualityContribution = score.totalScore - 100 - nonQualitySum
  }

  // Render a base starting value then all contributions (non-quality) and finally Quality.
  parts.push('Base: 100')

  // Render other contribution lines (include diagnostic keys)
  contributions.forEach(c => {
    const sign = c.value > 0 ? '+' : ''
    parts.push(`${c.key}: ${sign}${c.value}`)
  })

  // Render Quality contribution as (raw - 100) when available, otherwise as derived.
  const qualitySign = qualityContribution > 0 ? '+' : ''
  parts.push(`Quality: ${qualitySign}${qualityContribution}`)

  // Final line: backend reported total score
  // Compute the displayed total from Base + contributions + Quality so the popover math
  // reflects all parameters (diagnostic keys included).
  const computedTotal = 100 + nonQualitySum + qualityContribution
  parts.push(`Computed Total: ${computedTotal}`)
  // If backend reported a different total, show it for transparency
  if (computedTotal !== score.totalScore) {
    parts.push(`Backend Total: ${score.totalScore}`)
  }

  // If this score is a backend rejection (sentinel), make it explicit at the end
  if (isRejected) {
    parts.push('Status: Rejected')
    // Also include any rejection reasons if present
    const reasons = scoreMeta.rejectionReasons
    if (reasons && Array.isArray(reasons) && reasons.length > 0) {
      parts.push(`Rejection Reasons: ${reasons.join('; ')}`)
    }
  }

  return parts.join('\n')
}
