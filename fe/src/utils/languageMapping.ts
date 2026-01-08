// Language to region code mapping for Audible/Audimeta API
// Maps user-friendly language names to Audible market region codes
// Supported regions: us, ca, uk, au, fr, de, jp, it, in, es, br

export const languageToRegion: Record<string, string> = {
  english: 'us',
  'english-uk': 'uk',
  'english-ca': 'ca',
  'english-au': 'au',
  'english-in': 'in',
  german: 'de',
  french: 'fr',
  spanish: 'es',
  italian: 'it',
  portuguese: 'br',
  japanese: 'jp',
}

export const regionToLanguage: Record<string, string> = {
  us: 'english',
  uk: 'english-uk',
  gb: 'english-uk',
  ca: 'english-ca',
  au: 'english-au',
  in: 'english-in',
  de: 'german',
  fr: 'french',
  es: 'spanish',
  it: 'italian',
  br: 'portuguese',
  jp: 'japanese',
}

/**
 * Convert language name to region code
 * @param language - Language name (e.g., 'english', 'german')
 * @returns Region code (e.g., 'us', 'de') or 'us' as fallback
 */
export function getRegionFromLanguage(language: string): string {
  return languageToRegion[language.toLowerCase()] || 'us'
}

/**
 * Convert region code to language name
 * @param region - Region code (e.g., 'us', 'de')
 * @returns Language name (e.g., 'english', 'german') or 'english' as fallback
 */
export function getLanguageFromRegion(region: string): string {
  return regionToLanguage[region.toLowerCase()] || 'english'
}
