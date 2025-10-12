import type { QualityScore } from '@/types'

export function getScoreBreakdownTooltip(score: QualityScore | undefined): string {
  if (!score) return 'Score not available'

  const breakdown = score.scoreBreakdown || {}
  const scoreMeta = score as unknown as { isRejected?: boolean; rejectionReasons?: string[] }
  // Treat a negative total score or explicit isRejected flag as a rejection.
  const isRejected = (typeof score.totalScore === 'number' && score.totalScore < 0) || Boolean(scoreMeta.isRejected)
  const parts: string[] = []

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
