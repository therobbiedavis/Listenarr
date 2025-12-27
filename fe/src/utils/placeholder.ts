export function getPlaceholderUrl(): string {
  try {
    const base = (import.meta.env.BASE_URL || '/') as string
    const trimmed = base.endsWith('/') ? base : `${base}/`
    return `${trimmed}placeholder.svg`
  } catch {
    return '/placeholder.svg'
  }
}
