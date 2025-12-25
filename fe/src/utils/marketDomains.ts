// Region-aware domain and URL helpers for Amazon and Audible

const amazonDomainMap: Record<string, string> = {
  us: 'www.amazon.com',
  uk: 'www.amazon.co.uk',
  gb: 'www.amazon.co.uk',
  de: 'www.amazon.de',
  ca: 'www.amazon.ca',
  au: 'www.amazon.com.au',
  fr: 'www.amazon.fr',
  it: 'www.amazon.it',
  es: 'www.amazon.es',
  jp: 'www.amazon.co.jp'
}

const audibleDomainMap: Record<string, string> = {
  us: 'www.audible.com',
  uk: 'www.audible.co.uk',
  gb: 'www.audible.co.uk',
  de: 'www.audible.de',
  ca: 'www.audible.ca',
  au: 'www.audible.com.au',
  fr: 'www.audible.fr',
  it: 'www.audible.it',
  es: 'www.audible.es',
  jp: 'www.audible.co.jp'
}

export function getAmazonDomain(region?: string): string {
  const r = (region || 'us').toLowerCase()
  return amazonDomainMap[r] || amazonDomainMap['us'] || 'www.amazon.com'
}

export function getAudibleDomain(region?: string): string {
  const r = (region || 'us').toLowerCase()
  return audibleDomainMap[r] || audibleDomainMap['us'] || 'www.audible.com'
}

export function buildAmazonProductUrl(asin: string, region?: string): string {
  return `https://${getAmazonDomain(region)}/dp/${asin}`
}

export function buildAudibleProductUrl(asin: string, region?: string): string {
  return `https://${getAudibleDomain(region)}/pd/${asin}`
}

export function createAmazonSearchUrl(title: string, author?: string, region?: string): string {
  const base = `https://${getAmazonDomain(region)}/s`
  const params = new URLSearchParams()
  let q = title || ''
  if (author) q += ` ${author}`
  q += ' audiobook'
  params.set('k', q)
  params.set('i', 'audible')
  params.set('ref', 'sr_nr_i_0')
  return `${base}?${params.toString()}`
}

// Create stripbooks search URL that filters by ISBN using p_66
export function createAmazonIsbnSearchUrl(isbn: string, region?: string): string {
  const domain = getAmazonDomain(region)
  const encoded = encodeURIComponent(isbn)
  return `https://${domain}/s?i=stripbooks&rh=p_66%3A${encoded}&s=relevancerank&Adv-Srch-Books-Submit.x=20&Adv-Srch-Books-Submit.y=6&unfiltered=1`
}

export const _amazonDomainMap = amazonDomainMap
export const _audibleDomainMap = audibleDomainMap
