export type Rule = {
  type?: 'rule' | 'group'
  field?: string
  operator?: string
  value?: string
  conjunction?: 'and' | 'or'
  rules?: Rule[]
}

export type CustomFilter = { id?: string; label: string; rules: Rule[] }

function toLower(s: unknown) {
  return (s ?? '').toString().toLowerCase()
}

const numericFields = new Set(['publishYear', 'publishedYear', 'files', 'filesize'])

function evalSingleRule(rule: Rule, b: Record<string, unknown>): boolean {
  const field = rule.field || ''
  const op = rule.operator || 'contains'
  const val = (rule.value || '').toString()

  let left = ''
  switch (field) {
    case 'monitored':
      left = String(Boolean((b as Record<string, unknown>)['monitored']))
      break
    case 'title':
      left = String((b as Record<string, unknown>)['title'] ?? '')
      break
    case 'author': {
      const authors = ((b as Record<string, unknown>)['authors'] as unknown[]) || []
      left = authors.map((a) => String(a)).join(' ')
      break
    }
    case 'narrator': {
      const narrators = ((b as Record<string, unknown>)['narrators'] as unknown[]) || []
      left = narrators.map((n) => String(n)).join(' ')
      break
    }
    case 'language':
      left = String((b as Record<string, unknown>)['language'] ?? '')
      break
    case 'publisher':
      left = String((b as Record<string, unknown>)['publisher'] ?? '')
      break
    case 'qualityProfileId':
      left = String((b as Record<string, unknown>)['qualityProfileId'] ?? '')
      break
    case 'publishYear':
      left = String((b as Record<string, unknown>)['publishYear'] ?? '')
      break
    case 'publishedYear':
      left = String((b as Record<string, unknown>)['publishYear'] ?? '')
      break
    case 'path':
      left = String(
        (b as Record<string, unknown>)['filePath'] ?? (b as Record<string, unknown>)['path'] ?? '',
      )
      break
    case 'files': {
      const files = ((b as Record<string, unknown>)['files'] as unknown[]) || []
      left = String(files.length)
      break
    }
    case 'filesize':
      left = String((b as Record<string, unknown>)['fileSize'] ?? '')
      break
    default:
      left = String((b as Record<string, unknown>)[field] ?? '')
      break
  }

  const l = toLower(left)
  const v = toLower(val)

  if (numericFields.has(field)) {
    const leftNum = Number(left)
    const valNum = Number(val)
    if (isNaN(leftNum) || isNaN(valNum)) return false
    switch (op) {
      case 'eq':
        return leftNum === valNum
      case 'ne':
        return leftNum !== valNum
      case 'lt':
        return leftNum < valNum
      case 'lte':
        return leftNum <= valNum
      case 'gt':
        return leftNum > valNum
      case 'gte':
        return leftNum >= valNum
      case 'is':
        return leftNum === valNum
      case 'is_not':
        return leftNum !== valNum
      default:
        return true
    }
  }

  switch (op) {
    case 'is':
      return left === val
    case 'is_not':
      return left !== val
    case 'contains':
      return l.includes(v)
    case 'not_contains':
      return !l.includes(v)
    default:
      return true
  }
}

export function evaluateNode(node: Rule, b: Record<string, unknown>): boolean {
  if (!node) return true
  if (node.type === 'group') {
    const rules = node.rules || []
    if (!rules.length) return true
    return rules.reduce((acc: boolean | null, r, idx) => {
      const res = evaluateNode(r, b)
      if (idx === 0) return res
      const conj = r.conjunction === 'or' ? 'or' : 'and'
      return conj === 'and' ? (acc as boolean) && res : (acc as boolean) || res
    }, null) as boolean
  }

  // plain rule
  return evalSingleRule(node, b)
}

export function matchesFilter(
  b: Record<string, unknown>,
  filter: CustomFilter | undefined | null,
): boolean {
  if (!filter || !filter.rules || filter.rules.length === 0) return true
  const rules = filter.rules
  return rules.reduce((acc: boolean | null, r, idx) => {
    const res = evaluateNode(r, b)
    if (idx === 0) return res
    const conj = r.conjunction === 'or' ? 'or' : 'and'
    return conj === 'and' ? (acc as boolean) && res : (acc as boolean) || res
  }, null) as boolean
}
