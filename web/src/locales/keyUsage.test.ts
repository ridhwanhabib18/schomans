import { describe, expect, it } from 'vitest'
import { navItems } from '@/app/layouts/navigation'
import '@/features/dashboard/i18n'
import '@/features/grading/i18n'
import '@/features/reportcards/i18n'
import i18n from '@/shared/lib/i18n'

// A key that is missing, or that names a whole group instead of a text, shows the reader "key 'x' returned an object
// instead of string" in the page. This test reads every t() call of the Phase 4 features and the navigation labels and
// checks that each key leads to a text.

const sources = import.meta.glob('/src/features/{grading,reportcards,dashboard}/**/*.{ts,tsx}', {
  query: '?raw',
  import: 'default',
  eager: true,
}) as Record<string, string>

const files = Object.entries(sources).filter(([path]) => !/\.test\.tsx?$/.test(path))

const resource = (namespace: string, key: string): unknown => i18n.getResource('en', namespace, key)

/** A text, or the plural forms of one (`done_one` and `done_other` for `done`). */
const isText = (namespace: string, key: string) =>
  typeof resource(namespace, key) === 'string' || typeof resource(namespace, `${key}_other`) === 'string'

const isGroup = (namespace: string, prefix: string) => {
  const value = resource(namespace, prefix)
  return typeof value === 'object' && value !== null
}

interface Used {
  file: string
  namespace: string
  key: string
}

function keysOf(path: string, source: string): Used[] {
  const folder = /\/features\/(\w+)\//.exec(path)![1]!
  const own = /\{ t \} = useTranslation\('(\w+)'\)/.exec(source)?.[1] ?? folder
  const used: Used[] = []

  for (const match of source.matchAll(/\b(t|tCommon)\(\s*(['"`])([^'"`]+)\2/g)) {
    used.push({ file: path, namespace: match[1] === 'tCommon' ? 'common' : own, key: match[3]! })
  }
  return used
}

const usages = files.flatMap(([path, source]) => keysOf(path, source))

describe('translation keys of the Phase 4 features', () => {
  it('finds the t() calls it is meant to check', () => {
    expect(files.length).toBeGreaterThan(20)
    expect(usages.length).toBeGreaterThan(100)
  })

  it('every fixed key leads to a text', () => {
    const broken = usages
      .filter(({ key }) => !key.includes('${'))
      .filter(({ namespace, key }) => !isText(namespace, key))
      .map(({ file, namespace, key }) => `${file}: ${namespace}:${key}`)

    expect(broken).toEqual([])
  })

  it('every key built at run time starts at a group of texts', () => {
    const broken = usages
      .filter(({ key }) => key.includes('${'))
      .filter(({ namespace, key }) => !isGroup(namespace, key.slice(0, key.indexOf('${')).replace(/\.$/, '')))
      .map(({ file, namespace, key }) => `${file}: ${namespace}:${key}`)

    expect(broken).toEqual([])
  })

  it('every navigation label leads to a text', () => {
    expect(navItems.filter((item) => typeof resource('common', item.labelKey) !== 'string').map((item) => item.labelKey)).toEqual([])
  })
})
