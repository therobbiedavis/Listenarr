// Small helper to validate/sanitize redirect targets for login flow
export function isSafeRedirect(path: string | undefined | null): boolean {
  if (!path) return false
  // Must be a path that starts with a single slash (no protocol or host)
  if (!path.startsWith('/')) return false
  // Disallow network-path references that start with //
  if (path.startsWith('//')) return false
  // Disallow protocol-looking strings
  if (path.includes('://')) return false
  // Prevent CRLF injection
  if (/\r|\n/.test(path)) return false
  // Keep it short-ish to avoid overly long injection attempts
  if (path.length > 2000) return false
  return true
}

export function normalizeRedirect(path: string | undefined | null): string {
  return isSafeRedirect(path) ? (path as string) : '/'
}
